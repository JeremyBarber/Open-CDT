using CDT_2plus1;
using CDT_Agnostic;
using Open_CDT.Config;
using Open_CDT.Workflows;
using Open_CDT.Workflows.Jobs;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.PhaseClassifier;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Thermalizer;

namespace Open_CDT;

/// <summary>
/// Primary entry point for the Open-CDT simulation and plotting suite.
/// </summary>
public static class EntryPoint
{
    private static readonly Dictionary<Workflow, IWorkflow> Workflows = new()
    {
        { Workflow.PhaseSpaceMapper, new PhaseSpaceMapperWorkflow() },
        { Workflow.CanonicalEnsemble, new CanonicalEnsembleWorkflow() }
    };

    private static readonly Dictionary<Job, IJob> Jobs = new()
    {
        { Job.CosmologicalConstantTuner, new CosmologicalConstantTunerJob() },
        { Job.PhaseClassifier, new PhaseClassifierJob() },
        { Job.Thermalizer, new ThermalizerJob() },
        { Job.Sampling, new SamplingJob() }
    };

    /// <summary>
    /// Loads a <c>.cdt</c> YAML configuration file, prepares a timestamped run directory with provenance metadata,
    /// and executes the configured simulation workflow.
    /// </summary>
    /// <param name="configPath">Path to the <c>.cdt</c> YAML configuration file.</param>
    /// <param name="onProgress">Optional callback invoked with live <see cref="JobProgress"/> telemetry updates.</param>
    /// <returns><c>0</c> upon successful completion.</returns>
    public static int Run(
        string configPath,
        Action<JobProgress>? onProgress = null)
    {
        var config = PrepConfig(configPath);

        return config.Simulation.SpacelikeDimensions switch
        {
            2 => RunWorkflow<SimulationHarness>(config, onProgress),
            _ => throw new InvalidOperationException(
                $"No workflow available for {config.Simulation.SpacelikeDimensions}D.")
        };
    }

    /// <summary>
    /// Inspects the specified simulation output directory and renders all applicable diagnostic and publication charts.
    /// </summary>
    /// <param name="outputPath">Path to the directory containing simulation CSV datasets.</param>
    /// <param name="onProgress">Optional callback invoked as each chart is rendered and saved.</param>
    /// <returns><c>0</c> upon successful completion.</returns>
    public static int Plot(
        string outputPath,
        Action<JobProgress>? onProgress = null)
    {
        if (string.IsNullOrWhiteSpace(outputPath) || !Directory.Exists(outputPath))
            throw new DirectoryNotFoundException($"Output directory not found: '{outputPath}'.");

        foreach (var job in Jobs.Values)
            job.Plot(outputPath, onProgress);

        return 0;
    }

    private static int RunWorkflow<THarness>(
        RunConfig config,
        Action<JobProgress>? onProgress)
        where THarness : ISimulationHarness
    {
        if (!Workflows.TryGetValue(config.Simulation.Workflow, out var workflow))
            throw new InvalidOperationException($"Unsupported workflow '{config.Simulation.Workflow}'.");

        workflow.Run<THarness>(config, onProgress);
        return 0;
    }

    private static RunConfig PrepConfig(string configPath)
    {
        var config = RunConfig.FromYamlFile(configPath);

        if (string.IsNullOrWhiteSpace(config.Output.Directory))
            return config;

        var runDir = Path.Combine(
            config.Output.Directory,
            config.Simulation.Name,
            DateTime.Now.ToString("yyyyMMdd-HHmmss"));

        Directory.CreateDirectory(runDir);

        config.ToYamlFile(runDir);

        return config with { Output = config.Output with { Directory = runDir } };
    }
}