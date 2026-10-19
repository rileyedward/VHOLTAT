using System;

namespace VHOLTAT.Movement
{
    public enum MoveState : byte
    {
        Ground,
        Crouch,
        Slide,
        Air,
        WallRunLeft,
        WallRunRight,
        WallClimb,
        Mantle,
    }

    [Flags]
    public enum MoveEvents : ushort
    {
        None = 0,
        Jumped = 1 << 0,
        Landed = 1 << 1,
        SlideStarted = 1 << 2,
        SlideEnded = 1 << 3,
        WallRunStarted = 1 << 4,
        WallRunEnded = 1 << 5,
        WallJumped = 1 << 6,
        WallClimbStarted = 1 << 7,
        MantleStarted = 1 << 8,
        CoyoteJump = 1 << 9,
        BufferedJump = 1 << 10,
        VaultStarted = 1 << 11,
    }

    /// <summary>One frame of player intent. Directions are unit vectors in world space, Y up.</summary>
    public struct MovementInput
    {
        /// <summary>Strafe (-1..1, right positive) and forward (-1..1).</summary>
        public float MoveX, MoveY;
        public bool JumpPressed, JumpHeld;
        public bool CrouchPressed, CrouchHeld;
        /// <summary>Flat (Y = 0) facing and right vectors.</summary>
        public Vec3 Forward, Right;
        /// <summary>Full look direction (may point up or down).</summary>
        public Vec3 Look;
        /// <summary>Ground speed multiplier from the game (weapons, aiming). 0 is treated as 1.</summary>
        public float SpeedScale;
    }

    /// <summary>What the engine adapter sensed around the player this frame.</summary>
    public struct MovementSensors
    {
        public Vec3 Position;
        public bool Grounded;
        public Vec3 GroundNormal;
        /// <summary>True when there is no room to stand up.</summary>
        public bool CeilingBlocked;
        public float HeightAboveGround;

        public bool WallLeft, WallRight, WallFront;
        public Vec3 WallLeftNormal, WallRightNormal, WallFrontNormal;

        /// <summary>A ledge the player can be pulled onto, and the feet position on top of it.</summary>
        public bool LedgeFound;
        public Vec3 LedgeTarget;

        /// <summary>Waist-high cover ahead that can be vaulted from the ground, its standing spot and its height above the feet.</summary>
        public bool VaultFound;
        public Vec3 VaultTarget;
        public float VaultHeight;
    }
}
