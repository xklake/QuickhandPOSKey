using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Quickhand.ReleaseBuild;

namespace LicenseGenerator
{
    // Only launches the existing build pipeline; never installs software or changes services.
    public sealed class IssuerBuildForm : Form
    {
        private readonly string root;
        private readonly bool installer;
        private readonly TextBox version = new TextBox(), updateKey = new TextBox { ReadOnly = true }, compilerPath = new TextBox { ReadOnly = true, Name = "InnoCompilerPath" };
        private readonly CheckBox test = new CheckBox { Text = "Test package (not for customers)", Checked = true, AutoSize = true };
        private readonly ComboBox edition = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 150 };
        private readonly TextBox log = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false, Dock = DockStyle.Fill };
        private readonly Label summary = new Label { Dock = DockStyle.Top, Height = 76 };
        private readonly Button start, close, open;
        private bool running;
        private string resultFolder;
        private readonly string compilerPreferences = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "QuickhandPOS", "LicenseIssuer", "build-tools.json");

        public IssuerBuildForm(string sourceRoot, bool createInstaller)
        {
            root = IssuerWorkspace.FindSourceRoot(sourceRoot);
            if (string.IsNullOrEmpty(root)) throw new InvalidDataException("Choose the POS source folder in First-time setup.");
            installer = createInstaller;
            Text = installer ? "Build license-ready installer" : "Build local POS";
            Font = new Font("Segoe UI", 12F); AutoScaleMode = AutoScaleMode.None; BackColor = Color.White;
            ClientSize = new Size(940, 640); MinimumSize = new Size(900, 600); StartPosition = FormStartPosition.CenterParent;
            var options = new TableLayoutPanel { Dock = DockStyle.Top, Height = installer ? 216 : 0, ColumnCount = 4, RowCount = 4, Visible = installer };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            for (int i = 0; i < 4; i++) options.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            edition.Items.AddRange(new object[] { "catering", "common" }); edition.SelectedIndex = 0;
            version.Text = SuggestVersion(root);
            options.Controls.Add(new Label { Text = "Version", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0); options.Controls.Add(version, 1, 0);
            options.Controls.Add(new Label { Text = "Edition", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 0); options.Controls.Add(edition, 3, 0);
            options.Controls.Add(test, 0, 1); options.SetColumnSpan(test, 4);
            options.Controls.Add(new Label { Text = "Update signing key", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2); options.Controls.Add(updateKey, 1, 2); options.SetColumnSpan(updateKey, 2);
            var choose = MakeButton("Choose XML...", (s, e) => { using (var picker = new OpenFileDialog { Filter = "Update private key (*.xml)|*.xml", CheckFileExists = true }) if (picker.ShowDialog(this) == DialogResult.OK) updateKey.Text = picker.FileName; });
            options.Controls.Add(choose, 3, 2);
            options.Controls.Add(new Label { Text = "Inno compiler", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
            options.Controls.Add(compilerPath, 1, 3); options.SetColumnSpan(compilerPath, 2);
            options.Controls.Add(MakeButton("Choose ISCC.exe...", ChooseCompiler), 3, 3);
            version.Dock = edition.Dock = updateKey.Dock = compilerPath.Dock = DockStyle.Fill;
            version.Margin = edition.Margin = updateKey.Margin = compilerPath.Margin = new Padding(4, 10, 4, 4);
            if (installer) compilerPath.Text = FindCompiler();
            var footer = new Panel { Dock = DockStyle.Bottom, Height = 74, BackColor = Color.FromArgb(234, 244, 253) };
            var actions = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
            close = MakeButton("Close", (s, e) => Close()); open = MakeButton("Open output folder", (s, e) => { if (Directory.Exists(resultFolder)) Process.Start(new ProcessStartInfo(resultFolder) { UseShellExecute = true }); }); open.Enabled = false;
            start = MakeButton("Start build", StartBuild); start.BackColor = Color.FromArgb(0, 120, 215); start.ForeColor = Color.White;
            actions.Controls.AddRange(new Control[] { close, open, start }); footer.Controls.Add(actions);
            footer.Resize += (s, e) => actions.Location = new Point(Math.Max(0, (footer.Width - actions.PreferredSize.Width) / 2), 10);
            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) }; body.Controls.Add(log); body.Controls.Add(summary); body.Controls.Add(options);
            Controls.Add(body); Controls.Add(footer);
            Action explain = () => summary.Text = installer
                ? "The first package in this release line becomes its baseline automatically. Existing history is preserved. An XML update key is optional and is different from your PFX license key."
                : "Builds the Debug POS and MenuTool with the configured public key. Close POS/MenuTool and stop VS debugging first. No installer is run.";
            explain(); test.CheckedChanged += (s, e) => explain();
            FormClosing += (s, e) => { if (running) { e.Cancel = true; summary.Text = "Build is still running. Wait for completion; the window stays open to retain its result."; } };
        }

        private string FindCompiler()
        {
            try
            {
                if (File.Exists(compilerPreferences) && new FileInfo(compilerPreferences).Length <= 8192)
                {
                    var saved = Quickhand.Licensing.LicenseJson.Read(File.ReadAllText(compilerPreferences));
                    string path = (string)saved["isccPath"];
                    if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) return InnoCompilerLocator.Find(path);
                }
            }
            catch (Exception ex) when (ex is IOException || ex is ArgumentException || ex is InvalidCastException || ex is UnauthorizedAccessException) { }
            return InnoCompilerLocator.Find(null) ?? "";
        }

        private void ChooseCompiler(object sender, EventArgs args)
        {
            using (var dialog = new OpenFileDialog { Title = "Select Inno Setup command-line compiler", Filter = "Inno Setup compiler (ISCC.exe)|ISCC.exe", FileName = "ISCC.exe", CheckFileExists = true })
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    compilerPath.Text = InnoCompilerLocator.Find(dialog.FileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(compilerPreferences));
                    string temp = compilerPreferences + "." + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        File.WriteAllText(temp, new Newtonsoft.Json.Linq.JObject { ["isccPath"] = compilerPath.Text }.ToString(), Quickhand.Licensing.LicenseJson.Utf8);
                        if (File.Exists(compilerPreferences)) File.Replace(temp, compilerPreferences, null); else File.Move(temp, compilerPreferences);
                    }
                    finally { if (File.Exists(temp)) File.Delete(temp); }
                    summary.Text = "Compiler selected and remembered. Click Start build.";
                }
                catch (Exception ex) { summary.Text = ex.Message; }
            }
        }

        public static string QuoteArgument(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0 || value.Contains("\r") || value.Contains("\n")) throw new ArgumentException("Invalid build argument.");
            // Windows argv quoting; no command shell and no string-evaluated PowerShell commands.
            var text = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                text.Append('\\', c == '"' ? slashes * 2 + 1 : slashes); text.Append(c); slashes = 0;
            }
            text.Append('\\', slashes * 2); return text.Append('"').ToString();
        }

        public static bool NeedsBaseline(string repository, string selectedEdition, bool testOnly)
        {
            string history = Path.Combine(repository, ".releases", "archive", testOnly ? "test-only" : "", selectedEdition);
            return !Directory.Exists(history) || !Directory.GetDirectories(history).Any(p => !Path.GetFileName(p).StartsWith(".pending-", StringComparison.OrdinalIgnoreCase));
        }

        private static string SuggestVersion(string repository)
        {
            Version latest = new Version(0, 0, 0, 0);
            string archive = Path.Combine(repository, ".releases", "archive");
            foreach (string line in new[] { "catering", "common", "test-only/catering", "test-only/common" })
            {
                string path = Path.Combine(archive, line.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(path)) continue;
                foreach (string dir in Directory.GetDirectories(path)) { Version v; if (Version.TryParse(Path.GetFileName(dir), out v) && v > latest) latest = v; }
            }
            return latest.Major == 0 ? "26.11.15.1" : new Version(latest.Major, latest.Minor, latest.Build, latest.Revision + 1).ToString();
        }

        private async void StartBuild(object sender, EventArgs args)
        {
            string selectedEdition = (string)edition.SelectedItem, selectedVersion = version.Text.Trim(), selectedKey = updateKey.Text;
            bool testOnly = test.Checked;
            string selectedCompiler = null;
            if (installer)
            {
                Version parsed;
                if (!Version.TryParse(selectedVersion, out parsed) || selectedVersion.Split('.').Length != 4 || new[] { parsed.Major, parsed.Minor, parsed.Build, parsed.Revision }.Any(n => n < 0 || n > 65534)) { summary.Text = "Enter a four-part version, for example 26.11.15.14."; return; }
                try { selectedCompiler = InnoCompilerLocator.Find(compilerPath.Text); }
                catch (Exception ex) { summary.Text = ex.Message; return; }
                if (selectedCompiler == null) { summary.Text = "Inno Setup was not found. Install Inno Setup, then click Choose ISCC.exe and select its compiler."; return; }
                compilerPath.Text = selectedCompiler;
                if (!testOnly && string.IsNullOrWhiteSpace(selectedKey) && MessageBox.Show(this, "No update signing key is selected. This package will not enable signed online/offline updates. Continue?", "Installer signing", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            }
            running = true; start.Enabled = close.Enabled = open.Enabled = false; version.Enabled = edition.Enabled = test.Enabled = updateKey.Parent.Enabled = false; log.Clear();
            try
            {
                if (installer)
                {
                    var arguments = new List<string> { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", Path.Combine(root, "Scripts", "release", "Build-Release.ps1"), "-Version", selectedVersion, "-Edition", selectedEdition, "-ISCCPath", selectedCompiler };
                    Append("Inno Setup compiler: " + selectedCompiler);
                    if (testOnly) arguments.Add("-TestOnly");
                    bool baseline = NeedsBaseline(root, selectedEdition, testOnly);
                    if (baseline) arguments.Add("-Baseline");
                    if (!string.IsNullOrWhiteSpace(selectedKey)) { arguments.Add("-PrivateKeyPath"); arguments.Add(selectedKey); }
                    else if (!baseline)
                    {
                        // Reuse the existing release line's public key for unsigned local/test builds.
                        // The pipeline still refuses invalid archives or unexpected key changes.
                        string history = Path.Combine(root, ".releases", "archive", testOnly ? "test-only" : "", selectedEdition);
                        string last = Directory.GetDirectories(history).Where(p => !Path.GetFileName(p).StartsWith(".pending-", StringComparison.OrdinalIgnoreCase)).OrderBy(p => Version.Parse(Path.GetFileName(p))).Last();
                        var settings = Quickhand.Licensing.LicenseJson.Read(File.ReadAllText(Path.Combine(last, "full", "update-settings.json")));
                        string publicXml = (string)settings["publicKeyXml"];
                        if (!string.IsNullOrEmpty(publicXml))
                        {
                            // Strip to public parameters even if a malformed archive contains more.
                            using (var rsa = System.Security.Cryptography.RSA.Create()) { rsa.FromXmlString(publicXml); publicXml = rsa.ToXmlString(false); }
                            temporaryPublicKey = Path.Combine(Path.GetTempPath(), "Quickhand-release-public-" + Guid.NewGuid().ToString("N") + ".xml");
                            File.WriteAllText(temporaryPublicKey, publicXml); arguments.Add("-PublicKeyPath"); arguments.Add(temporaryPublicKey);
                        }
                    }
                    summary.Text = baseline ? "Building the first release in this line (baseline)." : "Building with existing release history.";
                    await RunProcess(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), arguments);
                    resultFolder = Path.Combine(root, ".releases", "archive", testOnly ? "test-only" : "", selectedEdition, selectedVersion, "packages");
                    summary.Text = testOnly ? "Test package created. Use only for validation, not customer distribution." : "Release package created. Validate it on a separate test machine before distribution.";
                }
                else
                {
                    string vswhere = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Visual Studio", "Installer", "vswhere.exe");
                    string msbuild = null;
                    await RunProcess(vswhere, new[] { "-latest", "-products", "*", "-requires", "Microsoft.Component.MSBuild", "-find", "MSBuild\\**\\Bin\\MSBuild.exe" }, text => { if (!string.IsNullOrWhiteSpace(text) && File.Exists(text.Trim())) msbuild = text.Trim(); });
                    if (msbuild == null) throw new FileNotFoundException("Visual Studio MSBuild was not found.");
                    foreach (string project in new[] { "SmartPOS/QuickhandPOS.csproj", "ManagerTool/MenuTool.csproj" })
                        await RunProcess(msbuild, new[] { Path.Combine(root, project), "/t:Build", "/restore", "/p:Configuration=Debug", "/p:Platform=AnyCPU", "/p:PostBuildEvent=", "/v:minimal", "/nologo" });
                    resultFolder = Path.Combine(root, "SmartPOS", "bin", "Debug"); summary.Text = "Local POS and MenuTool built. Restart them before importing a license. For customers, use Build installer.";
                }
                open.Enabled = Directory.Exists(resultFolder);
            }
            catch (Exception ex) { summary.Text = "Build failed. See details below; no installer was run."; Append(ex.Message); }
            finally
            {
                if (temporaryPublicKey != null) { try { File.Delete(temporaryPublicKey); } catch (IOException) { } temporaryPublicKey = null; }
                running = false; start.Enabled = close.Enabled = true; version.Enabled = edition.Enabled = test.Enabled = updateKey.Parent.Enabled = true;
            }
        }
        private string temporaryPublicKey;
        private Task RunProcess(string executable, IEnumerable<string> arguments, Action<string> output = null)
        {
            return Task.Run(() =>
            {
                var info = new ProcessStartInfo(executable, string.Join(" ", arguments.Select(QuoteArgument))) { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = new Process { StartInfo = info })
                {
                    process.OutputDataReceived += (s, e) => { if (e.Data != null) { if (output != null) output(e.Data); else Append(e.Data); } };
                    process.ErrorDataReceived += (s, e) => { if (e.Data != null) Append(e.Data); };
                    process.Start(); process.BeginOutputReadLine(); process.BeginErrorReadLine(); process.WaitForExit();
                    if (process.ExitCode != 0) throw new InvalidOperationException("Build stopped (exit " + process.ExitCode + "). Correct the reported error and retry.");
                }
            });
        }
        private void Append(string text) { if (IsDisposed) return; if (InvokeRequired) { BeginInvoke(new Action<string>(Append), text); return; } log.AppendText(text + Environment.NewLine); }
        private static Button MakeButton(string title, EventHandler action) { var b = new Button { Text = title, Width = 196, Height = 46, FlatStyle = FlatStyle.Flat, BackColor = Color.White }; b.FlatAppearance.BorderColor = Color.FromArgb(170, 195, 220); b.Click += action; return b; }
    }
}
