using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.Accessors;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static CDT_Agnostic.RuntimeMemory.RuntimeConstants;

namespace CDT_2plus1.Engine.State.Mesh;

internal unsafe partial struct Manifold
{
    /// <summary>
    /// A zero-allocation mutation lens for the unmanaged 2+1D CDT <see cref="Manifold"/>.
    /// Enforces reciprocal neighbor links, per-type active-set tracking, and <c>O(1)</c> slot recycling.
    /// </summary>
    /// <param name="state">Pointer to the owning unmanaged <see cref="Manifold"/>.</param>
    [StructLayout(LayoutKind.Sequential)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly struct MutatingAccessor(Manifold* state) : IMutatingAccessor<InspectingAccessor>
    {
        /// <inheritdoc/>
        public InspectingAccessor Inspect
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(state);
        }

        /// <summary>
        /// Claims a simplex slot from the arena free-list, populates its four vertices,
        /// resets its four neighbor links to <see cref="InvalidData"/>, links the simplex into each vertex's
        /// adjacency chain, and registers it in the corresponding <see cref="SimplexType"/> store.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SimplexId RegisterSimplex(
            VertexId vertex0,
            VertexId vertex1,
            VertexId vertex2,
            VertexId vertex3,
            SimplexType type)
        {
            var simplexId = state->Memory.AllocSimplex();

            ref var s = ref state->SimplexPool[simplexId.Value];

            s.Vertices[0] = vertex0.Value;
            s.Vertices[1] = vertex1.Value;
            s.Vertices[2] = vertex2.Value;
            s.Vertices[3] = vertex3.Value;

            s.NeighborSimplices[0] = InvalidData;
            s.NeighborSimplices[1] = InvalidData;
            s.NeighborSimplices[2] = InvalidData;
            s.NeighborSimplices[3] = InvalidData;

            AddSimplexToVertex(vertex0, simplexId);
            AddSimplexToVertex(vertex1, simplexId);
            AddSimplexToVertex(vertex2, simplexId);
            AddSimplexToVertex(vertex3, simplexId);

            state->SimplexTypes[simplexId.Value] = (byte)type;

            switch (type)
            {
                case SimplexType.ThreeOne: state->TypeStore31.Add(simplexId); break;
                case SimplexType.TwoTwo: state->TypeStore22.Add(simplexId); break;
                case SimplexType.OneThree: state->TypeStore13.Add(simplexId); break;
            }

            return simplexId;
        }

        /// <summary>
        /// Removes <paramref name="simplexId"/> from its <see cref="SimplexType"/> store, unlinks it from all four
        /// of its vertices' adjacency chains, clears its type marker to <see cref="InvalidDataByte"/>,
        /// and returns its slot ID to the arena free-stack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UnregisterSimplex(SimplexId simplexId)
        {
            ref var s = ref state->SimplexPool[simplexId.Value];

            var type = (SimplexType)state->SimplexTypes[simplexId.Value];
            state->SimplexTypes[simplexId.Value] = InvalidDataByte;

            switch (type)
            {
                case SimplexType.ThreeOne: state->TypeStore31.Remove(simplexId); break;
                case SimplexType.TwoTwo: state->TypeStore22.Remove(simplexId); break;
                case SimplexType.OneThree: state->TypeStore13.Remove(simplexId); break;
            }

            RemoveSimplexFromVertex(new(s.Vertices[0]), simplexId);
            RemoveSimplexFromVertex(new(s.Vertices[1]), simplexId);
            RemoveSimplexFromVertex(new(s.Vertices[2]), simplexId);
            RemoveSimplexFromVertex(new(s.Vertices[3]), simplexId);

            state->Memory.FreeSimplex(simplexId);
        }

        /// <summary>
        /// Establishes a reciprocal boundary link between <paramref name="simplexA"/> (at <paramref name="facetA"/>)
        /// and <paramref name="simplexB"/> (at <paramref name="facetB"/>).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void LinkNeighbors(SimplexId simplexA, LocalFacetSlot facetA, SimplexId simplexB, LocalFacetSlot facetB)
        {
            state->SimplexPool[simplexA.Value].NeighborSimplices[facetA.Value] = simplexB.Value;
            state->SimplexPool[simplexB.Value].NeighborSimplices[facetB.Value] = simplexA.Value;
        }

        /// <summary>
        /// Allocates a vertex slot from the arena free-stack and assigns its discrete proper-time <paramref name="timeslice"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal VertexId AddVertex(TimeSlice timeslice)
        {
            var vertexId = state->Memory.AllocVertex();
            state->VertexTimes[vertexId.Value] = timeslice.Value;
            return vertexId;
        }

        /// <summary>
        /// Resets a vertex's proper-time slice to <see cref="InvalidData"/> and returns its slot ID to the arena free-stack.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveVertex(VertexId vertexId)
        {
            state->VertexTimes[vertexId.Value] = InvalidData;
            state->Memory.FreeVertex(vertexId);
        }

        /// <summary>
        /// Appends <paramref name="simplexId"/> to <paramref name="vertexId"/>'s incident adjacency chain.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void AddSimplexToVertex(VertexId vertexId, SimplexId simplexId) =>
            state->Memory.Link(vertexId, simplexId);

        /// <summary>
        /// Removes <paramref name="simplexId"/> from <paramref name="vertexId"/>'s incident adjacency chain.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void RemoveSimplexFromVertex(VertexId vertexId, SimplexId simplexId) =>
            state->Memory.Unlink(vertexId, simplexId);
    }
}