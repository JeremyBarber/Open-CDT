using CDT_Agnostic.Measurements.Observables;
using Open_CDT.Config;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Open_CDT.Workflows.Jobs.Sampling.Data;

/// <summary>
/// Single decorrelated observable snapshot recorded on one replica chain.
/// </summary>
internal sealed record ObservableSampleRecord(
    uint ReplicaId,
    int SampleIndex,
    int[] CenteredVolumeProfile,
    HausdorffResult Hausdorff,
    SpectralDimensionResult Spectral);

/// <summary>
/// Complete multi-replica observable dataset produced by <see cref="SamplingJob"/>.
/// </summary>
internal sealed record SamplingResult(
    uint SpacelikeDimensions,
    int SweepsBetweenSamples,
    IReadOnlyList<ObservableSampleRecord> Samples);

/// <summary>
/// Persists and reloads <c>volume_profile.csv</c>, <c>hausdorff.csv</c>, and <c>spectral.csv</c>.
/// </summary>
internal static class Output
{
    private const string VolumeProfileFileName = "volume_profile.csv";
    private const string HausdorffFileName = "hausdorff.csv";
    private const string SpectralFileName = "spectral.csv";

    private const string ColReplicaId = "ReplicaId";
    private const string ColSample = "Sample";
    private const string ColFittedDimension = "FittedDimension";
    private const string ColEstimatedDimension = "EstimatedDimension";

    private const string PrefixTimeslice = "t_";
    private const string PrefixRadius = "r_";
    private const string PrefixSigma = "sigma_";
    private const string HeaderDimsPrefix = "# SpacelikeDimensions=";
    private const string HeaderSweepsPrefix = "# SweepsBetweenSamples=";

    public static void WriteToDisk(RunConfig config, SamplingResult result)
    {
        var dir = config.Output.Directory;
        Directory.CreateDirectory(dir);
        WriteVolumeProfileCsv(Path.Combine(dir, VolumeProfileFileName), result);
        WriteHausdorffCsv(Path.Combine(dir, HausdorffFileName), result.Samples);
        WriteSpectralCsv(Path.Combine(dir, SpectralFileName), result.Samples);
    }

    public static bool TryReadFromDisk(
        string directory,
        [NotNullWhen(true)] out SamplingResult? result)
    {
        result = null;

        var volPath = Path.Combine(directory, VolumeProfileFileName);
        var hausPath = Path.Combine(directory, HausdorffFileName);
        var specPath = Path.Combine(directory, SpectralFileName);

        if (!File.Exists(volPath) || !File.Exists(hausPath) || !File.Exists(specPath))
            return false;

        (var spacelikeDims, var sweepsBetweenSamples) = ReadVolumeMetadataHeaders(volPath);
        if (spacelikeDims == 0 || sweepsBetweenSamples <= 0)
            return false;

        var volRows = ReadVolumeProfileCsv(volPath);
        var hausRows = ReadHausdorffCsv(hausPath);
        var specRows = ReadSpectralCsv(specPath);

        var count = volRows.Count;
        if (count == 0 || hausRows.Count != count || specRows.Count != count)
            return false;

        var samples = new List<ObservableSampleRecord>(count);
        for (var i = 0; i < count; i++)
        {
            if (hausRows[i].ReplicaId != volRows[i].ReplicaId ||
                specRows[i].ReplicaId != volRows[i].ReplicaId ||
                hausRows[i].SampleIndex != volRows[i].SampleIndex ||
                specRows[i].SampleIndex != volRows[i].SampleIndex)
            {
                return false;
            }

            samples.Add(new ObservableSampleRecord(
                ReplicaId: volRows[i].ReplicaId,
                SampleIndex: volRows[i].SampleIndex,
                CenteredVolumeProfile: volRows[i].Profile,
                Hausdorff: hausRows[i].Hausdorff,
                Spectral: specRows[i].Spectral));
        }

        result = new SamplingResult(spacelikeDims, sweepsBetweenSamples, samples);
        return true;
    }

    private static void WriteVolumeProfileCsv(string path, SamplingResult result)
    {
        var samples = result.Samples;
        if (samples.Count == 0)
            return;

        var timeslices = samples[0].CenteredVolumeProfile.Length;

        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{HeaderDimsPrefix}{result.SpacelikeDimensions}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{HeaderSweepsPrefix}{result.SweepsBetweenSamples}"));

        csv.WriteField(ColReplicaId);
        csv.WriteField(ColSample);
        for (var t = 0; t < timeslices; t++)
            csv.WriteField($"{PrefixTimeslice}{t}");
        csv.NextRecord();

        foreach (var s in samples)
        {
            csv.WriteField(s.ReplicaId);
            csv.WriteField(s.SampleIndex);
            for (var t = 0; t < timeslices; t++)
                csv.WriteField(s.CenteredVolumeProfile[t]);
            csv.NextRecord();
        }
    }

    private static void WriteHausdorffCsv(string path, IReadOnlyList<ObservableSampleRecord> samples)
    {
        if (samples.Count == 0)
            return;

        var radiiCount = samples[0].Hausdorff.Profile.Length;

        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField(ColReplicaId);
        csv.WriteField(ColSample);
        csv.WriteField(ColFittedDimension);
        for (var r = 0; r < radiiCount; r++)
            csv.WriteField($"{PrefixRadius}{r}");
        csv.NextRecord();

        foreach (var s in samples)
        {
            csv.WriteField(s.ReplicaId);
            csv.WriteField(s.SampleIndex);
            csv.WriteField(s.Hausdorff.Dimension);
            for (var r = 0; r < radiiCount; r++)
                csv.WriteField(s.Hausdorff.Profile[r]);
            csv.NextRecord();
        }
    }

