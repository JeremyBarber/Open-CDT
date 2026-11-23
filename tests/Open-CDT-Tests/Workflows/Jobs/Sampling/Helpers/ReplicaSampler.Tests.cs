using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Sampling.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.Sampling.Helpers;

public sealed class ReplicaSamplerTests
{
    private sealed class OffCenterDropletHarness : ISimulationHarness
    {
        public uint MaxSimplices => 10_000;
        public int TotalSweepsExecuted { get; private set; }

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            throw new NotSupportedException();

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null) =>
            TotalSweepsExecuted += sweepsToRun;

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable)
        {
            if (typeof(TResult) == typeof(int[]))
                return (TResult)(object)new[] { 10, 500, 10, 10, 10, 10, 10, 10 };

            if (typeof(TResult) == typeof(HausdorffResult))
                return (TResult)(object)new HausdorffResult(3.0, [1.0, 4.0, 12.0, 24.0, 40.0, 60.0]);

            if (typeof(TResult) == typeof(SpectralDimensionResult))
                return (TResult)(object)new SpectralDimensionResult(2.9, [1.0, 0.2, 0.12, 0.08, 0.06, 0.05, 0.04]);

            throw new NotSupportedException();
        }

        public void Dispose() { }
    }

    [Fact]
    public void CenterPeriodicProfile_AlignsPeakToHalfTimeslicesAndPreservesUniformProfiles()
    {
        Assert.Empty(ReplicaSampler.CenterPeriodicProfile([]));

        int[] uniform = [100, 100, 100, 100, 100, 100, 100, 100];
        Assert.Equal(uniform, ReplicaSampler.CenterPeriodicProfile(uniform));

        // Peak at slice 1 on T = 8 shifts circularly to targetCenter = T / 2 = 4 while conserving total volume.
        int[] raw = [10, 500, 10, 10, 10, 10, 10, 10];
        var centered = ReplicaSampler.CenterPeriodicProfile(raw);
        Assert.Equal(raw.Sum(), centered.Sum());
        Assert.Equal(500, centered[4]);
    }

    [Fact]
    public void RunReplica_StepsBySweepsBetweenSamplesAndCentersRecordedProfiles()
    {
        using var harness = new OffCenterDropletHarness();
        var config = new RunConfig
        {
            Seed = new SeedParameters { K = 1, Timeslices = 8, Rng = 42UL },
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 2,
                MinDecorrelationSweeps = 25,
                HausdorffSamples = 2,
                HausdorffMaxRadius = 5,
                SpectralSamples = 2,
                SpectralMaxSigma = 6
            }
        };

        var completedGlobal = 0;
        var updates = new List<JobProgress>();

        var records = ReplicaSampler.RunReplica(
            harness,
            replicaId: 1,
            totalReplicas: 2,
            sweepsBetweenSamples: 25,
            config,
            totalEnsembleSamples: 4,
            ref completedGlobal,
            updates.Add);

        Assert.Equal(50, harness.TotalSweepsExecuted);
        Assert.Equal(2, completedGlobal);
        Assert.Equal(2, updates.Count);
        Assert.Equal(2, records.Count);
        Assert.Equal(500, records[0].CenteredVolumeProfile[4]);
    }
}