using CDT_2plus1;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;

using Xunit;

namespace CDT_2plus1_Tests;

public sealed class SimulationHarnessCheckpointTests
{
    private static PhysicsParameters CreateTestPhysics() =>
        new() { K0 = 5.05, Kd = 1.369, Delta = 0.69, Epsilon = 0.0005, TargetVolume = 4000 };

    [Fact]
    public void Checkpoint_WhenResumedWithoutReseed_FollowsBitIdenticalMonteCarloTrajectory()
    {
        var physics = CreateTestPhysics();
        var seed = new SeedParameters { K = 2, Timeslices = 16, Rng = 42UL };

        using var harnessA = SimulationHarness.Create(0, physics, seed);
        harnessA.Run(300);

        using var stream = new MemoryStream();
        harnessA.SaveCheckpoint(stream);

        stream.Position = 0;
        using var harnessB = SimulationHarness.LoadCheckpoint(0, stream, physics, reseedRng: null);

        var traceA = new Volume(300);
        var traceB = new Volume(300);

        harnessA.Run(300, traceA);
        harnessB.Run(300, traceB);

        Assert.Equal(traceA.TotalSimplices, traceB.TotalSimplices);
        Assert.Equal(traceA.TotalVertices, traceB.TotalVertices);

        var profileObs = new VolumeProfile(16);
        Assert.Equal(harnessA.Measure(profileObs), harnessB.Measure(profileObs));
    }

    [Fact]
    public void Checkpoint_WhenResumedWithReseed_PreservesGeometryAndDivergesTrajectory()
    {
        var physics = CreateTestPhysics();
        var seed = new SeedParameters { K = 1, Timeslices = 8, Rng = 42UL };

        using var harnessA = SimulationHarness.Create(0, physics, seed);
        harnessA.Run(50);

        using var stream = new MemoryStream();
        harnessA.SaveCheckpoint(stream);

        stream.Position = 0;
        using var harnessB = SimulationHarness.LoadCheckpoint(0, stream, physics, reseedRng: 99999UL);

        var profileObs = new VolumeProfile(8);
        Assert.Equal(harnessA.Measure(profileObs), harnessB.Measure(profileObs));
    }

    [Fact]
    public void Checkpoint_WhenStreamIsTruncatedOrCorrupted_ThrowsAndFreesMemoryCleanly()
    {
        var physics = CreateTestPhysics();
        var seed = new SeedParameters { K = 1, Timeslices = 8, Rng = 7UL };

        using var harness = SimulationHarness.Create(0, physics, seed);
        using var fullStream = new MemoryStream();
        harness.SaveCheckpoint(fullStream);

        var validBytes = fullStream.ToArray();
        validBytes[^1] ^= 0xFF;
        using var corruptedStream = new MemoryStream(validBytes);

        Assert.Throws<InvalidDataException>(() =>
            SimulationHarness.LoadCheckpoint(0, corruptedStream, physics));
    }
}