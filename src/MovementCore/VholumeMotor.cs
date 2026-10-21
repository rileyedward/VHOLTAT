using System;

namespace VHOLTAT.Movement
{
    /// <summary>
    /// VHOLUME's movement rules as written in docs/MOVEMENT_FORMULAS.md (decoded from VHOLUME's script and fitted to its
    /// replays). Ground and air physics follow Unreal's CharacterMovement model with VHOLUME's values; parkour moves set
    /// velocity directly, as VHOLUME does. Section numbers in comments refer to that document. Metres and seconds.
    /// No allocations after construction.
    /// </summary>
    public sealed class VholumeMotor : MotorBase
    {
        public readonly VholumeSettings S;

        public override float RunSpeed => S.RunSpeed;
        public override float SlideStartSpeed => S.SlideMinStartSpeed;
        public override string ModelName => "VHOLUME";

        private const float DegToRad = (float)(Math.PI / 180.0);

        private float time;
        private float maxWalk;          // §2 MaxWalkSpeed this tick
        private float slideCap;         // §6 slide cap, kept while a slide continues in the air
        private bool manualBoost;       // §2 boost only exists while a boost source set this

        // §3 jump timing
        private float jumpPressTime = -999f;
        private float lastJumpTime = -999f;
        private float fallingTime;
        private bool fallingJumpAvailable;
        private float airTime, peakFall;
        private float lastCrouchPressTime = -999f;

        // §4 landing and hops
        private float hopBufferUntil = -999f;
        private float landSpeed3D;
        private float conserveUntil = -999f;
        private bool bufferedJumpPending;
        private Vec3 landVelocity;
        private float landCap, landTime;
        private MoveState lastHopState;
        private float lastHopTime = -999f;
        private Vec3 lastHopPosition;

        // §6 slide
        private float slideTimer;
        private bool slideDowngrade;
        private bool slideAir;          // slide continuing in the air (crouch held after a slide jump)
        private float releaseCap = -1f; // one-tick cap after releasing crouch out of a slide

        // §8/§9 walls
        private float gateClosedTime;
        private bool gateNeedsTakeoff;
        private int blockedSide;        // +1 right, -1 left, 0 none
        private int wallSide;
        private float wallTau;
        private float climbMult = 1f;
        private bool climbRamp;
        private bool hasLastWall;
        private Vec3 lastWallNormal;
        private bool wallJumpPending;
        private bool storedWallJump;
        private Vec3 storedNormal;
        private int storedSide;
        private float storedSpeed;
        private MoveState groundStateAtTakeoff = MoveState.Ground;

        // §10 vault
        private Vec3 vaultStart, vaultDest, vaultExitDir;
        private float vaultDuration, vaultKeep, vaultAvgSpeed;
        private readonly Vec3[] historySum = new Vec3[10];
        private readonly float[] historyTime = new float[10];
        private int historySlot;
        private float historySlotTime;

        public VholumeMotor(VholumeSettings settings)
        {
            S = settings;
            maxWalk = S.RunSpeed;
        }

        public override void Reset(Vec3 velocity, bool grounded)
        {
            Velocity = velocity;
            Boost = 0f;
            manualBoost = false;
            State = grounded ? MoveState.Ground : MoveState.Air;
            StateTime = 0f;
            maxWalk = S.RunSpeed;
            jumpPressTime = lastJumpTime = lastCrouchPressTime = hopBufferUntil = conserveUntil = lastHopTime = -999f;
            fallingTime = airTime = peakFall = 0f;
            fallingJumpAvailable = bufferedJumpPending = slideAir = wallJumpPending = storedWallJump = false;
            gateClosedTime = 0f;
            gateNeedsTakeoff = false;
            blockedSide = 0;
            hasLastWall = false;
            climbMult = 1f;
            climbRamp = false;
            releaseCap = -1f;
            for (int i = 0; i < historySum.Length; i++) { historySum[i] = Vec3.Zero; historyTime[i] = 0f; }
        }

        public override void AddImpulse(Vec3 deltaV)
        {
            Velocity += deltaV;
            if (deltaV.Y > 0.5f && State != MoveState.Air)
            {
                EndWallMoves(0f, false);
                TakeOff(jumped: true);
            }
        }

        public override void CancelHorizontal()
        {
            Velocity.X = 0f;
            Velocity.Z = 0f;
            Boost = 0f;
            manualBoost = false;
        }

