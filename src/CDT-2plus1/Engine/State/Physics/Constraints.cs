using System.Runtime.InteropServices;

using CDT_Agnostic.Config;

namespace CDT_2plus1.Engine.State.Physics;

/// <summary>
/// A dense, blittable container for the 2+1D universe's thermodynamic coupling constants.
/// Bridges managed configuration parameters (<see cref="PhysicsParameters"/>) into the unmanaged engine.
/// </summary>
/// <param name="physics">The managed physical coupling and volume-fixing parameters.</param>
[StructLayout(LayoutKind.Sequential, Pack = 8)]
internal readonly struct Constraints(PhysicsParameters physics)
{
    /// <summary>The bare inverse gravitational coupling constant (<c>k_0</c>, conjugate to vertex count <c>N_0</c>).</summary>
    public readonly double K0 = physics.K0;

    /// <summary>The bare cosmological coupling constant (<c>k_3</c>, conjugate to total tetrahedron count <c>N_3</c>).</summary>
    public readonly double K3 = physics.Kd;

    /// <summary>The causal asymmetry parameter (<c>Δ</c>) between spacelike and timelike edge lengths.</summary>
    public readonly double Delta = physics.Delta;

    /// <summary>The quadratic volume-fixing stiffness constant (<c>ε</c>).</summary>
    public readonly double Epsilon = physics.Epsilon;

    /// <summary>The target top-dimensional simplex volume (<c>V_target = N_3</c>).</summary>
    public readonly int TargetVolume = (int)physics.TargetVolume;
}