namespace Open_CDT.Workflows.Jobs.Thermalizer;

/// <summary>
/// Configuration parameters for deterministic ensemble burn-in and trailing-window
/// equilibrium autocorrelation diagnostics (<c>tau_int</c>).
/// </summary>
public sealed record ThermalizationParameters
{
    /// <summary>Total deterministic burn-in sweeps executed per replica chain (must be <c>&gt;= WindowSweeps</c>).</summary>
    public int BurnInSweeps { get; init; }

    /// <summary>Trailing equilibrium sweep window over which equilibrium means and Madras-Sokal <c>tau_int</c> are evaluated.</summary>
    public int WindowSweeps { get; init; }
}