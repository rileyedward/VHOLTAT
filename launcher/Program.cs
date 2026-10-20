using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VHOLTAT.Launcher
{
    /// <summary>
    /// VHOLTAT-Launcher.exe                     opens the launcher window
    /// VHOLTAT-Launcher.exe &lt;command&gt;           runs one step without the window and exits:
    ///   status                                 what's found and installed
    ///   install                                install VHOLTAT and BepInEx, or update VHOLTAT in place, and prepare the STRAFTAT folder
    ///   play [--dry-run]                       start STRAFTAT with VHOLTAT
    ///   play-vanilla [--dry-run]               start plain STRAFTAT through Steam
    ///   uninstall [--keep-config]
    /// Options: --zip &lt;file&gt; (install the plugin from this release zip), --home &lt;folder&gt; (instead of
    /// %APPDATA%\VHOLTAT), --straftat &lt;folder&gt;, --steam &lt;folder&gt; (Steam's folder, instead of the registry's).
    /// </summary>
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            string command = null, zip = null, home = null, straftat = null, steam = null;
            bool keepConfig = false, dryRun = false;
            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i].ToLowerInvariant();
                switch (a)
                {
                    case "--zip": case "--home": case "--straftat": case "--steam":
                        if (i + 1 >= args.Length) return Fail($"{args[i]} needs a value.");
                        var value = args[++i];
                        if (a == "--zip") zip = value; else if (a == "--home") home = value; else if (a == "--steam") steam = value; else straftat = value;
                        break;
                    case "--keep-config": keepConfig = true; break;
                    case "--dry-run": dryRun = true; break;
                    default:
                        if (command == null && !args[i].StartsWith("-")) command = a;
                        else return Fail($"Unknown argument: {args[i]}");
                        break;
                }
            }
            var paths = new LauncherPaths(home ?? LauncherPaths.DefaultHome);

            if (command == null)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(paths, straftat, zip, steam));
                return 0;
            }

            AttachConsole(-1);
            Console.WriteLine();
            using var launcher = new Launcher(paths, Console.WriteLine, straftat, steam);
            launcher.CommandLine = true;
            try
            {
                Run(launcher, command, zip, keepConfig, dryRun).GetAwaiter().GetResult();
                return 0;
            }
            catch (Exception e)
            {
                launcher.LogError(e);
                return 1;
            }
        }

        private static async Task Run(Launcher launcher, string command, string zip, bool keepConfig, bool dryRun)
        {
            launcher.Find();
            switch (command)
            {
                case "status":
                    await launcher.CheckForUpdates();
                    var state = launcher.State;
                    var update = launcher.ModStatus switch
                    {
                        ModStatus.Older => $" (update to {launcher.BestVersion.ToString(3)} available: run install)",
                        ModStatus.Newer => $" (newer than the {launcher.BestVersion.ToString(3)} this launcher carries; kept)",
                        _ => "",
                    };
                    var steamState = launcher.SteamPath == null ? "not found" : $"{launcher.SteamPath} ({(Steam.IsSteamRunning(launcher.SteamPath) ? "running" : "not running")})";
                    var gameFiles = state.GameFiles.Count > 0
                        ? string.Join(", ", state.GameFiles.Select(f => f.Name)) + " in " + state.GameFilesDir
                        : "none";
                    Console.WriteLine($"Launcher:   {LauncherConfig.LauncherVersion.ToString(3)} (carries VHOLTAT {LauncherConfig.BundledVersion?.ToString(3) ?? "none"})");
                    Console.WriteLine($"Home:       {launcher.Paths.Home}");
                    Console.WriteLine($"Steam:      {steamState}");
                    Console.WriteLine($"STRAFTAT:   {launcher.StraftatPath ?? "not found"}");
                    Console.WriteLine($"VHOLTAT:    {(launcher.IsInstalled ? "installed " + state.ModVersion : "not installed")}{update}");
                    Console.WriteLine($"Game files: {gameFiles}");
                    Console.WriteLine($"GitHub:     {(LauncherConfig.HasGitHub ? launcher.Latest?.Version.ToString(3) ?? launcher.UpdateCheckError : "no repo set, update check off")}");
                    break;
                case "install":
                case "update":
                    if (zip != null) await launcher.InstallFromZip(zip); else await launcher.Install();
                    break;
                case "play":
                    launcher.Play(dryRun);
                    break;
                case "play-vanilla":
                    launcher.PlayVanilla(dryRun);
                    break;
                case "uninstall":
                    launcher.Uninstall(keepConfig);
                    break;
                default:
                    throw new ArgumentException($"Unknown command '{command}'. Use status, install, play, play-vanilla or uninstall.");
            }
        }

        private static int Fail(string message)
        {
            AttachConsole(-1);
            Console.WriteLine();
            Console.WriteLine(message);
            return 2;
        }

        [DllImport("kernel32.dll")]
        private static extern bool AttachConsole(int processId);
    }
}
