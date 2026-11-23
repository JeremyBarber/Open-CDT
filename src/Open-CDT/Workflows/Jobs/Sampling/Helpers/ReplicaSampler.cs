using CDT_Agnostic;
using CDT_Agnostic.Measurements.Observables;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Sampling.Data;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.Sampling.Helpers;

/// <summary>
/// Executes decorrelated MCMC evolution and observable snapshot collection for a single thermalized replica.
/// </summary>
internal static class ReplicaSampler
{
    /// <summary>
    /// Steps <paramref name="harness"/> by <paramref name="sweepsBetweenSamples"/> sweeps per interval and
    /// records volume-profile, Hausdorff, and spectral-dimension snapshots across <see cref="SamplingParameters.SamplesPerReplica"/> samples.
    /// </summary>
    public static List<ObservableSampleRecord> RunReplica(
        ISimulationHarness harness,
        uint replicaId,
        int totalReplicas,
        int sweepsBetweenSamples,
        RunConfig config,
        int totalEnsembleSamples,
        ref int completedGlobalSamples,
        Action<JobProgress>? onProgress = null)
    {
        var sampling = config.Sampling;
        var totalSamples = sampling.SamplesPerReplica;
        var timeslices = (int)config.Seed.Timeslices;
        var arenaCapacity = (int)harness.MaxSimplices;

        var volumeObservable = new VolumeProfile(timeslices);
        var hausdorffObservable = new Hausdorff(
            sampling.HausdorffSamples,
            sampling.HausdorffMaxRadius,
            arenaCapacity);
        var spectralObservable = new SpectralDimension(
            sampling.SpectralSamples,
            sampling.SpectralMaxSigma,
            arenaCapacity);

        var records = new List<ObservableSampleRecord>(totalSamples);
        var headerTitle =
            $"Sampling │ {totalEnsembleSamples} Samples │ {sweepsBetweenSamples} sw/sample";

        for (var sample = 1; sample <= totalSamples; sample++)
        {
            var start = Stopwatch.GetTimestamp();

            // 1. Run decorrelation sweeps untraced for maximum raw throughput.
            harness.Run(sweepsBetweenSamples);
            var sweepElapsedSec = Stopwatch.GetElapsedTime(start).TotalSeconds;

            // 2. Measure the three observables on the paused manifold.
            var rawProfile = harness.Measure(volumeObservable);
            var centeredProfile = CenterPeriodicProfile(rawProfile);
            var hausdorff = harness.Measure(hausdorffObservable);
            var spectral = harness.Measure(spectralObservable);

            records.Add(new ObservableSampleRecord(
                ReplicaId: replicaId,
                SampleIndex: sample,
                CenteredVolumeProfile: centeredProfile,
                Hausdorff: hausdorff,
                Spectral: spectral));

            if (onProgress is not null)
            {
                var totalVol = centeredProfile.Sum();
                var globalDone = Interlocked.Increment(ref completedGlobalSamples);
                var headerProgress = totalEnsembleSamples > 0
                    ? (double)globalDone / totalEnsembleSamples
                    : (double)sample / totalSamples;

                (var uvDs, var irDs) = SpectralFlowAnalysis.ComputeReturnCurveRange(
                    spectral.ReturnProbabilities,
                    spectral.EstimatedDimension);

                var status =
                    $"{sample,2} / {totalSamples,-2} samples   N = {totalVol,6:N0}   D_H = {hausdorff.Dimension,4:F2}   D_S = {uvDs,4:F2}→{irDs,4:F2}";

                onProgress(new JobProgress(
                    HeaderTitle: headerTitle,
                    HeaderProgress: headerProgress,
                    ReplicaId: replicaId,
                    TotalReplicas: totalReplicas,
                    Status: status,
                    CompletedSteps: sample,
                    TotalSteps: totalSamples,
                    SweepsInStep: sweepsBetweenSamples,
                    StepElapsedSeconds: sweepElapsedSec,
                    MeanVolume: totalVol,
                    ShowProgressBar: true));
            }
        }

        return records;
    }

    /// <summary>
    /// Circularly shifts a periodic <c>S^1</c> volume profile <c>N(t)</c> so that its first circular
    /// Fourier harmonic center-of-mass is aligned at <c>t = T / 2</c>, eliminating translational zero-mode smearing.
    /// </summary>
    public static int[] CenterPeriodicProfile(ReadOnlySpan<int> rawProfile)
    {
        var n = rawProfile.Length;
        if (n == 0)
            return [];

        var totalVolume = 0L;
        var sumCos = 0.0;
        var sumSin = 0.0;

        for (var t = 0; t < n; t++)
        {
            var v = rawProfile[t];
            totalVolume += v;

            var angle = 2.0 * Math.PI * t / n;
            sumCos += v * Math.Cos(angle);
            sumSin += v * Math.Sin(angle);
        }

        if (totalVolume <= 0 || double.Hypot(sumCos, sumSin) < 1e-9 * totalVolume)
            return rawProfile.ToArray();

        var meanAngle = Math.Atan2(sumSin, sumCos);
        if (meanAngle < 0.0)
            meanAngle += 2.0 * Math.PI;

        var centerSlice = (int)Math.Round(meanAngle * n / (2.0 * Math.PI)) % n;
        var targetCenter = n / 2;
        var shift = (targetCenter - centerSlice + n) % n;

        var centered = new int[n];
        for (var t = 0; t < n; t++)
        {
            centered[(t + shift) % n] = rawProfile[t];
        }

        return centered;
    }
}