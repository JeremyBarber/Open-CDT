using CDT_Agnostic.RuntimeMemory.Native;

namespace CDT_Agnostic.RuntimeMemory.Stores;

internal unsafe partial struct SlotStore
{
    /// <summary>
    /// Gets the total slot capacity of this store using the slot conservation law (<c>Count + FreeCount == maxSlots</c>).
    /// </summary>
    internal readonly uint Capacity => (uint)(_activeCount + _freeHead);

    /// <summary>
    /// Writes a bit-exact binary snapshot of the active list, inverse index, and LIFO free-stack to <paramref name="stream"/>.
    /// </summary>
    internal readonly void WriteCheckpoint(Stream stream, uint expectedCapacity)
    {
        if (_active == null || _activeIndex == null || _freeStack == null)
            throw new InvalidOperationException("Cannot checkpoint an uninitialized or disposed SlotStore.");
        if (_activeCount < 0 || _freeHead < 0 || (ulong)(uint)_activeCount + (ulong)(uint)_freeHead != expectedCapacity)
            throw new InvalidOperationException("SlotStore capacity conservation invariant violated.");

        ArenaBinaryIO.WriteValue(stream, in _activeCount);
        ArenaBinaryIO.WriteValue(stream, in _freeHead);
        ArenaBinaryIO.WriteBlit(stream, _active, expectedCapacity);
        ArenaBinaryIO.WriteBlit(stream, _activeIndex, expectedCapacity);
        ArenaBinaryIO.WriteBlit(stream, _freeStack, expectedCapacity);
    }

    /// <summary>
    /// Restores a bit-exact binary snapshot of the active list, inverse index, and LIFO free-stack from <paramref name="stream"/>.
    /// </summary>
    internal void ReadCheckpoint(Stream stream, uint expectedCapacity)
    {
        if (_active == null || _activeIndex == null || _freeStack == null)
            throw new InvalidOperationException("Cannot restore into an uninitialized or disposed SlotStore.");

        var activeCount = ArenaBinaryIO.ReadValue<int>(stream);
        var freeHead = ArenaBinaryIO.ReadValue<int>(stream);

        if (activeCount < 0 || freeHead < 0 || (ulong)(uint)activeCount + (ulong)(uint)freeHead != expectedCapacity)
        {
            throw new InvalidDataException(
                $"Corrupted SlotStore header in checkpoint: active ({activeCount}) + free ({freeHead}) != capacity ({expectedCapacity}).");
        }

        _activeCount = activeCount;
        _freeHead = freeHead;

        ArenaBinaryIO.ReadBlit(stream, _active, expectedCapacity);
        ArenaBinaryIO.ReadBlit(stream, _activeIndex, expectedCapacity);
        ArenaBinaryIO.ReadBlit(stream, _freeStack, expectedCapacity);
    }
}