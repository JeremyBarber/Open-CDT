using CDT_Agnostic.Physics;

using Xunit;

using RealMove26 = CDT_2plus1.Engine.Topology.Pachner.Move26.Move26;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move26;

public sealed class Move26PhysicsTests
{
    [Theory]
    [InlineData(10, 5)]
    [InlineData(10_000, 5_000)]
    [InlineData(500_000, 250_000)]
    public void Move26_HastingsRatio_MatchesDoublePrecisionContinuousMath(int n13, int n0)
    {
        double n0Final = n0 + RealMove26.Deltas.DeltaN0;
        var theoreticalRatio = (n13 / n0Final) * (RealMove26.ReverseProbability / RealMove26.ForwardProbability);

        var h_Q28 = RealMove26.CalculateHastingsRatioQ28(n13, n0);
        var fastPathResult = (double)h_Q28 / MoveWeights.Q_ONE;

        Assert.Equal(theoreticalRatio, fastPathResult, tolerance: 1e-7);
    }
}