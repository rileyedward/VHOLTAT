using System;

namespace VHOLTAT.Movement
{
    /// <summary>
    /// The parkour movement state machine. Engine-agnostic: the adapter feeds input + sensors each frame,
    /// calls <see cref="Step"/>, then moves the character by <see cref="Velocity"/> * dt.
    /// No allocations after construction.
    /// </summary>
    public sealed class MovementMotor : MotorBase
    {
        public readonly MovementSettings S;

        public override float RunSpeed => S.RunSpeed;
        public override float SlideStartSpeed => S.SlideMinStartSpeed;
        public override string ModelName => S.Playtest2Model ? "0.3 locked (Playtest2)" : "0.3 locked";

        private float coyoteTimer;
        private float jumpBufferTimer;
        private float sinceLanding = 999f;
        private float landingSpeed;
        private float wallJumpGraceTimer;
        private Vec3 graceWallNormal;
        private bool jumpedThisAir;
        /// <summary>Rising from a ground jump: JumpRiseGravity applies until the apex.</summary>
        private bool groundJumpRise;
        /// <summary>The wall just left can't be run again until landing or touching a different wall.</summary>
        private bool hasBlockedWall;
        private Vec3 blockedWallNormal;
        private bool climbUsed;
        private Vec3 mantleTarget;
        private float mantleRiseSpeed;
        /// <summary>Best horizontal speed over roughly the last second (10 slots of 0.1 s).</summary>
        private readonly float[] speedHistory = new float[10];
        private int speedSlot;
        private float speedSlotTime;
        private Vec3 mantleExitDir;
        private float mantleExitSpeed;

        public MovementMotor(MovementSettings settings)
        {
            S = settings;
        }

        /// <summary>Clears all state (respawn, teleport, toggling the mod on).</summary>
        public override void Reset(Vec3 velocity, bool grounded)
        {
            Velocity = velocity;
            Boost = 0f;
            SetState(grounded ? MoveState.Ground : MoveState.Air);
            coyoteTimer = jumpBufferTimer = wallJumpGraceTimer = 0f;
            sinceLanding = 999f;
            jumpedThisAir = false;
            hasBlockedWall = climbUsed = false;
        }

        /// <summary>External velocity change (explosions, knockback, jump pads).</summary>
        public override void AddImpulse(Vec3 deltaV)
        {
            Velocity += deltaV;
            if (deltaV.Y > 0.5f)
                groundJumpRise = false;
            if (deltaV.Y > 0.5f && State != MoveState.Air)
            {
                SetState(MoveState.Air);
                jumpedThisAir = true;
                coyoteTimer = 0f;
            }
        }

        /// <summary>
        /// Knockback that replaces movement (STRAFTAT's AddForce zeroes the player's own velocity before pushing, like
        /// VHOLUME's launchers): drops horizontal speed and carried boost; the push itself arrives separately.
        /// </summary>
        public override void CancelHorizontal()
        {
            Velocity.X = 0f;
            Velocity.Z = 0f;
            Boost = 0f;
        }

        public override void Step(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (dt <= 0f)
                return;
            Events = MoveEvents.None;
            StateTime += dt;
            sinceLanding += dt;
            coyoteTimer -= dt;
            wallJumpGraceTimer -= dt;
            jumpBufferTimer -= dt;
            if (input.JumpPressed)
                jumpBufferTimer = S.JumpBufferTime;

            DecayBoost(dt);
            RecordSpeed(dt);
            HandleGroundTransitions(in input, in sensors);

            switch (State)
            {
                case MoveState.Ground: UpdateGround(in input, in sensors, dt); break;
                case MoveState.Crouch: UpdateCrouch(in input, in sensors, dt); break;
                case MoveState.Slide: UpdateSlide(in input, in sensors, dt); break;
                case MoveState.Air: UpdateAir(in input, in sensors, dt); break;
                case MoveState.WallRunLeft:
                case MoveState.WallRunRight: UpdateWallRun(in input, in sensors, dt); break;
                case MoveState.WallClimb: UpdateWallClimb(in input, in sensors, dt); break;
                case MoveState.Mantle: UpdateMantle(in sensors, dt); break;
            }
        }

