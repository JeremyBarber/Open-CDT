using CDT_Agnostic;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Sampling.Data;
using Open_CDT.Workflows.Jobs.Sampling.Helpers;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.Sampling;

/// <summary>
/// Orchestrates parallel, <c>&gt;= 2 * tau_int</c>-decorrelated observable sampling across a thermalized
/// replica ensemble, persisting volume-profile, Hausdorff, and spectral-dimension datasets.
/// </summary>
internal sealed partial class SamplingJob : JobBase<SamplingResult>
{
    // Hausdorff 2-parameter OLS skips UV shells r = 0..2 and requires >= 3 bulk radii (r = 3, 4, 5) for >= 1 residual DoF.
    private const int MinHausdorffMaxRadius = 5;

    // Equal-parity central differences on sigma in [2 .. sigmaMax - 1] require >= 4 points (2..5) for the 3-parameter AJL fit.
    private const int MinSpectralMaxSigma = 6;

    /// <inheritdoc/>
    public override IEnumerable<string> Validate(RunConfig config)
    {
        var s = config.Sampling;

        if (s.SamplesPerReplica <= 0)
            yield return "Sampling.SamplesPerReplica must be > 0.";
        if (s.MinDecorrelationSweeps <= 0)
            yield return "Sampling.MinDecorrelationSweeps must be > 0.";
        if (s.HausdorffSamples <= 0)
            yield return "Sampling.HausdorffSamples must be > 0.";
        if (s.HausdorffMaxRadius < MinHausdorffMaxRadius)
            yield return $"Sampling.HausdorffMaxRadius must be >= {MinHausdorffMaxRadius}.";
        if (s.SpectralSamples <= 0)
            yield return "Sampling.SpectralSamples must be > 0.";
        if (s.SpectralMaxSigma < MinSpectralMaxSigma)
            yield return $"Sampling.SpectralMaxSigma must be >= {MinSpectralMaxSigma}.";
    }

    /// <summary>
    /// Evolves all <paramref name="harnesses"/> in parallel across <see cref="SamplingParameters.SamplesPerReplica"/>
    /// decorrelated intervals and records geometric observables at each snapshot.
    /// </summary>
    public SamplingResult Execute(
        ISimulationHarness[] harnesses,
        int ensembleTauInt,
        RunConfig config,
        Action<JobProgress>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(harnesses);
        ArgumentOutOfRangeException.ThrowIfZero(harnesses.Length);

        var startTimestamp = Stopwatch.GetTimestamp();
        var replicaCount = harnesses.Length;
        var sweepsBetweenSamples = Math.Max(config.Sampling.MinDecorrelationSweeps, 2 * Math.Max(1, ensembleTauInt));
        var totalEnsembleSamples = replicaCount * config.Sampling.SamplesPerReplica;
        var globalSamplesCompleted = 0;

        var replicaBuckets = new List<ObservableSampleRecord>[replicaCount];

        Parallel.For(0, replicaCount, i =>
        {
            replicaBuckets[i] = ReplicaSampler.RunReplica(
                harnesses[i],
                (uint)i,
                replicaCount,
                sweepsBetweenSamples,
                config,
                totalEnsembleSamples,
                ref globalSamplesCompleted,
                onProgress);
        });

        var allSamples = new List<ObservableSampleRecord>(totalEnsembleSamples);
        for (var i = 0; i < replicaCount; i++)
        {
            allSamples.AddRange(replicaBuckets[i]);
        }

        var result = new SamplingResult(
            config.Simulation.SpacelikeDimensions,
            sweepsBetweenSamples,
            allSamples);

        return Complete(config, result, replicaCount, startTimestamp, onProgress);
    }
}