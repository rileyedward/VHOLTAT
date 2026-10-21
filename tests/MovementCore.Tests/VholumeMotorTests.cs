using System;
using VHOLTAT.Movement;
using Xunit;

/// <summary>The VHOLUME model against the numbers in docs/MOVEMENT_FORMULAS.md.</summary>
public class VholumeMotorTests
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

    private static MovementSensors RightWall() =>
        new MovementSensors { Grounded = false, GroundNormal = Vec3.Up, HeightAboveGround = 3f, WallRight = true, WallRightNormal = new Vec3(-1f, 0f, 0f) };

    private static VholumeMotor Make(Vec3 v, bool grounded)
    {
        var m = new VholumeMotor(new VholumeSettings());
        m.Reset(v, grounded);
        return m;
    }

    private static void Run(VholumeMotor m, MovementInput input, MovementSensors sensors, float seconds)
    {
        var first = input;
        int n = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < n; i++)
        {
            m.Step(in first, in sensors, Dt);
            first.JumpPressed = false;
            first.CrouchPressed = false;
        }
    }

    [Fact]
    public void RunAcceleratesAt100AndCapsAt13()
    {
        var m = Make(Vec3.Zero, true);
        Run(m, Input(moveY: 1f), Ground(), 0.05f);
        Assert.InRange(m.HorizontalSpeed, 4.8f, 5.2f);  // 100 m/s^2
        Run(m, Input(moveY: 1f), Ground(), 0.3f);
        Assert.Equal(13f, m.HorizontalSpeed, 2);
    }

    [Fact]
    public void BrakingStopsFromRunSpeedInAQuarterSecond()
    {
        // dv/dt = -(20.48 + 8 v): from 13 m/s, t = ln(1 + 8*13/20.48) / 8 = 0.226 s
        var m = Make(new Vec3(0f, 0f, 13f), true);
        Run(m, Input(), Ground(), 0.1f);
        Assert.InRange(m.HorizontalSpeed, 3.0f, 4.6f);
        Run(m, Input(), Ground(), 0.15f);
        Assert.Equal(0f, m.HorizontalSpeed, 2);
    }

    [Fact]
    public void JumpIs8UpWithGravity2156()
    {
        var m = Make(new Vec3(0f, 0f, 13f), true);
        m.Step(Input(moveY: 1f, jump: true), Ground(), Dt);
        Assert.Equal(MoveState.Air, m.State);
        Assert.InRange(m.Velocity.Y, 8f - 21.56f * Dt - 0.01f, 8f);
        Run(m, Input(moveY: 1f), Ground(false), 0.5f);
        Assert.InRange(m.Velocity.Y, 8f - 21.56f * (0.5f + Dt) - 0.05f, 8f - 21.56f * 0.5f + 0.05f);
    }

    [Fact]
    public void AirControlDoesNotBuildPastTheCap()
    {
        var m = Make(new Vec3(0f, 0f, 13f), false);
        Run(m, Input(moveY: 1f), Ground(false), 0.5f);
        Assert.Equal(13f, m.HorizontalSpeed, 2);
        var fast = Make(new Vec3(0f, 0f, 18f), false);
        Run(fast, Input(moveY: 1f), Ground(false), 0.5f);
        Assert.Equal(18f, fast.HorizontalSpeed, 2);  // no air friction
    }

    [Fact]
    public void HopInsideTheLandingBufferAddsAFifthOfTheExcess()
    {
        var m = Make(new Vec3(0f, -6f, 20f), false);
        Run(m, Input(moveY: 1f), Ground(false), 0.2f);  // real landing needs 0.1 s in the air
        m.Step(Input(moveY: 1f), Ground(), Dt);         // land
        Assert.True(m.IsGroundState);
        m.Step(Input(moveY: 1f, jump: true), Ground(), Dt);
        Assert.Equal(MoveState.Air, m.State);
        // landing 3D speed sqrt(20^2 + fall^2) is above 20; the gain is 0.2 * (S - 13)
        Assert.InRange(m.Boost, 0.2f * (20f - 13f) - 0.05f, 0.2f * (23f - 13f));
    }

    [Fact]
    public void HopAfterTheBufferGivesNothing()
    {
        var m = Make(new Vec3(0f, -6f, 20f), false);
        Run(m, Input(moveY: 1f), Ground(false), 0.2f);
        m.Step(Input(moveY: 1f), Ground(), Dt);
        Run(m, Input(moveY: 1f), Ground(), 0.3f);
        m.Step(Input(moveY: 1f, jump: true), Ground(), Dt);
        Assert.Equal(0f, m.Boost);
    }

    [Fact]
    public void BoostDecaysAt08PerSecondAfterTheConserveWindow()
    {
        var m = Make(new Vec3(0f, -6f, 25f), false);
        Run(m, Input(moveY: 1f), Ground(false), 0.2f);
        m.Step(Input(moveY: 1f), Ground(), Dt);
        m.Step(Input(moveY: 1f, jump: true), Ground(), Dt);
        float b0 = m.Boost;
        Assert.True(b0 > 2f);
        Run(m, Input(moveY: 1f), Ground(false), 0.4f);
        Assert.Equal(b0, m.Boost, 3);  // conserve window 0.5 s
        float b1 = m.Boost;
        Run(m, Input(moveY: 1f), Ground(false), 0.5f);
        Assert.InRange(m.Boost, b1 * (float)Math.Exp(-0.8 * 0.45), b1 * (float)Math.Exp(-0.8 * 0.35));
    }

    [Fact]
    public void FlatSlideLastsAbout08SecondsThenDropsToCrouchCap()
    {
        // Timer starts at 45 and falls 50 per second on flat ground: the 16 m/s cap holds until 5, at 0.8 s.
        var m = Make(new Vec3(0f, 0f, 13f), true);
        m.Step(Input(moveY: 1f, crouch: true, crouchPressed: true), Ground(), Dt);
        Assert.Equal(MoveState.Slide, m.State);
        Run(m, Input(moveY: 1f, crouch: true), Ground(), 0.7f);
        Assert.Equal(MoveState.Slide, m.State);
        Assert.True(m.HorizontalSpeed >= 13f);
        Run(m, Input(moveY: 1f, crouch: true), Ground(), 0.6f);
        Assert.Equal(MoveState.Crouch, m.State);
        Assert.True(m.HorizontalSpeed <= 5.6f);
    }

    [Fact]
    public void SlideNeedsSevenMetresPerSecond()
    {
        var m = Make(new Vec3(0f, 0f, 6f), true);
        m.Step(Input(moveY: 1f, crouch: true, crouchPressed: true), Ground(), Dt);
        Assert.Equal(MoveState.Crouch, m.State);
    }

    [Fact]
    public void CoyoteJumpWorksForAThirdOfASecondAfterWalkingOff()
    {
        var m = Make(new Vec3(0f, 0f, 13f), true);
        Run(m, Input(moveY: 1f), Ground(), 0.6f);  // last jump long ago
        Run(m, Input(moveY: 1f), Ground(false), 0.25f);
        m.Step(Input(moveY: 1f, jump: true), Ground(false), Dt);
        Assert.True((m.Events & MoveEvents.CoyoteJump) != 0);
        Assert.InRange(m.Velocity.Y, 7.5f, 8f);

        var late = Make(new Vec3(0f, 0f, 13f), true);
        Run(late, Input(moveY: 1f), Ground(), 0.6f);
        Run(late, Input(moveY: 1f), Ground(false), 0.4f);
        late.Step(Input(moveY: 1f, jump: true), Ground(false), Dt);
        Assert.True((late.Events & MoveEvents.CoyoteJump) == 0);
    }

    [Fact]
    public void JumpPressedJustBeforeLandingFiresOnTheNextTick()
    {
        var m = Make(new Vec3(0f, -6f, 13f), false);
        Run(m, Input(moveY: 1f), Ground(false), 0.2f);
        m.Step(Input(moveY: 1f, jump: true), Ground(false), Dt);
        Run(m, Input(moveY: 1f), Ground(false), 0.05f);
        m.Step(Input(moveY: 1f), Ground(), Dt);  // land
        m.Step(Input(moveY: 1f), Ground(), Dt);
        Assert.True((m.Events & MoveEvents.BufferedJump) != 0);
        Assert.Equal(MoveState.Air, m.State);
    }

    [Fact]
    public void WallRunFollowsTheVerticalCurve()
    {
        // vz = 2 for the first 0.3 s (tau < 0.2), then falls along ((tau-0.2)/0.8)^2.4 to -4 at 1.5 s.
        var m = Make(new Vec3(0f, 0f, 13f), false);
        m.Step(Input(moveY: 1f), RightWall(), Dt);
        Assert.Equal(MoveState.WallRunRight, m.State);
        Run(m, Input(moveY: 1f), RightWall(), 0.25f);
        Assert.Equal(2f, m.Velocity.Y, 3);
        Assert.Equal(16.5f, m.Velocity.Z, 2);
        Run(m, Input(moveY: 1f), RightWall(), 0.5f);  // t = 0.76 s, tau ~ 0.51
        float c = (float)Math.Pow((0.51 - 0.2) / 0.8, 2.4);
        Assert.InRange(m.Velocity.Y, 2f - 6f * c - 0.2f, 2f - 6f * c + 0.2f);
        Run(m, Input(moveY: 1f), RightWall(), 0.8f);
        Assert.Equal(-4f, m.Velocity.Y, 2);
        Assert.Equal(MoveState.WallRunRight, m.State);  // no time limit
    }

    [Fact]
    public void WallJumpUsesTheLookDirectionAtWallRunSpeed()
    {
        var m = Make(new Vec3(0f, 0f, 13f), false);
        Run(m, Input(moveY: 1f), RightWall(), 0.2f);
        var jump = Input(moveY: 1f, jump: true);
        jump.Look = new Vec3(-0.7071f, 0f, 0.7071f);  // 45 degrees off the wall
        m.Step(in jump, RightWall(), Dt);
        Assert.True((m.Events & MoveEvents.WallJumped) != 0);
        Assert.Equal(16.5f, m.HorizontalSpeed, 1);    // max(14, wall-run speed)
        Assert.InRange(m.Velocity.Y, 7.5f - 0.2f, 7.5f);
        Assert.True(m.Velocity.X < -11f);
    }

    [Fact]
    public void WallJumpLookingAlongTheWallTurns30DegreesAway()
    {
        var m = Make(new Vec3(0f, 0f, 13f), false);
        Run(m, Input(moveY: 1f), RightWall(), 0.2f);
        m.Step(Input(moveY: 1f, jump: true), RightWall(), Dt);
        float angle = (float)(Math.Atan2(-m.Velocity.X, m.Velocity.Z) * 180.0 / Math.PI);
        Assert.InRange(angle, 29f, 31f);
    }

    [Fact]
    public void ReleasingForwardEndsTheWallRunAndBlocksThatSide()
    {
        var m = Make(new Vec3(0f, 0f, 13f), false);
        Run(m, Input(moveY: 1f), RightWall(), 0.2f);
        m.Step(Input(), RightWall(), Dt);
        Assert.Equal(MoveState.Air, m.State);
        Run(m, Input(moveY: 1f), RightWall(), 0.5f);
        Assert.Equal(MoveState.Air, m.State);  // gate 0.8 s
    }

    [Fact]
    public void SmallVaultKeeps95PercentOfTheAverageSpeed()
    {
        // 1 s of running at 10 m/s (weapon speed scale), then off an edge onto a low ledge
        var m = Make(new Vec3(0f, 0f, 10f), true);
        var run = Input(moveY: 1f);
        run.SpeedScale = 10f / 13f;
        Run(m, run, Ground(), 1f);
        var air = Ground(false);
        Run(m, run, air, 2 * Dt);
        var ledge = air;
        ledge.Position = new Vec3(0f, 0f, 0f);
        ledge.LedgeFound = true;
        ledge.LedgeTarget = new Vec3(0f, 0.3f, 0.5f);
        m.Step(Input(moveY: 1f), in ledge, Dt);
        Assert.Equal(MoveState.Mantle, m.State);
        var done = ledge;
        done.Position = ledge.LedgeTarget;
        m.Step(Input(moveY: 1f), in done, Dt);
        Assert.Equal(MoveState.Air, m.State);
        Assert.InRange(m.Velocity.Magnitude, 0.95f * 9.5f, 0.95f * 10.1f);
    }

    [Fact]
    public void NoAllocationsPerStep()
    {
        var m = Make(new Vec3(0f, 0f, 13f), true);
        Run(m, Input(moveY: 1f), Ground(), 0.5f);
        long before = GC.GetAllocatedBytesForCurrentThread();
        Run(m, Input(moveY: 1f, crouch: true, crouchPressed: true), Ground(), 0.5f);
        Run(m, Input(moveY: 1f), RightWall(), 0.5f);
        Assert.Equal(before, GC.GetAllocatedBytesForCurrentThread());
    }
}
