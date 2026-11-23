using System.Numerics;

namespace Open_CDT.Statistics;

/// <summary>
/// Allocation-free descriptive statistics and ordinary least-squares regression over contiguous spans.
/// </summary>
internal static class SeriesStatistics
{
    /// <summary>
    /// Computes the arithmetic mean and population standard deviation (<c>div N</c>) of a value span.
    /// </summary>
    /// <typeparam name="T">Numeric value type of the input observations.</typeparam>
    /// <param name="values">Contiguous span of population measurements.</param>
    /// <returns>The arithmetic mean and population standard deviation.</returns>
    public static (double Mean, double StdDev) ComputePopulation<T>(ReadOnlySpan<T> values)
        where T : struct, INumberBase<T>
    {
        if (values.IsEmpty)
            return (0.0, 0.0);

        (var mean, var sumSq) = Accumulate(values);
        return (mean, Math.Sqrt(sumSq / values.Length));
    }

    /// <summary>
    /// Computes the arithmetic mean, Bessel-corrected sample standard deviation (<c>div (N - 1)</c>),
    /// and standard error of the mean (<c>SEM = StdDev / sqrt(N)</c>) of a value span.
    /// </summary>
    /// <typeparam name="T">Numeric value type of the input observations.</typeparam>
    /// <param name="values">Contiguous span of sample measurements.</param>
    /// <returns>The sample mean, unbiased sample standard deviation, and standard error of the mean.</returns>
    public static (double Mean, double StdDev, double Sem) ComputeSample<T>(ReadOnlySpan<T> values)
        where T : struct, INumberBase<T>
    {
        if (values.IsEmpty)
            return (0.0, 0.0, 0.0);

        (var mean, var sumSq) = Accumulate(values);
        if (values.Length < 2)
            return (mean, 0.0, 0.0);

        var stdDev = Math.Sqrt(sumSq / (values.Length - 1));
        return (mean, stdDev, stdDev / Math.Sqrt(values.Length));
    }

    /// <summary>
    /// Evaluates ordinary least-squares linear regression <c>y = Slope * x + Intercept</c>.
    /// </summary>
    /// <param name="x">Independent variable coordinates.</param>
    /// <param name="y">Dependent variable observations (must match <paramref name="x"/> in length).</param>
    /// <returns>The fitted slope and vertical intercept, or <c>(0, mean(y))</c> when degenerate.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="x"/> and <paramref name="y"/> differ in length.</exception>
    public static (double Slope, double Intercept) FitLinear(ReadOnlySpan<double> x, ReadOnlySpan<double> y)
    {
        if (x.Length != y.Length)
            throw new ArgumentException("Coordinate spans must have identical lengths.");

        var n = x.Length;
        if (n == 0)
            return (0.0, 0.0);

        double sumX = 0.0, sumY = 0.0, sumXY = 0.0, sumXX = 0.0;
        for (var i = 0; i < n; i++)
        {
            sumX += x[i];
            sumY += y[i];
            sumXY += x[i] * y[i];
            sumXX += x[i] * x[i];
        }

        var denom = (n * sumXX) - (sumX * sumX);
        if (n < 2 || Math.Abs(denom) < 1e-15)
            return (0.0, sumY / n);

        var slope = ((n * sumXY) - (sumX * sumY)) / denom;
        return (slope, (sumY - (slope * sumX)) / n);
    }

    private static (double Mean, double SumSq) Accumulate<T>(ReadOnlySpan<T> values)
        where T : struct, INumberBase<T>
    {
        var sum = 0.0;
        foreach (var v in values)
            sum += double.CreateChecked(v);

        var mean = sum / values.Length;
        var sumSq = 0.0;
        foreach (var v in values)
        {
            var diff = double.CreateChecked(v) - mean;
            sumSq += diff * diff;
        }

        return (mean, sumSq);
    }
}