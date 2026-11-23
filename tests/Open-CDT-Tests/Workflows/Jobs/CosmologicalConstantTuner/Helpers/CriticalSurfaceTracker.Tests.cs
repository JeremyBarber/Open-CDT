using CDT_Agnostic.Config;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Helpers;

using System.Collections.Concurrent;
using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.CosmologicalConstantTuner.Helpers;

public sealed class CriticalSurfaceTrackerTests
{
    private static RunConfig CreateGridConfig(int k0Steps = 1, int deltaSteps = 1) =>
        new()
        {
            Physics = new PhysicsParameters
            {
                K0 = 2.2,
                Kd = 0.5,
                Delta = 0.6,
                Epsilon = 0.0005,
                TargetVolume = 10000
            },
            Tuning = new TuningParameters
            {
                ToleranceRatio = 0.005,
                PlateauSweeps = 100,
                MaxSweepsPerProbe = 500,
                K0Low = 1.0,
                K0High = 3.0,
                K0Steps = k0Steps,
                DeltaLow = 0.2,
                DeltaHigh = 0.6,
                DeltaSteps = deltaSteps
            }
        };
    [Fact]
    public void BuildCoordinateGrid_ConstructsSingleAnd2DGridsAccurately()
    {
        var singleGrid = CriticalSurfaceTracker.BuildCoordinateGrid(CreateGridConfig(k0Steps: 1, deltaSteps: 1));
        var singlePoint = Assert.Single(singleGrid);
        Assert.Equal((2.2, 0.6), singlePoint);

        var grid2D = CriticalSurfaceTracker.BuildCoordinateGrid(CreateGridConfig(k0Steps: 3, deltaSteps: 2));
        Assert.Equal(6, grid2D.Count);
        Assert.Equal((1.0, 0.2), grid2D[0]);
        Assert.Equal((2.0, 0.2), grid2D[1]);
        Assert.Equal((3.0, 0.6), grid2D[5]);
    }

    [Fact]
    public void PredictInitialKd_PrioritizesCollinearSecantThenSameSliceThenNearestNeighbor()
    {
        var completed = new ConcurrentBag<(double K0, double Delta, double KdCrit)>();

        Assert.Equal(0.42, CriticalSurfaceTracker.PredictInitialKd(2.0, 0.5, completed, fallbackKd: 0.42));

        completed.Add((1.0, 0.5, 0.90));
        Assert.Equal(0.90, CriticalSurfaceTracker.PredictInitialKd(2.0, 0.5, completed, fallbackKd: 0.0), tolerance: 1e-12);

        // Two collinear points on Delta = 0.5 -> linear secant extrapolates K0 = 3.0 to 1.10.
        completed.Add((2.0, 0.5, 1.00));
        Assert.Equal(1.10, CriticalSurfaceTracker.PredictInitialKd(3.0, 0.5, completed, fallbackKd: 0.0), tolerance: 1e-12);

        // Off-slice query falls back to Euclidean nearest neighbor (2.0, 0.5 -> 1.00).
        Assert.Equal(1.00, CriticalSurfaceTracker.PredictInitialKd(2.1, 0.9, completed, fallbackKd: 0.0), tolerance: 1e-12);
    }

    [Fact]
    public void TryClaimNext_PrioritizesRetriesAndBisectsWhenProjectionLeavesObservedBracket()
    {
        var tracker = new CriticalSurfaceTracker(CreateGridConfig(k0Steps: 2, deltaSteps: 1));
        Assert.Equal(2, tracker.TotalCoordinates);

        Assert.True(tracker.TryClaimNext(out var first));
        Assert.Equal(1.0, first.K0);
        Assert.Equal(0.5, first.Kd);

        // Report AboveTarget at Kd = 0.5 with MeanVolume = 10500 -> projects nextKd = 0.5 + 2*0.0005*500 = 1.0.
        tracker.ReportResult(in first, new ProbeRecord(1.0, 0.6, 0.5, 10500.0, 30.0, 2000.0, ProbeOutcome.AboveTarget));
        Assert.Equal(0, tracker.CompletedCoordinates);

        // Next claim must pull the retry for K0 = 1.0 ahead of the initial K0 = 3.0 item.
        Assert.True(tracker.TryClaimNext(out var retry1));
        Assert.Equal(1.0, retry1.K0);
        Assert.Equal(1.0, retry1.Kd, tolerance: 1e-12);
        Assert.Equal(0.5, retry1.LowBound, tolerance: 1e-12);

        // Crushed BelowTarget at Kd = 1.0 with MeanVolume = 2000 would linearly step to 1.0 - 8.0 = -7.0 (< LowBound 0.5),
        // so bracket safeguard bisects [0.5, 1.0] -> 0.75.
        tracker.ReportResult(in retry1, new ProbeRecord(1.0, 0.6, 1.0, 2000.0, 5.0, 400.0, ProbeOutcome.BelowTarget));

        Assert.True(tracker.TryClaimNext(out var retry2));
        Assert.Equal(0.75, retry2.Kd, tolerance: 1e-12);

        tracker.ReportResult(in retry2, new ProbeRecord(1.0, 0.6, 0.75, 10000.0, 30.0, 2000.0, ProbeOutcome.LockedOnTarget));
        Assert.Equal(1, tracker.CompletedCoordinates);

        // Second coordinate now warm-starts from the completed K0 = 1.0 neighbor (KdCrit = 0.75).
        Assert.True(tracker.TryClaimNext(out var secondCoord));
        Assert.Equal(3.0, secondCoord.K0);
        Assert.Equal(0.75, secondCoord.Kd, tolerance: 1e-12);
    }
}