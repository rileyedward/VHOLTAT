using UnityEngine;
using VHOLTAT.Adapter;

namespace VHOLTAT.Net
{
    /// <summary>
    /// Join-time version check. The host's settings broadcast starts with its VHOLTAT version. A client running a
    /// different version switches to vanilla movement for that lobby (so nobody plays with mismatched rules) and shows
    /// a banner telling the player which version to install. Leaving the lobby clears it.
    /// </summary>
    internal sealed class VersionCheck : MonoBehaviour
    {
        /// <summary>This copy's version ([Dev] FakeVersion overrides it for the mismatch test).</summary>
        public static string LocalVersion =>
            string.IsNullOrEmpty(Plugin.Settings.DevFakeVersion.Value) ? MyPluginInfo.PLUGIN_VERSION : Plugin.Settings.DevFakeVersion.Value;

        public static string Mismatch { get; private set; }
        private static bool forcedVanilla;

        /// <summary>Returns true when the host runs the same version.</summary>
        public static bool CheckHost(string hostVersion)
        {
            if (hostVersion == LocalVersion)
                return true;
            if (Mismatch == null)
            {
                Mismatch = hostVersion;
                Plugin.Logger.LogWarning($"[version] host runs VHOLTAT {hostVersion}, this copy has {LocalVersion}: using vanilla movement in this lobby");
                if (MotorDriver.Active)
                {
                    MotorDriver.SetActive(false);
                    forcedVanilla = true;
                }
            }
            return false;
        }

        private void Update()
        {
            // Left the host's lobby: restore the player's own choice.
            if (Mismatch != null && !(FishNet.InstanceFinder.IsClient && !FishNet.InstanceFinder.IsServer))
            {
                Plugin.Logger.LogInfo("[version] left the mismatched lobby");
                Mismatch = null;
                if (forcedVanilla)
                    MotorDriver.SetActive(Plugin.Settings.Enabled.Value);
                forcedVanilla = false;
            }
            // Keep vanilla even if the player presses the toggle key.
            if (Mismatch != null && MotorDriver.Active)
                MotorDriver.SetActive(false);
        }

        private GUIStyle style;

        private void OnGUI()
        {
            if (Mismatch == null)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                style.normal.textColor = new Color(1f, 0.85f, 0.3f);
            }
            float w = Mathf.Min(760f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - w) / 2f, 24f, w, 70f),
                $"VHOLTAT version mismatch: the host has {Mismatch}, you have {LocalVersion}.\nUpdate to VHOLTAT {Mismatch}. Using vanilla movement in this lobby.", style);
        }
    }
}
