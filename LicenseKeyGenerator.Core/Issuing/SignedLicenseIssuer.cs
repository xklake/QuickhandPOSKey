using Newtonsoft.Json.Linq;
using Quickhand.Licensing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LicenseGenerator
{
    // Vendor-only code. This assembly must never be part of a customer installation.
    public static class SignedLicenseIssuer
    {
        public static void CreateKey(string pfxPath, string password)
        {
            RequirePrivateLocation(pfxPath);
            if (string.IsNullOrWhiteSpace(password) || password.Length < 16)
                throw new InvalidDataException("Use a signing-key password of at least 16 characters.");
            using (var rsa = new RSACng(3072))
            {
                var request = new CertificateRequest("CN=Quickhand Offline License Signing", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                using (var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddYears(20)))
                {
                    var publicKey = new JObject { ["keyId"] = SignedLicenseVerifier.KeyId(rsa), ["publicKeyXml"] = rsa.ToXmlString(false) };
                    string publicPath = pfxPath + ".public.json";
                    if (File.Exists(pfxPath) || File.Exists(publicPath)) throw new IOException("The key or public-key export already exists. Choose a new filename.");
                    WriteNew(pfxPath, certificate.Export(X509ContentType.Pfx, password));
                    WriteNew(publicPath, LicenseJson.Utf8.GetBytes(LicenseJson.Compact(publicKey)));
                }
            }
        }

        public static JObject ReadRequest(string json)
        {
            var request = LicenseJson.Read(json);
            LicenseJson.Fields(request, "schemaVersion", "product", "requestId", "installationId", "deviceBinding", "requestedAt", "currentLicenseId", "currentRevision");
            LicenseJson.Number(request, "schemaVersion", 2, 2);
            if (LicenseJson.Text(request, "product") != "QuickhandPOS") throw new InvalidDataException("The request is for another product.");
            RequireId(LicenseJson.Text(request, "requestId", 36));
            string installation = LicenseJson.Text(request, "installationId", 36);
            if (installation.Length != 36 || !installation.StartsWith("QH2-", StringComparison.Ordinal) ||
                !LicenseJson.IsHash(LicenseJson.Text(request, "deviceBinding", 64))) throw new InvalidDataException("Invalid installation request.");
            foreach (char c in installation.Substring(4)) if (!Uri.IsHexDigit(c)) throw new InvalidDataException("Invalid installation code.");
            LicenseJson.Time(request, "requestedAt");
            string id = LicenseJson.Text(request, "currentLicenseId", 36, true);
            int revision = LicenseJson.Number(request, "currentRevision", 0, int.MaxValue - 1);
            if (id.Length > 0) RequireId(id);
            if ((id.Length == 0) != (revision == 0)) throw new InvalidDataException("Invalid renewal request.");
            return request;
        }

        public static JObject NewPayload(JObject request, string customer, string store, DateTime issuedUtc)
        {
            ReadRequest(LicenseJson.Compact(request));
            string currentId = (string)request["currentLicenseId"];
            return new JObject {
                ["schemaVersion"] = 2, ["keyId"] = new string('0', 64),
                ["licenseId"] = string.IsNullOrEmpty(currentId) ? Guid.NewGuid().ToString("D") : currentId,
                ["revision"] = (int)request["currentRevision"] + 1, ["customerId"] = customer, ["storeId"] = store,
                ["installationId"] = (string)request["installationId"], ["deviceBinding"] = (string)request["deviceBinding"],
                ["product"] = "QuickhandPOS", ["edition"] = "standard", ["issuedAt"] = LicenseJson.TimeText(issuedUtc), ["notBefore"] = LicenseJson.TimeText(issuedUtc),
                ["pos"] = Feature(true, true, null), ["app"] = Feature(false, false, null), ["web"] = Feature(false, false, null), ["payment"] = Feature(false, false, null),
                ["appFullAccess"] = false, ["appDeviceId"] = "", ["paymentTerminals"] = 0, ["replacesLicenseId"] = "", ["changeReason"] = ""
            };
        }

        public static JObject Feature(bool enabled, bool permanent, DateTime? expiryUtc)
        { return new JObject { ["enabled"] = enabled, ["permanent"] = enabled && permanent, ["expiresAt"] = enabled && !permanent && expiryUtc.HasValue ? (JToken)LicenseJson.TimeText(expiryUtc.Value) : JValue.CreateNull() }; }

        public static string Sign(JObject draft, string pfxPath, string password)
        {
            RequirePrivateLocation(pfxPath);
            var info = new FileInfo(pfxPath);
            if (!info.Exists || info.Length > 65536) throw new InvalidDataException("Signing key is missing or too large.");
            using (var certificate = new X509Certificate2(File.ReadAllBytes(pfxPath), password, X509KeyStorageFlags.EphemeralKeySet))
            using (var rsa = certificate.GetRSAPrivateKey())
            {
                if (rsa == null || rsa.KeySize != 3072) throw new InvalidDataException("An RSA-3072 private signing key is required.");
                var payload = (JObject)draft.DeepClone();
                payload["keyId"] = SignedLicenseVerifier.KeyId(rsa);
                var grant = SignedLicenseVerifier.ReadPayload(payload);
                if (!grant.Pos.Enabled) throw new InvalidDataException("A POS license must enable POS.");
                byte[] bytes = LicenseJson.Utf8.GetBytes(LicenseJson.Compact(payload));
                string envelope = LicenseJson.Compact(new JObject { ["schemaVersion"] = 2, ["keyId"] = grant.KeyId,
                    ["payload"] = Convert.ToBase64String(bytes), ["signature"] = Convert.ToBase64String(rsa.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)) });
                new SignedLicenseVerifier(new Dictionary<string, string> { [grant.KeyId] = rsa.ToXmlString(false) }).Verify(envelope);
                return envelope;
            }
        }

        public static void IssueToFile(JObject draft, string pfxPath, string password, string outputPath, string operatorName, bool recordsConfirmed)
        {
            if (!recordsConfirmed || string.IsNullOrWhiteSpace(operatorName)) throw new InvalidDataException("Confirm the customer's purchased rights and enter the issuing operator.");
            LicenseJson.Text(new JObject { ["operator"] = operatorName }, "operator", 160);
            RequirePrivateLocation(outputPath);
            string content = Sign(draft, pfxPath, password);
            // Serialize issuance and audit on the vendor workstation. Reserve each ID/revision
            // in the append-only journal BEFORE publishing; a crash burns that revision safely.
            string journal = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(pfxPath)), "license-issuance.jsonl");
            using (var file = new FileStream(journal, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                string id = (string)draft["licenseId"]; int revision = (int)draft["revision"];
                using (var reader = new StreamReader(file, LicenseJson.Utf8, false, 4096, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var previous = LicenseJson.Read(line);
                        if ((string)previous["licenseId"] == id && (int)previous["revision"] >= revision)
                            throw new InvalidDataException("This revision has already been issued or reserved. Increase the revision; never reuse it.");
                        if ((string)previous["licenseId"] == id && (string)previous["deviceBinding"] != (string)draft["deviceBinding"] && string.IsNullOrWhiteSpace((string)draft["changeReason"]))
                            throw new InvalidDataException("Rebinding a license to another computer requires a reason.");
                    }
                }
                if (File.Exists(outputPath)) throw new IOException("The output exists. Choose a new filename.");
                var entry = new JObject { ["licenseId"] = id, ["revision"] = revision, ["customerId"] = (string)draft["customerId"], ["storeId"] = (string)draft["storeId"],
                    ["deviceBinding"] = (string)draft["deviceBinding"], ["operator"] = operatorName, ["time"] = LicenseJson.TimeText(DateTime.UtcNow),
                    ["reason"] = (string)draft["changeReason"], ["replacesLicenseId"] = (string)draft["replacesLicenseId"], ["fileHash"] = LicenseIdentity.Hash(content) };
                file.Seek(0, SeekOrigin.End);
                byte[] audit = LicenseJson.Utf8.GetBytes(LicenseJson.Compact(entry) + Environment.NewLine);
                file.Write(audit, 0, audit.Length); file.Flush(true);
                WriteNew(outputPath, LicenseJson.Utf8.GetBytes(content));
            }
        }

        public static void RequirePrivateLocation(string path)
        {
            var directory = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path)));
            for (var current = directory; current != null; current = current.Parent)
                if (File.Exists(Path.Combine(current.FullName, ".git")) || Directory.Exists(Path.Combine(current.FullName, ".git")))
                    throw new InvalidDataException("Store private keys, issued licenses and the vendor journal outside all Git workspaces.");
        }

        private static void WriteNew(string path, byte[] bytes)
        {
            // CreateNew never overwrites a key or an already issued customer license.
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
        }
        private static void RequireId(string id)
        { Guid parsed; if (!Guid.TryParseExact(id, "D", out parsed) || parsed == Guid.Empty) throw new InvalidDataException("Invalid request/license identifier."); }
    }
}
