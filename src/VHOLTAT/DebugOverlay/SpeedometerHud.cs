using UnityEngine;
using VHOLTAT.Adapter;

namespace VHOLTAT.DebugOverlay
{
    /// <summary>VHOLUME-style speedometer: horizontal speed in VHOLUME units (cm/s) in the top-right corner. Text refreshed at 20 Hz.</summary>
    internal sealed class SpeedometerHud : MonoBehaviour
    {
        private GUIStyle style, shadow;
        private string text = "0u";
        private float next;
        private int lastShown = -1;

        private void Update()
        {
            if (!Plugin.Settings.Speedometer.Value || MotorDriver.Motor == null || Time.unscaledTime < next)
                return;
            next = Time.unscaledTime + 0.05f;
            int u = Mathf.RoundToInt(MotorDriver.Motor.HorizontalSpeed * 100f);
            if (u != lastShown)
            {
                lastShown = u;
                text = u + "u";
            }
        }

        private void OnGUI()
        {
            if (!Plugin.Settings.Speedometer.Value || MotorDriver.Current == null || !MotorDriver.Active)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight };
                style.normal.textColor = Color.white;
                shadow = new GUIStyle(style);
                shadow.normal.textColor = new Color(0f, 0f, 0f, 0.6f);
            }
            var r = new Rect(Screen.width - 340f, 120f, 300f, 50f);
            GUI.Label(new Rect(r.x + 2f, r.y + 2f, r.width, r.height), text, shadow);
            GUI.Label(r, text, style);
        }
    }
}