    private static void WriteSpectralCsv(string path, IReadOnlyList<ObservableSampleRecord> samples)
    {
        if (samples.Count == 0)
            return;

        var sigmaCount = samples[0].Spectral.ReturnProbabilities.Length;

        using var writer = new StreamWriter(path);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField(ColReplicaId);
        csv.WriteField(ColSample);
        csv.WriteField(ColEstimatedDimension);
        for (var sigma = 0; sigma < sigmaCount; sigma++)
            csv.WriteField($"{PrefixSigma}{sigma}");
        csv.NextRecord();

        foreach (var s in samples)
        {
            csv.WriteField(s.ReplicaId);
            csv.WriteField(s.SampleIndex);
            csv.WriteField(s.Spectral.EstimatedDimension);
            for (var sigma = 0; sigma < sigmaCount; sigma++)
                csv.WriteField(s.Spectral.ReturnProbabilities[sigma]);
            csv.NextRecord();
        }
    }

    private static (uint SpacelikeDimensions, int SweepsBetweenSamples) ReadVolumeMetadataHeaders(string filePath)
    {
        uint spacelikeDims = 0;
        var sweeps = 0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (!line.StartsWith('#'))
                break;

            if (line.StartsWith(HeaderDimsPrefix, StringComparison.Ordinal) &&
                uint.TryParse(line.AsSpan(HeaderDimsPrefix.Length), CultureInfo.InvariantCulture, out var parsedDims))
            {
                spacelikeDims = parsedDims;
            }
            else if (line.StartsWith(HeaderSweepsPrefix, StringComparison.Ordinal) &&
                int.TryParse(line.AsSpan(HeaderSweepsPrefix.Length), CultureInfo.InvariantCulture, out var parsedSweeps))
            {
                sweeps = parsedSweeps;
            }
        }

        return (spacelikeDims, sweeps);
    }

    private static List<(uint ReplicaId, int SampleIndex, int[] Profile)> ReadVolumeProfileCsv(string path)
    {
        var rows = new List<(uint, int, int[])>();
        using var reader = new StreamReader(path);
        using var csv = CreateCommentAwareReader(reader);

        if (!TryResolveSeriesColumns(csv, PrefixTimeslice, out var colIndices))
            return rows;

        while (csv.Read())
        {
            var rep = csv.GetField<uint>(ColReplicaId);
            var sample = csv.GetField<int>(ColSample);
            var profile = new int[colIndices.Length];

            for (var t = 0; t < colIndices.Length; t++)
                profile[t] = csv.GetField<int>(colIndices[t]);

            rows.Add((rep, sample, profile));
        }

        return rows;
    }

    private static List<(uint ReplicaId, int SampleIndex, HausdorffResult Hausdorff)> ReadHausdorffCsv(string path)
    {
        var rows = new List<(uint, int, HausdorffResult)>();
        using var reader = new StreamReader(path);
        using var csv = CreateCommentAwareReader(reader);

        if (!TryResolveSeriesColumns(csv, PrefixRadius, out var colIndices))
            return rows;

        var dimCol = csv.GetFieldIndex(ColFittedDimension, isTryGet: true);
        if (dimCol < 0)
            return rows;

        while (csv.Read())
        {
            var rep = csv.GetField<uint>(ColReplicaId);
            var sample = csv.GetField<int>(ColSample);
            var dim = csv.GetField<double>(dimCol);
            var profile = new double[colIndices.Length];

            for (var r = 0; r < colIndices.Length; r++)
                profile[r] = csv.GetField<double>(colIndices[r]);

            rows.Add((rep, sample, new HausdorffResult(dim, profile)));
        }

        return rows;
    }

    private static List<(uint ReplicaId, int SampleIndex, SpectralDimensionResult Spectral)> ReadSpectralCsv(string path)
    {
        var rows = new List<(uint, int, SpectralDimensionResult)>();
        using var reader = new StreamReader(path);
        using var csv = CreateCommentAwareReader(reader);

        if (!TryResolveSeriesColumns(csv, PrefixSigma, out var colIndices))
            return rows;

        var dimCol = csv.GetFieldIndex(ColEstimatedDimension, isTryGet: true);
        if (dimCol < 0)
            return rows;

        while (csv.Read())
        {
            var rep = csv.GetField<uint>(ColReplicaId);
            var sample = csv.GetField<int>(ColSample);
            var dim = csv.GetField<double>(dimCol);
            var probs = new double[colIndices.Length];

            for (var s = 0; s < colIndices.Length; s++)
                probs[s] = csv.GetField<double>(colIndices[s]);

            rows.Add((rep, sample, new SpectralDimensionResult(dim, probs)));
        }

        return rows;
    }

    private static CsvReader CreateCommentAwareReader(StreamReader reader) =>
        new(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            AllowComments = true,
            Comment = '#'
        });

    private static bool TryResolveSeriesColumns(CsvReader csv, string prefix, out int[] columnIndices)
    {
        columnIndices = [];
        if (!csv.Read() || !csv.ReadHeader() || csv.HeaderRecord is null)
            return false;

        if (csv.GetFieldIndex(ColReplicaId, isTryGet: true) < 0 ||
            csv.GetFieldIndex(ColSample, isTryGet: true) < 0)
        {
            return false;
        }

        var count = csv.HeaderRecord.Count(h => h.StartsWith(prefix, StringComparison.Ordinal));
        if (count == 0)
            return false;

        var indices = new int[count];
        for (var i = 0; i < count; i++)
        {
            var idx = csv.GetFieldIndex($"{prefix}{i}", isTryGet: true);
            if (idx < 0)
                return false;

            indices[i] = idx;
        }

        columnIndices = indices;
        return true;
    }
}