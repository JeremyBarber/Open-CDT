namespace Open_CDT.Workflows.Jobs.Sampling;

/// <summary>
/// Configuration parameters for equilibrium observable sampling across the thermalized ensemble.
/// </summary>
public sealed record SamplingParameters
{
    /// <summary>Number of decorrelated measurement snapshots to record per replica chain.</summary>
    public int SamplesPerReplica { get; init; }

    /// <summary>Minimum MCMC sweeps executed between consecutive samples (overridden by <c>2 * tau_int</c> if larger).</summary>
    public int MinDecorrelationSweeps { get; init; }

    /// <summary>Number of random dual-graph origin simplices sampled per snapshot for the Hausdorff BFS wavefront.</summary>
    public int HausdorffSamples { get; init; }

    /// <summary>Maximum geodesic dual-graph radius <c>r_max</c> tracked during Hausdorff shell measurements.</summary>
    public int HausdorffMaxRadius { get; init; }

    /// <summary>Number of random dual-graph origin simplices sampled per snapshot for deterministic heat-kernel diffusion.</summary>
    public int SpectralSamples { get; init; }

    /// <summary>Maximum discrete diffusion step <c>sigma_max</c> tracked during spectral dimension measurements.</summary>
    public int SpectralMaxSigma { get; init; }
}