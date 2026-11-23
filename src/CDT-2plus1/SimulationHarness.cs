using CDT_2plus1.Engine;
using CDT_2plus1.Engine.State;
using CDT_2plus1.Engine.State.Physics;
using CDT_Agnostic;
using CDT_Agnostic.Accessors.Implementations;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1;

/// <summary>
/// Represents a single, thread-isolated instance of a 2+1D CDT simulation.
/// Acts as the managed "Air-Gap" bridge to the unmanaged, zero-allocation physics engine,
/// exposing a safe API to orchestration workflows while hiding all raw pointer complexity.
/// </summary>
public sealed partial class SimulationHarness : ISimulationHarness
{
    // =====================================================================================
    // STATE & METADATA
    // =====================================================================================

    /// <summary>
    /// The unique identifier for this simulation instance and its RNG stream sequence.
    /// </summary>
    public readonly uint HarnessId;

    /// <summary>
    /// The maximum number of simplices that can be supported by this simulation
    /// </summary>
    public uint MaxSimplices => _universe.MaxSimplices;

    private readonly Universe _universe;

    // =====================================================================================
    // THE NATIVE TRAMPOLINE
    // =====================================================================================
    // Caches a raw C-ABI function pointer to the unmanaged engine core, bypassing
    // the .NET P/Invoke marshaller on sweep chunk invocations.
    private static readonly unsafe delegate* unmanaged<RandomAccessor, MutatingAccessor, PhysicsAccessor, Volume.Unmanaged*, int, void> _executeSweepBurst =
        &SimulationEngine.ExecuteSweepBurst;

    // =====================================================================================
    // LIFECYCLE
    // =====================================================================================

    /// <summary>
    /// Allocates unmanaged memory for a new universe and bootstraps the initial icosahedral triangulation.
    /// </summary>
    /// <param name="harnessId">Unique identifier for this simulation instance and RNG stream sequence.</param>
    /// <param name="physics">The initial coupling constants (<c>k_0</c>, <c>k_3</c>, <c>Δ</c>, <c>ε</c>) and target volume.</param>
    /// <param name="seed">Parameters for spacetime foliation and random seed generation.</param>
    /// <returns>An initialized <see cref="ISimulationHarness"/> instance.</returns>
    public static ISimulationHarness Create(
        uint harnessId,
        PhysicsParameters physics,
        SeedParameters seed) =>
        new SimulationHarness(harnessId, physics, seed);

    private SimulationHarness(
        uint harnessId,
        PhysicsParameters physics,
        SeedParameters seed)
    {
        HarnessId = harnessId;
        var constraints = new Constraints(physics);

        _universe = Universe.Build(
            in constraints,
            seed.K,
            seed.Timeslices,
            seed.Rng,
            harnessId);
    }

    /// <summary>
    /// Deterministically frees all unmanaged pointers and memory arenas associated with this universe.
    /// </summary>
    public void Dispose() => _universe.Dispose();

    // =====================================================================================
    // EXECUTION & ORCHESTRATION API
    // =====================================================================================

    /// <summary>
    /// Crosses the managed boundary and executes a batch of Monte Carlo sweeps at native speed.
    /// </summary>
    /// <param name="sweepsToRun">The number of <c>N_3</c> sweeps to perform.</param>
    /// <param name="trace">Optional POH-backed telemetry buffer for recording per-sweep vertex and simplex counts.</param>
    public unsafe void Run(int sweepsToRun, Volume? trace = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sweepsToRun);

        if (trace != null && trace.Length < sweepsToRun)
        {
            throw new ArgumentException(
                $"Volume trace capacity ({trace.Length}) is smaller than sweepsToRun ({sweepsToRun}).",
                nameof(trace));
        }

        var random = _universe.GetRandom();
        var mutator = _universe.GetMutator();
        var physics = _universe.GetPhysics();

        if (sweepsToRun == 0)
            return;

        _executeSweepBurst(
            random,
            mutator,
            physics,
            trace != null ? trace.UnmanagedPointer : null,
            sweepsToRun);

        GC.KeepAlive(trace);
    }

    /// <summary>
    /// Evaluates a physical or topological observable against the current spacetime state.
    /// </summary>
    /// <typeparam name="TResult">The measurement result type produced by the observable.</typeparam>
    /// <param name="observable">The observable to evaluate.</param>
    /// <returns>The evaluated measurement snapshot.</returns>
    public TResult Measure<TResult>(IManifoldObservable<TResult> observable)
    {
        ArgumentNullException.ThrowIfNull(observable);
        return observable.Measure<AdjacencyAccessor, InspectingAccessor, RandomAccessor>(
            _universe.GetInspector(),
            _universe.GetRandom());
    }
}