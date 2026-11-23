using CDT_Agnostic.Physics.Components;

using Xunit;

namespace CDT_Agnostic_Tests.Physics.Components;

public sealed class VolumeFixingTests
{
    [Theory]
    [InlineData(0.0, 4)]
    [InlineData(0.002, 0)]
    [InlineData(0.0, 0)]
    public void CalculateConstants_ZeroEpsilonOrZeroDeltaVolume_ReturnsZeros(double epsilon, int deltaVolume)
    {
        (var baseVolumeS, var driftFactor) = VolumeFixing.CalculateConstants(epsilon, deltaVolume);

        Assert.Equal(0.0, baseVolumeS);
        Assert.Equal(0.0, driftFactor);
    }

    [Theory]
    [InlineData(-4)] // Move (6,2) contraction
    [InlineData(-1)] // Move (3,2) contraction
    [InlineData(1)]  // Move (2,3) expansion
    [InlineData(4)]  // Move (2,6) expansion
    public void Evaluate_AcrossVolumeDrifts_MatchesExactQuadraticActionDifference(int deltaVolume)
    {
        const double epsilon = 0.0025;
        const int targetVolume = 10_000;

        (var baseVolumeS, var driftFactor) = VolumeFixing.CalculateConstants(epsilon, deltaVolume);

        foreach (var currentVolume in new[] { 9_200, 9_999, 10_000, 10_001, 10_800 })
        {
            var drift = currentVolume - targetVolume;

            var actualDeltaS = VolumeFixing.Evaluate(baseVolumeS, driftFactor, drift);

            var initialAction = epsilon * (currentVolume - targetVolume) * (currentVolume - targetVolume);
            var finalAction = epsilon * (currentVolume + deltaVolume - targetVolume) * (currentVolume + deltaVolume - targetVolume);
            var expectedDeltaS = finalAction - initialAction;

            Assert.Equal(expectedDeltaS, actualDeltaS, precision: 10);
        }
    }
}