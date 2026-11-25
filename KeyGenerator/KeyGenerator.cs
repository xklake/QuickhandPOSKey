using Quickhand.Licensing.Legacy;

using System;
using System.Windows.Forms;

namespace KeyGenerator
{
    public partial class KeyGenerator : Form
    {
        public KeyGenerator()
        {
            InitializeComponent();

            m_ActivitionCode.Text = EncrptyFile.GetHardwareInfo();
            CenterToScreen();
        }

        private void m_Generator_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(m_ActivitionCode.Text);
        }
    }
}
