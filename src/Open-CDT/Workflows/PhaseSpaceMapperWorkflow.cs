using CDT_Agnostic;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.PhaseClassifier;

namespace Open_CDT.Workflows;

/// <summary>
/// Calibrates the critical cosmological constant surface <c>k_d^c(k_0, Delta)</c> and classifies
/// macroscopic spacetime phases across the calibrated grid.
/// </summary>
internal sealed class PhaseSpaceMapperWorkflow : IWorkflow
{
    private readonly CosmologicalConstantTunerJob _tuner = new();
    private readonly PhaseClassifierJob _classifier = new();

    /// <inheritdoc/>
    public IEnumerable<string> Validate(RunConfig config) =>
        IWorkflow.ValidateCoreConfig(config)
            .Concat(ValidateMapperConfig(config))
            .Concat(_tuner.Validate(config))
            .Concat(_classifier.Validate(config));

    /// <inheritdoc/>
    public void Run<THarness>(
        RunConfig config,
        Action<JobProgress>? onProgress = null)
        where THarness : ISimulationHarness
    {
        ArgumentNullException.ThrowIfNull(config);

        var errors = Validate(config).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "PhaseSpaceMapperWorkflow pre-flight validation failed:\n - " + string.Join("\n - ", errors));
        }

        // Job 1: Map the critical surface kd_c(k0, Delta).
        var tuningResult = _tuner.Execute<THarness>(config, onProgress);

        // Job 2: Equilibrate on the critical surface and classify Phases A, B, and C.
        _classifier.Execute<THarness>(config, tuningResult, onProgress);
    }

    private static IEnumerable<string> ValidateMapperConfig(RunConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Seed.StartFrom))
            yield return "Seed.StartFrom is only supported by the CanonicalEnsemble workflow.";
        if (!string.IsNullOrWhiteSpace(config.Output.SaveTo))
            yield return "Output.SaveTo is only supported by the CanonicalEnsemble workflow.";
    }
}