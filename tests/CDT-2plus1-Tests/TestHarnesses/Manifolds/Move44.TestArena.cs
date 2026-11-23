using CDT_2plus1.Engine.State.Mesh;
using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using static CDT_2plus1.Engine.State.Mesh.Manifold;

namespace CDT_2plus1_Tests.TestHarnesses.Manifolds;

/// <summary>
/// Constructs minimal, isolated sub-manifolds for testing <c>(4,4)</c> spatial edge-flip moves.
/// </summary>
internal static unsafe class Move44TestArena
{
    public static Manifold* AllocateEmptyArena() => TestArena.AllocateEmpty();

    public static void FreeArena(Manifold* m) => TestArena.Free(m);

    // ==========================================
    // BUILDER 1: The Perfect 4->4 Diamond
    // ==========================================
    public static Manifold* CreateValid44Cluster()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(10)); // ID 0
        var vB = seeder.SeedVertex(new TimeSlice(10)); // ID 1
        var vC = seeder.SeedVertex(new TimeSlice(10)); // ID 2
        var vD = seeder.SeedVertex(new TimeSlice(10)); // ID 3

        var vU = seeder.SeedVertex(new TimeSlice(11)); // ID 4 (Top)
        var vV = seeder.SeedVertex(new TimeSlice(9));  // ID 5 (Bottom)

        var sC31 = seeder.SeedSimplex(vA, vB, vC, vU, SimplexType.ThreeOne);
        var sD31 = seeder.SeedSimplex(vA, vB, vD, vU, SimplexType.ThreeOne);
        var sC13 = seeder.SeedSimplex(vA, vB, vC, vV, SimplexType.OneThree);
        var sD13 = seeder.SeedSimplex(vA, vB, vD, vV, SimplexType.OneThree);

        seeder.SeedNeighbors(sC31, new LocalFacetSlot(2), sD31, new LocalFacetSlot(2));
        seeder.SeedNeighbors(sC13, new LocalFacetSlot(2), sD13, new LocalFacetSlot(2));

        seeder.SeedNeighbors(sC31, new LocalFacetSlot(3), sC13, new LocalFacetSlot(3));
        seeder.SeedNeighbors(sD31, new LocalFacetSlot(3), sD13, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }

    // ==========================================
    // BUILDER 2: Invalid 4->4 (Degenerate Diagonal)
    // ==========================================
    public static Manifold* CreateInvalid44Cluster_DegenerateDiagonal()
    {
        var m = CreateValid44Cluster();
        var seeder = new SeederAccessor(m);

        VertexId vC = new(2);
        VertexId vD = new(3);

        var rogue1 = seeder.SeedVertex(new TimeSlice(10));
        var rogue2 = seeder.SeedVertex(new TimeSlice(11));
        seeder.SeedSimplex(vC, vD, rogue1, rogue2, SimplexType.ThreeOne);

        return m;
    }

    // ==========================================
    // BUILDER 3: PBC for 4->4
    // ==========================================
    public static Manifold* CreateValid44Cluster_PBC()
    {
        var m = AllocateEmptyArena();
        var seeder = new SeederAccessor(m);

        var vA = seeder.SeedVertex(new TimeSlice(0)); // ID 0
        var vB = seeder.SeedVertex(new TimeSlice(0)); // ID 1
        var vC = seeder.SeedVertex(new TimeSlice(0)); // ID 2
        var vD = seeder.SeedVertex(new TimeSlice(0)); // ID 3

        var vTop = seeder.SeedVertex(new TimeSlice(1));
        var vBot = seeder.SeedVertex(new TimeSlice(3));

        var sC31 = seeder.SeedSimplex(vA, vB, vC, vTop, SimplexType.ThreeOne);
        var sD31 = seeder.SeedSimplex(vA, vB, vD, vTop, SimplexType.ThreeOne);
        var sC13 = seeder.SeedSimplex(vA, vB, vC, vBot, SimplexType.OneThree);
        var sD13 = seeder.SeedSimplex(vA, vB, vD, vBot, SimplexType.OneThree);

        seeder.SeedNeighbors(sC31, new LocalFacetSlot(2), sD31, new LocalFacetSlot(2));
        seeder.SeedNeighbors(sC13, new LocalFacetSlot(2), sD13, new LocalFacetSlot(2));
        seeder.SeedNeighbors(sC31, new LocalFacetSlot(3), sC13, new LocalFacetSlot(3));
        seeder.SeedNeighbors(sD31, new LocalFacetSlot(3), sD13, new LocalFacetSlot(3));

        TestArena.PadExternalBoundaries(m);
        return m;
    }
}