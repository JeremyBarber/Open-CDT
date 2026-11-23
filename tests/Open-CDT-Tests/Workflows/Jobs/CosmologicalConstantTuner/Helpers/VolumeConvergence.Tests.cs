using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.CosmologicalConstantTuner.Helpers;

public sealed class VolumeConvergenceTests
{
    private sealed class ScriptedVolumeHarness(int[] burstVolumes, uint maxSimplices = 50_000) : ISimulationHarness
    {
        private int _callIndex;

        public uint MaxSimplices => maxSimplices;
        public int RunCalls => _callIndex;

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            throw new NotSupportedException();

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null)
        {
            var vol = burstVolumes[Math.Min(_callIndex, burstVolumes.Length - 1)];
            _callIndex++;

            if (trace is null)
                return;

            Array.Fill(trace.TotalSimplices, vol, 0, sweepsToRun);
            Array.Fill(trace.TotalVertices, vol / 5, 0, sweepsToRun);
        }

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable) =>
            throw new NotSupportedException();

        public void Dispose() { }
    }

    private static TuningParameters CreateTuning(int plateauSweeps = 100, int maxSweeps = 400) =>
        new()
        {
            ToleranceRatio = 0.01,
            PlateauSweeps = plateauSweeps,
            MaxSweepsPerProbe = maxSweeps
        };

    [Fact]
    public void RunUntilPlateau_LocksAfterTwoStationaryHalvesInsideTargetBand()
    {
        // Half 1 climbs at 8,000; Halves 2 and 3 hold stationary at 10,020 inside [9900, 10100].
        using var harness = new ScriptedVolumeHarness([8000, 10020, 10020]);
        var trace = new Volume(50);
        var progress = new List<JobProgress>();

        var record = VolumeConvergence.RunUntilPlateau(
            harness,
            trace,
            replicaId: 0,
            totalReplicas: 1,
            k0: 2.0,
            delta: 0.5,
            kdTested: 1.0,
            targetVolume: 10000,
            tuning: CreateTuning(),
            onProgress: progress.Add);

        Assert.Equal(ProbeOutcome.LockedOnTarget, record.Outcome);
        Assert.Equal(3, harness.RunCalls);
        Assert.Equal(10020.0, record.MeanVolume, tolerance: 1e-12);
        Assert.Equal(3, progress.Count);
    }

    [Fact]
    public void RunUntilPlateau_AbortsImmediatelyWhenSafetyCeilingIsCrossed()
    {
        // maxTarget = 10,100, MaxSimplices = 15,000 -> safetyCeiling = 0.5 * (10100 + 15000) = 12,550.
        using var harness = new ScriptedVolumeHarness([13000, 13000], maxSimplices: 15_000);
        var trace = new Volume(50);

        var record = VolumeConvergence.RunUntilPlateau(
            harness,
            trace,
            replicaId: 0,
            totalReplicas: 1,
            k0: 2.0,
            delta: 0.5,
            kdTested: 0.0,
            targetVolume: 10000,
            tuning: CreateTuning());

        Assert.Equal(ProbeOutcome.AboveTarget, record.Outcome);
        Assert.Equal(1, harness.RunCalls);
    }

    [Fact]
    public void RunUntilPlateau_ClassifiesStationaryBelowTargetAndDriftingTimeout()
    {
        using var belowHarness = new ScriptedVolumeHarness([6500, 6500]);
        var trace = new Volume(50);

        var belowRecord = VolumeConvergence.RunUntilPlateau(
            belowHarness,
            trace,
            0,
            1,
            2.0,
            0.5,
            1.5,
            10000,
            CreateTuning());

        Assert.Equal(ProbeOutcome.BelowTarget, belowRecord.Outcome);
        Assert.Equal(2, belowHarness.RunCalls);

        // Continuously climbing probe hits MaxSweepsPerProbe (4 halves = 200 sweeps) and classifies by final position.
        using var climbingHarness = new ScriptedVolumeHarness([4000, 5500, 7000, 8500]);
        var timeoutRecord = VolumeConvergence.RunUntilPlateau(
            climbingHarness,
            trace,
            0,
            1,
            2.0,
            0.5,
            1.1,
            10000,
            CreateTuning(plateauSweeps: 100, maxSweeps: 200));

        Assert.Equal(ProbeOutcome.BelowTarget, timeoutRecord.Outcome);
        Assert.Equal(4, climbingHarness.RunCalls);
    }
}