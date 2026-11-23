namespace CDT_Agnostic.Config;

/// <summary>
/// Bare coupling constants and volume-fixing parameters defining the Wick-rotated Regge action.
/// </summary>
public sealed record PhysicsParameters
{
    /// <summary>Bare inverse gravitational coupling (<c>k_0</c>), conjugate to vertex count <c>N_0</c>.</summary>
    public double K0 { get; init; }

    /// <summary>Bare cosmological coupling (<c>k_d</c>), conjugate to top-dimensional simplex count <c>N_d</c>.</summary>
    public double Kd { get; init; }

    /// <summary>Spacetime causal asymmetry parameter (<c>Delta</c>) between spacelike and timelike edge lengths.</summary>
    public double Delta { get; init; }

    /// <summary>Quadratic volume-fixing stiffness (<c>epsilon</c>) penalizing excursions from <see cref="TargetVolume"/>.</summary>
    public double Epsilon { get; init; }

    /// <summary>Target top-dimensional simplex volume (<c>N_target</c>).</summary>
    public uint TargetVolume { get; init; }
}