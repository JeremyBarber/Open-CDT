using CDT_Agnostic;
using Open_CDT.Config;

namespace Open_CDT.Workflows;

/// <summary>
/// Contract for a top-level simulation workflow orchestrating one or more <see cref="Jobs.IJob"/> stages
/// across a dimension-specific <see cref="ISimulationHarness"/> implementation.
/// </summary>
internal interface IWorkflow
{
    /// <summary>
    /// Validates the full configuration for this workflow and all of its constituent jobs.
    /// </summary>
    IEnumerable<string> Validate(RunConfig config);

    /// <summary>
    /// Validates <paramref name="config"/> and executes all stages of the workflow.
    /// </summary>
    void Run<THarness>(
        RunConfig config,
        Action<JobProgress>? onProgress = null)
        where THarness : ISimulationHarness;

    /// <summary>
    /// Validates core simulation, foliation, and bare-action configuration invariants shared by all workflows.
    /// </summary>
    internal static IEnumerable<string> ValidateCoreConfig(RunConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Simulation.Name))
            yield return "Simulation.Name must be specified.";
        if (config.Simulation.SpacelikeDimensions is not (2 or 3))
            yield return "Simulation.SpacelikeDimensions must be 2 or 3.";
        if (config.Simulation.Replicas is 0 or > int.MaxValue)
            yield return "Simulation.Replicas must be > 0.";
        if (string.IsNullOrWhiteSpace(config.Seed.StartFrom) && config.Seed.K < 1)
            yield return "Seed.K (subdivisions) must be >= 1 when starting from a cold seed.";
        if (config.Seed.Timeslices < 3)
            yield return "Seed.Timeslices must be >= 3.";
        if (config.Physics.TargetVolume == 0)
            yield return "Physics.TargetVolume must be > 0.";
        if (!double.IsFinite(config.Physics.Epsilon) || config.Physics.Epsilon <= 0.0)
            yield return "Physics.Epsilon must be a finite value > 0.";
        if (!double.IsFinite(config.Physics.K0) ||
            !double.IsFinite(config.Physics.Kd) ||
            !double.IsFinite(config.Physics.Delta))
        {
            yield return "Physics.K0, Physics.Kd, and Physics.Delta must be finite.";
        }
    }
}