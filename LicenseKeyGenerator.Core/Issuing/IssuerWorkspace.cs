using Newtonsoft.Json.Linq;
using Quickhand.Licensing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace LicenseGenerator
{
    // Vendor workstation only. No secrets are saved in the profile or client source.
    public static class IssuerWorkspace
    {
        public static string ProfilePath => new LicenseStorage(LicenseStorage.DefaultRoot).ProfilePath;

        public static JObject ReadPublicKey(string pfxPath, string password)
        {
            if (!string.Equals(Path.GetExtension(pfxPath), ".pfx", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Select a signing key (.pfx). Customer requests (.json) belong in Load request.");
            var file = new FileInfo(pfxPath);
            if (!file.Exists || file.Length > 65536) throw new InvalidDataException("The signing key is missing or too large.");
            using (var certificate = new X509Certificate2(File.ReadAllBytes(pfxPath), password, X509KeyStorageFlags.EphemeralKeySet))
            using (var rsa = certificate.GetRSAPrivateKey())
            {
                if (rsa == null || rsa.KeySize != 3072) throw new InvalidDataException("Select a Quickhand RSA-3072 license signing key.");
                return new JObject { ["keyId"] = SignedLicenseVerifier.KeyId(rsa), ["publicKeyXml"] = rsa.ToXmlString(false) };
            }
        }

        public static string ImportKey(string source, string destinationFolder, string password)
        {
            var publicKey = ReadPublicKey(source, password);
            string target = Path.Combine(Path.GetFullPath(destinationFolder), "license-signing.pfx");
            SignedLicenseIssuer.RequirePrivateLocation(target);
            if (string.Equals(Path.GetFullPath(source), target, StringComparison.OrdinalIgnoreCase)) return target;
            // A separate empty folder prevents merging two independent issuance histories.
            if (Directory.Exists(destinationFolder) && Directory.GetFileSystemEntries(destinationFolder).Length != 0)
                throw new IOException("Choose an empty private folder. Existing keys and issuance records will not be overwritten.");
            Directory.CreateDirectory(destinationFolder);
            string journal = LicenseStorage.JournalPath(source);
            // Lock the original journal while copying, to retain all reserved revisions.
            using (var audit = File.Exists(journal) ? new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.None) : null)
            {
                File.Copy(source, target, false);
                File.WriteAllText(target + ".public.json", LicenseJson.Compact(publicKey), LicenseJson.Utf8);
                if (audit != null)
                    using (var copy = new FileStream(Path.Combine(destinationFolder, "license-issuance.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    { audit.CopyTo(copy); copy.Flush(true); }
            }
            return target;
        }

        public static string FindSourceRoot(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return "";
            for (var dir = new DirectoryInfo(path); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "SmartPOS", "QuickhandPOS.csproj")) &&
                    File.Exists(Path.Combine(dir.FullName, "Seals", "Seals.csproj"))) return dir.FullName;
            return "";
        }

        public static string ConfigureClientTrust(string sourceRoot, JObject publicKey)
        {
            if (FindSourceRoot(sourceRoot) != Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar))
                throw new InvalidDataException("Select the Quickhand POS source folder containing SmartPOS and Seals.");
            LicenseJson.Fields(publicKey, "keyId", "publicKeyXml");
            string id = LicenseJson.Text(publicKey, "keyId", 64), xml = LicenseJson.Text(publicKey, "publicKeyXml", 4096);
            using (var rsa = SignedLicenseVerifier.PublicKey(xml))
                if (SignedLicenseVerifier.KeyId(rsa) != id) throw new InvalidDataException("Public key identifier mismatch.");
            string path = Path.Combine(sourceRoot, "Seals", "Licensing", "TrustedLicenseKeys.json");
            using (var mutex = new System.Threading.Mutex(false, "Local\\Quickhand-LicenseTrust-" + LicenseIdentity.Hash(path.ToUpperInvariant())))
            {
                bool locked = false;
                try
                {
                    try { locked = mutex.WaitOne(TimeSpan.FromSeconds(5)); } catch (System.Threading.AbandonedMutexException) { locked = true; }
                    if (!locked) throw new IOException("Another setup is updating the public keys. Retry shortly.");
                    string original = File.ReadAllText(path, LicenseJson.Utf8);
                    var trust = LicenseJson.Read(original); LicenseJson.Fields(trust, "keys");
                    var keys = trust["keys"] as JArray ?? throw new InvalidDataException("Invalid client public-key list.");
                    var verified = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (JObject entry in keys)
                    {
                        LicenseJson.Fields(entry, "keyId", "publicKeyXml");
                        verified.Add(LicenseJson.Text(entry, "keyId", 64), LicenseJson.Text(entry, "publicKeyXml", 4096));
                    }
                    new SignedLicenseVerifier(verified);
                    if (verified.ContainsKey(id)) return id;
                    if (keys.Count >= 16) throw new InvalidDataException("Review existing public keys before adding more.");
                    keys.Add(publicKey.DeepClone());
                    string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(temp, trust.ToString(), LicenseJson.Utf8);
                        if (File.ReadAllText(path, LicenseJson.Utf8) != original) throw new IOException("Client keys changed during setup. Retry.");
                        File.Replace(temp, path, null);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                    return id;
                }
                finally { if (locked) mutex.ReleaseMutex(); }
            }
        }

        public static JObject LoadProfile(string path)
        {
            if (!File.Exists(path)) return new JObject();
            if (new FileInfo(path).Length > 16384) throw new InvalidDataException("Issuer preferences are too large.");
            var profile = LicenseJson.Read(File.ReadAllText(path, LicenseJson.Utf8));
            LicenseJson.Fields(profile, "keyPath", "sourceRoot", "outputFolder");
            return profile;
        }

        public static void SaveProfile(string path, string keyPath, string sourceRoot, string outputFolder)
        {
            SignedLicenseIssuer.RequirePrivateLocation(path);
            if (!string.IsNullOrEmpty(keyPath)) SignedLicenseIssuer.RequirePrivateLocation(keyPath);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, new JObject { ["keyPath"] = keyPath, ["sourceRoot"] = sourceRoot, ["outputFolder"] = outputFolder }.ToString(), LicenseJson.Utf8);
                if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        public static LicensePartyIds FindPartyIds(string keyPath, string licenseId)
        {
            if (string.IsNullOrWhiteSpace(keyPath) || string.IsNullOrWhiteSpace(licenseId)) return null;
            string journal = LicenseStorage.JournalPath(keyPath);
            if (!File.Exists(journal)) return null;
            LicensePartyIds result = null;
            int latest = 0;
            using (var file = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new StreamReader(file, LicenseJson.Utf8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    var entry = LicenseJson.Read(line);
                    if (!string.Equals((string)entry["licenseId"], licenseId, StringComparison.OrdinalIgnoreCase)) continue;
                    int entryRevision = LicenseJson.Number(entry, "revision", 1, int.MaxValue);
                    if (entryRevision < latest) continue;
                    var candidate = new LicensePartyIds(LicenseJson.Text(entry, "customerId"), LicenseJson.Text(entry, "storeId"));
                    if (entryRevision == latest && result != null &&
                        (candidate.CustomerId != result.CustomerId || candidate.StoreId != result.StoreId))
                        throw new InvalidDataException("Conflicting customer/store IDs in the issuance history. Check your records before renewing.");
                    result = candidate; latest = entryRevision;
                }
            }
            return result;
        }

        public static int NextRevision(string keyPath, string licenseId, int requestedRevision)
        {
            int next = requestedRevision;
            string journal = LicenseStorage.JournalPath(keyPath);
            if (File.Exists(journal))
                using (var file = new FileStream(journal, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(file, LicenseJson.Utf8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var entry = LicenseJson.Read(line);
                        if ((string)entry["licenseId"] == licenseId) next = Math.Max(next, checked((int)entry["revision"] + 1));
                    }
                }
            return next;
        }
    }
}
