using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using Xunit;

using static CDT_2plus1.Engine.State.Mesh.Manifold;
using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_2plus1_Tests.TestHarnesses.Manifolds;

/// <summary>
/// Full-manifold topological, causal, and combinatorial invariant verifier for 2+1D <c>S^2 × S^1</c> triangulations.
/// </summary>
internal static unsafe class ManifoldValidation
{
    public static void AssertIntegrity(
            InspectingAccessor inspector,
            uint timeslices,
            bool checkSeedOrientationParity = true)
    {
        NoBoundaries_IsClosed(inspector);
        TimeFoliation_IsStrictlyCausal(inspector, timeslices);
        Manifold_IsCombinatorial(inspector);
        ManifoldConnection_IsNotFragmented(inspector);
        SpatialSlice_AreS2(inspector, timeslices);
        SimplexNeighbours_AreMutuallyConsistent(inspector);

        if (checkSeedOrientationParity)
        {
            SimplexNeighbours_AreOrientatedConsistently(inspector);
        }
    }

    private static void NoBoundaries_IsClosed(InspectingAccessor inspector)
    {
        var totalSimplices = inspector.Adjacency.TotalSimplices;
        for (var i = 0; i < totalSimplices; i++)
        {
            var id = inspector.Adjacency.GetNthSimplexId(i);

            for (var face = 0; face < TopologyConstants.FacetsPerSimplex; face++)
            {
                if (inspector.GetNeighbor(id, face).Value == InvalidData)
                {
                    Assert.Fail(
                        $"Manifold is OPEN (found [InvalidData] boundary at Simplex {id.Value}, Face {face}). Expected closed S^2 x S^1.");
                }
            }
        }
    }

    private static void TimeFoliation_IsStrictlyCausal(InspectingAccessor inspector, uint timeslices)
    {
        var totalSimplices = inspector.Adjacency.TotalSimplices;
        for (var i = 0; i < totalSimplices; i++)
        {
            var id = inspector.Adjacency.GetNthSimplexId(i);

            var t0 = inspector.GetVertexTime(inspector.GetVertex(id, 0)).Value;
            var t1 = inspector.GetVertexTime(inspector.GetVertex(id, 1)).Value;
            var t2 = inspector.GetVertexTime(inspector.GetVertex(id, 2)).Value;
            var t3 = inspector.GetVertexTime(inspector.GetVertex(id, 3)).Value;

            var minT = Math.Min(Math.Min(t0, t1), Math.Min(t2, t3));
            var maxT = Math.Max(Math.Max(t0, t1), Math.Max(t2, t3));
            var diff = maxT - minT;

            if (diff != 1 && diff != (timeslices - 1))
                Assert.Fail($"Simplex {id.Value} bridges multiple timeslices (T={minT} to T={maxT}).");

            if (t0 != minT && t0 != maxT) Assert.Fail($"Vertex 0 in Simplex {id.Value} is out of bounds.");
            if (t1 != minT && t1 != maxT) Assert.Fail($"Vertex 1 in Simplex {id.Value} is out of bounds.");
            if (t2 != minT && t2 != maxT) Assert.Fail($"Vertex 2 in Simplex {id.Value} is out of bounds.");
            if (t3 != minT && t3 != maxT) Assert.Fail($"Vertex 3 in Simplex {id.Value} is out of bounds.");
        }
    }

    private static void SimplexNeighbours_AreMutuallyConsistent(InspectingAccessor inspector)
    {
        var totalSimplices = inspector.Adjacency.TotalSimplices;
        for (var i = 0; i < totalSimplices; i++)
        {
            var idA = inspector.Adjacency.GetNthSimplexId(i);
            ref readonly var sA = ref inspector.GetTetrahedron(idA);

            for (byte faceA = 0; faceA < TopologyConstants.FacetsPerSimplex; faceA++)
            {
                SimplexId idB = new(sA.NeighborSimplices[faceA]);

                if (idB.Value == InvalidData)
                    Assert.Fail($"Simplex {idA.Value} has an unlinked [InvalidData] neighbor on face {faceA}.");

                ref readonly var sB = ref inspector.GetTetrahedron(idB);

                var mutualLinkFound = false;
                for (byte faceB = 0; faceB < TopologyConstants.FacetsPerSimplex; faceB++)
                {
                    if (sB.NeighborSimplices[faceB] == idA.Value)
                    {
                        mutualLinkFound = true;
                        break;
                    }
                }

                if (!mutualLinkFound)
                    Assert.Fail($"Simplex {idA.Value} points to {idB.Value}, but {idB.Value} has no reciprocal link back.");
            }
        }
    }

    private static void ManifoldConnection_IsNotFragmented(InspectingAccessor inspector)
    {
        var totalSimplices = inspector.Adjacency.TotalSimplices;
        if (totalSimplices == 0)
            Assert.Fail("Manifold is empty.");

        var visited = new HashSet<int>();
        var queue = new Queue<int>();

        var startId = inspector.Adjacency.GetNthSimplexId(0);
        queue.Enqueue(startId.Value);
        visited.Add(startId.Value);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            ref readonly var s = ref inspector.GetTetrahedron(new SimplexId(current));

            for (byte i = 0; i < TopologyConstants.FacetsPerSimplex; i++)
            {
                var neighbor = s.NeighborSimplices[i];
                if (neighbor != InvalidData && visited.Add(neighbor))
                    queue.Enqueue(neighbor);
            }
        }

