using CDT_Agnostic.Physics;
using CDT_Agnostic.Physics.Components;
using CDT_Agnostic.RuntimeMemory;

using System.Runtime.InteropServices;

using Xunit;

namespace CDT_Agnostic_Tests.Physics;

public sealed unsafe class MoveWeightsTests
{
    /// <summary>
    /// Testing-only memory overlay that mirrors the sequential layout of <see cref="MoveWeights"/>
    /// to verify 64-byte L1 cache alignment and pointer zeroing on disposal without widening field visibility.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    private struct MoveWeightsOverlay
    {
        public int VolumeTarget;
        public int VolumeRadius;
        public double ReggeDelta;
        public double BaseVolumeS;
        public double DriftFactor;
        public uint* ExpDeltaS;
    }

    [Theory]
    [InlineData(1, 16)]
    [InlineData(16, 16)]
    [InlineData(17, 32)]
    [InlineData(100, 112)]
    public void Allocate_PadsVolumeRadiusToMultipleOfSixteen_AndAlignsTo64ByteBoundary(
        int requestedRadius,
        int expectedPaddedRadius)
    {
        var weights = MoveWeights.Allocate(requestedRadius);
        var overlay = (MoveWeightsOverlay*)&weights;

        try
        {
            Assert.Equal(expectedPaddedRadius, weights.VolumeRadius);
            Assert.NotEqual(nint.Zero, (nint)overlay->ExpDeltaS);

            // Prove the LUT pointer sits on an exact 64-byte L1 cache-line boundary
            Assert.Equal(0UL, (ulong)overlay->ExpDeltaS % (ulong)RuntimeConstants.CacheLineBytes);
        }
        finally
        {
            weights.Dispose();
            Assert.Equal(nint.Zero, (nint)overlay->ExpDeltaS);
        }
    }

    [Fact]
    public void UpdateWeights_MatchesFloatingPointBoltzmannWeightWithinQ28Precision()
    {
        var weights = MoveWeights.Allocate(volumeRadius: 32);
        try
        {
            const double reggeDelta = 0.35;
            const int volumeTarget = 5000;
            const int volumeDeltaForMove = 4;
            const double epsilon = 0.002;

            weights.BuildWeights(reggeDelta, volumeTarget, volumeDeltaForMove, epsilon);

            Assert.Equal(volumeTarget, weights.VolumeTarget);
            Assert.Equal(reggeDelta, weights.ReggeDelta);

            for (var diff = -weights.VolumeRadius; diff <= weights.VolumeRadius; diff++)
            {
                var q28Value = weights.GetExpDeltaS(diff);
                var actual = (double)q28Value / MoveWeights.Q_ONE;

                var expected = Math.Clamp(
                    BoltzmannWeight.Evaluate(reggeDelta, weights.BaseVolumeS, weights.DriftFactor, diff),
                    0.0,
                    15.99);

                // Q4.28 quantization step is 2^-28 ≈ 3.725e-9; assert absolute difference <= 1e-7
                Assert.Equal(expected, actual, tolerance: 1e-7);
            }
        }
        finally
        {
            weights.Dispose();
        }
    }

    [Fact]
    public void UpdateWeights_ClampsExtremeWeightsAndHandlesDoubleDisposeSafely()
    {
        var weights = MoveWeights.Allocate(volumeRadius: 16);
        try
        {
            // Large negative ReggeDelta (-10.0) produces exp(+10) ≈ 22,026.46 > 15.99 -> must clamp to 15.99
            weights.BuildWeights(reggeDelta: -10.0, volumeTarget: 100, volumeDeltaForMove: 0, epsilon: 0.0);
            Assert.Equal((uint)(15.99 * MoveWeights.Q_ONE), weights.GetExpDeltaS(0));

            // NaN ReggeDelta -> must clamp to 0
            weights.BuildWeights(reggeDelta: double.NaN, volumeTarget: 100, volumeDeltaForMove: 0, epsilon: 0.0);
            Assert.Equal(0u, weights.GetExpDeltaS(0));
        }
        finally
        {
            weights.Dispose();
            weights.Dispose(); // Second Dispose must be a safe no-op
        }
    }
}