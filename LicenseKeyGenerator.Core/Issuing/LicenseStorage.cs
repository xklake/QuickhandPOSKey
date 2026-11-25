using Newtonsoft.Json.Linq;
using Quickhand.Licensing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace LicenseGenerator
{
    /// <summary>Vendor data lives outside source and installation directories.</summary>
    public sealed class LicenseStorage
    {
        public const string SigningKeys = "01_SigningKeys";
        public const string PublicKeys = "02_PublicKeys";
        public const string Requests = "03_CustomerRequests";
        public const string Licenses = "04_IssuedLicenses";
        public const string Records = "05_IssuanceRecords";
        public const string Settings = "06_Settings";
        public const string Notes = "07_PrivateNotes";
        public const string Backups = "08_Backups";
        public string Root { get; private set; }
        public string ProfilePath => Path.Combine(Root, Settings, "issuer-profile.json");
        public static string LegacyProfilePath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickhandPOS", "LicenseIssuer", "profile.json");
        public static string LocationPath => Path.Combine(Path.GetDirectoryName(LegacyProfilePath), "storage-location.json");

        public LicenseStorage(string root)
        {
            Root = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            SignedLicenseIssuer.RequirePrivateLocation(Path.Combine(Root, "data.marker"));
        }

        public static string DefaultRoot
        {
            get
            {
                if (File.Exists(LocationPath))
                {
                    if (new FileInfo(LocationPath).Length > 16384) throw new InvalidDataException("The license data location file is too large.");
                    var saved = LicenseJson.Read(File.ReadAllText(LocationPath, LicenseJson.Utf8));
                    LicenseJson.Fields(saved, "root");
                    return new LicenseStorage(LicenseJson.Text(saved, "root", 1024)).Root;
                }
                for (var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory); dir != null; dir = dir.Parent)
                    if (File.Exists(Path.Combine(dir.FullName, "LicenseKeyGenerator.sln")) && dir.Parent != null)
                        return Path.Combine(dir.Parent.FullName, "QuickhandLicenseKeys");
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "QuickhandLicenseKeys");
            }
        }

        public void EnsureFolders()
        {
            foreach (string folder in new[] { SigningKeys, PublicKeys, Requests, Licenses, Records, Settings, Notes, Backups })
                Directory.CreateDirectory(Path.Combine(Root, folder));
        }

        public void RememberLocation()
        {
            EnsureFolders();
            Directory.CreateDirectory(Path.GetDirectoryName(LocationPath));
            string temp = LocationPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, new JObject { ["root"] = Root }.ToString(), LicenseJson.Utf8);
                if (File.Exists(LocationPath)) File.Replace(temp, LocationPath, null); else File.Move(temp, LocationPath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private static DirectoryInfo ManagedRoot(string keyPath)
        {
            var bucket = new FileInfo(Path.GetFullPath(keyPath)).Directory;
            return bucket?.Parent?.Name == SigningKeys && bucket.Name.StartsWith("Key-", StringComparison.Ordinal)
                ? bucket.Parent.Parent : null;
        }
        public static string JournalPath(string keyPath)
        {
            var root = ManagedRoot(keyPath);
            return root == null ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(keyPath)), "license-issuance.jsonl")
                : Path.Combine(root.FullName, Records, new FileInfo(keyPath).Directory.Name, "license-issuance.jsonl");
        }
        public static string PublicExportPath(string keyPath)
        {
            var root = ManagedRoot(keyPath);
            return root == null ? keyPath + ".public.json"
                : Path.Combine(root.FullName, PublicKeys, new FileInfo(keyPath).Directory.Name, "license-signing.public.json");
        }

        public string CreateKey(string password)
        {
            EnsureFolders();
            string temporary = Path.Combine(Root, Backups, ".new-key-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(temporary);
            string key = Path.Combine(temporary, "license-signing.pfx");
            try { SignedLicenseIssuer.CreateKey(key, password); return ImportKey(key, password); }
            finally
            {
                // Only the exact two newly generated temporary files are removed.
                if (File.Exists(key)) File.Delete(key);
                if (File.Exists(key + ".public.json")) File.Delete(key + ".public.json");
                if (Directory.GetFileSystemEntries(temporary).Length == 0) Directory.Delete(temporary);
            }
        }

        public string ImportKey(string source, string password)
        {
            var publicKey = IssuerWorkspace.ReadPublicKey(source, password);
            EnsureFolders();
            if (ManagedRoot(source)?.FullName.Equals(Root, StringComparison.OrdinalIgnoreCase) == true) return Path.GetFullPath(source);
            string bucket = "Key-" + ((string)publicKey["keyId"]).Substring(0, 16);
            string target = Path.Combine(Root, SigningKeys, bucket, "license-signing.pfx");
            string journal = JournalPath(target), exported = PublicExportPath(target);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            Directory.CreateDirectory(Path.GetDirectoryName(journal));
            Directory.CreateDirectory(Path.GetDirectoryName(exported));
            using (var gate = new System.Threading.Mutex(false, "Local\\Quickhand-KeyImport-" + LicenseIdentity.Hash(target.ToUpperInvariant())))
            {
                bool locked = false;
                try
                {
                    try { locked = gate.WaitOne(TimeSpan.FromSeconds(10)); } catch (System.Threading.AbandonedMutexException) { locked = true; }
                    if (!locked) throw new IOException("Another operator is importing this key. Retry shortly.");
                    if (File.Exists(target))
                    {
                        if ((string)IssuerWorkspace.ReadPublicKey(target, password)["keyId"] != (string)publicKey["keyId"])
                            throw new InvalidDataException("The destination key folder belongs to another signing key.");
                        MergeJournal(JournalPath(source), journal);
                        return target;
                    }
                    // History/public export first, PFX last. A completed key always has its history.
                    MergeJournal(JournalPath(source), journal);
                    File.WriteAllText(exported, LicenseJson.Compact(publicKey), LicenseJson.Utf8);
                    File.Copy(source, target, false);
                    return target;
                }
                finally { if (locked) gate.ReleaseMutex(); }
            }
        }

        private static void MergeJournal(string source, string destination)
        {
            if (string.Equals(Path.GetFullPath(source), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase)) return;
            using (var original = File.Exists(source) ? new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.None) : null)
            using (var current = new FileStream(destination, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                var records = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
                Action<JObject> add = entry =>
                {
                    string id = LicenseJson.Text(entry, "licenseId", 36) + "/" + LicenseJson.Number(entry, "revision", 1, int.MaxValue);
                    JObject previous;
                    if (records.TryGetValue(id, out previous) && !JToken.DeepEquals(previous, entry))
                        throw new InvalidDataException("Issuance histories conflict at the same license/revision. Review the originals before importing.");
                    records[id] = entry;
                };
                using (var reader = new StreamReader(current, LicenseJson.Utf8, false, 4096, true))
                { string line; while ((line = reader.ReadLine()) != null) add(LicenseJson.Read(line)); }
                if (original == null) return;
                var additions = new List<string>();
                using (var reader = new StreamReader(original, LicenseJson.Utf8, false, 4096, true))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        var entry = LicenseJson.Read(line);
                        string id = LicenseJson.Text(entry, "licenseId", 36) + "/" + LicenseJson.Number(entry, "revision", 1, int.MaxValue);
                        bool exists = records.ContainsKey(id); add(entry);
                        if (!exists) additions.Add(LicenseJson.Compact(entry));
                    }
                }
                if (additions.Count == 0) return;
                bool newline = false;
                if (current.Length > 0) { current.Position = current.Length - 1; newline = current.ReadByte() != 10; }
                current.Position = current.Length;
                byte[] bytes = LicenseJson.Utf8.GetBytes((newline ? Environment.NewLine : "") + string.Join(Environment.NewLine, additions) + Environment.NewLine);
                current.Write(bytes, 0, bytes.Length); current.Flush(true);
            }
        }

        public string ArchiveRequest(JObject request)
        {
            SignedLicenseIssuer.ReadRequest(LicenseJson.Compact(request));
            string folder = Path.Combine(Root, Requests, (string)request["installationId"]);
            Directory.CreateDirectory(folder);
            string file = Path.Combine(folder, "request-" + (string)request["requestId"] + ".json");
            if (File.Exists(file))
            {
                if (!JToken.DeepEquals(LicenseJson.Read(File.ReadAllText(file, LicenseJson.Utf8)), request))
                    throw new InvalidDataException("A different request with this request ID is already archived.");
            }
            else using (var stream = new FileStream(file, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { var bytes = LicenseJson.Utf8.GetBytes(LicenseJson.Compact(request)); stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            return file;
        }

        public string LicensePath(JObject draft)
        {
            string folder = Path.Combine(Root, Licenses, "Customer-" + SafeName(LicenseJson.Text(draft, "customerId")), "Store-" + SafeName(LicenseJson.Text(draft, "storeId")));
            Guid id;
            if (!Guid.TryParseExact((string)draft["licenseId"], "D", out id) || id == Guid.Empty) throw new InvalidDataException("Invalid license ID.");
            int revision = LicenseJson.Number(draft, "revision", 1, int.MaxValue);
            Directory.CreateDirectory(folder);
            return Path.Combine(folder, "license-" + id.ToString("D") + "-r" + revision + ".qhlic");
        }

        private static string SafeName(string value)
        {
            string clean = new string(value.Select(c => char.IsLetterOrDigit(c) && c < 128 || c == '-' || c == '_' ? c : '_').ToArray());
            if (clean.Length <= 36 && clean == value) return clean;
            return clean.Substring(0, Math.Min(24, clean.Length)) + "-" + LicenseIdentity.Hash(value).Substring(0, 16);
        }
    }
}
