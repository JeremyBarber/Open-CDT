using CDT_Agnostic;
using CDT_Agnostic.Measurements.Traces;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Helpers;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;

/// <summary>
/// Calibrates the critical cosmological constant surface <c>k_d^c(k_0, Delta)</c> via parallel
/// susceptibility root-finding and neighbor secant warm-starting.
/// </summary>
internal sealed partial class CosmologicalConstantTunerJob : JobBase<TuningResult>
{
    /// <inheritdoc/>
    public override IEnumerable<string> Validate(RunConfig config)
    {
        var t = config.Tuning;

        if (!double.IsFinite(t.ToleranceRatio) || t.ToleranceRatio <= 0.0 || t.ToleranceRatio >= 1.0)
            yield return "Tuning.ToleranceRatio must be a finite value in (0, 1).";
        if (t.PlateauSweeps < 2)
            yield return "Tuning.PlateauSweeps must be >= 2.";
        if (t.MaxSweepsPerProbe < t.PlateauSweeps)
            yield return "Tuning.MaxSweepsPerProbe must be >= Tuning.PlateauSweeps.";
        if (t.K0Steps < 1)
            yield return "Tuning.K0Steps must be >= 1.";
        if (!double.IsFinite(t.K0Low) || !double.IsFinite(t.K0High))
            yield return "Tuning.K0Low and Tuning.K0High must be finite.";
        if (t.K0Steps > 1 && t.K0High <= t.K0Low)
            yield return "Tuning.K0High must be > Tuning.K0Low when K0Steps > 1.";
        if (t.DeltaSteps < 1)
            yield return "Tuning.DeltaSteps must be >= 1.";
        if (!double.IsFinite(t.DeltaLow) || !double.IsFinite(t.DeltaHigh))
            yield return "Tuning.DeltaLow and Tuning.DeltaHigh must be finite.";
        if (t.DeltaSteps > 1 && t.DeltaHigh <= t.DeltaLow)
            yield return "Tuning.DeltaHigh must be > Tuning.DeltaLow when DeltaSteps > 1.";
    }

    /// <summary>
    /// Executes parallel critical-coupling root-finding across all configured <c>(k_0, Delta)</c> coordinates.
    /// </summary>
    public TuningResult Execute<THarness>(
        RunConfig config,
        Action<JobProgress>? onProgress = null)
        where THarness : ISimulationHarness
    {
        var startTimestamp = Stopwatch.GetTimestamp();
        var tracker = new CriticalSurfaceTracker(config);
        var totalCoords = tracker.TotalCoordinates;
        var replicaCount = Math.Clamp((int)config.Simulation.Replicas, 1, Math.Max(1, totalCoords));
        var halfSweeps = Math.Max(1, (config.Tuning.PlateauSweeps + 1) / 2);
        var showDelta = config.Tuning.DeltaSteps > 1;

        Parallel.For(0, replicaCount, workerIndex =>
        {
            var replicaId = (uint)workerIndex;
            var trace = new Volume(halfSweeps);

            while (tracker.TryClaimNext(out var item))
            {
                var coordPrefix = FormatCoordinatePrefix(item.K0, item.Delta, totalCoords, showDelta);

                using var harness = THarness.Create(
                    replicaId,
                    config.Physics with { K0 = item.K0, Delta = item.Delta, Kd = item.Kd },
                    config.Seed);

                var probeResult = VolumeConvergence.RunUntilPlateau(
                    harness,
                    trace,
                    replicaId,
                    replicaCount,
                    item.K0,
                    item.Delta,
                    item.Kd,
                    config.Physics.TargetVolume,
                    config.Tuning,
                    tracker,
                    coordPrefix,
                    onProgress);

                tracker.ReportResult(in item, probeResult);
            }
        });

        var finalResult = tracker.BuildResult();
        return Complete(config, finalResult, replicaCount, startTimestamp, onProgress);
    }

    private static string? FormatCoordinatePrefix(double k0, double delta, int totalCoords, bool showDelta)
    {
        if (totalCoords == 1)
            return null;

        return showDelta
            ? $"(k0={k0,5:F2}, Δ={delta:F2})"
            : $"k0 = {k0,5:F2}";
    }
}