        // ---------------------------------------------------------------- momentum

        private void DecayBoost(float dt)
        {
            float rate;
            switch (State)
            {
                case MoveState.Ground: rate = S.BoostDecayGround; break;
                case MoveState.Slide: rate = S.BoostDecaySlide; break;
                case MoveState.WallRunLeft:
                case MoveState.WallRunRight: rate = S.BoostDecayWallRun; break;
                case MoveState.Crouch: Boost = 0f; return; // crouch-walking clears momentum
                default: return; // air, climb, mantle: kept
            }
            Boost *= (float)Math.Exp(-rate * dt);
        }

        private void AddBoost(float amount)
        {
            Boost = Math.Max(0f, Math.Min(S.Playtest2Model ? S.P2BoostMax : S.BoostMax, Boost + amount));
        }

        /// <summary>Playtest 2 model: adds speed along the current horizontal direction and the same amount of boost.</summary>
        private void Gain(float amount)
        {
            if (amount <= 0f)
                return;
            float before = Boost;
            AddBoost(amount);
            float added = Boost - before;
            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            if (speed > 0.1f && added > 0f)
            {
                float y = Velocity.Y;
                Velocity = flat / speed * (speed + added);
                Velocity.Y = y;
            }
        }

        // ---------------------------------------------------------------- transitions

        private void HandleGroundTransitions(in MovementInput input, in MovementSensors sensors)
        {
            if (State == MoveState.Mantle)
                return;

            if (sensors.Grounded && !IsGroundState && Velocity.Y <= 0.01f)
            {
                Land(in input);
                return;
            }

            if (!sensors.Grounded && IsGroundState)
            {
                if (State == MoveState.Slide)
                    Events |= MoveEvents.SlideEnded;
                SetState(MoveState.Air);
                if (!jumpedThisAir)
                    coyoteTimer = S.CoyoteTime;
            }
        }

        private void Land(in MovementInput input)
        {
            Events |= MoveEvents.Landed;
            if (State == MoveState.WallRunLeft || State == MoveState.WallRunRight)
                Events |= MoveEvents.WallRunEnded;
            sinceLanding = 0f;
            jumpedThisAir = false;
            hasBlockedWall = climbUsed = false;
            wallJumpGraceTimer = 0f;
            Velocity.Y = 0f;

            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            if (speed > S.RunSpeed)
            {
                speed = Math.Max(S.RunSpeed, speed - S.HopLandingLoss);
                Velocity = flat.Normalized * speed;
            }
            // A jump inside the hop window gets this speed back (the ground state bleeds anything above base + boost).
            landingSpeed = speed;

            if (S.Playtest2Model)
            {
                // Part of the speed carried in above the new state's base + boost becomes boost, so chains build.
                bool slideNext = input.CrouchHeld && speed >= S.SlideMinStartSpeed;
                float baseSpeed = slideNext ? S.SlideSpeed : input.CrouchHeld ? S.CrouchSpeed : S.RunSpeed;
                float excess = speed - (baseSpeed + Boost);
                if (excess > 0f && (slideNext || !input.CrouchHeld))
                    AddBoost(excess * S.P2LandingCarry);
            }

            if (input.CrouchHeld && speed >= S.SlideMinStartSpeed)
                StartSlide(in input);
            else
                SetState(input.CrouchHeld ? MoveState.Crouch : MoveState.Ground);
        }

        private void SetState(MoveState next)
        {
            if (next != MoveState.Air)
                groundJumpRise = false;
            State = next;
            StateTime = 0f;
        }

        // ---------------------------------------------------------------- ground states

        private void UpdateGround(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (TryGroundJump())
                return;

            if (sensors.VaultFound && input.MoveY > 0.5f && HorizontalSpeed >= S.VaultMinSpeed)
            {
                StartVault(in input, in sensors);
                return;
            }

            if (input.CrouchPressed && HorizontalSpeed >= S.SlideMinStartSpeed)
            {
                StartSlide(in input);
                UpdateSlide(in input, in sensors, dt);
                return;
            }
            if (input.CrouchHeld)
            {
                SetState(MoveState.Crouch);
                UpdateCrouch(in input, in sensors, dt);
                return;
            }

            GroundMove(WishDir(in input), S.RunSpeed * Scale(in input) + Boost, dt);
            FollowGround(in sensors);
        }

