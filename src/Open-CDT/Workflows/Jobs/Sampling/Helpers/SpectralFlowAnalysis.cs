using Open_CDT.Statistics;
using Open_CDT.Workflows.Jobs.Sampling.Data;

namespace Open_CDT.Workflows.Jobs.Sampling.Helpers;

/// <summary>
/// Evaluates running spectral dimension curves <c>D_S(σ)</c>, leave-one-replica-out jackknife
/// uncertainties, and Ambjørn-Jurkiewicz-Loll rational asymptote fits.
/// </summary>
internal static class SpectralFlowAnalysis
{
    /// <summary>
    /// Computes the ensemble running spectral dimension <c>D_S(σ)</c> via symmetric equal-parity
    /// central differences <c>(σ - 1, σ + 1)</c> and leave-one-replica-out jackknife standard errors.
    /// </summary>
    public static (double[] Sigmas, double[] Ds, double[] Sem, int ReplicaCount) ComputeRunningCurve(
        IReadOnlyList<ObservableSampleRecord> samples)
    {
        if (samples.Count == 0)
            return ([], [], [], 0);

        var nSigma = samples[0].Spectral.ReturnProbabilities.Length;
        var minSigma = ResolveMinEvaluationSigma(nSigma);
        if (minSigma >= nSigma - 1)
            return ([], [], [], 0);

        var replicaGroups = samples
            .GroupBy(s => s.ReplicaId)
            .OrderBy(g => g.Key)
            .ToArray();

        var nReplicas = replicaGroups.Length;
        var totalSamples = samples.Count;

        var totalSumP = new double[nSigma];
        var replicaSumP = new double[nReplicas][];
        var replicaCounts = new int[nReplicas];

        for (var r = 0; r < nReplicas; r++)
        {
            var sumP = new double[nSigma];
            var count = 0;

            foreach (var sample in replicaGroups[r])
            {
                var probs = sample.Spectral.ReturnProbabilities;
                var limit = Math.Min(nSigma, probs.Length);
                for (var s = 0; s < limit; s++)
                    sumP[s] += probs[s];
                count++;
            }

            replicaSumP[r] = sumP;
            replicaCounts[r] = count;
            for (var s = 0; s < nSigma; s++)
                totalSumP[s] += sumP[s];
        }

        var evalSigmas = new List<double>(nSigma - minSigma);
        var meanDs = new List<double>(nSigma - minSigma);
        var semDs = new List<double>(nSigma - minSigma);
        var jkEstimates = new double[nReplicas];

        for (var sigma = minSigma; sigma < nSigma - 1; sigma++)
        {
            var meanPrev = totalSumP[sigma - 1] / totalSamples;
            var meanNext = totalSumP[sigma + 1] / totalSamples;
            if (meanPrev <= 0.0 || meanNext <= 0.0)
                continue;

            var logStep = Math.Log(sigma + 1) - Math.Log(sigma - 1);
            var dsPoint = -2.0 * (Math.Log(meanNext) - Math.Log(meanPrev)) / logStep;

            var jkSem = 0.0;
            if (nReplicas > 1)
            {
                var validJk = true;
                for (var r = 0; r < nReplicas; r++)
                {
                    var remCount = totalSamples - replicaCounts[r];
                    if (remCount <= 0)
                    {
                        validJk = false;
                        break;
                    }

                    var remPrev = (totalSumP[sigma - 1] - replicaSumP[r][sigma - 1]) / remCount;
                    var remNext = (totalSumP[sigma + 1] - replicaSumP[r][sigma + 1]) / remCount;
                    if (remPrev <= 0.0 || remNext <= 0.0)
                    {
                        validJk = false;
                        break;
                    }

                    jkEstimates[r] = -2.0 * (Math.Log(remNext) - Math.Log(remPrev)) / logStep;
                }

                if (validJk)
                {
                    (_, var jkSd, _) = SeriesStatistics.ComputeSample<double>(jkEstimates);
                    jkSem = ((nReplicas - 1.0) / Math.Sqrt(nReplicas)) * jkSd;
                }
            }

            evalSigmas.Add(sigma);
            meanDs.Add(dsPoint);
            semDs.Add(jkSem);
        }

        return (evalSigmas.ToArray(), meanDs.ToArray(), semDs.ToArray(), nReplicas);
    }

