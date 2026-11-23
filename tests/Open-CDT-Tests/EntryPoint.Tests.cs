using CDT_Agnostic.Config;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.PhaseClassifier;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Thermalizer;

using Xunit;

namespace Open_CDT_Tests;

public sealed class EntryPointTests : IDisposable
{
    private readonly string _tempDir;

    public EntryPointTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_entry_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private string WriteTestConfigFile(uint spacelikeDimensions = 2)
    {
        var config = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "EntryRunTest",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = 2,
                SpacelikeDimensions = spacelikeDimensions
            },
            Seed = new SeedParameters { K = 1, Timeslices = 6, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 1.0, Kd = 0.92, Delta = 0.5, Epsilon = 0.02, TargetVolume = 600 },
            Thermalization = new ThermalizationParameters { BurnInSweeps = 60, WindowSweeps = 40 },
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 2,
                MinDecorrelationSweeps = 5,
                HausdorffSamples = 4,
                HausdorffMaxRadius = 6,
                SpectralSamples = 4,
                SpectralMaxSigma = 10
            },
            Output = new OutputParameters { Directory = _tempDir }
        };

        config.ToYamlFile(_tempDir);
        return Path.Combine(_tempDir, "EntryRunTest.cdt");
    }

    [Fact]
    public void Run_And_Plot_ExecuteFullPipelineAndRenderChartsInTimestampedFolder()
    {
        var cfgPath = WriteTestConfigFile(spacelikeDimensions: 2);
        var progressUpdates = new List<JobProgress>();

        var runCode = EntryPoint.Run(cfgPath, progressUpdates.Add);
        Assert.Equal(0, runCode);
        Assert.NotEmpty(progressUpdates);

        var simRoot = Path.Combine(_tempDir, "EntryRunTest");
        var timestampDirs = Directory.GetDirectories(simRoot);
        var runDir = Assert.Single(timestampDirs);

        Assert.True(File.Exists(Path.Combine(runDir, "EntryRunTest.cdt")));
        Assert.True(File.Exists(Path.Combine(runDir, "thermalization.csv")));
        Assert.True(File.Exists(Path.Combine(runDir, "volume_profile.csv")));

        var plotUpdates = new List<JobProgress>();
        var plotCode = EntryPoint.Plot(runDir, plotUpdates.Add);
        Assert.Equal(0, plotCode);
        Assert.Equal(11, plotUpdates.Count);
        Assert.True(File.Exists(Path.Combine(runDir, "thermalization_autocorrelation.png")));
        Assert.True(File.Exists(Path.Combine(runDir, "volume_profile_ensemble.png")));
    }

    [Fact]
    public void Run_And_Plot_PhaseSpaceMapper_ExecutesRealEnginePipelineAndRendersCharts()
    {
        var config = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "EntryMapTest",
                Workflow = Workflow.PhaseSpaceMapper,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 1, Timeslices = 6, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 1.0, Kd = 0.92, Delta = 0.5, Epsilon = 0.02, TargetVolume = 600 },
            Tuning = new TuningParameters
            {
                ToleranceRatio = 0.05,
                PlateauSweeps = 40,
                MaxSweepsPerProbe = 160,
                K0Low = 1.0,
                K0High = 1.2,
                K0Steps = 2,
                DeltaSteps = 1
            },
            Classification = new ClassificationParameters { SweepsPerCoordinate = 120 },
            Output = new OutputParameters { Directory = _tempDir }
        };

        config.ToYamlFile(_tempDir);
        var cfgPath = Path.Combine(_tempDir, "EntryMapTest.cdt");

        var runUpdates = new List<JobProgress>();
        Assert.Equal(0, EntryPoint.Run(cfgPath, runUpdates.Add));
        Assert.Equal(2, runUpdates.Count(p => p.IsJobComplete));

        var runDir = Assert.Single(Directory.GetDirectories(Path.Combine(_tempDir, "EntryMapTest")));
        Assert.True(File.Exists(Path.Combine(runDir, "EntryMapTest.cdt")));
        Assert.True(File.Exists(Path.Combine(runDir, "tuning.csv")));
        Assert.True(File.Exists(Path.Combine(runDir, "phase_space.csv")));

        var plotUpdates = new List<JobProgress>();
        Assert.Equal(0, EntryPoint.Plot(runDir, plotUpdates.Add));
        Assert.Equal(6, plotUpdates.Count);
        Assert.True(File.Exists(Path.Combine(runDir, "tuning_critical_surface.png")));
        Assert.True(File.Exists(Path.Combine(runDir, "phase_vertex_density.png")));
    }

    [Fact]
    public void Run_WithUnimplementedCartridgeDimension_ThrowsInvalidOperationException()
    {
        var badDimPath = WriteTestConfigFile(spacelikeDimensions: 3);
        Assert.Throws<InvalidOperationException>(() => EntryPoint.Run(badDimPath));
    }

    [Fact]
    public void Plot_WithMissingDirectory_ThrowsDirectoryNotFoundException()
    {
        var missingDir = Path.Combine(_tempDir, "does_not_exist");
        Assert.Throws<DirectoryNotFoundException>(() => EntryPoint.Plot(missingDir));
    }
}