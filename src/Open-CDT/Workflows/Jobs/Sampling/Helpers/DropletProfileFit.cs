using Open_CDT.Statistics;

namespace Open_CDT.Workflows.Jobs.Sampling.Helpers;

/// <summary>
/// Fits the continuum spherical proper-time profile <c>N(t) = stalk + A * cos^{d_s}(π (t - t_0) / W)</c>
/// to an ensemble-averaged volume profile for summary reporting and plot overlays.
/// </summary>
internal static class DropletProfileFit
{
    /// <summary>
    /// Evaluates ordinary least-squares droplet fits across widths <c>W in [2, T]</c> (at <c>0.1</c>-slice
    /// reporting resolution) and sub-slice centers <c>t_0 in [T/2 - 1, T/2 + 1]</c>.
    /// </summary>
    public static (double[] Curve, double Width, double Stalk, double RSquared) Fit(
        ReadOnlySpan<double> meanProfile,
        uint spacelikeDimensions)
    {
        var timeslices = meanProfile.Length;
        if (timeslices == 0)
            return ([], 0.0, 0.0, 0.0);

        (var yMean, var popStd) = SeriesStatistics.ComputePopulation(meanProfile);
        var sst = timeslices * popStd * popStd;
        var flatStalk = Math.Max(0.0, yMean);

        var bestCurve = new double[timeslices];
        Array.Fill(bestCurve, flatStalk);

        if (timeslices < 3 || sst <= 0.0)
            return (bestCurve, timeslices, flatStalk, 0.0);

        var power = (int)Math.Max(1u, spacelikeDimensions);
        var nominalCenter = timeslices / 2.0;
        var basis = new double[timeslices];

        var bestSse = sst;
        var bestWidth = (double)timeslices;
        var bestStalk = flatStalk;

        // Absorb ±0.5-slice integer-shift rounding in 0.05-slice increments and scan W at 0.1-slice resolution.
        for (var cStep = -20; cStep <= 20; cStep++)
        {
            var center = nominalCenter + (cStep * 0.05);

            for (var wTenths = 20; wTenths <= timeslices * 10; wTenths++)
            {
                var w = wTenths * 0.1;
                var halfW = w * 0.5;

                for (var t = 0; t < timeslices; t++)
                {
                    var dt = t - center;
                    dt -= timeslices * Math.Round(dt / timeslices);

                    if (Math.Abs(dt) <= halfW)
                    {
                        var c = Math.Max(0.0, Math.Cos(Math.PI * dt / w));
                        basis[t] = power == 2 ? c * c : Math.Pow(c, power);
                    }
                    else
                    {
                        basis[t] = 0.0;
                    }
                }

                (var amp, var stalk) = SeriesStatistics.FitLinear(basis, meanProfile);
                if (stalk < 0.0)
                {
                    stalk = 0.0;
                    double sumBY = 0.0, sumB2 = 0.0;
                    for (var t = 0; t < timeslices; t++)
                    {
                        sumBY += basis[t] * meanProfile[t];
                        sumB2 += basis[t] * basis[t];
                    }

                    amp = sumB2 > 0.0 ? sumBY / sumB2 : 0.0;
                }

                if (amp <= 0.0)
                    continue;

                var sse = 0.0;
                for (var t = 0; t < timeslices; t++)
                {
                    var diff = meanProfile[t] - (stalk + (amp * basis[t]));
                    sse += diff * diff;
                }

                if (sse < bestSse)
                {
                    bestSse = sse;
                    bestWidth = w;
                    bestStalk = stalk;
                    for (var t = 0; t < timeslices; t++)
                        bestCurve[t] = stalk + (amp * basis[t]);
                }
            }
        }

        var rSquared = Math.Clamp(1.0 - (bestSse / sst), 0.0, 1.0);
        return (bestCurve, bestWidth, bestStalk, rSquared);
    }
}