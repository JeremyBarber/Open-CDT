using Xunit;

using RealMove44 = CDT_2plus1.Engine.Topology.Pachner.Move44.Move44;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move44;

public sealed class Move44PhysicsTests
{
    [Fact]
    public void Move44_IsAlwaysEnergeticallyFavourable_BecauseMathIsSymmetric()
    {
        Assert.Equal(0, RealMove44.Deltas.DeltaN0);
        Assert.Equal(0, RealMove44.Deltas.DeltaN3);

        var theoreticalRatio = RealMove44.ReverseProbability / RealMove44.ForwardProbability;
        Assert.Equal(1.0, theoreticalRatio);

        Assert.True(RealMove44.IsMoveEnergeticallyFavourable(default, default, default));
    }
}