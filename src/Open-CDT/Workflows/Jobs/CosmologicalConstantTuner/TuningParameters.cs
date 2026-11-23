namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;

/// <summary>
/// Configuration parameters for parallel critical cosmological constant (<c>k_d</c>) calibration
/// and <c>(k_0, Delta)</c> critical surface mapping.
/// </summary>
public sealed record TuningParameters
{
    /// <summary>Fractional tolerance band around <c>TargetVolume</c> required to lock (e.g., <c>0.005</c> for ±0.5%).</summary>
    public double ToleranceRatio { get; init; }

    /// <summary>Consecutive sweeps within or outside the target band required to confirm a probe outcome.</summary>
    public int PlateauSweeps { get; init; }

    /// <summary>Maximum sweeps allowed for a single probe before timing out.</summary>
    public int MaxSweepsPerProbe { get; init; }

    /// <summary>Lower bound of the <c>k_0</c> grid sweep when <see cref="K0Steps"/> &gt; 1.</summary>
    public double K0Low { get; init; }

    /// <summary>Upper bound of the <c>k_0</c> grid sweep when <see cref="K0Steps"/> &gt; 1.</summary>
    public double K0High { get; init; }

    /// <summary>Number of <c>k_0</c> grid points (defaults to <c>1</c>, evaluating <c>Physics.K0</c> unless <see cref="K0Low"/>/<see cref="K0High"/> are set).</summary>
    public int K0Steps { get; init; } = 1;

    /// <summary>Lower bound of the <c>Delta</c> grid sweep when <see cref="DeltaSteps"/> &gt; 1.</summary>
    public double DeltaLow { get; init; }

    /// <summary>Upper bound of the <c>Delta</c> grid sweep when <see cref="DeltaSteps"/> &gt; 1.</summary>
    public double DeltaHigh { get; init; }

    /// <summary>Number of <c>Delta</c> grid points (defaults to <c>1</c>, evaluating <c>Physics.Delta</c> unless <see cref="DeltaLow"/>/<see cref="DeltaHigh"/> are set).</summary>
    public int DeltaSteps { get; init; } = 1;
}