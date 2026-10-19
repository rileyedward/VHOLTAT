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
                float baseFov = speed > 1f ? RunFov(fpc) : fpc.defaultFOV;
                float over = Mathf.InverseLerp(m.S.RunSpeed, cfg.FovFullSpeed.Value, speed);
                target = baseFov + over * cfg.FovSpeedBonus.Value;
            }
            float k = fpc.isAiming ? 20f : cfg.FovLerp.Value;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, target, 1f - Mathf.Exp(-k * Time.deltaTime));

            float tilt;
            switch (m.State)
            {
                case MoveState.WallRunRight: tilt = cfg.WallRunRoll.Value; break;
                case MoveState.WallRunLeft: tilt = -cfg.WallRunRoll.Value; break;
                case MoveState.Slide: tilt = -SlideTilt(fpc); break;
                default: tilt = 0f; break;
            }
            if (fpc.camController)
                SideTilt(fpc, tilt);
        }
    }
}
