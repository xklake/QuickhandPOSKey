using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace Quickhand.Licensing.Legacy
{
    public static class EncrptyFile
    {
        private static readonly byte[] Keys = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };
        public static string FilePassword = "ac1&3*XY";
        public static string LicenseFileName = "license.key";
        public static string EncrypSalt = "dc.quickhandpos.com";

        public static string DiskSeriesNumber { get; }
        public static string BiosInfo { get; }
        public static string FirstMACAddres { get; }
        public static string HardWareInfo;
        private static readonly object HardwareInfoLock = new object();

        /// <summary>
        /// DES加密字符串
        /// </summary>
        /// <param name="encryptString">待加密的字符串</param>
        /// <param name="encryptKey">加密密钥,要求为8位</param>
        /// <returns>Legacy-compatible ciphertext. Failures never return plaintext.</returns>
        public static string EncryptDES(string encryptString, string encryptKey)
        {
            if (encryptString == null) throw new ArgumentNullException(nameof(encryptString));
            using (var des = DES.Create())
            using (var encryptor = des.CreateEncryptor(GetLegacyKey(encryptKey), Keys))
            {
                byte[] data = Encoding.UTF8.GetBytes(encryptString);
                return Convert.ToBase64String(encryptor.TransformFinalBlock(data, 0, data.Length));
            }
        }

        /// <summary>
        /// DES解密字符串
        /// </summary>
        /// <param name="decryptString">待解密的字符串</param>
        /// <param name="decryptKey">解密密钥,要求为8位,和加密密钥相同</param>
        /// <returns>Plaintext, or an exception handled by the licensing UI.</returns>
        public static string DecryptDES(string decryptString, string decryptKey)
        {
            byte[] data = Convert.FromBase64String(decryptString);
            using (var des = DES.Create())
            using (var decryptor = des.CreateDecryptor(GetLegacyKey(decryptKey), Keys))
            {
                return new UTF8Encoding(false, true).GetString(decryptor.TransformFinalBlock(data, 0, data.Length));
            }
        }

        private static byte[] GetLegacyKey(string key)
        {
            if (key == null || key.Length < 8) throw new ArgumentException("The legacy key must contain eight UTF-8 bytes.");
            byte[] bytes = Encoding.UTF8.GetBytes(key.Substring(0, 8));
            if (bytes.Length != 8) throw new ArgumentException("The legacy key must contain eight UTF-8 bytes.");
            return bytes;
        }

        public static string GetProductCode(string hardwareCode)
        {
            return GetMd5Hash(GetMd5Hash(hardwareCode.Trim() + EncrypSalt) + EncrypSalt);
        }

        public static string GetMd5Hash(string input)
        {
            try
            {
                using (MD5 md5Hash = MD5.Create())
                {
                    // Convert the input string to a byte array and compute the hash.
                    byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

                    // Create a new Stringbuilder to collect the bytes
                    // and create a string.
                    StringBuilder sBuilder = new StringBuilder();

                    // Loop through each byte of the hashed data 
                    // and format each one as a hexadecimal string.
                    for (int i = 0; i < data.Length; i++)
                    {
                        sBuilder.Append(data[i].ToString("x2"));
                    }

                    // Return the hexadecimal string.
                    return sBuilder.ToString();
                }
            }
            catch (Exception ex)
            {
                
                System.Diagnostics.Trace.TraceError(ex.ToString());
                throw;
            }
        }

        public static string GetHardwareInfo()
        {
            // Startup and licence validation can arrive here on different threads.
            // Only detached query values leave HardwareQuery; publish the finished ID.
            lock (HardwareInfoLock)
            {
                if (!string.IsNullOrEmpty(HardWareInfo)) { return HardWareInfo; }
                HardWareInfo = GetMd5Hash(GetBiosInfo() + GetDiskSeriesNumber()).ToUpper().Substring(0, 16);
                return HardWareInfo;
            }
        }

        private static string GetBiosInfo()
        {
            if (!string.IsNullOrEmpty(BiosInfo)) { return BiosInfo; }

            try
            {
                var board = HardwareQuery.ReadFirst("SELECT Product, SerialNumber FROM Win32_BaseBoard", "SerialNumber");
                // Preserve the existing ID: do not trim the board serial or change its case.
                if (board != null) return board[0];
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
            }

            return BiosInfo;
        }

        private static string GetDiskSeriesNumber()
        {
            if (!string.IsNullOrEmpty(DiskSeriesNumber)) { return DiskSeriesNumber; }

            try
            {
                string system_disk = Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
                var partition = HardwareQuery.ReadFirst("ASSOCIATORS OF {Win32_LogicalDisk.DeviceID='" + system_disk + "'} WHERE ResultClass=Win32_DiskPartition", "DeviceID");
                if (partition != null)
                {
                    var disk = HardwareQuery.ReadFirst("ASSOCIATORS OF {Win32_DiskPartition.DeviceID='" + partition[0] + "'} WHERE ResultClass=Win32_DiskDrive", "Model", "InterfaceType", "Caption", "SerialNumber");
                    if (disk != null)
                    {
                        // Keep the same first system-disk association, required fields,
                        // serial trimming and minimum length as existing licences.
                        if (disk[0] == null || disk[1] == null || disk[2] == null || disk[3] == null)
                            return DiskSeriesNumber;
                        var serial = disk[3].Trim();
                        if (serial.Length > 6)
                        {
                            return serial;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError(ex.ToString());
            }

            return DiskSeriesNumber;
        }
    }
}
