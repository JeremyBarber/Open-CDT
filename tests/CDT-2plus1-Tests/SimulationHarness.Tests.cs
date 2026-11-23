using CDT_2plus1;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;

using Xunit;

namespace CDT_2plus1_Tests;

public sealed class SimulationHarnessTests
{
    private static PhysicsParameters CreateTestPhysics() =>
        new() { K0 = 1.0, Kd = 1.0, Delta = 0.5, Epsilon = 0.02, TargetVolume = 1000 };

    [Fact]
    public void Create_AssignsHarnessId_AndSupportsRunAndMeasure()
    {
        var physics = CreateTestPhysics();
        var seed = new SeedParameters { K = 1, Timeslices = 4, Rng = 42UL };

        using var harness = (SimulationHarness)SimulationHarness.Create(7u, physics, seed);

        Assert.Equal(7u, harness.HarnessId);
        Assert.True(harness.MaxSimplices >= physics.TargetVolume);

        var trace = new Volume(10);
        harness.Run(10, trace);

        var profile = harness.Measure(new VolumeProfile(4));
        Assert.Equal(trace.TotalSimplices[^1], profile.Sum());
    }

    [Fact]
    public void Run_WithInvalidSweepCountOrUndersizedTrace_ThrowsExpectedException()
    {
        var physics = CreateTestPhysics();
        var seed = new SeedParameters { K = 1, Timeslices = 4, Rng = 42UL };

        using var harness = SimulationHarness.Create(0, physics, seed);
        var smallTrace = new Volume(5);

        Assert.Throws<ArgumentOutOfRangeException>(() => harness.Run(-1));
        Assert.Throws<ArgumentException>(() => harness.Run(10, smallTrace));
    }

    [Fact]
    public void Dispose_PreventsSubsequentRunMeasureAndSaveCheckpoint()
    {
        var physics = CreateTestPhysics();
        var seed = new SeedParameters { K = 1, Timeslices = 4, Rng = 42UL };

        var harness = SimulationHarness.Create(0, physics, seed);
        harness.Dispose();

        using var stream = new MemoryStream();
        Assert.Throws<ObjectDisposedException>(() => harness.Run(1));
        Assert.Throws<ObjectDisposedException>(() => harness.Measure(new VolumeProfile(4)));
        Assert.Throws<ObjectDisposedException>(() => harness.SaveCheckpoint(stream));
    }
}