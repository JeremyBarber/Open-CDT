using CDT_Agnostic.Accessors;

namespace CDT_Agnostic.Measurements.Observables;

/// <summary>
/// Measures the discrete spatial volume profile <c>N_d(t)</c> across all proper-time slabs for a single manifold snapshot.
/// Dimensionally agnostic: operates identically on 2D triangles, 3D tetrahedra, or 4D pentachorons.
/// </summary>
/// <param name="timeslices">The total number of discrete proper-time slices (<c>T</c>) in the periodic temporal foliation.</param>
public sealed class VolumeProfile(int timeslices) : IManifoldObservable<int[]>
{
    /// <inheritdoc/>
    public int[] Measure<TAdjacency, TInspector, TRandom>(TInspector inspector, TRandom random)
        where TAdjacency : unmanaged, IAdjacencyAccessor
        where TInspector : unmanaged, IInspectingAccessor<TAdjacency>
        where TRandom : unmanaged, IRandomAccessor
    {
        var currentProfile = new int[timeslices];

        var total = inspector.Adjacency.TotalSimplices;
        if (total == 0)
            return currentProfile;

        // ====================================================================
        // SLAB CLASSIFICATION WITH PERIODIC TEMPORAL BOUNDARY CONDITIONS
        // ====================================================================
        // Every top-dimensional causal simplex spans two adjacent proper-time slices: t and (t + 1) mod T.
        //   - Standard interior slab: vertices lie on t and t + 1, so (tMax - tMin == 1) and the originating slab is tMin.
        //   - Periodic boundary wrap: vertices lie on (T - 1) and 0, so tMin == 0 and tMax == T - 1.
        //     Because (tMax - tMin > 1), the simplex originates on the final slab (tMax == T - 1) and wraps to 0.
        for (var i = 0; i < total; i++)
        {
            var id = inspector.Adjacency.GetNthSimplexId(i);

            var tMin = int.MaxValue;
            var tMax = int.MinValue;

            for (var v = 0; v < TInspector.VerticesPerSimplex; v++)
            {
                var vId = inspector.GetVertex(id, v);
                var t = inspector.GetVertexTime(vId).Value;

                if (t < tMin) tMin = t;
                if (t > tMax) tMax = t;
            }

            var slab = (tMax - tMin > 1) ? tMax : tMin;

            // Unsigned cast checks both (slab >= 0) and (slab < timeslices) in a single comparison
            if ((uint)slab < (uint)timeslices)
            {
                currentProfile[slab]++;
            }
        }

        return currentProfile;
    }
}