        public override void Step(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (dt <= 0f)
                return;
            Events = MoveEvents.None;
            time += dt;
            StateTime += dt;
            RecordVelocity(dt);
            if (input.JumpPressed)
                jumpPressTime = time - dt * 0.5f;  // §3 press time minus half a frame
            if (input.CrouchPressed)
                lastCrouchPressTime = time;
            if (gateClosedTime > 0f && (gateClosedTime -= dt) <= 0f)
                blockedSide = 0;

            if (State == MoveState.Mantle)
            {
                UpdateVault(in sensors, dt);
                DecayBoost(dt);
                return;
            }

            if (sensors.Grounded && !IsGroundState && Velocity.Y <= 0.01f)
                Land(in input, in sensors);
            else if (!sensors.Grounded && IsGroundState)
                TakeOff(jumped: false);

            CalculateMaxSpeed(in input);

            switch (State)
            {
                case MoveState.Ground: UpdateGround(in input, in sensors, dt); break;
                case MoveState.Crouch: UpdateCrouch(in input, in sensors, dt); break;
                case MoveState.Slide: UpdateSlide(in input, in sensors, dt); break;
                case MoveState.Air: UpdateAir(in input, in sensors, dt); break;
                case MoveState.WallRunLeft:
                case MoveState.WallRunRight: UpdateWallRun(in input, in sensors, dt); break;
                case MoveState.WallClimb: UpdateClimb(in input, in sensors, dt); break;
            }
            DecayBoost(dt);
        }

        // ------------------------------------------------------------------ §2 max speed and boost

        private void CalculateMaxSpeed(in MovementInput input)
        {
            if (!manualBoost)
                Boost = 0f;
            float scale = input.SpeedScale > 0f ? input.SpeedScale : 1f;
            bool noInput = Math.Abs(input.MoveX) < 0.01f && Math.Abs(input.MoveY) < 0.01f;
            if (State == MoveState.Slide || State == MoveState.Crouch)
                return; // slide and crouch own the cap
            if (noInput && IsGroundState)
                maxWalk = S.RunSpeed * scale;
            else if (releaseCap >= 0f)
            {
                maxWalk = releaseCap;
                releaseCap = -1f;
            }
            else
                maxWalk = (S.RunSpeed + Boost) * scale;
        }

        private void DecayBoost(float dt)
        {
            if (!manualBoost)
            {
                Boost = 0f;
                return;
            }
            if (Velocity.Magnitude <= S.BoostResetSpeed)
            {
                Boost = 0f;
                manualBoost = false;
                return;
            }
            bool wallRun = State == MoveState.WallRunLeft || State == MoveState.WallRunRight;
            bool hopOpen = time <= hopBufferUntil;
            if (time < conserveUntil || (hopOpen && (wallRun || State == MoveState.Ground || State == MoveState.Slide
                                                     || State == MoveState.Crouch || State == MoveState.Air)))
                return;
            Boost *= (float)Math.Exp(-(wallRun ? S.BoostDecayWallRun : S.BoostDecay) * dt);
            if (Boost < 0.01f)
            {
                Boost = 0f;
                manualBoost = false;
            }
        }

        // ------------------------------------------------------------------ transitions

        private void SetState(MoveState next)
        {
            State = next;
            StateTime = 0f;
        }

        private void TakeOff(bool jumped)
        {
            groundStateAtTakeoff = State;
            if (State == MoveState.Slide)
                Events |= MoveEvents.SlideEnded;
            slideAir = State == MoveState.Slide;
            if (!jumped && (State == MoveState.Ground || State == MoveState.Slide || State == MoveState.Crouch))
            {
                fallingJumpAvailable = true; // §3 coyote armed by walking -> falling
                fallingTime = 0f;
            }
            gateNeedsTakeoff = false;        // §4 gates reopen on walking -> falling
            hasLastWall = false;
            airTime = 0f;
            peakFall = 0f;
            SetState(MoveState.Air);
        }

        private void Land(in MovementInput input, in MovementSensors sensors)
        {
            var prev = State;
            Events |= MoveEvents.Landed;
            bool real = airTime >= S.RealLandingAirTime || peakFall >= S.RealLandingFallSpeed
                        || time - lastCrouchPressTime <= S.RealLandingCrouchTime;

            // §4 hop buffer: from a wall run any landing opens 0.25 s; otherwise real landings in None/Slide open 0.15 s.
            if (prev == MoveState.WallRunLeft || prev == MoveState.WallRunRight)
            {
                landSpeed3D = Velocity.Magnitude;
                hopBufferUntil = time + S.HopWindowWallRun;
                Events |= MoveEvents.WallRunEnded;
            }
            else if (real && (prev == MoveState.Air || slideAir))
            {
                landSpeed3D = Velocity.Magnitude;
                hopBufferUntil = time + S.HopWindow;
            }

            landVelocity = Velocity.Flat;
            landCap = maxWalk;
            landTime = time;
            bufferedJumpPending = time - jumpPressTime <= S.JumpBufferTime;  // §3 buffered jump fires next tick

            storedWallJump = false;
            gateNeedsTakeoff = true;  // §4 gates closed until the next takeoff
            climbRamp = false;
            climbMult = 1f;
            Velocity.Y = 0f;

            bool canSlide = input.MoveY > 0f && Velocity.Magnitude > S.SlideMinStartSpeed;
            if (input.CrouchHeld && slideAir)
                SetState(MoveState.Slide);  // §5/§6 the slide continues with its timer
            else if (input.CrouchHeld && canSlide)
                StartSlide();
            else if (input.CrouchHeld)
                SetState(MoveState.Crouch);
            else
                SetState(MoveState.Ground);
            slideAir = false;
        }

