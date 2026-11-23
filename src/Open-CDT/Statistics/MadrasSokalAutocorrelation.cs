namespace Open_CDT.Statistics;

/// <summary>
/// Encapsulates the result of a Madras-Sokal integrated autocorrelation time analysis.
/// </summary>
/// <param name="Converged">True if the self-consistent cutoff <c>k &gt;= 6 * tau_int</c> was reached within <c>N / 4</c>.</param>
/// <param name="TauInt">Ceiling-rounded integer autocorrelation time (<c>sweeps</c>), guaranteed <c>&gt;= 1</c>.</param>
/// <param name="RawTauInt">Unrounded floating-point integrated autocorrelation time.</param>
/// <param name="Acf">Normalized autocorrelation function <c>rho(k)</c> from lag <c>0</c> up to the truncation lag.</param>
internal readonly record struct AutocorrelationResult(
    bool Converged,
    int TauInt,
    double RawTauInt,
    double[] Acf);

/// <summary>
/// Computes the integrated autocorrelation time (<c>tau_int</c>) of a stationary Markov chain trace
/// using the Madras-Sokal automatic self-consistent windowing algorithm (<c>M &gt;= 6 * tau_int</c>)
/// (Madras &amp; Sokal, J. Stat. Phys. 50, 109–186, 1988).
/// </summary>
internal static class MadrasSokalAutocorrelation
{
    /// <summary>
    /// Minimum trace length (<c>W = 24</c>) required for <c>maxLag = W / 4</c> to reach the
    /// Madras-Sokal self-consistent cutoff <c>k &gt;= 6 * tau_int</c> at <c>tau_int = 1</c>.
    /// </summary>
    public const int MinWindowSweeps = 24;

    /// <summary>
    /// Evaluates the normalized autocorrelation function and integrated autocorrelation time for <paramref name="series"/>.
    /// </summary>
    /// <param name="series">Contiguous trace of per-sweep observable measurements (e.g., total vertex count <c>N_0</c>).</param>
    /// <returns>An <see cref="AutocorrelationResult"/> containing <c>tau_int</c>, convergence status, and the ACF array.</returns>
    public static AutocorrelationResult Compute(ReadOnlySpan<int> series)
    {
        var n = series.Length;
        if (n < 4)
            return new AutocorrelationResult(false, 1, 0.5, [1.0]);

        (var mean, var stdDev) = SeriesStatistics.ComputePopulation(series);
        var c0 = stdDev * stdDev;

        if (c0 < 1e-12)
            return new AutocorrelationResult(false, 1, 0.5, [1.0]);

        var maxLag = Math.Min(n / 4, 1000);
        var acf = new double[maxLag + 1];
        acf[0] = 1.0;

        var tauInt = 0.5;

        for (var k = 1; k <= maxLag; k++)
        {
            var ck = 0.0;
            var pairs = n - k;
            for (var i = 0; i < pairs; i++)
            {
                ck += (series[i] - mean) * (series[i + k] - mean);
            }

            acf[k] = ck / (pairs * c0);
            tauInt += acf[k];

            if (k >= 6.0 * tauInt)
            {
                var roundedTau = Math.Max(1, (int)Math.Ceiling(tauInt));
                return new AutocorrelationResult(true, roundedTau, tauInt, acf[..(k + 1)]);
            }
        }

        var fallbackTau = Math.Max(1, (int)Math.Ceiling(tauInt));
        return new AutocorrelationResult(false, fallbackTau, tauInt, acf);
    }
}