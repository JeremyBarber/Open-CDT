using Open_CDT_CLI.ConsoleWriter.Components;

using Xunit;

namespace Open_CDT_CLI_Tests.ConsoleWriter.Components;

public sealed class LogoTests
{
    [Fact]
    public void RenderRow_WithEmptyString_ReturnsEmptyStringWithoutAnsiCodes()
    {
        var result = Logo.RenderRow(string.Empty, rowIndex: 0, maxCols: 95, waveCenter: 0.0, isFinalFrame: false);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void RenderRow_PreservesSpacesUnstyledAndTerminatesWithAnsiReset()
    {
        var result = Logo.RenderRow("   ", rowIndex: 0, maxCols: 95, waveCenter: 0.0, isFinalFrame: true);
        Assert.Equal("   \x1b[0m", result);
    }

    [Fact]
    public void RenderRow_WhenMaxColsIsSmallerThanRowLength_ClipsOutputToMaxCols()
    {
        // Only the first 2 characters ("AB") should be emitted before the trailing reset sequence
        var result = Logo.RenderRow("ABCDEF", rowIndex: 0, maxCols: 2, waveCenter: 0.0, isFinalFrame: true);

        Assert.Contains("A", result);
        Assert.Contains("B", result);
        Assert.DoesNotContain("C", result);
        Assert.EndsWith("\x1b[0m", result);
    }

    [Fact]
    public void RenderRow_OnFinalFrame_AppliesVerticalCyanToOceanBlueGradient()
    {
        // Row 0 (tRow = 0): RGB = (90, 210, 255)
        var topRow = Logo.RenderRow("#", rowIndex: 0, maxCols: 95, waveCenter: 100.0, isFinalFrame: true);
        Assert.Equal("\x1b[38;2;90;210;255m#\x1b[0m", topRow);

        // Row 8 (tRow = 1): RGB = (90 - 55, 210 - 105, 255 - 50) = (35, 105, 205)
        var bottomRow = Logo.RenderRow("═", rowIndex: 8, maxCols: 95, waveCenter: 100.0, isFinalFrame: true);
        Assert.Equal("\x1b[38;2;35;105;205m═\x1b[0m", bottomRow);
    }

    [Fact]
    public void RenderRow_DuringWaveAnimation_TraversesAllFourWaveZones()
    {
        // At rowIndex = 0, c = 0 -> pos = 0.0, delta = waveCenter:
        // 1. Pre-wave dim slate zone (delta = -10.0 < -6.0): RGB = (30, 45, 65)
        var preWave = Logo.RenderRow("#", rowIndex: 0, maxCols: 95, waveCenter: -10.0, isFinalFrame: false);
        Assert.Equal("\x1b[38;2;30;45;65m#\x1b[0m", preWave);

        // 2. Wave crest (delta = 0.0): pure white highlight RGB = (255, 255, 255)
        var crest = Logo.RenderRow("#", rowIndex: 0, maxCols: 95, waveCenter: 0.0, isFinalFrame: false);
        Assert.Equal("\x1b[38;2;255;255;255m#\x1b[0m", crest);

        // 3. Leading ramp (delta = -3.0, t = 0.5): midpoint between (30, 45, 65) and (255, 255, 255)
        var leadingRamp = Logo.RenderRow("#", rowIndex: 0, maxCols: 95, waveCenter: -3.0, isFinalFrame: false);
        Assert.Equal("\x1b[38;2;142;150;160m#\x1b[0m", leadingRamp);

        // 4. Post-wave resting zone (delta = 25.0 >= 18.0): resting row 0 color (90, 210, 255)
        var postWave = Logo.RenderRow("#", rowIndex: 0, maxCols: 95, waveCenter: 25.0, isFinalFrame: false);
        Assert.Equal("\x1b[38;2;90;210;255m#\x1b[0m", postWave);
    }

    [Fact]
    public void GetMaxColumns_AlwaysReturnsPositiveColumnLimit()
    {
        Assert.True(Logo.GetMaxColumns() > 0);
    }
}