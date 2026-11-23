using CDT_Agnostic.RuntimeMemory.Native;

namespace CDT_Agnostic.RandomNumberGeneration;

public unsafe partial struct Pcg32
{
    /// <summary>
    /// Writes the 16-byte internal state (<c>_state</c> and <c>_inc</c>) of the generator to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The destination checkpoint stream.</param>
    public readonly void WriteCheckpoint(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        ArenaBinaryIO.WriteValue(stream, in _state);
        ArenaBinaryIO.WriteValue(stream, in _inc);
    }

    /// <summary>
    /// Restores the 16-byte internal state of the generator from <paramref name="stream"/>,
    /// validating the Hull-Dobell odd-increment invariant before mutating state.
    /// </summary>
    /// <param name="stream">The source checkpoint stream.</param>
    /// <exception cref="InvalidDataException">Thrown when the serialized stream increment is even.</exception>
    public void ReadCheckpoint(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var state = ArenaBinaryIO.ReadValue<ulong>(stream);
        var inc = ArenaBinaryIO.ReadValue<ulong>(stream);

        if ((inc & 1UL) == 0UL)
            throw new InvalidDataException("Corrupted PCG32 state in checkpoint: stream increment must be odd.");

        _state = state;
        _inc = inc;
    }
}