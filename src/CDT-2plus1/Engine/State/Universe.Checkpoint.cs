using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.State.Physics;
using CDT_Agnostic.RandomNumberGeneration;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_Agnostic.RuntimeMemory.Native.Allocation;

namespace CDT_2plus1.Engine.State;

internal sealed unsafe partial class Universe
{
    /// <summary>
    /// Restores a complete <see cref="Universe"/> from a binary checkpoint stream, reconstructing its
    /// <see cref="Pcg32"/> state, <see cref="Manifold"/> arena, and <see cref="Dynamics"/> lookup tables.
    /// </summary>
    /// <param name="stream">The readable binary stream positioned at the start of a universe checkpoint.</param>
    /// <param name="constraints">The physical coupling parameters to hydrate into the <see cref="Dynamics"/> tables.</param>
    /// <param name="reseedRng">Optional RNG seed override; when <see langword="null"/>, preserves the checkpointed PRNG state.</param>
    /// <param name="rngSequence">The stream sequence identifier used when <paramref name="reseedRng"/> is specified.</param>
    /// <returns>The restored <see cref="Universe"/> instance.</returns>
    public static Universe LoadCheckpoint(
        Stream stream,
        in Constraints constraints,
        ulong? reseedRng = null,
        ulong rngSequence = 0)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead)
            throw new ArgumentException("Checkpoint stream must be readable.", nameof(stream));

        var rng = Allocate<Pcg32>(1);
        Manifold* manifold = null;
        Dynamics* dynamics = null;

        try
        {
            rng->ReadCheckpoint(stream);
            if (reseedRng is ulong seed)
                rng->Seed(seed, rngSequence);

            manifold = ReadCheckpoint(stream);
            dynamics = Dynamics.Create(in constraints);

            return new Universe(manifold, dynamics, rng);
        }
        catch
        {
            Destroy(ref manifold);
            Dynamics.Destroy(ref dynamics);
            Pcg32.Destroy(rng);
            throw;
        }
    }

    /// <summary>
    /// Writes a bit-exact binary snapshot of the <see cref="Pcg32"/> state and <see cref="Manifold"/> arena to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The writable destination stream.</param>
    public void WriteCheckpoint(Stream stream)
    {
        ThrowIfDisposed();
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanWrite)
            throw new ArgumentException("Checkpoint stream must be writable.", nameof(stream));

        _rng->WriteCheckpoint(stream);
        _manifold->WriteCheckpoint(stream);
    }
}