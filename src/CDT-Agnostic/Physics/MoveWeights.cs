using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using CDT_Agnostic.Physics.Components;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.Physics;

/// <summary>
/// A 64-byte cache-line-aligned Look-Up Table (LUT) precomputing the Boltzmann weights
/// <c>exp(-ΔS)</c> in unsigned <c>Q4.28</c> fixed-point format across a symmetric volume window
/// <c>[-VolumeRadius .. +VolumeRadius]</c> around <see cref="VolumeTarget"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct MoveWeights
{
    /// <summary>
    /// Fractional bit precision (28 bits) for unsigned <c>Q4.28</c> fixed-point representation.
    /// </summary>
    public const int Q_SHIFT = 28;

    /// <summary>
    /// The <c>Q4.28</c> fixed-point representation of <c>1.0</c> (<c>2^28 = 268,435,456</c>).
    /// </summary>
    public const uint Q_ONE = 1u << Q_SHIFT;

    private const int ElementsPerCacheLine = CacheLineBytes / sizeof(uint);
    private const int AlignmentMask = ElementsPerCacheLine - 1;
    private const double MaxQ28Weight = 15.99;

    /// <summary>
    /// The target top-dimensional simplex volume (<c>V_target</c>) at the center of the lookup table.
    /// </summary>
    public int VolumeTarget;

    /// <summary>
    /// The half-width radius of the precomputed volume window (padded to a multiple of 16 elements).
    /// </summary>
    public int VolumeRadius;

    /// <summary>
    /// The cached bare Regge action differential (<c>ΔS_Regge</c>) used by the slow-path fallback.
    /// </summary>
    public double ReggeDelta;

    /// <summary>
    /// The cached base volume-fixing term <c>ε(ΔV)^2</c> used by the slow-path fallback.
    /// </summary>
    public double BaseVolumeS;

    /// <summary>
    /// The cached linear volume-drift slope <c>2εΔV</c> used by the slow-path fallback.
    /// </summary>
    public double DriftFactor;

    private uint* _expDeltaS;

    /// <summary>
    /// Allocates an unmanaged, 64-byte aligned <see cref="MoveWeights"/> lookup table for the specified <paramref name="volumeRadius"/>.
    /// </summary>
    /// <param name="volumeRadius">The desired volume excursion radius around the target volume.</param>
    /// <returns>An initialized <see cref="MoveWeights"/> struct owning the aligned native buffer.</returns>
    public static MoveWeights Allocate(int volumeRadius)
    {
        // Pad the radius up to the nearest multiple of 16 (64 bytes) for L1 cache-line alignment.
        volumeRadius = (volumeRadius + AlignmentMask) & ~AlignmentMask;

        MoveWeights lut = default;
        lut.VolumeRadius = volumeRadius;

        // A radius of R covers integer volume drifts in [-R .. +R], requiring (2R + 1) elements.
        var elements = (nuint)((volumeRadius * 2) + 1);
        var bytesToAllocate = elements * sizeof(uint);

        // Allocate explicitly on a 64-byte boundary to align with CPU L1 cache lines.
        lut._expDeltaS = (uint*)NativeMemory.AlignedAlloc(bytesToAllocate, alignment: CacheLineBytes);

        return lut;
    }

    /// <summary>
    /// Recomputes all <c>Q4.28</c> fixed-point Boltzmann weights across <c>[-VolumeRadius .. +VolumeRadius]</c>
    /// for the specified coupling parameters.
    /// </summary>
    /// <param name="reggeDelta">The move's bare Regge action change (<c>ΔS_Regge</c>).</param>
    /// <param name="volumeTarget">The target top-dimensional simplex volume (<c>V_target</c>).</param>
    /// <param name="volumeDeltaForMove">The discrete simplex volume change (<c>ΔV</c>) of the move.</param>
    /// <param name="epsilon">The quadratic volume-fixing coupling constant (<c>ε</c>).</param>
    public void BuildWeights(double reggeDelta, int volumeTarget, int volumeDeltaForMove, double epsilon)
    {
        VolumeTarget = volumeTarget;
        ReggeDelta = reggeDelta;

        (BaseVolumeS, DriftFactor) = VolumeFixing.CalculateConstants(epsilon, volumeDeltaForMove);

        for (var diff = -VolumeRadius; diff <= VolumeRadius; diff++)
        {
            var e = BoltzmannWeight.Evaluate(ReggeDelta, BaseVolumeS, DriftFactor, diff);

            // Clamp to the unsigned Q4.28 representable range [0.0, 15.99] to prevent 32-bit uint overflow.
            // In Q4.28, 4 integer bits represent [0 .. 15], with uint.MaxValue / Q_ONE ≈ 15.999999996.
            if (double.IsNaN(e) || e < 0)
                e = 0;

            if (e > MaxQ28Weight)
                e = MaxQ28Weight;

            _expDeltaS[diff + VolumeRadius] = (uint)(e * Q_ONE);
        }
    }

    /// <summary>
    /// Reads the precomputed <c>Q4.28</c> Boltzmann weight <c>exp(-ΔS)</c> for the specified volume drift.
    /// Marked <see langword="readonly"/> so calling via <c>in MoveWeights</c> never triggers a defensive struct copy.
    /// </summary>
    /// <param name="diff">The signed volume drift (<c>V_current - V_target</c>) within <c>[-VolumeRadius .. +VolumeRadius]</c>.</param>
    /// <returns>The <c>Q4.28</c> fixed-point Boltzmann weight.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly uint GetExpDeltaS(int diff) => _expDeltaS[diff + VolumeRadius];

    /// <summary>
    /// Frees the 64-byte aligned native lookup table buffer.
    /// </summary>
    public void Dispose()
    {
        if (_expDeltaS != null)
        {
            NativeMemory.AlignedFree(_expDeltaS);
            _expDeltaS = null;
        }
    }
}