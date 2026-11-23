using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Identifiers;

/// <summary>
/// Represents the global arena slot identifier of a vertex (0-simplex) in the manifold.
/// </summary>
/// <param name="value">The zero-based arena slot index of the vertex.</param>
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
public readonly struct VertexId(int value) : IEquatable<VertexId>
{
    /// <summary>
    /// The underlying arena slot index of the vertex.
    /// </summary>
    public readonly int Value = value;

    /// <summary>
    /// Gets the sentinel value (<c>-1</c>) representing an unallocated or invalid vertex slot.
    /// </summary>
    public static VertexId Invalid => new(InvalidData);

    /// <summary>
    /// Determines whether two <see cref="VertexId"/> instances refer to the same arena slot.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(VertexId left, VertexId right) => left.Value == right.Value;

    /// <summary>
    /// Determines whether two <see cref="VertexId"/> instances refer to different arena slots.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(VertexId left, VertexId right) => left.Value != right.Value;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(VertexId other) => Value == other.Value;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is VertexId other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value;

    /// <inheritdoc/>
    public override string ToString() => Value.ToString();
}