        private void UpdateCrouch(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (!sensors.CeilingBlocked)
            {
                if (TryGroundJump())
                    return;
                if (!input.CrouchHeld)
                {
                    SetState(MoveState.Ground);
                    UpdateGround(in input, in sensors, dt);
                    return;
                }
            }

            GroundMove(WishDir(in input), S.CrouchSpeed * Scale(in input), dt);
            FollowGround(in sensors);
        }

        private void StartSlide(in MovementInput input)
        {
            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            Vec3 dir = speed > 0.1f ? flat / speed : input.Forward;
            Velocity = dir * Math.Max(speed, S.SlideSpeed + Boost);
            SetState(MoveState.Slide);
            Events |= MoveEvents.SlideStarted;
        }

        private void UpdateSlide(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (!sensors.CeilingBlocked)
            {
                if (TryGroundJump())
                {
                    Events |= MoveEvents.SlideEnded;
                    return;
                }
                if (!input.CrouchHeld)
                {
                    Events |= MoveEvents.SlideEnded;
                    SetState(MoveState.Ground);
                    UpdateGround(in input, in sensors, dt);
                    return;
                }
            }

            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            Vec3 dir = speed > 0.01f ? flat / speed : input.Forward;

            // Positive = moving downhill. The ground normal's horizontal part points downhill and its length is sin(slope).
            // Slopes don't change slide speed in VHOLUME except uphill classes, which brake.
            float slope = Vec3.Dot(sensors.GroundNormal.Flat, dir);
            if (slope > 0f && S.Playtest2Model)
            {
                float gain = S.Gravity * slope * dt * S.P2SlideSlopeGain;
                AddBoost(gain);
                speed += gain;
            }
            else if (slope <= 0f)
            {
                float decel = slope <= S.SlopeSteep ? S.SlideDecelSteepUpslope
                    : slope <= S.SlopeMedium ? S.SlideDecelMediumUpslope
                    : S.SlideDecelFlat;
                if (StateTime > S.SlideHoldTime)
                    decel += S.SlideFriction;
                speed -= decel * dt;
            }

            // Speed above base + boost (e.g. landing fast) bleeds off once the hop window has passed.
            float target = S.SlideSpeed + Boost;
            if (speed > target && sinceLanding >= S.HopWindow)
                speed = Math.Max(target, speed - S.OverspeedDecay * dt);

            Vec3 wish = WishDir(in input);
            if (wish.SqrMagnitude > 0.01f)
                dir = RotateTowardsFlat(dir, wish, S.SlideSteerRate * DegToRad * dt);

            if (speed < S.SlideEndSpeed)
            {
                Events |= MoveEvents.SlideEnded;
                Velocity = dir * Math.Max(speed, 0f);
                SetState(MoveState.Crouch);
                FollowGround(in sensors);
                return;
            }

            Velocity = dir * speed;
            FollowGround(in sensors);
        }

        private bool TryGroundJump()
        {
            if (jumpBufferTimer <= 0f)
                return false;
            if (sinceLanding < dtEpsilon)
                Events |= MoveEvents.BufferedJump;
            Jump();
            return true;
        }

        private const float dtEpsilon = 1e-4f;

