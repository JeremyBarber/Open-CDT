using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;

using System.Collections.Concurrent;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Helpers;

/// <summary>
/// Represents a single queued <c>(k_0, Delta, k_d)</c> probe evaluation and its safeguarded root-finding bounds.
/// </summary>
internal readonly record struct ProbeWorkItem(
    double K0,
    double Delta,
    double Kd,
    double LowBound = double.NegativeInfinity,
    double HighBound = double.PositiveInfinity);

/// <summary>
/// Manages parallel coordinate queues, neighbor secant warm-starting, and safeguarded
/// susceptibility root-finding across a <c>(k_0, Delta)</c> tuning grid.
/// </summary>
internal sealed class CriticalSurfaceTracker
{
    private readonly RunConfig _config;
    private readonly double _targetBandWidthKd;
    private readonly ConcurrentQueue<ProbeWorkItem> _initialQueue = new();
    private readonly ConcurrentQueue<ProbeWorkItem> _retryQueue = new();
    private readonly ConcurrentBag<(double K0, double Delta, double KdCrit)> _completedCriticals = [];
    private readonly ConcurrentBag<ProbeRecord> _allProbes = [];

    private int _completedCoords;

    public CriticalSurfaceTracker(RunConfig config)
    {
        _config = config;

        var toleranceDeltaKd = 2.0 * config.Physics.Epsilon * config.Physics.TargetVolume * config.Tuning.ToleranceRatio;
        _targetBandWidthKd = 2.0 * toleranceDeltaKd;

        var grid = BuildCoordinateGrid(config);
        TotalCoordinates = grid.Count;

        foreach (var (k0, delta) in grid)
        {
            _initialQueue.Enqueue(new ProbeWorkItem(k0, delta, config.Physics.Kd));
        }
    }

    public int TotalCoordinates { get; }

    public int CompletedCoordinates => Volatile.Read(ref _completedCoords);

    public bool TryClaimNext(out ProbeWorkItem workItem)
    {
        if (_retryQueue.TryDequeue(out workItem))
            return true;

        if (_initialQueue.TryDequeue(out var initial))
        {
            var warmKd = PredictInitialKd(initial.K0, initial.Delta, _completedCriticals, _config.Physics.Kd);
            workItem = initial with { Kd = warmKd };
            return true;
        }

        workItem = default;
        return false;
    }

    public void ReportResult(in ProbeWorkItem item, ProbeRecord result)
    {
        _allProbes.Add(result);

        var lowBound = result.Outcome == ProbeOutcome.AboveTarget
            ? Math.Max(item.LowBound, item.Kd)
            : item.LowBound;

        var highBound = result.Outcome == ProbeOutcome.BelowTarget
            ? Math.Min(item.HighBound, item.Kd)
            : item.HighBound;

        // Saddle-point susceptibility step: Δk_d = 2ε (<N_d> - N_target), safeguarded by [lowBound, highBound].
        var linearStep = 2.0 * _config.Physics.Epsilon * (result.MeanVolume - _config.Physics.TargetVolume);
        var nextKd = item.Kd + linearStep;

        var isBracketed = double.IsFinite(lowBound) && double.IsFinite(highBound);
        if (isBracketed && (nextKd <= lowBound || nextKd >= highBound))
            nextKd = 0.5 * (lowBound + highBound);

        var bracketConverged = isBracketed && (highBound - lowBound) <= _targetBandWidthKd;

        if (result.Outcome == ProbeOutcome.LockedOnTarget || bracketConverged)
        {
            _completedCriticals.Add((item.K0, item.Delta, nextKd));
            Interlocked.Increment(ref _completedCoords);
            return;
        }

        _retryQueue.Enqueue(new ProbeWorkItem(
            item.K0,
            item.Delta,
            nextKd,
            lowBound,
            highBound));
    }

