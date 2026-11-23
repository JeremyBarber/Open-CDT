using CDT_Agnostic;
using Open_CDT.Config;

namespace Open_CDT.Workflows.Checkpoints;

/// <summary>
/// Reads and writes multi-replica binary checkpoint archives (<c>.cdtbin</c>).
/// Supports both bit-exact Markov chain continuation (<c>i &lt; savedCount</c>) and modulo
/// fleet expansion (<c>i &gt;= savedCount</c>) with automatic <c>Pcg32</c> stream re-keying.
/// </summary>
internal static class CheckpointArchive
{
    private const string DefaultExtension = ".cdtbin";
    private const uint ExpectedMagic = 0x42544443; // ASCII "CDTB" (little-endian)
    private const uint CurrentVersion = 1;

    /// <summary>
    /// Resolves <see cref="OutputParameters.SaveTo"/> to a normalized file path with the <c>.cdtbin</c> extension
    /// and ensures the destination parent directory exists.
    /// </summary>
    /// <param name="config">Active run configuration containing output directory and checkpoint path settings.</param>
    /// <returns>The resolved file path for writing the checkpoint archive.</returns>
    public static string ResolveSavePath(RunConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.Output.SaveTo))
            throw new InvalidOperationException("Output.SaveTo must be specified when saving a checkpoint.");

        var raw = config.Output.SaveTo;
        if (!raw.EndsWith(DefaultExtension, StringComparison.OrdinalIgnoreCase))
            raw += DefaultExtension;

        var baseDir = string.IsNullOrWhiteSpace(config.Output.Directory) ? "." : config.Output.Directory;
        var fullPath = Path.IsPathRooted(raw) ? raw : Path.Combine(baseDir, raw);

        if (Path.GetDirectoryName(fullPath) is string parent && !string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        return fullPath;
    }

    /// <summary>
    /// Serializes an array of live <see cref="ISimulationHarness"/> instances into a single indexed <c>.cdtbin</c> archive.
    /// </summary>
    /// <param name="harnesses">Live replica harnesses to snapshot (must contain at least one replica).</param>
    /// <param name="config">Active run configuration supplying foliation metadata and output path.</param>
    public static void SaveEnsemble(ISimulationHarness[] harnesses, RunConfig config)
    {
        ArgumentNullException.ThrowIfNull(harnesses);
        if (harnesses.Length == 0)
            throw new ArgumentException("Cannot save an empty replica fleet.", nameof(harnesses));

        var path = ResolveSavePath(config);
        var count = (uint)harnesses.Length;

        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = new BinaryWriter(stream);

        // 20-byte archive header: five uint32 fields (magic, version, spatial dims, timeslices, replica count).
        writer.Write(ExpectedMagic);
        writer.Write(CurrentVersion);
        writer.Write(config.Simulation.SpacelikeDimensions);
        writer.Write(config.Seed.Timeslices);
        writer.Write(count);

        // Reserve the int64 stream-offset table before writing variable-length replica payloads.
        var offsetTablePosition = stream.Position;
        var offsets = new long[count];
        for (uint i = 0; i < count; i++)
            writer.Write(0L);

        for (uint i = 0; i < count; i++)
        {
            offsets[i] = stream.Position;
            harnesses[i].SaveCheckpoint(stream);
        }

        // Backfill the recorded byte offsets into the reserved table.
        stream.Position = offsetTablePosition;
        for (uint i = 0; i < count; i++)
            writer.Write(offsets[i]);
    }

    /// <summary>
    /// Reconstructs a fleet of <see cref="ISimulationHarness"/> instances from a <c>.cdtbin</c> archive on disk.
    /// </summary>
    /// <typeparam name="THarness">Concrete dimension-specific <see cref="ISimulationHarness"/> implementation.</typeparam>
    /// <param name="config">Active run configuration specifying checkpoint source, target replica count, and physics couplings.</param>
    /// <returns>An initialized array of <see cref="ISimulationHarness"/> instances ready for sweep execution.</returns>
    public static ISimulationHarness[] LoadEnsemble<THarness>(RunConfig config)
        where THarness : ISimulationHarness
    {
        if (string.IsNullOrWhiteSpace(config.Seed.StartFrom))
            throw new InvalidOperationException("Seed.StartFrom must be specified when loading a checkpoint.");
        if (config.Simulation.Replicas == 0)
            throw new InvalidOperationException("Simulation.Replicas must be > 0.");

        var path = config.Seed.StartFrom;
        if (!File.Exists(path))
            throw new FileNotFoundException($"Checkpoint file not found: '{path}'.", path);

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var reader = new BinaryReader(stream);

        var magic = reader.ReadUInt32();
        var version = reader.ReadUInt32();
        var spacelikeDims = reader.ReadUInt32();
        var timeslices = reader.ReadUInt32();
        var savedCount = reader.ReadUInt32();

        if (magic != ExpectedMagic)
            throw new InvalidDataException($"Invalid checkpoint file '{path}' (unrecognized magic header).");
        if (version != CurrentVersion)
            throw new InvalidDataException($"Unsupported checkpoint version {version} (expected {CurrentVersion}).");
        if (spacelikeDims != config.Simulation.SpacelikeDimensions)
            throw new InvalidOperationException(
                $"Checkpoint SpacelikeDimensions ({spacelikeDims}) does not match config ({config.Simulation.SpacelikeDimensions}).");
        if (timeslices != config.Seed.Timeslices)
            throw new InvalidOperationException(
                $"Checkpoint Timeslices (T={timeslices}) does not match config Seed.Timeslices (T={config.Seed.Timeslices}).");
        if (savedCount == 0 || savedCount > int.MaxValue)
            throw new InvalidDataException("Checkpoint archive contains an invalid replica count.");

        var offsets = new long[savedCount];
        for (uint i = 0; i < savedCount; i++)
            offsets[i] = reader.ReadInt64();

        var payloadStart = stream.Position;
        var streamLength = stream.Length;
        for (uint i = 0; i < savedCount; i++)
        {
            if (offsets[i] < payloadStart || offsets[i] >= streamLength)
                throw new InvalidDataException($"Checkpoint archive '{path}' contains an out-of-bounds replica offset.");
        }

        var targetReplicas = config.Simulation.Replicas;
        var harnesses = new ISimulationHarness[targetReplicas];

        try
        {
            for (uint i = 0; i < targetReplicas; i++)
            {
                var slotIndex = i % savedCount;
                stream.Position = offsets[slotIndex];

                // Direct continuation (i < savedCount) preserves the saved Pcg32 state;
                // modulo-cloned replicas (i >= savedCount) re-key their Pcg32 stream so trajectories diverge.
                ulong? reseed = i < savedCount ? null : config.Seed.Rng;

                harnesses[i] = THarness.LoadCheckpoint(
                    i,
                    stream,
                    config.Physics,
                    reseed);

                if (config.Physics.TargetVolume > harnesses[i].MaxSimplices)
                    throw new InvalidOperationException(
                        $"Configured Physics.TargetVolume ({config.Physics.TargetVolume}) exceeds checkpoint arena capacity ({harnesses[i].MaxSimplices}).");
            }

            return harnesses;
        }
        catch
        {
            foreach (var h in harnesses)
                h?.Dispose();
            throw;
        }
    }
}