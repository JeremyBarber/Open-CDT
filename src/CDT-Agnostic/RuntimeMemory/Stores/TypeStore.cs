using CDT_Agnostic.RandomNumberGeneration;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;

using static CDT_Agnostic.RuntimeMemory.Native.Allocation;
using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Stores;

/// <summary>
/// Unmanaged <c>O(1)</c> active-set tracker for a typed subset of simplex IDs (e.g., <c>(3,1)</c>, <c>(2,2)</c>, or <c>(1,3)</c> simplices).
/// Uses a Briggs-Torczon sparse-set (<c>_active</c> + <c>_position</c>) to support <c>O(1)</c> insertion,
/// <c>O(1)</c> swap-with-last removal, and <c>O(1)</c> uniform random sampling.
/// Does not mint new IDs—tracks externally allocated <see cref="SimplexId"/> values from <c>SlotStore</c>.
/// </summary>
public unsafe partial struct TypeStore
{
    private readonly int* _active;
    private readonly int* _position;

    private int _count;

    /// <summary>
    /// Gets the number of simplices currently registered in this type store.
    /// </summary>
    public readonly int Count => _count;

    /// <summary>
    /// Allocates the dense active list and inverse position map for up to <paramref name="maxIds"/> simplices.
    /// </summary>
    /// <param name="maxIds">The maximum simplex slot capacity (<c>MaxSimplices</c>) of the arena.</param>
    public TypeStore(uint maxIds)
    {
        _active = Allocate<int>(maxIds);
        _position = Allocate<int>(maxIds);

        _count = 0;

        // Initialize all inverse positions to InvalidData (-1 via 0xFF byte fill).
        Fill(_position, maxIds);
    }

    /// <summary>
    /// Draws a uniformly distributed active <see cref="SimplexId"/> from this type subset in strictly <c>O(1)</c> time.
    /// </summary>
    /// <param name="rng">Reference to the unmanaged <see cref="Pcg32"/> generator.</param>
    /// <returns>A randomly selected active <see cref="SimplexId"/> of this type.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly SimplexId SelectRandom(ref Pcg32 rng) =>
        new(_active[rng.NextBounded(_count)]);

    /// <summary>
    /// Gets a zero-allocation read-only span over the densely packed active simplex IDs in <c>[0 .. Count - 1]</c>.
    /// </summary>
    public readonly ReadOnlySpan<int> IdsOfType
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => new(_active, _count);
    }

    /// <summary>
    /// Releases all unmanaged buffers owned by this instance.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly void Dispose()
    {
        SafeFree(_active);
        SafeFree(_position);
    }

    /// <summary>
    /// Registers <paramref name="id"/> at the end of the dense active list in <c>O(1)</c> time.
    /// </summary>
    /// <param name="id">The simplex ID to add to this type subset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(SimplexId id)
    {
        _position[id.Value] = _count;
        _active[_count++] = id.Value;
    }

    /// <summary>
    /// Removes <paramref name="id"/> from the dense active list via <c>O(1)</c> swap-with-last
    /// and updates the displaced element's inverse position.
    /// </summary>
    /// <param name="id">The simplex ID to remove from this type subset.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(SimplexId id)
    {
        var pos = _position[id.Value];
        var last = _active[--_count];

        _active[pos] = last;
        _position[last] = pos;

        // Must be assigned AFTER _position[last] = pos so that removing the tail element
        // (where id.Value == last) still marks _position[id.Value] as InvalidData (-1).
        _position[id.Value] = InvalidData;
    }
}