        private void Jump()
        {
            // Bunny hop: jumping inside the hop window restores the landing speed. Fast hops are graded; a hop that
            // isn't perfectly timed loses a little.
            if (S.Playtest2Model)
            {
                if (sinceLanding <= S.HopWindow)
                {
                    float gain = 0f;
                    if (State == MoveState.Slide)
                        gain += S.P2SlideHopGain;
                    if (sinceLanding <= S.PerfectHopWindow)
                        gain += S.P2PerfectHopGain;
                    Gain(gain);
                }
            }
            else if (sinceLanding <= S.HopWindow && landingSpeed > 0f)
            {
                float keep = landingSpeed;
                if (keep >= S.PerfectHopSpeed && sinceLanding > S.PerfectHopWindow)
                    keep -= keep >= S.PerfectHopHighSpeed ? S.MissedHopLossHigh : S.MissedHopLoss;
                var flat = Velocity.Flat;
                float speed = flat.Magnitude;
                if (keep > speed && speed > 0.1f)
                {
                    var v = flat / speed * keep;
                    Velocity.X = v.X;
                    Velocity.Z = v.Z;
                }
            }
            landingSpeed = 0f;
            Velocity.Y = S.JumpSpeed;
            jumpBufferTimer = 0f;
            coyoteTimer = 0f;
            jumpedThisAir = true;
            groundJumpRise = true;
            SetState(MoveState.Air);
            Events |= MoveEvents.Jumped;
        }

        /// <summary>
        /// Accelerates toward wish * maxSpeed. Speed above the cap (from slides, wall runs, hops) is kept
        /// untouched inside the hop window after landing, then bleeds off at OverspeedDecay.
        /// </summary>
        private void GroundMove(Vec3 wish, float maxSpeed, float dt)
        {
            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            bool hasWish = wish.SqrMagnitude > 0.0001f;

            if (speed > maxSpeed + 0.01f)
            {
                if (sinceLanding < S.HopWindow)
                    return;
                float newSpeed = Math.Max(maxSpeed, speed - S.OverspeedDecay * dt);
                Vec3 dir = hasWish ? RotateTowardsFlat(flat / speed, wish, S.GroundAcceleration / speed * dt) : flat / speed;
                Velocity = dir * newSpeed;
                return;
            }

            Vec3 target = hasWish ? wish * maxSpeed : Vec3.Zero;
            float rate = hasWish ? S.GroundAcceleration : S.GroundBraking;
            Velocity = MoveTowards(flat, target, rate * dt);
        }

        /// <summary>Gives grounded velocity the vertical part that keeps it on the ground plane.</summary>
        private void FollowGround(in MovementSensors sensors)
        {
            Vec3 n = sensors.GroundNormal;
            Velocity.Y = n.Y > 0.1f ? -(n.X * Velocity.X + n.Z * Velocity.Z) / n.Y : 0f;
        }

        // ---------------------------------------------------------------- air

        private void UpdateAir(in MovementInput input, in MovementSensors sensors, float dt)
        {
            if (jumpBufferTimer > 0f)
            {
                if (coyoteTimer > 0f && !jumpedThisAir)
                {
                    Events |= MoveEvents.CoyoteJump;
                    Jump();
                    return;
                }
                if (wallJumpGraceTimer > 0f)
                {
                    WallJump(in input, graceWallNormal);
                    return;
                }
            }

            if (sensors.LedgeFound && input.MoveY > 0.1f && Velocity.Y < S.JumpSpeed * 0.75f)
            {
                StartMantle(in input, in sensors);
                return;
            }

            if (TryStartWallRun(in input, in sensors) || TryStartWallClimb(in input, in sensors))
                return;

            AirSteer(WishDir(in input), dt);
            if (Velocity.Y <= 0f)
                groundJumpRise = false;
            ApplyGravity(groundJumpRise && !S.ExactVholumeJump ? S.JumpRiseGravity : 1f, dt);
        }

        /// <summary>
        /// Air control: below run speed (+boost) the player can accelerate toward the input; at or above it the
        /// input only turns the velocity (lateral acceleration AirAcceleration), never adding or removing speed.
        /// </summary>
        private void AirSteer(Vec3 wish, float dt)
        {
            if (wish.SqrMagnitude < 0.0001f)
                return;
            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            float cap = S.RunSpeed + Boost;
            Vec3 steered;
            if (S.Playtest2Model)
                steered = Vec3.ClampMagnitude(MoveTowards(flat, wish * Math.Max(speed, cap), S.AirAcceleration * dt), Math.Max(speed, cap));
            else if (speed >= cap - 0.05f)
                steered = RotateTowardsFlat(flat / speed, wish, S.AirAcceleration / speed * dt) * speed;
            else
                steered = Vec3.ClampMagnitude(MoveTowards(flat, wish * cap, S.AirAcceleration * dt), Math.Max(speed, cap));
            Velocity.X = steered.X;
            Velocity.Z = steered.Z;
        }

