using System.IO;
using System.Text;
using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using VHOLTAT.Adapter;

namespace VHOLTAT.DebugOverlay
{
    /// <summary>
    /// Opt-in ([Debug] MarkSpots): the mark key appends the map, position, facing, movement state and what is around
    /// the player to BepInEx\vholtat_spots.txt, so a problem place found in normal play can be reported and fixed.
    /// </summary>
    internal sealed class SpotMarker : MonoBehaviour
    {
        private readonly Collider[] nearby = new Collider[16];

        private void Update()
        {
            var cfg = Plugin.Settings;
            if (!cfg.MarkSpots.Value || !Input.GetKeyDown(cfg.MarkSpotKey.Value))
                return;
            var fpc = MotorDriver.Current;
            if (fpc == null)
                return;

            var m = MotorDriver.Motor;
            var s = MotorDriver.Sensors;
            var t = fpc.transform;
            var sb = new StringBuilder();
            sb.Append(System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("  map=").Append(SceneManager.GetActiveScene().name)
              .Append("  pos=").Append(t.position.ToString("F2")).Append("  yaw=").Append(t.eulerAngles.y.ToString("F0"))
              .Append("  pitch=").Append(fpc.playerCamera != null ? fpc.playerCamera.transform.localEulerAngles.x.ToString("F0") : "?")
              .AppendLine();
            sb.Append("  state=").Append(m.State).Append(" t=").Append(m.StateTime.ToString("F2"))
              .Append(" speed=").Append(m.HorizontalSpeed.ToString("F2")).Append(" vy=").Append(m.Velocity.Y.ToString("F2"))
              .Append(" boost=").Append(m.Boost.ToString("F2")).Append(" vholtat=").Append(MotorDriver.Active).AppendLine();
            sb.Append("  grounded=").Append(s.Grounded).Append(" groundNormal=").Append(s.GroundNormal)
              .Append(" height=").Append(s.HeightAboveGround.ToString("F2")).Append(" ceiling=").Append(s.CeilingBlocked)
              .Append(" wallL=").Append(s.WallLeft).Append(" wallR=").Append(s.WallRight).Append(" wallF=").Append(s.WallFront)
              .Append(" ledge=").Append(s.LedgeFound).AppendLine();
            int n = Physics.OverlapSphereNonAlloc(t.position + Vector3.up, 1.5f, nearby, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var c = nearby[i];
                if (c == null || c.transform.IsChildOf(t)) continue;
                Vector3 p = c.ClosestPoint(t.position + Vector3.up);
                sb.Append("  near: ").Append(c.name).Append(" (layer ").Append(LayerMask.LayerToName(c.gameObject.layer))
                  .Append(c.isTrigger ? ", trigger" : "").Append(", tag ").Append(c.tag).Append(") closest ").Append(p.ToString("F2")).AppendLine();
            }
            sb.AppendLine();
            string path = Path.Combine(Paths.BepInExRootPath, "vholtat_spots.txt");
            File.AppendAllText(path, sb.ToString());
            Plugin.Logger.LogInfo($"spot marked in {path}");
        }
    }
}
