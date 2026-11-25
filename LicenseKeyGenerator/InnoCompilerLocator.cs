// Shared by the vendor UI and Windows PowerShell (Add-Type). Keep C# 5 compatible.
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security;

namespace Quickhand.ReleaseBuild
{
    public static class InnoCompilerLocator
    {
        public static string Find(string explicitPath)
        {
            if (!string.IsNullOrWhiteSpace(explicitPath))
            {
                string result = ExistingCompiler(explicitPath);
                if (result == null) throw new FileNotFoundException("Select the Inno Setup command-line compiler ISCC.exe (not Compil32.exe). The selected file was not found.", explicitPath);
                return result;
            }
            var roots = new List<string>();
            // ProgramFiles points at x86 when a 32-bit issuer launches PowerShell.
            roots.Add(Environment.GetEnvironmentVariable("ProgramW6432"));
            roots.Add(Environment.GetEnvironmentVariable("ProgramFiles"));
            roots.Add(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
            roots.Add(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86));
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(local)) roots.Add(Path.Combine(local, "Programs"));
            string search = Environment.GetEnvironmentVariable("PATH") ?? "";
            return FindInLocations(roots, InstalledLocations(), search.Split(Path.PathSeparator));
        }

        // Deterministic resolver also used by isolated fixtures. No executables are run here.
        public static string FindInLocations(IEnumerable<string> roots, IEnumerable<string> installLocations, IEnumerable<string> searchDirectories)
        {
            foreach (string root in roots)
            {
                if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root)) continue;
                foreach (string version in new[] { "7", "6" })
                {
                    string found = ExistingCompiler(Path.Combine(root, "Inno Setup " + version, "ISCC.exe"));
                    if (found != null) return found;
                }
            }
            foreach (var directories in new[] { installLocations, searchDirectories })
                foreach (string directory in directories)
                {
                    if (string.IsNullOrWhiteSpace(directory)) continue;
                    string path = directory.Trim().Trim('"');
                    if (!Path.IsPathRooted(path)) continue;
                    string found = ExistingCompiler(Path.Combine(path, "ISCC.exe"));
                    if (found != null) return found;
                }
            return null;
        }

        private static string ExistingCompiler(string path)
        {
            try
            {
                return string.Equals(Path.GetFileName(path), "ISCC.exe", StringComparison.OrdinalIgnoreCase) && File.Exists(path) ? Path.GetFullPath(path) : null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (IOException) { return null; }
            catch (SecurityException) { return null; }
        }

        private static IEnumerable<string> InstalledLocations()
        {
            var paths = new List<string>();
            foreach (RegistryHive hive in new[] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
                    foreach (string version in new[] { "7", "6" })
                    {
                        try
                        {
                            using (RegistryKey root = RegistryKey.OpenBaseKey(hive, view))
                            using (RegistryKey key = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Inno Setup " + version + "_is1"))
                            {
                                if (key == null) continue;
                                string location = key.GetValue("InstallLocation") as string;
                                if (!string.IsNullOrWhiteSpace(location)) paths.Add(location);
                            }
                        }
                        catch (SecurityException) { }
                        catch (UnauthorizedAccessException) { }
                        catch (IOException) { }
                    }
            return paths;
        }
    }
}