        private void ApplyGravity(float scale, float dt)
        {
            Velocity.Y = Math.Max(Velocity.Y - S.Gravity * scale * dt, -S.TerminalFallSpeed);
        }

        // ---------------------------------------------------------------- wall run

        private bool TryStartWallRun(in MovementInput input, in MovementSensors sensors)
        {
            if (input.MoveY <= 0.1f || sensors.HeightAboveGround < S.WallRunMinHeight)
                return false;
            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            if (speed < S.WallRunMinStartSpeed)
                return false;
            Vec3 dir = flat / speed;

            bool right = sensors.WallRight && !IsBlocked(sensors.WallRightNormal) && IsRunnableWall(sensors.WallRightNormal, dir);
            bool left = !right && sensors.WallLeft && !IsBlocked(sensors.WallLeftNormal) && IsRunnableWall(sensors.WallLeftNormal, dir);
            if (!right && !left)
                return false;

            WallNormal = right ? sensors.WallRightNormal : sensors.WallLeftNormal;
            Vec3 along = Vec3.ProjectOnPlane(dir, WallNormal.Flat.Normalized).Normalized;
            // Air speed above run speed becomes boost, so the wall run is WallRunSpeed + that (chains build here).
            if (S.Playtest2Model)
                Velocity = along * Math.Max(speed, S.WallRunSpeed + Boost);
            else
            {
                AddBoost(Math.Max(0f, speed - S.RunSpeed - Boost) * S.WallRunAirCarry);
                Velocity = along * (S.WallRunSpeed + Boost);
            }
            Velocity.Y = S.WallRunStartUpSpeed;
            SetState(right ? MoveState.WallRunRight : MoveState.WallRunLeft);
            Events |= MoveEvents.WallRunStarted;
            jumpedThisAir = false;
            return true;
        }

        private bool IsRunnableWall(Vec3 normal, Vec3 moveDir)
        {
            if (Math.Abs(normal.Y) > S.WallMaxNormalY)
                return false;
            Vec3 n = normal.Flat.Normalized;
            float into = Vec3.Dot(moveDir, n);
            if (into > 0.1f)
                return false; // moving away from it
            float alongness = (moveDir - n * into).Magnitude;
            return alongness >= S.WallRunMinAlignment;
        }

        private void UpdateWallRun(in MovementInput input, in MovementSensors sensors, float dt)
        {
            bool right = State == MoveState.WallRunRight;
            if (jumpBufferTimer > 0f)
            {
                BlockWall();
                Events |= MoveEvents.WallRunEnded;
                WallJump(in input, WallNormal);
                return;
            }

            bool hasWall = right ? sensors.WallRight : sensors.WallLeft;
            bool frontSameWall = !hasWall && sensors.WallFront
                && Vec3.Dot(sensors.WallFrontNormal.Flat.Normalized, WallNormal.Flat.Normalized) > 0.9f;
            if (hasWall || frontSameWall)
            {
                // Running along a wall we're facing (e.g. after turning a climb into a run) counts too.
                Vec3 n = hasWall ? (right ? sensors.WallRightNormal : sensors.WallLeftNormal) : sensors.WallFrontNormal;
                hasWall = true;
                if (Math.Abs(n.Y) <= S.WallMaxNormalY)
                    WallNormal = n;
                else
                    hasWall = false;
            }

            Vec3 nFlat = WallNormal.Flat.Normalized;
            float awayInput = Vec3.Dot(WishDir(in input), nFlat);
            // Input must keep pushing along the run (forward on a side wall, sideways on a wall we face).
            Vec3 runDir = Velocity.Flat.Normalized;
            bool driving = Vec3.Dot(WishDir(in input), runDir) > 0.1f;
            if (!hasWall || StateTime >= S.WallRunDuration || awayInput > S.WallRunPushOff || !driving)
            {
                EndWallRun(right);
                AirSteer(WishDir(in input), dt);
                ApplyGravity(1f, dt);
                return;
            }

            var flat = Velocity.Flat;
            float speed = S.Playtest2Model ? Math.Max(flat.Magnitude, S.WallRunSpeed + Boost) : S.WallRunSpeed + Boost;
            Vec3 along = Vec3.ProjectOnPlane(flat, nFlat).Normalized;
            if (along.SqrMagnitude < 0.01f)
            {
                EndWallRun(right);
                return;
            }

            float vy = Velocity.Y;
            if (StateTime < S.WallRunHoldTime)
                vy = Math.Max(vy, S.WallRunStartUpSpeed);
            else
            {
                float ramp = (StateTime - S.WallRunHoldTime) / Math.Max(0.01f, S.WallRunDuration - S.WallRunHoldTime);
                vy -= S.Gravity * S.WallRunGravityEnd * Math.Min(1f, ramp) * dt;
            }

            // A small push into the wall keeps the side probe in contact on curved walls.
            Velocity = along * speed - nFlat * 0.5f;
            Velocity.Y = Math.Max(vy, -S.TerminalFallSpeed);
        }

