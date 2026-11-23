using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;

using Xunit;
using PhaseOutput = Open_CDT.Workflows.Jobs.PhaseClassifier.Data.Output;

namespace Open_CDT_Tests.Workflows.Jobs.PhaseClassifier;

public sealed class PhaseClassifierJobTests : IDisposable
{
    private readonly string _tempDir;

    public PhaseClassifierJobTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_phase_job_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class SyntheticPhaseHarness(PhysicsParameters physics, uint timeslices) : ISimulationHarness
    {
        public uint MaxSimplices => physics.TargetVolume * 2;

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            new SyntheticPhaseHarness(physics, seed.Timeslices);

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null)
        {
            if (trace is null)
                return;

            var vol = (int)physics.TargetVolume;
            var density = physics.K0 < 3.0 ? 0.14 + (0.03 * physics.K0) : 0.245;
            var vert = (int)Math.Round(vol * density);

            for (var s = 0; s < sweepsToRun; s++)
            {
                trace.TotalSimplices[s] = vol;
                trace.TotalVertices[s] = vert + (s % 2);
            }
        }

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable)
        {
            var tCount = (int)timeslices;
            var profile = new int[tCount];

            for (var t = 0; t < tCount; t++)
            {
                profile[t] = physics.K0 < 3.0
                    ? (int)Math.Round(100.0 + (80.0 * Math.Cos(2.0 * Math.PI * t / tCount)))
                    : (t % 2 == 0 ? 160 : 40);
            }

            return (TResult)(object)profile;
        }

        public void Dispose() { }
    }

    private static PhasePointRecord CreatePoint(double k0, double delta, SpacetimePhase phase) =>
        new(
            K0: k0,
            Delta: delta,
            KdCrit: 0.85 + (0.15 * k0),
            MeanVolume: 16000.0,
            StdDevVolume: 31.6,
            VertexDensity: phase == SpacetimePhase.PhaseC ? 0.17 : 0.245,
            PeakSliceFraction: 0.09,
            TemporalContinuity: phase == SpacetimePhase.PhaseC ? 0.88 : 0.55,
            Phase: phase);

    private static RunConfig CreateConfig(string dir, int sweepsPerCoordinate = 200) =>
        new()
        {
            Simulation = new SimulationParameters
            {
                Name = "PhaseJobTest",
                Workflow = Workflow.PhaseSpaceMapper,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 1, Timeslices = 16, Rng = 42UL },
            Physics = new PhysicsParameters { K0 = 2.0, Kd = 1.0, Delta = 0.5, Epsilon = 0.0005, TargetVolume = 1600 },
            Classification = new ClassificationParameters { SweepsPerCoordinate = sweepsPerCoordinate },
            Output = new OutputParameters { Directory = dir }
        };

    [Fact]
    public void Validate_EnforcesMinimumSweepsPerCoordinate()
    {
        var job = new PhaseClassifierJob();

        Assert.Single(job.Validate(CreateConfig(_tempDir, sweepsPerCoordinate: 50)));
        Assert.Empty(job.Validate(CreateConfig(_tempDir, sweepsPerCoordinate: 200)));
    }

    [Fact]
    public void Execute_ClassifiesGridWritesCsvAndEmitsCompletionSummary()
    {
        var job = new PhaseClassifierJob();
        var config = CreateConfig(_tempDir, sweepsPerCoordinate: 200);
        var progress = new List<JobProgress>();

        CriticalPointRecord[] criticals =
        [
            new(1.0, 0.5, 0.95, 1600.0, 12.0, 272.0),
            new(2.0, 0.5, 1.10, 1600.0, 12.0, 320.0),
            new(3.0, 0.5, 1.25, 1600.0, 12.0, 392.0),
            new(4.0, 0.5, 1.40, 1600.0, 12.0, 392.0)
        ];

        var tuningResult = new TuningResult(1600, 0.005, 0.0005, criticals, []);
        var result = job.Execute<SyntheticPhaseHarness>(config, tuningResult, progress.Add);

        Assert.Equal(4, result.Points.Count);
        Assert.True(File.Exists(Path.Combine(_tempDir, "phase_space.csv")));
        Assert.Contains(progress, p => p.IsJobComplete && p.SummaryLines is { Length: 3 });
    }

    [Fact]
    public void Plot_RendersOnlyChartsSupportedByGridDimensionality()
    {
        var job = new PhaseClassifierJob();

        // 1x1 -> 0 plots.
        var dir1x1 = Path.Combine(_tempDir, "grid_1x1");
        Directory.CreateDirectory(dir1x1);
        PhaseOutput.WriteToDisk(
            CreateConfig(dir1x1),
            new PhaseSpaceResult(2, 16, 16000, [CreatePoint(2.0, 0.5, SpacetimePhase.PhaseC)]));

        var prog1x1 = new List<JobProgress>();
        job.Plot(dir1x1, prog1x1.Add);
        Assert.Empty(prog1x1);

        // 4x1 -> 3 order-parameter plots; 2D diagram skipped.
        var dir4x1 = Path.Combine(_tempDir, "grid_4x1");
        Directory.CreateDirectory(dir4x1);
        PhasePointRecord[] slicePoints =
        [
            CreatePoint(1.0, 0.5, SpacetimePhase.PhaseC),
            CreatePoint(2.0, 0.5, SpacetimePhase.PhaseC),
            CreatePoint(3.0, 0.5, SpacetimePhase.PhaseA),
            CreatePoint(4.0, 0.5, SpacetimePhase.PhaseA)
        ];
        PhaseOutput.WriteToDisk(CreateConfig(dir4x1), new PhaseSpaceResult(2, 16, 16000, slicePoints));

        var prog4x1 = new List<JobProgress>();
        job.Plot(dir4x1, prog4x1.Add);
        Assert.Equal(3, prog4x1.Count);
        Assert.False(File.Exists(Path.Combine(dir4x1, "phase_space_diagram.png")));
        Assert.True(File.Exists(Path.Combine(dir4x1, "phase_vertex_density.png")));
        Assert.True(File.Exists(Path.Combine(dir4x1, "phase_droplet_concentration.png")));
        Assert.True(File.Exists(Path.Combine(dir4x1, "phase_slice_continuity.png")));

        // 2x2 -> all 4 plots rendered.
        var dir2x2 = Path.Combine(_tempDir, "grid_2x2");
        Directory.CreateDirectory(dir2x2);
        PhasePointRecord[] gridPoints =
        [
            CreatePoint(1.0, 0.3, SpacetimePhase.PhaseC),
            CreatePoint(3.0, 0.3, SpacetimePhase.PhaseA),
            CreatePoint(1.0, 0.7, SpacetimePhase.PhaseC),
            CreatePoint(3.0, 0.7, SpacetimePhase.PhaseA)
        ];
        PhaseOutput.WriteToDisk(CreateConfig(dir2x2), new PhaseSpaceResult(2, 16, 16000, gridPoints));

        var prog2x2 = new List<JobProgress>();
        job.Plot(dir2x2, prog2x2.Add);
        Assert.Equal(4, prog2x2.Count);
        Assert.True(File.Exists(Path.Combine(dir2x2, "phase_space_diagram.png")));
    }
}