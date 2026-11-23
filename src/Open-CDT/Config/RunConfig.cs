using CDT_Agnostic.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;
using Open_CDT.Workflows.Jobs.PhaseClassifier;
using Open_CDT.Workflows.Jobs.Sampling;
using Open_CDT.Workflows.Jobs.Thermalizer;

using YamlDotNet.Serialization;

namespace Open_CDT.Config;

/// <summary>
/// High-level identification, workflow selection, concurrency, and spatial dimensionality for a simulation run.
/// </summary>
public sealed record SimulationParameters
{
    /// <summary>Descriptive identifier for the run, used to name the output directory and provenance file.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Top-level execution workflow (<see cref="Workflow.PhaseSpaceMapper"/> or <see cref="Workflow.CanonicalEnsemble"/>).</summary>
    public Workflow Workflow { get; init; }

    /// <summary>Number of parallel simulation replicas or worker threads to execute.</summary>
    public uint Replicas { get; init; }

    /// <summary>Number of spatial dimensions (e.g., <c>2</c> for 2+1D spacetime).</summary>
    public uint SpacelikeDimensions { get; init; }
}

/// <summary>
/// Output directory and optional binary checkpoint destination for a simulation run.
/// </summary>
public sealed record OutputParameters
{
    /// <summary>Root directory where the timestamped run folder, CSV datasets, and plots are written (empty for in-memory runs).</summary>
    public string Directory { get; init; } = string.Empty;

    /// <summary>Optional path (relative to the run directory or absolute) for saving a <c>.cdtbin</c> checkpoint archive.</summary>
    public string? SaveTo { get; init; }
}

/// <summary>
/// Complete immutable configuration bundle for an Open-CDT simulation run.
/// Loaded from a <c>.cdt</c> YAML configuration file and saved into the run output directory for provenance.
/// </summary>
public sealed record RunConfig
{
    /// <summary>High-level identification, workflow mode, replica count, and spatial dimensionality.</summary>
    public SimulationParameters Simulation { get; init; } = new();

    /// <summary>Initial triangulation seed and optional checkpoint source parameters.</summary>
    public SeedParameters Seed { get; init; } = new();

    /// <summary>Bare Regge coupling constants and target volume parameters.</summary>
    public PhysicsParameters Physics { get; init; } = new();

    /// <summary>Critical cosmological constant calibration and <c>(k_0, Delta)</c> grid parameters.</summary>
    public TuningParameters Tuning { get; init; } = new();

    /// <summary>Critical-surface equilibration and phase classification parameters.</summary>
    public ClassificationParameters Classification { get; init; } = new();

    /// <summary>Deterministic burn-in and trailing equilibrium window parameters.</summary>
    public ThermalizationParameters Thermalization { get; init; } = new();

    /// <summary>Decorrelated observable measurement parameters.</summary>
    public SamplingParameters Sampling { get; init; } = new();

    /// <summary>Output directory and optional binary checkpoint destination.</summary>
    public OutputParameters Output { get; init; } = new();

    /// <summary>
    /// Deserializes a <see cref="RunConfig"/> instance from a <c>.cdt</c> YAML file on disk.
    /// </summary>
    /// <param name="filepath">Path to the YAML configuration file.</param>
    /// <returns>The parsed <see cref="RunConfig"/>.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the file does not exist or cannot be deserialized.</exception>
    public static RunConfig FromYamlFile(string? filepath)
    {
        if (string.IsNullOrWhiteSpace(filepath) || !File.Exists(filepath))
            throw new InvalidOperationException("Config file not found.");

        var deserializer = new DeserializerBuilder().Build();

        using var reader = File.OpenText(filepath);
        return deserializer.Deserialize<RunConfig>(reader)
            ?? throw new InvalidOperationException("Failed to deserialize configuration.");
    }

    /// <summary>
    /// Serializes this <see cref="RunConfig"/> to <c>{Simulation.Name}.cdt</c> inside <paramref name="directoryPath"/>.
    /// </summary>
    /// <param name="directoryPath">Destination directory path.</param>
    public void ToYamlFile(string directoryPath)
    {
        var serializer = new SerializerBuilder().Build();

        using var writer = File.CreateText(Path.Combine(directoryPath, $"{Simulation.Name}.cdt"));
        serializer.Serialize(writer, this);
    }
}