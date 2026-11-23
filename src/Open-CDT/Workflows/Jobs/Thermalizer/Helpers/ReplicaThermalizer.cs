using CDT_Agnostic;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT.Statistics;
using Open_CDT.Workflows.Jobs.Thermalizer.Data;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.Thermalizer.Helpers;

/// <summary>
/// Evolves a single <see cref="ISimulationHarness"/> replica through deterministic burn-in and
/// evaluates equilibrium means and Madras-Sokal <c>tau_int</c> over the trailing sweep window.
/// </summary>
internal static class ReplicaThermalizer
{
    private const int TelemetryBurstSweeps = 100;

    /// <summary>
    /// Runs <paramref name="burnInSweeps"/> total sweeps on <paramref name="harness"/>, recording
    /// per-sweep telemetry across the final <paramref name="windowSweeps"/> sweeps.
    /// </summary>
    public static ReplicaThermalizationRecord RunReplica(
        ISimulationHarness harness,
        uint replicaId,
        int totalReplicas,
        int burnInSweeps,
        int windowSweeps,
        string headerTitle,
        long totalEnsembleSweeps,
        ref long completedGlobalSweeps,
        Action<JobProgress>? onProgress = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(windowSweeps, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(burnInSweeps, windowSweeps);

        var relaxSweeps = burnInSweeps - windowSweeps;
        var trace = new Volume(Math.Min(TelemetryBurstSweeps, burnInSweeps));
        var completedSweeps = 0;

        // 1. Initial burn-in relaxation prior to the equilibrium measurement window.
        if (onProgress is null)
        {
            if (relaxSweeps > 0)
            {
                harness.Run(relaxSweeps);
                completedSweeps = relaxSweeps;
            }
        }
        else
        {
            while (completedSweeps < relaxSweeps)
            {
                var sweepsToRun = Math.Min(TelemetryBurstSweeps, relaxSweeps - completedSweeps);
                var start = Stopwatch.GetTimestamp();
                harness.Run(sweepsToRun, trace);
                var elapsedSec = Stopwatch.GetElapsedTime(start).TotalSeconds;

                completedSweeps += sweepsToRun;
                ReportBurst(
                    onProgress,
                    trace,
                    replicaId,
                    totalReplicas,
                    burnInSweeps,
                    completedSweeps,
                    sweepsToRun,
                    elapsedSec,
                    headerTitle,
                    totalEnsembleSweeps,
                    ref completedGlobalSweeps);
            }
        }

        // 2. Trailing equilibrium window for <N>, <N_0>, and Madras-Sokal tau_int.
        var windowSimplices = new int[windowSweeps];
        var windowVertices = new int[windowSweeps];
        var windowRecorded = 0;

        while (windowRecorded < windowSweeps)
        {
            var sweepsToRun = Math.Min(TelemetryBurstSweeps, windowSweeps - windowRecorded);
            var start = Stopwatch.GetTimestamp();
            harness.Run(sweepsToRun, trace);
            var elapsedSec = Stopwatch.GetElapsedTime(start).TotalSeconds;

            trace.TotalSimplices.AsSpan(0, sweepsToRun).CopyTo(windowSimplices.AsSpan(windowRecorded, sweepsToRun));
            trace.TotalVertices.AsSpan(0, sweepsToRun).CopyTo(windowVertices.AsSpan(windowRecorded, sweepsToRun));

            windowRecorded += sweepsToRun;
            completedSweeps += sweepsToRun;

            ReportBurst(
                onProgress,
                trace,
                replicaId,
                totalReplicas,
                burnInSweeps,
                completedSweeps,
                sweepsToRun,
                elapsedSec,
                headerTitle,
                totalEnsembleSweeps,
                ref completedGlobalSweeps);
        }

        (var meanSimplices, _) = SeriesStatistics.ComputePopulation<int>(windowSimplices);
        (var meanVertices, _) = SeriesStatistics.ComputePopulation<int>(windowVertices);
        var acf = MadrasSokalAutocorrelation.Compute(windowVertices);

        return new ReplicaThermalizationRecord(
            ReplicaId: replicaId,
            MeanSimplices: meanSimplices,
            MeanVertices: meanVertices,
            TauInt: acf.TauInt,
            TauConverged: acf.Converged);
    }

    private static void ReportBurst(
        Action<JobProgress>? onProgress,
        Volume trace,
        uint replicaId,
        int totalReplicas,
        int burnInSweeps,
        int completedSweeps,
        int sweepsInBurst,
        double elapsedSec,
        string headerTitle,
        long totalEnsembleSweeps,
        ref long completedGlobalSweeps)
    {
        if (onProgress is null)
            return;

        (var batchMeanSimplices, _) = SeriesStatistics.ComputePopulation<int>(
            trace.TotalSimplices.AsSpan(0, sweepsInBurst));
        (var batchMeanVertices, _) = SeriesStatistics.ComputePopulation<int>(
            trace.TotalVertices.AsSpan(0, sweepsInBurst));

        var globalDone = Interlocked.Add(ref completedGlobalSweeps, sweepsInBurst);
        var headerProgress = totalEnsembleSweeps > 0
            ? (double)globalDone / totalEnsembleSweeps
            : (double)completedSweeps / burnInSweeps;

        var status =
            $"{completedSweeps / 1000.0,5:F1}k / {burnInSweeps / 1000.0:F1}k sw   N = {batchMeanSimplices,6:N0}   N0 = {batchMeanVertices,5:N0}";

        onProgress(new JobProgress(
            HeaderTitle: headerTitle,
            HeaderProgress: headerProgress,
            ReplicaId: replicaId,
            TotalReplicas: totalReplicas,
            Status: status,
            CompletedSteps: completedSweeps,
            TotalSteps: burnInSweeps,
            SweepsInStep: sweepsInBurst,
            StepElapsedSeconds: elapsedSec,
            MeanVolume: batchMeanSimplices,
            ShowProgressBar: true));
    }
}