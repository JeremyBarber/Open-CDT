using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Identifiers;

/// <summary>
/// Represents a strongly-typed local edge slot index inside a single <c>d</c>-simplex
/// (e.g., <c>0 .. 5</c> in a 3D tetrahedron, or <c>0 .. 9</c> in a 4D pentachoron).
/// Compiles down to a single 32-bit register with zero runtime overhead.
/// </summary>
/// <param name="value">The zero-based local edge index within the simplex.</param>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct LocalEdgeSlot(int value) : IEquatable<LocalEdgeSlot>
{
    /// <summary>
    /// The underlying zero-based local edge slot index.
    /// </summary>
    public readonly int Value = value;

    /// <summary>
    /// Gets the sentinel value (<c>-1</c>) representing an uninitialized or invalid local edge slot.
    /// </summary>
    public static LocalEdgeSlot Invalid => new(InvalidData);

    /// <summary>
    /// Determines whether two <see cref="LocalEdgeSlot"/> instances refer to the same local edge index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(LocalEdgeSlot left, LocalEdgeSlot right) => left.Value == right.Value;

    /// <summary>
    /// Determines whether two <see cref="LocalEdgeSlot"/> instances refer to different local edge indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(LocalEdgeSlot left, LocalEdgeSlot right) => left.Value != right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(LocalEdgeSlot other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LocalEdgeSlot other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}