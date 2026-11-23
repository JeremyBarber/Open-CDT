using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Workflows.Jobs.Thermalizer.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.Thermalizer.Helpers;

public sealed class ReplicaThermalizerTests
{
    private sealed class TwoStageHarness(int relaxSweepsCutoff) : ISimulationHarness
    {
        private int _elapsedSweeps;

        public uint MaxSimplices => 32_000;
        public int TotalSweepsExecuted => _elapsedSweeps;
        public int UntracedRunCalls { get; private set; }

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            throw new NotSupportedException();

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null)
        {
            if (trace is null)
            {
                UntracedRunCalls++;
                _elapsedSweeps += sweepsToRun;
                return;
            }

            for (var s = 0; s < sweepsToRun; s++)
            {
                var globalSweep = _elapsedSweeps + s;
                var inEquilibrium = globalSweep >= relaxSweepsCutoff;

                // Early transient sweeps sit at 4,000; trailing window sweeps sit at 16,000.
                trace.TotalSimplices[s] = inEquilibrium ? 16000 : 4000;
                trace.TotalVertices[s] = inEquilibrium ? 2800 + (s % 2 == 0 ? 2 : -2) : 800;
            }

            _elapsedSweeps += sweepsToRun;
        }

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable) =>
            throw new NotSupportedException();

        public void Dispose() { }
    }

    [Fact]
    public void RunReplica_HeadlessFastPath_ExcludesBurnInTransientAndExecutesExactNonMultipleSweepCounts()
    {
        // BurnIn = 235, Window = 115 -> relaxSweeps = 120 (neither is a multiple of TelemetryBurstSweeps = 100).
        using var harness = new TwoStageHarness(relaxSweepsCutoff: 120);
        var completedGlobal = 0L;

        var record = ReplicaThermalizer.RunReplica(
            harness,
            replicaId: 3,
            totalReplicas: 4,
            burnInSweeps: 235,
            windowSweeps: 115,
            headerTitle: "Thermalising",
            totalEnsembleSweeps: 940,
            ref completedGlobal,
            onProgress: null);

        Assert.Equal(235, harness.TotalSweepsExecuted);
        Assert.Equal(1, harness.UntracedRunCalls);
        Assert.Equal(3u, record.ReplicaId);
        Assert.Equal(16000.0, record.MeanSimplices, tolerance: 1e-12);
        Assert.Equal(1, record.TauInt);
        Assert.True(record.TauConverged);
    }

    [Fact]
    public void RunReplica_WithProgressCallback_StreamsBurstsAcrossBothPhasesAndAccumulatesGlobalSweeps()
    {
        using var harness = new TwoStageHarness(relaxSweepsCutoff: 150);
        var completedGlobal = 0L;
        var updates = new List<JobProgress>();

        var record = ReplicaThermalizer.RunReplica(
            harness,
            replicaId: 0,
            totalReplicas: 2,
            burnInSweeps: 250,
            windowSweeps: 100,
            headerTitle: "Thermalising",
            totalEnsembleSweeps: 500,
            ref completedGlobal,
            onProgress: updates.Add);

        Assert.Equal(250, harness.TotalSweepsExecuted);
        Assert.Equal(250L, completedGlobal);
        Assert.Equal(0, harness.UntracedRunCalls);
        Assert.Equal(3, updates.Count); // 100 + 50 (relax) + 100 (window)
        Assert.Equal(16000.0, record.MeanSimplices, tolerance: 1e-12);
    }
}