        // ------------------------------------------------------------------ ground (§1, §3, §4, §6, §7)

        private void UpdateGround(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (TryGroundJump(in input, in sensors, dt))
                return;
            if (input.CrouchPressed)
            {
                if (input.MoveY > 0f && Velocity.Magnitude > S.SlideMinStartSpeed)
                {
                    StartSlide();
                    UpdateSlide(in input, in sensors, dt);
                    return;
                }
                SetState(MoveState.Crouch);
                UpdateCrouch(in input, in sensors, dt);
                return;
            }
            Walk(Wish(in input, 1f), maxWalk, dt);
            FollowGround(in sensors);
        }

        private void UpdateCrouch(in MovementInput input, in MovementSensors sensors, float dt)
        {
            float scale = input.SpeedScale > 0f ? input.SpeedScale : 1f;
            maxWalk = S.CrouchSpeed * scale;
            if (!sensors.CeilingBlocked)
            {
                if (TryGroundJump(in input, in sensors, dt))
                    return;
                if (!input.CrouchHeld)
                {
                    SetState(MoveState.Ground);
                    CalculateMaxSpeed(in input);
                    UpdateGround(in input, in sensors, dt);
                    return;
                }
            }
            Walk(Wish(in input, 1f), maxWalk, dt);
            FollowGround(in sensors);
        }

        private void StartSlide()
        {
            SetState(MoveState.Slide);
            slideTimer = S.SlideTimerStart;
            slideDowngrade = Boost >= S.SlideBoostDowngrade;
            Events |= MoveEvents.SlideStarted;
        }

        private void UpdateSlide(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (!sensors.CeilingBlocked)
            {
                if (TryGroundJump(in input, in sensors, dt))
                    return;
                if (!input.CrouchHeld)
                {
                    Events |= MoveEvents.SlideEnded;
                    releaseCap = Math.Min(HorizontalSpeed, S.SlideReleaseCap) + Boost;
                    SetState(MoveState.Ground);
                    CalculateMaxSpeed(in input);
                    UpdateGround(in input, in sensors, dt);
                    return;
                }
            }

            // §6 slide timer by slope class: d = floor normal . actor forward.
            float d = Vec3.Dot(sensors.GroundNormal, input.Forward);
            float target = 0f, rate;
            if (d >= S.SlopeDown) { target = 100f; rate = S.SlideTimerDownRate; }
            else if (d <= S.SlopeSteep) rate = slideDowngrade ? S.SlideTimerMediumRate : S.SlideTimerSteepRate;
            else if (d <= S.SlopeMedium) rate = S.SlideTimerMediumRate;
            else rate = S.SlideTimerFlatRate;
            slideTimer = MoveTowards(slideTimer, target, rate * dt);

            float scale = input.SpeedScale > 0f ? input.SpeedScale : 1f;
            slideCap = ((slideTimer > S.SlideTimerMin ? S.SlideSpeed : S.CrouchSpeed) + Boost) * scale;
            maxWalk = slideCap;
            Walk(Wish(in input, S.SlideStrafeScale), slideCap, dt);
            FollowGround(in sensors);

            if (Velocity.Magnitude <= S.CrouchSpeed + S.SlideEndMargin)
            {
                Events |= MoveEvents.SlideEnded;
                SetState(MoveState.Crouch);
            }
        }

        private bool TryGroundJump(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (bufferedJumpPending && time > landTime)  // fires on the tick after landing
            {
                bufferedJumpPending = false;
                RestoreLandingSpeed(dt);
                Events |= MoveEvents.BufferedJump;
                GroundJump(in input, in sensors);
                return true;
            }
            if (!input.JumpPressed)
                return false;
            GroundJump(in input, in sensors);
            return true;
        }