        private void EndWallRun(bool right)
        {
            BlockWall();
            graceWallNormal = WallNormal;
            wallJumpGraceTimer = S.WallJumpGrace;
            Events |= MoveEvents.WallRunEnded;
            SetState(MoveState.Air);
        }

        private void BlockWall()
        {
            hasBlockedWall = true;
            blockedWallNormal = WallNormal.Flat.Normalized;
        }

        /// <summary>Same wall plane as the one just left (normals within ~18 degrees).</summary>
        private bool IsBlocked(Vec3 normal) =>
            hasBlockedWall && Vec3.Dot(normal.Flat.Normalized, blockedWallNormal) > 0.95f;

        /// <summary>
        /// Jump off a wall: fixed upward speed, horizontal speed kept, heading toward the look direction but at
        /// least WallJumpMinAngle away from the wall plane.
        /// </summary>
        private void WallJump(in MovementInput input, Vec3 wallNormal)
        {
            Vec3 n = wallNormal.Flat.Normalized;
            var flat = Velocity.Flat;
            float speed = Math.Max(flat.Magnitude, S.RunSpeed);

            Vec3 look = input.Look.Flat.Normalized;
            if (look.SqrMagnitude < 0.01f)
                look = input.Forward;
            float minSin = (float)Math.Sin(S.WallJumpMinAngle * DegToRad);
            float away = Vec3.Dot(look, n);
            Vec3 dir;
            if (away >= minSin)
                dir = look;
            else
            {
                Vec3 tangent = (look - n * away).Normalized;
                if (tangent.SqrMagnitude < 0.01f)
                    tangent = (flat - n * Vec3.Dot(flat, n)).Normalized;
                dir = (tangent * (float)Math.Sqrt(1f - minSin * minSin) + n * minSin).Normalized;
            }

            if (S.Playtest2Model)
            {
                AddBoost(S.P2WallJumpGain);
                speed += S.P2WallJumpGain;
            }
            Velocity = dir * speed;
            Velocity.Y = S.WallJumpUpSpeed;
            wallJumpGraceTimer = 0f;
            jumpBufferTimer = 0f;
            jumpedThisAir = true;
            SetState(MoveState.Air);
            groundJumpRise = false;
            Events |= MoveEvents.WallJumped | MoveEvents.Jumped;
        }

        // ---------------------------------------------------------------- wall climb

        private bool TryStartWallClimb(in MovementInput input, in MovementSensors sensors)
        {
            if (climbUsed || !sensors.WallFront || input.MoveY <= 0.5f || Velocity.Y < -2f)
                return false;
            Vec3 n = sensors.WallFrontNormal;
            if (Math.Abs(n.Y) > S.WallMaxNormalY)
                return false;
            if (Vec3.Dot(input.Forward, -n.Flat.Normalized) < S.WallClimbMinFacing)
                return false;

            climbUsed = true;
            WallNormal = n;
            Velocity = Vec3.Zero;
            Velocity.Y = S.WallClimbSpeed;
            SetState(MoveState.WallClimb);
            Events |= MoveEvents.WallClimbStarted;
            return true;
        }

