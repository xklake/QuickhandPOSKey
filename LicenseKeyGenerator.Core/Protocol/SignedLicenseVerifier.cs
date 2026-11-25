using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Xml;

namespace Quickhand.Licensing
{
    public sealed class LicenseFeature
    {
        public bool Enabled { get; internal set; }
        public bool Permanent { get; internal set; }
        public DateTime? ExpiresAtUtc { get; internal set; }
        public bool IsActive(DateTime utcNow) { return Enabled && (Permanent || utcNow < ExpiresAtUtc.Value); }
        public DateTime LegacyLocalExpiry => Permanent ? DateTime.MaxValue : (ExpiresAtUtc?.ToLocalTime() ?? DateTime.MinValue);
    }

    public sealed class SignedLicenseGrant
    {
        public string LicenseId { get; internal set; }
        public string KeyId { get; internal set; }
        public int Revision { get; internal set; }
        public string CustomerId { get; internal set; }
        public string StoreId { get; internal set; }
        public string InstallationId { get; internal set; }
        public string DeviceBinding { get; internal set; }
        public string Edition { get; internal set; }
        public string ReplacesLicenseId { get; internal set; }
        public string ChangeReason { get; internal set; }
        public DateTime IssuedAtUtc { get; internal set; }
        public DateTime NotBeforeUtc { get; internal set; }
        public LicenseFeature Pos { get; internal set; }
        public LicenseFeature App { get; internal set; }
        public LicenseFeature Web { get; internal set; }
        public LicenseFeature Payment { get; internal set; }
        public bool AppFullAccess { get; internal set; }
        public string AppDeviceId { get; internal set; }
        public int PaymentTerminals { get; internal set; }
        public string PayloadHash { get; internal set; }
    }

    public sealed class SignedLicenseVerifier
    {
        private readonly Dictionary<string, string> keys;
        public int TrustedKeyCount => keys.Count;

        public SignedLicenseVerifier(IDictionary<string, string> trustedPublicKeys)
        {
            keys = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var item in trustedPublicKeys)
            {
                using (var rsa = PublicKey(item.Value))
                    if (KeyId(rsa) != item.Key) throw new InvalidDataException("The public key identifier does not match its key.");
                keys.Add(item.Key, item.Value);
            }
        }

        

