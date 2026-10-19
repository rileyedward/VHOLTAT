using System.Text;
using UnityEngine;
using VHOLTAT.Adapter;
using VHOLTAT.Movement;

namespace VHOLTAT.DebugOverlay
{
    /// <summary>Small IMGUI readout: mode, speed, state, grounded, walls. Text is rebuilt at 10 Hz into a reused builder.</summary>
    internal sealed class Overlay : MonoBehaviour
    {
        private readonly StringBuilder sb = new StringBuilder(256);
        private string text = "";
        private float nextRefresh;
        private float peakSpeed;
        private GUIStyle style;
        private Rect rect = new Rect(12f, 200f, 360f, 190f);

        private void Update()
        {
            if (!Plugin.Settings.ShowOverlay.Value)
                return;
            var m = MotorDriver.Motor;
            if (m != null)
                peakSpeed = Mathf.Max(peakSpeed * Mathf.Exp(-Time.unscaledDeltaTime * 0.5f), m.HorizontalSpeed);
            if (Time.unscaledTime < nextRefresh)
                return;
            nextRefresh = Time.unscaledTime + 0.1f;
            Rebuild(m);
        }

        private void Rebuild(MovementMotor m)
        {
            sb.Length = 0;
            sb.Append("VHOLTAT  [").Append(MotorDriver.Active ? "ON" : "vanilla").Append("]  ")
              .Append(Plugin.Settings.ToggleKey.Value).Append(" toggles\n");
            var fpc = MotorDriver.Current;
            if (!MotorDriver.Active || m == null || fpc == null)
            {
                if (fpc != null && fpc.characterController != null)
                {
                    var v = fpc.characterController.velocity;
                    sb.Append("speed ").Append(new Vector2(v.x, v.z).magnitude.ToString("0.0")).Append(" m/s (vanilla)\n");
                }
                text = sb.ToString();
                return;
            }
            var s = MotorDriver.Sensors;
            sb.Append("state ").Append(m.State).Append("  t=").Append(m.StateTime.ToString("0.00")).Append('\n');
            sb.Append("speed ").Append(m.HorizontalSpeed.ToString("0.0")).Append(" m/s   peak ").Append(peakSpeed.ToString("0.0"))
              .Append("   boost ").Append(m.Boost.ToString("0.0")).Append('\n');
            sb.Append("vy ").Append(m.Velocity.Y.ToString("0.0")).Append("   grounded ").Append(s.Grounded ? "yes" : "no")
              .Append("   h ").Append(s.HeightAboveGround.ToString("0.0")).Append('\n');
            sb.Append("wall L ").Append(s.WallLeft ? "#" : "-").Append("  R ").Append(s.WallRight ? "#" : "-")
              .Append("  F ").Append(s.WallFront ? "#" : "-").Append("  ledge ").Append(s.LedgeFound ? "#" : "-")
              .Append("  ceil ").Append(s.CeilingBlocked ? "#" : "-").Append('\n');
            sb.Append("weapon speed x").Append(fpc.movementFactor.ToString("0.00"));
            text = sb.ToString();
        }

        private void OnGUI()
        {
            if (!Plugin.Settings.ShowOverlay.Value)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = false };
                style.normal.textColor = Color.white;
            }
            GUI.Box(rect, text, style);
        }
    }
}
