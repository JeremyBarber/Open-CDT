using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.TestHarnesses.Manifolds;

/// <summary>
/// Constructs minimal, isolated sub-manifolds for testing <c>(2,3)</c> topological constraints and mutations.
/// </summary>
internal static unsafe class Move23TestArena
{
    public static Manifold* AllocateEmptyArena() => TestArena.AllocateEmpty();

    public static void FreeArena(Manifold* m) => TestArena.Free(m);

    // ==========================================
    // BUILDER 1: The Perfect State (Success Path)
    // ==========================================
    public static Manifold* CreateValid23Cluster()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var v0 = seeder.SeedVertex(new TimeSlice(10)); // ID 0
        var v1 = seeder.SeedVertex(new TimeSlice(10)); // ID 1
        var v2 = seeder.SeedVertex(new TimeSlice(11)); // ID 2

        var vTarget = seeder.SeedVertex(new TimeSlice(11));    // ID 3
        var vNeighbour = seeder.SeedVertex(new TimeSlice(10)); // ID 4

        var s0 = seeder.SeedSimplex(v0, v1, v2, vTarget, SimplexType.TwoTwo);
        var s1 = seeder.SeedSimplex(v0, v1, v2, vNeighbour, SimplexType.ThreeOne);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(3), s1, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 2: Invalid Neighbor (Two Type22s)
    // ==========================================
    public static Manifold* CreateInvalidCluster_NeighborIsType22()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var v0 = seeder.SeedVertex(new TimeSlice(10));
        var v1 = seeder.SeedVertex(new TimeSlice(10));
        var v2 = seeder.SeedVertex(new TimeSlice(11));
        var vTarget = seeder.SeedVertex(new TimeSlice(11));
        var s0 = seeder.SeedSimplex(v0, v1, v2, vTarget, SimplexType.TwoTwo);

        var vNeighbour = seeder.SeedVertex(new TimeSlice(11));
        var s1 = seeder.SeedSimplex(v0, v1, v2, vNeighbour, SimplexType.TwoTwo);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(3), s1, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 3: Degenerate Edge
    // ==========================================
    public static Manifold* CreateInvalidCluster_DegenerateEdge()
    {
        var m = CreateValid23Cluster();
        var seeder = new SeederAccessor(m);

        VertexId vTarget = new(3);
        VertexId vNeighbour = new(4);

        var vDummyTop1 = seeder.SeedVertex(new TimeSlice(11));
        var vDummyTop2 = seeder.SeedVertex(new TimeSlice(10));

        seeder.SeedSimplex(vTarget, vNeighbour, vDummyTop1, vDummyTop2, SimplexType.TwoTwo);

        return m;
    }

    // ==========================================
    // BUILDER 4: The Degenerate Manifold (Multiple Shared Faces)
    // ==========================================
    public static Manifold* CreateInvalidCluster_MultipleSharedFaces()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var v0 = seeder.SeedVertex(new TimeSlice(10));
        var v1 = seeder.SeedVertex(new TimeSlice(10));
        var v2 = seeder.SeedVertex(new TimeSlice(11));
        var vTarget = seeder.SeedVertex(new TimeSlice(11));
        var vNeighbour = seeder.SeedVertex(new TimeSlice(10));

        var s0 = seeder.SeedSimplex(v0, v1, v2, vTarget, SimplexType.TwoTwo);
        var s1 = seeder.SeedSimplex(v0, v1, v2, vNeighbour, SimplexType.ThreeOne);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(3), s1, new LocalFacetSlot(3));

        var vDummy = seeder.SeedVertex(new TimeSlice(10));
        var sExternal = seeder.SeedSimplex(v0, v1, vTarget, vDummy, SimplexType.TwoTwo);

        // s0 shares two faces with sExternal
        seeder.SeedNeighbors(s0, new LocalFacetSlot(0), sExternal, new LocalFacetSlot(0));
        seeder.SeedNeighbors(s0, new LocalFacetSlot(1), sExternal, new LocalFacetSlot(1));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 5: Periodic Boundary Conditions (PBC)
    // ==========================================
    public static Manifold* CreateValid23Cluster_PBC()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(3));
        var vB = seeder.SeedVertex(new TimeSlice(0));
        var vC = seeder.SeedVertex(new TimeSlice(3));

        var pole0 = seeder.SeedVertex(new TimeSlice(0));
        var pole1 = seeder.SeedVertex(new TimeSlice(3));

        var s0 = seeder.SeedSimplex(vA, vB, vC, pole0, SimplexType.TwoTwo);
        var s1 = seeder.SeedSimplex(vA, vB, vC, pole1, SimplexType.ThreeOne);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(3), s1, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }
}