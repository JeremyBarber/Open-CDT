using CDT_Agnostic.Measurements.Observables;
using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Sampling.Data;

using Xunit;
using SamplingOutput = Open_CDT.Workflows.Jobs.Sampling.Data.Output;

namespace Open_CDT_Tests.Workflows.Jobs.Sampling.Data;

public sealed class OutputTests : IDisposable
{
    private readonly string _tempDir;

    public OutputTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_samp_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteToDisk_And_TryReadFromDisk_RoundTripAllThreeCsvDatasets()
    {
        ObservableSampleRecord[] samples =
        [
            new(
                ReplicaId: 0,
                SampleIndex: 1,
                CenteredVolumeProfile: [20, 180, 400, 180],
                Hausdorff: new HausdorffResult(2.98, [1.0, 4.0, 12.0, 28.0]),
                Spectral: new SpectralDimensionResult(2.85, [1.0, 0.20, 0.08, 0.04])),
            new(
                ReplicaId: 1,
                SampleIndex: 1,
                CenteredVolumeProfile: [22, 175, 406, 177],
                Hausdorff: new HausdorffResult(3.02, [1.0, 4.0, 13.0, 30.0]),
                Spectral: new SpectralDimensionResult(2.91, [1.0, 0.20, 0.075, 0.038]))
        ];

        var original = new SamplingResult(SpacelikeDimensions: 2, SweepsBetweenSamples: 500, Samples: samples);
        var config = new RunConfig { Output = new OutputParameters { Directory = _tempDir } };

        SamplingOutput.WriteToDisk(config, original);

        Assert.True(SamplingOutput.TryReadFromDisk(_tempDir, out var loaded));
        Assert.NotNull(loaded);
        Assert.Equal(2u, loaded.SpacelikeDimensions);
        Assert.Equal(500, loaded.SweepsBetweenSamples);
        Assert.Equal(2, loaded.Samples.Count);
        Assert.Equal(samples[0].CenteredVolumeProfile, loaded.Samples[0].CenteredVolumeProfile);
        Assert.Equal(2.98, loaded.Samples[0].Hausdorff.Dimension, tolerance: 1e-12);
        Assert.Equal(2.91, loaded.Samples[1].Spectral.EstimatedDimension, tolerance: 1e-12);
    }

    [Fact]
    public void TryReadFromDisk_RejectsDesynchronizedRowCountsOrMismatchedSampleKeys()
    {
        ObservableSampleRecord[] samples =
        [
            new(0, 1, [10, 20], new HausdorffResult(3.0, [1.0, 4.0]), new SpectralDimensionResult(3.0, [1.0, 0.2])),
            new(0, 2, [12, 18], new HausdorffResult(3.0, [1.0, 4.0]), new SpectralDimensionResult(3.0, [1.0, 0.2]))
        ];

        var config = new RunConfig { Output = new OutputParameters { Directory = _tempDir } };
        SamplingOutput.WriteToDisk(config, new SamplingResult(2, 100, samples));

        // 1. Equal row count with mismatched (ReplicaId, Sample) key in hausdorff.csv must fail.
        var hausPath = Path.Combine(_tempDir, "hausdorff.csv");
        var hausLines = File.ReadAllLines(hausPath);
        var originalRow2 = hausLines[2];
        hausLines[2] = "9,2,3,1,4";
        File.WriteAllLines(hausPath, hausLines);

        Assert.False(SamplingOutput.TryReadFromDisk(_tempDir, out _));

        // Restore hausdorff.csv and verify it reads cleanly again.
        hausLines[2] = originalRow2;
        File.WriteAllLines(hausPath, hausLines);
        Assert.True(SamplingOutput.TryReadFromDisk(_tempDir, out _));

        // 2. Truncated row count in spectral.csv must fail.
        var specPath = Path.Combine(_tempDir, "spectral.csv");
        var specLines = File.ReadAllLines(specPath);
        File.WriteAllLines(specPath, specLines[..^1]);

        Assert.False(SamplingOutput.TryReadFromDisk(_tempDir, out _));
    }
}