        private void GroundJump(in MovementInput input, in MovementSensors sensors)
        {
            if (time <= hopBufferUntil)
                CheckForBunnyHop(in sensors);
            bool slide = State == MoveState.Slide && input.CrouchHeld;
            Velocity.Y = S.JumpSpeed;
            lastJumpTime = time;
            fallingJumpAvailable = false;
            jumpPressTime = -999f;
            TakeOff(jumped: true);
            slideAir = slide;
            Events |= MoveEvents.Jumped;
        }

        /// <summary>§4 hop boost: 20 % of the speed above run speed, from the landing or the current speed.</summary>
        private void CheckForBunnyHop(in MovementSensors sensors)
        {
            float s = Math.Max(Velocity.Magnitude, landSpeed3D);
            if (s > S.RunSpeed)
            {
                bool sameContact = (State == MoveState.Slide || State == MoveState.Crouch) && lastHopState == State
                    && time - lastHopTime < 0.12f && (sensors.Position - lastHopPosition).Magnitude < 1.5f;
                if (!sameContact)
                {
                    Boost = Math.Min(S.BoostCap, Boost + S.HopGain * (s - S.RunSpeed));
                    manualBoost = true;
                    conserveUntil = time + S.BoostConserveTime;
                    lastHopState = State;
                    lastHopTime = time;
                    lastHopPosition = sensors.Position;
                }
            }
            hopBufferUntil = -999f;
            landSpeed3D = 0f;
        }

        /// <summary>§4 buffered-jump landing restore with perfect-hop grading.</summary>
        private void RestoreLandingSpeed(float dt)
        {
            var now = Velocity.Flat;
            float vNow = now.Magnitude, vLand = landVelocity.Magnitude;
            if (vNow < 0.01f || vLand <= vNow || Vec3.Dot(landVelocity / vLand, now / vNow) < 0.9f)
                return;
            float r = Math.Min(vLand, vNow + S.RestoreTicks * dt * (S.BrakingFriction * vLand + S.BrakingDeceleration));
            float c = landCap;
            if (r <= c)
                return;
            if (r <= 1.01f * c)
                r = c;
            else
            {
                float e = landTime - jumpPressTime;
                float x = Math.Max(0f, Math.Min(1f, (e - S.PerfectHopTime) / S.PerfectHopSpan));
                float grade = 1f - (float)Math.Pow(x, S.PerfectHopPower);
                float baseLoss = Math.Max(0f, S.HopLossAt26 + (r - 26f) * S.HopLossSlope);
                r = Math.Max(r - baseLoss * (1f - grade), c);
            }
            var v = now / vNow * r;
            Velocity.X = v.X;
            Velocity.Z = v.Z;
        }

        // ------------------------------------------------------------------ air (§3, §5)

        private void UpdateAir(in MovementInput input, in MovementSensors sensors, float dt)
        {
            airTime += dt;
            if (Velocity.Y < 0f)
            {
                fallingTime += dt;
                peakFall = Math.Max(peakFall, -Velocity.Y);
            }
            else
                fallingTime = 0f;
            if (fallingTime >= S.CoyoteTime)
                storedWallJump = false;

            if (input.JumpPressed)
            {
                if (fallingJumpAvailable && fallingTime < S.CoyoteTime && time - lastJumpTime > S.CoyoteJumpCooldown)
                {
                    if (storedWallJump)
                    {
                        WallJump(in input, storedNormal, storedSide, storedSpeed, fromClimb: false);
                        return;
                    }
                    Events |= MoveEvents.CoyoteJump;
                    Velocity.Y = S.JumpSpeed;
                    lastJumpTime = time;
                    fallingJumpAvailable = false;
                    fallingTime = 0f;
                    Events |= MoveEvents.Jumped;
                    return;
                }
            }

            if (slideAir && !input.CrouchHeld)
            {
                slideAir = false;  // §5 releasing crouch in the air ends the slide
                maxWalk = Math.Min(HorizontalSpeed, S.SlideReleaseCap) + Boost;
            }

            if (TryVault(in input, in sensors) || TryWallRunOrClimb(in input, in sensors, fromClimb: false))
                return;

            AirMove(Wish(in input, 1f), slideAir ? slideCap : maxWalk, dt);
            Velocity.Y = Math.Max(Velocity.Y - S.Gravity * dt, -S.TerminalFallSpeed);
        }

        // ------------------------------------------------------------------ walls (§8, §9, §11)

        private bool GateAllows(int side)
        {
            if (gateNeedsTakeoff)
                return false;
            if (gateClosedTime <= 0f)
                return true;
            return blockedSide != 0 && side == -blockedSide; // §8 the opposite side may start while one is blocked
        }

