using Quickhand.Licensing.Legacy;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Quickhand.Licensing
{
    public sealed class LicenseIdentity
    {
        public string DeviceBinding { get; private set; }
        public string InstallationId { get; private set; }

        public LicenseIdentity(string hardwareCode, string machineGuid)
        {
            Guid parsed;
            if (hardwareCode == null || hardwareCode.Length != 16 || !hardwareCode.All(Uri.IsHexDigit) ||
                hardwareCode.Equals(EncrptyFile.GetMd5Hash(string.Empty).Substring(0, 16), StringComparison.OrdinalIgnoreCase) ||
                !Guid.TryParse(machineGuid, out parsed) || parsed == Guid.Empty)
                throw new InvalidDataException("A reliable computer identity could not be read. Retry or contact Quickhand POS.");
            DeviceBinding = Hash("QuickhandPOS/device/v2|" + hardwareCode.ToUpperInvariant() + "|" + parsed.ToString("D"));
            InstallationId = "QH2-" + Hash("QuickhandPOS/installation/v2|" + DeviceBinding).Substring(0, 32).ToUpperInvariant();
        }

        public static LicenseIdentity Capture()
        {
            using (var registry = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32))
            using (var key = registry.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography"))
                return new LicenseIdentity(EncrptyFile.GetHardwareInfo(), key?.GetValue("MachineGuid") as string);
        }

        public JObject CreateRequest(string currentLicenseId = "", int revision = 0)
        {
            return new JObject { ["schemaVersion"] = 2, ["product"] = "QuickhandPOS", ["requestId"] = Guid.NewGuid().ToString("D"),
                ["installationId"] = InstallationId, ["deviceBinding"] = DeviceBinding, ["requestedAt"] = LicenseJson.TimeText(DateTime.UtcNow),
                ["currentLicenseId"] = currentLicenseId ?? "", ["currentRevision"] = revision };
        }

        public static string Hash(string text)
        {
            using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
