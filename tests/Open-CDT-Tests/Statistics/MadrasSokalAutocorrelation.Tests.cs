using Open_CDT.Statistics;

using Xunit;

namespace Open_CDT_Tests.Statistics;

public sealed class MadrasSokalAutocorrelationTests
{
    [Fact]
    public void Compute_WithShortOrConstantSeries_ReturnsUnconvergedFallbackOfOne()
    {
        var shortResult = MadrasSokalAutocorrelation.Compute([10, 20, 30]);
        Assert.False(shortResult.Converged);
        Assert.Equal(1, shortResult.TauInt);
        Assert.Equal(0.5, shortResult.RawTauInt);
        Assert.Single(shortResult.Acf);

        var constantSeries = new int[64];
        Array.Fill(constantSeries, 16000);

        var constResult = MadrasSokalAutocorrelation.Compute(constantSeries);
        Assert.False(constResult.Converged);
        Assert.Equal(1, constResult.TauInt);
        Assert.Equal(0.5, constResult.RawTauInt);
    }

    [Fact]
    public void Compute_AlternatingAntiCorrelatedSeries_ConvergesAtLagOneWithMinimumTauOne()
    {
        var alternating = new int[64];
        for (var i = 0; i < alternating.Length; i++)
            alternating[i] = (i % 2 == 0) ? 10 : 20;

        var result = MadrasSokalAutocorrelation.Compute(alternating);

        Assert.True(result.Converged);
        Assert.Equal(1, result.TauInt);
        Assert.Equal(-0.5, result.RawTauInt, tolerance: 1e-12);
        Assert.Equal(2, result.Acf.Length);
        Assert.Equal(1.0, result.Acf[0], tolerance: 1e-12);
        Assert.Equal(-1.0, result.Acf[1], tolerance: 1e-12);
    }

    [Fact]
    public void Compute_PeriodicSquareWave_ConvergesWhenMadrasSokalWindowConditionIsMet()
    {
        // Square wave with half-period = 16 over N = 512 samples -> converges at lag k = 14 with TauInt = 2
        var squareWave = new int[512];
        for (var i = 0; i < squareWave.Length; i++)
            squareWave[i] = ((i / 16) % 2 == 0) ? 20 : 10;

        var result = MadrasSokalAutocorrelation.Compute(squareWave);

        Assert.True(result.Converged);
        Assert.Equal(2, result.TauInt);
        Assert.Equal(1.539887, result.RawTauInt, tolerance: 1e-5);
        Assert.Equal(15, result.Acf.Length);
    }

    [Fact]
    public void Compute_NonStationaryLinearRamp_ReturnsConvergedFalseAtMaxLag()
    {
        var ramp = new int[40];
        for (var i = 0; i < ramp.Length; i++)
            ramp[i] = i;

        var result = MadrasSokalAutocorrelation.Compute(ramp);

        Assert.False(result.Converged);
        Assert.Equal(8, result.TauInt);
        Assert.Equal(11, result.Acf.Length); // 0 .. maxLag (40 / 4 = 10)
    }
}