        private void UpdateWallClimb(in MovementInput input, in MovementSensors sensors, float dt)
        {
            Vec3 nFlat = WallNormal.Flat.Normalized;
            if (jumpBufferTimer > 0f)
            {
                Velocity = nFlat * S.WallClimbPushOff;
                Velocity.Y = S.WallJumpUpSpeed;
                jumpBufferTimer = 0f;
                jumpedThisAir = true;
                SetState(MoveState.Air);
                groundJumpRise = false;
            Events |= MoveEvents.WallJumped | MoveEvents.Jumped;
                return;
            }

            if (sensors.LedgeFound)
            {
                StartMantle(in input, in sensors);
                return;
            }

            if (Math.Abs(input.MoveX) >= S.WallClimbToRunInput && sensors.WallFront)
            {
                ClimbToWallRun(in input);
                return;
            }

            if (!sensors.WallFront || input.MoveY <= 0.1f || StateTime >= S.WallClimbDuration)
            {
                SetState(MoveState.Air);
                ApplyGravity(1f, dt);
                return;
            }

            WallNormal = sensors.WallFrontNormal;
            float vy = StateTime < S.WallClimbHoldTime ? S.WallClimbSpeed : Velocity.Y - S.WallClimbDecay * dt;
            Velocity = -nFlat * 0.5f;
            Velocity.Y = vy;
        }

        /// <summary>Strafing during a climb turns it into a wall run along the same wall.</summary>
        private void ClimbToWallRun(in MovementInput input)
        {
            Vec3 nFlat = WallNormal.Flat.Normalized;
            Vec3 along = Vec3.ProjectOnPlane(input.Right * (input.MoveX > 0f ? 1f : -1f), nFlat).Normalized;
            if (along.SqrMagnitude < 0.01f)
                return;
            Vec3 rightOfTravel = Vec3.Cross(Vec3.Up, along);
            bool wallOnRight = Vec3.Dot(-nFlat, rightOfTravel) > 0f;
            float vy = Velocity.Y * S.WallClimbToRunVertical;
            Velocity = along * (S.WallRunSpeed + Boost);
            Velocity.Y = vy;
            SetState(wallOnRight ? MoveState.WallRunRight : MoveState.WallRunLeft);
            Events |= MoveEvents.WallRunStarted;
        }

        // ---------------------------------------------------------------- mantle

        private void RecordSpeed(float dt)
        {
            float h = HorizontalSpeed;
            speedSlotTime += dt;
            if (speedSlotTime >= 0.1f)
            {
                speedSlotTime = 0f;
                speedSlot = (speedSlot + 1) % speedHistory.Length;
                speedHistory[speedSlot] = 0f;
            }
            if (h > speedHistory[speedSlot])
                speedHistory[speedSlot] = h;
        }

        private float RecentTopSpeed()
        {
            float best = 0f;
            for (int i = 0; i < speedHistory.Length; i++)
                if (speedHistory[i] > best) best = speedHistory[i];
            return best;
        }

