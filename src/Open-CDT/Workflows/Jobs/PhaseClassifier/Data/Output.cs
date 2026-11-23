using Open_CDT.Config;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier.Data;

/// <summary>
/// Macroscopic CDT spacetime phase classification along the critical surface <c>k_d^c(k_0, Delta)</c>.
/// </summary>
internal enum SpacetimePhase
{
    PhaseA,
    PhaseB,
    PhaseC
}

/// <summary>
/// Equilibrium order parameters and phase classification for a single critical coupling coordinate.
/// </summary>
internal sealed record PhasePointRecord(
    double K0,
    double Delta,
    double KdCrit,
    double MeanVolume,
    double StdDevVolume,
    double VertexDensity,
    double PeakSliceFraction,
    double TemporalContinuity,
    SpacetimePhase Phase);

/// <summary>
/// Complete output dataset produced by <see cref="PhaseClassifierJob"/>.
/// </summary>
internal sealed record PhaseSpaceResult(
    uint SpacelikeDimensions,
    uint Timeslices,
    uint TargetVolume,
    IReadOnlyList<PhasePointRecord> Points);

/// <summary>
/// Persists and reloads <c>phase_space.csv</c> order-parameter and phase-classification datasets.
/// </summary>
internal static class Output
{
    private const string FileName = "phase_space.csv";
    private const string DimHeaderPrefix = "# SpacelikeDimensions=";
    private const string SlicesHeaderPrefix = "# Timeslices=";
    private const string TargetHeaderPrefix = "# TargetVolume=";

    private const string ColK0 = "K0";
    private const string ColDelta = "Delta";
    private const string ColKdCrit = "KdCrit";
    private const string ColMeanVolume = "MeanVolume";
    private const string ColStdDevVolume = "StdDevVolume";
    private const string ColVertexDensity = "VertexDensity";
    private const string ColPeakSliceFraction = "PeakSliceFraction";
    private const string ColTemporalContinuity = "TemporalContinuity";
    private const string ColPhase = "Phase";

    public static void WriteToDisk(RunConfig config, PhaseSpaceResult result)
    {
        Directory.CreateDirectory(config.Output.Directory);
        using var writer = new StreamWriter(Path.Combine(config.Output.Directory, FileName));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{DimHeaderPrefix}{result.SpacelikeDimensions}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{SlicesHeaderPrefix}{result.Timeslices}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{TargetHeaderPrefix}{result.TargetVolume}"));

        csv.WriteField(ColK0);
        csv.WriteField(ColDelta);
        csv.WriteField(ColKdCrit);
        csv.WriteField(ColMeanVolume);
        csv.WriteField(ColStdDevVolume);
        csv.WriteField(ColVertexDensity);
        csv.WriteField(ColPeakSliceFraction);
        csv.WriteField(ColTemporalContinuity);
        csv.WriteField(ColPhase);
        csv.NextRecord();

        foreach (var p in result.Points)
        {
            csv.WriteField(p.K0);
            csv.WriteField(p.Delta);
            csv.WriteField(p.KdCrit);
            csv.WriteField(p.MeanVolume);
            csv.WriteField(p.StdDevVolume);
            csv.WriteField(p.VertexDensity);
            csv.WriteField(p.PeakSliceFraction);
            csv.WriteField(p.TemporalContinuity);
            csv.WriteField(p.Phase.ToString());
            csv.NextRecord();
        }
    }

    public static bool TryReadFromDisk(
        string directory,
        [NotNullWhen(true)] out PhaseSpaceResult? result)
    {
        result = null;
        var filePath = Path.Combine(directory, FileName);
        if (!File.Exists(filePath))
            return false;

        uint spacelikeDims = 0;
        uint timeslices = 0;
        uint targetVolume = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (!line.StartsWith('#'))
                break;

            if (line.StartsWith(DimHeaderPrefix, StringComparison.Ordinal) &&
                uint.TryParse(line.AsSpan(DimHeaderPrefix.Length), CultureInfo.InvariantCulture, out var d))
            {
                spacelikeDims = d;
            }
            else if (line.StartsWith(SlicesHeaderPrefix, StringComparison.Ordinal) &&
                uint.TryParse(line.AsSpan(SlicesHeaderPrefix.Length), CultureInfo.InvariantCulture, out var t))
            {
                timeslices = t;
            }
            else if (line.StartsWith(TargetHeaderPrefix, StringComparison.Ordinal) &&
                uint.TryParse(line.AsSpan(TargetHeaderPrefix.Length), CultureInfo.InvariantCulture, out var v))
            {
                targetVolume = v;
            }
        }

        if (spacelikeDims == 0 || timeslices == 0 || targetVolume == 0)
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

        var points = new List<PhasePointRecord>();
        while (csv.Read())
        {
            if (!Enum.TryParse<SpacetimePhase>(csv.GetField(ColPhase), out var phase))
                return false;

            points.Add(new PhasePointRecord(
                K0: csv.GetField<double>(ColK0),
                Delta: csv.GetField<double>(ColDelta),
                KdCrit: csv.GetField<double>(ColKdCrit),
                MeanVolume: csv.GetField<double>(ColMeanVolume),
                StdDevVolume: csv.GetField<double>(ColStdDevVolume),
                VertexDensity: csv.GetField<double>(ColVertexDensity),
                PeakSliceFraction: csv.GetField<double>(ColPeakSliceFraction),
                TemporalContinuity: csv.GetField<double>(ColTemporalContinuity),
                Phase: phase));
        }

        if (points.Count == 0)
            return false;

        result = new PhaseSpaceResult(spacelikeDims, timeslices, targetVolume, points);
        return true;
    }
}