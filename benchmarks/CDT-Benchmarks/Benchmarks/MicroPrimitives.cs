using CDT_Agnostic.Accessors;
using CDT_Agnostic.Physics;
using CDT_Agnostic.Physics.Components;
using CDT_Agnostic.RandomNumberGeneration;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using BenchmarkDotNet.Attributes;

namespace CDT_Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks the core hot-loop mathematical primitives in <c>CDT-Agnostic</c>:
/// (1) <c>Q4.28</c> fixed-point L1 LUT Metropolis acceptance vs. IEEE-754 <see cref="Math.Exp"/>, and
/// (2) <see cref="Pcg32.NextBounded"/> (Lemire divisionless multiply-shift) vs. <see cref="Random.Next(int)"/>.
/// </summary>
public unsafe class MicroPrimitives
{
    private Pcg32 _pcg;
    private Random _bclRandom = null!;
    private MoveWeights _weights;
    private int _bound;
    private int _currentVolume;
    private int _volumeDiff;
    private uint _hastingsQ28;

    private readonly struct DirectRandomAccessor(Pcg32* rng) : IRandomAccessor
    {
        public uint GetRandom() => rng->NextUInt32();
        public int GetRandom(int maxExclusive) => rng->NextBounded(maxExclusive);
        public uint GetRandomBoundedAtFour() => rng->NextUInt32() & 3u;
        public SimplexId SelectRandomActiveSimplex() => default;
        public VertexId SelectRandomVertex() => default;
    }

    [GlobalSetup]
    public void Setup()
    {
        _pcg.Seed(initState: 42UL, initInc: 54UL);
        _bclRandom = new Random(42);
        _bound = 16_000;

        _weights = MoveWeights.Allocate(volumeRadius: 256);
        _weights.BuildWeights(
            reggeDelta: 0.35,
            volumeTarget: 16_000,
            volumeDeltaForMove: 4,
            epsilon: 0.02);

        _currentVolume = 16_012;
        _volumeDiff = _currentVolume - _weights.VolumeTarget;
        _hastingsQ28 = (uint)(0.85 * MoveWeights.Q_ONE);
    }

    [GlobalCleanup]
    public void Cleanup() => _weights.Dispose();

    // =========================================================================
    // 1A. Q4.28 FIXED-POINT LUT vs. DOUBLE-PRECISION Math.Exp
    // =========================================================================

    [Benchmark(Description = "Metropolis: Double Math.Exp(-dS) Slow Path")]
    public bool Metropolis_DoubleMathExp()
    {
        fixed (Pcg32* rngPtr = &_pcg)
        {
            return Metropolis.EvaluateSlowPath(
                new DirectRandomAccessor(rngPtr),
                _volumeDiff,
                _hastingsQ28,
                in _weights);
        }
    }

    [Benchmark(Description = "Metropolis: Q4.28 Fixed-Point L1 LUT Fast Path")]
    public bool Metropolis_Q28FixedPointLut()
    {
        fixed (Pcg32* rngPtr = &_pcg)
        {
            return Metropolis.ShouldAccept(
                new DirectRandomAccessor(rngPtr),
                _currentVolume,
                _hastingsQ28,
                in _weights);
        }
    }

    // =========================================================================
    // 1B. PCG32 (LEMIRE MULTIPLY-SHIFT) vs. SYSTEM.RANDOM
    // =========================================================================

    [Benchmark(Description = "RNG: System.Random.Next(bound)")]
    public int Rng_SystemRandom() => _bclRandom.Next(_bound);

    [Benchmark(Description = "RNG: Pcg32.NextBounded(bound) [Lemire]")]
    public int Rng_Pcg32Lemire() => _pcg.NextBounded(_bound);
}