using CDT_Agnostic.RuntimeMemory.Native;

namespace CDT_2plus1.Engine.State.Mesh;

internal unsafe partial struct Manifold
{
    private const uint FrameMagic = 0x44464E4D;              // ASCII "MNFD" (little-endian)
    private const ulong FrameEndSentinel = 0x444E455F44464E4D; // ASCII "MNFD_END" (little-endian)

    /// <summary>
    /// Writes a framed, bit-exact binary snapshot of the entire <see cref="Manifold"/> arena to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The destination checkpoint stream.</param>
    internal readonly void WriteCheckpoint(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (SimplexPool == null || SimplexTypes == null || VertexTimes == null)
            throw new InvalidOperationException("Cannot checkpoint an uninitialized or disposed Manifold.");

        (var maxV, var maxS, var maxB) = Memory.GetCapacities();

        var magic = FrameMagic;
        ArenaBinaryIO.WriteValue(stream, in magic);
        ArenaBinaryIO.WriteValue(stream, in maxV);
        ArenaBinaryIO.WriteValue(stream, in maxS);
        ArenaBinaryIO.WriteValue(stream, in maxB);

        ArenaBinaryIO.WriteBlit(stream, SimplexPool, maxS);
        ArenaBinaryIO.WriteBlit(stream, SimplexTypes, maxS);
        ArenaBinaryIO.WriteBlit(stream, VertexTimes, maxV);

        Memory.WriteCheckpoint(stream, maxV, maxS, maxB);
        TypeStore31.WriteCheckpoint(stream, maxS);
        TypeStore22.WriteCheckpoint(stream, maxS);
        TypeStore13.WriteCheckpoint(stream, maxS);

        var endSentinel = FrameEndSentinel;
        ArenaBinaryIO.WriteValue(stream, in endSentinel);
    }

    /// <summary>
    /// Allocates a new <see cref="Manifold"/> on the native heap and restores its exact state from <paramref name="stream"/>.
    /// Validates frame magic, non-zero arena capacities, trailing end-sentinel, and simplex count conservation.
    /// </summary>
    /// <param name="stream">The source checkpoint stream.</param>
    /// <returns>An unmanaged pointer to the restored <see cref="Manifold"/>.</returns>
    internal static Manifold* ReadCheckpoint(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var magic = ArenaBinaryIO.ReadValue<uint>(stream);
        if (magic != FrameMagic)
            throw new InvalidDataException("Invalid Manifold frame magic in checkpoint stream.");

        var maxV = ArenaBinaryIO.ReadValue<uint>(stream);
        var maxS = ArenaBinaryIO.ReadValue<uint>(stream);
        var maxB = ArenaBinaryIO.ReadValue<uint>(stream);

        if (maxV == 0 || maxS == 0 || maxB == 0)
            throw new InvalidDataException("Checkpoint specifies zero capacity for Manifold arenas.");

        var state = Create(maxV, maxS, maxB);
        try
        {
            ArenaBinaryIO.ReadBlit(stream, state->SimplexPool, maxS);
            ArenaBinaryIO.ReadBlit(stream, state->SimplexTypes, maxS);
            ArenaBinaryIO.ReadBlit(stream, state->VertexTimes, maxV);

            state->Memory.ReadCheckpoint(stream, maxV, maxS, maxB);
            state->TypeStore31.ReadCheckpoint(stream, maxS);
            state->TypeStore22.ReadCheckpoint(stream, maxS);
            state->TypeStore13.ReadCheckpoint(stream, maxS);

            var endSentinel = ArenaBinaryIO.ReadValue<ulong>(stream);
            if (endSentinel != FrameEndSentinel)
            {
                throw new InvalidDataException(
                    "Manifold checkpoint frame failed end-sentinel verification (truncated or misaligned stream).");
            }

            var totalTyped = state->TypeStore31.Count + state->TypeStore22.Count + state->TypeStore13.Count;
            if (totalTyped != state->Memory.SimplexCount)
            {
                throw new InvalidDataException(
                    $"Manifold checkpoint failed simplex conservation check: N31+N22+N13 ({totalTyped}) != SimplexCount ({state->Memory.SimplexCount}).");
            }

            return state;
        }
        catch
        {
            Destroy(ref state);
            throw;
        }
    }
}