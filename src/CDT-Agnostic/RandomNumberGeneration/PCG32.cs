using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static CDT_Agnostic.RuntimeMemory.Native.Allocation;

namespace CDT_Agnostic.RandomNumberGeneration;

/// <summary>
/// A high-performance, 16-byte unmanaged <c>PCG32</c> (<c>PCG-XSH-RR</c>) pseudorandom number generator.
/// Provides deterministic, statistically rigorous entropy with zero GC allocations and <c>2^63</c> independent streams.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe partial struct Pcg32
{
    private const ulong LcgMultiplier = 6364136223846793005UL;
    private const int XorShiftBits = 18;
    private const int PermutationShiftBits = 27;
    private const int RotationShiftBits = 59;

    private ulong _state;
    private ulong _inc;

    /// <summary>
    /// Allocates a new <see cref="Pcg32"/> generator on the native heap and seeds its state.
    /// </summary>
    /// <param name="initState">The starting 64-bit state seed.</param>
    /// <param name="initInc">The 63-bit stream/sequence selector (enables independent parallel streams from the same <paramref name="initState"/>).</param>
    /// <returns>An unmanaged pointer to the initialized <see cref="Pcg32"/> struct.</returns>
    public static Pcg32* Create(ulong initState, ulong initInc)
    {
        var rng = Allocate<Pcg32>(1);
        rng->Seed(initState, initInc);
        return rng;
    }

    /// <summary>
    /// Safely frees the native heap memory allocated for a <see cref="Pcg32"/> generator.
    /// </summary>
    /// <param name="rng">The pointer to the generator to destroy (safe to pass <see langword="null"/>).</param>
    public static void Destroy(Pcg32* rng)
    {
        if (rng == null)
            return;

        NativeMemory.Free(rng);
    }

    /// <summary>
    /// Initializes the generator state and stream increment according to the canonical PCG32 two-step warm-up.
    /// </summary>
    /// <param name="initState">The starting 64-bit state seed.</param>
    /// <param name="initInc">The 63-bit stream/sequence selector.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Seed(ulong initState, ulong initInc)
    {
        _state = 0UL;

        // Hull-Dobell theorem: the LCG increment must be odd (coprime to 2^64) to guarantee a full 2^64 period.
        // Shifting left by 1 and setting bit 0 maps every initInc value onto a unique odd stream constant.
        _inc = (initInc << 1) | 1UL;
        NextUInt32();
        _state += initState;
        NextUInt32();
    }

    /// <summary>
    /// Advances the internal 64-bit LCG state and returns a uniformly distributed 32-bit unsigned integer.
    /// Executes entirely in CPU registers with zero heap or cache footprint.
    /// </summary>
    /// <returns>A uniformly distributed 32-bit unsigned integer.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public uint NextUInt32()
    {
        var oldState = _state;

        // 1. Advance the 64-bit Linear Congruential Generator using O'Neill's canonical multiplier.
        _state = (oldState * LcgMultiplier) + _inc;

        // 2. Permute oldState (rather than the new _state) via XSH-RR (XorShift High, Random Rotate).
        //    Operating on oldState breaks the data dependency on the 64-bit multiplication above,
        //    allowing out-of-order CPU execution units to run the state advance and output permutation in parallel.
        var xorshifted = (uint)(((oldState >> XorShiftBits) ^ oldState) >> PermutationShiftBits);
        var rot = (int)(oldState >> RotationShiftBits);

        return BitOperations.RotateRight(xorshifted, rot);
    }

    /// <summary>
    /// Generates an unbiased random integer strictly in the interval <c>[0, bound)</c> using
    /// Daniel Lemire's nearly divisionless algorithm (2019).
    /// </summary>
    /// <param name="bound">The exclusive upper bound (must be strictly greater than <c>0</c>).</param>
    /// <returns>A uniformly distributed integer in <c>[0 .. bound - 1]</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="bound"/> is less than or equal to <c>0</c>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int NextBounded(int bound)
    {
        if (bound <= 0)
            ThrowInvalidBound();

        var uBound = (uint)bound;
        var m = (ulong)NextUInt32() * uBound;
        var leftover = (uint)m;

        // Fast path (~99.99% of calls for typical bounds): no division/modulo instruction is executed.
        // Slow path: reject the rare biased remainder zone of size (2^32 mod bound) to guarantee exact uniformity.
        if (leftover < uBound)
        {
            var threshold = (uint)(-bound) % uBound;
            while (leftover < threshold)
            {
                m = (ulong)NextUInt32() * uBound;
                leftover = (uint)m;
            }
        }

        return (int)(m >> 32);
    }

    /// <summary>
    /// Extracted cold throw helper so the JIT compiler aggressively inlines <see cref="NextBounded"/>.
    /// </summary>
    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowInvalidBound() =>
        throw new ArgumentOutOfRangeException(nameof(NextBounded), "The exclusive upper bound must be positive.");
}