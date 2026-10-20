using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace VHOLTAT.Launcher
{
    /// <summary>How the installed VHOLTAT compares with the one Install would put in.</summary>
    public enum ModStatus { NotInstalled, Older, Current, Newer }

    /// <summary>What the window and the command line both do: find STRAFTAT, install, play, update and uninstall.</summary>
    public sealed class Launcher : IDisposable
    {
        /// <summary>True when run from the command line; fix suggestions then name options instead of buttons.</summary>
        public bool CommandLine { get; set; }

        public string StraftatNotFoundFix => CommandLine
            ? "Pass --straftat <folder> (the one with STRAFTAT.exe), or install STRAFTAT through Steam."
            : "Use Browse to pick its folder (the one with STRAFTAT.exe), or press Get STRAFTAT.";

        private readonly Action<string> sink;

        public Launcher(LauncherPaths paths, Action<string> log, string straftatOverride = null, string steamOverride = null)
        {
            Paths = paths;
            sink = log;
            Installer = new Installer(paths, Log);
            Sources = new Sources(paths, Log);
            Installer.LoadState();
            if (straftatOverride != null) StraftatOverride = Path.GetFullPath(straftatOverride);
            if (steamOverride != null) SteamOverride = Path.GetFullPath(steamOverride);
        }

        public LauncherPaths Paths { get; }
        public Installer Installer { get; }
        public Sources Sources { get; }
        public LauncherState State => Installer.State;
        public string SteamPath { get; private set; }
        public string StraftatPath { get; private set; }
        public string StraftatOverride { get; }
        public string SteamOverride { get; }
        public ModRelease Latest { get; private set; }
        public string UpdateCheckError { get; private set; }

        public void Dispose() => Sources.Dispose();

        /// <summary>Writes a line to launcher.log and to the window or console.</summary>
        public void Log(string message)
        {
            try
            {
                Directory.CreateDirectory(Paths.Home);
                File.AppendAllText(Paths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\r\n");
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { }
            sink(message);
        }

        /// <summary>Logs a problem and its fix as two lines.</summary>
        public void LogError(Exception e)
        {
            if (e is LauncherError error)
            {
                Log($"Error: {error.Message}");
                if (!string.IsNullOrEmpty(error.Fix)) Log($"  Fix: {error.Fix}");
            }
            else Log($"Error: {e.GetBaseException().Message}");
        }

        public void Find()
        {
            Installer.LoadState();
            SteamPath = SteamOverride != null ? (Steam.IsSteamFolder(SteamOverride) ? SteamOverride : null) : Steam.FindSteam();
            StraftatPath = Steam.IsStraftatFolder(StraftatOverride) ? StraftatOverride
                : Steam.IsStraftatFolder(State.StraftatPath) ? State.StraftatPath : Steam.FindStraftat(SteamPath);
        }

        public Version InstalledVersion => Sources.ParseVersion(State.ModVersion);

        /// <summary>Installed, and the profile's files are all there.</summary>
        public bool IsInstalled => State.IsInstalled && File.Exists(Paths.PluginDll) && File.Exists(Paths.Preloader);

        /// <summary>The plugin version Install would put in: GitHub's if a newer release is there, otherwise the one inside this exe.</summary>
        public Version BestVersion => Latest != null && Latest.Version > LauncherConfig.BundledVersion ? Latest.Version : LauncherConfig.BundledVersion;

        public ModStatus ModStatus
        {
            get
            {
                if (!IsInstalled) return ModStatus.NotInstalled;
                var installed = InstalledVersion;
                var best = BestVersion;
                if (installed == null || best == null) return ModStatus.Current;
                return installed < best ? ModStatus.Older : installed > best ? ModStatus.Newer : ModStatus.Current;
            }
        }

        public bool UpdateAvailable => ModStatus == ModStatus.Older;
        public bool LauncherUpdateAvailable => Latest?.LauncherUrl != null && Latest.Version > LauncherConfig.LauncherVersion;
        public bool ReadyToPlay => StraftatPath != null && IsInstalled && !UpdateAvailable;

        /// <summary>Checks GitHub for a newer release. Does nothing until LauncherConfig.GitHubRepo is set.</summary>
        public async Task CheckForUpdates()
        {
            if (!LauncherConfig.HasGitHub) return;
            try
            {
                Latest = await Sources.LatestModRelease();
                UpdateCheckError = Latest == null ? $"No VHOLTAT release found on GitHub ({LauncherConfig.GitHubRepo})." : null;
            }
            catch (Exception e)
            {
                Latest = null;
                UpdateCheckError = $"Couldn't check GitHub for updates: {e.GetBaseException().Message}";
            }
            if (UpdateCheckError != null) Log(UpdateCheckError);
        }

        /// <summary>
        /// Installs VHOLTAT, or updates it in place: the plugin DLL is replaced, BepInEx and the settings file stay, and
        /// the STRAFTAT folder's files are checked again. A newer VHOLTAT than this launcher has is kept as it is.
        /// Returns what happened ("installed 0.3.0", "updated 0.2.0 -> 0.3.0", ...).
        /// </summary>
        public async Task<string> Install()
        {
            Find();
            EnsureGameClosed();
            if (LauncherConfig.HasGitHub && Latest == null) await CheckForUpdates();
            if (ModStatus == ModStatus.Newer)
            {
                Log($"VHOLTAT {State.ModVersion} is installed, newer than the {BestVersion.ToString(3)} this launcher carries; keeping it.");
                PrepareGame();
                return Report($"kept {State.ModVersion}");
            }
            var before = IsInstalled ? State.ModVersion : null;
            string after;
            if (Latest != null && Latest.Version > LauncherConfig.BundledVersion)
            {
                var zip = await Sources.DownloadModZip(Latest);
                after = await Installer.Install(Installer.PluginFromZip(zip), $"{Latest.Version.ToString(3)} from GitHub", Sources);
            }
            else
            {
                var bundled = LauncherConfig.BundledVersion?.ToString(3) ?? "?";
                after = await Installer.Install(LauncherConfig.BundledPlugin(), $"{bundled}, bundled with the launcher", Sources);
            }
            PrepareGame();
            return Report(Change(before, after));
        }

        public async Task<string> InstallFromZip(string zip)
        {
            Find();
            EnsureGameClosed();
            zip = Path.GetFullPath(zip);
            var before = IsInstalled ? State.ModVersion : null;
            var after = await Installer.Install(Installer.PluginFromZip(zip), Path.GetFileName(zip), Sources);
            PrepareGame();
            return Report(Change(before, after));
        }

        private static string Change(string before, string after)
        {
            if (before == null) return $"installed {after}";
            var a = Sources.ParseVersion(before);
            var b = Sources.ParseVersion(after);
            if (a == null || b == null || a == b) return $"reinstalled {after}";
            return a < b ? $"updated {before} -> {after}" : $"downgraded {before} -> {after}";
        }

        private string Report(string what)
        {
            Log($"VHOLTAT {what}.");
            return what;
        }

        private void PrepareGame()
        {
            Find();
            if (StraftatPath == null)
            {
                Log("STRAFTAT wasn't found, so its folder isn't prepared yet (that happens once you pick it).");
                Log($"  Fix: {StraftatNotFoundFix}");
                return;
            }
            Installer.PrepareGameFolder(StraftatPath);
        }

        /// <summary>Starts STRAFTAT with VHOLTAT. A dry run makes every check and prepares the folder, but starts nothing.</summary>
        public void Play(bool dryRun = false) => Start(modded: true, dryRun);

        /// <summary>Starts plain STRAFTAT through Steam: no doorstop arguments, so the game folder's doorstop stays off.</summary>
        public void PlayVanilla(bool dryRun = false) => Start(modded: false, dryRun);

        private void Start(bool modded, bool dryRun)
        {
            Find();
            if (StraftatPath == null) throw new LauncherError("STRAFTAT wasn't found.", StraftatNotFoundFix);
            if (SteamPath == null)
                throw new LauncherError("Steam wasn't found, so STRAFTAT can't be started.",
                    $"Install Steam ({Steam.SteamDownloadUrl}) and sign in; if it's installed, start it once so Windows knows where it is.");
            EnsureGameClosed();
            if (!Steam.IsSteamRunning(SteamPath)) throw new SteamNotRunningError();

            var arguments = $"-applaunch {LauncherConfig.StraftatAppId}";
            if (modded)
            {
                if (!IsInstalled) throw new LauncherError("VHOLTAT isn't installed yet.", "Press Install (or run: install) first.");
                if (UpdateAvailable) Log($"VHOLTAT {BestVersion.ToString(3)} is available; Install updates it (playing {State.ModVersion} now).");
                arguments += " " + Installer.PrepareGameFolder(StraftatPath);
            }
            else CheckVanilla();

            if (dryRun)
            {
                Log($"Dry run, not starting STRAFTAT. Steam arguments: {arguments}");
                return;
            }
            Log(modded ? $"Starting STRAFTAT with VHOLTAT {State.ModVersion}" : "Starting plain STRAFTAT (vanilla, no VHOLTAT)");
            Steam.LaunchStraftat(SteamPath, arguments);
        }

        /// <summary>Warns if something in the game folder would still load BepInEx on a plain launch.</summary>
        private void CheckVanilla()
        {
            if (Directory.Exists(Path.Combine(StraftatPath, "BepInEx", "core")))
                Log("Note: STRAFTAT's folder has its own BepInEx, which loads on every launch; that isn't the launcher's.");
            var ini = Path.Combine(StraftatPath, "doorstop_config.ini");
            if (File.Exists(ini) && Regex.IsMatch(File.ReadAllText(ini), @"^\s*enabled\s*=\s*true", RegexOptions.Multiline | RegexOptions.IgnoreCase))
                Log("Note: STRAFTAT's doorstop_config.ini has enabled = true, so mods may load. Install (or Uninstall) resets the launcher's copy to off.");
        }

        public void Uninstall(bool keepConfig)
        {
            Find();
            EnsureGameClosed();
            Installer.Uninstall(keepConfig);
        }

        public void SetStraftatPath(string path)
        {
            Installer.LoadState();
            State.StraftatPath = path;
            Installer.SaveState();
            Find();
        }

        /// <summary>Moves the launcher's two game files to the current STRAFTAT folder, if VHOLTAT is installed.</summary>
        public void MoveGameFiles()
        {
            if (!IsInstalled || StraftatPath == null) return;
            EnsureGameClosed();
            Installer.PrepareGameFolder(StraftatPath);
        }

        /// <summary>STRAFTAT running from the folder the launcher would touch (or from anywhere, if no folder is known).</summary>
        private void EnsureGameClosed()
        {
            if (Steam.IsStraftatRunning(StraftatPath, State.GameFilesDir)) throw new GameRunningError();
        }
    }
}
