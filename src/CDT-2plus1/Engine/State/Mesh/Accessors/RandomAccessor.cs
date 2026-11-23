using CDT_2plus1.Engine.Topology;
using CDT_Agnostic.Accessors;
using CDT_Agnostic.RandomNumberGeneration;
using CDT_Agnostic.RuntimeMemory.Identifiers;

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CDT_2plus1.Engine.State.Mesh;

internal unsafe partial struct Manifold
{
    /// <summary>
    /// A zero-allocation, high-performance lens for random entity selection within the 2+1D <see cref="Manifold"/>.
    /// Wraps the unmanaged <see cref="Pcg32"/> generator to provide strictly <c>O(1)</c> uniform draws
    /// directly from the active slot stores and foliation type stores.
    /// </summary>
    /// <param name="state">Pointer to the owning unmanaged <see cref="Manifold"/>.</param>
    /// <param name="rng">Pointer to the unmanaged <see cref="Pcg32"/> random number generator.</param>
    [StructLayout(LayoutKind.Sequential)]
    [method: MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal readonly struct RandomAccessor(Manifold* state, Pcg32* rng) : IRandomAccessor
    {
        private const uint FacetBitMask = (uint)TopologyConstants.FacetsPerSimplex - 1u;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetRandom() =>
            rng->NextUInt32();

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int GetRandom(int bound) =>
            rng->NextBounded(bound);

        /// <inheritdoc/>
        /// <remarks>
        /// Uses a single-cycle bitwise AND mask (<c>&amp; 3</c>) rather than modulo division.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint GetRandomBoundedAtFour() =>
            rng->NextUInt32() & FacetBitMask;

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public SimplexId SelectRandomActiveSimplex() =>
            state->Memory.GetSimplexAt(rng->NextBounded(state->Memory.SimplexCount));

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public VertexId SelectRandomVertex() =>
            state->Memory.GetVertexAt(rng->NextBounded(state->Memory.VertexCount));

        /// <summary>Draws a random active <c>(3,1)</c> <see cref="SimplexId"/> in strictly <c>O(1)</c> time.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SimplexId SelectRandomType31() =>
            state->TypeStore31.SelectRandom(ref *rng);

        /// <summary>Draws a random active <c>(2,2)</c> <see cref="SimplexId"/> in strictly <c>O(1)</c> time.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SimplexId SelectRandomType22() =>
            state->TypeStore22.SelectRandom(ref *rng);

        /// <summary>Draws a random active <c>(1,3)</c> <see cref="SimplexId"/> in strictly <c>O(1)</c> time.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal SimplexId SelectRandomType13() =>
            state->TypeStore13.SelectRandom(ref *rng);
    }
}