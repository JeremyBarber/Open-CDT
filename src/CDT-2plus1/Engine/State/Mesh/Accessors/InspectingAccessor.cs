using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.Accessors;
using CDT_Agnostic.Accessors.Implementations;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.State.Mesh;

internal unsafe partial struct Manifold
{
    /// <summary>
    /// A zero-allocation, read-only lens into the unmanaged 2+1D CDT <see cref="Manifold"/>.
    /// Provides strictly <c>O(1)</c> pointer-math lookups for topological and geometric queries.
    /// </summary>
    /// <remarks>
    /// Bounds checking is intentionally omitted on all read operations to preserve L1 cache performance.
    /// </remarks>
    /// <param name="state">Pointer to the owning unmanaged <see cref="Manifold"/>.</param>
    [StructLayout(LayoutKind.Sequential)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly struct InspectingAccessor(Manifold* state) : IInspectingAccessor<AdjacencyAccessor>
    {
        /// <inheritdoc/>
        public AdjacencyAccessor Adjacency
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => new(&state->Memory);
        }

        // =========================================================================
        // IINSPECTINGACCESSOR IMPLEMENTATION (PUBLIC)
        // =========================================================================

        /// <inheritdoc/>
        /// <remarks>Always <c>4</c> in 2+1D CDT (four triangular boundary faces per tetrahedron).</remarks>
        public static int FacetsPerSimplex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TopologyConstants.FacetsPerSimplex;
        }

        /// <inheritdoc/>
        /// <remarks>Always <c>4</c> in 2+1D CDT (four vertices per tetrahedron).</remarks>
        public static int VerticesPerSimplex
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => TopologyConstants.VerticesPerSimplex;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VertexId GetVertex(SimplexId simplexId, int vertexIndex) =>
            new(GetTetrahedron(simplexId).Vertices[vertexIndex]);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TimeSlice GetVertexTime(VertexId vertexId) =>
            new(state->VertexTimes[vertexId.Value]);

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SimplexId GetNeighbor(SimplexId simplexId, int faceIndex) =>
            new(GetTetrahedron(simplexId).NeighborSimplices[faceIndex]);

        // =========================================================================
        // 2+1D INTERNAL TOPOLOGY & TYPE QUERIES
        // =========================================================================

        /// <summary>Gets the current count of active <c>(3,1)</c> simplices (<c>N_31</c>).</summary>
        internal int N31
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => state->TypeStore31.Count;
        }

        /// <summary>Gets a contiguous read-only span of active <c>(3,1)</c> simplex IDs.</summary>
        internal ReadOnlySpan<int> ActiveIds31
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => state->TypeStore31.IdsOfType;
        }

        /// <summary>Gets the current count of active <c>(2,2)</c> simplices (<c>N_22</c>).</summary>
        internal int N22
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => state->TypeStore22.Count;
        }

        /// <summary>Gets a contiguous read-only span of active <c>(2,2)</c> simplex IDs.</summary>
        internal ReadOnlySpan<int> ActiveIds22
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => state->TypeStore22.IdsOfType;
        }

        /// <summary>Gets the current count of active <c>(1,3)</c> simplices (<c>N_13</c>).</summary>
        internal int N13
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => state->TypeStore13.Count;
        }

        /// <summary>Gets a contiguous read-only span of active <c>(1,3)</c> simplex IDs.</summary>
        internal ReadOnlySpan<int> ActiveIds13
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => state->TypeStore13.IdsOfType;
        }

        /// <summary>Returns a zero-copy read-only reference directly to the unmanaged <see cref="Tetrahedron"/> slot.</summary>
        /// <param name="simplexId">The arena identifier of the simplex to read.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal ref readonly Tetrahedron GetTetrahedron(SimplexId simplexId) =>
            ref state->SimplexPool[simplexId.Value];

        /// <summary>Determines whether the cached foliation type for <paramref name="simplexId"/> matches <paramref name="type"/>.</summary>
        /// <param name="simplexId">The target simplex ID.</param>
        /// <param name="type">The expected foliation classification (<see cref="SimplexType.ThreeOne"/>, <see cref="SimplexType.TwoTwo"/>, or <see cref="SimplexType.OneThree"/>).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool SimplexIsType(SimplexId simplexId, SimplexType type) =>
            (SimplexType)state->SimplexTypes[simplexId.Value] == type;
    }
}