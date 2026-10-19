using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VHOLTAT.Launcher
{
    /// <summary>
    /// Installs BepInEx and VHOLTAT into the launcher's profile and prepares the STRAFTAT folder for launching
    /// from it. Plain STRAFTAT stays as it was: the only files added to the game folder are doorstop's loader
    /// (winhttp.dll) and a doorstop_config.ini that keeps it switched off unless the launcher's Play turns it on.
    /// </summary>
    public sealed class Installer
    {
        private readonly LauncherPaths paths;
        private readonly Action<string> log;

        public Installer(LauncherPaths paths, Action<string> log)
        {
            this.paths = paths;
            this.log = log;
        }

        public LauncherState State { get; private set; }

        public void LoadState() => State = LauncherState.Load(paths.StateFile);

        public void SaveState() => State.Save(paths.StateFile);

        /// <summary>Installs or updates VHOLTAT from the plugin DLL's bytes (BepInEx first, from its pinned download).
        /// Returns the installed version, read from the DLL's assembly version.</summary>
        public async Task<string> Install(byte[] plugin, string from, Sources sources)
        {
            LoadState();
            log($"Installing VHOLTAT ({from}) into {paths.Profile}");
            Directory.CreateDirectory(paths.Profile);

            if (!File.Exists(paths.Preloader) || !State.Installed.Contains(LauncherConfig.BepInEx.File))
            {
                var bepinex = await sources.Get(LauncherConfig.BepInEx);
                ExtractAll(bepinex, paths.Profile, keepPrefix: "BepInEx/config/");
                if (!State.Installed.Contains(LauncherConfig.BepInEx.File)) State.Installed.Add(LauncherConfig.BepInEx.File);
                SaveState();
                log("BepInEx installed");
            }

            // Replace the plugin folder so an update leaves no stale files. The config lives in BepInEx\config and stays.
            DeleteDirectory(paths.PluginDir);
            Directory.CreateDirectory(paths.PluginDir);
            File.WriteAllBytes(paths.PluginDll, plugin);
            var version = AssemblyName.GetAssemblyName(paths.PluginDll).Version;
            State.ModVersion = new Version(version.Major, version.Minor, Math.Max(version.Build, 0)).ToString(3);
            SaveState();
            log($"VHOLTAT {State.ModVersion} installed");
            return State.ModVersion;
        }

        /// <summary>The VHOLTAT.dll inside a release zip (the manual zip: BepInEx\plugins\VHOLTAT\VHOLTAT.dll).</summary>
        public static byte[] PluginFromZip(string zipPath)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            var entry = zip.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/').EndsWith("/" + LauncherConfig.PluginFile, StringComparison.OrdinalIgnoreCase)
                                                        || e.FullName.Equals(LauncherConfig.PluginFile, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidDataException($"{Path.GetFileName(zipPath)} has no {LauncherConfig.PluginFile}, so it isn't a VHOLTAT release zip.");
            using var stream = entry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        /// <summary>Unpacks a zip into a folder; files under <paramref name="keepPrefix"/> are never overwritten.</summary>
        public static void ExtractAll(string zipPath, string root, string keepPrefix = null, string stripPrefix = null)
        {
            using var zip = ZipFile.OpenRead(zipPath);
            foreach (var entry in zip.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (name.EndsWith("/")) continue;
                if (stripPrefix != null)
                {
                    if (!name.StartsWith(stripPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                    name = name.Substring(stripPrefix.Length);
                }
                var keep = keepPrefix != null && name.StartsWith(keepPrefix, StringComparison.OrdinalIgnoreCase);
                Extract(entry, root, name, overwrite: !keep);
            }
        }

        private static void Extract(ZipArchiveEntry entry, string root, string relative, bool overwrite)
        {
            var rootFull = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            var target = Path.GetFullPath(Path.Combine(rootFull, relative.Replace('/', '\\')));
            if (!target.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Zip entry {entry.FullName} points outside the install folder.");
            if (!overwrite && File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            entry.ExtractToFile(target, overwrite: true);
        }

        /// <summary>
        /// Puts doorstop's loader in the STRAFTAT folder (switched off by its config file, so plain Steam launches stay
        /// vanilla) and returns the Steam launch arguments that switch it on and point it at the profile's BepInEx.
        /// </summary>
        public string PrepareGameFolder(string gameDir)
        {
            LoadState();
            if (!File.Exists(paths.Preloader)) throw new InvalidOperationException("VHOLTAT isn't installed yet.");
            var packIni = Path.Combine(paths.Profile, "doorstop_config.ini");
            var packIniText = File.Exists(packIni) ? File.ReadAllText(packIni) : "";

            if (State.GameFilesDir != null && !string.Equals(State.GameFilesDir, gameDir, StringComparison.OrdinalIgnoreCase))
                RemoveGameFiles();
            State.GameFilesDir = gameDir;

            // A BepInEx installed straight into the game folder uses the same two files; leave them alone and rely on
            // the launch arguments instead.
            if (Directory.Exists(Path.Combine(gameDir, "BepInEx", "core")))
                log("STRAFTAT's folder has its own BepInEx; leaving its doorstop files alone.");
            else
            {
                PlaceGameFile(gameDir, "winhttp.dll", File.ReadAllBytes(Path.Combine(paths.Profile, "winhttp.dll")));
                PlaceGameFile(gameDir, "doorstop_config.ini", Encoding.UTF8.GetBytes(GameFolderIni(packIniText)));
            }
            SaveState();
            // BepInEx 5.4.23 ships doorstop 4.
            return $"--doorstop-enabled true --doorstop-target-assembly \"{paths.Preloader}\"";
        }

        /// <summary>The pack's doorstop config, switched off, with its paths made absolute into the profile.</summary>
        private string GameFolderIni(string packIni)
        {
            if (packIni.Length == 0)
                return $"[General]\r\nenabled = false\r\ntarget_assembly = {paths.Preloader}\r\n";
            var lines = Regex.Split(packIni, "\r?\n").Select(line =>
            {
                var match = Regex.Match(line, @"^(\s*)([A-Za-z_]+)(\s*=\s*)(.*)$");
                if (!match.Success) return line;
                var key = match.Groups[2].Value;
                var value = match.Groups[4].Value.Trim();
                if (key.Equals("enabled", StringComparison.OrdinalIgnoreCase)) value = "false";
                else if (IsPathKey(key) && value.Length > 0 && !Path.IsPathRooted(value))
                    value = Path.GetFullPath(Path.Combine(paths.Profile, value));
                return match.Groups[1].Value + key + match.Groups[3].Value + value;
            });
            return string.Join("\r\n", lines);
        }

        private static bool IsPathKey(string key)
        {
            var k = key.Replace("_", "").ToLowerInvariant();
            return k == "targetassembly" || k == "dllsearchpathoverride" || k == "bootconfigoverride";
        }

        private void PlaceGameFile(string gameDir, string name, byte[] content)
        {
            var target = Path.Combine(gameDir, name);
            var hash = Sha256(content);
            var record = State.GameFiles.FirstOrDefault(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (File.Exists(target) && Sources.Sha256(target) == hash)
            {
                if (record == null) State.GameFiles.Add(new GameFile { Name = name, Sha256 = hash, WasThere = true });
                return;
            }
            var backedUp = record?.BackedUp ?? false;
            if (File.Exists(target) && (record == null || Sources.Sha256(target) != record.Sha256))
            {
                Directory.CreateDirectory(paths.Backup);
                File.Copy(target, Path.Combine(paths.Backup, name), overwrite: true);
                backedUp = true;
                log($"Saved STRAFTAT's existing {name} to {paths.Backup}");
            }
            File.WriteAllBytes(target, content);
            State.GameFiles.RemoveAll(f => f.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            State.GameFiles.Add(new GameFile { Name = name, Sha256 = hash, BackedUp = backedUp });
            log($"Wrote {name} to the STRAFTAT folder");
        }

        /// <summary>Takes the launcher's files back out of the STRAFTAT folder, restoring any it replaced.</summary>
        public void RemoveGameFiles()
        {
            if (State.GameFilesDir == null) return;
            foreach (var file in State.GameFiles)
            {
                var target = Path.Combine(State.GameFilesDir, file.Name);
                var backup = Path.Combine(paths.Backup, file.Name);
                if (file.WasThere || (File.Exists(target) && Sources.Sha256(target) != file.Sha256)) continue;
                if (file.BackedUp && File.Exists(backup))
                {
                    File.Copy(backup, target, overwrite: true);
                    log($"Restored STRAFTAT's original {file.Name}");
                }
                else if (File.Exists(target))
                {
                    File.Delete(target);
                    log($"Removed {file.Name} from the STRAFTAT folder");
                }
            }
            State.GameFiles.Clear();
            State.GameFilesDir = null;
            DeleteDirectory(paths.Backup);
        }

        /// <summary>Removes everything the launcher installed. With keepConfig, VHOLTAT's settings file stays.</summary>
        public void Uninstall(bool keepConfig)
        {
            LoadState();
            RemoveGameFiles();
            string savedConfig = null;
            if (keepConfig && File.Exists(paths.ModConfig)) savedConfig = File.ReadAllText(paths.ModConfig);
            DeleteDirectory(paths.Profile);
            DeleteDirectory(paths.Cache);
            if (savedConfig != null)
            {
                Directory.CreateDirectory(paths.Config);
                File.WriteAllText(paths.ModConfig, savedConfig);
            }
            State = new LauncherState { StraftatPath = State.StraftatPath };
            SaveState();
            log("Uninstalled." + (savedConfig != null ? $" Settings kept in {paths.ModConfig}." : ""));
        }

        private static string Sha256(byte[] content)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(content)).Replace("-", "").ToLowerInvariant();
        }

        internal static void DeleteDirectory(string dir)
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, recursive: true);
        }
    }
}