    /// <summary>
    /// Fits <c>D_S(σ) = D_inf - b / (c + σ)</c> over the pre-turnover ascent at <c>0.1</c>-step
    /// resolution subject to <c>0 &lt; c &lt;= σ_peak</c>, <c>b &gt; 0</c>, and <c>D_S(0) &gt;= 0</c>.
    /// </summary>
    public static (double DInf, double B, double C) FitAjlCurve(
        ReadOnlySpan<double> sigmas,
        ReadOnlySpan<double> ds)
    {
        if (sigmas.IsEmpty || ds.IsEmpty)
            return (double.NaN, 0.0, 0.0);

        if (sigmas.Length < 3)
            return (ds[^1], 0.0, sigmas[^1]);

        var peakIdx = ds.Length - 1;
        var bestLocalMean = double.NegativeInfinity;
        for (var i = 0; i < ds.Length; i++)
        {
            var k0 = Math.Max(0, i - 2);
            var k1 = Math.Min(ds.Length - 1, i + 2);
            var sum = 0.0;
            for (var k = k0; k <= k1; k++)
                sum += ds[k];

            var localMean = sum / (k1 - k0 + 1);
            if (localMean > bestLocalMean)
            {
                bestLocalMean = localMean;
                peakIdx = i;
            }
        }

        var fitLen = Math.Max(3, peakIdx + 1);
        var fitSigmas = sigmas[..fitLen];
        var fitDs = ds[..fitLen];
        var invX = new double[fitLen];

        var maxTenths = Math.Max(1, (int)Math.Round(fitSigmas[^1] * 10.0));
        var bestSse = double.PositiveInfinity;
        var bestDInf = fitDs[^1];
        var bestB = 0.0;
        var bestC = fitSigmas[^1];

        for (var cTenths = 1; cTenths <= maxTenths; cTenths++)
        {
            var c = cTenths * 0.1;
            for (var i = 0; i < fitLen; i++)
                invX[i] = 1.0 / (c + fitSigmas[i]);

            (var slope, var intercept) = SeriesStatistics.FitLinear(invX, fitDs);
            var b = -slope;

            if (b <= 0.0 || intercept <= 0.0 || intercept - (b / c) < 0.0)
                continue;

            var sse = 0.0;
            for (var i = 0; i < fitLen; i++)
            {
                var diff = fitDs[i] - (intercept - (b * invX[i]));
                sse += diff * diff;
            }

            if (sse < bestSse)
            {
                bestSse = sse;
                bestDInf = intercept;
                bestB = b;
                bestC = c;
            }
        }

        return (bestDInf, bestB, bestC);
    }

    /// <summary>
    /// Summarizes the UV start, measured IR tail, and fitted AJL asymptote <c>D_inf</c> across <paramref name="samples"/>.
    /// </summary>
    public static (double UvDs, double IrDs, double DInf) SummarizeFlow(
        IReadOnlyList<ObservableSampleRecord> samples)
    {
        (var xs, var ys, _, _) = ComputeRunningCurve(samples);
        if (xs.Length == 0)
            return (double.NaN, double.NaN, double.NaN);

        (var dInf, _, _) = FitAjlCurve(xs, ys);
        return (ys[0], ys[^1], dInf);
    }

    /// <summary>
    /// Extracts the short-scale UV and asymptotic IR spectral dimensions from a single snapshot's return curve.
    /// </summary>
    public static (double UvDs, double IrDs) ComputeReturnCurveRange(
        ReadOnlySpan<double> probs,
        double fallbackIr)
    {
        var nSigma = probs.Length;
        var minSigma = ResolveMinEvaluationSigma(nSigma);
        if (minSigma >= nSigma - 1)
            return (fallbackIr, fallbackIr);

        var uv = probs[minSigma - 1] > 0.0 && probs[minSigma + 1] > 0.0
            ? -2.0 * (Math.Log(probs[minSigma + 1]) - Math.Log(probs[minSigma - 1]))
                / (Math.Log(minSigma + 1) - Math.Log(minSigma - 1))
            : fallbackIr;

        var ir = double.IsFinite(fallbackIr) && fallbackIr > 0.0
            ? fallbackIr
            : uv;

        return (uv, ir);
    }

    // Ensures σ - 1 >= 2 (skipping geometry-blind P(1) = 1 - χ) while leaving >= 3 points for short test curves.
    private static int ResolveMinEvaluationSigma(int nSigma) =>
        Math.Min(6, Math.Max(3, nSigma - 4));
}