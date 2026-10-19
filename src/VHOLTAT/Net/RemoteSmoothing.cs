using FishNet.Component.Transforming;
using HarmonyLib;
using UnityEngine;

namespace VHOLTAT.Net
{
    /// <summary>
    /// Viewer-side smoothing for other players. STRAFTAT's player NetworkTransform buffers a few ticks of positions;
    /// at VHOLTAT speeds a lost or late packet can drain that buffer and the remote player stutters. Raising the buffer
    /// on remote players (this machine only) trades a little extra display delay for smoother motion. Hits stay
    /// decided on the shooter's screen, as in vanilla.
    /// </summary>
    internal sealed class RemoteSmoothing : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<NetworkTransform, ushort> Interpolation =
            AccessTools.FieldRefAccess<NetworkTransform, ushort>("_interpolation");

        private float nextScan;

        private void Update()
        {
            int ticks = Plugin.Settings.RemoteInterpolationTicks.Value;
            if (ticks <= 0 || Time.unscaledTime < nextScan)
                return;
            nextScan = Time.unscaledTime + 1f;
            foreach (var fpc in FindObjectsOfType<FirstPersonController>(true))
            {
                if (fpc.IsOwner)
                    continue;
                var nt = fpc.GetComponent<NetworkTransform>();
                if (nt != null && Interpolation(nt) < ticks)
                    Interpolation(nt) = (ushort)ticks;
            }
        }
    }
}