    public TuningResult BuildResult()
    {
        var sortedHistory = _allProbes
            .OrderBy(p => p.Delta)
            .ThenBy(p => p.K0)
            .ThenBy(p => p.Kd)
            .ToList();

        var criticalPoints = Output.BuildCriticalPoints(
            sortedHistory,
            _config.Physics.TargetVolume,
            _config.Physics.Epsilon);

        return new TuningResult(
            _config.Physics.TargetVolume,
            _config.Tuning.ToleranceRatio,
            _config.Physics.Epsilon,
            criticalPoints,
            sortedHistory);
    }

    /// <summary>
    /// Extrapolates an initial <c>k_d</c> guess from completed grid neighbors via collinear secant projection,
    /// falling back to nearest-neighbor seeding or <paramref name="fallbackKd"/> on the initial cold wave.
    /// </summary>
    public static double PredictInitialKd(
        double k0,
        double delta,
        ConcurrentBag<(double K0, double Delta, double KdCrit)> completed,
        double fallbackKd = 0.0)
    {
        if (completed.IsEmpty)
            return fallbackKd;

        var snapshot = completed.ToArray();

        // 1. Collinear secant projection from the two nearest points on the same Delta or k0 slice.
        var sameDelta = snapshot
            .Where(p => p.Delta == delta)
            .OrderBy(p => Math.Abs(p.K0 - k0))
            .ToArray();

        if (sameDelta.Length >= 2 && sameDelta[1].K0 != sameDelta[0].K0)
        {
            var (k0A, _, kdCritA) = sameDelta[0];
            var (k0B, _, kdCritB) = sameDelta[1];
            return ProjectSecant(k0, k0A, kdCritA, k0B, kdCritB);
        }

        var sameK0 = snapshot
            .Where(p => p.K0 == k0)
            .OrderBy(p => Math.Abs(p.Delta - delta))
            .ToArray();

        if (sameK0.Length >= 2 && sameK0[1].Delta != sameK0[0].Delta)
        {
            var (_, deltaA, kdCritA) = sameK0[0];
            var (_, deltaB, kdCritB) = sameK0[1];
            return ProjectSecant(delta, deltaA, kdCritA, deltaB, kdCritB);
        }

        // 2. Direct warm-start from a single completed neighbor on the same slice.
        if (sameDelta.Length == 1)
            return sameDelta[0].KdCrit;

        if (sameK0.Length == 1)
            return sameK0[0].KdCrit;

        // 3. Nearest completed neighbor in (k0, Delta) Euclidean distance.
        return snapshot
            .OrderBy(p => ((p.K0 - k0) * (p.K0 - k0)) + ((p.Delta - delta) * (p.Delta - delta)))
            .First()
            .KdCrit;
    }

    public static List<(double K0, double Delta)> BuildCoordinateGrid(RunConfig config)
    {
        var t = config.Tuning;
        var k0Axis = BuildAxis(config.Physics.K0, t.K0Low, t.K0High, Math.Max(1, t.K0Steps));
        var deltaAxis = BuildAxis(config.Physics.Delta, t.DeltaLow, t.DeltaHigh, Math.Max(1, t.DeltaSteps));

        var grid = new List<(double K0, double Delta)>(k0Axis.Length * deltaAxis.Length);
        foreach (var delta in deltaAxis)
        {
            foreach (var k0 in k0Axis)
            {
                grid.Add((k0, delta));
            }
        }

        return grid;
    }

    private static double ProjectSecant(double targetX, double xA, double kdCritA, double xB, double kdCritB) =>
        kdCritA + (((kdCritB - kdCritA) / (xB - xA)) * (targetX - xA));

    private static double[] BuildAxis(double singleValue, double low, double high, int steps)
    {
        if (steps <= 1)
            return [singleValue];

        var axis = new double[steps];
        axis[0] = low;
        axis[^1] = high;

        var span = high - low;
        for (var i = 1; i < steps - 1; i++)
        {
            axis[i] = low + (((double)i / (steps - 1)) * span);
        }

        return axis;
    }
}