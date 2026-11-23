namespace Open_CDT.Workflows.Jobs.PhaseClassifier;

/// <summary>
/// Configuration parameters for equilibrating and classifying spacetime phases across calibrated critical coordinates.
/// </summary>
public sealed record ClassificationParameters
{
    /// <summary>Total Monte Carlo sweeps executed per calibrated <c>(k_0, Delta, k_d^c)</c> coordinate.</summary>
    public int SweepsPerCoordinate { get; init; }
}