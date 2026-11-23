using CDT_Agnostic;
using Open_CDT.Config;
using Open_CDT.Workflows.Checkpoints;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Thermalizer;

namespace Open_CDT.Workflows;

/// <summary>
/// Thermalizes an ensemble of parallel Markov-chain replicas at fixed couplings, calibrates
/// Madras-Sokal <c>tau_int</c>, and samples decorrelated geometric observables.
/// </summary>
internal sealed class CanonicalEnsembleWorkflow : IWorkflow
{
    private readonly ThermalizerJob _thermalizer = new();
    private readonly SamplingJob _sampler = new();

    /// <inheritdoc/>
    public IEnumerable<string> Validate(RunConfig config) =>
        IWorkflow.ValidateCoreConfig(config)
            .Concat(ValidateCheckpointConfig(config))
            .Concat(_thermalizer.Validate(config))
            .Concat(_sampler.Validate(config));

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
                "CanonicalEnsembleWorkflow pre-flight validation failed:\n - " + string.Join("\n - ", errors));
        }

        var replicaCount = (int)config.Simulation.Replicas;
        ISimulationHarness[] harnesses = [];

        try
        {
            if (!string.IsNullOrWhiteSpace(config.Seed.StartFrom))
            {
                harnesses = CheckpointArchive.LoadEnsemble<THarness>(config);
            }
            else
            {
                harnesses = new ISimulationHarness[replicaCount];
                for (var i = 0; i < replicaCount; i++)
                {
                    harnesses[i] = THarness.Create(
                        (uint)i,
                        config.Physics,
                        config.Seed);
                }
            }

            // Job 1: Burn-in to equilibrium and measure tau_int (writes thermalization.csv internally).
            var thermalResult = _thermalizer.Execute(
                harnesses,
                config,
                onProgress);

            // Job 2: Sample uncorrelated observables (writes observable CSVs internally).
            _sampler.Execute(
                harnesses,
                thermalResult.EnsembleTauInt,
                config,
                onProgress);

            // Optional: Save the final fleet state to a .cdtbin archive.
            if (!string.IsNullOrWhiteSpace(config.Output.SaveTo))
            {
                CheckpointArchive.SaveEnsemble(harnesses, config);
            }
        }
        finally
        {
            foreach (var harness in harnesses)
            {
                harness?.Dispose();
            }
        }
    }

    private static IEnumerable<string> ValidateCheckpointConfig(RunConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.Seed.StartFrom) && !File.Exists(config.Seed.StartFrom))
        {
            yield return $"Seed.StartFrom checkpoint file not found: '{config.Seed.StartFrom}'.";
        }
    }
}