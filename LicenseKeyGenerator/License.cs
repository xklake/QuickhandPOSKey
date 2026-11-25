using Quickhand.Licensing.Legacy;

using System;
using System.IO;
using System.Windows.Forms;

namespace LicenseGenerator
{
    public partial class License : Form
    {
        private readonly LicenseStorage storage;
        public License() : this(LicenseStorage.DefaultRoot) { }
        public License(string dataRoot)
        {
            storage = new LicenseStorage(dataRoot);
            InitializeComponent();

            m_DataPickTime.Value = DateTime.Now.AddYears(1);
            m_txtHardware.Text = EncrptyFile.GetHardwareInfo();
            CenterToScreen();
        }

        private void m_Generator_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(m_txtHardware.Text))
            {
                m_LicenseCode.Text = string.Empty;
                MessageBox.Show("Computer code cannot be empty", "Error");
                return;
            }

            m_HashCode.Text = EncrptyFile.GetMd5Hash(m_txtHardware.Text);
            m_LicenseCode.Text = EncrptyFile.GetProductCode(m_txtHardware.Text);
            try
            {
                if (!string.IsNullOrEmpty(m_LicenseCode.Text))
                {
                    // 剪贴板可能被其他程序占用，重试 10 次，每次间隔 100ms
                    Clipboard.SetDataObject(m_LicenseCode.Text, true, 10, 100);
                }
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                // 复制失败不影响 License 生成
            }
        }

        // Keep customer output outside the installation directory.
        private void m_GenerateFile_Click(object sender, EventArgs e)
        {
            string archive = null;
            try
            {
                storage.EnsureFolders();
                string folder = Path.Combine(storage.Root, LicenseStorage.Licenses, "Legacy", DateTime.Now.ToString("yyyy-MM-dd_HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(folder);
                archive = Path.Combine(folder, EncrptyFile.LicenseFileName);
                using (var dialog = new SaveFileDialog
                {
                    Title = "Save legacy customer license",
                    Filter = "Legacy license (license.key)|*.key",
                    FileName = EncrptyFile.LicenseFileName,
                    InitialDirectory = folder,
                    OverwritePrompt = true
                })
                {
                    if (dialog.ShowDialog(this) != DialogResult.OK) return;
                    SignedLicenseIssuer.RequirePrivateLocation(dialog.FileName);
                    bool copy = !Path.GetFullPath(dialog.FileName).Equals(archive, StringComparison.OrdinalIgnoreCase);
                    if (copy && File.Exists(dialog.FileName)) throw new IOException("The export file already exists. Choose a new filename.");
                    GenerateLicenseFile(archive);
                    if (copy) File.Copy(archive, dialog.FileName, false);
                    MessageBox.Show(this, "License saved to:\n" + dialog.FileName, "Information");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(archive != null && File.Exists(archive)
                    ? "The license is archived, but exporting failed. Retrieve it from:\n" + archive + "\n" + ex.Message
                    : "License was not saved: " + ex.Message, "Error");
            }
        }

        private void GenerateLicenseFile(string path)
        {
            if (!m_ChkTrial.Checked && string.IsNullOrWhiteSpace(m_txtHardware.Text))
                throw new InvalidDataException("Computer code cannot be empty.");

            string productCode = m_ChkTrial.Checked ? string.Empty : EncrptyFile.GetProductCode(m_txtHardware.Text);

            int terminals = 0;
            if (!string.IsNullOrWhiteSpace(m_txtTerminalsAmount.Text) &&
                (!int.TryParse(m_txtTerminalsAmount.Text, out terminals) || terminals < 0))
            {
                throw new InvalidDataException("Payment terminal count must be a non-negative whole number.");
            }

            string content;
            if (!Utility.GetXMLFileContent(productCode,
                m_ChkTrial.Checked ? 30 : Int16.MaxValue,
                Utility.GetUnixTimeStamp(m_DataPickTime.Value), m_ChKFullAccess.Checked, m_txbDeviceId.Text.Trim(),
                out content, m_ChkWeb.Checked, m_chbPaymentActiviated.Checked,
                Utility.GetUnixTimeStamp(m_PaymentExpiryDate.Value), terminals))
                throw new InvalidDataException("The license contents could not be generated.");

            string encrypted = EncrptyFile.EncryptDES(content, EncrptyFile.FilePassword);
            LegacyLicenseStore.SaveGenerated(path, encrypted);
            m_LicenseCode.Text = productCode;
        }

        public static string GetLicenseCode(string hashCode, string factor)
        {
            return EncrptyFile.GetMd5Hash(hashCode + factor);
        }
    }
}
