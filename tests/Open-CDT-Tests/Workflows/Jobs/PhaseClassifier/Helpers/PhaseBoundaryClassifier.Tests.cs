using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.PhaseClassifier.Helpers;

public sealed class PhaseBoundaryClassifierTests
{
    private static PhasePointRecord CreatePoint(
        double k0,
        double vertexDensity,
        double peakFraction,
        double continuity,
        double delta = 0.5) =>
        new(
            K0: k0,
            Delta: delta,
            KdCrit: 0.80 + (0.15 * k0),
            MeanVolume: 16000.0,
            StdDevVolume: 31.6,
            VertexDensity: vertexDensity,
            PeakSliceFraction: peakFraction,
            TemporalContinuity: continuity,
            Phase: SpacetimePhase.PhaseC);

    [Fact]
    public void ComputeTopologicalDensityBounds_MatchesBarnetteCeilingAcrossDimensions()
    {
        (_, var ceil2D) = PhaseBoundaryClassifier.ComputeTopologicalDensityBounds(2);
        Assert.Equal(0.25, ceil2D, tolerance: 1e-12);

        (_, var ceil3D) = PhaseBoundaryClassifier.ComputeTopologicalDensityBounds(3);
        Assert.Equal(1.0 / 6.0, ceil3D, tolerance: 1e-12);
    }

    [Fact]
    public void ClassifyAllPoints_DetectsPlateauElbowAndAppliesPhaseBSingleSliceOverride()
    {
        PhasePointRecord[] rawRow =
        [
            CreatePoint(k0: 1.0, vertexDensity: 0.145, peakFraction: 0.65, continuity: 0.85), // > 0.50 -> Phase B
            CreatePoint(k0: 2.0, vertexDensity: 0.168, peakFraction: 0.09, continuity: 0.88),
            CreatePoint(k0: 3.0, vertexDensity: 0.192, peakFraction: 0.09, continuity: 0.87),
            CreatePoint(k0: 4.0, vertexDensity: 0.218, peakFraction: 0.09, continuity: 0.84),
            CreatePoint(k0: 5.0, vertexDensity: 0.244, peakFraction: 0.08, continuity: 0.56), // Saturation -> Phase A
            CreatePoint(k0: 6.0, vertexDensity: 0.246, peakFraction: 0.08, continuity: 0.54),
            CreatePoint(k0: 7.0, vertexDensity: 0.247, peakFraction: 0.08, continuity: 0.53),
            CreatePoint(k0: 8.0, vertexDensity: 0.248, peakFraction: 0.08, continuity: 0.52)
        ];

        Assert.True(PhaseBoundaryClassifier.TryFindTransitionRibbon(rawRow, spacelikeDimensions: 2, out var lastC, out var firstA));
        Assert.Equal(3, lastC);
        Assert.Equal(4, firstA);

        var classified = PhaseBoundaryClassifier.ClassifyAllPoints(rawRow, spacelikeDimensions: 2);
        Assert.Equal(SpacetimePhase.PhaseB, classified[0].Phase);
        Assert.All(classified[1..4], p => Assert.Equal(SpacetimePhase.PhaseC, p.Phase));
        Assert.All(classified[4..], p => Assert.Equal(SpacetimePhase.PhaseA, p.Phase));
    }

