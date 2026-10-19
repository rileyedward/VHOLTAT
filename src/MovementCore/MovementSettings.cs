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
        /// <summary>
        /// Gravity multiplier while rising after a ground jump (slide jumps, hops and coyote jumps too). 1 = VHOLUME
        /// exact (1.48 m, 0.37 s up). Higher makes the jump snappier and smaller; the fall and wall moves are unaffected.
        /// </summary>
        public float JumpRiseGravity = 1.25f;
        /// <summary>Ignore JumpRiseGravity (VHOLUME's exact jump). Toggled live by the plugin for comparison.</summary>
        public bool ExactVholumeJump;

        // Bunny hop
        /// <summary>After landing, jumping again within this window keeps speed above the run cap.</summary>
        public float HopWindow = 0.15f;
        /// <summary>Speed lost on each landing while above the run cap.</summary>
        public float HopLandingLoss = 0.21f;

        // Momentum ("speed boost"), as measured from VHOLUME's data and dev replays: speed above each state's base
        // (run, slide, wall run) is carried as one boost value. It never decays in the air, decays exponentially on the
        // ground (0.8/s) and barely on walls (0.1/s). Entering a wall run turns air speed above run speed into boost.
        public float BoostMax = 60f;
        public float BoostDecayGround = 0.8f;
        public float BoostDecaySlide = 0.8f;
        public float BoostDecayWallRun = 0.1f;
        /// <summary>Above this speed, hops are graded: only perfect ones keep all speed.</summary>
        public float PerfectHopSpeed = 26f;
        public float PerfectHopHighSpeed = 35f;
        /// <summary>Jumping this soon after landing is a perfect hop.</summary>
        public float PerfectHopWindow = 0.034f;
        public float MissedHopLoss = 0.29f;
        public float MissedHopLossHigh = 0.61f;
        /// <summary>
        /// 1 = entering a wall run turns air speed above run speed into boost (wall run to wall run chains stack).
        /// 0 = only the existing boost carries (the strictest reading of VHOLUME's replays).
        /// </summary>
        public float WallRunAirCarry = 1f;

        // Playtest 2 preset (2026-10-19): skill-fed boost instead of VHOLUME's carry rules. Only used when
        // Playtest2Model is true; the plugin's Preset setting switches it.
        public bool Playtest2Model;
        public float P2BoostMax = 12f;
        /// <summary>On landing, this fraction of speed above the new state's base+boost becomes boost.</summary>
        public float P2LandingCarry = 0.5f;
        public float P2SlideHopGain = 0.75f;
        public float P2WallJumpGain = 1f;
        public float P2PerfectHopGain = 0.5f;
        /// <summary>Multiplier on the speed gained sliding downhill (goes into boost).</summary>
        public float P2SlideSlopeGain = 1f;

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
        public float SlideHoldTime = 3f;
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
