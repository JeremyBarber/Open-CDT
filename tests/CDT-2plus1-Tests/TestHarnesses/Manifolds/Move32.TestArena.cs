using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.TestHarnesses.Manifolds;

/// <summary>
/// Constructs minimal, isolated sub-manifolds for testing <c>(3,2)</c> topological constraints and mutations.
/// </summary>
internal static unsafe class Move32TestArena
{
    public static Manifold* AllocateEmptyArena() => TestArena.AllocateEmpty();

    public static void FreeArena(Manifold* m) => TestArena.Free(m);

    // ==========================================
    // BUILDER 1: The Perfect 3->2 State
    // ==========================================
    public static Manifold* CreateValid32Cluster()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(10)); // ID 0
        var vB = seeder.SeedVertex(new TimeSlice(11)); // ID 1

        var p = seeder.SeedVertex(new TimeSlice(10)); // ID 2
        var q = seeder.SeedVertex(new TimeSlice(11)); // ID 3
        var r = seeder.SeedVertex(new TimeSlice(10)); // ID 4

        var s0 = seeder.SeedSimplex(vA, vB, p, q, SimplexType.TwoTwo);
        var s1 = seeder.SeedSimplex(vA, vB, q, r, SimplexType.TwoTwo);
        var s2 = seeder.SeedSimplex(vA, vB, r, p, SimplexType.ThreeOne);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(2), s1, new LocalFacetSlot(3));
        seeder.SeedNeighbors(s1, new LocalFacetSlot(2), s2, new LocalFacetSlot(3));
        seeder.SeedNeighbors(s2, new LocalFacetSlot(2), s0, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 2: Invalid 3->2 (Wrong Degree)
    // ==========================================
    public static Manifold* CreateInvalid32Cluster_WrongDegree()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(10));
        var vB = seeder.SeedVertex(new TimeSlice(11));
        var p = seeder.SeedVertex(new TimeSlice(10));
        var q = seeder.SeedVertex(new TimeSlice(11));

        var s0 = seeder.SeedSimplex(vA, vB, p, q, SimplexType.TwoTwo);
        var s1 = seeder.SeedSimplex(vA, vB, q, p, SimplexType.TwoTwo);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(2), s1, new LocalFacetSlot(3));
        seeder.SeedNeighbors(s0, new LocalFacetSlot(3), s1, new LocalFacetSlot(2));

        return m;
    }

    // ==========================================
    // BUILDER 3: Invalid 3->2 (Wrong Simplex Types)
    // ==========================================
    public static Manifold* CreateInvalid32Cluster_WrongTypes()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(10));
        var vB = seeder.SeedVertex(new TimeSlice(11));
        var p = seeder.SeedVertex(new TimeSlice(10));
        var q = seeder.SeedVertex(new TimeSlice(11));
        var r = seeder.SeedVertex(new TimeSlice(10));

        var s0 = seeder.SeedSimplex(vA, vB, p, q, SimplexType.TwoTwo);
        var s1 = seeder.SeedSimplex(vA, vB, q, r, SimplexType.TwoTwo);
        var s2 = seeder.SeedSimplex(vA, vB, r, p, SimplexType.TwoTwo);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(2), s1, new LocalFacetSlot(3));
        seeder.SeedNeighbors(s1, new LocalFacetSlot(2), s2, new LocalFacetSlot(3));
        seeder.SeedNeighbors(s2, new LocalFacetSlot(2), s0, new LocalFacetSlot(3));

        return m;
    }

    // ==========================================
    // BUILDER 4: Invalid 3->2 (Degenerate Triangle)
    // ==========================================
    public static Manifold* CreateInvalid32Cluster_DegenerateTriangle()
    {
        var m = CreateValid32Cluster();
        var seeder = new SeederAccessor(m);

        VertexId p = new(2);
        VertexId q = new(3);
        VertexId r = new(4);
        var rogueVertex = seeder.SeedVertex(new TimeSlice(10));

        seeder.SeedSimplex(p, q, r, rogueVertex, SimplexType.TwoTwo);

        return m;
    }

    // ==========================================
    // BUILDER 5: PBC for 3->2
    // ==========================================
    public static Manifold* CreateValid32Cluster_PBC()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(3));
        var vB = seeder.SeedVertex(new TimeSlice(0));

        var p = seeder.SeedVertex(new TimeSlice(3));
        var q = seeder.SeedVertex(new TimeSlice(0));
        var r = seeder.SeedVertex(new TimeSlice(3));

        var s0 = seeder.SeedSimplex(vA, vB, p, q, SimplexType.TwoTwo);
        var s1 = seeder.SeedSimplex(vA, vB, q, r, SimplexType.TwoTwo);
        var s2 = seeder.SeedSimplex(vA, vB, r, p, SimplexType.ThreeOne);

        seeder.SeedNeighbors(s0, new LocalFacetSlot(2), s1, new LocalFacetSlot(3));
        seeder.SeedNeighbors(s1, new LocalFacetSlot(2), s2, new LocalFacetSlot(3));
        seeder.SeedNeighbors(s2, new LocalFacetSlot(2), s0, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }
}