        private bool CanWallRun(in MovementInput input, in MovementSensors sensors)
        {
            if (input.MoveY <= 0f || input.CrouchHeld)
                return false;
            if ((groundStateAtTakeoff == MoveState.Slide || groundStateAtTakeoff == MoveState.Crouch)
                && State == MoveState.Air && airTime < 0.5f
                && sensors.HeightAboveGround >= S.UncrouchGroundMin && sensors.HeightAboveGround <= S.UncrouchGroundMax)
                return false;  // §7 no wall run right after uncrouching low over the ground
            return true;
        }

        private bool ValidWall(Vec3 n) => Math.Abs(n.Y) <= S.WallMaxNormalY;

        /// <summary>Along-wall unit direction for a wall normal, pointing the way the given velocity goes.</summary>
        private static Vec3 AlongWall(Vec3 normal, Vec3 velocity, Vec3 fallback)
        {
            Vec3 n = normal.Flat.Normalized;
            Vec3 t = Vec3.Cross(n, Vec3.Up).Normalized;
            Vec3 refDir = velocity.Flat.SqrMagnitude > 0.01f ? velocity.Flat : fallback;
            return Vec3.Dot(t, refDir) >= 0f ? t : -t;
        }

        private bool TryWallRunOrClimb(in MovementInput input, in MovementSensors sensors, bool fromClimb)
        {
            if (!CanWallRun(in input, in sensors))
                return false;

            // §9 force climb: facing a wall close ahead
            bool facingWall = sensors.WallFront && ValidWall(sensors.WallFrontNormal)
                && Vec3.Dot(input.Forward, -sensors.WallFrontNormal.Flat.Normalized) > S.ClimbFacing;
            if (!fromClimb && facingWall && GateAllows(0))
            {
                StartClimb(sensors.WallFrontNormal);
                return true;
            }

            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? 1 : -1;  // right first
                bool hit = side > 0 ? sensors.WallRight : sensors.WallLeft;
                Vec3 n = side > 0 ? sensors.WallRightNormal : sensors.WallLeftNormal;
                if (!hit || !ValidWall(n) || !GateAllows(side))
                    continue;
                // pressing away from that wall
                if ((side > 0 && input.MoveX < -S.WallAwayInput) || (side < 0 && input.MoveX > S.WallAwayInput))
                    continue;
                if (hasLastWall && Vec3.Dot(n.Flat.Normalized, lastWallNormal) > (float)Math.Cos(S.WallChainMinAngle * DegToRad))
                    continue;  // §8 chaining needs a different wall
                if (!fromClimb)
                {
                    Vec3 along = AlongWall(n, Velocity, input.Forward);
                    var v3 = Velocity.Normalized;
                    if (Vec3.Dot(along, v3) < S.WallRunAlign)
                        continue;
                }
                StartWallRun(side, n, fromClimb, in input);
                return true;
            }

            if (!fromClimb && facingWall == false && sensors.WallFront && ValidWall(sensors.WallFrontNormal) && GateAllows(0)
                && Vec3.Dot(input.Forward, -sensors.WallFrontNormal.Flat.Normalized) > S.ClimbFacing)
            {
                StartClimb(sensors.WallFrontNormal);
                return true;
            }
            return false;
        }

        private void StartWallRun(int side, Vec3 n, bool fromClimb, in MovementInput input)
        {
            wallSide = side;
            WallNormal = n;
            if (!fromClimb)
                wallTau = 0f;   // the clock continues from a climb
            climbRamp = fromClimb;
            climbMult = fromClimb ? S.ClimbToRunStart : 1f;
            SetState(side > 0 ? MoveState.WallRunRight : MoveState.WallRunLeft);
            Events |= MoveEvents.WallRunStarted;
            wallJumpPending = time - jumpPressTime <= S.JumpBufferTime;  // §3 buffered jump onto a wall run
            fallingJumpAvailable = false;
        }

        private void StartClimb(Vec3 n)
        {
            WallNormal = n;
            wallTau = 0f;
            SetState(MoveState.WallClimb);
            Events |= MoveEvents.WallClimbStarted;
        }

        /// <summary>§8 measured curve c(tau): 0 until 0.2, then ((tau - 0.2)/0.8)^2.4, capped at 1.</summary>
        private float Curve(float tau)
        {
            if (tau <= S.WallCurveStart)
                return 0f;
            float x = Math.Min(1f, (tau - S.WallCurveStart) / S.WallCurveSpan);
            return (float)Math.Pow(x, S.WallCurvePower);
        }

