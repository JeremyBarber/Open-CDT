using CDT_Agnostic.Measurements.Observables;
using Open_CDT.Workflows.Jobs.Sampling.Data;
using Open_CDT.Workflows.Jobs.Sampling.Helpers;

using Xunit;

namespace Open_CDT_Tests.Workflows.Jobs.Sampling.Helpers;

public sealed class SpectralFlowAnalysisTests
{
    private static ObservableSampleRecord CreateSpectralSample(uint replicaId, double exponentDs, double parityRipple = 0.0)
    {
        const int maxSigma = 30;
        var probs = new double[maxSigma + 1];
        probs[0] = 1.0;

        for (var s = 1; s <= maxSigma; s++)
        {
            var parityFactor = s % 2 == 0 ? 1.0 + parityRipple : 1.0 - parityRipple;
            probs[s] = parityFactor * Math.Pow(s, -0.5 * exponentDs);
        }

        return new ObservableSampleRecord(
            ReplicaId: replicaId,
            SampleIndex: 1,
            CenteredVolumeProfile: [100, 100, 100, 100],
            Hausdorff: new HausdorffResult(3.0, [1.0, 4.0, 9.0, 16.0]),
            Spectral: new SpectralDimensionResult(exponentDs, probs));
    }

    [Fact]
    public void ComputeRunningCurve_CancelsEqualParityBipartiteRipplesAndComputesJackknifeSem()
    {
        // Pure power-law P(σ) = (1 ± 0.15) * σ^{-3/2}: equal-parity Δσ = 2 differences cancel (1 ± 0.15) identically.
        var rippled = CreateSpectralSample(replicaId: 0, exponentDs: 3.0, parityRipple: 0.15);
        (var sigmas, var ds, var semSingle, var repCountSingle) = SpectralFlowAnalysis.ComputeRunningCurve([rippled]);

        Assert.Equal(1, repCountSingle);
        Assert.NotEmpty(sigmas);
        Assert.All(ds, v => Assert.Equal(3.0, v, tolerance: 1e-12));
        Assert.All(semSingle, e => Assert.Equal(0.0, e, tolerance: 1e-12));

        // Two replicas with distinct scaling exponents produce positive leave-one-replica-out jackknife SEM.
        var rep0 = CreateSpectralSample(replicaId: 0, exponentDs: 2.8);
        var rep1 = CreateSpectralSample(replicaId: 1, exponentDs: 3.2);
        (_, _, var semTwo, var repCountTwo) = SpectralFlowAnalysis.ComputeRunningCurve([rep0, rep1]);

        Assert.Equal(2, repCountTwo);
        Assert.All(semTwo, e => Assert.True(e > 0.0));
    }

    [Fact]
    public void FitAjlCurve_RecoversExactRationalTrajectoryParameters()
    {
        var sigmas = new double[45];
        var ds = new double[45];

        for (var i = 0; i < sigmas.Length; i++)
        {
            var s = 6.0 + i;
            sigmas[i] = s;
            ds[i] = 3.0 - (9.0 / (3.5 + s));
        }

        (var dInf, var b, var c) = SpectralFlowAnalysis.FitAjlCurve(sigmas, ds);

        Assert.Equal(3.0, dInf, tolerance: 1e-9);
        Assert.Equal(9.0, b, tolerance: 1e-9);
        Assert.Equal(3.5, c, tolerance: 1e-9);
    }
}