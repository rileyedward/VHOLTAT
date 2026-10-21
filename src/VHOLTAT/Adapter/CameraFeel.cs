using System;
using HarmonyLib;
using UnityEngine;
using VHOLTAT.Movement;

namespace VHOLTAT.Adapter
{
    /// <summary>
    /// Replaces FirstPersonController.HandleCameraController for the local player while VHOLTAT is active:
    /// FOV widens with speed above the run speed, slides use the vanilla slide tilt, wall runs roll the camera
    /// away from the wall. Aiming keeps the vanilla zoom FOV.
    /// </summary>
    internal static class CameraFeel
    {
        private static readonly AccessTools.FieldRef<FirstPersonController, float> RunFov =
            AccessTools.FieldRefAccess<FirstPersonController, float>("runFOV");
        private static readonly AccessTools.FieldRef<FirstPersonController, float> SlideTilt =
            AccessTools.FieldRefAccess<FirstPersonController, float>("slideTiltAmount");
        private static readonly Action<FirstPersonController, float> SideTilt =
            AccessTools.MethodDelegate<Action<FirstPersonController, float>>(AccessTools.Method(typeof(FirstPersonController), "SideTilt"));

        /// <summary>Unity cameras take a vertical FOV; VHOLUME (Unreal) measures it horizontally.</summary>
        private static float VerticalFov(float horizontalDeg, float aspect) =>
            2f * Mathf.Atan(Mathf.Tan(horizontalDeg * 0.5f * Mathf.Deg2Rad) / Mathf.Max(0.1f, aspect)) * Mathf.Rad2Deg;

        /// <summary>VHOLUME's head tilt into the strafe, growing with speed up to run speed (positive = vanilla's strafe-left lean).</summary>
        private static float StrafeTilt(FirstPersonController fpc, MotorBase m, Config.PluginConfig cfg)
        {
            float strafe = fpc.move.ReadValue<Vector2>().x;
            return -strafe * cfg.StrafeTilt.Value * Mathf.Clamp01(m.HorizontalSpeed / m.RunSpeed);
        }

        public static void Apply(FirstPersonController fpc)
        {
            var cam = fpc.playerCamera;
            var m = MotorDriver.Motor;
            var cfg = Plugin.Settings;
            if (cam == null || m == null)
                return;

            bool wallRun = m.State == MoveState.WallRunLeft || m.State == MoveState.WallRunRight;
            float target, k, tilt;
            if (cfg.Camera.Value == Config.CameraStyle.Vholume)
            {
                // VHOLUME: horizontal FOV from its settings (100), +5 when running fast, +10 sliding or wall running,
                // eased at rate 10; roll 6 degrees toward the wall; head tilt into strafes.
                if (fpc.isAiming)
                    target = fpc.zoomFOV;
                else
                {
                    float kick = wallRun || m.State == MoveState.Slide ? 10f : m.HorizontalSpeed > m.RunSpeed * 0.75f ? 5f : 0f;
                    target = VerticalFov(cfg.VholumeFov.Value + kick, cam.aspect);
                }
                k = fpc.isAiming ? 20f : 10f;
                tilt = m.State == MoveState.WallRunRight ? -6f
                    : m.State == MoveState.WallRunLeft ? 6f
                    : StrafeTilt(fpc, m, cfg);
            }
            else
            {
                if (fpc.isAiming)
                    target = fpc.zoomFOV;
                else
                {
                    float speed = m.HorizontalSpeed;
                    float baseFov = speed > 1f ? RunFov(fpc) : fpc.defaultFOV;
                    float over = Mathf.InverseLerp(m.RunSpeed, cfg.FovFullSpeed.Value, speed);
                    target = baseFov + over * cfg.FovSpeedBonus.Value;
                }
                k = fpc.isAiming ? 20f : cfg.FovLerp.Value;
                float roll = cfg.WallRunRoll.Value;
                switch (m.State)
                {
                    case MoveState.WallRunRight: tilt = roll; break;
                    case MoveState.WallRunLeft: tilt = -roll; break;
                    case MoveState.Slide: tilt = -SlideTilt(fpc); break;
                    default: tilt = StrafeTilt(fpc, m, cfg); break;
                }
            }
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-k * Time.deltaTime));

            if (fpc.camController)
                SideTilt(fpc, tilt);
        }
    }
}
