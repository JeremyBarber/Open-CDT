using CDT_Agnostic;
using CDT_Agnostic.Config;
using CDT_Agnostic.Measurements;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Sampling;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.Sampling;

public sealed class SamplingJobTests : IDisposable
{
    private readonly string _tempDir;

    public SamplingJobTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_samp_job_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private sealed class SyntheticObservableHarness(int timeslices, int maxRadius, int maxSigma) : ISimulationHarness
    {
        public uint MaxSimplices => 24_000;
        public int TotalSweepsExecuted { get; private set; }

        public static ISimulationHarness Create(uint harnessId, PhysicsParameters physics, SeedParameters seed) =>
            throw new NotSupportedException();

        public static ISimulationHarness LoadCheckpoint(uint harnessId, Stream stream, PhysicsParameters physics, ulong? reseedRng = null) =>
            throw new NotSupportedException();

        public void SaveCheckpoint(Stream stream) => throw new NotSupportedException();

        public void Run(int sweepsToRun, Volume? trace = null) =>
            TotalSweepsExecuted += sweepsToRun;

        public TResult Measure<TResult>(IManifoldObservable<TResult> observable)
        {
            if (typeof(TResult) == typeof(int[]))
            {
                var profile = new int[timeslices];
                for (var t = 0; t < timeslices; t++)
                    profile[t] = (int)Math.Round(50.0 + (450.0 * Math.Pow(Math.Cos(Math.PI * (t - (timeslices / 2.0)) / timeslices), 2)));
                return (TResult)(object)profile;
            }

            if (typeof(TResult) == typeof(HausdorffResult))
            {
                var shells = new double[maxRadius + 1];
                shells[0] = 1.0;
                for (var r = 1; r <= maxRadius; r++)
                    shells[r] = 4.0 * r * r;
                return (TResult)(object)new HausdorffResult(3.0, shells);
            }

            if (typeof(TResult) == typeof(SpectralDimensionResult))
            {
                var probs = new double[maxSigma + 1];
                probs[0] = 1.0;
                for (var s = 1; s <= maxSigma; s++)
                    probs[s] = Math.Pow(s, -1.5);
                return (TResult)(object)new SpectralDimensionResult(3.0, probs);
            }

            throw new NotSupportedException();
        }

        public void Dispose() { }
    }

    [Fact]
    public void Validate_EnforcesObservableEstimatorDegreesOfFreedom()
    {
        var job = new SamplingJob();

        var invalid = new RunConfig
        {
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 0,
                MinDecorrelationSweeps = 0,
                HausdorffSamples = 0,
                HausdorffMaxRadius = 4,
                SpectralSamples = 0,
                SpectralMaxSigma = 5
            }
        };
        Assert.Equal(6, job.Validate(invalid).Count());

        var valid = new RunConfig
        {
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 2,
                MinDecorrelationSweeps = 10,
                HausdorffSamples = 4,
                HausdorffMaxRadius = 5,
                SpectralSamples = 4,
                SpectralMaxSigma = 6
            }
        };
        Assert.Empty(job.Validate(valid));
    }

    [Fact]
    public void Execute_And_Plot_EnforceTwoTauDecorrelationWriteThreeCsvsAndRenderEightCharts()
    {
        const int timeslices = 16;
        const int maxRadius = 10;
        const int maxSigma = 16;

        var config = new RunConfig
        {
            Simulation = new SimulationParameters
            {
                Name = "SamplingJobTest",
                Workflow = Workflow.CanonicalEnsemble,
                Replicas = 2,
                SpacelikeDimensions = 2
            },
            Seed = new SeedParameters { K = 1, Timeslices = timeslices, Rng = 42UL },
            Sampling = new SamplingParameters
            {
                SamplesPerReplica = 3,
                MinDecorrelationSweeps = 20,
                HausdorffSamples = 4,
                HausdorffMaxRadius = maxRadius,
                SpectralSamples = 4,
                SpectralMaxSigma = maxSigma
            },
            Output = new OutputParameters { Directory = _tempDir }
        };

        using var h0 = new SyntheticObservableHarness(timeslices, maxRadius, maxSigma);
        using var h1 = new SyntheticObservableHarness(timeslices, maxRadius, maxSigma);
        ISimulationHarness[] harnesses = [h0, h1];

        var job = new SamplingJob();
        var progress = new List<JobProgress>();

        // ensembleTauInt = 15 -> 2 * 15 = 30 overrides MinDecorrelationSweeps = 20.
        var result = job.Execute(harnesses, ensembleTauInt: 15, config, progress.Add);

        Assert.Equal(30, result.SweepsBetweenSamples);
        Assert.Equal(6, result.Samples.Count);
        Assert.Equal(90, h0.TotalSweepsExecuted);
        Assert.Equal(90, h1.TotalSweepsExecuted);
        Assert.Contains(progress, p => p.IsJobComplete && p.SummaryLines is { Length: 3 });

        var plotProgress = new List<JobProgress>();
        job.Plot(_tempDir, plotProgress.Add);

        Assert.Equal(8, plotProgress.Count);
        Assert.True(File.Exists(Path.Combine(_tempDir, "volume_profile_ensemble.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "volume_profile_replicas.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "volume_profile_harmonics.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "hausdorff_scaling_loglog.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "hausdorff_replicas.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "spectral_dimension_running.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "spectral_return_probability.png")));
        Assert.True(File.Exists(Path.Combine(_tempDir, "spectral_replicas.png")));
    }
}