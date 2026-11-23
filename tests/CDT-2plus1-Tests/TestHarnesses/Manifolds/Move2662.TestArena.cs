using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.TestHarnesses.Manifolds;

/// <summary>
/// Constructs minimal, isolated sub-manifolds for testing <c>(2,6)</c> and <c>(6,2)</c> spatial moves.
/// </summary>
internal static unsafe class Move26_62TestArena
{
    public static Manifold* AllocateEmptyArena() => TestArena.AllocateEmpty();

    public static void FreeArena(Manifold* m) => TestArena.Free(m);

    // ==========================================
    // BUILDER 1: The Perfect 2->6 State
    // ==========================================
    public static Manifold* CreateValid26Cluster()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(10)); // ID 0
        var vB = seeder.SeedVertex(new TimeSlice(10)); // ID 1
        var vC = seeder.SeedVertex(new TimeSlice(10)); // ID 2

        var vTop = seeder.SeedVertex(new TimeSlice(11)); // ID 3
        var vBot = seeder.SeedVertex(new TimeSlice(9));  // ID 4

        var s0 = seeder.SeedSimplex(vA, vB, vC, vTop, SimplexType.ThreeOne);
        var s1 = seeder.SeedSimplex(vA, vB, vC, vBot, SimplexType.OneThree);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(3), s1, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 2: The Perfect 6->2 State
    // ==========================================
    public static Manifold* CreateValid62Cluster()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vCenter = seeder.SeedVertex(new TimeSlice(10));

        var vA = seeder.SeedVertex(new TimeSlice(10));
        var vB = seeder.SeedVertex(new TimeSlice(10));
        var vC = seeder.SeedVertex(new TimeSlice(10));

        var vTop = seeder.SeedVertex(new TimeSlice(11));
        var vBot = seeder.SeedVertex(new TimeSlice(9));

        var t0 = seeder.SeedSimplex(vCenter, vA, vB, vTop, SimplexType.ThreeOne);
        var t1 = seeder.SeedSimplex(vCenter, vB, vC, vTop, SimplexType.ThreeOne);
        var t2 = seeder.SeedSimplex(vCenter, vC, vA, vTop, SimplexType.ThreeOne);

        var b0 = seeder.SeedSimplex(vCenter, vA, vB, vBot, SimplexType.OneThree);
        var b1 = seeder.SeedSimplex(vCenter, vB, vC, vBot, SimplexType.OneThree);
        var b2 = seeder.SeedSimplex(vCenter, vC, vA, vBot, SimplexType.OneThree);

        seeder.SeedNeighbors(t0, new LocalFacetSlot(3), b0, new LocalFacetSlot(3));
        seeder.SeedNeighbors(t1, new LocalFacetSlot(3), b1, new LocalFacetSlot(3));
        seeder.SeedNeighbors(t2, new LocalFacetSlot(3), b2, new LocalFacetSlot(3));

        seeder.SeedNeighbors(t0, new LocalFacetSlot(1), t1, new LocalFacetSlot(2));
        seeder.SeedNeighbors(t1, new LocalFacetSlot(1), t2, new LocalFacetSlot(2));
        seeder.SeedNeighbors(t2, new LocalFacetSlot(1), t0, new LocalFacetSlot(2));

        seeder.SeedNeighbors(b0, new LocalFacetSlot(1), b1, new LocalFacetSlot(2));
        seeder.SeedNeighbors(b1, new LocalFacetSlot(1), b2, new LocalFacetSlot(2));
        seeder.SeedNeighbors(b2, new LocalFacetSlot(1), b0, new LocalFacetSlot(2));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 3: Invalid 6->2 (Link Condition Failed)
    // ==========================================
    public static Manifold* CreateInvalid62Cluster_DegenerateLink()
    {
        var m = CreateValid62Cluster();
        var seeder = new SeederAccessor(m);

        VertexId vA = new(1);
        VertexId vB = new(2);
        VertexId vC = new(3);

        var rogueTop = seeder.SeedVertex(new TimeSlice(11));
        seeder.SeedSimplex(vA, vB, vC, rogueTop, SimplexType.ThreeOne);

        return m;
    }

    // ==========================================
    // BUILDER 4: PBC for 2->6
    // ==========================================
    public static Manifold* CreateValid26Cluster_PBC()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(0));
        var vB = seeder.SeedVertex(new TimeSlice(0));
        var vC = seeder.SeedVertex(new TimeSlice(0));

        var vTop = seeder.SeedVertex(new TimeSlice(1));
        var vBot = seeder.SeedVertex(new TimeSlice(3));

        var s0 = seeder.SeedSimplex(vA, vB, vC, vTop, SimplexType.ThreeOne);
        var s1 = seeder.SeedSimplex(vA, vB, vC, vBot, SimplexType.OneThree);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(3), s1, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 5: PBC for 6->2
    // ==========================================
    public static Manifold* CreateValid62Cluster_PBC()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vCenter = seeder.SeedVertex(new TimeSlice(0));

        var vA = seeder.SeedVertex(new TimeSlice(0));
        var vB = seeder.SeedVertex(new TimeSlice(0));
        var vC = seeder.SeedVertex(new TimeSlice(0));

        var vTop = seeder.SeedVertex(new TimeSlice(1));
        var vBot = seeder.SeedVertex(new TimeSlice(3));

        var t0 = seeder.SeedSimplex(vCenter, vA, vB, vTop, SimplexType.ThreeOne);
        var t1 = seeder.SeedSimplex(vCenter, vB, vC, vTop, SimplexType.ThreeOne);
        var t2 = seeder.SeedSimplex(vCenter, vC, vA, vTop, SimplexType.ThreeOne);

        var b0 = seeder.SeedSimplex(vCenter, vA, vB, vBot, SimplexType.OneThree);
        var b1 = seeder.SeedSimplex(vCenter, vB, vC, vBot, SimplexType.OneThree);
        var b2 = seeder.SeedSimplex(vCenter, vC, vA, vBot, SimplexType.OneThree);

        seeder.SeedNeighbors(t0, new LocalFacetSlot(3), b0, new LocalFacetSlot(3));
        seeder.SeedNeighbors(t1, new LocalFacetSlot(3), b1, new LocalFacetSlot(3));
        seeder.SeedNeighbors(t2, new LocalFacetSlot(3), b2, new LocalFacetSlot(3));

        seeder.SeedNeighbors(t0, new LocalFacetSlot(1), t1, new LocalFacetSlot(2));
        seeder.SeedNeighbors(t1, new LocalFacetSlot(1), t2, new LocalFacetSlot(2));
        seeder.SeedNeighbors(t2, new LocalFacetSlot(1), t0, new LocalFacetSlot(2));

        seeder.SeedNeighbors(b0, new LocalFacetSlot(1), b1, new LocalFacetSlot(2));
        seeder.SeedNeighbors(b1, new LocalFacetSlot(1), b2, new LocalFacetSlot(2));
        seeder.SeedNeighbors(b2, new LocalFacetSlot(1), b0, new LocalFacetSlot(2));

        TestArena.PadExternalBoundaries(m);
        return m;
    }
}