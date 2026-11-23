using CDT_Agnostic;
using CDT_Agnostic.Measurements.Observables;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT.Config;
using Open_CDT.Statistics;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier;

/// <summary>
/// Orchestrates parallel equilibration across calibrated critical-surface coordinates <c>(k_0, Delta, k_d^c)</c>,
/// classifies macroscopic spacetime phases, and renders phase-space diagnostic plots.
/// </summary>
internal sealed partial class PhaseClassifierJob : JobBase<PhaseSpaceResult>
{
    // Q2 stationary autocorrelation window spans 1/4 of SweepsPerCoordinate (Q1 relax, Q2 tau_int, Q3-Q4 sample).
    private const int MinClassificationSweeps = 4 * MadrasSokalAutocorrelation.MinWindowSweeps;

    /// <inheritdoc/>
    public override IEnumerable<string> Validate(RunConfig config)
    {
        if (config.Classification.SweepsPerCoordinate < MinClassificationSweeps)
            yield return $"Classification.SweepsPerCoordinate must be >= {MinClassificationSweeps}.";
    }

    /// <summary>
    /// Evolves and samples all calibrated critical coordinates in parallel, classifies their spacetime phases,
    /// and persists <c>phase_space.csv</c>.
    /// </summary>
    public PhaseSpaceResult Execute<THarness>(
        RunConfig config,
        TuningResult tuningResult,
        Action<JobProgress>? onProgress = null)
        where THarness : ISimulationHarness
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var criticalPoints = tuningResult.CriticalPoints;
        var totalCoords = criticalPoints.Count;
        var replicaCount = Math.Clamp((int)config.Simulation.Replicas, 1, Math.Max(1, totalCoords));

        var totalSweepsPerPoint = config.Classification.SweepsPerCoordinate;
        var maxBurstSweeps = totalSweepsPerPoint - (totalSweepsPerPoint / 2);

        var completedGlobalSweeps = 0L;
        var completedCoords = 0;
        var nextCoordIndex = -1;

        var rawPoints = new PhasePointRecord[totalCoords];
        var timeslices = (int)config.Seed.Timeslices;
        var showDelta = criticalPoints.Select(c => c.Delta).Distinct().Count() > 1;

        Parallel.For(0, replicaCount, workerIndex =>
        {
            var replicaId = (uint)workerIndex;
            var trace = new Volume(maxBurstSweeps);
            var volumeObservable = new VolumeProfile(timeslices);

            while (true)
            {
                var idx = Interlocked.Increment(ref nextCoordIndex);
                if (idx >= totalCoords)
                    break;

                var cp = criticalPoints[idx];

                using var harness = THarness.Create(
                    replicaId,
                    config.Physics with { K0 = cp.K0, Delta = cp.Delta, Kd = cp.KdCrit },
                    config.Seed);

                rawPoints[idx] = CoordinateSampler.MeasureCoordinate(
                    harness,
                    cp,
                    trace,
                    volumeObservable,
                    replicaId,
                    replicaCount,
                    totalCoords,
                    totalSweepsPerPoint,
                    showDelta,
                    ref completedCoords,
                    ref completedGlobalSweeps,
                    onProgress);

                Interlocked.Increment(ref completedCoords);
            }
        });

        var classifiedPoints = PhaseBoundaryClassifier.ClassifyAllPoints(
            rawPoints,
            config.Simulation.SpacelikeDimensions);

        var finalResult = new PhaseSpaceResult(
            config.Simulation.SpacelikeDimensions,
            config.Seed.Timeslices,
            config.Physics.TargetVolume,
            classifiedPoints);

        return Complete(config, finalResult, replicaCount, startTimestamp, onProgress);
    }
}