    [Fact]
    public void ClassifyAllPoints_OnSinglePhaseWindows_DistinguishesPhaseCFromSaturatedPhaseA()
    {
        PhasePointRecord[] purePhaseC =
        [
            CreatePoint(k0: 2.0, vertexDensity: 0.135, peakFraction: 0.09, continuity: 0.92),
            CreatePoint(k0: 2.5, vertexDensity: 0.142, peakFraction: 0.09, continuity: 0.91),
            CreatePoint(k0: 3.0, vertexDensity: 0.149, peakFraction: 0.09, continuity: 0.90),
            CreatePoint(k0: 3.5, vertexDensity: 0.156, peakFraction: 0.09, continuity: 0.89)
        ];

        Assert.False(PhaseBoundaryClassifier.TryFindTransitionRibbon(purePhaseC, spacelikeDimensions: 2, out _, out _));
        Assert.All(PhaseBoundaryClassifier.ClassifyAllPoints(purePhaseC, spacelikeDimensions: 2), p =>
            Assert.Equal(SpacetimePhase.PhaseC, p.Phase));

        PhasePointRecord[] purePhaseA =
        [
            CreatePoint(k0: 9.0,  vertexDensity: 0.2492, peakFraction: 0.06, continuity: 0.52),
            CreatePoint(k0: 9.5,  vertexDensity: 0.2495, peakFraction: 0.06, continuity: 0.51),
            CreatePoint(k0: 10.0, vertexDensity: 0.2498, peakFraction: 0.06, continuity: 0.50),
            CreatePoint(k0: 10.5, vertexDensity: 0.2499, peakFraction: 0.06, continuity: 0.50)
        ];

        Assert.False(PhaseBoundaryClassifier.TryFindTransitionRibbon(purePhaseA, spacelikeDimensions: 2, out _, out _));
        Assert.All(PhaseBoundaryClassifier.ClassifyAllPoints(purePhaseA, spacelikeDimensions: 2), p =>
            Assert.Equal(SpacetimePhase.PhaseA, p.Phase));
    }

    [Fact]
    public void ClassifyAllPoints_3Plus1D_ScalesWithOneSixthBarnetteCeiling()
    {
        // In 3+1D (d_s = 3), the topological polymer ceiling is 1 / (2 * 3) = 1/6 ≈ 0.1667.
        PhasePointRecord[] rawRow3D =
        [
            CreatePoint(k0: 1.0, vertexDensity: 0.0967, peakFraction: 0.08, continuity: 0.90),
            CreatePoint(k0: 2.0, vertexDensity: 0.1120, peakFraction: 0.08, continuity: 0.89),
            CreatePoint(k0: 3.0, vertexDensity: 0.1280, peakFraction: 0.08, continuity: 0.87),
            CreatePoint(k0: 4.0, vertexDensity: 0.1453, peakFraction: 0.08, continuity: 0.84),
            CreatePoint(k0: 5.0, vertexDensity: 0.1627, peakFraction: 0.07, continuity: 0.55), // Saturation -> Phase A
            CreatePoint(k0: 6.0, vertexDensity: 0.1640, peakFraction: 0.07, continuity: 0.53),
            CreatePoint(k0: 7.0, vertexDensity: 0.1647, peakFraction: 0.07, continuity: 0.52),
            CreatePoint(k0: 8.0, vertexDensity: 0.1653, peakFraction: 0.07, continuity: 0.51)
        ];

        Assert.True(PhaseBoundaryClassifier.TryFindTransitionRibbon(rawRow3D, spacelikeDimensions: 3, out var lastC, out var firstA));
        Assert.Equal(3, lastC);
        Assert.Equal(4, firstA);

        var classified = PhaseBoundaryClassifier.ClassifyAllPoints(rawRow3D, spacelikeDimensions: 3);
        Assert.All(classified[..4], p => Assert.Equal(SpacetimePhase.PhaseC, p.Phase));
        Assert.All(classified[4..], p => Assert.Equal(SpacetimePhase.PhaseA, p.Phase));

        PhasePointRecord[] saturated3D =
        [
            CreatePoint(k0: 7.0, vertexDensity: 0.1662, peakFraction: 0.06, continuity: 0.50),
            CreatePoint(k0: 8.0, vertexDensity: 0.1665, peakFraction: 0.06, continuity: 0.49)
        ];

        Assert.All(PhaseBoundaryClassifier.ClassifyAllPoints(saturated3D, spacelikeDimensions: 3), p =>
            Assert.Equal(SpacetimePhase.PhaseA, p.Phase));
    }
}