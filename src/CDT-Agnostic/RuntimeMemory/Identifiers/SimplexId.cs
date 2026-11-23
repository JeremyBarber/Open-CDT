using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Identifiers;

/// <summary>
/// Represents the global arena slot identifier of a top-dimensional simplex in the manifold.
/// </summary>
/// <param name="value">The zero-based arena slot index of the simplex.</param>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct SimplexId(int value) : IEquatable<SimplexId>
{
    /// <summary>
    /// The underlying arena slot index of the simplex.
    /// </summary>
    public readonly int Value = value;

    /// <summary>
    /// Gets the sentinel value (<c>-1</c>) representing an unlinked or invalid simplex slot.
    /// </summary>
    public static SimplexId Invalid => new(InvalidData);

    /// <summary>
    /// Determines whether two <see cref="SimplexId"/> instances refer to the same arena slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(SimplexId left, SimplexId right) => left.Value == right.Value;

    /// <summary>
    /// Determines whether two <see cref="SimplexId"/> instances refer to different arena slots.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(SimplexId left, SimplexId right) => left.Value != right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(SimplexId other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is SimplexId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}