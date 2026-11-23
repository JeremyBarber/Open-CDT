using Open_CDT.Statistics;

using Xunit;

namespace Open_CDT_Tests.Statistics;

public sealed class SeriesStatisticsTests
{
    [Fact]
    public void ComputePopulation_HandlesEmptySingleAndKnownPopulation()
    {
        Assert.Equal((0.0, 0.0), SeriesStatistics.ComputePopulation<int>([]));
        Assert.Equal((42.0, 0.0), SeriesStatistics.ComputePopulation([42]));

        (var mean, var stdDev) = SeriesStatistics.ComputePopulation([2, 4, 4, 4, 5, 5, 7, 9]);
        Assert.Equal(5.0, mean, tolerance: 1e-12);
        Assert.Equal(2.0, stdDev, tolerance: 1e-12);
    }

    [Fact]
    public void ComputeSample_HandlesEmptySingleAndBesselCorrectedSample()
    {
        Assert.Equal((0.0, 0.0, 0.0), SeriesStatistics.ComputeSample<double>([]));
        Assert.Equal((42.5, 0.0, 0.0), SeriesStatistics.ComputeSample([42.5]));

        (var mean, var sd, var sem) = SeriesStatistics.ComputeSample([2.0, 4.0, 4.0, 4.0, 5.0, 5.0, 7.0, 9.0]);
        var expectedSd = Math.Sqrt(32.0 / 7.0);
        Assert.Equal(5.0, mean, tolerance: 1e-12);
        Assert.Equal(expectedSd, sd, tolerance: 1e-12);
        Assert.Equal(expectedSd / Math.Sqrt(8.0), sem, tolerance: 1e-12);
    }

    [Fact]
    public void FitLinear_HandlesDegenerateCollinearAndMismatchedInputs()
    {
        Assert.Throws<ArgumentException>(() => SeriesStatistics.FitLinear([1.0, 2.0], [1.0]));
        Assert.Equal((0.0, 0.0), SeriesStatistics.FitLinear([], []));
        Assert.Equal((0.0, 10.0), SeriesStatistics.FitLinear([2.0], [10.0]));
        Assert.Equal((0.0, 5.0), SeriesStatistics.FitLinear([1.0, 1.0], [4.0, 6.0]));

        (var slope, var intercept) = SeriesStatistics.FitLinear([1.0, 2.0, 3.0], [5.0, 7.0, 9.0]);
        Assert.Equal(2.0, slope, tolerance: 1e-12);
        Assert.Equal(3.0, intercept, tolerance: 1e-12);
    }
}