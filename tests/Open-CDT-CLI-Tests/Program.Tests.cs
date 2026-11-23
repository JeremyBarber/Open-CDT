using CDT_Agnostic.Config;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Thermalizer;

using Xunit;
using Program = Open_CDT_CLI.Program;

namespace Open_CDT_CLI_Tests;

public sealed class ProgramTests : IDisposable
{
    private readonly string _tempDir;

    public ProgramTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_cli_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Theory]
    [InlineData]
    [InlineData("unknown", "arg")]
    [InlineData("simulate")]
    [InlineData("plot")]
    [InlineData("simulate", "a.cdt", "extra")]
    public void Main_WithInvalidArguments_PrintsUsageAndReturnsOne(params string[] args)
    {
        var exitCode = Program.Main(args);
        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Main_SimulateAndPlotCommands_ReturnZeroOnValidInputs()
    {
        var config = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "CliSmoke",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = 1,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 1, Timeslices = 6, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 1.0, Kd = 0.92, Delta = 0.5, Epsilon = 0.02, TargetVolume = 400 },
            Thermalization = new ThermalizationParameters { BurnInSweeps = 40, WindowSweeps = 30 },
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 1,
                MinDecorrelationSweeps = 5,
                HausdorffSamples = 2,
                HausdorffMaxRadius = 6,
                SpectralSamples = 2,
                SpectralMaxSigma = 8
            },
            Output = new OutputParameters { Directory = _tempDir }
        };

        config.ToYamlFile(_tempDir);
        var cfgPath = Path.Combine(_tempDir, "CliSmoke.cdt");

        Assert.Equal(0, Program.Main(["simulate", cfgPath]));

        var runDir = Assert.Single(Directory.GetDirectories(Path.Combine(_tempDir, "CliSmoke")));
        Assert.Equal(0, Program.Main(["plot", runDir]));
        Assert.True(File.Exists(Path.Combine(runDir, "volume_profile_ensemble.png")));
    }
}