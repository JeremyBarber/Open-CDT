using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.Topology.Pachner;

/// <summary>
/// Encapsulates the discrete topological vertex and simplex count changes (<c>ΔN</c>)
/// produced by a 2+1D Pachner move. Used during setup and tuning to precompute
/// the bare Regge action differentials (<c>ΔS_Regge</c>).
/// </summary>
/// <param name="dN0">The net change in total vertex count (<c>ΔN_0</c>).</param>
/// <param name="dN31">The net change in <c>(3,1)</c> simplex count (<c>ΔN_31</c>).</param>
/// <param name="dN22">The net change in <c>(2,2)</c> simplex count (<c>ΔN_22</c>).</param>
/// <param name="dN13">The net change in <c>(1,3)</c> simplex count (<c>ΔN_13</c>).</param>
[StructLayout(LayoutKind.Sequential)]
[method: MethodImpl(MethodImplOptions.AggressiveInlining)]
internal readonly struct PachnerDelta(int dN0, int dN31, int dN22, int dN13)
{
    /// <summary>The net change in the total number of vertices (<c>ΔN_0</c>).</summary>
    public readonly int DeltaN0 = dN0;

    /// <summary>The net change in the number of <c>(3,1)</c> simplices (<c>ΔN_31</c>).</summary>
    public readonly int DeltaN31 = dN31;

    /// <summary>The net change in the number of <c>(2,2)</c> simplices (<c>ΔN_22</c>).</summary>
    public readonly int DeltaN22 = dN22;

    /// <summary>The net change in the number of <c>(1,3)</c> simplices (<c>ΔN_13</c>).</summary>
    public readonly int DeltaN13 = dN13;

    /// <summary>The precomputed net change in total 3-simplex volume (<c>ΔN_3 = ΔN_31 + ΔN_22 + ΔN_13</c>).</summary>
    public readonly int DeltaN3 = dN31 + dN22 + dN13;
}