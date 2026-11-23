using CDT_Agnostic.RandomNumberGeneration;

using Xunit;

namespace CDT_Agnostic_Tests.RandomNumberGeneration;

public sealed unsafe class Pcg32Tests
{
    [Fact]
    public void NextUInt32_WithCanonicalONeillSeed_MatchesOfficialReferenceVectors()
    {
        // Canonical test vector from Melissa O'Neill's pcg32-demo.c: pcg32_srandom_r(&rng, 42u, 54u)
        Pcg32 rng = default;
        rng.Seed(initState: 42UL, initInc: 54UL);

        uint[] expected =
        [
            0xa15c02b7u,
            0x7b47f409u,
            0xba1d3330u,
            0x83d2f293u,
            0xbfa4784bu,
            0xcbed606eu
        ];

        foreach (var expectedValue in expected)
        {
            Assert.Equal(expectedValue, rng.NextUInt32());
        }
    }

    [Fact]
    public void CreateAndDestroy_AllocatesDeterministicGeneratorAndHandlesNullSafely()
    {
        var rngA = Pcg32.Create(initState: 123456UL, initInc: 1UL);
        var rngB = Pcg32.Create(initState: 123456UL, initInc: 2UL);

        try
        {
            Assert.NotEqual(nint.Zero, (nint)rngA);
            Assert.NotEqual(nint.Zero, (nint)rngB);

            // Different stream increments (initInc) from the same initState must diverge immediately
            Assert.NotEqual(rngA->NextUInt32(), rngB->NextUInt32());
        }
        finally
        {
            Pcg32.Destroy(rngA);
            Pcg32.Destroy(rngB);
            Pcg32.Destroy(null); // Null destroy must be a safe no-op
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(int.MinValue)]
    public void NextBounded_NonPositiveBound_ThrowsArgumentOutOfRangeException(int invalidBound)
    {
        Pcg32 rng = default;
        rng.Seed(42UL, 54UL);

        Assert.Throws<ArgumentOutOfRangeException>(() => rng.NextBounded(invalidBound));
    }

    [Fact]
    public void NextBounded_AcrossSmallAndLargeBounds_RespectsBoundsAndExercisesRejectionLoop()
    {
        Pcg32 rng = default;
        rng.Seed(42UL, 54UL);

        // 1. Bound of 1 must strictly return 0
        for (var i = 0; i < 10; i++)
        {
            Assert.Equal(0, rng.NextBounded(1));
        }

        // 2. Standard small bound [0, 7)
        for (var i = 0; i < 100; i++)
        {
            var val = rng.NextBounded(7);
            Assert.InRange(val, 0, 6);
        }

        // 3. Large non-power-of-two bound (0x60000000) has a large remainder threshold (0x40000000),
        // deterministically exercising Lemire's inner rejection loop (leftover < threshold).
        rng.Seed(42UL, 54UL);
        const int largeBound = 0x60000000;
        for (var i = 0; i < 10; i++)
        {
            var val = rng.NextBounded(largeBound);
            Assert.InRange(val, 0, largeBound - 1);
        }
    }
}