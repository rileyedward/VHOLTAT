using System;
using FishNet.Component.Animating;
using HarmonyLib;
using UnityEngine;
using VHOLTAT.Movement;

namespace VHOLTAT.Adapter
{
    /// <summary>
    /// Runs the movement motor for the local player in place of FirstPersonController.ApplyFinalMovements.
    /// The rest of the vanilla Update (camera, weapons, crouch height, animation, footsteps) keeps running.
    ///
    /// Vanilla and other code push the player by writing moveDirection.y (explosions, vaults, pads, ladders) and
    /// through force fields (forceAdded, bforcefinal, customForceFinal...). Vertical writes are detected as a change
    /// against what we left in moveDirection last frame and fed to the motor as impulses; the horizontal force
    /// fields are added on top of the motor velocity exactly like vanilla does.
    /// </summary>
    internal static class MotorDriver
    {
        public static MovementMotor Motor;
        public static MovementSensors Sensors;
        /// <summary>The input the motor saw this frame (for the recorder).</summary>
        public static MovementInput LastInput;
        public static FirstPersonController Current;
        public static bool Active;

        /// <summary>Dev self-test: when set, replaces the player's input (move, jump, crouch) each frame.</summary>
        public static bool ScriptActive;
        public static Vector2 ScriptMove;
        public static bool ScriptCrouch, ScriptJump;

        private const float GroundStick = 4f;

        private static float baselineY;
        private static bool lastCrouch;
        private static bool jumpQueued;
        private static bool jumpHeld;

        private static readonly AccessTools.FieldRef<FirstPersonController, Vector3> BForceFinal =
            AccessTools.FieldRefAccess<FirstPersonController, Vector3>("bforcefinal");
        private static readonly AccessTools.FieldRef<FirstPersonController, Vector3> ObjectCollisionMove =
            AccessTools.FieldRefAccess<FirstPersonController, Vector3>("objectCollisionMoveDirection");
        private static readonly AccessTools.FieldRef<FirstPersonController, float> SetSpeedTimer =
            AccessTools.FieldRefAccess<FirstPersonController, float>("setSpeedTimer");
        private static readonly AccessTools.FieldRef<FirstPersonController, float> TempSetSpeed =
            AccessTools.FieldRefAccess<FirstPersonController, float>("tempSetSpeed");
        private static readonly AccessTools.FieldRef<FirstPersonController, float> SprintSpeed =
            AccessTools.FieldRefAccess<FirstPersonController, float>("sprintSpeed");
        private static readonly AccessTools.FieldRef<FirstPersonController, bool> OnLadder =
            AccessTools.FieldRefAccess<FirstPersonController, bool>("onLadder");
        private static readonly AccessTools.FieldRef<FirstPersonController, bool> CanJump =
            AccessTools.FieldRefAccess<FirstPersonController, bool>("canJump");
        private static readonly AccessTools.FieldRef<FirstPersonController, NetworkAnimator> NetAnimator =
            AccessTools.FieldRefAccess<FirstPersonController, NetworkAnimator>("networkAnimator");
        private static readonly Action<FirstPersonController, int> PlaySoundServer =
            AccessTools.MethodDelegate<Action<FirstPersonController, int>>(AccessTools.Method(typeof(FirstPersonController), "PlaySoundServer"));

        public static void Init(MovementSettings settings, bool active)
        {
            Motor = new MovementMotor(settings);
            Active = active;
        }

        public static bool IsLocal(FirstPersonController fpc) => fpc != null && fpc.IsOwner;

        public static void SetActive(bool active)
        {
            Active = active;
            if (Current != null)
            {
                Resync(Current);
                if (!active)
                    Current.isSliding = false;
            }
        }

        private static void Resync(FirstPersonController fpc)
        {
            var cc = fpc.characterController;
            Motor.Reset(SensorProbe.ToVec(cc != null ? cc.velocity : Vector3.zero), cc != null && cc.isGrounded);
            baselineY = fpc.moveDirection.y;
            jumpQueued = false;
            Feedback.Reset();
        }

        public static void QueueJump() => jumpQueued = true;
        public static void SetJumpHeld(bool held) => jumpHeld = held;

        public static void OnTeleport(FirstPersonController fpc, float yawDelta)
        {
            if (fpc != Current)
                return;
            Vector3 v = Quaternion.Euler(0f, yawDelta, 0f) * SensorProbe.ToUnity(Motor.Velocity);
            Motor.Velocity = SensorProbe.ToVec(v);
        }

        public static void BeforeUpdate(FirstPersonController fpc)
        {
            if (fpc != Current || Motor.State != MoveState.Ground)
                return;
            if (fpc.isCrouching && !lastCrouch && Motor.HorizontalSpeed >= Motor.S.SlideMinStartSpeed)
                fpc.isSliding = true;
        }

        /// <summary>Called from the end of vanilla Update: remembers moveDirection.y so the next frame can spot external writes.</summary>
        public static void AfterUpdate(FirstPersonController fpc)
        {
            if (fpc == Current)
                baselineY = fpc.moveDirection.y;
        }

