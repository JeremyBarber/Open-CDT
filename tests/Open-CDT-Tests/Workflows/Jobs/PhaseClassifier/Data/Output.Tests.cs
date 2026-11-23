using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;

using Xunit;
using PhaseOutput = Open_CDT.Workflows.Jobs.PhaseClassifier.Data.Output;

namespace Open_CDT_Tests.Workflows.Jobs.PhaseClassifier.Data;

public sealed class OutputTests : IDisposable
{
    private readonly string _tempDir;

    public OutputTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_phase_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteToDisk_And_TryReadFromDisk_RoundTripMetadataAndClassifiedPoints()
    {
        PhasePointRecord[] points =
        [
            new(2.5, 0.6, 1.15, 16002.0, 31.4, 0.175, 0.088, 0.892, SpacetimePhase.PhaseC),
            new(6.0, 0.6, 1.52, 15998.0, 30.8, 0.246, 0.064, 0.521, SpacetimePhase.PhaseA)
        ];

        var original = new PhaseSpaceResult(SpacelikeDimensions: 2, Timeslices: 32, TargetVolume: 16000, Points: points);
        var config = new RunConfig { Output = new OutputParameters { Directory = _tempDir } };

        PhaseOutput.WriteToDisk(config, original);

        Assert.True(PhaseOutput.TryReadFromDisk(_tempDir, out var loaded));
        Assert.NotNull(loaded);
        Assert.Equal(2u, loaded.SpacelikeDimensions);
        Assert.Equal(32u, loaded.Timeslices);
        Assert.Equal(16000u, loaded.TargetVolume);
        Assert.Equal(points, loaded.Points);
    }

    [Fact]
    public void TryReadFromDisk_ReturnsFalseOnMissingFileOrInvalidHeaders()
    {
        Assert.False(PhaseOutput.TryReadFromDisk(_tempDir, out _));

        File.WriteAllText(Path.Combine(_tempDir, "phase_space.csv"), "# SpacelikeDimensions=2\nK0,Delta\n");
        Assert.False(PhaseOutput.TryReadFromDisk(_tempDir, out _));
    }
}