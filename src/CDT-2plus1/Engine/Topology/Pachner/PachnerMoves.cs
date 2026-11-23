namespace CDT_2plus1.Engine.Topology.Pachner;

/// <summary>
/// Enumerates the five ergodic, causal-foliation-preserving Pachner moves
/// in 2+1D Causal Dynamical Triangulations (<c>S^2 × S^1</c>).
/// </summary>
internal enum PachnerMove
{
    /// <summary>Replaces a shared timelike face with a timelike edge (<c>ΔN_0 = 0, ΔN_22 = +1, ΔN_3 = +1</c>).</summary>
    Move23 = 0,

    /// <summary>Collapses a degree-3 timelike edge into a shared timelike face (<c>ΔN_0 = 0, ΔN_22 = -1, ΔN_3 = -1</c>).</summary>
    Move32 = 1,

    /// <summary>Subdivides a shared spatial triangle by inserting a central vertex (<c>ΔN_0 = +1, ΔN_31 = +2, ΔN_13 = +2, ΔN_3 = +4</c>).</summary>
    Move26 = 2,

    /// <summary>Collapses a degree-6 spatial vertex into a shared spatial triangle (<c>ΔN_0 = -1, ΔN_31 = -2, ΔN_13 = -2, ΔN_3 = -4</c>).</summary>
    Move62 = 3,

    /// <summary>Flips the diagonal of a spatial rhombus shared by two <c>(3,1)</c> and two <c>(1,3)</c> simplices (<c>ΔN_0 = 0, ΔN_3 = 0</c>).</summary>
    Move44 = 4,

    /// <summary>Sentinel count of distinct Pachner move types used for uniform random move selection.</summary>
    _Count = 5
}