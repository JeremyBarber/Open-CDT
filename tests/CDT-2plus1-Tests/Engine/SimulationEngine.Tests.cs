using CDT_2plus1.Engine;
using CDT_2plus1.Engine.State;
using CDT_2plus1.Engine.State.Physics;
using CDT_2plus1_Tests.TestHarnesses.Manifolds;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements.Traces;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_2plus1.Engine.State.Physics.Dynamics;

namespace CDT_2plus1_Tests.Engine;

public sealed unsafe class SimulationEngineTests
{
    private static readonly delegate* unmanaged<RandomAccessor, MutatingAccessor, PhysicsAccessor, Volume.Unmanaged*, int, void> _executeSweepChunk =
        &SimulationEngine.ExecuteSweepBurst;

    [Fact]
    public void ExecuteSweepChunk_PopulatesVolumeTraceAndPreservesManifoldIntegrity()
    {
        const uint timeslices = 4;
        const int testSweeps = 15;

        var constraints = new Constraints(new PhysicsParameters
        {
            K0 = 1.0,
            Kd = 1.0,
            Delta = 0.5,
            Epsilon = 0.02,
            TargetVolume = 500
        });

        using var universe = Universe.Build(
            in constraints,
            subdivisions: 1,
            timeslices: timeslices,
            rngSeed: 42,
            rngSequence: 1);

        var trace = new Volume(testSweeps);

        // 1. Run with a pinned Volume trace buffer
        _executeSweepChunk(
            universe.GetRandom(),
            universe.GetMutator(),
            universe.GetPhysics(),
            trace.UnmanagedPointer,
            testSweeps);

        for (var i = 0; i < testSweeps; i++)
        {
            Assert.True(trace.TotalVertices[i] > 0);
            Assert.True(trace.TotalSimplices[i] > 0);
        }

        // 2. Run without a trace buffer (null fast-path)
        _executeSweepChunk(
            universe.GetRandom(),
            universe.GetMutator(),
            universe.GetPhysics(),
            null,
            5);

        // 3. Verify that all topological, causal, S^2 slice, and link invariants hold after 20 sweeps
        ManifoldValidation.AssertIntegrity(
            universe.GetInspector(),
            timeslices,
            checkSeedOrientationParity: false);
    }
}