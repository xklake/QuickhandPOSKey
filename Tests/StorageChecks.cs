using LicenseGenerator;
using Newtonsoft.Json.Linq;
using Quickhand.Licensing;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Windows.Forms;

internal static class StorageChecks
{
    internal static void Run(string temp, string artifacts, string pfx, string password, JObject request, JObject draft, Action<bool, string> check, Action<Action> reject)
    {
        var ids = new System.Collections.Concurrent.ConcurrentDictionary<string, bool>();
        System.Threading.Tasks.Parallel.For(0, 10000, i =>
        {
            var pair = LicensePartyIds.Create();
            foreach (string id in new[] { pair.CustomerId, pair.StoreId })
            {
                if (!Regex.IsMatch(id, "^(CUS|STR)-([0-9A-HJKMNP-TV-Z]{4}-){6}[0-9A-HJKMNP-TV-Z]{2}$") || id.Length != 36 || !ids.TryAdd(id, true))
                    throw new Exception("Malformed or duplicate generated identifier.");
            }
        });
        check(ids.Count == 20000, "20,000 concurrent customer/store identifiers have the readable format and no duplicates");
        var storage = new LicenseStorage(Path.Combine(temp, "organized"));
        storage.EnsureFolders();
        check(Directory.GetDirectories(storage.Root).Length == 8, "eight purpose-specific data folders are created outside source");
        string originalJournal = File.ReadAllText(LicenseStorage.JournalPath(pfx));
        string managed = storage.ImportKey(pfx, password);
        check(File.ReadAllBytes(pfx).SequenceEqual(File.ReadAllBytes(managed)) && File.ReadAllText(LicenseStorage.JournalPath(pfx)) == originalJournal,
            "managed import preserves original key and journal bytes");
        check(managed.Contains(LicenseStorage.SigningKeys) && File.Exists(LicenseStorage.PublicExportPath(managed)) && LicenseStorage.PublicExportPath(managed).Contains(LicenseStorage.PublicKeys)
            && LicenseStorage.JournalPath(managed).Contains(LicenseStorage.Records), "private key, public export and history use separate folders");
        string requestFile = storage.ArchiveRequest(request);
        check(storage.ArchiveRequest(request) == requestFile && JToken.DeepEquals(LicenseJson.Read(File.ReadAllText(requestFile)), request), "customer requests are archived and reloading is idempotent");
        var conflict = (JObject)request.DeepClone(); conflict["deviceBinding"] = new string('a', 64);
        reject(() => storage.ArchiveRequest(conflict));
        check(JToken.DeepEquals(LicenseJson.Read(File.ReadAllText(requestFile)), request), "conflicting request IDs cannot overwrite an archive");
        var renewal = (JObject)draft.DeepClone(); renewal["revision"] = 2;
        string issued = storage.LicensePath(renewal);
        SignedLicenseIssuer.IssueToFile(renewal, managed, password, issued, "isolated test", true);
        check(File.Exists(issued) && issued.Contains("Customer-DEMO") && issued.Contains("Store-DEMO-STORE"), "issued license is filed under its customer and store");
        storage.ImportKey(pfx, password);
        check(IssuerWorkspace.NextRevision(managed, (string)draft["licenseId"], 1) == 3 && File.ReadAllLines(LicenseStorage.JournalPath(managed)).Length == 2,
            "re-importing an old key backup retains newer revisions without duplicate history");
        var known = IssuerWorkspace.FindPartyIds(managed, (string)draft["licenseId"]);
        check(known.CustomerId == "DEMO" && known.StoreId == "DEMO-STORE", "renewals recover existing customer/store identifiers from managed history");
        string alternate = Path.Combine(temp, "conflicting-source"); Directory.CreateDirectory(alternate);
        string otherKey = Path.Combine(alternate, "same.pfx"); File.Copy(pfx, otherKey);
        var badHistory = LicenseJson.Read(originalJournal.Trim()); badHistory["customerId"] = "another-customer";
        File.WriteAllText(LicenseStorage.JournalPath(otherKey), badHistory.ToString(Newtonsoft.Json.Formatting.None) + Environment.NewLine);
        string before = File.ReadAllText(LicenseStorage.JournalPath(managed));
        reject(() => storage.ImportKey(otherKey, password));
        check(File.ReadAllText(LicenseStorage.JournalPath(managed)) == before, "conflicting histories fail without altering existing issuance records");
        var unsafeNames = (JObject)draft.DeepClone(); unsafeNames["customerId"] = "../customer:one"; unsafeNames["storeId"] = "CON/../../store";
        string safe = storage.LicensePath(unsafeNames);
        check(Path.GetFullPath(safe).StartsWith(Path.Combine(storage.Root, LicenseStorage.Licenses) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !safe.Contains(".."), "manually entered identifiers cannot escape the archive folder");
        string fresh = storage.CreateKey(password);
        check(fresh != managed && File.Exists(fresh) && File.Exists(LicenseStorage.PublicExportPath(fresh)), "new signing keys use their own folder and preserve existing keys");
        var auto = LicensePartyIds.Create(); var autoDraft = SignedLicenseIssuer.NewPayload(request, auto.CustomerId, auto.StoreId, DateTime.UtcNow);
        var pub = IssuerWorkspace.ReadPublicKey(managed, password);
        var verifier = new SignedLicenseVerifier(new Dictionary<string, string> { [(string)pub["keyId"]] = (string)pub["publicKeyXml"] });
        check(verifier.Verify(SignedLicenseIssuer.Sign(autoDraft, managed, password)).CustomerId == auto.CustomerId, "new identifiers work with the existing signed license protocol");
        string profile = Path.Combine(temp, "ui-profile.json"); IssuerWorkspace.SaveProfile(profile, managed, "", "");
        using (var form = new SignedLicenseForm(profile, storage.Root))
        {
            form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-3000, -3000); form.ShowInTaskbar = false;
            form.Show(); Application.DoEvents();
            var customer = Field<TextBox>(form, "customer"); var store = Field<TextBox>(form, "store");
            string initial = customer.Text;
            Load(form, request); check(customer.Text == initial && !string.IsNullOrEmpty(store.Text), "first request keeps the visible generated IDs");
            customer.Text = "CUSTOM-CUSTOMER"; store.Text = "CUSTOM-STORE";
            Load(form, request); check(customer.Text == "CUSTOM-CUSTOMER" && store.Text == "CUSTOM-STORE", "reloading the same request preserves manual identifiers");
            var another = new LicenseIdentity("FEDCBA9876543210", Guid.NewGuid().ToString("D")).CreateRequest();
            Load(form, another); check(customer.Text.StartsWith("CUS-") && customer.Text != initial && store.Text.StartsWith("STR-"), "a different new installation gets fresh IDs");
            string revisionId = (string)draft["licenseId"];
            var renewalRequest = (JObject)request.DeepClone(); renewalRequest["currentLicenseId"] = revisionId; renewalRequest["currentRevision"] = 1; renewalRequest["requestId"] = Guid.NewGuid().ToString("D");
            Load(form, renewalRequest); check(customer.Text == "DEMO" && store.Text == "DEMO-STORE", "UI renewal retains the original customer/store IDs");
            renewalRequest["currentLicenseId"] = Guid.NewGuid().ToString("D"); renewalRequest["requestId"] = Guid.NewGuid().ToString("D");
            Load(form, renewalRequest); check(customer.Text == "" && store.Text == "" && Field<Label>(form, "message").Text.Contains("EXISTING"), "unknown renewal asks for original IDs instead of assigning new ones");
            // Loading before selecting a key also restores IDs once the key is selected.
            Field<TextBox>(form, "keyPath").Clear(); renewalRequest["currentLicenseId"] = revisionId; renewalRequest["requestId"] = Guid.NewGuid().ToString("D");
            Load(form, renewalRequest); customer.Text = "OPERATOR-CHOICE"; Field<TextBox>(form, "keyPath").Text = managed;
            typeof(SignedLicenseForm).GetMethod("RefreshKey", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null);
            check(customer.Text == "OPERATOR-CHOICE" && store.Text == "DEMO-STORE", "late key selection fills missing IDs without replacing manual input");
            Load(form, another); Application.DoEvents();
            check(customer.ClientSize.Width >= TextRenderer.MeasureText(customer.Text, customer.Font).Width && store.ClientSize.Width >= TextRenderer.MeasureText(store.Text, store.Font).Width,
                "complete generated identifiers fit at 12pt in a 1024-wide desktop");
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(artifacts, "LicenseKeyGenerator-IDs.png")); }
            Field<TabControl>(form, "tabs").SelectedIndex = 1; Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(artifacts, "LicenseKeyGenerator-Storage.png")); }
            form.Close();
        }
    }
    private static T Field<T>(object form, string name) { return (T)form.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form); }
    private static void Load(SignedLicenseForm form, JObject request)
    { typeof(SignedLicenseForm).GetMethod("ApplyRequest", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, new object[] { request, "demo-request.json" }); }
}
