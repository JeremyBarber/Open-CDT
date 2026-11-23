using CDT_Agnostic.Physics.Components;

using Xunit;

namespace CDT_Agnostic_Tests.Physics.Components;

public sealed class BoltzmannWeightTests
{
    [Fact]
    public void Evaluate_ZeroTotalActionChange_ReturnsExactlyOne()
    {
        var weight = BoltzmannWeight.Evaluate(reggeDelta: 0.0, baseVolumeS: 0.0, driftFactor: 0.0, diff: 0);

        Assert.Equal(1.0, weight, precision: 15);
    }

    [Theory]
    [InlineData(0.5, 0.01, 0.004, 100)]   // Energetically unfavourable (ΔS > 0 -> weight < 1)
    [InlineData(-0.5, 0.01, 0.004, -100)] // Energetically favourable   (ΔS < 0 -> weight > 1)
    public void Evaluate_CombinesReggeAndVolumeFixingCorrectly(
        double reggeDelta,
        double baseVolumeS,
        double driftFactor,
        int diff)
    {
        var expectedDeltaS = reggeDelta + baseVolumeS + (driftFactor * diff);
        var expectedWeight = Math.Exp(-expectedDeltaS);

        var actualWeight = BoltzmannWeight.Evaluate(reggeDelta, baseVolumeS, driftFactor, diff);

        Assert.Equal(expectedWeight, actualWeight, precision: 14);
    }
}