using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace VHOLTAT.Launcher
{
    /// <summary>A pinned download: one exact file, checked against its SHA-256 before it's used.</summary>
    public sealed class Pinned
    {
        public Pinned(string file, string sha256, params string[] urls)
        {
            File = file;
            Sha256 = sha256;
            Urls = urls;
        }

        public string File { get; }
        public string Sha256 { get; }
        /// <summary>Tried in order (mirrors).</summary>
        public string[] Urls { get; }
    }

    /// <summary>Fixed settings: the game, the bundled plugin, the update source and the BepInEx download.</summary>
    public static class LauncherConfig
    {
        /// <summary>
        /// GitHub "owner/repo" to check for newer VHOLTAT releases. Empty: no update check, the launcher installs the
        /// plugin it carries. Riley sets this when he creates the repo.
        /// </summary>
        public const string GitHubRepo = "";

        public static bool HasGitHub => GitHubRepo.Length > 0;

        public const string StraftatAppId = "2386720";

        /// <summary>Release assets: VHOLTAT-&lt;ver&gt;-manual.zip (BepInEx\plugins\VHOLTAT\VHOLTAT.dll) and the launcher exe.</summary>
        public const string ModZipPrefix = "VHOLTAT-";
        public const string ModZipSuffix = "-manual.zip";
        public const string LauncherAssetPrefix = "VHOLTAT-Launcher";

        public const string PluginFile = "VHOLTAT.dll";
        public const string PluginResource = "VHOLTAT.dll";
        public const string PluginGuid = "dev.riley.vholtat";

        public static string ReleasesPage => HasGitHub ? $"https://github.com/{GitHubRepo}/releases" : null;

        public static Version LauncherVersion => typeof(LauncherConfig).Assembly.GetName().Version;

        /// <summary>Version of the plugin embedded in this exe (from its assembly version, stamped in at build time).</summary>
        public static Version BundledVersion
        {
            get
            {
                var text = typeof(LauncherConfig).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(a => a.Key == "PluginVersion")?.Value;
                return Version.TryParse(text, out var v) ? new Version(v.Major, v.Minor, Math.Max(v.Build, 0)) : null;
            }
        }

        /// <summary>The embedded plugin DLL.</summary>
        public static byte[] BundledPlugin()
        {
            using var stream = typeof(LauncherConfig).Assembly.GetManifestResourceStream(PluginResource)
                ?? throw new InvalidOperationException("This launcher was built without the VHOLTAT plugin inside.");
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            return memory.ToArray();
        }

        // --- Third-party download, pinned to the version the mod is built and tested with ---

        public static readonly Pinned BepInEx = new Pinned("BepInEx_win_x64_5.4.23.5.zip",
            "82f9878551030f54657792c0740d9d51a09500eeae1fba21106b0c441e6732c4",
            "https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/BepInEx_win_x64_5.4.23.5.zip");
    }

    /// <summary>
    /// Where the launcher keeps things. Home is %APPDATA%\VHOLTAT unless --home overrides it:
    ///   profile\      BepInEx, its doorstop files, the plugin and its config
    ///   cache\        downloads, so a reinstall doesn't download again
    ///   game-backup\  anything of STRAFTAT's the launcher replaced
    ///   state.json, launcher.log
    /// </summary>
    public sealed class LauncherPaths
    {
        public LauncherPaths(string home)
        {
            Home = Path.GetFullPath(home);
        }

        public static string DefaultHome =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VHOLTAT");

        public string Home { get; }
        public string Profile => Path.Combine(Home, "profile");
        public string Cache => Path.Combine(Home, "cache");
        public string Backup => Path.Combine(Home, "game-backup");
        public string StateFile => Path.Combine(Home, "state.json");
        public string LogFile => Path.Combine(Home, "launcher.log");

        /// <summary>Doorstop's loader as BepInEx ships it; copied into the STRAFTAT folder.</summary>
        public string ProfileLoader => Path.Combine(Profile, "winhttp.dll");
        public string BepInEx => Path.Combine(Profile, "BepInEx");
        public string Preloader => Path.Combine(BepInEx, "core", "BepInEx.Preloader.dll");
        public string Config => Path.Combine(BepInEx, "config");
        public string Plugins => Path.Combine(BepInEx, "plugins");
        public string PluginDir => Path.Combine(Plugins, "VHOLTAT");
        public string PluginDll => Path.Combine(PluginDir, LauncherConfig.PluginFile);
        public string ModConfig => Path.Combine(Config, LauncherConfig.PluginGuid + ".cfg");
        public string BepInExLog => Path.Combine(BepInEx, "LogOutput.log");
    }
}