        /// <summary>Replacement for ApplyFinalMovements. Returns false when vanilla should run instead.</summary>
        public static bool Drive(FirstPersonController fpc)
        {
            var cc = fpc.characterController;
            var pm = fpc.pauseManager;
            if (cc == null || pm == null || fpc.playerCamera == null)
                return false;

            if (fpc != Current)
            {
                Current = fpc;
                Resync(fpc);
            }

            if (pm.startRound || fpc.startOfRound)
                return true; // vanilla doesn't move the player during the round-start freeze either

            float dt = Time.deltaTime;
            float dy = fpc.moveDirection.y - baselineY;
            // Vanilla writes to moveDirection.y mean "set vertical speed" (knockback, vaults, ladders, pads).
            if (Mathf.Abs(dy) > 0.01f)
                Motor.AddImpulse(new Vec3(0f, fpc.moveDirection.y - Motor.Velocity.Y, 0f));

            bool canMove = fpc.sync___get_value_canMove();
            bool blocked = !canMove || pm.pause || (pm.steamPlaying && pm.chatting);

            Vector3 fwd = fpc.transform.forward;
            fwd.y = 0f;
            fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, fwd);

            Vector2 mv = blocked ? Vector2.zero : fpc.move.ReadValue<Vector2>();
            bool crouch = !blocked && fpc.isCrouching;
            if (ScriptActive && !blocked)
            {
                mv = ScriptMove;
                crouch = ScriptCrouch;
                jumpQueued |= ScriptJump;
                ScriptJump = false;
            }
            var input = new MovementInput
            {
                MoveX = mv.x,
                MoveY = mv.y,
                JumpPressed = jumpQueued && !blocked && CanJump(fpc),
                JumpHeld = jumpHeld && !blocked,
                CrouchHeld = crouch,
                CrouchPressed = crouch && !lastCrouch,
                Forward = SensorProbe.ToVec(fwd),
                Right = SensorProbe.ToVec(right),
                Look = SensorProbe.ToVec(fpc.playerCamera.transform.forward),
                SpeedScale = fpc.movementFactor * FireSlowdown(fpc),
            };
            jumpQueued = false;
            lastCrouch = crouch;

            LastInput = input;
            SensorProbe.Probe(fpc, cc, fwd, fpc.landLayer.value, ref Sensors);
            if (OnLadder(fpc))
            {
                // Vanilla ladders drive vertical speed themselves; don't turn them into wall runs or climbs.
                Sensors.WallFront = Sensors.WallLeft = Sensors.WallRight = false;
            }
            float fallSpeed = -Motor.Velocity.Y;
            Motor.Step(in input, in Sensors, dt);

            Vector3 vel = SensorProbe.ToUnity(Motor.Velocity);
            fpc.moveDirection = vel;
            baselineY = vel.y;
            fpc.isSliding = Motor.State == MoveState.Slide;

            Vector3 external = fpc.forceAdded + BForceFinal(fpc) + fpc.moveAdded + ObjectCollisionMove(fpc) + fpc.customForceFinal;
            var slope = fpc.slopeSlideScript;
            if (slope != null)
                external += slope.slopeSlideMove + slope.steepSlopeSlideMove;

            // Ground states press down a little (vanilla uses -1) so the CharacterController keeps reporting grounded.
            Vector3 stick = Motor.IsGroundState ? new Vector3(0f, -GroundStick, 0f) : Vector3.zero;
            Dev.PerfProbe.MoveBegin();
            CollisionFlags flags = cc.Move((vel + external + stick) * dt);
            Dev.PerfProbe.MoveEnd();
            AfterMove(cc, flags);
            PlayEvents(fpc);
            Feedback.Apply(fpc, Motor, fallSpeed);
            SpeedFx.Apply(fpc, Motor);
            return true;
        }

        /// <summary>
        /// Some weapons slow the shooter for a moment (Weapon.OnShoot -> SetSpeed, an absolute vanilla speed). Applied as
        /// the same fraction of vanilla sprint speed.
        /// </summary>
        private static float FireSlowdown(FirstPersonController fpc)
        {
            if (SetSpeedTimer(fpc) <= 0f)
                return 1f;
            float sprint = SprintSpeed(fpc);
            return sprint > 0.1f ? Mathf.Clamp01(TempSetSpeed(fpc) / sprint) : 1f;
        }

        private static void AfterMove(CharacterController cc, CollisionFlags flags)
        {
            if ((flags & CollisionFlags.Above) != 0 && Motor.Velocity.Y > 0f)
                Motor.Velocity.Y = 0f;

            // Running into an obstacle on the ground or in the air takes the blocked part of the speed away, so the
            // motor doesn't keep momentum it can't use. Wall states push into their wall on purpose.
            var state = Motor.State;
            if ((flags & CollisionFlags.Sides) != 0 && state != MoveState.WallRunLeft && state != MoveState.WallRunRight
                && state != MoveState.WallClimb && state != MoveState.Mantle)
            {
                Vector3 actual = cc.velocity;
                var flat = Motor.Velocity.Flat;
                var actualFlat = new Vec3(actual.x, 0f, actual.z);
                if (actualFlat.SqrMagnitude < flat.SqrMagnitude)
                {
                    Motor.Velocity.X = actualFlat.X;
                    Motor.Velocity.Z = actualFlat.Z;
                }
            }
        }

        private static void PlayEvents(FirstPersonController fpc)
        {
            var e = Motor.Events;
            if (e == MoveEvents.None)
                return;
            try
            {
                if ((e & MoveEvents.Jumped) != 0)
                {
                    NetAnimator(fpc)?.SetTrigger("Jump");
                    PlaySoundServer(fpc, (e & MoveEvents.WallJumped) != 0 ? 22 : 10);
                }
                else if ((e & MoveEvents.Landed) != 0)
                    PlaySoundServer(fpc, 11);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"movement event feedback failed: {ex.Message}");
            }
        }
    }
}
