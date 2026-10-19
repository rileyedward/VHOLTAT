using System;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace VHOLTAT.Launcher
{
    /// <summary>The launcher window: the STRAFTAT folder, a status line, one main button (Install, then Play),
    /// Uninstall, Open folder and a log.</summary>
    public sealed class MainForm : Form
    {
        private readonly Launcher launcher;
        private readonly string zipOverride;

        private readonly TextBox pathBox = new TextBox { ReadOnly = true, TabStop = false, Anchor = AnchorStyles.Left | AnchorStyles.Right, BackColor = SystemColors.Window };
        private readonly Button browseButton = new Button { Text = "Browse…", AutoSize = true, Anchor = AnchorStyles.Right };
        private readonly Label statusLabel = new Label { AutoSize = false, AutoEllipsis = true, Dock = DockStyle.Fill, Height = 22, TextAlign = ContentAlignment.MiddleLeft };
        private readonly Button mainButton = new Button { Width = 150, Height = 40, Font = new Font("Segoe UI", 11f, FontStyle.Bold) };
        private readonly Button uninstallButton = new Button { Text = "Uninstall", Width = 90, Height = 40 };
        private readonly Button folderButton = new Button { Text = "Open folder", Width = 100, Height = 40 };
        private readonly TextBox logBox = new TextBox
        {
            Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, MinimumSize = new Size(0, 120),
            BackColor = SystemColors.Window, Font = new Font(FontFamily.GenericMonospace, 8.5f),
        };
        private bool busy;
        private string download;

        public MainForm(LauncherPaths paths, string straftatOverride, string zipOverride)
        {
            this.zipOverride = zipOverride == null ? null : Path.GetFullPath(zipOverride);
            launcher = new Launcher(paths, Log, straftatOverride);
            launcher.Sources.Progress = (file, done, total) => BeginInvoke(new Action(() =>
            {
                download = total > 0 ? $"Downloading {file}: {done * 100 / total}%" : $"Downloading {file}: {done / (1 << 20)} MB";
                RefreshView();
            }));

            Text = $"VHOLTAT Launcher {LauncherConfig.LauncherVersion.ToString(3)}";
            Font = new Font("Segoe UI", 9f);
            AutoScaleMode = AutoScaleMode.Dpi;
            ClientSize = new Size(540, 400);
            MinimumSize = new Size(480, 360);
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
            buttons.Controls.AddRange(new Control[] { mainButton, uninstallButton, folderButton });

            var keys = new Label
            {
                Text = "In game: F6 switches between VHOLTAT and vanilla movement, F7 shows the speed overlay.",
                AutoSize = true, ForeColor = SystemColors.GrayText, Padding = new Padding(0, 4, 0, 4),
            };

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
            uninstallButton.Click += async (s, e) => await Run(OnUninstall);
            browseButton.Click += async (s, e) => await Run(OnBrowse);
            folderButton.Click += (s, e) => { Directory.CreateDirectory(paths.Home); Steam.Open(paths.Home); };
            Shown += async (s, e) => await Run(OnStart);
            FormClosed += (s, e) => launcher.Dispose();
        }

        private async Task OnStart()
        {
            launcher.Find();
            Log(launcher.StraftatPath != null ? $"STRAFTAT: {launcher.StraftatPath}" : "STRAFTAT wasn't found in your Steam libraries. Use Browse to pick its folder.");
            if (zipOverride != null) Log($"Installing from a local zip: {zipOverride}");
            if (launcher.IsInstalled) Log($"VHOLTAT {launcher.State.ModVersion} is installed.");
            RefreshView();
            await launcher.CheckForUpdates();
        }

        private async Task OnMain()
        {
            launcher.Find();
            if (launcher.StraftatPath == null) { Steam.OpenStorePage(LauncherConfig.StraftatAppId); return; }
            if (launcher.ReadyToPlay) { launcher.Play(); return; }
            if (zipOverride != null) await launcher.InstallFromZip(zipOverride);
            else await launcher.Install();
            Log("Ready. Press Play.");
        }

        private Task OnUninstall()
        {
            var answer = MessageBox.Show(this,
                "Remove VHOLTAT and BepInEx from the launcher's folder, and the launcher's two files from the STRAFTAT folder?\n\n" +
                "Keep your VHOLTAT settings for a later reinstall?\n\nYes: keep settings    No: delete everything",
                "Uninstall VHOLTAT", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.Cancel) launcher.Uninstall(keepConfig: answer == DialogResult.Yes);
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
            if (launcher.IsInstalled) launcher.Installer.PrepareGameFolder(launcher.StraftatPath);
            await Task.CompletedTask;
        }

        /// <summary>Runs an action with the buttons disabled, showing any error.</summary>
        private async Task Run(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            RefreshView();
            try
            {
                await action();
            }
            catch (Exception e)
            {
                var message = e.GetBaseException().Message;
                Log($"Error: {message}");
                MessageBox.Show(this, message, "VHOLTAT", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            string status;
            if (busy && download != null) status = download;
            else if (busy) status = "Working…";
            else if (launcher.StraftatPath == null) status = "STRAFTAT wasn't found. Pick its folder, or get it on Steam.";
            else if (!launcher.IsInstalled) status = $"Not installed. Install puts BepInEx and VHOLTAT {launcher.BestVersion?.ToString(3)} in the launcher's own folder.";
            else if (launcher.UpdateAvailable) status = $"VHOLTAT {state.ModVersion} installed; {launcher.BestVersion.ToString(3)} is available.";
            else status = $"VHOLTAT {state.ModVersion} installed. Plain Steam launches stay vanilla.";
            if (!busy && launcher.LauncherUpdateAvailable) status += " A newer launcher is on the Releases page.";
            statusLabel.Text = status;

            mainButton.Text = launcher.StraftatPath == null ? "Get STRAFTAT"
                : !launcher.IsInstalled ? "Install"
                : launcher.UpdateAvailable ? "Update"
                : "Play";
            mainButton.Enabled = !busy;
            browseButton.Enabled = !busy;
            uninstallButton.Enabled = !busy && (state.IsInstalled || Directory.Exists(launcher.Paths.Profile) || state.GameFiles.Count > 0);
            UseWaitCursor = busy;
        }

        private void Log(string message)
        {
            if (InvokeRequired) { BeginInvoke(new Action<string>(Log), message); return; }
            logBox.AppendText(message + Environment.NewLine);
        }
    }
}
