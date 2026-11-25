using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml;
namespace Quickhand.Licensing.Legacy
{
    public sealed class LegacyLicenseData
    {
        public string ProductCode { get; internal set; }
        public int TrialTimes { get; internal set; }
        public DateTime AppExpiry { get; internal set; }
        public bool AppFullAccess { get; internal set; }
        public string DeviceId { get; internal set; }
        public bool WebAccess { get; internal set; }
        public bool PaymentActivated { get; internal set; }
        public DateTime PaymentExpiry { get; internal set; }
        public int PaymentTerminals { get; internal set; }
        internal XmlDocument Document { get; set; }
    }

    public static class LegacyLicenseStore
    {
        private const int MaxFileBytes = 128 * 1024;
        private static readonly object Gate = new object();
        public static void SaveGenerated(string path, string encryptedContent)
        {
            var data = Parse(encryptedContent);
            ValidateTrial(data, false);
            Locked(path, () => { WriteAtomic(path, encryptedContent, false, true); return true; });
        }

        internal static LegacyLicenseData Parse(string content)
        {
            if (string.IsNullOrWhiteSpace(content)) throw new InvalidDataException("The license file is empty or missing.");
            if (content.Length > MaxFileBytes) throw new InvalidDataException("The license file is too large.");
            string xml;
            try { xml = EncrptyFile.DecryptDES(content, EncrptyFile.FilePassword); }
            catch (Exception ex) when (ex is CryptographicException || ex is FormatException || ex is ArgumentException)
            { throw new InvalidDataException("The license file is damaged or uses an unsupported format. Import a valid license.key file.", ex); }

            var document = new XmlDocument { XmlResolver = null, PreserveWhitespace = true };
            try
            {
                using (var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
                { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxFileBytes })) document.Load(reader);
            }
            catch (XmlException ex) { throw new InvalidDataException("The license contents are damaged.", ex); }
            if (document.DocumentElement == null || document.DocumentElement.Name != "license")
                throw new InvalidDataException("This is not a Quickhand POS license file.");
            foreach (string name in new[] { "productcode", "trialtimes", "app", "web", "payment" })
                if (document.SelectNodes("/license/" + name).Count > 1) throw new InvalidDataException("The license contains duplicate fields.");
            if (document.SelectNodes("/license/app/deviceid").Count > 1) throw new InvalidDataException("The license contains duplicate fields.");

            // Older licences can omit newer feature fields. Missing features remain disabled.
            string product = Value(document, "/license/productcode", string.Empty);
            var data = new LegacyLicenseData
            {
                Document = document,
                ProductCode = product,
                TrialTimes = Integer(document, "/license/trialtimes", string.IsNullOrEmpty(product) ? (int?)null : -1),
                AppExpiry = Expiry(document, "/license/app/@expirydate"),
                AppFullAccess = Boolean(document, "/license/app/@fullaccess"),
                DeviceId = Value(document, "/license/app/deviceid", string.Empty),
                WebAccess = Boolean(document, "/license/web"),
                PaymentActivated = Boolean(document, "/license/payment"),
                PaymentExpiry = Expiry(document, "/license/payment/@expirydate"),
                PaymentTerminals = Integer(document, "/license/payment/@terminalamount", 0)
            };
            // Old settings reloads wrote -1 when a pre-payment licence omitted
            // terminalamount. It meant no usable terminals, never an invalid POS.
            if (data.PaymentTerminals == -1) data.PaymentTerminals = 0;
            if (data.PaymentTerminals < 0) throw new InvalidDataException("The licensed payment terminal count is invalid.");
            return data;
        }

        private static string Value(XmlDocument doc, string xpath, string fallback)
        {
            var node = doc.SelectSingleNode(xpath);
            if (node == null) return fallback;
            foreach (XmlNode child in node.ChildNodes)
                if (child.NodeType == XmlNodeType.Element) throw new InvalidDataException("A license field must contain a single value.");
            return node.InnerText;
        }

        private static int Integer(XmlDocument doc, string xpath, int? fallback)
        {
            string text = Value(doc, xpath, null);
            if (string.IsNullOrWhiteSpace(text) && fallback.HasValue) return fallback.Value;
            int result;
            if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out result))
                throw new InvalidDataException("A license number is missing or invalid.");
            return result;
        }

        private static bool Boolean(XmlDocument doc, string xpath)
        {
            string text = Value(doc, xpath, null);
            if (string.IsNullOrWhiteSpace(text)) return false;
            bool result;
            if (!bool.TryParse(text, out result)) throw new InvalidDataException("A license feature flag is invalid.");
            return result;
        }

        private static DateTime Expiry(XmlDocument doc, string xpath)
        {
            // Keep the legacy local-time encoding. Converting it to UTC would move
            // existing customer expiry boundaries. Accept 64-bit future timestamps.
            string text = Value(doc, xpath, null);
            long seconds = -1;
            if (!string.IsNullOrWhiteSpace(text) && !long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out seconds))
                throw new InvalidDataException("A license expiry date is invalid.");
            try { return Utility.GetDateFromUnixTimeStamp(seconds); }
            catch (ArgumentOutOfRangeException ex) { throw new InvalidDataException("A license expiry date is out of range.", ex); }
        }

        private static void ValidateTrial(LegacyLicenseData data, bool alreadyCounted)
        {
            // A non-empty, matching product code is the existing paid-POS rule.
            // Its counter and APP/payment expiry must never expire the POS itself.
            if (!string.IsNullOrEmpty(data.ProductCode)) return;
            if (data.TrialTimes < 0 || data.TrialTimes > 30)
                throw new InvalidDataException("The trial allowance is invalid. Contact Quickhand POS for a valid license.");
            if (data.TrialTimes == 0 && !alreadyCounted)
                throw new InvalidDataException("The trial has ended. Import a license supplied by Quickhand POS.");
        }

        internal static void WriteAtomic(string path, string content, bool preserveBackup, bool allowCreate)
        {
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                byte[] bytes = new UTF8Encoding(false).GetBytes(content);
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
                if (new FileInfo(temporary).Length != bytes.Length) throw new IOException("The license could not be completely written.");
                if (allowCreate && !File.Exists(path)) File.Move(temporary, path);
                else File.Replace(temporary, path, preserveBackup ? null : path + ".bak");
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static string Fingerprint(string content)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", string.Empty);
        }

        internal static T Locked<T>(string path, Func<T> action)
        {
            lock (Gate)
            using (var mutex = new Mutex(false, @"Local\QuickhandLicense-" + Fingerprint(Path.GetFullPath(path).ToUpperInvariant())))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(TimeSpan.FromSeconds(10)); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new IOException("Another POS program is updating this license. Please retry.");
                    return action();
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }
    }
}
