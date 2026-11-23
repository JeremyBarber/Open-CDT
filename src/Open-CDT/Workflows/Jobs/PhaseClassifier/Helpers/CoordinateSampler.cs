using CDT_Agnostic;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT.Statistics;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;

/// <summary>
/// Equilibrates a single critical-surface coordinate <c>(k_0, Delta, k_d^c)</c>, calibrates its local
/// integrated autocorrelation time <c>tau_int</c>, and samples decorrelated foliation order parameters.
/// </summary>
internal static class CoordinateSampler
{
    private const int TelemetryBurstSweeps = 100;

    /// <summary>
    /// Evolves <paramref name="harness"/> for <paramref name="totalSweeps"/> sweeps: relaxes the cold seed over Q1,
    /// calibrates Madras-Sokal <c>tau_int</c> over Q2, and samples <see cref="VolumeProfile"/> at
    /// <c>&gt;= 2 * tau_int</c> intervals across the second half.
    /// </summary>
    public static PhasePointRecord MeasureCoordinate(
        ISimulationHarness harness,
        CriticalPointRecord criticalPoint,
        Volume trace,
        VolumeProfile volumeObservable,
        uint replicaId,
        int totalReplicas,
        int totalCoordinates,
        int totalSweeps,
        bool showDelta,
        ref int completedCoordinates,
        ref long completedGlobalSweeps,
        Action<JobProgress>? onProgress = null)
    {
        var burnInSweeps = totalSweeps / 2;
        var relaxSweeps = burnInSweeps / 2;
        var windowSweeps = burnInSweeps - relaxSweeps;
        var samplingSweeps = totalSweeps - burnInSweeps;
        var stepCap = Math.Min(TelemetryBurstSweeps, trace.TotalSimplices.Length);

        var coordLabel = showDelta
            ? $"(k0={criticalPoint.K0,5:F2}, Δ={criticalPoint.Delta:F2})"
            : $"k0 = {criticalPoint.K0,5:F2}";

        var sweepsDone = 0;

        // 1. Q1: Cold-seed relaxation.
        if (onProgress is null)
        {
            if (relaxSweeps > 0)
            {
                harness.Run(relaxSweeps);
                sweepsDone += relaxSweeps;
            }
        }
        else
        {
            var relaxed = 0;
            while (relaxed < relaxSweeps)
            {
                var burst = Math.Min(stepCap, relaxSweeps - relaxed);
                var start = Stopwatch.GetTimestamp();
                harness.Run(burst, trace);
                var elapsedSec = Stopwatch.GetElapsedTime(start).TotalSeconds;

                relaxed += burst;
                sweepsDone += burst;

                (var burstMeanVol, _) = SeriesStatistics.ComputePopulation<int>(
                    trace.TotalSimplices.AsSpan(0, burst));

                ReportProgress(
                    onProgress,
                    coordLabel,
                    criticalPoint.KdCrit,
                    replicaId,
                    totalReplicas,
                    totalCoordinates,
                    totalSweeps,
                    sweepsDone,
                    burst,
                    elapsedSec,
                    burstMeanVol,
                    "↗ Relaxing seed...",
                    ref completedCoordinates,
                    ref completedGlobalSweeps);
            }
        }

        // 2. Q2: Stationary burn-in window to calibrate local Madras-Sokal tau_int.
        var windowVertices = new int[windowSweeps];
        var sampleInterval = 1;

        if (onProgress is null && trace.TotalSimplices.Length >= windowSweeps)
        {
            harness.Run(windowSweeps, trace);
            trace.TotalVertices.AsSpan(0, windowSweeps).CopyTo(windowVertices);
            sweepsDone += windowSweeps;

            var acf = MadrasSokalAutocorrelation.Compute(windowVertices);
            sampleInterval = Math.Clamp(2 * acf.TauInt, 1, samplingSweeps);
        }
        else
        {
            var windowRecorded = 0;
            while (windowRecorded < windowSweeps)
            {
                var burst = Math.Min(stepCap, windowSweeps - windowRecorded);
                var start = Stopwatch.GetTimestamp();
                harness.Run(burst, trace);
                var elapsedSec = Stopwatch.GetElapsedTime(start).TotalSeconds;

                var burstSimplices = trace.TotalSimplices.AsSpan(0, burst);
                trace.TotalVertices.AsSpan(0, burst).CopyTo(windowVertices.AsSpan(windowRecorded, burst));

                windowRecorded += burst;
                sweepsDone += burst;

                var stageLabel = "↗ Measuring τ_int...";
                if (windowRecorded == windowSweeps)
                {
                    var acf = MadrasSokalAutocorrelation.Compute(windowVertices);
                    sampleInterval = Math.Clamp(2 * acf.TauInt, 1, samplingSweeps);
                    stageLabel = $"● Sampling (2τ={sampleInterval})";
                }

                if (onProgress is not null)
                {
                    (var burstMeanVol, _) = SeriesStatistics.ComputePopulation<int>(burstSimplices);

                    ReportProgress(
                        onProgress,
                        coordLabel,
                        criticalPoint.KdCrit,
                        replicaId,
                        totalReplicas,
                        totalCoordinates,
                        totalSweeps,
                        sweepsDone,
                        burst,
                        elapsedSec,
                        burstMeanVol,
                        stageLabel,
                        ref completedCoordinates,
                        ref completedGlobalSweeps);
                }
            }
        }

        // 3. Q3-Q4: Decorrelated >= 2 * tau_int observable sampling over the second half.
        var targetSamples = samplingSweeps / sampleInterval;
        var remainder = samplingSweeps - (targetSamples * sampleInterval);

        var sampledSimplices = new int[samplingSweeps];
        var sampledVertices = new int[samplingSweeps];
        var recordedSweeps = 0;

        var sumPeakFraction = 0.0;
        var sumContinuity = 0.0;

        for (var s = 1; s <= targetSamples; s++)
        {
            var burstSweeps = s == 1 ? sampleInterval + remainder : sampleInterval;

            var stepStart = Stopwatch.GetTimestamp();
            harness.Run(burstSweeps, trace);
            var stepSec = Stopwatch.GetElapsedTime(stepStart).TotalSeconds;

            var burstSimplices = trace.TotalSimplices.AsSpan(0, burstSweeps);
            var burstVertices = trace.TotalVertices.AsSpan(0, burstSweeps);

            burstSimplices.CopyTo(sampledSimplices.AsSpan(recordedSweeps, burstSweeps));
            burstVertices.CopyTo(sampledVertices.AsSpan(recordedSweeps, burstSweeps));
            recordedSweeps += burstSweeps;
            sweepsDone += burstSweeps;

            var profile = harness.Measure(volumeObservable);
            (var peakFraction, var continuity) = AnalyzeSliceProfile(profile);
            sumPeakFraction += peakFraction;
            sumContinuity += continuity;

            if (onProgress is not null)
            {
                (var burstMeanVol, _) = SeriesStatistics.ComputePopulation<int>(burstSimplices);
                (var burstMeanVert, _) = SeriesStatistics.ComputePopulation<int>(burstVertices);
                var currentDensity = burstMeanVol > 0.0 ? burstMeanVert / burstMeanVol : 0.0;

                ReportProgress(
                    onProgress,
                    coordLabel,
                    criticalPoint.KdCrit,
                    replicaId,
                    totalReplicas,
                    totalCoordinates,
                    totalSweeps,
                    sweepsDone,
                    burstSweeps,
                    stepSec,
                    burstMeanVol,
                    $"● N0/N = {currentDensity:F3}",
                    ref completedCoordinates,
                    ref completedGlobalSweeps);
            }
        }

        (var meanVolume, var stdVolume) = SeriesStatistics.ComputePopulation<int>(sampledSimplices);
        (var meanVertices, _) = SeriesStatistics.ComputePopulation<int>(sampledVertices);
        var vertexDensity = meanVolume > 0.0 ? meanVertices / meanVolume : criticalPoint.VertexDensity;

        return new PhasePointRecord(
            K0: criticalPoint.K0,
            Delta: criticalPoint.Delta,
            KdCrit: criticalPoint.KdCrit,
            MeanVolume: meanVolume,
            StdDevVolume: stdVolume,
            VertexDensity: vertexDensity,
            PeakSliceFraction: sumPeakFraction / targetSamples,
            TemporalContinuity: sumContinuity / targetSamples,
            Phase: SpacetimePhase.PhaseC);
    }