        private void EndWallMoves(float gate, bool blockSide)
        {
            if (State == MoveState.WallRunLeft || State == MoveState.WallRunRight)
            {
                Events |= MoveEvents.WallRunEnded;
                lastWallNormal = WallNormal.Flat.Normalized;
                hasLastWall = true;
                if (blockSide)
                    blockedSide = wallSide;
            }
            if (gate > 0f)
                gateClosedTime = Math.Max(gateClosedTime, gate);
            climbRamp = false;
            climbMult = 1f;
            SetState(MoveState.Air);
        }

        private void UpdateWallRun(in MovementInput input, in MovementSensors sensors, float dt)
        {
            bool right = State == MoveState.WallRunRight;
            if (wallJumpPending || input.JumpPressed)
            {
                wallJumpPending = false;
                WallJump(in input, WallNormal, wallSide, S.WallRunSpeed + Boost, fromClimb: false);
                return;
            }
            if (TryVault(in input, in sensors))
                return;

            bool away = (right && input.MoveX < -S.WallAwayInput) || (!right && input.MoveX > S.WallAwayInput);
            if (input.MoveY <= 0f || away)
            {
                // §8 released or pressing away: store a wall jump, block this side, gate 0.8 s
                storedWallJump = true;
                storedNormal = WallNormal;
                storedSide = wallSide;
                storedSpeed = S.WallRunSpeed + Boost;
                EndWallMoves(S.GateAfterRelease, true);
                AirMove(Wish(in input, 1f), maxWalk, dt);
                return;
            }
            if (input.CrouchHeld)
            {
                EndWallMoves(S.GateAfterFail, false);
                return;
            }

            bool hasWall = right ? sensors.WallRight : sensors.WallLeft;
            Vec3 n = right ? sensors.WallRightNormal : sensors.WallLeftNormal;
            if (!hasWall && sensors.WallFront && Vec3.Dot(sensors.WallFrontNormal.Flat.Normalized, WallNormal.Flat.Normalized) > 0.9f)
            {
                hasWall = true;   // §8 direct player-to-wall trace along the last normal
                n = sensors.WallFrontNormal;
            }
            if (hasWall && ValidWall(n))
                WallNormal = n;
            else
            {
                // §8 wall ran out: try a climb, else end with the side blocked, gate 0.2 s and coyote armed
                if (sensors.WallFront && ValidWall(sensors.WallFrontNormal)
                    && Vec3.Dot(input.Forward, -sensors.WallFrontNormal.Flat.Normalized) > S.ClimbFacing)
                {
                    StartClimb(sensors.WallFrontNormal);
                    return;
                }
                EndWallMoves(S.GateAfterFail, true);
                fallingJumpAvailable = true;
                fallingTime = 0f;
                return;
            }

            wallTau += dt / S.WallRunTime;
            float force = S.WallRunUpSpeed + (S.WallRunDownSpeed - S.WallRunUpSpeed) * Curve(wallTau);
            if (climbRamp)
            {
                climbMult = Math.Min(1f, climbMult + dt / S.ClimbToRunTime);
                if (climbMult >= 1f) climbRamp = false;
            }
            float speed = (S.WallRunSpeed + Boost) * climbMult;
            Vec3 along = AlongWall(WallNormal, Velocity, input.Forward);
            Vec3 nFlat = WallNormal.Flat.Normalized;
            Velocity = along * speed - nFlat * S.WallRunPush;
            Velocity.Y = Math.Max(-S.WallRunZMax, Math.Min(S.WallRunZMax, force));
        }

        private void UpdateClimb(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (input.JumpPressed)
            {
                WallJump(in input, WallNormal, 0, Math.Max(Velocity.Y, 0f), fromClimb: true);
                return;
            }
            if (TryVault(in input, in sensors))
                return;
            if (!CanWallRun(in input, in sensors))
            {
                EndWallMoves(S.GateAfterFail, false);
                return;
            }
            // §9 a side wall while climbing turns the climb into a wall run (along speed ramps from 10 %)
            if (TryWallRunOrClimb(in input, in sensors, fromClimb: true))
                return;
            if (!sensors.WallFront || !ValidWall(sensors.WallFrontNormal))
            {
                EndWallMoves(S.GateAfterClimbTop, false);  // §9 top of the wall
                return;
            }
            WallNormal = sensors.WallFrontNormal;
            wallTau += dt / S.WallRunTime;
            float force = S.ClimbUpSpeed + (S.ClimbDownSpeed - S.ClimbUpSpeed) * Curve(wallTau);
            Velocity = -WallNormal.Flat.Normalized * S.WallRunPush;
            Velocity.Y = force;
        }

