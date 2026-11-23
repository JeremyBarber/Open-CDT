using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.PhaseClassifier.Helpers;

public sealed class CoordinateSamplerTests
{
    private sealed class SyntheticProfileHarness(int simplices, int vertices, int[] sliceProfile) : ISimulationHarness
    {
        public uint MaxSimplices => (uint)(simplices * 2);
        public int RunCalls { get; private set; }
        public int TotalSweepsExecuted { get; private set; }
        public int MeasureCalls { get; private set; }

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            throw new NotSupportedException();

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null)
        {
            RunCalls++;
            TotalSweepsExecuted += sweepsToRun;

            if (trace is null)
                return;

            for (var s = 0; s < sweepsToRun; s++)
            {
                trace.TotalSimplices[s] = simplices;
                trace.TotalVertices[s] = vertices + (s % 2 == 0 ? 1 : -1);
            }
        }

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable)
        {
            MeasureCalls++;
            return (TResult)(object)sliceProfile;
        }

        public void Dispose() { }
    }

    [Fact]
    public void AnalyzeSliceProfile_HandlesEmptyUniformAndSingleSliceProfiles()
    {
        Assert.Equal((0.0, 0.0), CoordinateSampler.AnalyzeSliceProfile([]));
        Assert.Equal((0.0, 0.0), CoordinateSampler.AnalyzeSliceProfile([0, 0, 0, 0]));

        // Uniform cylinder (T = 8): PeakSliceFraction = 1/8 = 0.125, Continuity = 1.0.
        int[] uniform = [100, 100, 100, 100, 100, 100, 100, 100];
        (var uPeak, var uCont) = CoordinateSampler.AnalyzeSliceProfile(uniform);
        Assert.Equal(0.125, uPeak, tolerance: 1e-12);
        Assert.Equal(1.0, uCont, tolerance: 1e-12);

        // Single-slice localization: PeakSliceFraction = 1.0, Continuity = 0.0.
        int[] pancake = [800, 0, 0, 0, 0, 0, 0, 0];
        (var pPeak, var pCont) = CoordinateSampler.AnalyzeSliceProfile(pancake);
        Assert.Equal(1.0, pPeak, tolerance: 1e-12);
        Assert.Equal(0.0, pCont, tolerance: 1e-12);
    }

    [Fact]
    public void MeasureCoordinate_ExecutesExactSweepBudgetAndSamplesDecorrelatedIntervals()
    {
        int[] profile = [100, 300, 300, 100];
        using var harness = new SyntheticProfileHarness(simplices: 800, vertices: 160, profile);
        var trace = new Volume(100);
        var volumeObs = new VolumeProfile(timeslices: 4);
        var cp = new CriticalPointRecord(2.5, 0.6, 1.12, 800.0, 10.0, 160.0);
        var progress = new List<JobProgress>();

        var completedCoords = 0;
        var completedGlobalSweeps = 0L;

        var record = CoordinateSampler.MeasureCoordinate(
            harness,
            cp,
            trace,
            volumeObs,
            replicaId: 0,
            totalReplicas: 1,
            totalCoordinates: 4,
            totalSweeps: 200,
            showDelta: true,
            ref completedCoords,
            ref completedGlobalSweeps,
            progress.Add);

        // Q1 (50) + Q2 (50) + 50 samples at 2 * tau_int = 2 sweeps (100) = 200 sweeps across 52 Run calls.
        Assert.Equal(200, harness.TotalSweepsExecuted);
        Assert.Equal(200L, completedGlobalSweeps);
        Assert.Equal(52, harness.RunCalls);
        Assert.Equal(50, harness.MeasureCalls);
        Assert.Equal(800.0, record.MeanVolume, tolerance: 1e-12);
        Assert.Equal(0.20, record.VertexDensity, tolerance: 1e-12);
        Assert.Equal(0.375, record.PeakSliceFraction, tolerance: 1e-12);
    }
}