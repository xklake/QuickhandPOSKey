using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace LicenseGenerator
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            // Inno checks this marker before replacing or uninstalling a running tool.
            using (var running = new System.Threading.Mutex(false, @"Local\QuickhandPOS-LicenseKeyGenerator"))
            {
                Application.Run(args.Contains("--legacy") ? (Form)new License() : new SignedLicenseForm());
                GC.KeepAlive(running);
            }
        }
    }
}
