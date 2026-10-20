using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VHOLTAT.Launcher
{
    /// <summary>The launcher window: the STRAFTAT folder, a status line, one main button (Install, Update to X, Play),
    /// Play vanilla, Uninstall, Open folder and a log.</summary>
    public sealed class MainForm : Form
    {
        private const string VanillaTip =
            "Starts plain STRAFTAT through Steam, without VHOLTAT or BepInEx.\n\n" +
            "Starting STRAFTAT from Steam as usual is vanilla too: the launcher's\n" +
            "doorstop file in the game folder stays switched off, and only the\n" +
            "launcher's Play button turns it on, for that one launch.";

        private readonly Launcher launcher;
        private readonly string zipOverride;

        private readonly TextBox pathBox = new TextBox { ReadOnly = true, TabStop = false, Anchor = AnchorStyles.Left | AnchorStyles.Right, BackColor = SystemColors.Window };
        private readonly Button browseButton = new Button { Text = "Browse…", AutoSize = true, Anchor = AnchorStyles.Right };
        private readonly Label statusLabel = new Label { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, Height = 38, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Button mainButton = new Button { Width = 160, Height = 40, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
        private readonly Button vanillaButton = new Button { Text = "Play vanilla", Width = 100, Height = 40 };
        private readonly Button uninstallButton = new Button { Text = "Uninstall", Width = 90, Height = 40 };
        private readonly Button folderButton = new Button { Text = "Open folder", Width = 100, Height = 40 };
        private readonly ToolTip tips = new ToolTip { AutoPopDelay = 20000, InitialDelay = 400 };
        private readonly TextBox logBox = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, MinimumSize = new Size(0, 120),
            BackColor = SystemColors.Window, Font = new Font(FontFamily.GenericMonospace, 8.5f),
        };
        private bool busy;
        private string download;
        /// <summary>Last problem and its fix, shown in the status line until the next action.</summary>
        private string problem;
        /// <summary>A one-off message for the status line (e.g. "Starting Steam…"), cleared by the next action.</summary>
        private string notice;

        public MainForm(LauncherPaths paths, string straftatOverride, string zipOverride, string steamOverride = null)
        {
            this.zipOverride = zipOverride == null ? null : Path.GetFullPath(zipOverride);
            launcher = new Launcher(paths, AppendLog, straftatOverride, steamOverride);
            launcher.Sources.Progress = (file, done, total) => BeginInvoke(new Action(() =>
            {
                download = total > 0 ? $"Downloading {file}: {done * 100 / total}%" : $"Downloading {file}: {done / (1 << 20)} MB";
                RefreshView();
            }));

            Text = $"VHOLTAT Launcher {LauncherConfig.LauncherVersion.ToString(3)}";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(560, 440);
            MinimumSize = new Size(520, 400);
            StartPosition = FormStartPosition.CenterScreen;

            var title = new Label { Text = "VHOLTAT", AutoSize = true, Font = new Font("Segoe UI", 18f, FontStyle.Bold) };
            var subtitle = new Label { Text = "Parkour movement for STRAFTAT", AutoSize = true, ForeColor = SystemColors.GrayText };
            var header = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
            header.Controls.AddRange(new Control[] { title, subtitle });

            var pathLabel = new Label { Text = "STRAFTAT:", AutoSize = true, Anchor = AnchorStyles.Left };
            var pathRow = new TableLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, Padding = new Padding(0, 8, 0, 0) };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            pathRow.Controls.Add(pathLabel, 0, 0);
            pathRow.Controls.Add(pathBox, 1, 0);
            pathRow.Controls.Add(browseButton, 2, 0);

            var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill, Padding = new Padding(0, 4, 0, 0) };
            buttons.Controls.AddRange(new Control[] { mainButton, vanillaButton, uninstallButton, folderButton });

            var keys = new Label
            {
                Text = "Play vanilla, or starting STRAFTAT from Steam as usual, runs plain STRAFTAT without mods.\n" +
                       "In game: F6 switches between VHOLTAT and vanilla movement, F7 shows the speed overlay.",
                AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 4),
            };

            tips.SetToolTip(vanillaButton, VanillaTip);
            tips.SetToolTip(keys, VanillaTip);
            tips.SetToolTip(browseButton, "Pick the STRAFTAT folder by hand (the one with STRAFTAT.exe).");
            tips.SetToolTip(uninstallButton, "Removes VHOLTAT and BepInEx from the launcher's folder and its two files from the STRAFTAT folder.");
            tips.SetToolTip(folderButton, $"Opens {paths.Home}");

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
            for (int i = 0; i < 5; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.Controls.Add(header);
            layout.Controls.Add(pathRow);
            layout.Controls.Add(statusLabel);
            layout.Controls.Add(buttons);
            layout.Controls.Add(keys);
            layout.Controls.Add(logBox);
            Controls.Add(layout);

            mainButton.Click += async (s, e) => await Run(OnMain);
            vanillaButton.Click += async (s, e) => await Run(OnVanilla);
            uninstallButton.Click += async (s, e) => await Run(OnUninstall);
            browseButton.Click += async (s, e) => await Run(OnBrowse);
            folderButton.Click += (s, e) => { Directory.CreateDirectory(paths.Home); Steam.Open(paths.Home); };
            Shown += async (s, e) => await Run(OnStart);
            FormClosed += (s, e) => launcher.Dispose();
        }

        private async Task OnStart()
        {
            launcher.Find();
            if (launcher.StraftatPath != null) Log($"STRAFTAT: {launcher.StraftatPath}");
            else
            {
                Log("STRAFTAT wasn't found in your Steam libraries.");
                Log($"  Fix: {launcher.StraftatNotFoundFix}");
            }
            if (launcher.SteamPath == null) Log("Steam wasn't found. Play needs Steam: install it and sign in.");
            if (zipOverride != null) Log($"Installing from a local zip: {zipOverride}");
            if (launcher.IsInstalled) Log($"VHOLTAT {launcher.State.ModVersion} is installed.");
            RefreshView();
            await launcher.CheckForUpdates();
        }

        private async Task OnMain()
        {
            launcher.Find();
            if (launcher.StraftatPath == null) { Steam.OpenStorePage(launcher.SteamPath); return; }
            if (launcher.ReadyToPlay)
            {
                launcher.Play();
                notice = "STRAFTAT is starting with VHOLTAT through Steam.";
                return;
            }
            var result = zipOverride != null ? await launcher.InstallFromZip(zipOverride) : await launcher.Install();
            notice = $"VHOLTAT {result}. Press Play.";
            Log("Ready. Press Play.");
        }

        private Task OnVanilla()
        {
            launcher.PlayVanilla();
            notice = "Plain STRAFTAT is starting through Steam (no VHOLTAT).";
            return Task.CompletedTask;
        }

        private Task OnUninstall()
        {
            var answer = MessageBox.Show(this,
                "Remove VHOLTAT and BepInEx from the launcher's folder, and the launcher's two files from the STRAFTAT folder?\n\n" +
                "Keep your VHOLTAT settings for a later reinstall?\n\nYes: keep settings    No: delete everything",
                "Uninstall VHOLTAT", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.Cancel)
            {
                launcher.Uninstall(keepConfig: answer == DialogResult.Yes);
                notice = "Uninstalled. STRAFTAT is back to plain.";
            }
            return Task.CompletedTask;
        }

        private async Task OnBrowse()
        {
            using var dialog = new FolderBrowserDialog { Description = "Pick the STRAFTAT folder (the one with STRAFTAT.exe)" };
            if (launcher.StraftatPath != null) dialog.SelectedPath = launcher.StraftatPath;
            if (dialog.ShowDialog(this) != DialogResult.OK) return;
            if (!Steam.IsStraftatFolder(dialog.SelectedPath))
            {
                MessageBox.Show(this, "That folder has no STRAFTAT.exe.", "VHOLTAT", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            launcher.SetStraftatPath(dialog.SelectedPath);
            Log($"STRAFTAT: {launcher.StraftatPath}");
            // Moves the launcher's two game files to the new folder if VHOLTAT is already installed.
            launcher.MoveGameFiles();
            await Task.CompletedTask;
        }

        /// <summary>Runs an action with the buttons disabled. STRAFTAT running asks to close it and retry; Steam not
        /// running offers to start Steam; any other problem goes to the status line, the log and a message box.</summary>
        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            problem = null;
            notice = null;
            RefreshView();
            try
            {
                while (true)
                {
                    try
                    {
                        await action();
                        break;
                    }
                    catch (GameRunningError e)
                    {
                        launcher.LogError(e);
                        var answer = MessageBox.Show(this, "STRAFTAT is running. Close the game, then press Retry.",
                            "VHOLTAT", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);
                        if (answer != DialogResult.Retry) { problem = e.OneLine; break; }
                    }
                }
            }
            catch (SteamNotRunningError e)
            {
                launcher.LogError(e);
                var answer = MessageBox.Show(this, "Steam isn't running.\n\nStart Steam now? Once you're signed in, press Play again.",
                    "VHOLTAT", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.Yes)
                {
                    Steam.StartSteam(launcher.SteamPath);
                    launcher.Log("Starting Steam. Press Play again once you're signed in.");
                    notice = "Starting Steam… press Play again once you're signed in.";
                }
                else problem = e.OneLine;
            }
            catch (Exception e)
            {
                launcher.LogError(e);
                var error = e as LauncherError;
                problem = error?.OneLine ?? e.GetBaseException().Message;
                var text = error == null ? problem : string.IsNullOrEmpty(error.Fix) ? error.Message : $"{error.Message}\n\n{error.Fix}";
                MessageBox.Show(this, text, "VHOLTAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                busy = false;
                download = null;
                launcher.Find();
                RefreshView();
            }
        }

        private void RefreshView()
        {
            var state = launcher.State;
            pathBox.Text = launcher.StraftatPath ?? "not found";
            var best = launcher.BestVersion?.ToString(3);

            string status;
            var color = SystemColors.ControlText;
            if (busy && download != null) status = download;
            else if (busy) status = "Working…";
            else if (problem != null) { status = problem; color = Color.Firebrick; }
            else if (notice != null) status = notice;
            else if (launcher.StraftatPath == null) { status = $"STRAFTAT wasn't found. {launcher.StraftatNotFoundFix}"; color = Color.Firebrick; }
            else if (launcher.ModStatus == ModStatus.NotInstalled) status = $"Not installed. Install puts BepInEx and VHOLTAT {best} in the launcher's own folder.";
            else if (launcher.ModStatus == ModStatus.Older) status = $"VHOLTAT {state.ModVersion} installed; {best} is available. Your settings are kept.";
            else if (launcher.ModStatus == ModStatus.Newer) status = $"VHOLTAT {state.ModVersion} installed, newer than the {best} this launcher carries. Play uses {state.ModVersion}.";
            else status = $"VHOLTAT {state.ModVersion} installed. Plain Steam launches stay vanilla.";
            if (!busy && problem == null && launcher.StraftatPath != null && launcher.SteamPath == null)
            {
                status += " Steam wasn't found: install Steam and sign in to play.";
                color = Color.Firebrick;
            }
            if (!busy && launcher.LauncherUpdateAvailable) status += " A newer launcher is on the Releases page.";
            statusLabel.Text = status;
            statusLabel.ForeColor = color;
            tips.SetToolTip(statusLabel, status);

            mainButton.Text = launcher.StraftatPath == null ? "Get STRAFTAT"
                : launcher.ModStatus == ModStatus.NotInstalled ? "Install"
                : launcher.ModStatus == ModStatus.Older ? $"Update to {best}"
                : "Play";
            tips.SetToolTip(mainButton, launcher.StraftatPath == null ? "Opens STRAFTAT's Steam store page."
                : launcher.ModStatus == ModStatus.Older ? "Replaces the plugin; BepInEx and your VHOLTAT settings stay."
                : launcher.ModStatus == ModStatus.NotInstalled ? "Installs BepInEx and VHOLTAT into the launcher's own folder."
                : "Starts STRAFTAT with VHOLTAT through Steam.");
            mainButton.Enabled = !busy;
            vanillaButton.Enabled = !busy && launcher.StraftatPath != null;
            browseButton.Enabled = !busy;
            uninstallButton.Enabled = !busy && (state.IsInstalled || Directory.Exists(launcher.Paths.Profile) || state.GameFiles.Count > 0);
            UseWaitCursor = busy;
        }

        /// <summary>Window side of the launcher's log (the launcher also writes launcher.log).</summary>
        private void AppendLog(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(AppendLog), message); return; }
            logBox.AppendText(message + Environment.NewLine);
        }

        private void Log(string message) => launcher.Log(message);
    }
}
