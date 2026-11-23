using CDT_Agnostic.Config;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows;

using Xunit;

namespace Open_CDT_Tests.Workflows;

public sealed class IWorkflowTests
{
    private static RunConfig CreateValidCoreConfig() =>
        new()
        {
            Simulation = new SimulationParameters
            {
                Name = "ValidRun",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = 4,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 2, Timeslices = 16, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 2.0, Kd = 1.0, Delta = 0.5, Epsilon = 0.0005, TargetVolume = 8000 }
        };

    [Theory]
    [InlineData(2u)]
    [InlineData(3u)]
    public void ValidateCoreConfig_AcceptsValid2DAnd3DConfigs(uint spacelikeDimensions)
    {
        var cfg = CreateValidCoreConfig();
        cfg = cfg with { Simulation = cfg.Simulation with { SpacelikeDimensions = spacelikeDimensions } };

        Assert.Empty(IWorkflow.ValidateCoreConfig(cfg));
    }

    [Fact]
    public void ValidateCoreConfig_FlagsEveryCoreViolationIncludingNonFiniteCouplings()
    {
        var invalid = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = " ",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = 0,
                SpacelikeDimensions = 1
            },
            Seed = new SeedParameters { K = 0, Timeslices = 2, Rng = 42UL },
            Physics = new PhysicsParameters
            {
                K0 = double.NaN,
                Kd = 1.0,
                Delta = 0.5,
                Epsilon = 0.0,
                TargetVolume = 0
            }
        };

        var errors = IWorkflow.ValidateCoreConfig(invalid).ToArray();
        Assert.Equal(8, errors.Length);
    }

    [Fact]
    public void ValidateCoreConfig_WhenStartingFromCheckpoint_AllowsZeroSeedSubdivisions()
    {
        var cfg = CreateValidCoreConfig();
        cfg = cfg with { Seed = cfg.Seed with { K = 0, StartFrom = "checkpoint.cdtbin" } };

        Assert.Empty(IWorkflow.ValidateCoreConfig(cfg));
    }
}