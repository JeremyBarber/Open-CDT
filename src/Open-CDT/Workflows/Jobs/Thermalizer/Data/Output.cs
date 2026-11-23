using Open_CDT.Config;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Open_CDT.Workflows.Jobs.Thermalizer.Data;

/// <summary>
/// Trailing-window equilibrium summary and autocorrelation metrics for a single replica chain.
/// </summary>
internal readonly record struct ReplicaThermalizationRecord(
    uint ReplicaId,
    double MeanSimplices,
    double MeanVertices,
    int TauInt,
    bool TauConverged)
{
    /// <summary>Gets the intensive vertex-to-simplex ratio <c>&lt;N_0&gt; / &lt;N_d&gt;</c>.</summary>
    public double VertexDensity => MeanSimplices > 0.0 ? MeanVertices / MeanSimplices : 0.0;
}

/// <summary>
/// Multi-replica equilibrium and autocorrelation dataset produced by <see cref="ThermalizerJob"/>.
/// </summary>
internal sealed record ThermalizationResult(
    uint TargetVolume,
    IReadOnlyList<ReplicaThermalizationRecord> Replicas)
{
    /// <summary>Gets the worst-case integrated autocorrelation ceiling across all replicas (<c>max_r tau_int</c>).</summary>
    public int EnsembleTauInt => Replicas.Count > 0 ? Replicas.Max(r => r.TauInt) : 1;
}

/// <summary>
/// Persists and reloads <c>thermalization.csv</c> replica equilibrium and autocorrelation datasets.
/// </summary>
internal static class Output
{
    private const string FileName = "thermalization.csv";
    private const string TargetHeaderPrefix = "# TargetVolume=";

    private const string ColReplicaId = "ReplicaId";
    private const string ColMeanSimplices = "MeanSimplices";
    private const string ColMeanVertices = "MeanVertices";
    private const string ColTauInt = "TauInt";
    private const string ColTauConverged = "TauConverged";

    public static void WriteToDisk(RunConfig config, ThermalizationResult result)
    {
        Directory.CreateDirectory(config.Output.Directory);
        using var writer = new StreamWriter(Path.Combine(config.Output.Directory, FileName));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{TargetHeaderPrefix}{result.TargetVolume}"));

        csv.WriteField(ColReplicaId);
        csv.WriteField(ColMeanSimplices);
        csv.WriteField(ColMeanVertices);
        csv.WriteField(ColTauInt);
        csv.WriteField(ColTauConverged);
        csv.NextRecord();

        foreach (var row in result.Replicas)
        {
            csv.WriteField(row.ReplicaId);
            csv.WriteField(row.MeanSimplices);
            csv.WriteField(row.MeanVertices);
            csv.WriteField(row.TauInt);
            csv.WriteField(row.TauConverged);
            csv.NextRecord();
        }
    }

    public static bool TryReadFromDisk(
        string directory,
        [NotNullWhen(true)] out ThermalizationResult? result)
    {
        result = null;
        var filePath = Path.Combine(directory, FileName);
        if (!File.Exists(filePath))
            return false;

        uint targetVolume = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (!line.StartsWith('#'))
                break;

            if (line.StartsWith(TargetHeaderPrefix, StringComparison.Ordinal) &&
                uint.TryParse(line.AsSpan(TargetHeaderPrefix.Length), CultureInfo.InvariantCulture, out var parsedTarget))
            {
                targetVolume = parsedTarget;
            }
        }

        if (targetVolume == 0)
            return false;

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            AllowComments = true,
            Comment = '#'
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, csvConfig);

        if (!csv.Read() || !csv.ReadHeader())
            return false;

        var replicas = new List<ReplicaThermalizationRecord>();
        while (csv.Read())
        {
            replicas.Add(new ReplicaThermalizationRecord(
                ReplicaId: csv.GetField<uint>(ColReplicaId),
                MeanSimplices: csv.GetField<double>(ColMeanSimplices),
                MeanVertices: csv.GetField<double>(ColMeanVertices),
                TauInt: Math.Max(1, csv.GetField<int>(ColTauInt)),
                TauConverged: csv.GetField<bool>(ColTauConverged)));
        }

        if (replicas.Count == 0)
            return false;

        result = new ThermalizationResult(targetVolume, replicas);
        return true;
    }
}