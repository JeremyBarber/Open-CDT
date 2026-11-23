using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.Thermalizer.Data;

using Xunit;
using ThermalOutput = Open_CDT.Workflows.Jobs.Thermalizer.Data.Output;

namespace Open_CDT_Tests.Workflows.Jobs.Thermalizer.Data;

public sealed class OutputTests : IDisposable
{
    private readonly string _tempDir;

    public OutputTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_therm_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WriteToDisk_And_TryReadFromDisk_RoundTripTargetVolumeAndReplicas()
    {
        ReplicaThermalizationRecord[] replicas =
        [
            new(ReplicaId: 0, MeanSimplices: 16001.5, MeanVertices: 2880.2, TauInt: 24, TauConverged: true),
            new(ReplicaId: 1, MeanSimplices: 15998.0, MeanVertices: 2879.0, TauInt: 36, TauConverged: false)
        ];

        var original = new ThermalizationResult(TargetVolume: 16000, Replicas: replicas);
        var config = new RunConfig { Output = new OutputParameters { Directory = _tempDir } };

        ThermalOutput.WriteToDisk(config, original);

        Assert.True(ThermalOutput.TryReadFromDisk(_tempDir, out var loaded));
        Assert.NotNull(loaded);
        Assert.Equal(16000u, loaded.TargetVolume);
        Assert.Equal(36, loaded.EnsembleTauInt);
        Assert.Equal(replicas, loaded.Replicas);
        Assert.Equal(2880.2 / 16001.5, loaded.Replicas[0].VertexDensity, tolerance: 1e-12);
    }

    [Fact]
    public void TryReadFromDisk_ReturnsFalseWhenFileMissingOrTargetHeaderAbsent()
    {
        Assert.False(ThermalOutput.TryReadFromDisk(_tempDir, out _));

        File.WriteAllText(
            Path.Combine(_tempDir, "thermalization.csv"),
            "ReplicaId,MeanSimplices,MeanVertices,TauInt,TauConverged\n0,16000,2800,20,true\n");
        Assert.False(ThermalOutput.TryReadFromDisk(_tempDir, out _));
    }
}