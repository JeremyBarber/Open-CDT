using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;

namespace CDT_Agnostic;

/// <summary>
/// Dimension-agnostic contract for the managed "Air-Gap" simulation harness that owns
/// an unmanaged spacetime manifold arena, thermodynamic lookup tables, and PRNG state.
/// </summary>
public interface ISimulationHarness : IDisposable
{
    /// <summary>
    /// The maximum number of simplices that this simulation is capable of supporting
    /// </summary>
    uint MaxSimplices { get; }

    /// <summary>
    /// Allocates and seeds a new unmanaged spacetime universe on the native heap.
    /// </summary>
    /// <param name="harnessId">The unique worker/replica stream identifier used to select an independent <c>PCG32</c> sequence.</param>
    /// <param name="physics">The initial bare coupling and volume-fixing parameters.</param>
    /// <param name="seed">The initial topological seeding parameters (proper-time slices, subdivision, and RNG seed).</param>
    /// <returns>An initialized <see cref="ISimulationHarness"/> ready for Monte Carlo sweeps.</returns>
    static abstract ISimulationHarness Create(
        uint harnessId,
        PhysicsParameters physics,
        SeedParameters seed);

    /// <summary>
    /// Allocates an unmanaged arena and restores a bit-exact universe snapshot from a binary checkpoint stream.
    /// </summary>
    /// <param name="harnessId">The unique worker/replica stream identifier.</param>
    /// <param name="stream">The binary stream positioned at the start of the checkpoint payload.</param>
    /// <param name="physics">The thermodynamic coupling parameters to apply to the restored manifold.</param>
    /// <param name="reseedRng">
    /// Optional RNG seed override. When <see langword="null"/>, the exact serialized <c>PCG32</c> state is preserved
    /// for bit-identical trajectory continuation; when specified, re-seeds the generator on stream <paramref name="harnessId"/>.
    /// </param>
    /// <returns>An initialized <see cref="ISimulationHarness"/> restored to the checkpointed state.</returns>
    static abstract ISimulationHarness LoadCheckpoint(
        uint harnessId,
        Stream stream,
        PhysicsParameters physics,
        ulong? reseedRng = null);

    /// <summary>
    /// Writes a bit-exact binary snapshot of the unmanaged manifold arena and PRNG state to <paramref name="stream"/>.
    /// </summary>
    /// <param name="stream">The destination stream to write the checkpoint into.</param>
    void SaveCheckpoint(Stream stream);

    /// <summary>
    /// Executes a burst of <paramref name="sweepsToRun"/> Markov Chain Monte Carlo sweeps inside the unmanaged engine.
    /// </summary>
    /// <param name="sweepsToRun">The number of sweeps to execute in this micro-burst.</param>
    /// <param name="stats">Optional pinned <see cref="Volume"/> trace buffer populated directly by the unmanaged engine after each sweep.</param>
    void Run(int sweepsToRun, Volume? stats = null);

    /// <summary>
    /// Evaluates a read-only geometric observable across the current manifold state without mutating the arena or advancing the simulation RNG.
    /// </summary>
    /// <typeparam name="TResult">The snapshot result type produced by the observable.</typeparam>
    /// <param name="observable">The dimension-agnostic observable to measure.</param>
    /// <returns>The computed measurement result snapshot.</returns>
    TResult Measure<TResult>(IManifoldObservable<TResult> observable);
}