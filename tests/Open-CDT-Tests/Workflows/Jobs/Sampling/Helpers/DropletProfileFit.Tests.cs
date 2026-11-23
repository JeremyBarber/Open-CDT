using Open_CDT.Workflows.Jobs.Sampling.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.Sampling.Helpers;

public sealed class DropletProfileFitTests
{
    [Fact]
    public void Fit_HandlesEmptyAndFlatCylinderProfilesGracefully()
    {
        (var emptyCurve, var emptyW, var emptyStalk, var emptyR2) = DropletProfileFit.Fit([], spacelikeDimensions: 2);
        Assert.Empty(emptyCurve);
        Assert.Equal(0.0, emptyW);
        Assert.Equal(0.0, emptyStalk);
        Assert.Equal(0.0, emptyR2);

        double[] cylinder = [500, 500, 500, 500, 500, 500, 500, 500];
        (var cylCurve, var cylW, var cylStalk, var cylR2) = DropletProfileFit.Fit(cylinder, spacelikeDimensions: 2);
        Assert.Equal(8.0, cylW, tolerance: 1e-12);
        Assert.Equal(500.0, cylStalk, tolerance: 1e-12);
        Assert.Equal(0.0, cylR2, tolerance: 1e-12);
        Assert.All(cylCurve, v => Assert.Equal(500.0, v, tolerance: 1e-12));
    }

    [Theory]
    [InlineData(2u)]
    [InlineData(3u)]
    public void Fit_RecoversWidthStalkAndSubSliceOffsetAcross2Plus1DAnd3Plus1D(uint spacelikeDimensions)
    {
        const int timeslices = 32;
        const double expectedWidth = 20.0;
        const double expectedStalk = 20.0;
        const double shiftedCenter = 15.65;

        var synthetic = new double[timeslices];
        for (var t = 0; t < timeslices; t++)
        {
            var dt = t - shiftedCenter;
            dt -= timeslices * Math.Round(dt / timeslices);

            synthetic[t] = Math.Abs(dt) <= expectedWidth * 0.5
                ? expectedStalk + (1500.0 * Math.Pow(Math.Cos(Math.PI * dt / expectedWidth), spacelikeDimensions))
                : expectedStalk;
        }

        (_, var fittedWidth, var fittedStalk, var rSquared) =
            DropletProfileFit.Fit(synthetic, spacelikeDimensions);

        Assert.Equal(expectedWidth, fittedWidth, tolerance: 0.11);
        Assert.Equal(expectedStalk, fittedStalk, tolerance: 1e-6);
        Assert.True(rSquared > 0.9999);
    }
}