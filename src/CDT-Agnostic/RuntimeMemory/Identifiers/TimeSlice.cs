using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Identifiers;

/// <summary>
/// Represents the discrete proper-time coordinate (<c>t in [0 .. T - 1]</c>) of a vertex in the causal foliation.
/// </summary>
/// <param name="value">The discrete proper-time slice index.</param>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct TimeSlice(int value) : IEquatable<TimeSlice>
{
    /// <summary>
    /// The underlying discrete proper-time slice index.
    /// </summary>
    public readonly int Value = value;

    /// <summary>
    /// Gets the sentinel value (<c>-1</c>) representing an uninitialized or invalid time slice.
    /// </summary>
    public static TimeSlice Invalid => new(InvalidData);

    /// <summary>
    /// Determines whether two <see cref="TimeSlice"/> instances refer to the same proper-time slice.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(TimeSlice left, TimeSlice right) => left.Value == right.Value;

    /// <summary>
    /// Determines whether two <see cref="TimeSlice"/> instances refer to different proper-time slices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(TimeSlice left, TimeSlice right) => left.Value != right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(TimeSlice other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is TimeSlice other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}