    /// <summary>
    /// Computes single-slice peak fraction <c>O_max = max_t N_d(t) / N_d</c> and periodic inter-slice
    /// smoothness <c>C_time = 1 - sum_t |N_d(t+1) - N_d(t)| / (2 N_d)</c> for a proper-time volume profile.
    /// </summary>
    public static (double PeakSliceFraction, double TemporalContinuity) AnalyzeSliceProfile(
        ReadOnlySpan<int> profile)
    {
        var n = profile.Length;
        var total = 0L;
        var maxSlice = 0;
        var totalDiff = 0L;

        for (var t = 0; t < n; t++)
        {
            var v = profile[t];
            total += v;
            if (v > maxSlice)
                maxSlice = v;

            var next = profile[(t + 1) % n];
            totalDiff += Math.Abs((long)next - v);
        }

        if (total <= 0)
            return (0.0, 0.0);

        var peakFraction = (double)maxSlice / total;
        var continuity = Math.Clamp(1.0 - ((double)totalDiff / (2.0 * total)), 0.0, 1.0);
        return (peakFraction, continuity);
    }

    private static void ReportProgress(
        Action<JobProgress>? onProgress,
        string coordLabel,
        double kdCrit,
        uint replicaId,
        int totalReplicas,
        int totalCoordinates,
        int totalSweeps,
        int sweepsDone,
        int burstSweeps,
        double elapsedSec,
        double meanVol,
        string stageLabel,
        ref int completedCoordinates,
        ref long completedGlobalSweeps)
    {
        if (onProgress is null)
            return;

        var totalEnsembleSweeps = Math.Max(1L, (long)Math.Max(1, totalCoordinates) * totalSweeps);
        var globalDone = burstSweeps > 0
            ? Interlocked.Add(ref completedGlobalSweeps, burstSweeps)
            : Volatile.Read(ref completedGlobalSweeps);
        var doneCoords = Volatile.Read(ref completedCoordinates);

        var headerTitle = totalCoordinates > 1
            ? $"Classifying Phases │ {doneCoords} / {totalCoordinates} │ {totalSweeps / 1000.0:F1}k sw/pt"
            : $"Classifying Phases │ {coordLabel}";
        var headerProgress = totalCoordinates > 1
            ? (double)globalDone / totalEnsembleSweeps
            : (double)sweepsDone / totalSweeps;

        onProgress(new JobProgress(
            HeaderTitle: headerTitle,
            HeaderProgress: headerProgress,
            ReplicaId: replicaId,
            TotalReplicas: totalReplicas,
            Status: $"{coordLabel}  kd={kdCrit:F5}  N={meanVol,6:N0}  {stageLabel}",
            CompletedSteps: sweepsDone,
            TotalSteps: totalSweeps,
            SweepsInStep: burstSweeps,
            StepElapsedSeconds: elapsedSec,
            MeanVolume: meanVol,
            ShowProgressBar: true));
    }
}