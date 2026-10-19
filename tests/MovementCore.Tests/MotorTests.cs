using System;
using VHOLTAT.Movement;
using Xunit;

public class MotorTests
{
    private const float Dt = 1f / 120f;
    private static readonly Vec3 Fwd = new Vec3(0f, 0f, 1f);
    private static readonly Vec3 Rgt = new Vec3(1f, 0f, 0f);

    private static MovementInput Input(float moveY = 0f, float moveX = 0f, bool jump = false, bool crouch = false, bool crouchPressed = false) =>
        new MovementInput
        {
            MoveX = moveX, MoveY = moveY,
            JumpPressed = jump, JumpHeld = jump,
            CrouchHeld = crouch, CrouchPressed = crouchPressed,
            Forward = Fwd, Right = Rgt, Look = Fwd,
        };

    private static MovementSensors Ground(bool grounded = true) =>
        new MovementSensors { Grounded = grounded, GroundNormal = Vec3.Up, HeightAboveGround = grounded ? 0f : 3f };

    private static MovementMotor Grounded()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(Vec3.Zero, grounded: true);
        return m;
    }

    private static void Run(MovementMotor m, MovementInput input, MovementSensors sensors, float seconds)
    {
        var first = input;
        for (float t = 0f; t < seconds; t += Dt)
        {
            m.Step(in first, in sensors, Dt);
            first.JumpPressed = false;
            first.CrouchPressed = false;
        }
    }

    [Fact]
    public void RunReachesRunSpeedQuickly()
    {
        var m = Grounded();
        Run(m, Input(moveY: 1f), Ground(), 0.2f);
        Assert.Equal(m.S.RunSpeed, m.HorizontalSpeed, 2);
        Assert.Equal(MoveState.Ground, m.State);
    }

    [Fact]
    public void CrouchWhileRunningStartsSlideAtSlideSpeed()
    {
        var m = Grounded();
        Run(m, Input(moveY: 1f), Ground(), 0.3f);
        m.Step(Input(moveY: 1f, crouch: true, crouchPressed: true), Ground(), Dt);
        Assert.Equal(MoveState.Slide, m.State);
        Assert.True((m.Events & MoveEvents.SlideStarted) != 0);
        Assert.InRange(m.HorizontalSpeed, m.S.SlideSpeed - 0.1f, m.S.SlideSpeed + 0.01f);
    }

    [Fact]
    public void FlatSlideHoldsSpeedThenEnds()
    {
        var m = Grounded();
        Run(m, Input(moveY: 1f), Ground(), 0.3f);
        m.Step(Input(moveY: 1f, crouch: true, crouchPressed: true), Ground(), Dt);
        Run(m, Input(moveY: 1f, crouch: true), Ground(), 1.0f);
        Assert.Equal(MoveState.Slide, m.State);
        Assert.True(m.HorizontalSpeed > 15.5f);
        Run(m, Input(moveY: 1f, crouch: true), Ground(), 3f);
        Assert.Equal(MoveState.Crouch, m.State);
    }

    [Fact]
    public void SlowCrouchDoesNotSlide()
    {
        var m = Grounded();
        m.Step(Input(crouch: true, crouchPressed: true), Ground(), Dt);
        Assert.Equal(MoveState.Crouch, m.State);
        Run(m, Input(moveY: 1f, crouch: true), Ground(), 1f);
        Assert.Equal(m.S.CrouchSpeed, m.HorizontalSpeed, 2);
    }

    [Fact]
    public void SlideJumpKeepsSlideSpeed()
    {
        var m = Grounded();
        Run(m, Input(moveY: 1f), Ground(), 0.3f);
        m.Step(Input(moveY: 1f, crouch: true, crouchPressed: true), Ground(), Dt);
        m.Step(Input(moveY: 1f, crouch: true, jump: true), Ground(), Dt);
        Assert.Equal(MoveState.Air, m.State);
        Assert.Equal(m.S.JumpSpeed, m.Velocity.Y, 3);
        Assert.True(m.HorizontalSpeed > 15.5f);
    }

    [Fact]
    public void JumpApexMatchesSpec()
    {
        var m = Grounded();
        m.Step(Input(jump: true), Ground(), Dt);
        float y = 0f, apex = 0f;
        for (int i = 0; i < 240; i++)
        {
            m.Step(Input(), Ground(false), Dt);
            y += m.Velocity.Y * Dt;
            apex = Math.Max(apex, y);
        }
        Assert.InRange(apex, 1.4f, 1.55f); // spec: 1.48 m
    }

    [Fact]
    public void CoyoteJumpWorksJustAfterLeavingLedge()
    {
        var m = Grounded();
        Run(m, Input(moveY: 1f), Ground(), 0.2f);
        Run(m, Input(moveY: 1f), Ground(false), 0.2f);
        Assert.Equal(MoveState.Air, m.State);
        m.Step(Input(moveY: 1f, jump: true), Ground(false), Dt);
        Assert.True((m.Events & MoveEvents.CoyoteJump) != 0);
        Assert.Equal(m.S.JumpSpeed, m.Velocity.Y, 3);
    }

    [Fact]
    public void NoCoyoteJumpAfterWindow()
    {
        var m = Grounded();
        Run(m, Input(moveY: 1f), Ground(false), 0.4f);
        m.Step(Input(moveY: 1f, jump: true), Ground(false), Dt);
        Assert.True(m.Velocity.Y < 0f);
    }

    [Fact]
    public void NoDoubleJumpThroughCoyote()
    {
        var m = Grounded();
        m.Step(Input(jump: true), Ground(), Dt);
        m.Step(Input(), Ground(false), Dt);
        m.Step(Input(jump: true), Ground(false), Dt);
        Assert.True(m.Velocity.Y < m.S.JumpSpeed);
    }

    [Fact]
    public void BufferedJumpFiresOnLanding()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, -5f, 0f), grounded: false);
        m.Step(Input(jump: true), Ground(false), Dt);
        Run(m, Input(), Ground(false), 0.05f);
        m.Step(Input(), Ground(), Dt);
        Assert.Equal(MoveState.Air, m.State);
        Assert.Equal(m.S.JumpSpeed, m.Velocity.Y, 3);
    }

    [Fact]
    public void LateBufferedJumpExpires()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, -5f, 0f), grounded: false);
        m.Step(Input(jump: true), Ground(false), Dt);
        Run(m, Input(), Ground(false), 0.15f);
        m.Step(Input(), Ground(), Dt);
        Assert.Equal(MoveState.Ground, m.State);
    }

    [Fact]
    public void BunnyHopKeepsLandingSpeedButStandingBleedsIt()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, -1f, 18f), grounded: false);
        m.Step(Input(moveY: 1f), Ground(), Dt); // land
        Run(m, Input(moveY: 1f), Ground(), 0.1f);
        m.Step(Input(moveY: 1f, jump: true), Ground(), Dt);
        Assert.True(m.HorizontalSpeed > 17.5f, $"hop keeps landing speed: {m.HorizontalSpeed}");

        m.Reset(new Vec3(0f, -1f, 18f), grounded: false);
        m.Step(Input(moveY: 1f), Ground(), Dt);
        Run(m, Input(moveY: 1f), Ground(), 0.5f);
        Assert.Equal(m.S.RunSpeed, m.HorizontalSpeed, 1);
    }

    [Fact]
    public void WallRunChainBuildsSpeed()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 0f, 16f), grounded: false); // e.g. off a slide jump
        var right = Ground(false);
        right.WallRight = true;
        right.WallRightNormal = new Vec3(-1f, 0f, 0f);
        m.Step(Input(moveY: 1f), right, Dt);
        Assert.Equal(m.S.WallRunSpeed + 3f, m.HorizontalSpeed, 1); // 16.5 + (16 - 13)
        Run(m, Input(moveY: 1f), right, 0.3f);
        m.Step(Input(moveY: 1f, jump: true), right, Dt);
        float afterJump = m.HorizontalSpeed;
        Assert.InRange(afterJump, 19.3f, 19.6f); // wall jump keeps it

        var left = Ground(false);
        left.WallLeft = true;
        left.WallLeftNormal = new Vec3(1f, 0f, 0f);
        Run(m, Input(moveY: 1f), Ground(false), 0.2f);
        m.Step(Input(moveY: 1f), left, Dt);
        Assert.Equal(MoveState.WallRunLeft, m.State);
        Assert.True(m.HorizontalSpeed > afterJump + 3f, $"second wall run stacks: {afterJump} -> {m.HorizontalSpeed}");
    }

    [Fact]
    public void LandingAfterWallJumpDropsToRunPlusBoost()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 0f, 16f), grounded: false);
        var s = Ground(false);
        s.WallRight = true;
        s.WallRightNormal = new Vec3(-1f, 0f, 0f);
        m.Step(Input(moveY: 1f), s, Dt);
        m.Step(Input(moveY: 1f, jump: true), s, Dt);
        Run(m, Input(moveY: 1f), Ground(false), 0.8f); // past the apex, falling
        m.Step(Input(moveY: 1f), Ground(), Dt);
        Run(m, Input(moveY: 1f), Ground(), 0.3f);
        Assert.Equal(m.S.RunSpeed + m.Boost, m.HorizontalSpeed, 1);
        Assert.True(m.Boost > 2f);
    }

    [Fact]
    public void CrouchWalkClearsMomentum()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 0f, 16f), grounded: false);
        var wall = Ground(false);
        wall.WallRight = true;
        wall.WallRightNormal = new Vec3(-1f, 0f, 0f);
        m.Step(Input(moveY: 1f), wall, Dt); // wall run gives boost
        m.Step(Input(moveY: 1f), Ground(), Dt);
        Assert.True(m.Boost > 0f);
        Run(m, Input(moveY: 1f, crouch: true), Ground(), 2f); // slide ends into crouch eventually or stays sliding
        m.Step(Input(crouch: true), Ground(), Dt);
        Run(m, Input(crouch: true), Ground(), 4f);
        Assert.Equal(MoveState.Crouch, m.State);
        Assert.Equal(0f, m.Boost);
    }

    [Fact]
    public void AirKeepsSpeedAndCannotStrafeGain()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 0f, 16f), grounded: false);
        Run(m, Input(moveX: 1f), Ground(false), 0.5f);
        Assert.True(m.HorizontalSpeed <= 16.001f);
        Assert.True(m.Velocity.X > 1f, "steered toward strafe");
    }

    [Fact]
    public void WallRunStartsAndWallJumpLeavesAtMinAngle()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 0f, 13f), grounded: false);
        var s = Ground(false);
        s.WallRight = true;
        s.WallRightNormal = new Vec3(-1f, 0f, 0f);
        m.Step(Input(moveY: 1f), s, Dt);
        Assert.Equal(MoveState.WallRunRight, m.State);
        Assert.Equal(m.S.WallRunSpeed, m.Velocity.Flat.Magnitude, 1);

        Run(m, Input(moveY: 1f), s, 0.3f);
        Assert.Equal(MoveState.WallRunRight, m.State);
        Assert.True(m.Velocity.Y > 0f);

        m.Step(Input(moveY: 1f, jump: true), s, Dt);
        Assert.Equal(MoveState.Air, m.State);
        Assert.Equal(m.S.WallJumpUpSpeed, m.Velocity.Y, 3);
        float away = Vec3.Dot(m.Velocity.Flat.Normalized, new Vec3(-1f, 0f, 0f));
        Assert.Equal(0.5f, away, 2); // sin(30 deg)

        // Same side is blocked until landing.
        m.Step(Input(moveY: 1f), s, Dt);
        Assert.Equal(MoveState.Air, m.State);
    }

    [Fact]
    public void WallRunEndsAfterDuration()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 0f, 13f), grounded: false);
        var s = Ground(false);
        s.WallLeft = true;
        s.WallLeftNormal = new Vec3(1f, 0f, 0f);
        Run(m, Input(moveY: 1f), s, 1.6f);
        Assert.Equal(MoveState.Air, m.State);
    }

    [Fact]
    public void WallClimbGoesUpWhenFacingWall()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 3f, 5f), grounded: false);
        var s = Ground(false);
        s.WallFront = true;
        s.WallFrontNormal = new Vec3(0f, 0f, -1f);
        m.Step(Input(moveY: 1f), s, Dt);
        Assert.Equal(MoveState.WallClimb, m.State);
        Assert.Equal(m.S.WallClimbSpeed, m.Velocity.Y, 3);
    }

    [Fact]
    public void Playtest2SlideHopChainBuildsSpeed()
    {
        var m = new MovementMotor(new MovementSettings { Playtest2Model = true });
        m.Reset(Vec3.Zero, grounded: true);
        Run(m, Input(moveY: 1f), Ground(), 0.3f);
        m.Step(Input(moveY: 1f, crouch: true, crouchPressed: true), Ground(), Dt);
        float first = 0f;
        for (int hop = 0; hop < 5; hop++)
        {
            m.Step(Input(moveY: 1f, crouch: true, jump: true), Ground(), Dt);
            if (hop == 0) first = m.HorizontalSpeed;
            Run(m, Input(moveY: 1f, crouch: true), Ground(false), 0.5f);
            m.Step(Input(moveY: 1f, crouch: true), Ground(), Dt);
        }
        Assert.True(m.HorizontalSpeed > first + 2f, $"chain should build: {first} -> {m.HorizontalSpeed}");
    }

    [Fact]
    public void Playtest2WallJumpAddsGainAndVholumeDoesNot()
    {
        foreach (bool p2 in new[] { true, false })
        {
            var m = new MovementMotor(new MovementSettings { Playtest2Model = p2 });
            m.Reset(new Vec3(0f, 0f, 13f), grounded: false);
            var s = Ground(false);
            s.WallRight = true;
            s.WallRightNormal = new Vec3(-1f, 0f, 0f);
            m.Step(Input(moveY: 1f), s, Dt);
            m.Step(Input(moveY: 1f, jump: true), s, Dt);
            float expected = m.S.WallRunSpeed + (p2 ? m.S.P2WallJumpGain : 0f);
            Assert.Equal(expected, m.HorizontalSpeed, 1);
        }
    }

    [Fact]
    public void MantleRisesThenMovesOntoLedgeAndKeepsMomentum()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 2f, 10f), grounded: false);
        var s = Ground(false);
        s.Position = new Vec3(0f, 0.5f, 0f);
        s.WallFront = true;
        s.WallFrontNormal = new Vec3(0f, 0f, -1f);
        s.LedgeFound = true;
        s.LedgeTarget = new Vec3(0f, 1.3f, 0.9f);
        m.Step(Input(moveY: 1f), s, Dt);
        Assert.Equal(MoveState.Mantle, m.State);

        // Integrate position like the adapter would; the motor must finish within MantleMaxTime.
        var pos = s.Position;
        bool rose = false;
        for (int i = 0; i < 120 && m.State == MoveState.Mantle; i++)
        {
            s.Position = pos;
            m.Step(Input(moveY: 1f), s, Dt);
            if (m.Velocity.Y > 0f) rose = true;
            pos = pos + m.Velocity * Dt;
        }
        Assert.True(rose);
        Assert.NotEqual(MoveState.Mantle, m.State);
        Assert.True(pos.Y >= 1.25f, $"ended on top: {pos}");
        Assert.True(m.HorizontalSpeed >= 10f * m.S.MantleMomentumKeep - 0.01f, "momentum kept");
    }

    [Fact]
    public void NoMantleWithoutForwardInput()
    {
        var m = new MovementMotor(new MovementSettings());
        m.Reset(new Vec3(0f, 2f, 0f), grounded: false);
        var s = Ground(false);
        s.LedgeFound = true;
        s.LedgeTarget = new Vec3(0f, 1.3f, 0.9f);
        m.Step(Input(), s, Dt);
        Assert.Equal(MoveState.Air, m.State);
    }

    [Fact]
    public void RotateTowardsFlatTurnsTheShortWay()
    {
        var r = MovementMotor.RotateTowardsFlat(Fwd, Rgt, 0.1f);
        Assert.True(r.X > 0f);
        Assert.Equal(1f, r.Magnitude, 4);
        var full = MovementMotor.RotateTowardsFlat(Fwd, Rgt, 10f);
        Assert.Equal(1f, full.X, 4);
    }
}
