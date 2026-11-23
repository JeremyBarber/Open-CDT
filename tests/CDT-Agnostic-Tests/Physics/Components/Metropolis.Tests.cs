using CDT_Agnostic.Accessors;
using CDT_Agnostic.Physics;
using CDT_Agnostic.Physics.Components;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

namespace CDT_Agnostic_Tests.Physics.Components;

public sealed class MetropolisTests
{
    private readonly struct MockEntropyRandomAccessor(uint rawEntropy) : IRandomAccessor
    {
        public uint GetRandom() => rawEntropy;
        public int GetRandom(int bound) => 0;
        public uint GetRandomBoundedAtFour() => 0;
        public SimplexId SelectRandomActiveSimplex() => new(0);
        public VertexId SelectRandomVertex() => new(0);
    }

    [Fact]
    public void ShouldAccept_FastPath_WhenProbabilityAtLeastOne_AlwaysAcceptsEvenAtMaxEntropy()
    {
        var weights = MoveWeights.Allocate(volumeRadius: 64);
        try
        {
            // ΔS = 0 -> e_Q28 = Q_ONE (1.0). With h_Q28 = Q_ONE (1.0), pAccept_Q28 == Q_ONE.
            weights.BuildWeights(
                reggeDelta: 0.0,
                volumeTarget: 1000,
                volumeDeltaForMove: 0,
                epsilon: 0.0);

            // Even if the RNG rolls uint.MaxValue (all 1s), shifting right by 4 yields Q_ONE - 1 < Q_ONE.
            var maxEntropyRng = new MockEntropyRandomAccessor(uint.MaxValue);

            var accepted = Metropolis.ShouldAccept(
                maxEntropyRng,
                currentVolume: 1000,
                h_Q28: MoveWeights.Q_ONE,
                in weights);

            Assert.True(accepted);
        }
        finally
        {
            weights.Dispose();
        }
    }

    [Fact]
    public void ShouldAccept_FastPath_RespectsExactQ28ThresholdBoundary()
    {
        var weights = MoveWeights.Allocate(volumeRadius: 64);
        try
        {
            weights.BuildWeights(
                reggeDelta: 0.0,
                volumeTarget: 1000,
                volumeDeltaForMove: 0,
                epsilon: 0.0);

            // Set Hastings ratio to exactly 0.5 in Q4.28 (Q_ONE / 2)
            var halfQ28 = MoveWeights.Q_ONE >> 1;

            // 1. Just below threshold: entropy == halfQ28 - 1 -> MUST ACCEPT
            var rawJustBelow = (halfQ28 - 1) << (32 - MoveWeights.Q_SHIFT);
            Assert.True(Metropolis.ShouldAccept(
                new MockEntropyRandomAccessor(rawJustBelow),
                currentVolume: 1000,
                h_Q28: halfQ28,
                in weights));

            // 2. Exactly at threshold: entropy == halfQ28 -> MUST REJECT (strict '<')
            var rawAtThreshold = halfQ28 << (32 - MoveWeights.Q_SHIFT);
            Assert.False(Metropolis.ShouldAccept(
                new MockEntropyRandomAccessor(rawAtThreshold),
                currentVolume: 1000,
                h_Q28: halfQ28,
                in weights));
        }
        finally
        {
            weights.Dispose();
        }
    }

    [Theory]
    [InlineData(1000 - 5000)] // Far below -VolumeRadius (negative diff wraps in unsigned check -> Slow Path)
    [InlineData(1000 + 5000)] // Far above +VolumeRadius (exceeds 2 * VolumeRadius -> Slow Path)
    public void ShouldAccept_OutsideVolumeRadius_SafelyFallsBackToSlowPathWithoutOutOfBoundsRead(int currentVolume)
    {
        var weights = MoveWeights.Allocate(volumeRadius: 64);
        try
        {
            // Expansion move (+4) with strong volume-fixing spring (epsilon = 0.05) around target 1000
            weights.BuildWeights(
                reggeDelta: 0.0,
                volumeTarget: 1000,
                volumeDeltaForMove: 4,
                epsilon: 0.05);

            var accepted = Metropolis.ShouldAccept(
                new MockEntropyRandomAccessor(uint.MaxValue / 2),
                currentVolume: currentVolume,
                h_Q28: MoveWeights.Q_ONE,
                in weights);

            var expected = currentVolume < 1000;
            Assert.Equal(expected, accepted);
        }
        finally
        {
            weights.Dispose();
        }
    }
}