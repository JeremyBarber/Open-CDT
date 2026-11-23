using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;

/// <summary>
/// Selects an interior Phase C operating coordinate from a classified phase-space grid
/// via 2D Shoelace polygon centroid targeting.
/// </summary>
internal static class PhaseCentroid
{
    /// <summary>
    /// Constructs the closed bounding polygon of the Phase C region, computes its
    /// 2D Shoelace area barycentre, and returns the Phase C grid point closest to that centroid.
    /// </summary>
    public static PhasePointRecord? SelectTarget(IReadOnlyList<PhasePointRecord> points)
    {
        var cPts = points
            .Where(p => p.Phase == SpacetimePhase.PhaseC)
            .ToArray();

        if (cPts.Length == 0)
            return null;

        var rows = cPts
            .GroupBy(p => p.Delta)
            .OrderBy(g => g.Key)
            .Select(g => g.OrderBy(p => p.K0).ToArray())
            .ToArray();

        // 1D scan (single Delta slice): select the Phase C grid point closest to the interval midpoint.
        if (rows.Length == 1)
        {
            var row = rows[0];
            var midK0 = 0.5 * (row[0].K0 + row[^1].K0);
            return row.OrderBy(p => Math.Abs(p.K0 - midK0)).First();
        }

        // Trace the closed monotone Phase C hull: left boundary bottom-to-top, then right boundary top-to-bottom.
        var poly = new (double X, double Y)[rows.Length * 2];
        for (var i = 0; i < rows.Length; i++)
            poly[i] = (rows[i][0].K0, rows[i][0].Delta);

        for (var i = 0; i < rows.Length; i++)
        {
            var rev = rows.Length - 1 - i;
            poly[rows.Length + i] = (rows[rev][^1].K0, rows[rev][^1].Delta);
        }

        (var centroidK0, var centroidDelta) = ComputePolygonBarycentre(poly);

        var k0Span = cPts.Max(p => p.K0) - cPts.Min(p => p.K0);
        var deltaSpan = cPts.Max(p => p.Delta) - cPts.Min(p => p.Delta);

        return cPts
            .OrderBy(p =>
            {
                var nk = k0Span > 0.0 ? (p.K0 - centroidK0) / k0Span : 0.0;
                var nd = deltaSpan > 0.0 ? (p.Delta - centroidDelta) / deltaSpan : 0.0;
                return (nk * nk) + (nd * nd);
            })
            .First();
    }

    /// <summary>
    /// Computes the area-weighted 2D Shoelace centroid of a closed simple polygon, falling back
    /// to the arithmetic vertex mean when the polygon encloses zero area (collinear strip).
    /// </summary>
    public static (double Cx, double Cy) ComputePolygonBarycentre(ReadOnlySpan<(double X, double Y)> polygon)
    {
        var n = polygon.Length;
        if (n == 0)
            return (0.0, 0.0);

        var signedArea2 = 0.0;
        var cxAccum = 0.0;
        var cyAccum = 0.0;
        var sumX = 0.0;
        var sumY = 0.0;

        for (var i = 0; i < n; i++)
        {
            (var x0, var y0) = polygon[i];
            (var x1, var y1) = polygon[(i + 1) % n];

            sumX += x0;
            sumY += y0;

            var cross = (x0 * y1) - (x1 * y0);
            signedArea2 += cross;
            cxAccum += (x0 + x1) * cross;
            cyAccum += (y0 + y1) * cross;
        }

        if (Math.Abs(signedArea2) < 1e-12)
            return (sumX / n, sumY / n);

        var inv3Area2 = 1.0 / (3.0 * signedArea2);
        return (cxAccum * inv3Area2, cyAccum * inv3Area2);
    }
}