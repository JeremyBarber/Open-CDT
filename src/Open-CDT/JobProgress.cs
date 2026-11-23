namespace Open_CDT;

/// <summary>
/// Immutable telemetry snapshot emitted by an active simulation or plotting job to drive UI and log reporting.
/// </summary>
/// <param name="HeaderTitle">Title displayed in the top summary banner of the active job block.</param>
/// <param name="HeaderProgress">Normalized overall job progress in <c>[0, 1]</c>, or <c>-1</c> when indeterminate.</param>
/// <param name="ReplicaId">Zero-based worker or replica identifier emitting this update.</param>
/// <param name="TotalReplicas">Total number of parallel workers or items in the current job block.</param>
/// <param name="Status">Formatted status text for this replica row.</param>
/// <param name="CompletedSteps">Completed steps for this replica row.</param>
/// <param name="TotalSteps">Total target steps for this replica row.</param>
/// <param name="SweepsInStep">Number of Monte Carlo sweeps executed during the most recent step burst.</param>
/// <param name="StepElapsedSeconds">Wall-clock duration (in seconds) of the most recent step burst.</param>
/// <param name="MeanVolume">Current mean simplex volume <c>&lt;N&gt;</c> of the replica.</param>
/// <param name="ShowProgressBar">True to render a per-replica progress bar alongside <paramref name="Status"/>.</param>
/// <param name="IsJobComplete">True on the final completion update of the job.</param>
/// <param name="SummaryLines">Optional footer summary lines emitted upon job completion.</param>
public readonly record struct JobProgress(
    string HeaderTitle,
    double HeaderProgress,
    uint ReplicaId,
    int TotalReplicas,
    string Status,
    double CompletedSteps = 0,
    double TotalSteps = 0,
    int SweepsInStep = 0,
    double StepElapsedSeconds = 0,
    double MeanVolume = 0,
    bool ShowProgressBar = true,
    bool IsJobComplete = false,
    string[]? SummaryLines = null);