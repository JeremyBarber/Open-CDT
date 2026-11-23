using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.PhaseClassifier;

using Xunit;

namespace Open_CDT_Tests.Workflows;

public sealed class PhaseSpaceMapperWorkflowTests : IDisposable
{
    private readonly string _tempDir;

    public PhaseSpaceMapperWorkflowTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_mapper_wf_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class FastMapperHarness(PhysicsParameters physics, uint timeslices) : ISimulationHarness
    {
        public uint MaxSimplices => physics.TargetVolume * 2;

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            new FastMapperHarness(physics, seed.Timeslices);

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null)
        {
            if (trace is null)
                return;

            var kdCrit = 0.8 + (0.1 * physics.K0);
            var slope = -1.0 / (2.0 * physics.Epsilon);
            var eqVol = (int)Math.Clamp(Math.Round(physics.TargetVolume + ((physics.Kd - kdCrit) * slope)), 100, 100000);
            var eqVert = (int)Math.Round(eqVol * 0.18);

            for (var s = 0; s < sweepsToRun; s++)
            {
                trace.TotalSimplices[s] = eqVol;
                trace.TotalVertices[s] = eqVert + (s % 2);
            }
        }

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable)
        {
            var profile = new int[(int)timeslices];
            Array.Fill(profile, (int)(physics.TargetVolume / timeslices));
            return (TResult)(object)profile;
        }

        public void Dispose() { }
    }

    private RunConfig CreateValidMapperConfig() =>
        new()
        {
            Simulation = new SimulationParameters
            {
                Name = "MapperTest",
                Workflow = Workflow.PhaseSpaceMapper,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 1, Timeslices = 8, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 2.0, Kd = 0.0, Delta = 0.5, Epsilon = 0.0005, TargetVolume = 1600 },
            Tuning = new TuningParameters
            {
                ToleranceRatio = 0.005,
                PlateauSweeps = 20,
                MaxSweepsPerProbe = 100,
                K0Low = 1.5,
                K0High = 2.5,
                K0Steps = 2,
                DeltaSteps = 1
            },
            Classification = new ClassificationParameters { SweepsPerCoordinate = 120 },
            Output = new OutputParameters { Directory = _tempDir }
        };

    [Fact]
    public void Validate_RejectsCheckpointParametersOnPhaseSpaceMapper()
    {
        var workflow = new PhaseSpaceMapperWorkflow();
        var cfg = CreateValidMapperConfig();
        cfg = cfg with
        {
            Seed = cfg.Seed with { StartFrom = "in.cdtbin" },
            Output = cfg.Output with { SaveTo = "out.cdtbin" }
        };

        var errors = workflow.Validate(cfg).ToArray();
        Assert.Equal(2, errors.Length);
        Assert.Throws<InvalidOperationException>(() => workflow.Run<FastMapperHarness>(cfg));
    }

    [Fact]
    public void Run_ExecutesTuningAndClassificationPipelineAndWritesBothCsvs()
    {
        var workflow = new PhaseSpaceMapperWorkflow();
        var cfg = CreateValidMapperConfig();
        var progress = new List<JobProgress>();

        workflow.Run<FastMapperHarness>(cfg, progress.Add);

        Assert.True(File.Exists(Path.Combine(_tempDir, "tuning.csv")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "phase_space.csv")));
        Assert.Equal(2, progress.Count(p => p.IsJobComplete));
    }
}