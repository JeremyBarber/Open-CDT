using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.CosmologicalConstantTuner;

public sealed class CosmologicalConstantTunerJobTests : IDisposable
{
    private readonly string _tempDir;

    public CosmologicalConstantTunerJobTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_tuner_job_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Analytical mock harness following the exact linear saddle-point susceptibility law:
    /// N(kd) = TargetVolume - (kd - kdCrit) / (2 * Epsilon), where kdCrit = 0.8 + 0.1 * K0.
    /// </summary>
    private sealed class AnalyticalTuningHarness(PhysicsParameters physics) : ISimulationHarness
    {
        public uint MaxSimplices => physics.TargetVolume * 3;

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            new AnalyticalTuningHarness(physics);

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null)
        {
            if (trace is null)
                return;

            var kdCrit = 0.80 + (0.10 * physics.K0);
            var slope = -1.0 / (2.0 * physics.Epsilon);
            var eqVol = (int)Math.Clamp(Math.Round(physics.TargetVolume + ((physics.Kd - kdCrit) * slope)), 100, (int)MaxSimplices);
            var eqVert = eqVol / 5;

            Array.Fill(trace.TotalSimplices, eqVol, 0, sweepsToRun);
            Array.Fill(trace.TotalVertices, eqVert, 0, sweepsToRun);
        }

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable) =>
            throw new NotSupportedException();

        public void Dispose() { }
    }

    [Fact]
    public void Validate_FlagsInvalidParametersAndAcceptsValidConfig()
    {
        var job = new CosmologicalConstantTunerJob();

        var invalidConfig = new RunConfig
        {
            Tuning = new TuningParameters
            {
                ToleranceRatio = 1.5,
                PlateauSweeps = 1,
                MaxSweepsPerProbe = 0,
                K0Low = 5.0,
                K0High = 1.0,
                K0Steps = 2,
                DeltaLow = 1.0,
                DeltaHigh = 0.2,
                DeltaSteps = 2
            }
        };

        Assert.Equal(5, job.Validate(invalidConfig).Count());

        var validConfig = new RunConfig
        {
            Tuning = new TuningParameters
            {
                ToleranceRatio = 0.005,
                PlateauSweeps = 200,
                MaxSweepsPerProbe = 2000,
                K0Low = 1.0,
                K0High = 5.0,
                K0Steps = 4,
                DeltaLow = 0.2,
                DeltaHigh = 1.0,
                DeltaSteps = 4
            }
        };

        Assert.Empty(job.Validate(validConfig));
    }

    [Fact]
    public void Execute_SingleCoordinate_LocksOnExactCriticalKdAndRendersTwoPlots()
    {
        var singleDir = Path.Combine(_tempDir, "single");
        Directory.CreateDirectory(singleDir);

        var config = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "SingleTuneTest",
                Workflow = Workflow.PhaseSpaceMapper,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 2, Timeslices = 16, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 2.0, Kd = 0.0, Delta = 0.5, Epsilon = 0.0005, TargetVolume = 10000 },
            Tuning = new TuningParameters
            {
                ToleranceRatio = 0.005,
                PlateauSweeps = 40,
                MaxSweepsPerProbe = 200
            },
            Output = new OutputParameters { Directory = singleDir }
        };

        var job = new CosmologicalConstantTunerJob();
        var progress = new List<JobProgress>();

        var result = job.Execute<AnalyticalTuningHarness>(config, progress.Add);

        Assert.Equal(1.000, result.TunedKd, tolerance: 0.01);
        Assert.Single(result.CriticalPoints);
        Assert.Contains(progress, p => p.IsJobComplete && p.SummaryLines is { Length: 3 });

        var plotProgress = new List<JobProgress>();
        job.Plot(singleDir, plotProgress.Add);

        Assert.Equal(2, plotProgress.Count);
        Assert.False(File.Exists(Path.Combine(singleDir, "tuning_critical_surface.png")));
        Assert.True(File.Exists(Path.Combine(singleDir, "tuning_calibration.png")));
        Assert.True(File.Exists(Path.Combine(singleDir, "tuning_bracket_zoom.png")));
    }

    [Fact]
    public void Execute_MultiCoordinateGrid_LocksEveryGridPointAndRendersAllThreePlots()
    {
        var gridDir = Path.Combine(_tempDir, "grid");
        Directory.CreateDirectory(gridDir);

        var config = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "GridTuneTest",
                Workflow = Workflow.PhaseSpaceMapper,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 2, Timeslices = 16, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 1.0, Kd = 0.0, Delta = 0.4, Epsilon = 0.0005, TargetVolume = 10000 },
            Tuning = new TuningParameters
            {
                ToleranceRatio = 0.005,
                PlateauSweeps = 40,
                MaxSweepsPerProbe = 200,
                K0Low = 1.0,
                K0High = 3.0,
                K0Steps = 3,
                DeltaLow = 0.4,
                DeltaHigh = 0.8,
                DeltaSteps = 2
            },
            Output = new OutputParameters { Directory = gridDir }
        };

        var job = new CosmologicalConstantTunerJob();
        var result = job.Execute<AnalyticalTuningHarness>(config);

        Assert.Equal(6, result.CriticalPoints.Count);
        Assert.Equal(0.90, result.CriticalPoints[0].KdCrit, tolerance: 0.01);
        Assert.Equal(1.10, result.CriticalPoints[2].KdCrit, tolerance: 0.01);

        var plotProgress = new List<JobProgress>();
        job.Plot(gridDir, plotProgress.Add);

        Assert.Equal(3, plotProgress.Count);
        Assert.True(File.Exists(Path.Combine(gridDir, "tuning_critical_surface.png")));
    }
}