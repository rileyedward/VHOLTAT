namespace VHOLTAT.Movement
{
    /// <summary>
    /// Constants of the VHOLUME model (docs/MOVEMENT_FORMULAS.md), in metres and seconds. Values tagged CONFIRMED in the
    /// spec are VHOLUME's own; MEASURED ones were fitted from replays. The plugin binds every field to the config.
    /// </summary>
    public sealed class VholumeSettings
    {
        // §1 base physics (UE CharacterMovement with VHOLUME's values)
        public float Gravity = 21.56f;
        public float TerminalFallSpeed = 40f;
        public float JumpSpeed = 8f;
        public float MaxAcceleration = 100f;
        /// <summary>Braking with no input (or above the cap): dv/dt = -(BrakingDeceleration + BrakingFriction * v).</summary>
        public float BrakingDeceleration = 20.48f;
        public float BrakingFriction = 8f;
        /// <summary>How fast ground velocity turns toward the input direction (UE GroundFriction).</summary>
        public float GroundFriction = 8f;
        public float AirControl = 0.35f;

        // §2 speeds and boost
        public float RunSpeed = 13f;
        public float CrouchSpeed = 5f;
        public float BoostCap = 10f;
        public float BoostDecay = 0.8f;
        public float BoostDecayWallRun = 0.1f;
        /// <summary>Boost is cleared when the 3D speed drops to this.</summary>
        public float BoostResetSpeed = 10f;
        /// <summary>No boost decay for this long after a hop boost.</summary>
        public float BoostConserveTime = 0.5f;

        // §3 jump timing
        public float CoyoteTime = 0.33f;
        /// <summary>A coyote jump needs this long since the previous jump.</summary>
        public float CoyoteJumpCooldown = 0.5f;
        public float JumpBufferTime = 0.1f;

        // §4 landing, hop buffer, bunny hop, perfect hop
        public float HopWindow = 0.15f;
        public float HopWindowWallRun = 0.25f;
        /// <summary>Hop boost = HopGain * (speed - RunSpeed).</summary>
        public float HopGain = 0.2f;
        public float RealLandingAirTime = 0.1f;
        public float RealLandingFallSpeed = 2f;
        public float RealLandingCrouchTime = 0.25f;
        public float PerfectHopTime = 0.034f;
        public float PerfectHopSpan = 0.066f;
        public float PerfectHopPower = 0.6f;
        /// <summary>Hop loss line: LossAt26 at 26 m/s, rising LossSlope per m/s (extrapolated both ways, floored at 0).</summary>
        public float HopLossAt26 = 0.29f;
        public float HopLossSlope = 0.32f / 9f;
        /// <summary>Buffered-jump speed restore undoes this many ticks of braking.</summary>
        public float RestoreTicks = 1.25f;

        // §6 slide
        public float SlideSpeed = 16f;
        public float SlideMinStartSpeed = 7f;
        public float SlideTimerStart = 45f;
        public float SlideTimerFlatRate = 50f;
        public float SlideTimerDownRate = 20f;
        public float SlideTimerMediumRate = 100f;
        public float SlideTimerSteepRate = 300f;
        /// <summary>Above this timer value the slide cap applies; below it the crouch cap.</summary>
        public float SlideTimerMin = 5f;
        public float SlopeDown = 0.1f;
        public float SlopeGentle = -0.1f;
        public float SlopeMedium = -0.25f;
        public float SlopeSteep = -0.5f;
        /// <summary>Boost at slide start that turns steep upslopes into medium ones.</summary>
        public float SlideBoostDowngrade = 3f;
        /// <summary>The slide ends into a crouch at crouch speed plus this.</summary>
        public float SlideEndMargin = 0.5f;
        public float SlideStrafeScale = 0.3f;
        /// <summary>Releasing crouch caps speed at this (+ boost) for a moment.</summary>
        public float SlideReleaseCap = 12f;

        // §8 wall run
        public float WallRunSpeed = 16.5f;
        public float WallRunPush = 1f;
        public float WallRunUpSpeed = 2f;
        public float WallRunDownSpeed = -4f;
        public float WallRunZMax = 15f;
        public float WallRunTime = 1.5f;
        public float WallCurveStart = 0.2f;
        public float WallCurveSpan = 0.8f;
        public float WallCurvePower = 2.4f;
        public float WallMaxNormalY = 0.52f;
        /// <summary>Entry needs the 3D velocity within ~60 degrees of the along-wall direction (cosine).</summary>
        public float WallRunAlign = 0.5f;
        /// <summary>A new wall must differ from the last one by at least this many degrees.</summary>
        public float WallChainMinAngle = 30f;
        public float WallAwayInput = 0.2f;
        public float GateAfterRelease = 0.8f;
        public float GateAfterFail = 0.2f;
        public float GateAfterWallJump = 0.35f;
        public float GateAfterClimbTop = 1f;
        /// <summary>No wall run straight after a slide/crouch when the ground is this close below (min, max).</summary>
        public float UncrouchGroundMin = 0.35f;
        public float UncrouchGroundMax = 0.8f;

        // §9 climb
        public float ClimbUpSpeed = 4f;
        public float ClimbDownSpeed = -4f;
        public float ClimbFacing = 0.85f;
        public float ClimbToRunTime = 1f;
        public float ClimbToRunStart = 0.1f;

        // §10 vault
        public float VaultSmallHeight = 0.4f;
        public float VaultLargeHeight = 1.1f;
        public float VaultSmallTime = 0.05f, VaultMediumTime = 0.15f, VaultLargeTime = 0.4f;
        public float VaultSmallKeep = 0.95f, VaultMediumKeep = 0.8f, VaultLargeKeep = 0.7f;
        public float VaultSmallSpeedMin = 6f, VaultSmallSpeedMax = 14f, VaultSmallScaleMin = 0.5f;
        public float VaultMediumSpeedMin = 7f, VaultMediumSpeedMax = 14f, VaultMediumScaleMin = 0.5f;
        public float VaultLargeSpeedMin = 8f, VaultLargeSpeedMax = 13f, VaultLargeScaleMin = 0.6f;
        /// <summary>Below this average speed a vault keeps no momentum at all.</summary>
        public float VaultMinMomentum = 3f;

        // §11 wall jump
        public float WallJumpUpSpeed = 7.5f;
        public float WallJumpMinSpeed = 14f;
        public float WallJumpMinAngle = 30f;
    }
}
