using CDT_2plus1;
using CDT_Agnostic.Config;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Thermalizer;

using Xunit;

namespace Open_CDT_Tests.Workflows;

public sealed class CanonicalEnsembleWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public CanonicalEnsembleWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_canon_wf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private RunConfig CreateValidConfig(string outDir, string? saveTo = null, string? startFrom = null) =>
        new()
        {
            Simulation = new SimulationParameters
            {
                Name = "CanonTest",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters
            {
                K = startFrom is null ? 1u : 0u,
                Timeslices = 6,
                Rng = 42UL,
                StartFrom = startFrom
            },
            Physics = new PhysicsParameters { K0 = 1.0, Kd = 0.92, Delta = 0.5, Epsilon = 0.02, TargetVolume = 400 },
            Thermalization = new ThermalizationParameters { BurnInSweeps = 50, WindowSweeps = 30 },
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 2,
                MinDecorrelationSweeps = 5,
                HausdorffSamples = 2,
                HausdorffMaxRadius = 6,
                SpectralSamples = 2,
                SpectralMaxSigma = 8
            },
            Output = new OutputParameters { Directory = outDir, SaveTo = saveTo }
        };

    [Fact]
    public void Run_WithInvalidConfigOrMissingCheckpoint_ThrowsInvalidOperationException()
    {
        var workflow = new CanonicalEnsembleWorkflow();
        var missingCkpt = CreateValidConfig(_tempDir, startFrom: Path.Combine(_tempDir, "missing.cdtbin"));

        Assert.NotEmpty(workflow.Validate(missingCkpt));
        Assert.Throws<InvalidOperationException>(() => workflow.Run<SimulationHarness>(missingCkpt));
    }

    [Fact]
    public void Run_ExecutesColdSeedSavesEnsembleAndResumesFromCheckpoint()
    {
        var workflow = new CanonicalEnsembleWorkflow();
        var run1Dir = Path.Combine(_tempDir, "run1");
        var run1Cfg = CreateValidConfig(run1Dir, saveTo: "state");

        workflow.Run<SimulationHarness>(run1Cfg);

        var ckptPath = Path.Combine(run1Dir, "state.cdtbin");
        Assert.True(File.Exists(ckptPath));
        Assert.True(File.Exists(Path.Combine(run1Dir, "thermalization.csv")));
        Assert.True(File.Exists(Path.Combine(run1Dir, "volume_profile.csv")));
        Assert.True(File.Exists(Path.Combine(run1Dir, "hausdorff.csv")));
        Assert.True(File.Exists(Path.Combine(run1Dir, "spectral.csv")));

        var run2Dir = Path.Combine(_tempDir, "run2");
        var run2Cfg = CreateValidConfig(run2Dir, startFrom: ckptPath);
        workflow.Run<SimulationHarness>(run2Cfg);

        Assert.True(File.Exists(Path.Combine(run2Dir, "volume_profile.csv")));
    }
}