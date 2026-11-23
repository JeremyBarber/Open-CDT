using CDT_Agnostic.Physics;

using Xunit;

using RealMove23 = CDT_2plus1.Engine.Topology.Pachner.Move23.Move23;

namespace CDT_2plus1_Tests.Engine.Topology.Pachner.Move23;

public sealed class Move23PhysicsTests
{
    [Theory]
    [InlineData(10)]
    [InlineData(10_000)]
    [InlineData(500_000)]
    public void Move23_HastingsRatio_MatchesDoublePrecisionContinuousMath(int n22)
    {
        double n22Final = n22 + RealMove23.Deltas.DeltaN22;
        var theoreticalRatio = (n22 / n22Final) * (RealMove23.ReverseProbability / RealMove23.ForwardProbability);

        var h_Q28 = RealMove23.CalculateHastingsRatioQ28(n22);
        var fastPathResult = (double)h_Q28 / MoveWeights.Q_ONE;

        Assert.Equal(theoreticalRatio, fastPathResult, tolerance: 1e-7);
    }
}