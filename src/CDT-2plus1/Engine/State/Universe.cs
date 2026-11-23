using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.State.Physics;
using CDT_2plus1.Seed;
using CDT_Agnostic.RandomNumberGeneration;
using CDT_Agnostic.RuntimeMemory.Adjacency;

using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;
using static CDT_2plus1.Engine.Topology.TopologyConstants;

namespace CDT_2plus1.Engine.State;

/// <summary>
/// The master container for a single, isolated 2+1D CDT simulation.
/// Owns the unmanaged <see cref="Manifold"/>, <see cref="Dynamics"/>, and <see cref="Pcg32"/> arenas
/// and exposes zero-allocation accessor lenses to the managed orchestration layer.
/// </summary>
internal sealed unsafe partial class Universe : IDisposable
{
    private const double SimplexHeadroomFactor = 1.5;
    private const double VertexHeadroomFactor = 1.2;

    private bool _disposed;

    private Pcg32* _rng;
    private Manifold* _manifold;
    private Dynamics* _dynamics;

    /// <summary>
    /// Gets the maximum number of simplices allowed in the current Simulation.
    /// </summary>
    public uint MaxSimplices => _manifold->MaxSimplices;

    private Universe(Manifold* state, Dynamics* dynamics, Pcg32* rng)
    {
        _manifold = state;
        _dynamics = dynamics;
        _rng = rng;
    }

    /// <summary>
    /// Allocates all unmanaged arenas and seeds an initial closed <c>S^2 × S^1</c> icosahedral spacetime.
    /// </summary>
    public static Universe Build(
        in Constraints constraints,
        uint subdivisions,
        uint timeslices,
        ulong rngSeed,
        ulong rngSequence)
    {
        (var initialVertices, var initialSimplices) = Icosahedral.CalculateIcosahedralSize(subdivisions, timeslices);

        // Derive maxVsimplices from the target with headroom
        var maxSimplices = (uint)(constraints.TargetVolume * SimplexHeadroomFactor);

        // Derive maxVertices from the seed's vertex-to-simplex ratio plus a headroom
        var maxVertices = (uint)(maxSimplices * ((double)initialVertices / initialSimplices) * VertexHeadroomFactor);

        // Each simplex links to 4 vertices (14 IDs per 64-byte block), plus 1 extra block per vertex for tail slack.
        var maxAdjacencyBlocks = ((maxSimplices * VerticesPerSimplex) / AdjacencyBlock.Capacity) + maxVertices;

        Manifold* manifold = null;
        Dynamics* dynamics = null;
        Pcg32* rng = null;

        try
        {
            manifold = Create(maxVertices, maxSimplices, maxAdjacencyBlocks);
            dynamics = Create(in constraints);
            rng = Pcg32.Create(rngSeed, rngSequence);

            var universe = new Universe(manifold, dynamics, rng);
            Icosahedral.BuildIcosahedral(universe.GetSeeder(), subdivisions, timeslices);

            return universe;
        }
        catch
        {
            Destroy(ref manifold);
            Destroy(ref dynamics);
            Pcg32.Destroy(rng);
            throw;
        }
    }

    /// <summary>
    /// Acquires a zero-allocation random selection lens over the manifold and <see cref="Pcg32"/> generator.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RandomAccessor GetRandom()
    {
        ThrowIfDisposed();
        return new(_manifold, _rng);
    }

    /// <summary>
    /// Acquires a zero-allocation, read-only inspection lens into the unmanaged manifold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public InspectingAccessor GetInspector()
    {
        ThrowIfDisposed();
        return new(_manifold);
    }

    /// <summary>
    /// Acquires a zero-allocation topological mutation lens into the unmanaged manifold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public MutatingAccessor GetMutator()
    {
        ThrowIfDisposed();
        return new(_manifold);
    }

    /// <summary>
    /// Acquires a zero-allocation read-only lens into the precomputed thermodynamic lookup tables.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PhysicsAccessor GetPhysics()
    {
        ThrowIfDisposed();
        return new(_dynamics);
    }

    /// <summary>
    /// Acquires the initialization lens reserved exclusively for constructing the initial seed geometry.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SeederAccessor GetSeeder()
    {
        ThrowIfDisposed();
        return new(_manifold);
    }

    /// <summary>
    /// Deterministically releases all unmanaged memory arenas owned by this universe.
    /// </summary>
    public void Dispose()
    {
        ReleaseUnmanagedResources();
        GC.SuppressFinalize(this);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    ~Universe()
    {
        ReleaseUnmanagedResources();
    }

    private void ReleaseUnmanagedResources()
    {
        if (_disposed)
            return;

        Destroy(ref _manifold);
        Destroy(ref _dynamics);

        Pcg32.Destroy(_rng);
        _rng = null;

        _disposed = true;
    }
}