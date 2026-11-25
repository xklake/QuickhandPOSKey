using LicenseGenerator;
using Newtonsoft.Json.Linq;
using Quickhand.Licensing;
using Quickhand.Licensing.Legacy;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

internal static class StandaloneChecks
{
    private const string Password = "Isolated-test-password-2026!";
    private static int count;
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); count++; Console.WriteLine("PASS: " + message); }
    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception e) when (e is InvalidDataException || e is IOException || e is System.Security.Cryptography.CryptographicException) { return; }
        throw new Exception("Invalid operation was accepted.");
    }
    private static IEnumerable<Control> Controls(Control control)
    {
        foreach (Control child in control.Controls) { yield return child; foreach (var descendant in Controls(child)) yield return descendant; }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        string temp = Path.Combine(Path.GetTempPath(), "qh-independent-license-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp); // Test keys never enter a Git checkout.
        try
        {
            foreach (var assembly in new[] { typeof(SignedLicenseForm).Assembly, typeof(SignedLicenseIssuer).Assembly })
            {
                string[] forbidden = { "Common", "Seals", "DBAccess", "Logger", "XyzAbc", "MySql.Data" };
                Check(!assembly.GetReferencedAssemblies().Any(a => forbidden.Contains(a.Name)), assembly.GetName().Name + " has no POS dependencies");
            }
            Check(IssuerWorkspace.FindSourceRoot(args[0]) == "" && IssuerWorkspace.FindSourceRoot("") == "", "standalone repository and empty paths are not mistaken for POS source");
            EncrptyFile.HardWareInfo = "0123456789ABCDEF"; // Never query real hardware.
            var identity = new LicenseIdentity(EncrptyFile.HardWareInfo, "12121212-3434-5656-7878-909090909090");
            var request = identity.CreateRequest();
            var draft = SignedLicenseIssuer.NewPayload(SignedLicenseIssuer.ReadRequest(request.ToString()), "DEMO", "DEMO-STORE", DateTime.UtcNow.AddMinutes(-1));
            string pfx = Path.Combine(temp, "test.pfx");
            Reject(() => SignedLicenseIssuer.CreateKey(Path.Combine(args[0], "forbidden-test.pfx"), Password));
            SignedLicenseIssuer.CreateKey(pfx, Password);
            var key = IssuerWorkspace.ReadPublicKey(pfx, Password);
            Check(!key.ToString().Contains("<D>"), "public export contains no private material");
            var verifier = new SignedLicenseVerifier(new Dictionary<string, string> { [(string)key["keyId"]] = (string)key["publicKeyXml"] });
            string signed = SignedLicenseIssuer.Sign(draft, pfx, Password);
            var grant = verifier.Verify(signed);
            SignedLicenseVerifier.ValidateUse(grant, identity, DateTime.UtcNow);
            Check(grant.Pos.Permanent && grant.CustomerId == "DEMO", "signed license verifies with the exported public key");
            var bad = JObject.Parse(signed); bad["signature"] = Convert.ToBase64String(new byte[384]);
            Reject(() => verifier.Verify(bad.ToString()));
            Reject(() => SignedLicenseIssuer.Sign(draft, pfx, "wrong-password"));
            Check(true, "tampered signatures and wrong passwords are rejected");
            string issued = Path.Combine(temp, "issued.qhlic");
            Reject(() => SignedLicenseIssuer.IssueToFile(draft, pfx, Password, issued, "demo", false));
            SignedLicenseIssuer.IssueToFile(draft, pfx, Password, issued, "demo", true);
            Reject(() => SignedLicenseIssuer.IssueToFile(draft, pfx, Password, Path.Combine(temp, "duplicate.qhlic"), "demo", true));
            Check(IssuerWorkspace.NextRevision(pfx, (string)draft["licenseId"], 1) == 2, "issuance is audited and duplicate revisions cannot be issued");
            string journal = File.ReadAllText(Path.Combine(temp, "license-issuance.jsonl"));
            Check(!journal.Contains(Password), "issuance journal never stores the password");
            string imported = IssuerWorkspace.ImportKey(pfx, Path.Combine(temp, "imported"), Password);
            Check(File.ReadAllBytes(imported).SequenceEqual(File.ReadAllBytes(pfx)) && File.ReadAllText(Path.Combine(temp,"imported","license-issuance.jsonl")) == journal, "existing-key import preserves key and issuance history");
            Check(EncrptyFile.GetProductCode("TEST-HARDWARE-0001") == "0cbf5672a1f05bfb9a23631c36b254d4", "legacy product-code compatibility vector is unchanged");
            string xml;
            Check(Utility.GetXMLFileContent("", 30, Utility.GetUnixTimeStamp(DateTime.Now.AddYears(1)), false, "", out xml), "legacy license XML can be generated");
            string oldPath = Path.Combine(temp, "license.key");
            LegacyLicenseStore.SaveGenerated(oldPath, EncrptyFile.EncryptDES(xml, EncrptyFile.FilePassword));
            Check(EncrptyFile.DecryptDES(File.ReadAllText(oldPath), EncrptyFile.FilePassword) == xml, "legacy encrypted license saves without format changes");
            string profile = Path.Combine(temp, "preferences.json");
            IssuerWorkspace.SaveProfile(profile, pfx, "", temp);
            Check(!File.ReadAllText(profile).Contains(Password), "profile works without POS source and stores only paths");
            Application.EnableVisualStyles();
            StorageChecks.Run(temp, args[1], pfx, Password, request, draft, Check, Reject);
            using (var form = new SignedLicenseForm(profile))
            {
                form.StartPosition = FormStartPosition.Manual; form.Location = new Point(-3000, -3000); form.ShowInTaskbar = false;
                form.Show(); Application.DoEvents();
                Check(form.Width <= 1024 && form.Height <= 728 && Controls(form).All(c => c.Font.SizeInPoints >= 12), "issuer UI fits 1024x768 with 12pt-or-larger text");
                using (var bitmap = new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap, new Rectangle(Point.Empty,bitmap.Size)); bitmap.Save(Path.Combine(args[1], "LicenseKeyGenerator.png")); }
                form.Close();
            }
            Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => new[] { "Common", "Seals", "DBAccess", "Logger" }.Contains(a.GetName().Name)), "issue/verify/UI workflow runs without loading any POS assemblies");
            Console.WriteLine("PASS: " + count + " independent license checks.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // This exact newly created, random directory contains test data only.
            string resolved = Path.GetFullPath(temp), allowed = Path.GetFullPath(Path.GetTempPath());
            if (Path.GetDirectoryName(resolved).TrimEnd(Path.DirectorySeparatorChar).Equals(allowed.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)
                && Path.GetFileName(resolved).StartsWith("qh-independent-license-", StringComparison.Ordinal)) Directory.Delete(resolved, true);
        }
    }
}
