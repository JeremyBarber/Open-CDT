using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.PhaseClassifier.Helpers;

public sealed class PhaseCentroidTests
{
    private static PhasePointRecord CreatePoint(double k0, double delta, SpacetimePhase phase) =>
        new(
            K0: k0,
            Delta: delta,
            KdCrit: 1.0 + (0.1 * k0),
            MeanVolume: 16000.0,
            StdDevVolume: 31.6,
            VertexDensity: 0.18,
            PeakSliceFraction: 0.09,
            TemporalContinuity: 0.88,
            Phase: phase);

    [Fact]
    public void ComputePolygonBarycentre_HandlesEmptyCollinearAndTrianglePolygons()
    {
        Assert.Equal((0.0, 0.0), PhaseCentroid.ComputePolygonBarycentre([]));

        (double X, double Y)[] collinear = [(1.0, 0.5), (2.0, 0.5), (3.0, 0.5)];
        (var linX, var linY) = PhaseCentroid.ComputePolygonBarycentre(collinear);
        Assert.Equal(2.0, linX, tolerance: 1e-12);
        Assert.Equal(0.5, linY, tolerance: 1e-12);

        (double X, double Y)[] triangle = [(0.0, 0.0), (6.0, 0.0), (0.0, 6.0)];
        (var triX, var triY) = PhaseCentroid.ComputePolygonBarycentre(triangle);
        Assert.Equal(2.0, triX, tolerance: 1e-12);
        Assert.Equal(2.0, triY, tolerance: 1e-12);
    }

    [Fact]
    public void SelectTarget_HandlesEmpty1DSliceAnd2DShoelaceGrid()
    {
        PhasePointRecord[] onlyPhaseA = [CreatePoint(6.0, 0.5, SpacetimePhase.PhaseA)];
        Assert.Null(PhaseCentroid.SelectTarget(onlyPhaseA));

        PhasePointRecord[] slice1D =
        [
            CreatePoint(1.0, 0.5, SpacetimePhase.PhaseC),
            CreatePoint(2.0, 0.5, SpacetimePhase.PhaseC),
            CreatePoint(3.0, 0.5, SpacetimePhase.PhaseC),
            CreatePoint(4.0, 0.5, SpacetimePhase.PhaseA)
        ];
        var rec1D = PhaseCentroid.SelectTarget(slice1D);
        Assert.NotNull(rec1D);
        Assert.Equal(2.0, rec1D.K0, tolerance: 1e-12);

        var grid2D = new List<PhasePointRecord>();
        foreach (var delta in new[] { 0.2, 0.5, 0.8 })
        {
            foreach (var k0 in new[] { 1.0, 2.0, 3.0 })
                grid2D.Add(CreatePoint(k0, delta, SpacetimePhase.PhaseC));

            grid2D.Add(CreatePoint(4.0, delta, SpacetimePhase.PhaseA));
        }

        var rec2D = PhaseCentroid.SelectTarget(grid2D);
        Assert.NotNull(rec2D);
        Assert.Equal(2.0, rec2D.K0, tolerance: 1e-12);
        Assert.Equal(0.5, rec2D.Delta, tolerance: 1e-12);
    }
}