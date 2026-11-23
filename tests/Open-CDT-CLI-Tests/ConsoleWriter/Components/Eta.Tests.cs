using Open_CDT_CLI.ConsoleWriter.Components;

using Xunit;

namespace Open_CDT_CLI_Tests.ConsoleWriter.Components;

public sealed class EtaTests
{
    [Theory]
    [InlineData(0, 1.0)]
    [InlineData(-100, 1.0)]
    [InlineData(100, 0.0)]
    [InlineData(100, -0.5)]
    [InlineData(100, 1e-7)]
    public void ComputeSweepRate_WithNonPositiveSweepsOrSubMicrosecondDuration_ReturnsZero(
        int sweepsInStep,
        double stepElapsedSeconds)
    {
        var rate = Eta.ComputeSweepRate(sweepsInStep, stepElapsedSeconds);
        Assert.Equal(0.0, rate);
    }

    [Theory]
    [InlineData(100, 0.25, 400.0)]
    [InlineData(500, 0.5, 1000.0)]
    [InlineData(200, 0.08, 2500.0)]
    public void ComputeSweepRate_WithValidInputs_ReturnsExactSweepsPerSecond(
        int sweepsInStep,
        double stepElapsedSeconds,
        double expectedRate)
    {
        var rate = Eta.ComputeSweepRate(sweepsInStep, stepElapsedSeconds);
        Assert.Equal(expectedRate, rate, tolerance: 1e-9);
    }

    [Fact]
    public void ShowSweepRate_WithZeroRate_ReturnsEmptyString()
    {
        Assert.Equal(string.Empty, Eta.ShowSweepRate(0, 1.0));
        Assert.Equal(string.Empty, Eta.ShowSweepRate(100, 0.0));
    }

    [Fact]
    public void ShowSweepRate_WithPositiveRate_FormatsRightAlignedWithUnits()
    {
        var formatted = Eta.ShowSweepRate(250, 0.1);
        Assert.Equal(" 2,500 sw/s", formatted);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(-120.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void FormatDuration_WithNegativeOrNonFiniteSeconds_ReturnsPlaceholder(double invalidSeconds)
    {
        Assert.Equal("--:--", Eta.FormatDuration(invalidSeconds));
    }

    [Theory]
    [InlineData(0.0, "00:00")]
    [InlineData(9.4, "00:09")]
    [InlineData(9.6, "00:10")]
    [InlineData(59.4, "00:59")]
    [InlineData(59.6, "01:00")]
    [InlineData(65.0, "01:05")]
    [InlineData(3599.0, "59:59")]
    [InlineData(3600.0, "1:00:00")]
    [InlineData(3661.0, "1:01:01")]
    [InlineData(45296.0, "12:34:56")]
    public void FormatDuration_WithValidSeconds_FormatsMmSsBelowOneHourAndHhMmSsAbove(
        double seconds,
        string expected)
    {
        Assert.Equal(expected, Eta.FormatDuration(seconds));
    }
}