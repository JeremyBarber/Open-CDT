using Open_CDT.Config;
using Open_CDT.Workflows.Plots;

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Open_CDT.Workflows.Jobs;

/// <summary>
/// Shared lifecycle base class for typed simulation jobs, standardizing dataset persistence,
/// terminal completion summaries, and timed plot rendering across all <see cref="IJob"/> implementations.
/// </summary>
/// <typeparam name="TResult">Strongly-typed output dataset produced and plotted by the job.</typeparam>
internal abstract class JobBase<TResult> : IJob
    where TResult : class
{
    private const int PlotFileNameWidth = 38;
    private const int PlotDescriptionWidth = 31;

    /// <summary>
    /// Describes the batch of <see cref="PlotBase{TResult}"/> charts to render for a loaded dataset.
    /// </summary>
    protected readonly record struct PlotPlan(
        string HeaderTitle,
        string SourceSummary,
        IReadOnlyList<PlotBase<TResult>> Plots);

    /// <summary>
    /// Describes the finalized header banner, per-worker row statuses, and footer summary lines for a completed job.
    /// </summary>
    protected readonly record struct CompletionSummary(
        string HeaderTitle,
        double HeaderProgress,
        IReadOnlyList<string> ReplicaStatuses,
        string[] SummaryLines);

    /// <inheritdoc/>
    public abstract IEnumerable<string> Validate(RunConfig config);

    /// <summary>
    /// Writes <paramref name="result"/> to <see cref="OutputParameters.Directory"/>.
    /// </summary>
    protected abstract void WriteOutput(RunConfig config, TResult result);

    /// <summary>
    /// Attempts to load a previously persisted <typeparamref name="TResult"/> dataset from <paramref name="directory"/>.
    /// </summary>
    protected abstract bool TryReadOutput(string directory, [NotNullWhen(true)] out TResult? result);

    /// <summary>
    /// Selects the applicable charts to render for <paramref name="result"/> based on its dimensionality and contents.
    /// </summary>
    protected abstract PlotPlan BuildPlotPlan(TResult result);

    /// <summary>
    /// Formats the final per-replica status rows and summary footer lines emitted when job execution finishes.
    /// </summary>
    protected abstract CompletionSummary BuildCompletionSummary(
        RunConfig config,
        TResult result,
        int replicaCount,
        double totalSeconds);

    /// <summary>
    /// Persists <paramref name="result"/> when an output directory is configured, emits the finalized
    /// <see cref="JobProgress"/> completion block, and returns <paramref name="result"/>.
    /// </summary>
    protected TResult Complete(
        RunConfig config,
        TResult result,
        int replicaCount,
        long startTimestamp,
        Action<JobProgress>? onProgress)
    {
        if (!string.IsNullOrWhiteSpace(config.Output.Directory))
        {
            WriteOutput(config, result);
        }

        if (onProgress is not null)
        {
            var totalSeconds = Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds;
            var summary = BuildCompletionSummary(config, result, replicaCount, totalSeconds);
            var statuses = summary.ReplicaStatuses;

            for (var i = 0; i < statuses.Count; i++)
            {
                var isLast = i == statuses.Count - 1;
                onProgress(new JobProgress(
                    HeaderTitle: summary.HeaderTitle,
                    HeaderProgress: summary.HeaderProgress,
                    ReplicaId: (uint)i,
                    TotalReplicas: statuses.Count,
                    Status: statuses[i],
                    CompletedSteps: 1,
                    TotalSteps: 1,
                    ShowProgressBar: false,
                    IsJobComplete: isLast,
                    SummaryLines: isLast ? summary.SummaryLines : null));
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public void Plot(string directory, Action<JobProgress>? onProgress = null)
    {
        if (!TryReadOutput(directory, out var result))
            return;

        var plan = BuildPlotPlan(result);
        var plots = plan.Plots;
        if (plots.Count == 0)
            return;

        for (var i = 0; i < plots.Count; i++)
        {
            var plot = plots[i];
            var start = Stopwatch.GetTimestamp();
            plot.Render(directory, result);
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var isLast = i == plots.Count - 1;

            onProgress?.Invoke(new JobProgress(
                HeaderTitle: plan.HeaderTitle,
                HeaderProgress: (double)(i + 1) / plots.Count,
                ReplicaId: (uint)i,
                TotalReplicas: plots.Count,
                Status: $"{plot.FileName,-PlotFileNameWidth}  {plot.Description,-PlotDescriptionWidth}  ✓ Saved {elapsedMs,4:F0} ms",
                CompletedSteps: i + 1,
                TotalSteps: plots.Count,
                ShowProgressBar: false,
                IsJobComplete: isLast,
                SummaryLines: isLast
                    ? [
                        $"Source : {plan.SourceSummary}",
                        $"Output : {plots.Count} {(plots.Count == 1 ? "chart" : "charts")} written to {directory}"
                      ]
                    : null));
        }
    }
}