        /// <summary>Vault: like a mantle, but from a run, quicker the faster you go, keeping a share of recent speed.</summary>
        private void StartVault(in MovementInput input, in MovementSensors sensors)
        {
            float height = sensors.VaultHeight;
            float keep, scaleMin, vMin, vMax;
            if (height <= S.VaultSmallHeight) { keep = S.VaultSmallKeep; scaleMin = S.VaultSmallScaleMin; vMin = S.VaultSmallSpeedMin; vMax = S.VaultSmallSpeedMax; }
            else if (height <= S.VaultMediumHeight) { keep = S.VaultMediumKeep; scaleMin = S.VaultMediumScaleMin; vMin = S.VaultMediumSpeedMin; vMax = S.VaultMediumSpeedMax; }
            else { keep = S.VaultLargeKeep; scaleMin = S.VaultLargeScaleMin; vMin = S.VaultLargeSpeedMin; vMax = S.VaultLargeSpeedMax; }

            float pre = Math.Max(RecentTopSpeed(), HorizontalSpeed);
            float k = vMax > vMin ? Math.Max(0f, Math.Min(1f, (pre - vMin) / (vMax - vMin))) : 1f;
            float scale = 1f + (scaleMin - 1f) * k;

            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            mantleExitDir = speed > 0.5f ? flat / speed : input.Forward;
            mantleExitSpeed = Math.Max(S.MantleMinExitSpeed, pre * keep);
            mantleRiseSpeed = S.MantleSpeed / Math.Max(0.1f, scale);
            mantleTarget = sensors.VaultTarget;
            SetState(MoveState.Mantle);
            Events |= MoveEvents.VaultStarted | MoveEvents.MantleStarted;
        }

        private void StartMantle(in MovementInput input, in MovementSensors sensors)
        {
            var flat = Velocity.Flat;
            float speed = flat.Magnitude;
            mantleExitDir = speed > 0.5f ? flat / speed : input.Forward;
            mantleExitSpeed = Math.Max(S.MantleMinExitSpeed, speed * S.MantleMomentumKeep);
            mantleTarget = sensors.LedgeTarget;
            mantleRiseSpeed = S.MantleSpeed;
            SetState(MoveState.Mantle);
            Events |= MoveEvents.MantleStarted;
        }

        private void UpdateMantle(in MovementSensors sensors, float dt)
        {
            Vec3 to = mantleTarget - sensors.Position;
            // Done once risen to the ledge and over its edge (the capsule may settle a little above the target).
            if (StateTime >= S.MantleMaxTime || (to.Y <= 0.05f && to.Flat.SqrMagnitude < 0.0025f))
            {
                Velocity = mantleExitDir * mantleExitSpeed;
                SetState(MoveState.Air);
                return;
            }

            // Rise first, then move over the edge, so the capsule doesn't clip the lip.
            if (to.Y > 0.02f)
                Velocity = new Vec3(0f, Math.Min(mantleRiseSpeed, to.Y / dt), 0f);
            else
            {
                Vec3 flat = to.Flat;
                float d = flat.Magnitude;
                Velocity = d > 0.0001f ? flat / d * Math.Min(Math.Max(mantleRiseSpeed, mantleExitSpeed), d / dt) : Vec3.Zero;
            }
        }

        // ---------------------------------------------------------------- helpers

        private const float DegToRad = (float)(Math.PI / 180.0);

        private static float Scale(in MovementInput input) => input.SpeedScale > 0f ? input.SpeedScale : 1f;

        private static Vec3 WishDir(in MovementInput input)
        {
            Vec3 wish = input.Forward * input.MoveY + input.Right * input.MoveX;
            float sq = wish.SqrMagnitude;
            return sq > 1f ? wish / (float)Math.Sqrt(sq) : wish;
        }

        public static Vec3 MoveTowards(Vec3 current, Vec3 target, float maxDelta)
        {
            Vec3 d = target - current;
            float dist = d.Magnitude;
            return dist <= maxDelta || dist < 1e-6f ? target : current + d / dist * maxDelta;
        }

        /// <summary>Rotates flat unit vector a toward flat direction b around Y by at most maxRadians.</summary>
        public static Vec3 RotateTowardsFlat(Vec3 a, Vec3 b, float maxRadians)
        {
            Vec3 bn = b.Flat.Normalized;
            float angle = (float)Math.Atan2(a.Z * bn.X - a.X * bn.Z, a.X * bn.X + a.Z * bn.Z);
            float step = Math.Max(-maxRadians, Math.Min(maxRadians, angle));
            float c = (float)Math.Cos(step), s = (float)Math.Sin(step);
            // Rotation about Y by -step in the (x, z) plane, matching the sign of atan2 above.
            return new Vec3(a.X * c + a.Z * s, 0f, -a.X * s + a.Z * c);
        }
    }
}
