using System;
using HarmonyLib;
using UnityEngine;
using VHOLTAT.Movement;

namespace VHOLTAT.Adapter
{
    /// <summary>
    /// Sound and camera feedback that vanilla runs from code VHOLTAT replaces (HandleSlide, ApplyFinalMovements),
    /// using STRAFTAT's own clips and values: the networked slide loop for slides (ground clip) and wall runs
    /// (wall clip) with vanilla's fade-out, and the landing dip on the land-bob pivot.
    /// </summary>
    internal static class Feedback
    {
        private static readonly Action<FirstPersonController, int> ChangeSlideClip =
            AccessTools.MethodDelegate<Action<FirstPersonController, int>>(AccessTools.Method(typeof(FirstPersonController), "ChangeSlideClipServer"));
        private static readonly Action<FirstPersonController> SlideAudioPlay =
            AccessTools.MethodDelegate<Action<FirstPersonController>>(AccessTools.Method(typeof(FirstPersonController), "SlideAudioPlay"));
        private static readonly Action<FirstPersonController> SlideAudioStop =
            AccessTools.MethodDelegate<Action<FirstPersonController>>(AccessTools.Method(typeof(FirstPersonController), "SlideAudioStop"));
        private static readonly Action<FirstPersonController, string> VaultClip =
            AccessTools.MethodDelegate<Action<FirstPersonController, string>>(AccessTools.Method(typeof(FirstPersonController), "VaultClip"));
        private static readonly AccessTools.FieldRef<FirstPersonController, AudioSource> SlideAudio =
            AccessTools.FieldRefAccess<FirstPersonController, AudioSource>("slideAudio");
        private static readonly AccessTools.FieldRef<FirstPersonController, AudioSource> MainAudio =
            AccessTools.FieldRefAccess<FirstPersonController, AudioSource>("audio");
        private static readonly AccessTools.FieldRef<FirstPersonController, Transform> LandBobPivot =
            AccessTools.FieldRefAccess<FirstPersonController, Transform>("landBobPivot");
        private static readonly AccessTools.FieldRef<FirstPersonController, float> LandBobAmount =
            AccessTools.FieldRefAccess<FirstPersonController, float>("landBobAmount");
        private static readonly AccessTools.FieldRef<FirstPersonController, float> LandRecoverSpeed =
            AccessTools.FieldRefAccess<FirstPersonController, float>("landRecoverSpeed");

        private static bool loopStopped = true;

        /// <param name="impactSpeed">Downward speed just before this frame's landing (m/s, positive).</param>
        public static void Apply(FirstPersonController fpc, MovementMotor m, float impactSpeed)
        {
            try
            {
                Audio(fpc, m);
                if ((m.Events & MoveEvents.MantleStarted) != 0)
                {
                    var surface = SensorProbe.LastCoverSurface;
                    VaultClip(fpc, surface != null ? surface.tag : "Untagged");
                }
                LandDip(fpc, m, impactSpeed);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"feedback failed: {ex.Message}");
            }
        }

        private static void Audio(FirstPersonController fpc, MovementMotor m)
        {
            var e = m.Events;
            var slide = SlideAudio(fpc);
            var main = MainAudio(fpc);
            if (slide == null)
                return;

            if ((e & (MoveEvents.SlideStarted | MoveEvents.WallRunStarted)) != 0)
            {
                ChangeSlideClip(fpc, (e & MoveEvents.WallRunStarted) != 0 ? 1 : 0);
                slide.volume = main != null ? main.volume : 1f;
                SlideAudioPlay(fpc);
                loopStopped = false;
                return;
            }

            var s = m.State;
            bool looping = s == MoveState.Slide || s == MoveState.WallRunLeft || s == MoveState.WallRunRight;
            if (looping || loopStopped)
                return;
            // Vanilla's fade-out (also catches the wall-slide loop vanilla starts when brushing a wall).
            slide.volume = Mathf.Lerp(slide.volume, 0f, 5f * Time.deltaTime);
            if (slide.volume < 0.03f)
            {
                SlideAudioStop(fpc);
                loopStopped = true;
            }
        }

        /// <summary>Vanilla's land bob: dip the camera pivot on hard landings, then ease it back.</summary>
        private static void LandDip(FirstPersonController fpc, MovementMotor m, float impactSpeed)
        {
            var pivot = LandBobPivot(fpc);
            if (pivot == null)
                return;
            if ((m.Events & MoveEvents.Landed) != 0 && impactSpeed > 3f && m.State != MoveState.Slide && pivot.localPosition.y > -0.4f)
            {
                float k = Mathf.Clamp(impactSpeed / 15f, 0.3f, 1f);
                pivot.localPosition -= Vector3.up * LandBobAmount(fpc) * k;
            }
            if (pivot.localPosition.y < -0.02f)
            {
                float rate = m.State == MoveState.Crouch ? 7f : m.IsGroundState ? 1f : 5f;
                pivot.localPosition = Vector3.Lerp(pivot.localPosition, Vector3.zero, LandRecoverSpeed(fpc) * Time.deltaTime * rate);
            }
        }

        /// <summary>Loop state is per player life; a new controller starts silent.</summary>
        public static void Reset() => loopStopped = true;
    }
}
