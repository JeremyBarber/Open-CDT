namespace CDT_2plus1.Engine.Topology;

/// <summary>
/// Foliation type of a tetrahedron in 2+1D Causal Dynamical Triangulations.
/// 
/// A tetrahedron has exactly 4 vertices distributed strictly across two adjacent timeslices (t and t+1).
/// This strict foliation prevents causality-violating fractional timeslices.
/// </summary>
internal enum SimplexType : byte
{
    /// <summary>3 vertices at t, 1 vertex at t+1 (base-heavy, spatial triangle at bottom).</summary>
    ThreeOne = 0,

    /// <summary>2 vertices at t, 2 vertices at t+1 (bulk/interior, timelike edge at bottom/top).</summary>
    TwoTwo = 1,

    /// <summary>1 vertex at t, 3 vertices at t+1 (apex-heavy, spatial triangle at top).</summary>
    OneThree = 2,

    /// <summary>Utility marker for fixed-size buffer allocations and loop unrolling.</summary>
    _Count = 3
}