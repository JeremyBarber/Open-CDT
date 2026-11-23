using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Identifiers;

/// <summary>
/// Represents a strongly-typed local facet (codimension-1 boundary face / neighbor) index inside a single <c>d</c>-simplex
/// (e.g., <c>0 .. 3</c> in a 3D tetrahedron, or <c>0 .. 4</c> in a 4D pentachoron).
/// By convention, facet <c>i</c> is always the boundary face opposite local vertex <c>i</c>.
/// </summary>
/// <param name="value">The zero-based local facet index within the simplex.</param>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct LocalFacetSlot(int value) : IEquatable<LocalFacetSlot>
{
    /// <summary>
    /// The underlying zero-based local facet slot index.
    /// </summary>
    public readonly int Value = value;

    /// <summary>
    /// Gets the sentinel value (<c>-1</c>) representing an uninitialized or invalid local facet slot.
    /// </summary>
    public static LocalFacetSlot Invalid => new(InvalidData);

    /// <summary>
    /// Determines whether two <see cref="LocalFacetSlot"/> instances refer to the same local facet index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(LocalFacetSlot left, LocalFacetSlot right) => left.Value == right.Value;

    /// <summary>
    /// Determines whether two <see cref="LocalFacetSlot"/> instances refer to different local facet indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(LocalFacetSlot left, LocalFacetSlot right) => left.Value != right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(LocalFacetSlot other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LocalFacetSlot other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}