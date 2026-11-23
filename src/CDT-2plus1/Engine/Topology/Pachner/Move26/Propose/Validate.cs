using CDT_2plus1.Engine.Topology.Pachner.Move26.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1.Engine.Topology.Pachner.Move26;

/// <summary>
/// Executes the (2,6) Pachner Move for 2+1D Causal Dynamical Triangulations.
/// Safely inserts a new vertex into the center of a spacelike triangle shared by 
/// a (1,3) and a (3,1) simplex, driving spatial expansion in the manifold.
/// </summary>
internal readonly partial struct Move26
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TrySelectValidSiteForMove(RandomAccessor random, InspectingAccessor inspector, out Context context)
    {
        var id13 = random.SelectRandomType13();

        return IsValidSite(inspector, id13, out context);
    }

    /// <summary>
    /// Validates the spatial constraints required for a (2,6) move.
    /// Evaluated in O(1) time entirely without allocations or loops.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static unsafe bool IsValidSite(InspectingAccessor inspector, SimplexId id13, out Context context)
    {
        ref readonly var s13 = ref inspector.GetTetrahedron(id13);

        // =====================================================================================
        // 1. BRANCHLESS EQUATOR DEDUCTION
        // =====================================================================================
        // By the Pigeonhole Principle, a (1,3) simplex has exactly 3 vertices on the spatial 
        // equator and 1 pole. Therefore, if we sample ANY three vertices (slots 0, 1, and 2), 
        // at least two of them MUST reside on the equator's time slice. We use this mathematical 
        // guarantee to deduce the equator time branchlessly without needing to check slot 3.
        var t0 = inspector.GetVertexTime(new VertexId(s13.Vertices[0]));
        var t1 = inspector.GetVertexTime(new VertexId(s13.Vertices[1]));
        var t2 = inspector.GetVertexTime(new VertexId(s13.Vertices[2]));

        var b01 = t0 == t1; var b02 = t0 == t2;
        int eq01 = Unsafe.As<bool, byte>(ref b01);
        int eq02 = Unsafe.As<bool, byte>(ref b02);

        var isT0 = eq01 | eq02;
        TimeSlice timeEquator = new((isT0 * t0.Value) + ((1 - isT0) * t1.Value));

        // =====================================================================================
        // 2. BRANCHLESS POLE ISOLATION
        // =====================================================================================
        // Now that we know the exact time slice of the equator, we find the single vertex 
        // (out of all 4 slots) that does NOT live on that slice. This is the Downward Pole.
        var bd0 = t0 != timeEquator; var bd1 = t1 != timeEquator; var bd2 = t2 != timeEquator;
        int isD0 = Unsafe.As<bool, byte>(ref bd0);
        int isD1 = Unsafe.As<bool, byte>(ref bd1);
        int isD2 = Unsafe.As<bool, byte>(ref bd2);
        var isD3 = 1 - (isD0 | isD1 | isD2);

        LocalVertexSlot slotDown = new((byte)(isD1 * 1 + isD2 * 2 + isD3 * 3));

        // =====================================================================================
        // 3. MINIMAL TOPOLOGICAL SAFETY CHECK
        // =====================================================================================
        // By standard CDT convention, the face opposite the Downward Pole IS the spatial equator.
        // We look across that specific face to find the neighboring simplex.
        SimplexId id31 = new(s13.NeighborSimplices[slotDown.Value]);

        // FAST FAIL: For a valid (2,6) spatial insertion, the neighbor across the equator MUST be a (3,1).
        if (id31 == SimplexId.Invalid || !inspector.SimplexIsType(id31, SimplexType.ThreeOne))
        {
            Unsafe.SkipInit(out context);
            return false;
        }

        // WHY NO LINK CONDITION CHECK?
        // Unlike collapsing moves (Move 62, 32) which can pinch the manifold, the (2,6) move 
        // is purely additive. It inserts a brand-new vertex and expands the space. Because it 
        // only creates new internal edges and cannot possibly duplicate existing ones, we can 
        // completely bypass the expensive O(E) graph traversal (Link Condition).
        context = new Context(id13, id31, slotDown, timeEquator);
        return true;
    }
}