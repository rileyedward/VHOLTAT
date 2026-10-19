namespace VHOLTAT.Movement
{
    /// <summary>
    /// Every tunable of the movement model, in metres and seconds. Defaults come from docs/MOVEMENT_SPEC.md
    /// (VHOLUME's cm values / 100). The plugin overwrites these from BepInEx config.
    /// </summary>
    public sealed class MovementSettings
    {
        // Global
        public float Gravity = 21.56f;
        public float TerminalFallSpeed = 40f;

        // Ground
        public float RunSpeed = 13f;
        public float GroundAcceleration = 100f;
        public float GroundBraking = 50f;
        /// <summary>How fast speed above the run cap bleeds off on the ground once the hop window has passed.</summary>
        public float OverspeedDecay = 30f;
        public float CrouchSpeed = 5f;

        // Air
        /// <summary>Steering acceleration in the air (spec: about 0.35 of ground acceleration).</summary>
        public float AirAcceleration = 35f;

        // Jump
        public float JumpSpeed = 8f;
        public float CoyoteTime = 0.33f;
        public float JumpBufferTime = 0.1f;

        // Bunny hop
        /// <summary>After landing, jumping again within this window keeps speed above the run cap.</summary>
        public float HopWindow = 0.15f;
        /// <summary>Speed lost on each landing while above the run cap.</summary>
        public float HopLandingLoss = 0.21f;

        // Slide
        public float SlideMinStartSpeed = 7f;
        public float SlideSpeed = 16f;
        /// <summary>Slides below this speed turn into a crouch.</summary>
        public float SlideEndSpeed = 12f;
        public float SlideDecelFlat = 0.2f;
        public float SlideDecelMediumUpslope = 1f;
        public float SlideDecelSteepUpslope = 3f;
        public float SlopeGentle = -0.1f;
        public float SlopeMedium = -0.25f;
        public float SlopeSteep = -0.5f;
        /// <summary>Flat slides hold speed this long, then friction starts (keeps slides from being endless).</summary>
        public float SlideHoldTime = 1.5f;
        public float SlideFriction = 6f;
        /// <summary>How fast a slide can turn toward the move input, degrees per second.</summary>
        public float SlideSteerRate = 60f;

        // Wall run
        public float WallRunSpeed = 16.5f;
        public float WallRunDuration = 1.5f;
        public float WallRunStartUpSpeed = 2f;
        public float WallRunHoldTime = 0.4f;
        /// <summary>Fraction of gravity reached at the end of the run (ramps from 0 after the hold).</summary>
        public float WallRunGravityEnd = 0.5f;
        /// <summary>Wall normals with |y| above this are floors/ceilings, not walls.</summary>
        public float WallMaxNormalY = 0.52f;
        /// <summary>Velocity must point at least this much along the wall (cosine) to start a run.</summary>
        public float WallRunMinAlignment = 0.2f;
        public float WallRunMinStartSpeed = 4f;
        public float WallRunMinHeight = 0.6f;
        /// <summary>Move input pushing away from the wall beyond this ends the run.</summary>
        public float WallRunPushOff = 0.2f;
        public float WallJumpUpSpeed = 7.5f;
        public float WallJumpMinAngle = 30f;
        /// <summary>After leaving a wall run without jumping, a wall jump is still allowed this long.</summary>
        public float WallJumpGrace = 0.33f;

        // Wall climb
        public float WallClimbSpeed = 4f;
        public float WallClimbHoldTime = 0.4f;
        public float WallClimbDuration = 0.8f;
        /// <summary>Upward speed lost per second once the hold time has passed.</summary>
        public float WallClimbDecay = 1.2f;
        /// <summary>Facing the wall: dot(forward, -normal) at least this.</summary>
        public float WallClimbMinFacing = 0.85f;
        public float WallClimbPushOff = 14f;

        // Mantle
        public float MantleSpeed = 5f;
        public float MantleMaxTime = 0.45f;
        /// <summary>Horizontal speed kept after a mantle (spec's small-vault momentum multiplier).</summary>
        public float MantleMomentumKeep = 0.95f;
        public float MantleMinExitSpeed = 3f;
    }
}
