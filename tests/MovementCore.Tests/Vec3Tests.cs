using VHOLTAT.Movement;
using Xunit;

public class Vec3Tests
{
    [Fact]
    public void ProjectOnPlaneRemovesNormalComponent()
    {
        var v = Vec3.ProjectOnPlane(new Vec3(1f, 2f, 3f), Vec3.Up);
        Assert.Equal(0f, v.Y);
        Assert.Equal(1f, v.X);
    }

    [Fact]
    public void ClampMagnitudeKeepsDirection()
    {
        var v = Vec3.ClampMagnitude(new Vec3(3f, 0f, 4f), 1f);
        Assert.Equal(1f, v.Magnitude, 4);
        Assert.Equal(0.6f, v.X, 4);
    }
}
