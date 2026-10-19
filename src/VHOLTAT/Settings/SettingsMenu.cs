using UnityEngine;
using VHOLTAT.Adapter;

namespace VHOLTAT.Settings
{
    /// <summary>
    /// In-game settings: a "VHOLTAT" button in the corner of STRAFTAT's pause menu opens a small window. Personal
    /// settings (on/off, overlay, camera, sounds, smoothing) are always editable; movement numbers can only be reset
    /// when playing solo or hosting, since in someone else's lobby the host's movement settings apply.
    /// Changes write straight to the BepInEx config.
    /// </summary>
    internal sealed class SettingsMenu : MonoBehaviour
    {
        /// <summary>Opens the window the next time the pause menu shows (dev self-test).</summary>
        internal static bool ForceOpen;
        private bool open;
        private Rect window = new Rect(0f, 0f, 420f, 10f); // GUILayout grows it to fit
        private bool placed;
        private GUIStyle header;

        private static bool Paused
        {
            get
            {
                var pm = PauseManager.Instance;
                return pm != null && pm.pause;
            }
        }

        private void OnGUI()
        {
            if (!Paused)
            {
                open = false;
                return;
            }
            if (ForceOpen)
            {
                open = true;
                ForceOpen = false;
            }
            if (header == null)
                header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };

            if (GUI.Button(new Rect(Screen.width - 150f, 16f, 134f, 32f), open ? "Close VHOLTAT" : "VHOLTAT settings"))
                open = !open;
            if (!open)
                return;
            if (!placed)
            {
                window.x = Screen.width - window.width - 16f;
                window.y = 56f;
                placed = true;
            }
            window = GUILayout.Window(0x5648_4F4C, window, Draw, "VHOLTAT " + MyPluginInfo.PLUGIN_VERSION);
        }

        private void Draw(int id)
        {
            var c = Plugin.Settings;

            GUILayout.Label("General", header);
            c.Enabled.Value = GUILayout.Toggle(c.Enabled.Value, " VHOLTAT movement (F6 switches live)");
            c.ShowOverlay.Value = GUILayout.Toggle(c.ShowOverlay.Value, " Speed / state overlay (F7)");

            GUILayout.Space(8f);
            GUILayout.Label("Camera and sound", header);
            c.FovSpeedBonus.Value = Slider("FOV kick at high speed", c.FovSpeedBonus.Value, 0f, 25f, "0");
            c.StrafeTilt.Value = Slider("Head tilt into strafes", c.StrafeTilt.Value, 0f, 6f, "0.0");
            c.WallRunRoll.Value = Slider("Wall-run camera roll", c.WallRunRoll.Value, 0f, 15f, "0");
            c.VholumeWallRunRoll.Value = GUILayout.Toggle(c.VholumeWallRunRoll.Value, " VHOLUME wall-run roll (6° toward the wall)");
            c.VholumeFov.Value = GUILayout.Toggle(c.VholumeFov.Value, " VHOLUME FOV kicks (+5 fast, +10 slide, +15 wall run)");
            c.MoveSounds.Value = GUILayout.Toggle(c.MoveSounds.Value, " Slide, wall-run and vault sounds");
            c.LandingDip.Value = GUILayout.Toggle(c.LandingDip.Value, " Camera dip on hard landings");

            GUILayout.Space(8f);
            GUILayout.Label("Multiplayer", header);
            c.UseHostSettings.Value = GUILayout.Toggle(c.UseHostSettings.Value, " Use the host's movement settings in their lobby");
            c.RemoteInterpolationTicks.Value = Mathf.RoundToInt(Slider("Smoothing of other players (ticks)", c.RemoteInterpolationTicks.Value, 0f, 12f, "0"));

            GUILayout.Space(8f);
            GUILayout.Label("Movement", header);
            if (c.RemoteActive)
            {
                GUILayout.Label("You're in someone else's lobby: their movement settings apply.");
            }
            else
            {
                GUILayout.Label("Movement numbers live in BepInEx/config/dev.riley.vholtat.cfg\n(saved edits apply instantly). Hosts share theirs with the lobby.");
                if (GUILayout.Button("Reset movement to defaults"))
                {
                    c.ResetMovement();
                    MotorDriver.SetActive(c.Enabled.Value);
                }
            }

            GUI.DragWindow();
        }

        private static float Slider(string label, float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(220f));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(120f));
            GUILayout.Label(v.ToString(format), GUILayout.Width(40f));
            GUILayout.EndHorizontal();
            return Mathf.Abs(v - value) < 1e-4f ? value : v;
        }
    }
}
