using CDT_Agnostic;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT.Statistics;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Helpers;

/// <summary>
/// Drives a single <see cref="ISimulationHarness"/> in half-plateau sweep bursts until two consecutive
/// halves agree within thermal fluctuations or cross the arena headroom midpoint.
/// </summary>
internal static class VolumeConvergence
{
    /// <summary>
    /// Evolves <paramref name="harness"/> at fixed couplings until a stationary two-half volume plateau is confirmed,
    /// the overvolume headroom midpoint is crossed, or <see cref="TuningParameters.MaxSweepsPerProbe"/> is reached.
    /// </summary>
    public static ProbeRecord RunUntilPlateau(
        ISimulationHarness harness,
        Volume trace,
        uint replicaId,
        int totalReplicas,
        double k0,
        double delta,
        double kdTested,
        uint targetVolume,
        TuningParameters tuning,
        CriticalSurfaceTracker? tracker = null,
        string? coordinatePrefix = null,
        Action<JobProgress>? onProgress = null)
    {
        var halfSweeps = Math.Max(1, (tuning.PlateauSweeps + 1) / 2);

        var tolerance = targetVolume * tuning.ToleranceRatio;
        var minTarget = targetVolume - tolerance;
        var maxTarget = targetVolume + tolerance;
        var safetyCeiling = harness.MaxSimplices > maxTarget
            ? 0.5 * (maxTarget + harness.MaxSimplices)
            : double.PositiveInfinity;

        var hasPrevious = false;
        var prevMean = 0.0;
        var prevStdDev = 0.0;
        var prevVertices = 0.0;
        var prevCount = 0;

        var plateauMean = 0.0;
        var plateauStdDev = 0.0;
        var plateauVertices = 0.0;

        var totalSweepsRun = 0;
        while (totalSweepsRun < tuning.MaxSweepsPerProbe)
        {
            var sweepsToRun = Math.Min(halfSweeps, tuning.MaxSweepsPerProbe - totalSweepsRun);
            totalSweepsRun += sweepsToRun;

            var startTimestamp = Stopwatch.GetTimestamp();
            harness.Run(sweepsToRun, trace);
            var stepSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;

            (var currentMean, var currentStdDev) = SeriesStatistics.ComputePopulation<int>(
                trace.TotalSimplices.AsSpan(0, sweepsToRun));
            (var currentVertices, _) = SeriesStatistics.ComputePopulation<int>(
                trace.TotalVertices.AsSpan(0, sweepsToRun));

            // Floor at 1.0 simplex (the discrete integer lattice quantum) so zero-variance floor plateaus converge cleanly.
            var stationarityLimit = Math.Max(1.0, Math.Min(prevStdDev, currentStdDev));
            var isStationary = hasPrevious && Math.Abs(currentMean - prevMean) <= stationarityLimit;

            int stableSweeps;
            if (isStationary)
            {
                (plateauMean, plateauStdDev, plateauVertices, stableSweeps) = PoolHalves(
                    prevMean, prevStdDev, prevVertices, prevCount,
                    currentMean, currentStdDev, currentVertices, sweepsToRun);
            }
            else
            {
                plateauMean = currentMean;
                plateauStdDev = currentStdDev;
                plateauVertices = currentVertices;
                stableSweeps = sweepsToRun;
            }

            var clampedStableSweeps = Math.Min(stableSweeps, tuning.PlateauSweeps);

            ProbeOutcome? finishedOutcome = null;
            string phaseBadge;

            var endSimplices = trace.TotalSimplices[sweepsToRun - 1];

            if (currentMean >= safetyCeiling || endSimplices >= safetyCeiling)
            {
                plateauMean = Math.Max(plateauMean, endSimplices);
                phaseBadge = $"▲ Ceiling ({FormatK(totalSweepsRun)})";
                finishedOutcome = ProbeOutcome.AboveTarget;
            }
            else if (!isStationary)
            {
                var climbing = !hasPrevious ? currentMean >= targetVolume : currentMean >= prevMean;
                phaseBadge = climbing ? "↗ Equilibrating" : "↘ Equilibrating";
            }
            else
            {
                var windowComplete = stableSweeps >= tuning.PlateauSweeps;

                if (plateauMean >= minTarget && plateauMean <= maxTarget)
                {
                    phaseBadge = $"● Holding ({FormatK(clampedStableSweeps)}/{FormatK(tuning.PlateauSweeps)})";
                    if (windowComplete)
                        finishedOutcome = ProbeOutcome.LockedOnTarget;
                }
                else if (plateauMean > maxTarget)
                {
                    phaseBadge = $"▲ Above ({FormatK(clampedStableSweeps)}/{FormatK(tuning.PlateauSweeps)})";
                    if (windowComplete)
                        finishedOutcome = ProbeOutcome.AboveTarget;
                }
                else
                {
                    phaseBadge = $"▼ Below ({FormatK(clampedStableSweeps)}/{FormatK(tuning.PlateauSweeps)})";
                    if (windowComplete)
                        finishedOutcome = ProbeOutcome.BelowTarget;
                }
            }

            hasPrevious = true;
            prevMean = currentMean;
            prevStdDev = currentStdDev;
            prevVertices = currentVertices;
            prevCount = sweepsToRun;

            if (onProgress is not null)
            {
                var statusLine = coordinatePrefix is null
                    ? $"kd = {kdTested:F5}   N = {plateauMean,6:N0} ± {plateauStdDev,-4:F0}  {phaseBadge,-26}  {FormatK(totalSweepsRun),5} sw"
                    : $"{coordinatePrefix}  kd={kdTested:F5}  N={plateauMean,6:N0}  {phaseBadge}";

                string header;
                double headerProgress;
                if (tracker is not null && tracker.TotalCoordinates > 1)
                {
                    var done = tracker.CompletedCoordinates;
                    var total = tracker.TotalCoordinates;
                    header = $"Tuning Critical Sheet │ {done} / {total} │ Target N={targetVolume:N0}";
                    headerProgress = (double)done / total;
                }
                else
                {
                    header = $"Tuning Critical Coupling │ Target N={targetVolume:N0} ± {tolerance:F0}";
                    headerProgress = -1.0;
                }

                onProgress(new JobProgress(
                    HeaderTitle: header,
                    HeaderProgress: headerProgress,
                    ReplicaId: replicaId,
                    TotalReplicas: totalReplicas,
                    Status: statusLine,
                    CompletedSteps: clampedStableSweeps,
                    TotalSteps: tuning.PlateauSweeps,
                    SweepsInStep: sweepsToRun,
                    StepElapsedSeconds: stepSeconds,
                    MeanVolume: plateauMean,
                    ShowProgressBar: coordinatePrefix is not null));
            }

            if (finishedOutcome is ProbeOutcome outcome)
            {
                return new ProbeRecord(
                    k0,
                    delta,
                    kdTested,
                    plateauMean,
                    plateauStdDev,
                    plateauVertices,
                    outcome);
            }
        }

        var timeoutOutcome = plateauMean >= minTarget && plateauMean <= maxTarget
            ? ProbeOutcome.LockedOnTarget
            : (plateauMean < targetVolume ? ProbeOutcome.BelowTarget : ProbeOutcome.AboveTarget);

        return new ProbeRecord(
            k0,
            delta,
            kdTested,
            plateauMean,
            plateauStdDev,
            plateauVertices,
            timeoutOutcome);
    }

    private static (double MeanVolume, double StdDevVolume, double MeanVertices, int TotalSweeps) PoolHalves(
        double meanA, double stdA, double vertA, int countA,
        double meanB, double stdB, double vertB, int countB)
    {
        var total = countA + countB;
        var grandMean = ((countA * meanA) + (countB * meanB)) / total;
        var grandVertices = ((countA * vertA) + (countB * vertB)) / total;

        var diffA = meanA - grandMean;
        var diffB = meanB - grandMean;
        var pooledVar = (
            (countA * ((stdA * stdA) + (diffA * diffA))) +
            (countB * ((stdB * stdB) + (diffB * diffB)))) / total;

        return (grandMean, Math.Sqrt(pooledVar), grandVertices, total);
    }

    private static string FormatK(int sweeps) => $"{sweeps / 1000.0:F1}k";
}