        /// <summary>§11 wall jump: 3D look direction (or 30 degrees off the wall), speed max(14, wall-run speed), 7.5 up.</summary>
        private void WallJump(in MovementInput input, Vec3 normal, int side, float wallSpeed, bool fromClimb)
        {
            Vec3 n = normal.Flat.Normalized;
            Vec3 dir;
            if (fromClimb)
                dir = n;
            else
            {
                Vec3 look = input.Look.SqrMagnitude > 0.01f ? input.Look.Normalized : input.Forward;
                float q = Math.Max(0f, Math.Min(1f, Vec3.Dot(look, n)));
                if (q >= S.WallJumpMinAngle / 90f)
                    dir = look;
                else
                {
                    Vec3 along = AlongWall(normal, Velocity, input.Forward);
                    float a = S.WallJumpMinAngle * DegToRad;
                    dir = (along * (float)Math.Cos(a) + n * (float)Math.Sin(a)).Normalized;
                }
            }
            float speed = Math.Max(S.WallJumpMinSpeed, wallSpeed);
            if (State == MoveState.WallRunLeft || State == MoveState.WallRunRight || side != 0)
                blockedSide = side != 0 ? side : wallSide;
            lastWallNormal = n;
            hasLastWall = true;
            gateClosedTime = Math.Max(gateClosedTime, S.GateAfterWallJump);
            if (State == MoveState.WallRunLeft || State == MoveState.WallRunRight)
                Events |= MoveEvents.WallRunEnded;
            climbRamp = false;
            climbMult = 1f;
            storedWallJump = false;

            Velocity = new Vec3(dir.X * speed, S.WallJumpUpSpeed, dir.Z * speed);
            lastJumpTime = time;
            fallingJumpAvailable = false;
            jumpPressTime = -999f;
            fallingTime = 0f;
            SetState(MoveState.Air);
            Events |= MoveEvents.WallJumped | MoveEvents.Jumped;
        }

        // ------------------------------------------------------------------ §10 vault

        private void RecordVelocity(float dt)
        {
            historySlotTime += dt;
            if (historySlotTime >= 0.1f)
            {
                historySlotTime = 0f;
                historySlot = (historySlot + 1) % historySum.Length;
                historySum[historySlot] = Vec3.Zero;
                historyTime[historySlot] = 0f;
            }
            historySum[historySlot] += Velocity * dt;
            historyTime[historySlot] += dt;
        }

        private Vec3 AverageVelocity()
        {
            Vec3 sum = Vec3.Zero;
            float t = 0f;
            for (int i = 0; i < historySum.Length; i++) { sum += historySum[i]; t += historyTime[i]; }
            return t > 0f ? sum / t : Vec3.Zero;
        }

        private bool TryVault(in MovementInput input, in MovementSensors sensors)
        {
            if (!sensors.LedgeFound || input.MoveY < 0f || input.CrouchHeld || gateNeedsTakeoff)
                return false;
            if (State != MoveState.Air && State != MoveState.WallRunLeft && State != MoveState.WallRunRight && State != MoveState.WallClimb)
                return false;

            vaultStart = sensors.Position;
            vaultDest = sensors.LedgeTarget;
            float h = vaultDest.Y - vaultStart.Y;
            float baseTime, thrMin, thrMax, scaleMin;
            if (h < S.VaultSmallHeight) { baseTime = S.VaultSmallTime; vaultKeep = S.VaultSmallKeep; thrMin = S.VaultSmallSpeedMin; thrMax = S.VaultSmallSpeedMax; scaleMin = S.VaultSmallScaleMin; }
            else if (h < S.VaultLargeHeight) { baseTime = S.VaultMediumTime; vaultKeep = S.VaultMediumKeep; thrMin = S.VaultMediumSpeedMin; thrMax = S.VaultMediumSpeedMax; scaleMin = S.VaultMediumScaleMin; }
            else { baseTime = S.VaultLargeTime; vaultKeep = S.VaultLargeKeep; thrMin = S.VaultLargeSpeedMin; thrMax = S.VaultLargeSpeedMax; scaleMin = S.VaultLargeScaleMin; }

            vaultAvgSpeed = AverageVelocity().Magnitude;
            float scale = 1f;
            if (vaultAvgSpeed > thrMin)
            {
                float k = Math.Min(1f, (vaultAvgSpeed - thrMin) / Math.Max(0.01f, thrMax - thrMin));
                scale = 1f + (scaleMin - 1f) * k;
            }
            vaultDuration = Math.Max(0.02f, baseTime * scale);
            vaultExitDir = (vaultDest - vaultStart).Normalized;

            if (State == MoveState.WallRunLeft || State == MoveState.WallRunRight)
                Events |= MoveEvents.WallRunEnded;
            gateNeedsTakeoff = true;  // §10 wall-run and climb gates close
            climbRamp = false;
            climbMult = 1f;
            SetState(MoveState.Mantle);
            Events |= MoveEvents.VaultStarted | MoveEvents.MantleStarted;
            return true;
        }

