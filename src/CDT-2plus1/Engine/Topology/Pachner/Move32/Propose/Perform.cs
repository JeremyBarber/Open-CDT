using CDT_2plus1.Engine.Topology.Pachner.Move32.Propose;
using CDT_Agnostic.RuntimeMemory.Identifiers;
using System.Runtime.CompilerServices;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1.Engine.Topology.Pachner.Move32;

internal readonly partial struct Move32
{
    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static unsafe void PerformMove(MutatingAccessor mutator, in Context context)
    {
        // 0. Deferred extraction - Cache is piping hot from Validate!
        ref readonly var s0 = ref mutator.Inspect.GetTetrahedron(context.Id0);
        ref readonly var s1 = ref mutator.Inspect.GetTetrahedron(context.Id1);
        ref readonly var s2 = ref mutator.Inspect.GetTetrahedron(context.Id2);
        ref readonly var sAsym = ref mutator.Inspect.GetTetrahedron(context.IdAsym);

        // =====================================================================================
        // 1. ASYMMETRIC TYPE DEDUCTION (1-Cycle Bitwise Mask)
        // =====================================================================================
        // GEOMETRY: The cluster contains two (2,2)s and one Asymmetric simplex. 
        // We need to know if the Asym is a (3,1) or a (1,3) to recreate it correctly.
        var isType13 = mutator.Inspect.SimplexIsType(context.IdAsym, SimplexType.OneThree);
        int is13_int = Unsafe.As<bool, byte>(ref isType13);

        // OPTIMIZATION: If is13_int == 1, mask13 = 0. ~mask13 = -1 (All 1s). 
        // We use this mask to pass the correct Enum integer without multiplication.
        var mask13 = is13_int - 1;
        var asymType = (SimplexType)((((int)SimplexType.OneThree) & ~mask13) | (((int)SimplexType.ThreeOne) & mask13));

        // =====================================================================================
        // 2. FINDING THE UNIQUE EQUATORIAL VERTEX
        // =====================================================================================
        // GEOMETRY: The Asymmetric simplex holds the two poles (VA, VB) and two equatorial 
        // vertices. We arbitrarily grab the first non-pole vertex to test its time slice.
        (var eqV, _) = sAsym.GetNonEdgeVertices(context.VA, context.VB);

        // =====================================================================================
        // 3. POLE TO SIMPLEX MAPPING (Branchless Vertex Deduction)
        // =====================================================================================
        // GEOMETRY: In a (3,2) move, the new (2,2) simplex will inherit one pole, and the 
        // new Asym simplex will inherit the other. We compare the time slice of our equatorial 
        // vertex (eqV) to pole VA. If they differ, VA is the singleton pole belonging to the Asym.
        var eqV_is_singleton = mutator.Inspect.GetVertexTime(eqV) != mutator.Inspect.GetVertexTime(context.VA);
        int vaIsSingleton = Unsafe.As<bool, byte>(ref eqV_is_singleton);

        var maskVA = vaIsSingleton - 1;

        // If VA is singleton (mask = 0): v22 gets VA, vAsym gets VB.
        // If VB is singleton (mask = -1): v22 gets VB, vAsym gets VA.
        VertexId v22 = new((context.VA.Value & ~maskVA) | (context.VB.Value & maskVA));
        VertexId vAsym = new((context.VB.Value & ~maskVA) | (context.VA.Value & maskVA));

        // =====================================================================================
        // 4. UNROLL EXTERNAL NEIGHBORS (Memory Read)
        // =====================================================================================
        // GEOMETRY: We need to know what exists outside our 3-simplex cluster.
        // We look across the faces opposite VA and VB to grab the IDs of the external universe.
        var slot_s0_VA = s0.FindVertexSlot(context.VA);
        var slot_s0_VB = s0.FindVertexSlot(context.VB);
        SimplexId ext_s0_oppVA = new(s0.NeighborSimplices[slot_s0_VA.Value]);
        SimplexId ext_s0_oppVB = new(s0.NeighborSimplices[slot_s0_VB.Value]);

        var slot_s1_VA = s1.FindVertexSlot(context.VA);
        var slot_s1_VB = s1.FindVertexSlot(context.VB);
        SimplexId ext_s1_oppVA = new(s1.NeighborSimplices[slot_s1_VA.Value]);
        SimplexId ext_s1_oppVB = new(s1.NeighborSimplices[slot_s1_VB.Value]);

        var slot_s2_VA = s2.FindVertexSlot(context.VA);
        var slot_s2_VB = s2.FindVertexSlot(context.VB);
        SimplexId ext_s2_oppVA = new(s2.NeighborSimplices[slot_s2_VA.Value]);
        SimplexId ext_s2_oppVB = new(s2.NeighborSimplices[slot_s2_VB.Value]);

        // =====================================================================================
        // 5. EXTERNAL BOUNDARY ALIGNMENT (Bitwise Pointer Swap)
        // =====================================================================================
        // GEOMETRY: We map the external boundary IDs we just found to the two NEW simplices 
        // we are about to create, strictly based on which pole (VA or VB) the new simplex inherited.
        SimplexId ext_s0_22 = new((ext_s0_oppVB.Value & ~maskVA) | (ext_s0_oppVA.Value & maskVA));
        SimplexId ext_s0_Asym = new((ext_s0_oppVA.Value & ~maskVA) | (ext_s0_oppVB.Value & maskVA));

        SimplexId ext_s1_22 = new((ext_s1_oppVB.Value & ~maskVA) | (ext_s1_oppVA.Value & maskVA));
        SimplexId ext_s1_Asym = new((ext_s1_oppVA.Value & ~maskVA) | (ext_s1_oppVB.Value & maskVA));

        SimplexId ext_s2_22 = new((ext_s2_oppVB.Value & ~maskVA) | (ext_s2_oppVA.Value & maskVA));
        SimplexId ext_s2_Asym = new((ext_s2_oppVA.Value & ~maskVA) | (ext_s2_oppVB.Value & maskVA));

        // Grab the incoming back-pointers from the external universe so we can seal the hole.
        var back_s0_22 = mutator.Inspect.GetTetrahedron(ext_s0_22).GetFacetFacingSimplexIndex(context.Id0);
        var back_s0_Asym = mutator.Inspect.GetTetrahedron(ext_s0_Asym).GetFacetFacingSimplexIndex(context.Id0);

        var back_s1_22 = mutator.Inspect.GetTetrahedron(ext_s1_22).GetFacetFacingSimplexIndex(context.Id1);
        var back_s1_Asym = mutator.Inspect.GetTetrahedron(ext_s1_Asym).GetFacetFacingSimplexIndex(context.Id1);

        var back_s2_22 = mutator.Inspect.GetTetrahedron(ext_s2_22).GetFacetFacingSimplexIndex(context.Id2);
        var back_s2_Asym = mutator.Inspect.GetTetrahedron(ext_s2_Asym).GetFacetFacingSimplexIndex(context.Id2);

        // =====================================================================================
        // 6. TOPOLOGICAL PARITY DEDUCTION
        // =====================================================================================
        // GEOMETRY: We must ensure our new shared equatorial face (P, Q, R) maintains the same 
        // winding order (positive/negative volume orientation) as the hole we are filling.

        // A. Deduce which simplex (s1 or s2) holds P vs Q.
        var pSlot_S1 = s1.FindVertexSlot(context.P);
        var s1HasP = pSlot_S1.Value < 4;
        int s1HasP_int = Unsafe.As<bool, byte>(ref s1HasP);
        var slot_s1 = s1HasP_int;
        var slot_s2 = 1 - s1HasP_int;

        // B. Count inversion permutations of the old s0 to determine its native orientation.
        var sVA = slot_s0_VA.Value;
        var sVB = slot_s0_VB.Value;
        var sP = s0.FindVertexSlot(context.P).Value;
        var sQ = s0.FindVertexSlot(context.Q).Value;

        var inversions = (sVA > sVB ? 1 : 0) + (sVA > sP ? 1 : 0) + (sVA > sQ ? 1 : 0) +
                         (sVB > sP ? 1 : 0) + (sVB > sQ ? 1 : 0) + (sP > sQ ? 1 : 0);

        // C. Mapping [Pole, Pole, Eq, Eq] to [Eq, Eq, Eq, Pole] is an inherently odd permutation.
        // If inversions is EVEN, we must invert our mapping to counteract the topological reflection.
        var invertWinding = (inversions & 1) == 0;
        int inv = Unsafe.As<bool, byte>(ref invertWinding);
        var maskInv = inv - 1;

        // If we need to invert (inv = 1, mask = 0), P and Q swap positions.
        VertexId t22_P = new((context.Q.Value & ~maskInv) | (context.P.Value & maskInv));
        VertexId t22_Q = new((context.P.Value & ~maskInv) | (context.Q.Value & maskInv));

        // The two new simplices must have inverted faces relative to each other to share the equator legally.
        var tAsym_P = t22_Q;
        var tAsym_Q = t22_P;

        // Shift the external slot wiring indices if P and Q swapped positions.
        var final_slot_s1 = (slot_s2 & ~maskInv) | (slot_s1 & maskInv);
        var final_slot_s2 = (slot_s1 & ~maskInv) | (slot_s2 & maskInv);

        // =====================================================================================
        // 7. MEMORY MUTATION & WIRING
        // =====================================================================================
        mutator.UnregisterSimplex(context.Id0);
        mutator.UnregisterSimplex(context.Id1);
        mutator.UnregisterSimplex(context.Id2);

        // Register the two new simplices. R is permanently affixed to Index 2.
        var t22Id = mutator.RegisterSimplex(t22_P, t22_Q, context.R, v22, SimplexType.TwoTwo);
        var tAsymId = mutator.RegisterSimplex(tAsym_P, tAsym_Q, context.R, vAsym, asymType);

        // Wire the two simplices together across the new internal equatorial face (Index 3).
        mutator.LinkNeighbors(t22Id, new(3), tAsymId, new(3));

        // Wire to the external boundary opposite vertex R (Index 2).
        mutator.LinkNeighbors(t22Id, new(2), ext_s0_22, back_s0_22);
        mutator.LinkNeighbors(tAsymId, new(2), ext_s0_Asym, back_s0_Asym);

        // Wire to the external boundaries opposite vertices P and Q (Indices 0 and 1).
        mutator.LinkNeighbors(t22Id, new((byte)final_slot_s1), ext_s1_22, back_s1_22);
        mutator.LinkNeighbors(tAsymId, new((byte)(1 - final_slot_s1)), ext_s1_Asym, back_s1_Asym);

        mutator.LinkNeighbors(t22Id, new((byte)final_slot_s2), ext_s2_22, back_s2_22);
        mutator.LinkNeighbors(tAsymId, new((byte)(1 - final_slot_s2)), ext_s2_Asym, back_s2_Asym);
    }
}