using CDT_Agnostic.Physics;

using Xunit;

using RealMove62 = CDT_2plus1.Engine.Topology.Pachner.Move62.Move62;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move62;

public sealed class Move62PhysicsTests
{
    [Theory]
    [InlineData(10, 5)]
    [InlineData(10_000, 5_000)]
    [InlineData(500_000, 250_000)]
    public void Move62_HastingsRatio_MatchesDoublePrecisionContinuousMath(int n13, int n0)
    {
        double n13Final = n13 + RealMove62.Deltas.DeltaN13;
        var theoreticalRatio = (n0 / n13Final) * (RealMove62.ReverseProbability / RealMove62.ForwardProbability);

        var h_Q28 = RealMove62.CalculateHastingsRatioQ28(currentN13: n13, currentN0: n0);
        var fastPathResult = (double)h_Q28 / MoveWeights.Q_ONE;

        Assert.Equal(theoreticalRatio, fastPathResult, tolerance: 1e-7);
    }
}