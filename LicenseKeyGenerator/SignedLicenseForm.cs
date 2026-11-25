using Newtonsoft.Json.Linq;
using Quickhand.Licensing;
using System;
using System.Drawing;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace LicenseGenerator
{
    public sealed class SignedLicenseForm : Form
    {
        private readonly TextBox customer = Box(), store = Box(), password = Box(true), setupPassword = Box(true), confirmPassword = Box(true), keyPath = Box(), sourceRoot = Box();
        private readonly Label requestLabel = Label("1. Load the customer's license-request.json file."), identityLabel = Label("License ID and revision are filled automatically."), keyLabel = Label("Not configured"), message = Label("");
        private readonly CheckBox confirmed = new CheckBox { Text = "I checked the purchased features and approved this authorization.", Dock = DockStyle.Fill }, trial = new CheckBox { Text = "Time-limited POS trial", Dock = DockStyle.Fill };
        private readonly NumericUpDown terminals = new NumericUpDown { Minimum = 0, Maximum = 9999, Dock = DockStyle.Fill, Enabled = false };
        private readonly FeatureEditor pos = new FeatureEditor(true), app = new FeatureEditor(false), web = new FeatureEditor(false), payment = new FeatureEditor(false);
        private readonly TabControl tabs = new TabControl { Dock = DockStyle.Fill, SizeMode = TabSizeMode.Fixed, ItemSize = new Size(220, 44) };
        private JObject request;
        private string licenseId = "", replaces = "", reason = "", appDevice = "", outputFolder = "", operatorName = Environment.UserName;
        private int revision = 1;
        private bool appFull;
        private string profilePath;
        private LicenseStorage storage;
        private readonly bool rememberLocation;
        private readonly TextBox dataFolder = Box();

        public SignedLicenseForm() : this(null, LicenseStorage.DefaultRoot) { }
        // Explicit preferences keep automated checks and portable callers isolated.
        public SignedLicenseForm(string preferencesPath) : this(preferencesPath, Path.Combine(Path.GetDirectoryName(Path.GetFullPath(preferencesPath)), "license-data")) { }
        public SignedLicenseForm(string preferencesPath, string dataRoot)
        {
            storage = new LicenseStorage(dataRoot); rememberLocation = preferencesPath == null;
            profilePath = preferencesPath ?? storage.ProfilePath;
            Text = "Quickhand POS - License issuer"; Font = new Font("Segoe UI", 12F);
            BackColor = Color.White; ForeColor = Color.FromArgb(25, 35, 45); StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None; ClientSize = new Size(980, 670); MinimumSize = new Size(900, 650);
            var daily = Page("Generate license"); var setup = Page("First-time setup"); tabs.TabPages.Add(daily); tabs.TabPages.Add(setup);
            var form = Rows(daily);
            customer.Name = "CustomerId"; store.Name = "StoreId";
            customer.Font = store.Font = new Font("Consolas", 12F);
            SetPartyIds(LicensePartyIds.Create());
            Wide(form, requestLabel, 44); Wide(form, PartyIdRow(), 76); Wide(form, identityLabel, 28);
            Pair(form, "Key password", password, "Signing key", keyLabel);
            Wide(form, Label("2. Choose purchased features. Permanent POS does not expire."), 28);
            foreach (var item in new[] { Tuple.Create("POS", pos), Tuple.Create("APP", app), Tuple.Create("Web", web), Tuple.Create("Payment", payment) }) Single(form, item.Item1, item.Item2, 46);
            Pair(form, "Card terminals", terminals, "Trial", trial);
            payment.Changed += (s, e) => { terminals.Enabled = payment.IsEnabled; terminals.Value = payment.IsEnabled ? Math.Max(1, terminals.Value) : 0; confirmed.Checked = false; };
            trial.CheckedChanged += (s, e) => { pos.SetTrial(trial.Checked); confirmed.Checked = false; };
            foreach (var feature in new[] { pos, app, web }) feature.Changed += (s, e) => confirmed.Checked = false;
            foreach (var field in new[] { customer, store, keyPath }) field.TextChanged += (s, e) => confirmed.Checked = false;
            terminals.ValueChanged += (s, e) => confirmed.Checked = false;

            var first = Rows(setup);
            Wide(first, Label("Set up once. Customers receive only the generated .qhlic file."), 28);
            dataFolder.ReadOnly = true; dataFolder.Text = storage.Root; Single(first, "License data folder", dataFolder, 36);
            Wide(first, ActionRow(Button("Open data folder", false, (s, e) => Run(() => { storage.EnsureFolders(); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(storage.Root) { UseShellExecute = true }); })), Button("Choose data folder...", false, ChooseDataFolder)), 52);
            Wide(first, Label("1. Create a key or use your existing key. Keep it outside the source folder."), 28);
            Pair(first, "Key password", setupPassword, "Confirm new password", confirmPassword);
            Wide(first, ActionRow(Button("Use existing key...", false, UseKey), Button("Create new key...", true, CreateKey)), 54);
            keyPath.ReadOnly = true; Single(first, "Selected key", keyPath, 40);
            Wide(first, Label("2. Add the public key to POS source. Existing trusted keys are preserved."), 28);
            sourceRoot.ReadOnly = true; Single(first, "POS source folder", sourceRoot, 40);
            Wide(first, ActionRow(Button("Choose source...", false, ChooseSource), Button("Configure POS key", true, ConfigureTrust)), 54);
            Wide(first, Label("3. Build POS with this key. Customers need the new build before importing licenses."), 40);
            Wide(first, ActionRow(Button("Build local POS", false, (s, e) => Build(false)), Button("Build installer...", true, (s, e) => Build(true))), 54);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 74, BackColor = Color.FromArgb(234, 244, 253) };
            var issue = Button("Generate license", true, Issue); var load = Button("Load request...", false, LoadRequest);
            var advanced = Button("Advanced...", false, (s, e) => Advanced());
            var footerButtons = new[] { Button("Close", false, (s, e) => Close()), Button("Legacy...", false, (s, e) => { using (var old = new License(storage.Root)) old.ShowDialog(this); }), advanced, load, issue };
            foreach (var button in footerButtons) button.Width = 164;
            footer.Controls.Add(ActionRow(footerButtons));
            var feedback = new Panel { Dock = DockStyle.Bottom, Height = 86, Padding = new Padding(16, 0, 16, 0), BackColor = Color.FromArgb(245, 249, 253) };
            confirmed.Dock = DockStyle.Top; confirmed.Height = 34; message.Dock = DockStyle.Fill;
            feedback.Controls.Add(message); feedback.Controls.Add(confirmed);
            Controls.Add(tabs); Controls.Add(feedback); Controls.Add(footer);
            tabs.SelectedIndexChanged += (s, e) => { bool dailyTab = tabs.SelectedIndex == 0; load.Enabled = issue.Enabled = advanced.Enabled = dailyTab; confirmed.Visible = dailyTab; };
            message.Text = "Back up your key, password and issuance records. Passwords are not saved by this tool.";
            try
            {
                var saved = IssuerWorkspace.LoadProfile(rememberLocation && !File.Exists(profilePath) ? LicenseStorage.LegacyProfilePath : profilePath);
                keyPath.Text = (string)saved["keyPath"] ?? ""; sourceRoot.Text = (string)saved["sourceRoot"] ?? "";
                outputFolder = (string)saved["outputFolder"] ?? "";
            }
            catch (Exception) { message.Text = "Saved setup could not be read. Select your existing key in First-time setup."; }
            if (string.IsNullOrEmpty(sourceRoot.Text)) sourceRoot.Text = IssuerWorkspace.FindSourceRoot(AppDomain.CurrentDomain.BaseDirectory);
            Run(RefreshKey); if (string.IsNullOrEmpty(keyPath.Text)) tabs.SelectedIndex = 1;
        }

        private void RefreshKey()
        {
            keyLabel.Text = string.IsNullOrEmpty(keyPath.Text) ? "Open First-time setup" : Path.GetFileName(keyPath.Text);
            // A request may be loaded before the operator selects an existing key.
            if (request != null && !string.IsNullOrEmpty((string)request["currentLicenseId"]) &&
                (string.IsNullOrWhiteSpace(customer.Text) || string.IsNullOrWhiteSpace(store.Text)))
            {
                var known = IssuerWorkspace.FindPartyIds(keyPath.Text, (string)request["currentLicenseId"]);
                if (known != null)
                {
                    if (string.IsNullOrWhiteSpace(customer.Text)) customer.Text = known.CustomerId;
                    if (string.IsNullOrWhiteSpace(store.Text)) store.Text = known.StoreId;
                }
            }
        }
        private void SavePreferences()
        {
            storage.EnsureFolders(); IssuerWorkspace.SaveProfile(profilePath, keyPath.Text, sourceRoot.Text, outputFolder);
            if (rememberLocation) storage.RememberLocation();
        }
        private void ChooseDataFolder(object sender, EventArgs args)
        {
            using (var folder = new FolderBrowserDialog { Description = "Choose the QuickhandLicenseKeys data folder (outside source and program folders)", SelectedPath = storage.Root })
                if (folder.ShowDialog(this) == DialogResult.OK) Run(() =>
                {
                    var selected = new LicenseStorage(folder.SelectedPath);
                    // Open the selected workspace, rather than silently moving its files or overwriting its profile.
                    var saved = IssuerWorkspace.LoadProfile(selected.ProfilePath); selected.EnsureFolders();
                    storage = selected; profilePath = selected.ProfilePath; dataFolder.Text = storage.Root;
                    keyPath.Text = (string)saved["keyPath"] ?? ""; sourceRoot.Text = (string)saved["sourceRoot"] ?? "";
                    outputFolder = (string)saved["outputFolder"] ?? "";
                    password.Clear(); setupPassword.Clear(); confirmPassword.Clear(); confirmed.Checked = false;
                    RefreshKey(); SavePreferences();
                    message.Text = "Data folder selected. Use existing key to copy a key and its records here. Original files are kept.";
                });
        }
        private void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { message.Text = ex is CryptographicException ? "Cannot open this key. Check the PFX file and its password." : ex.Message; }
        }

        private void CreateKey(object sender, EventArgs args)
        {
            Run(() =>
            {
                if (setupPassword.Text != confirmPassword.Text) throw new InvalidDataException("The two new-key passwords do not match.");
                if (setupPassword.Text.Length < 16) throw new InvalidDataException("Use at least 16 characters for the new signing-key password.");
                if (!string.IsNullOrEmpty(keyPath.Text) && MessageBox.Show(this, "A key is already selected. A new key requires a POS update. Create another key?", "Create new key", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                keyPath.Text = storage.CreateKey(setupPassword.Text); password.Text = setupPassword.Text; RefreshKey(); SavePreferences();
                message.Text = "Key saved in 01_SigningKeys; public export in 02_PublicKeys. Next: Configure POS key, then build POS.";
            });
        }

        private void UseKey(object sender, EventArgs args)
        {
            Run(() =>
            {
                using (var dialog = new OpenFileDialog { Filter = "Signing key (*.pfx)|*.pfx", CheckFileExists = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    keyPath.Text = storage.ImportKey(dialog.FileName, setupPassword.Text); password.Text = setupPassword.Text; RefreshKey(); SavePreferences();
                    message.Text = "Existing key and issuance records retained in the data folder. Original files are kept. Back up the whole folder.";
                }
            });
        }

        private void ChooseSource(object sender, EventArgs args)
        {
            using (var folder = new FolderBrowserDialog { Description = "Select the Quickhand POS source folder", SelectedPath = sourceRoot.Text })
                if (folder.ShowDialog(this) == DialogResult.OK) Run(() => { string root = IssuerWorkspace.FindSourceRoot(folder.SelectedPath); if (string.IsNullOrEmpty(root)) throw new InvalidDataException("This is not a Quickhand POS source folder."); sourceRoot.Text = root; SavePreferences(); });
        }
        private void ConfigureTrust(object sender, EventArgs args)
        {
            Run(() =>
            {
                SignedLicenseIssuer.RequirePrivateLocation(keyPath.Text);
                IssuerWorkspace.ConfigureClientTrust(sourceRoot.Text, IssuerWorkspace.ReadPublicKey(keyPath.Text, setupPassword.Text));
                password.Text = setupPassword.Text; SavePreferences();
                message.Text = "Public key configured. No private key was copied to POS. Next: Build local POS or Build installer.";
            });
        }
        private void Build(bool installer) { Run(() => { using (var dialog = new IssuerBuildForm(sourceRoot.Text, installer)) dialog.ShowDialog(this); }); }

        private void LoadRequest(object sender, EventArgs args)
        {
            Run(() =>
            {
                using (var dialog = new OpenFileDialog { Filter = "Customer request (*.json)|*.json", CheckFileExists = true })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    if (new FileInfo(dialog.FileName).Length > LicenseJson.MaximumBytes) throw new InvalidDataException("The customer request is too large.");
                    var loaded = SignedLicenseIssuer.ReadRequest(File.ReadAllText(dialog.FileName, LicenseJson.Utf8));
                    ApplyRequest(loaded, Path.GetFileName(dialog.FileName));
                }
            });
        }
        private void ApplyRequest(JObject loaded, string name)
        {
            // Finish validation/history reads before replacing the visible draft.
            var draft = SignedLicenseIssuer.NewPayload(loaded, "pending", "pending", DateTime.UtcNow);
            bool renewal = !string.IsNullOrEmpty((string)loaded["currentLicenseId"]);
            bool same = request != null && (string)request["installationId"] == (string)loaded["installationId"] &&
                (string)request["deviceBinding"] == (string)loaded["deviceBinding"] &&
                (string)request["currentLicenseId"] == (string)loaded["currentLicenseId"];
            string loadedId = same ? licenseId : (string)draft["licenseId"];
            int loadedRevision = (int)draft["revision"];
            if (File.Exists(keyPath.Text)) loadedRevision = IssuerWorkspace.NextRevision(keyPath.Text, loadedId, loadedRevision);
            LicensePartyIds ids;
            if (same || (request == null && !renewal)) ids = new LicensePartyIds(customer.Text, store.Text);
            else if (renewal) ids = IssuerWorkspace.FindPartyIds(keyPath.Text, loadedId) ?? new LicensePartyIds("", "");
            else ids = LicensePartyIds.Create();
            storage.ArchiveRequest(loaded);
            request = (JObject)loaded.DeepClone(); licenseId = loadedId; revision = loadedRevision;
            replaces = reason = appDevice = ""; appFull = false; SetPartyIds(ids); confirmed.Checked = false;
            // Never carry purchased rights from a different customer's request.
            trial.Checked = false; pos.Reset(true); app.Reset(false); web.Reset(false); payment.Reset(false); terminals.Value = 0;
            requestLabel.Text = "Request loaded: " + name + Environment.NewLine + "Computer: " + (string)request["installationId"];
            RefreshIdentity();
            message.Text = renewal && (string.IsNullOrWhiteSpace(customer.Text) || string.IsNullOrWhiteSpace(store.Text))
                ? "Renewal: no matching local record. Enter the customer's EXISTING Customer ID and Store ID."
                : renewal ? "Existing customer/store IDs retained. Check the purchased features before renewing."
                : "Customer/store IDs are ready and editable. Keep an existing Customer ID when adding another store.";
        }
        private void SetPartyIds(LicensePartyIds ids) { customer.Text = ids.CustomerId; store.Text = ids.StoreId; }
        private Control PartyIdRow()
        {
            var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Margin = Padding.Empty };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            row.RowStyles.Add(new RowStyle(SizeType.Absolute, 28)); row.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            row.Controls.Add(Label("Customer ID (editable)"), 0, 0); row.Controls.Add(Label("Store ID (editable)"), 1, 0);
            customer.Margin = new Padding(3, 2, 12, 8); store.Margin = new Padding(3, 2, 3, 8);
            row.Controls.Add(customer, 0, 1); row.Controls.Add(store, 1, 1);
            return row;
        }
        private void RefreshIdentity() { identityLabel.Text = "License: " + licenseId + "    Revision: " + revision; }

        private void Advanced()
        {
            using (var dialog = new Form { Text = "Advanced - renewals and computer changes", Font = Font, BackColor = Color.White, AutoScaleMode = AutoScaleMode.None, ClientSize = new Size(860, 480), StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
            {
                var rows = Rows(dialog);
                Wide(rows, Label("Normally keep the generated ID/revision. For an approved computer change, use the existing license ID, a higher revision and a reason."), 70);
                var id = Box(); id.Text = licenseId; var rev = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = revision, Dock = DockStyle.Fill };
                var replace = Box(); replace.Text = replaces; var why = Box(); why.Text = reason; var op = Box(); op.Text = operatorName; var device = Box(); device.Text = appDevice;
                var full = new CheckBox { Text = "APP full access", Checked = appFull, Dock = DockStyle.Fill };
                Single(rows, "License ID", id, 48); Pair(rows, "Revision", rev, "Operator", op); Single(rows, "Replace license ID", replace, 48); Single(rows, "Change reason", why, 48); Pair(rows, "APP device", device, "APP access", full);
                var footer = ActionRow(Button("Cancel", false, (s, e) => dialog.Close()), Button("Apply", true, (s, e) =>
                {
                    Guid parsed;
                    if (!Guid.TryParseExact(id.Text.Trim(), "D", out parsed) || parsed == Guid.Empty) { MessageBox.Show(dialog, "Load a request first, or enter a valid existing license ID."); return; }
                    try
                    {
                        if (licenseId != id.Text.Trim() || replaces != replace.Text.Trim())
                            SetPartyIds(IssuerWorkspace.FindPartyIds(keyPath.Text, string.IsNullOrWhiteSpace(replace.Text) ? id.Text.Trim() : replace.Text.Trim()) ?? new LicensePartyIds("", ""));
                        licenseId = id.Text.Trim(); revision = (int)rev.Value; replaces = replace.Text.Trim(); reason = why.Text.Trim(); operatorName = op.Text.Trim(); appDevice = device.Text.Trim(); appFull = full.Checked; confirmed.Checked = false; RefreshIdentity(); dialog.Close();
                    }
                    catch (Exception ex) { MessageBox.Show(dialog, ex.Message); }
                }));
                footer.Dock = DockStyle.Bottom; footer.Height = 76; dialog.Controls.Add(footer); dialog.ShowDialog(this);
            }
        }

        private void Issue(object sender, EventArgs args)
        {
            Run(() =>
            {
                if (request == null) throw new InvalidDataException("Click Load request and choose the customer's license-request.json first.");
                if (string.IsNullOrWhiteSpace(customer.Text) || string.IsNullOrWhiteSpace(store.Text)) throw new InvalidDataException("Enter the customer ID and store ID.");
                if (string.IsNullOrEmpty(keyPath.Text)) { tabs.SelectedIndex = 1; throw new InvalidDataException("Complete First-time setup using a signing key (.pfx)."); }
                if (!confirmed.Checked) throw new InvalidDataException("Check the purchased features and tick the confirmation first.");
                keyPath.Text = storage.ImportKey(keyPath.Text, password.Text); RefreshKey();
                revision = IssuerWorkspace.NextRevision(keyPath.Text, licenseId, revision); RefreshIdentity();
                var draft = SignedLicenseIssuer.NewPayload(request, customer.Text.Trim(), store.Text.Trim(), DateTime.UtcNow);
                draft["licenseId"] = licenseId; draft["revision"] = revision; draft["paymentTerminals"] = payment.IsEnabled ? (int)terminals.Value : 0;
                draft["replacesLicenseId"] = replaces; draft["changeReason"] = reason; draft["edition"] = trial.Checked ? "trial" : "standard";
                draft["pos"] = pos.Value; draft["app"] = app.Value; draft["web"] = web.Value; draft["payment"] = payment.Value;
                draft["appFullAccess"] = app.IsEnabled && appFull; draft["appDeviceId"] = app.IsEnabled ? appDevice : "";
                string archive = storage.LicensePath(draft);
                using (var dialog = new SaveFileDialog { Filter = "Customer license (*.qhlic)|*.qhlic", FileName = Path.GetFileName(archive), InitialDirectory = Path.GetDirectoryName(archive) })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    SignedLicenseIssuer.RequirePrivateLocation(dialog.FileName);
                    bool copy = !Path.GetFullPath(dialog.FileName).Equals(archive, StringComparison.OrdinalIgnoreCase);
                    if (copy && File.Exists(dialog.FileName)) throw new IOException("The export file already exists. Choose a new filename.");
                    SignedLicenseIssuer.IssueToFile(draft, keyPath.Text, password.Text, archive, operatorName, true);
                    outputFolder = Path.GetDirectoryName(dialog.FileName); confirmed.Checked = false;
                    message.Text = "License archived in 04_IssuedLicenses under this customer/store. Send only the .qhlic file.";
                    if (copy)
                        try { File.Copy(archive, dialog.FileName, false); message.Text += " Export copy saved."; }
                        catch (Exception) { message.Text = "License created and archived, but export copy failed. Retrieve it from: " + archive; }
                    try { SavePreferences(); } catch (Exception) { message.Text += " Preferences could not be saved; the license was created."; }
                }
            });
        }

        private static TextBox Box(bool secret = false) { return new TextBox { Dock = DockStyle.Fill, UseSystemPasswordChar = secret, BorderStyle = BorderStyle.FixedSingle }; }
        private static Label Label(string text) { return new Label { Text = text, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }; }
        private static TabPage Page(string text) { return new TabPage(text) { BackColor = Color.White, Padding = new Padding(10, 0, 10, 0) }; }
        private static TableLayoutPanel Rows(Control parent)
        {
            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(8, 4, 8, 4) };
            var rows = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4 };
            rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180)); rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            scroll.Controls.Add(rows); parent.Controls.Add(scroll); return rows;
        }
        private static void Pair(TableLayoutPanel rows, string a, Control first, string b, Control second)
        {
            int row = rows.RowCount++; rows.RowStyles.Add(new RowStyle(SizeType.Absolute, 46)); rows.Controls.Add(Label(a), 0, row); rows.Controls.Add(first, 1, row); rows.Controls.Add(Label(b), 2, row); rows.Controls.Add(second, 3, row);
            first.Margin = second.Margin = new Padding(3, 8, 10, 5);
        }
        private static void Single(TableLayoutPanel rows, string title, Control control, int height)
        { int row = rows.RowCount++; rows.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); rows.Controls.Add(Label(title), 0, row); rows.Controls.Add(control, 1, row); rows.SetColumnSpan(control, 3); }
        private static void Wide(TableLayoutPanel rows, Control control, int height)
        { int row = rows.RowCount++; rows.RowStyles.Add(new RowStyle(SizeType.Absolute, height)); rows.Controls.Add(control, 0, row); rows.SetColumnSpan(control, 4); }
        private static Button Button(string text, bool primary, EventHandler handler)
        {
            var button = new Button { Text = text, Width = 214, Height = 46, FlatStyle = FlatStyle.Flat, BackColor = primary ? Color.FromArgb(0, 120, 215) : Color.White, ForeColor = primary ? Color.White : Color.Black, Margin = new Padding(4, 2, 4, 2) };
            button.FlatAppearance.BorderColor = Color.FromArgb(170, 195, 220); button.Click += handler; return button;
        }
        private static Panel ActionRow(params Button[] buttons)
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false }; row.Controls.AddRange(buttons); panel.Controls.Add(row);
            panel.Resize += (s, e) => { row.Left = Math.Max(0, (panel.Width - row.PreferredSize.Width) / 2); row.Top = Math.Max(0, (panel.Height - row.PreferredSize.Height) / 2); };
            return panel;
        }
        private sealed class FeatureEditor : TableLayoutPanel
        {
            private readonly CheckBox enabled = new CheckBox { Text = "Enabled", Dock = DockStyle.Fill }, permanent = new CheckBox { Text = "Permanent", Dock = DockStyle.Fill };
            private readonly DateTimePicker expiry = new DateTimePicker { Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd HH:mm", Dock = DockStyle.Fill, Value = DateTime.Now.AddYears(1) };
            private bool isTrial;
            public event EventHandler Changed;
            public bool IsEnabled => enabled.Checked;
            public FeatureEditor(bool required)
            {
                Dock = DockStyle.Fill; ColumnCount = 3; RowCount = 1;
                ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130)); ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150)); ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                Controls.Add(enabled, 0, 0); Controls.Add(permanent, 1, 0); Controls.Add(expiry, 2, 0); enabled.Enabled = !required;
                enabled.CheckedChanged += (s, e) => UpdateState(); permanent.CheckedChanged += (s, e) => UpdateState(); expiry.ValueChanged += (s, e) => Changed?.Invoke(this, EventArgs.Empty); Reset(required);
            }
            public void Reset(bool on) { enabled.Checked = on; permanent.Checked = on && !isTrial; expiry.Value = DateTime.Now.AddYears(1); UpdateState(); }
            public void SetTrial(bool value) { isTrial = value; if (value) permanent.Checked = false; UpdateState(); }
            private void UpdateState() { permanent.Enabled = enabled.Checked && !isTrial; expiry.Enabled = enabled.Checked && !permanent.Checked; Changed?.Invoke(this, EventArgs.Empty); }
            public JObject Value => SignedLicenseIssuer.Feature(enabled.Checked, permanent.Checked, expiry.Value.ToUniversalTime());
        }
    }
}
