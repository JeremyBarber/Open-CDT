using CDT_Agnostic.Physics;

using Xunit;

using RealMove32 = CDT_2plus1.Engine.Topology.Pachner.Move32.Move32;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move32;

public sealed class Move32PhysicsTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(10_000)]
    [InlineData(500_000)]
    public void Move32_HastingsRatio_MatchesDoublePrecisionContinuousMath(int n22)
    {
        double n22Final = n22 + RealMove32.Deltas.DeltaN22;
        var theoreticalRatio = (n22Final / n22) * (RealMove32.ReverseProbability / RealMove32.ForwardProbability);

        var h_Q28 = RealMove32.CalculateHastingsRatioQ28(n22);
        var fastPathResult = (double)h_Q28 / MoveWeights.Q_ONE;

        Assert.Equal(theoreticalRatio, fastPathResult, tolerance: 1e-7);
    }
}