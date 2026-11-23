namespace CDT_Agnostic.Config;

/// <summary>
/// Configuration parameters governing initial spacetime triangulation seeding or binary checkpoint resumption.
/// </summary>
public sealed record SeedParameters
{
    /// <summary>Spatial geodesic icosahedral subdivision frequency (<c>k &gt;= 1</c>) when cold-seeding.</summary>
    public uint K { get; init; }

    /// <summary>Total number of discrete proper-time slices (<c>T &gt;= 3</c>) in the periodic foliation.</summary>
    public uint Timeslices { get; init; }

    /// <summary>Base 64-bit seed used to initialize deterministic per-replica <c>Pcg32</c> streams.</summary>
    public ulong Rng { get; init; }

    /// <summary>Optional path to a <c>.cdtbin</c> checkpoint archive to resume from instead of cold-seeding.</summary>
    public string? StartFrom { get; init; }
}