        if (totalSimplices != visited.Count)
            Assert.Fail($"Manifold has split into disconnected islands (visited {visited.Count} of {totalSimplices}).");
    }

    private static void SpatialSlice_AreS2(InspectingAccessor inspector, uint timeslices)
    {
        var totalSimplices = inspector.Adjacency.TotalSimplices;

        for (var t = 0; t < timeslices; t++)
        {
            var vSet = new HashSet<int>();
            var eSet = new HashSet<(int, int)>();
            var fSet = new HashSet<(int, int, int)>();

            for (var i = 0; i < totalSimplices; i++)
            {
                var id = inspector.Adjacency.GetNthSimplexId(i);
                ref readonly var s = ref inspector.GetTetrahedron(id);

                var tVertices = new List<int>(TopologyConstants.VerticesPerSimplex);
                for (var v = 0; v < TopologyConstants.VerticesPerSimplex; v++)
                {
                    var vId = s.Vertices[v];
                    if (inspector.GetVertexTime(new(vId)) == new TimeSlice(t))
                    {
                        tVertices.Add(vId);
                        vSet.Add(vId);
                    }
                }

                tVertices.Sort();

                if (tVertices.Count >= 2)
                {
                    for (var a = 0; a < tVertices.Count - 1; a++)
                        for (var b = a + 1; b < tVertices.Count; b++)
                            eSet.Add((tVertices[a], tVertices[b]));
                }

                if (tVertices.Count >= 3)
                {
                    for (var a = 0; a < tVertices.Count - 2; a++)
                        for (var b = a + 1; b < tVertices.Count - 1; b++)
                            for (var c = b + 1; c < tVertices.Count; c++)
                                fSet.Add((tVertices[a], tVertices[b], tVertices[c]));
                }
            }

            var chi = vSet.Count - eSet.Count + fSet.Count;
            Assert.True(chi == 2, $"Spatial slice at T={t} is corrupted. Expected Euler characteristic 2 (S^2), got {chi}.");
        }
    }

    private static void Manifold_IsCombinatorial(InspectingAccessor inspector)
    {
        var linkVertices = new Dictionary<int, HashSet<int>>();
        var linkEdges = new Dictionary<int, HashSet<(int, int)>>();
        var linkFaces = new Dictionary<int, HashSet<(int, int, int)>>();

        var totalSimplices = inspector.Adjacency.TotalSimplices;
        for (var i = 0; i < totalSimplices; i++)
        {
            var id = inspector.Adjacency.GetNthSimplexId(i);
            ref readonly var s = ref inspector.GetTetrahedron(id);

            for (var v = 0; v < TopologyConstants.VerticesPerSimplex; v++)
            {
                var vCore = s.Vertices[v];

                if (!linkVertices.TryGetValue(vCore, out var value))
                {
                    value = [];
                    linkVertices[vCore] = value;
                    linkEdges[vCore] = [];
                    linkFaces[vCore] = [];
                }

                var v1 = s.Vertices[(v + 1) & 3];
                var v2 = s.Vertices[(v + 2) & 3];
                var v3 = s.Vertices[(v + 3) & 3];

                int[] face = [v1, v2, v3];
                Array.Sort(face);
                value.Add(face[0]);
                value.Add(face[1]);
                value.Add(face[2]);

                linkEdges[vCore].Add((face[0], face[1]));
                linkEdges[vCore].Add((face[0], face[2]));
                linkEdges[vCore].Add((face[1], face[2]));

                linkFaces[vCore].Add((face[0], face[1], face[2]));
            }
        }

        foreach (var kvp in linkVertices)
        {
            var vCore = kvp.Key;
            var vCount = kvp.Value.Count;
            var eCount = linkEdges[vCore].Count;
            var fCount = linkFaces[vCore].Count;
            var chi = vCount - eCount + fCount;

            Assert.True(
                chi == 2,
                $"Combinatorial failure at Vertex {vCore}. Link Euler characteristic is {chi}, expected 2. Spacetime is pinched.");
        }
    }

    private static void SimplexNeighbours_AreOrientatedConsistently(InspectingAccessor inspector)
    {
        var totalSimplices = inspector.Adjacency.TotalSimplices;
        for (var i = 0; i < totalSimplices; i++)
        {
            var idA = inspector.Adjacency.GetNthSimplexId(i);
            ref readonly var sA = ref inspector.GetTetrahedron(idA);

            for (byte faceA = 0; faceA < TopologyConstants.FacetsPerSimplex; faceA++)
            {
                SimplexId idB = new(sA.NeighborSimplices[faceA]);

                if (idB.Value <= idA.Value || idB.Value == InvalidData)
                    continue;

                ref readonly var sB = ref inspector.GetTetrahedron(idB);

                var v1 = sA.Vertices[(faceA + 1) & 3];
                var v2 = sA.Vertices[(faceA + 2) & 3];
                var v3 = sA.Vertices[(faceA + 3) & 3];

                var slot1 = sB.FindVertexSlot(new(v1)).Value;
                var slot2 = sB.FindVertexSlot(new(v2)).Value;
                var slot3 = sB.FindVertexSlot(new(v3)).Value;

                var faceB = 6 - (slot1 + slot2 + slot3);

                var signA = (faceA % 2 == 0) ? 1 : -1;
                var signB = (faceB % 2 == 0) ? 1 : -1;

                var inversions = 0;
                if (slot1 > slot2) inversions++;
                if (slot1 > slot3) inversions++;
                if (slot2 > slot3) inversions++;
                var parity = (inversions % 2 == 0) ? 1 : -1;

                var isConsistent = (signA * signB * parity) == -1;

                Assert.True(
                    isConsistent,
                    $"Orientation mismatch between Simplex {idA.Value} and Simplex {idB.Value}. Winding order is inverted.");
            }
        }
    }
}