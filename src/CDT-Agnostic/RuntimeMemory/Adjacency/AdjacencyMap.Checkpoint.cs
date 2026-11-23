using CDT_Agnostic.RuntimeMemory.Native;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_Agnostic.RuntimeMemory.Adjacency;

internal unsafe partial struct AdjacencyMap
{
    /// <summary>
    /// Recovers the total <see cref="AdjacencyBlock"/> pool capacity (<c>maxBlocks</c>) by summing
    /// the unallocated blocks on the free-stack with all active blocks chained across <c>[0 .. maxEntities - 1]</c>.
    /// </summary>
    internal readonly uint ComputeTotalBlocks(uint maxEntities)
    {
        if (_pool == null || _entityHeads == null || _entityTails == null || _freeStack == null || _freeHead < 0)
            throw new InvalidOperationException("AdjacencyMap is uninitialized or disposed.");

        var allocatedBlocks = 0;
        for (uint e = 0; e < maxEntities; e++)
        {
            var idx = _entityHeads[e];
            while (idx != InvalidData)
            {
                allocatedBlocks++;
                idx = _pool[idx].Next;
            }
        }

        return (uint)(_freeHead + allocatedBlocks);
    }

    /// <summary>
    /// Writes a bit-exact binary snapshot of the adjacency block pool, head/tail lookup tables, and free-stack to <paramref name="stream"/>.
    /// </summary>
    internal readonly void WriteCheckpoint(Stream stream, uint maxBlocks, uint maxEntities)
    {
        if (_pool == null || _entityHeads == null || _entityTails == null || _freeStack == null)
            throw new InvalidOperationException("Cannot checkpoint an uninitialized or disposed AdjacencyMap.");
        if (_freeHead < 0 || (uint)_freeHead > maxBlocks)
            throw new InvalidOperationException("AdjacencyMap freeHead invariant violated.");

        ArenaBinaryIO.WriteValue(stream, in _freeHead);
        ArenaBinaryIO.WriteBlit(stream, _pool, maxBlocks);
        ArenaBinaryIO.WriteBlit(stream, _entityHeads, maxEntities);
        ArenaBinaryIO.WriteBlit(stream, _entityTails, maxEntities);
        ArenaBinaryIO.WriteBlit(stream, _freeStack, maxBlocks);
    }

    /// <summary>
    /// Restores a bit-exact binary snapshot of the adjacency block pool, head/tail lookup tables, and free-stack from <paramref name="stream"/>.
    /// </summary>
    internal void ReadCheckpoint(Stream stream, uint maxBlocks, uint maxEntities)
    {
        if (_pool == null || _entityHeads == null || _entityTails == null || _freeStack == null)
            throw new InvalidOperationException("Cannot restore into an uninitialized or disposed AdjacencyMap.");

        var freeHead = ArenaBinaryIO.ReadValue<int>(stream);
        if (freeHead < 0 || (uint)freeHead > maxBlocks)
        {
            throw new InvalidDataException(
                $"Corrupted AdjacencyMap freeHead ({freeHead}) for block capacity ({maxBlocks}).");
        }

        _freeHead = freeHead;
        ArenaBinaryIO.ReadBlit(stream, _pool, maxBlocks);
        ArenaBinaryIO.ReadBlit(stream, _entityHeads, maxEntities);
        ArenaBinaryIO.ReadBlit(stream, _entityTails, maxEntities);
        ArenaBinaryIO.ReadBlit(stream, _freeStack, maxBlocks);
    }
}