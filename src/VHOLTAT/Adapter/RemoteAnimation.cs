using HarmonyLib;
using UnityEngine;
using VHOLTAT.Movement;

namespace VHOLTAT.Adapter
{
    /// <summary>
    /// Other players see us through the owner's animator parameters (synced by NetworkAnimator), so after vanilla
    /// HandleAnimation we correct them for VHOLTAT states: wall runs and climbs read as running (leaning away from
    /// the wall), and fast ground movement uses the full run animation. Peers need no mod for this.
    /// </summary>
    internal static class RemoteAnimation
    {
        private static readonly AccessTools.FieldRef<FirstPersonController, Animator> AnimatorRef =
            AccessTools.FieldRefAccess<FirstPersonController, Animator>("animator");

        private static readonly int Grounded = Animator.StringToHash("Grounded");
        private static readonly int MovementSpeed = Animator.StringToHash("MovementSpeed");
        private static readonly int LeanLeft = Animator.StringToHash("LeanLeft");
        private static readonly int LeanRight = Animator.StringToHash("LeanRight");

        public static void Apply(FirstPersonController fpc)
        {
            var anim = AnimatorRef(fpc);
            var m = MotorDriver.Motor;
            if (anim == null || m == null)
                return;

            switch (m.State)
            {
                case MoveState.WallRunLeft:
                case MoveState.WallRunRight:
                    anim.SetBool(Grounded, true);
                    anim.SetFloat(MovementSpeed, 1f);
                    anim.SetBool(LeanLeft, m.State == MoveState.WallRunRight);
                    anim.SetBool(LeanRight, m.State == MoveState.WallRunLeft);
                    break;
                case MoveState.WallClimb:
                    anim.SetBool(Grounded, true);
                    anim.SetFloat(MovementSpeed, 1f);
                    break;
                case MoveState.Ground:
                    if (m.HorizontalSpeed > 7f)
                        anim.SetFloat(MovementSpeed, 1f);
                    break;
            }
        }
    }
}
