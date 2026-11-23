using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Thermalizer;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.Thermalizer;

public sealed class ThermalizerJobTests : IDisposable
{
    private readonly string _tempDir;

    public ThermalizerJobTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_therm_job_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class SyntheticThermalHarness(int eqSimplices, int eqVertices) : ISimulationHarness
    {
        public uint MaxSimplices => (uint)(eqSimplices * 2);

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            throw new NotSupportedException();

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null)
        {
            if (trace is null)
                return;

            for (var s = 0; s < sweepsToRun; s++)
            {
                trace.TotalSimplices[s] = eqSimplices + (s % 2 == 0 ? 4 : -4);
                trace.TotalVertices[s] = eqVertices + (s % 2 == 0 ? 1 : -1);
            }
        }

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable) =>
            throw new NotSupportedException();

        public void Dispose() { }
    }

    [Fact]
    public void Validate_EnforcesWindowFloorAndBurnInAtLeastWindow()
    {
        var job = new ThermalizerJob();

        var tooShortWindow = new RunConfig
        {
            Thermalization = new ThermalizationParameters { BurnInSweeps = 100, WindowSweeps = 2 }
        };
        Assert.Single(job.Validate(tooShortWindow));

        var burnInSmallerThanWindow = new RunConfig
        {
            Thermalization = new ThermalizationParameters { BurnInSweeps = 50, WindowSweeps = 100 }
        };
        Assert.Single(job.Validate(burnInSmallerThanWindow));

        var valid = new RunConfig
        {
            Thermalization = new ThermalizationParameters { BurnInSweeps = 200, WindowSweeps = 100 }
        };
        Assert.Empty(job.Validate(valid));
    }

    [Fact]
    public void Execute_And_Plot_PersistCsvEmitSummaryAndRenderAllThreeCharts()
    {
        var config = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "ThermalJobTest",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Physics = new PhysicsParameters { K0 = 5.0, Kd = 1.37, Delta = 0.69, Epsilon = 0.0005, TargetVolume = 16000 },
            Thermalization = new ThermalizationParameters { BurnInSweeps = 250, WindowSweeps = 100 },
            Output = new OutputParameters { Directory = _tempDir }
        };

        using var h0 = new SyntheticThermalHarness(15998, 2880);
        using var h1 = new SyntheticThermalHarness(16004, 2884);
        ISimulationHarness[] harnesses = [h0, h1];

        var job = new ThermalizerJob();
        var progress = new List<JobProgress>();

        var result = job.Execute(harnesses, config, progress.Add);

        Assert.Equal(16000u, result.TargetVolume);
        Assert.Equal(2, result.Replicas.Count);
        Assert.Equal(1, result.EnsembleTauInt);
        Assert.True(File.Exists(Path.Combine(_tempDir, "thermalization.csv")));
        Assert.Contains(progress, p => p.IsJobComplete && p.SummaryLines is { Length: 3 });

        var plotProgress = new List<JobProgress>();
        job.Plot(_tempDir, plotProgress.Add);

        Assert.Equal(3, plotProgress.Count);
        Assert.True(File.Exists(Path.Combine(_tempDir, "thermalization_autocorrelation.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "thermalization_equilibrium_volumes.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "thermalization_curvature_ratio.png")));
    }
}