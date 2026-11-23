using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Identifiers;

/// <summary>
/// Represents a strongly-typed local vertex slot index inside a single <c>d</c>-simplex
/// (e.g., <c>0 .. 3</c> in a 3D tetrahedron, or <c>0 .. 4</c> in a 4D pentachoron).
/// By convention, local vertex <c>i</c> lies opposite local facet <c>i</c>.
/// </summary>
/// <param name="value">The zero-based local vertex index within the simplex.</param>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct LocalVertexSlot(int value) : IEquatable<LocalVertexSlot>
{
    /// <summary>
    /// The underlying zero-based local vertex slot index.
    /// </summary>
    public readonly int Value = value;

    /// <summary>
    /// Gets the sentinel value (<c>-1</c>) representing an uninitialized or invalid local vertex slot.
    /// </summary>
    public static LocalVertexSlot Invalid => new(InvalidData);

    /// <summary>
    /// Determines whether two <see cref="LocalVertexSlot"/> instances refer to the same local vertex index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(LocalVertexSlot left, LocalVertexSlot right) => left.Value == right.Value;

    /// <summary>
    /// Determines whether two <see cref="LocalVertexSlot"/> instances refer to different local vertex indices.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(LocalVertexSlot left, LocalVertexSlot right) => left.Value != right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(LocalVertexSlot other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is LocalVertexSlot other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}