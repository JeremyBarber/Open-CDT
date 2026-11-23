using Open_CDT;
using Open_CDT_CLI.ConsoleWriter;

using Xunit;

namespace Open_CDT_CLI_Tests.ConsoleWriter;

public sealed class ConsoleRendererTests
{
    [Fact]
    public void BuildHeaderLine_BeforeProgressUpdates_ShowsInitialisingWithoutProgressBar()
    {
        using var renderer = new ConsoleRenderer();

        var header = renderer.BuildHeaderLine();

        Assert.Contains("Initialising", header);
        Assert.DoesNotContain("[", header);
    }

    [Fact]
    public void OnProgress_AggregatesSweepRatesAcrossReplicas_AndRetainsLastPositiveRate()
    {
        using var renderer = new ConsoleRenderer();

        // Replica 0 reports 400 sw/s, Replica 1 reports 600 sw/s -> Total = 1,000 sw/s
        renderer.OnProgress(new JobProgress(
            HeaderTitle: "Thermalising",
            HeaderProgress: 0.5,
            ReplicaId: 0,
            TotalReplicas: 2,
            Status: "Rep 0",
            SweepsInStep: 100,
            StepElapsedSeconds: 0.25));

        renderer.OnProgress(new JobProgress(
            HeaderTitle: "Thermalising",
            HeaderProgress: 0.5,
            ReplicaId: 1,
            TotalReplicas: 2,
            Status: "Rep 1",
            SweepsInStep: 150,
            StepElapsedSeconds: 0.25));

        var headerWithBoth = renderer.BuildHeaderLine();
        Assert.Contains("1,000 sw/s", headerWithBoth);
        Assert.Contains("[████████░░░░░░░░]", headerWithBoth);

        // If Replica 0 emits a status-only update (SweepsInStep = 0), its previous 400 sw/s rate is preserved
        renderer.OnProgress(new JobProgress(
            HeaderTitle: "Thermalising",
            HeaderProgress: 0.5,
            ReplicaId: 0,
            TotalReplicas: 2,
            Status: "Rep 0 Idle",
            SweepsInStep: 0,
            StepElapsedSeconds: 0.0));

        Assert.Contains("1,000 sw/s", renderer.BuildHeaderLine());
    }

    [Fact]
    public void OnProgress_WithOutOfRangeReplicaId_IgnoresReplicaSafelyWithoutThrowing()
    {
        using var renderer = new ConsoleRenderer();

        renderer.OnProgress(new JobProgress(
            HeaderTitle: "BoundsTest",
            HeaderProgress: 0.25,
            ReplicaId: 99,
            TotalReplicas: 2,
            Status: "Out of bounds"));

        Assert.Contains("BoundsTest", renderer.BuildHeaderLine());
    }

    [Fact]
    public void BuildReplicaLine_TogglesProgressBarAndSweepRateSuffixCleanly()
    {
        var withBarAndRate = new JobProgress(
            HeaderTitle: "Job",
            HeaderProgress: 0.5,
            ReplicaId: 3,
            TotalReplicas: 4,
            Status: "Running",
            CompletedSteps: 5,
            TotalSteps: 10,
            SweepsInStep: 100,
            StepElapsedSeconds: 0.2,
            ShowProgressBar: true);

        var fullLine = ConsoleRenderer.BuildReplicaLine(3, in withBarAndRate);
        Assert.StartsWith("  #3   [█████████░░░░░░░░░]  Running", fullLine);
        Assert.EndsWith("500 sw/s", fullLine);

        var withoutBarOrRate = withBarAndRate with
        {
            ShowProgressBar = false,
            SweepsInStep = 0,
            StepElapsedSeconds = 0.0
        };

        var minimalLine = ConsoleRenderer.BuildReplicaLine(3, in withoutBarOrRate);
        Assert.Equal("  #3   Running", minimalLine);
    }

    [Fact]
    public void OnProgress_WhenJobCompletesAndNextJobStarts_SwitchesCheckmarkAndResetsBlockState()
    {
        using var renderer = new ConsoleRenderer();

        renderer.OnProgress(new JobProgress(
            HeaderTitle: "Job 1",
            HeaderProgress: 1.0,
            ReplicaId: 0,
            TotalReplicas: 1,
            Status: "Done",
            SweepsInStep: 500,
            StepElapsedSeconds: 0.5,
            IsJobComplete: true,
            SummaryLines: ["Summary Line 1"]));

        var completedHeader = renderer.BuildHeaderLine();
        Assert.StartsWith("✓ Job 1", completedHeader);

        // Emitting the first update of Job 2 must clear Job 1's completion state and old sweep rates
        renderer.OnProgress(new JobProgress(
            HeaderTitle: "Job 2",
            HeaderProgress: -1.0,
            ReplicaId: 0,
            TotalReplicas: 1,
            Status: "Starting Job 2",
            SweepsInStep: 0,
            StepElapsedSeconds: 0.0,
            IsJobComplete: false));

        var job2Header = renderer.BuildHeaderLine();
        Assert.Contains("Job 2", job2Header);
        Assert.DoesNotContain("✓", job2Header);
        Assert.DoesNotContain("1,000 sw/s", job2Header);
        Assert.DoesNotContain("[", job2Header);
    }

    [Fact]
    public void Dispose_IsIdempotentAndIgnoresSubsequentProgressUpdates()
    {
        var renderer = new ConsoleRenderer();
        renderer.Dispose();
        renderer.Dispose();

        renderer.OnProgress(new JobProgress(
            HeaderTitle: "AfterDispose",
            HeaderProgress: 0.5,
            ReplicaId: 0,
            TotalReplicas: 1,
            Status: "Should be ignored"));

        Assert.Contains("Initialising", renderer.BuildHeaderLine());
    }
}