        private void UpdateVault(in MovementSensors sensors, float dt)
        {
            Vec3 to = vaultDest - sensors.Position;
            bool arrived = to.Y <= 0.05f && to.Flat.SqrMagnitude < 0.0025f;
            if (arrived || StateTime >= vaultDuration + 0.15f)
            {
                // §10 exit along the vault path with the kept share of the average speed (none below 3 m/s)
                Velocity = vaultAvgSpeed >= S.VaultMinMomentum ? vaultExitDir * (vaultAvgSpeed * vaultKeep) : Vec3.Zero;
                SetState(MoveState.Air);
                return;
            }
            // Position follows the path over the vault duration: rise first, then over the edge, so the capsule
            // doesn't catch the lip.
            float rise = Math.Max(0f, vaultDest.Y - vaultStart.Y);
            float across = (vaultDest - vaultStart).Flat.Magnitude;
            float pathSpeed = (rise + across) / vaultDuration;
            if (to.Y > 0.02f)
                Velocity = new Vec3(0f, Math.Min(pathSpeed, to.Y / dt), 0f);
            else
            {
                Vec3 flat = to.Flat;
                float d = flat.Magnitude;
                Velocity = d > 0.0001f ? flat / d * Math.Min(pathSpeed, d / dt) : Vec3.Zero;
            }
        }

        // ------------------------------------------------------------------ §1 Unreal CharacterMovement physics

        private Vec3 Wish(in MovementInput input, float strafeScale)
        {
            Vec3 w = input.Forward * input.MoveY + input.Right * (input.MoveX * strafeScale);
            float sq = w.SqrMagnitude;
            return sq > 1f ? w / (float)Math.Sqrt(sq) : w;
        }

        /// <summary>Walking: braking with no input or above the cap, friction turning toward the input, then acceleration.</summary>
        private void Walk(Vec3 wish, float max, float dt)
        {
            Vec3 v = Velocity.Flat;
            float speed = v.Magnitude;
            bool zero = wish.SqrMagnitude < 1e-6f;
            bool over = speed > max + 0.001f;
            if (zero || over)
            {
                Vec3 old = v;
                v = Brake(v, dt);
                if (over && !zero && v.Magnitude < max && Vec3.Dot(wish, old) > 0f)
                    v = old.Normalized * max;
            }
            else
            {
                Vec3 dir = wish.Normalized;
                v -= (v - dir * speed) * Math.Min(dt * S.GroundFriction, 1f);
            }
            if (!zero)
            {
                float newMax = v.Magnitude > max ? v.Magnitude : max;
                v += wish * (S.MaxAcceleration * dt);
                v = Vec3.ClampMagnitude(v, newMax);
            }
            Velocity.X = v.X;
            Velocity.Z = v.Z;
        }

        private Vec3 Brake(Vec3 v, float dt)
        {
            float remaining = dt;
            while (remaining > 1e-6f && v.SqrMagnitude > 0f)
            {
                float step = Math.Min(remaining, 1f / 33f);
                remaining -= step;
                Vec3 prev = v;
                Vec3 rev = v.Normalized * -S.BrakingDeceleration;
                v += (v * -S.BrakingFriction + rev) * step;
                if (Vec3.Dot(v, prev) <= 0f)
                    return Vec3.Zero;
            }
            return v.SqrMagnitude < 0.01f ? Vec3.Zero : v;
        }

        /// <summary>Falling: no friction or braking; input accelerates at AirControl x MaxAcceleration, never past the cap
        /// (speed already above it is kept and only turned).</summary>
        private void AirMove(Vec3 wish, float max, float dt)
        {
            if (wish.SqrMagnitude < 1e-6f)
                return;
            Vec3 v = Velocity.Flat;
            float newMax = v.Magnitude > max ? v.Magnitude : max;
            v += wish * (S.MaxAcceleration * S.AirControl * dt);
            v = Vec3.ClampMagnitude(v, newMax);
            Velocity.X = v.X;
            Velocity.Z = v.Z;
        }

        private void FollowGround(in MovementSensors sensors)
        {
            Vec3 n = sensors.GroundNormal;
            Velocity.Y = n.Y > 0.1f ? -(n.X * Velocity.X + n.Z * Velocity.Z) / n.Y : 0f;
        }

        private static float MoveTowards(float a, float b, float step) =>
            a < b ? Math.Min(b, a + step) : Math.Max(b, a - step);
    }
}
