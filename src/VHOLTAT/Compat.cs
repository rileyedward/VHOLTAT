using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace VHOLTAT
{
    /// <summary>
    /// Game-update guard. Before anything is patched, checks that every STRAFTAT method and field VHOLTAT patches or
    /// reads still exists with the expected kind. If a STRAFTAT update renamed or removed one, VHOLTAT stays off
    /// (vanilla movement), logs what is missing and shows a one-line notice instead of failing mid-game.
    /// </summary>
    internal static class Compat
    {
        /// <summary>The STRAFTAT build this VHOLTAT version was made and tested against (its lobby version string).</summary>
        public const string BuiltFor = "1.4.9f";

        private static readonly string[] FpcMethods =
        {
            "ApplyFinalMovements", "Update", "Jump", "Slide", "HandleSlide", "CheckForVault", "HandleCameraController",
            "HandleAnimation", "Teleport", "AddForce", "AddHorizontalForce", "PlaySoundServer", "SideTilt",
            "ChangeSlideClipServer", "SlideAudioPlay", "SlideAudioStop", "VaultClip", "sync___get_value_canMove",
        };

        private static readonly string[] FpcFields =
        {
            "moveDirection", "isSliding", "isCrouching", "characterController", "playerCamera", "pauseManager",
            "startOfRound", "forceAdded", "moveAdded", "customForceFinal", "slopeSlideScript", "movementFactor",
            "landLayer", "crouchToggle", "move", "zoomFOV", "defaultFOV", "isAiming", "camController", "volume",
            "bforcefinal", "objectCollisionMoveDirection", "canJump", "networkAnimator", "setSpeedTimer", "tempSetSpeed",
            "sprintSpeed", "onLadder", "animator", "runFOV", "slideTiltAmount", "slideAudio", "audio", "landBobPivot",
            "landBobAmount", "landRecoverSpeed", "ladderLayer",
        };

        private static readonly string[] PauseManagerFields = { "pause", "startRound", "steamPlaying", "chatting", "pauseMenu" };

        public static readonly List<string> Missing = new List<string>();

        /// <summary>One-line notice shown in game when VHOLTAT had to stay off; null when everything is fine.</summary>
        public static string Notice { get; private set; }

        public static bool Check()
        {
            Missing.Clear();
            var fpc = typeof(FirstPersonController);
            foreach (var m in FpcMethods)
                if (AccessTools.Method(fpc, m) == null) Missing.Add("FirstPersonController." + m + "()");
            foreach (var f in FpcFields)
                if (AccessTools.Field(fpc, f) == null) Missing.Add("FirstPersonController." + f);
            foreach (var f in PauseManagerFields)
                if (AccessTools.Field(typeof(PauseManager), f) == null) Missing.Add("PauseManager." + f);
            if (AccessTools.Field(typeof(FishNet.Component.Transforming.NetworkTransform), "_interpolation") == null)
                Missing.Add("NetworkTransform._interpolation");

            // Dev: simulate a STRAFTAT update that removed something (--vholtat-compat-test).
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--vholtat-compat-test") >= 0)
                Missing.Add("FirstPersonController.simulatedMissingMember");

            if (Missing.Count == 0)
                return true;
            Fail("this STRAFTAT version changed (" + Missing.Count + " missing: " + string.Join(", ", Missing) + ")");
            return false;
        }

        public static void Fail(string reason)
        {
            Notice = $"VHOLTAT {MyPluginInfo.PLUGIN_VERSION} is off: {reason.Split('(')[0].Trim()}. Using vanilla movement - check for a VHOLTAT update.";
            Plugin.Logger.LogError($"VHOLTAT disabled: {reason}. Made for STRAFTAT {BuiltFor}.");
        }
    }

    /// <summary>Shows the compatibility notice for a few seconds whenever a map loads.</summary>
    internal sealed class CompatNotice : MonoBehaviour
    {
        private float until;
        private GUIStyle style;

        private void OnEnable() => UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => until = Time.unscaledTime + 12f;

        private void OnGUI()
        {
            if (Compat.Notice == null || Time.unscaledTime > until)
                return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                style.normal.textColor = new Color(1f, 0.85f, 0.3f);
            }
            float w = Mathf.Min(900f, Screen.width - 40f);
            GUI.Box(new Rect((Screen.width - w) / 2f, Screen.height - 120f, w, 48f), Compat.Notice, style);
        }
    }
}
