using Xunit;

using Progress = Open_CDT_CLI.ConsoleWriter.Components.Progress;

namespace Open_CDT_CLI_Tests.ConsoleWriter.Components;

public sealed class ProgressTests
{
    [Theory]
    [InlineData(0.0, 100.0, 10, "[░░░░░░░░░░]")]
    [InlineData(25.0, 100.0, 8, "[██░░░░░░]")]
    [InlineData(50.0, 100.0, 10, "[█████░░░░░]")]
    [InlineData(100.0, 100.0, 10, "[██████████]")]
    public void ShowBar_WithInRangeValues_RendersProportionalFilledAndEmptyBlocks(
        double completed,
        double total,
        int width,
        string expected)
    {
        Assert.Equal(expected, Progress.ShowBar(completed, total, width));
    }

    [Theory]
    [InlineData(-50.0, 100.0, 10, "[░░░░░░░░░░]")]
    [InlineData(250.0, 100.0, 10, "[██████████]")]
    [InlineData(50.0, 0.0, 10, "[░░░░░░░░░░]")]
    [InlineData(50.0, -10.0, 10, "[░░░░░░░░░░]")]
    public void ShowBar_WithOutOfBoundsOrNonPositiveTotal_ClampsSafelyToBounds(
        double completed,
        double total,
        int width,
        string expected)
    {
        Assert.Equal(expected, Progress.ShowBar(completed, total, width));
    }

    [Theory]
    [InlineData(0.0, 100.0, 10, "[░░░░░░░░░░]   0%")]
    [InlineData(5.0, 100.0, 10, "[█░░░░░░░░░]   5%")]
    [InlineData(50.0, 100.0, 10, "[█████░░░░░]  50%")]
    [InlineData(100.0, 100.0, 10, "[██████████] 100%")]
    [InlineData(150.0, 100.0, 10, "[██████████] 100%")]
    [InlineData(-20.0, 100.0, 10, "[░░░░░░░░░░]   0%")]
    [InlineData(10.0, 0.0, 10, "[░░░░░░░░░░]   0%")]
    public void ShowProgress_FormatsBarWithRightAlignedPercentage(
        double completed,
        double total,
        int width,
        string expected)
    {
        Assert.Equal(expected, Progress.ShowProgress(completed, total, width));
    }
}