        public SignedLicenseGrant Verify(string envelopeText)
        {
            var envelope = LicenseJson.Read(envelopeText);
            LicenseJson.Fields(envelope, "schemaVersion", "keyId", "payload", "signature");
            LicenseJson.Number(envelope, "schemaVersion", 2, 2);
            string keyId = LicenseJson.Text(envelope, "keyId", 64);
            string publicKey;
            if (!keys.TryGetValue(keyId, out publicKey)) throw new InvalidDataException(keys.Count == 0
                ? "This build has no vendor signing key. Install the vendor's update before importing a signed license."
                : "This license uses an unknown signing key. Install the vendor's update or contact Quickhand POS.");
            byte[] payload, signature;
            try
            {
                payload = Convert.FromBase64String(LicenseJson.Text(envelope, "payload", 48000));
                signature = Convert.FromBase64String(LicenseJson.Text(envelope, "signature", 1024));
            }
            catch (FormatException ex) { throw new InvalidDataException("Invalid license encoding.", ex); }
            using (var rsa = PublicKey(publicKey))
            {
                if (signature.Length != rsa.KeySize / 8 || !rsa.VerifyData(payload, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                    throw new InvalidDataException("The license signature is invalid. The file may have been changed.");
            }
            // Verify exactly the original UTF-8 bytes before interpreting permissions.
            var grant = ReadPayload(LicenseJson.Read(LicenseJson.Utf8.GetString(payload)));
            if (grant.KeyId != keyId) throw new InvalidDataException("The license signing key identifiers do not match.");
            grant.PayloadHash = LicenseIdentity.Hash(Convert.ToBase64String(payload));
            return grant;
        }

        public static SignedLicenseGrant ReadPayload(JObject data)
        {
            LicenseJson.Fields(data, "schemaVersion", "keyId", "licenseId", "revision", "customerId", "storeId", "installationId", "deviceBinding",
                "product", "edition", "issuedAt", "notBefore", "pos", "app", "web", "payment", "appFullAccess", "appDeviceId", "paymentTerminals", "replacesLicenseId", "changeReason");
            LicenseJson.Number(data, "schemaVersion", 2, 2);
            if (LicenseJson.Text(data, "product") != "QuickhandPOS") throw new InvalidDataException("This license is for another product.");
            var grant = new SignedLicenseGrant {
                KeyId = LicenseJson.Text(data, "keyId", 64), LicenseId = Id(data, "licenseId"), Revision = LicenseJson.Number(data, "revision", 1, int.MaxValue),
                CustomerId = LicenseJson.Text(data, "customerId"), StoreId = LicenseJson.Text(data, "storeId"),
                InstallationId = LicenseJson.Text(data, "installationId", 36), DeviceBinding = LicenseJson.Text(data, "deviceBinding", 64),
                Edition = LicenseJson.Text(data, "edition", 20), IssuedAtUtc = LicenseJson.Time(data, "issuedAt"), NotBeforeUtc = LicenseJson.Time(data, "notBefore"),
                Pos = Feature(data["pos"] as JObject), App = Feature(data["app"] as JObject), Web = Feature(data["web"] as JObject), Payment = Feature(data["payment"] as JObject),
                AppFullAccess = LicenseJson.Flag(data, "appFullAccess"), AppDeviceId = LicenseJson.Text(data, "appDeviceId", 160, true),
                PaymentTerminals = LicenseJson.Number(data, "paymentTerminals", 0, 9999), ReplacesLicenseId = LicenseJson.Text(data, "replacesLicenseId", 36, true),
                ChangeReason = LicenseJson.Text(data, "changeReason", 300, true)
            };
            if (!LicenseJson.IsHash(grant.KeyId) || !LicenseJson.IsHash(grant.DeviceBinding) ||
                grant.InstallationId.Length != 36 || !grant.InstallationId.StartsWith("QH2-", StringComparison.Ordinal) || !grant.InstallationId.Substring(4).All(Uri.IsHexDigit) ||
                (grant.Edition != "standard" && grant.Edition != "trial") || (grant.IssuedAtUtc - grant.NotBeforeUtc).TotalMinutes > 5 ||
                (grant.Edition == "trial" && grant.Pos.Permanent)) throw new InvalidDataException("The license identity, edition or dates are invalid.");
            if (grant.ReplacesLicenseId.Length > 0) { Id(data, "replacesLicenseId"); if (string.IsNullOrWhiteSpace(grant.ChangeReason)) throw new InvalidDataException("A replacement license requires a reason."); }
            foreach (var feature in new[] { grant.Pos, grant.App, grant.Web, grant.Payment })
                if (feature.Enabled && !feature.Permanent && feature.ExpiresAtUtc <= grant.NotBeforeUtc) throw new InvalidDataException("A feature expires before the license becomes valid.");
            if (grant.Payment.Enabled && grant.PaymentTerminals == 0) throw new InvalidDataException("Payment requires at least one licensed terminal.");
            if (!grant.Payment.Enabled && grant.PaymentTerminals != 0) throw new InvalidDataException("Disabled payment must have zero terminals.");
            if (!grant.App.Enabled && (grant.AppFullAccess || grant.AppDeviceId.Length > 0)) throw new InvalidDataException("Disabled APP access cannot carry APP permissions.");
            return grant;
        }

        public static void ValidateUse(SignedLicenseGrant grant, LicenseIdentity identity, DateTime utcNow)
        {
            if (grant.InstallationId != identity.InstallationId || grant.DeviceBinding != identity.DeviceBinding)
                throw new InvalidDataException("This license belongs to a different computer. Export a request for an approved rebind.");
            if ((grant.IssuedAtUtc - utcNow).TotalMinutes > 5 || grant.NotBeforeUtc > utcNow)
                throw new InvalidDataException("The license is not active yet. Check the computer date and time.");
            if (!grant.Pos.IsActive(utcNow)) throw new InvalidDataException("The POS license has expired or is not enabled. Import a renewal from Quickhand POS.");
        }

        public static string KeyId(RSA rsa)
        {
            var key = rsa.ExportParameters(false);
            return LicenseIdentity.Hash(Convert.ToBase64String(key.Modulus) + ":" + Convert.ToBase64String(key.Exponent));
        }

        public static RSA PublicKey(string xml)
        {
            var document = new XmlDocument { XmlResolver = null };
            using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4096 })) document.Load(reader);
            if (document.DocumentElement?.Name != "RSAKeyValue" || document.DocumentElement.ChildNodes.Count != 2 ||
                document.SelectNodes("/RSAKeyValue/Modulus").Count != 1 || document.SelectNodes("/RSAKeyValue/Exponent").Count != 1)
                throw new InvalidDataException("Only an RSA public key may be trusted by a customer application.");
            var rsa = RSA.Create();
            try
            {
                rsa.FromXmlString(xml);
                if (rsa.KeySize != 3072 || Convert.ToBase64String(rsa.ExportParameters(false).Exponent) != "AQAB")
                    throw new InvalidDataException("License signing requires RSA-3072 with the standard public exponent.");
                return rsa;
            }
            catch { rsa.Dispose(); throw; }
        }

        private static string Id(JObject value, string name)
        {
            string text = LicenseJson.Text(value, name, 36); Guid parsed;
            if (!Guid.TryParseExact(text, "D", out parsed) || parsed == Guid.Empty) throw new InvalidDataException("Invalid license identifier: " + name);
            return text;
        }

        private static LicenseFeature Feature(JObject data)
        {
            LicenseJson.Fields(data, "enabled", "permanent", "expiresAt");
            var feature = new LicenseFeature { Enabled = LicenseJson.Flag(data, "enabled"), Permanent = LicenseJson.Flag(data, "permanent") };
            if (data["expiresAt"].Type != JTokenType.Null) feature.ExpiresAtUtc = LicenseJson.Time(data, "expiresAt");
            if ((feature.Permanent && feature.ExpiresAtUtc.HasValue) || (feature.Enabled && !feature.Permanent && !feature.ExpiresAtUtc.HasValue) ||
                (!feature.Enabled && (feature.Permanent || feature.ExpiresAtUtc.HasValue))) throw new InvalidDataException("A license feature has contradictory expiry settings.");
            return feature;
        }
    }
}
