using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;

using Xunit;
using TunerOutput = Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data.Output;

namespace Open_CDT_Tests.Workflows.Jobs.CosmologicalConstantTuner.Data;

public sealed class OutputTests : IDisposable
{
    private readonly string _tempDir;

    public OutputTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"open_cdt_tuner_out_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void BuildCriticalPoints_ProjectsSaddlePointAndSafeguardsObservedBounds()
    {
        ProbeRecord[] history =
        [
            new(K0: 2.0, Delta: 0.5, Kd: 0.90, MeanVolume: 16100.0, StdDevVolume: 30.0, MeanVertices: 2900.0, ProbeOutcome.AboveTarget),
            new(K0: 2.0, Delta: 0.5, Kd: 1.00, MeanVolume: 16010.0, StdDevVolume: 31.0, MeanVertices: 2880.0, ProbeOutcome.LockedOnTarget),
            new(K0: 2.0, Delta: 0.5, Kd: 1.10, MeanVolume: 15900.0, StdDevVolume: 30.0, MeanVertices: 2860.0, ProbeOutcome.BelowTarget)
        ];

        var criticals = TunerOutput.BuildCriticalPoints(history, targetVolume: 16000, epsilon: 0.0005);
        var pt = Assert.Single(criticals);

        // 1.00 + 2 * 0.0005 * (16010 - 16000) = 1.010 (inside [0.90, 1.10]).
        Assert.Equal(1.010, pt.KdCrit, tolerance: 1e-12);
        Assert.Equal(16010.0, pt.MeanVolume, tolerance: 1e-12);
        Assert.Equal(2880.0 / 16010.0, pt.VertexDensity, tolerance: 1e-12);

        // Linear step from best (1.00 + 2 * 0.0005 * 150 = 1.15) overshoots highBound (1.04) -> bisects to 1.02.
        ProbeRecord[] overshootHistory =
        [
            new(K0: 2.0, Delta: 0.5, Kd: 1.00, MeanVolume: 10150.0, StdDevVolume: 25.0, MeanVertices: 1900.0, ProbeOutcome.AboveTarget),
            new(K0: 2.0, Delta: 0.5, Kd: 1.04, MeanVolume: 2000.0,  StdDevVolume: 5.0,  MeanVertices: 400.0,  ProbeOutcome.BelowTarget)
        ];

        var safeguarded = Assert.Single(TunerOutput.BuildCriticalPoints(overshootHistory, targetVolume: 10000, epsilon: 0.0005));
        Assert.Equal(1.020, safeguarded.KdCrit, tolerance: 1e-12);
    }

    [Fact]
    public void WriteToDisk_And_TryReadFromDisk_RoundTripHistoryAndAnalyticalProperties()
    {
        ProbeRecord[] history =
        [
            new(K0: 1.5, Delta: 0.6, Kd: 0.95, MeanVolume: 10020.0, StdDevVolume: 22.0, MeanVertices: 1800.0, ProbeOutcome.LockedOnTarget)
        ];

        var criticals = TunerOutput.BuildCriticalPoints(history, targetVolume: 10000, epsilon: 0.0005);
        var original = new TuningResult(10000, 0.005, 0.0005, criticals, history);
        var config = new RunConfig { Output = new OutputParameters { Directory = _tempDir } };

        TunerOutput.WriteToDisk(config, original);

        Assert.True(TunerOutput.TryReadFromDisk(_tempDir, out var loaded));
        Assert.NotNull(loaded);
        Assert.Equal(10000u, loaded.TargetVolume);
        Assert.Equal(0.005, loaded.ToleranceRatio, tolerance: 1e-12);
        Assert.Equal(0.0005, loaded.Epsilon, tolerance: 1e-12);
        Assert.Equal(-1000.0, loaded.SusceptibilitySlope, tolerance: 1e-12);
        Assert.Equal(original.TunedKd, loaded.TunedKd, tolerance: 1e-12);
        Assert.Single(loaded.History);
    }

    [Fact]
    public void TryReadFromDisk_ReturnsFalseWhenMissingOrHeadersIncomplete()
    {
        Assert.False(TunerOutput.TryReadFromDisk(_tempDir, out _));

        File.WriteAllText(Path.Combine(_tempDir, "tuning.csv"), "# TargetVolume=10000\nK0,Delta,Kd\n");
        Assert.False(TunerOutput.TryReadFromDisk(_tempDir, out _));
    }
}