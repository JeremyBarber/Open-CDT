namespace Open_CDT;

/// <summary>
/// Top-level scientific execution workflow supported by Open-CDT.
/// </summary>
public enum Workflow
{
    /// <summary>
    /// Calibrates the critical cosmological constant surface <c>k_d^c(k_0, Delta)</c>
    /// and classifies the resulting equilibrium geometries into Phases A, B, and C.
    /// </summary>
    PhaseSpaceMapper,

    /// <summary>
    /// Thermalizes an ensemble of independent Markov chain replicas at fixed couplings,
    /// measures integrated autocorrelation time (<c>tau_int</c>), and samples physical observables.
    /// </summary>
    CanonicalEnsemble
}