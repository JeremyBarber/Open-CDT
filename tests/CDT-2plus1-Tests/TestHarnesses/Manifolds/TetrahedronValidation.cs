using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

namespace CDT_2plus1_Tests.TestHarnesses.Manifolds;

internal static unsafe class TetrahedronValidation
{
    public static bool Contains(ref readonly Tetrahedron s, int vertexId) =>
        s.FindVertexSlot(new VertexId(vertexId)).Value < TopologyConstants.VerticesPerSimplex;

    public static LocalEdgeSlot FindEdgeSlot(ref readonly Tetrahedron s, int v1, int v2)
    {
        for (byte e = 0; e < TopologyConstants.EdgesPerSimplex; e++)
        {
            var edge = new LocalEdgeSlot(e);
            (var slotA, var slotB) = Tetrahedron.GetEdgeVertexSlots(edge);

            if ((s.Vertices[slotA.Value] == v1 && s.Vertices[slotB.Value] == v2) ||
                (s.Vertices[slotA.Value] == v2 && s.Vertices[slotB.Value] == v1))
            {
                return edge;
            }
        }

        throw new InvalidOperationException($"Edge ({v1}, {v2}) not found in tetrahedron.");
    }
}