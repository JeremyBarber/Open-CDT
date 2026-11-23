using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.State.Mesh;

internal unsafe partial struct Manifold
{
    /// <summary>
    /// A specialized initialization lens over the unmanaged 2+1D <see cref="Manifold"/>.
    /// Reserved exclusively for the cold-start seeding phase to construct the initial <c>S^2 × S^1</c> spacetime.
    /// </summary>
    /// <param name="state">Pointer to the owning unmanaged <see cref="Manifold"/>.</param>
    [StructLayout(LayoutKind.Sequential)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly struct SeederAccessor(Manifold* state)
    {
        /// <summary>Gets the underlying topological mutation lens.</summary>
        internal MutatingAccessor Mutate
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(state);
        }

        /// <summary>Returns a read-only reference directly to the unmanaged <see cref="Tetrahedron"/> slot.</summary>
        /// <param name="simplexId">The arena identifier of the simplex to read.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly Tetrahedron GetSimplex(SimplexId simplexId) =>
            ref Mutate.Inspect.GetTetrahedron(simplexId);

        /// <summary>Allocates a new vertex at the specified proper-time <paramref name="timeslice"/>.</summary>
        /// <param name="timeslice">The discrete proper-time slice the vertex resides on.</param>
        /// <returns>The newly allocated <see cref="VertexId"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal VertexId SeedVertex(TimeSlice timeslice) =>
            Mutate.AddVertex(timeslice);

        /// <summary>Claims a simplex slot, populates its four vertices, and registers its foliation type.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SimplexId SeedSimplex(
            VertexId vertex0,
            VertexId vertex1,
            VertexId vertex2,
            VertexId vertex3,
            SimplexType type) =>
            Mutate.RegisterSimplex(vertex0, vertex1, vertex2, vertex3, type);

        /// <summary>Establishes a bidirectional neighbor link between two simplices across the specified local facets.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void SeedNeighbors(SimplexId simplexA, LocalFacetSlot facetA, SimplexId simplexB, LocalFacetSlot facetB) =>
            Mutate.LinkNeighbors(simplexA, facetA, simplexB, facetB);

        /// <summary>
        /// Reverses the 3D winding order (chirality) of <paramref name="simplexId"/> by swapping local slots <c>0</c> and <c>1</c>.
        /// <para>
        /// WARNING: Strictly restricted to initial manifold gluing during seeding before neighbor links are finalized.
        /// Never invoke during live Monte Carlo sweeps.
        /// </para>
        /// </summary>
        /// <param name="simplexId">The identifier of the simplex whose orientation should be inverted.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void InvertOrientation(SimplexId simplexId)
        {
            ref var s = ref state->SimplexPool[simplexId.Value];

            // Swap local slots 0 and 1 in both the vertex and opposite-facet neighbor arrays to flip chirality.
            (s.Vertices[0], s.Vertices[1]) = (s.Vertices[1], s.Vertices[0]);
            (s.NeighborSimplices[0], s.NeighborSimplices[1]) = (s.NeighborSimplices[1], s.NeighborSimplices[0]);
        }
    }
}