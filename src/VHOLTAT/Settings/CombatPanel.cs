using BepInEx.Configuration;
using UnityEngine;

namespace VHOLTAT.Settings
{
    /// <summary>
    /// Hidden combat tuning (Shift + CombatMenuKey, F12 by default; not listed in the pause menu). Fast targets are
    /// part of the fun, so everything here defaults to VHOLUME's numbers; the panel only exists to try limits.
    /// Speed numbers come from the host in someone else's lobby, so they're read-only there. Changes save to the config.
    /// </summary>
    internal sealed class CombatPanel : MonoBehaviour
    {
        /// <summary>Dev self-test: keeps the panel open while set.</summary>
        internal static bool ForceOpen;
        private bool open;
        private Rect window = new Rect(0f, 120f, 440f, 10f);
        private bool placed;
        private GUIStyle header;

        private void Update()
        {
            var c = Plugin.Settings;
            if (Input.GetKeyDown(c.CombatMenuKey.Value) && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)))
            {
                open = !open;
                if (!open)
                    Relock();
                Plugin.Logger.LogInfo($"combat panel {(open ? "open" : "closed")}");
            }
        }

        private void OnGUI()
        {
            if (!open && !ForceOpen)
                return;
            if (header == null)
                header = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = 15 };
            if (!placed)
            {
                window.x = Screen.width - window.width - 16f; // right side, clear of the debug overlay
                placed = true;
            }
            // The cursor is locked while playing; free it so the sliders can be used.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            window = GUILayout.Window(0x5648_4342, window, Draw, "VHOLTAT combat tuning (Shift+" + Plugin.Settings.CombatMenuKey.Value + ")");
        }

        private void Draw(int id)
        {
            var c = Plugin.Settings;

            GUILayout.Label("How fast players can go", header);
            if (c.RemoteActive)
                GUILayout.Label("In someone else's lobby: the host's speed settings apply.\nLimit in use: " +
                                (c.ActiveMaxSpeed > 0f ? c.ActiveMaxSpeed.ToString("0.0") + " m/s" : "none"));
            else
            {
                c.MaxSpeed.Value = Mathf.Round(Slider("Speed limit (0 = none)", c.MaxSpeed.Value, 0f, 40f, "0") * 2f) / 2f;
                Number("BoostCap", "Max hop boost (m/s)", 0f, 20f);
                Number("HopGain", "Hop gain (share of excess)", 0f, 0.5f);
                Number("BoostDecay", "Boost decay (per s)", 0f, 3f);
                Number("WallRunSpeed", "Wall-run speed (m/s)", 8f, 25f);
                Number("WallJumpMinSpeed", "Wall-jump speed (m/s)", 8f, 25f);
                Number("SlideSpeed", "Slide speed (m/s)", 8f, 25f);
                GUILayout.Label("These apply to the VHOLUME model and are shared with your lobby when you host.");
            }

            GUILayout.Space(8f);
            GUILayout.Label("How other players look on your screen", header);
            c.RemoteInterpolationTicks.Value = Mathf.RoundToInt(Slider("Smoothing (ticks, lower = less delay)", c.RemoteInterpolationTicks.Value, 0f, 12f, "0"));
            c.RemotePoses.Value = GUILayout.Toggle(c.RemotePoses.Value, " Lean / tuck their model on wall runs, climbs and vaults");
            GUILayout.Label("Hits are decided on the shooter's screen, as in vanilla STRAFTAT.");

            GUILayout.Space(8f);
            if (!c.RemoteActive && GUILayout.Button("Back to VHOLUME numbers"))
            {
                c.MaxSpeed.Value = 0f;
                var defaults = new Movement.VholumeSettings();
                foreach (var name in Names)
                {
                    var e = c.VholumeEntry(name);
                    if (e != null)
                        e.Value = (float)typeof(Movement.VholumeSettings).GetField(name).GetValue(defaults);
                }
                c.RemoteInterpolationTicks.Value = (int)c.RemoteInterpolationTicks.DefaultValue;
                c.RemotePoses.Value = true;
            }
            if (GUILayout.Button("Close"))
            {
                open = false;
                Relock();
            }
            GUI.DragWindow();
        }

        private static void Relock()
        {
            var pm = PauseManager.Instance;
            if (pm == null || !pm.pause)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private static readonly string[] Names = { "BoostCap", "HopGain", "BoostDecay", "WallRunSpeed", "WallJumpMinSpeed", "SlideSpeed" };

        private static void Number(string field, string label, float min, float max)
        {
            ConfigEntry<float> e = Plugin.Settings.VholumeEntry(field);
            if (e == null)
                return;
            float v = Slider(label, e.Value, min, max, max <= 1f ? "0.00" : "0.0");
            e.Value = max <= 1f ? Mathf.Round(v * 100f) / 100f : Mathf.Round(v * 10f) / 10f;
        }

        private static float Slider(string label, float value, float min, float max, string format)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, GUILayout.Width(230f));
            float v = GUILayout.HorizontalSlider(value, min, max, GUILayout.Width(130f));
            GUILayout.Label(v.ToString(format), GUILayout.Width(44f));
            GUILayout.EndHorizontal();
            return Mathf.Abs(v - value) < 1e-4f ? value : v;
        }
    }
}
