using System;
using System.IO;
using System.Threading.Tasks;

namespace VHOLTAT.Launcher
{
    /// <summary>What the window and the command line both do: find STRAFTAT, install, play, update and uninstall.</summary>
    public sealed class Launcher : IDisposable
    {
        private readonly Action<string> log;

        public Launcher(LauncherPaths paths, Action<string> log, string straftatOverride = null)
        {
            Paths = paths;
            this.log = message =>
            {
                try
                {
                    Directory.CreateDirectory(paths.Home);
                    File.AppendAllText(paths.LogFile, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}\r\n");
                }
                catch (IOException) { }
                log(message);
            };
            Installer = new Installer(paths, this.log);
            Sources = new Sources(paths, this.log);
            Installer.LoadState();
            if (straftatOverride != null) StraftatOverride = Path.GetFullPath(straftatOverride);
        }

        public LauncherPaths Paths { get; }
        public Installer Installer { get; }
        public Sources Sources { get; }
        public LauncherState State => Installer.State;
        public string SteamPath { get; private set; }
        public string StraftatPath { get; private set; }
        public string StraftatOverride { get; }
        public ModRelease Latest { get; private set; }
        public string UpdateCheckError { get; private set; }

        public void Dispose() => Sources.Dispose();

        public void Find()
        {
            Installer.LoadState();
            SteamPath = Steam.FindSteam();
            StraftatPath = Steam.IsStraftatFolder(StraftatOverride) ? StraftatOverride
                : Steam.IsStraftatFolder(State.StraftatPath) ? State.StraftatPath : Steam.FindStraftat(SteamPath);
        }

        public Version InstalledVersion => Sources.ParseVersion(State.ModVersion);

        /// <summary>Installed, and the profile's files are all there.</summary>
        public bool IsInstalled => State.IsInstalled && File.Exists(Paths.PluginDll) && File.Exists(Paths.Preloader);

        /// <summary>The plugin version Install would put in: GitHub's if a newer release is there, otherwise the one inside this exe.</summary>
        public Version BestVersion => Latest != null && Latest.Version > LauncherConfig.BundledVersion ? Latest.Version : LauncherConfig.BundledVersion;

        public bool UpdateAvailable => IsInstalled && BestVersion != null && BestVersion > InstalledVersion;
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
            if (UpdateCheckError != null) log(UpdateCheckError);
        }

        /// <summary>Installs or updates to the best available plugin, then prepares the STRAFTAT folder.</summary>
        public async Task Install()
        {
            EnsureGameClosed();
            if (LauncherConfig.HasGitHub && Latest == null) await CheckForUpdates();
            if (Latest != null && Latest.Version > LauncherConfig.BundledVersion)
            {
                var zip = await Sources.DownloadModZip(Latest);
                await Installer.Install(Installer.PluginFromZip(zip), $"{Latest.Version.ToString(3)} from GitHub", Sources);
            }
            else
            {
                var bundled = LauncherConfig.BundledVersion?.ToString(3) ?? "?";
                await Installer.Install(LauncherConfig.BundledPlugin(), $"{bundled}, bundled with the launcher", Sources);
            }
            PrepareGame();
        }

        public async Task InstallFromZip(string zip)
        {
            EnsureGameClosed();
            zip = Path.GetFullPath(zip);
            await Installer.Install(Installer.PluginFromZip(zip), Path.GetFileName(zip), Sources);
            PrepareGame();
        }

        private void PrepareGame()
        {
            Find();
            if (StraftatPath == null)
            {
                log("STRAFTAT wasn't found; its folder is prepared once you pick it.");
                return;
            }
            Installer.PrepareGameFolder(StraftatPath);
        }

        public void Play(bool dryRun = false)
        {
            if (StraftatPath == null) throw new InvalidOperationException("STRAFTAT wasn't found.");
            EnsureGameClosed();
            var args = Installer.PrepareGameFolder(StraftatPath);
            if (dryRun)
            {
                log($"Dry run, not starting STRAFTAT. Steam arguments: -applaunch {LauncherConfig.StraftatAppId} {args}");
                return;
            }
            log("Starting STRAFTAT with VHOLTAT");
            Steam.LaunchStraftat(SteamPath, args);
        }

        public void Uninstall(bool keepConfig)
        {
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

        private static void EnsureGameClosed()
        {
            if (Steam.IsStraftatRunning()) throw new InvalidOperationException("STRAFTAT is running. Close it first.");
        }
    }
}
