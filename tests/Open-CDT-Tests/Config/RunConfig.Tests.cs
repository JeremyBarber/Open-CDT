using CDT_Agnostic.Config;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.PhaseClassifier;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Thermalizer;

using Xunit;

namespace Open_CDT_Tests.Config;

public sealed class RunConfigTests : IDisposable
{
    private readonly string _tempDir;

    public RunConfigTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_cfg_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void DefaultConstructor_InitializesSubRecordsWithSafeStepDefaults()
    {
        var config = new RunConfig();

        Assert.Equal(string.Empty, config.Simulation.Name);
        Assert.Equal(0u, config.Simulation.Replicas);
        Assert.Equal(0u, config.Simulation.SpacelikeDimensions);
        Assert.Equal(1, config.Tuning.K0Steps);
        Assert.Equal(1, config.Tuning.DeltaSteps);
        Assert.Equal(string.Empty, config.Output.Directory);
    }

    [Fact]
    public void ToYamlFile_And_FromYamlFile_RoundTripAllSectionsBitIdentically()
    {
        var original = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "RoundTripRun",
                Workflow = Workflow.PhaseSpaceMapper,
                Replicas = 8,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 2, Timeslices = 32, Rng = 42UL, StartFrom = "seed.cdtbin" },
            Physics = new PhysicsParameters { K0 = 5.05, Kd = 1.369, Delta = 0.69, Epsilon = 0.0005, TargetVolume = 16000 },
            Tuning = new TuningParameters
            {
                ToleranceRatio = 0.005,
                PlateauSweeps = 1000,
                MaxSweepsPerProbe = 20000,
                K0Low = 2.0,
                K0High = 12.5,
                K0Steps = 32,
                DeltaLow = 0.0,
                DeltaHigh = 1.2,
                DeltaSteps = 8
            },
            Classification = new ClassificationParameters { SweepsPerCoordinate = 4000 },
            Thermalization = new ThermalizationParameters { BurnInSweeps = 150000, WindowSweeps = 5000 },
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 25,
                MinDecorrelationSweeps = 500,
                HausdorffSamples = 25,
                HausdorffMaxRadius = 25,
                SpectralSamples = 25,
                SpectralMaxSigma = 100
            },
            Output = new OutputParameters { Directory = "results", SaveTo = "ensemble.cdtbin" }
        };

        original.ToYamlFile(_tempDir);
        var loaded = RunConfig.FromYamlFile(Path.Combine(_tempDir, "RoundTripRun.cdt"));

        Assert.Equal(original, loaded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("nonexistent_file.cdt")]
    public void FromYamlFile_WithMissingOrBlankPath_ThrowsInvalidOperationException(string? invalidPath)
    {
        Assert.Throws<InvalidOperationException>(() => RunConfig.FromYamlFile(invalidPath));
    }
}