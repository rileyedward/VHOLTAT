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

        public static void Apply(FirstPersonController fpc)
        {
            var cam = fpc.playerCamera;
            var m = MotorDriver.Motor;
            var cfg = Plugin.Settings;
            if (cam == null || m == null)
                return;

            float target;
            if (fpc.isAiming)
                target = fpc.zoomFOV;
            else
            {
                float speed = m.HorizontalSpeed;
                if (cfg.VholumeFov.Value)
                {
                    float kick = m.State == MoveState.WallRunLeft || m.State == MoveState.WallRunRight ? 15f
                        : m.State == MoveState.Slide ? 10f
                        : speed > m.S.RunSpeed * 0.75f ? 5f : 0f;
                    target = fpc.defaultFOV + kick;
                }
                else
                {
                    float baseFov = speed > 1f ? RunFov(fpc) : fpc.defaultFOV;
                    float over = Mathf.InverseLerp(m.S.RunSpeed, cfg.FovFullSpeed.Value, speed);
                    target = baseFov + over * cfg.FovSpeedBonus.Value;
                }
            }
            float k = fpc.isAiming ? 20f : cfg.FovLerp.Value;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-k * Time.deltaTime));

            // Positive tilt matches vanilla's strafe-left lean; the wall-run roll is away from the wall by default and
            // toward it with VholumeWallRunRoll (6 degrees, as in VHOLUME).
            float roll = cfg.VholumeWallRunRoll.Value ? -6f : cfg.WallRunRoll.Value;
            float tilt;
            switch (m.State)
            {
                case MoveState.WallRunRight: tilt = roll; break;
                case MoveState.WallRunLeft: tilt = -roll; break;
                case MoveState.Slide: tilt = -SlideTilt(fpc); break;
                default:
                    // VHOLUME's head tilt into the strafe, growing with speed up to run speed.
                    float strafe = fpc.move.ReadValue<Vector2>().x;
                    tilt = -strafe * cfg.StrafeTilt.Value * Mathf.Clamp01(m.HorizontalSpeed / m.S.RunSpeed);
                    break;
            }
            if (fpc.camController)
                SideTilt(fpc, tilt);
        }
    }
}
