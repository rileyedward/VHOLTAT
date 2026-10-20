using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace VHOLTAT.Launcher
{
    /// <summary>Finds Steam and STRAFTAT, tells whether they're running, and starts STRAFTAT through Steam.</summary>
    public static class Steam
    {
        public const string StoreUrl = "https://store.steampowered.com/app/" + LauncherConfig.StraftatAppId + "/";
        public const string SteamDownloadUrl = "https://store.steampowered.com/about/";

        /// <summary>Steam's install folder from the registry, or null.</summary>
        public static string FindSteam()
        {
            var candidates = new[]
            {
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null) as string,
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string,
            };
            foreach (var candidate in candidates)
            {
                if (string.IsNullOrEmpty(candidate)) continue;
                var path = Path.GetFullPath(candidate.Replace('/', '\\'));
                if (IsSteamFolder(path)) return path;
            }
            return null;
        }

        public static bool IsSteamFolder(string dir) =>
            !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "steam.exe"));

        /// <summary>Every Steam library folder listed in steamapps\libraryfolders.vdf, starting with Steam's own.</summary>
        public static List<string> LibraryFolders(string steam)
        {
            var folders = new List<string> { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                // "path"  "D:\\SteamLibrary" (current format); older files used "1" "D:\\SteamLibrary".
                foreach (Match match in Regex.Matches(File.ReadAllText(vdf), "^\\s*\"(?:path|\\d+)\"\\s+\"([^\"]+)\"", RegexOptions.Multiline))
                {
                    var path = match.Groups[1].Value.Replace("\\\\", "\\");
                    if (Directory.Exists(path)) folders.Add(path);
                }
            }
            return folders.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>A game's folder in any Steam library (from its app manifest), or null.</summary>
        private static string FindGame(string steam, string appId, string defaultDir, Func<string, bool> isGame)
        {
            if (steam == null) return null;
            foreach (var library in LibraryFolders(steam))
            {
                var apps = Path.Combine(library, "steamapps");
                var installDir = defaultDir;
                var manifest = Path.Combine(apps, $"appmanifest_{appId}.acf");
                if (File.Exists(manifest))
                {
                    var match = Regex.Match(File.ReadAllText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                    if (match.Success) installDir = match.Groups[1].Value;
                }
                var dir = Path.Combine(apps, "common", installDir);
                if (isGame(dir)) return dir;
            }
            return null;
        }

        public static string FindStraftat(string steam) => FindGame(steam, LauncherConfig.StraftatAppId, "STRAFTAT", IsStraftatFolder);

        public static bool IsStraftatFolder(string dir) =>
            !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "STRAFTAT.exe"));

        /// <summary>A STRAFTAT.exe is running from one of these folders (or from anywhere, if none is given).</summary>
        public static bool IsStraftatRunning(params string[] dirs) => IsRunning("STRAFTAT", dirs);

        /// <summary>Steam's steam.exe is running (the one in this folder, if given).</summary>
        public static bool IsSteamRunning(string steam) => IsRunning("steam", steam);

        /// <summary>A process with this name runs from one of the folders. A process whose path can't be read counts,
        /// so the answer errs towards "running".</summary>
        private static bool IsRunning(string name, params string[] dirs)
        {
            var folders = dirs.Where(d => !string.IsNullOrEmpty(d)).Select(d => Path.GetFullPath(d).TrimEnd('\\')).ToList();
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    if (folders.Count == 0) return true;
                    var image = ImagePath(process);
                    if (image == null) return true;
                    var folder = Path.GetDirectoryName(image)?.TrimEnd('\\');
                    if (folders.Any(f => string.Equals(f, folder, StringComparison.OrdinalIgnoreCase))) return true;
                }
            }
            return false;
        }

        private static string ImagePath(Process process)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, process.Id);
                if (handle == IntPtr.Zero) return null;
                var buffer = new StringBuilder(1024);
                var size = buffer.Capacity;
                return QueryFullProcessImageName(handle, 0, buffer, ref size) ? buffer.ToString(0, size) : null;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (handle != IntPtr.Zero) CloseHandle(handle);
            }
        }

        /// <summary>The Steam store page for an app: in the Steam client if Steam is there, otherwise in the browser.</summary>
        public static void OpenStorePage(string steam)
        {
            Open(steam != null ? $"steam://store/{LauncherConfig.StraftatAppId}" : StoreUrl);
        }

        /// <summary>Starts the Steam client on its own (no game).</summary>
        public static void StartSteam(string steam)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(steam, "steam.exe")) { UseShellExecute = false, WorkingDirectory = steam });
        }

        /// <summary>Runs steam.exe with these arguments (-applaunch ...).</summary>
        public static void LaunchStraftat(string steam, string arguments)
        {
            Process.Start(new ProcessStartInfo(Path.Combine(steam, "steam.exe"), arguments) { UseShellExecute = false, WorkingDirectory = steam });
        }

        public static void Open(string target)
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
    }
}
