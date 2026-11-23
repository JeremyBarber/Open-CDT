using CDT_Agnostic.RuntimeMemory.Native;

namespace CDT_Agnostic.RuntimeMemory.Stores;

public unsafe partial struct TypeStore
{
    /// <summary>
    /// Writes a bit-exact binary snapshot of this type subset's count, dense active list, and inverse position array to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The destination checkpoint stream.</param>
    /// <param name="maxIds">The maximum simplex slot capacity (<c>MaxSimplices</c>) of the arena.</param>
    public readonly void WriteCheckpoint(Stream stream, uint maxIds)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (_active == null || _position == null)
            throw new InvalidOperationException("Cannot checkpoint an uninitialized or disposed TypeStore.");
        if (_count < 0 || (uint)_count > maxIds)
            throw new InvalidOperationException("TypeStore count invariant violated.");

        ArenaBinaryIO.WriteValue(stream, in _count);
        ArenaBinaryIO.WriteBlit(stream, _active, maxIds);
        ArenaBinaryIO.WriteBlit(stream, _position, maxIds);
    }

    /// <summary>
    /// Restores a bit-exact binary snapshot of this type subset's count, dense active list, and inverse position array from <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The source checkpoint stream.</param>
    /// <param name="maxIds">The maximum simplex slot capacity (<c>MaxSimplices</c>) of the arena.</param>
    public void ReadCheckpoint(Stream stream, uint maxIds)
    {
        ArgumentNullException.ThrowIfNull(stream);

        if (_active == null || _position == null)
            throw new InvalidOperationException("Cannot restore into an uninitialized or disposed TypeStore.");

        var count = ArenaBinaryIO.ReadValue<int>(stream);
        if (count < 0 || (uint)count > maxIds)
        {
            throw new InvalidDataException(
                $"Corrupted TypeStore count ({count}) for capacity ({maxIds}).");
        }

        _count = count;
        ArenaBinaryIO.ReadBlit(stream, _active, maxIds);
        ArenaBinaryIO.ReadBlit(stream, _position, maxIds);
    }
}