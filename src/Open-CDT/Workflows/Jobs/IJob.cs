using Open_CDT.Config;

namespace Open_CDT.Workflows.Jobs;

/// <summary>
/// Contract for a discrete simulation job capable of validating its configuration parameters
/// and rendering diagnostic/publication plots from its persisted CSV datasets.
/// </summary>
internal interface IJob
{
    /// <summary>
    /// Validates the job-specific configuration slice prior to workflow execution.
    /// </summary>
    /// <param name="config">Complete run configuration to validate.</param>
    /// <returns>Zero or more human-readable validation error messages.</returns>
    IEnumerable<string> Validate(RunConfig config);

    /// <summary>
    /// Renders all applicable diagnostic and publication plots when this job's CSV dataset exists in <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">Run output directory containing persisted CSV files.</param>
    /// <param name="onProgress">Optional callback receiving per-chart rendering progress.</param>
    void Plot(string directory, Action<JobProgress>? onProgress = null);
}