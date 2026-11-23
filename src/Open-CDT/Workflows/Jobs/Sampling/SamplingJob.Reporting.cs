using Open_CDT.Config;
using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;
using Open_CDT.Workflows.Jobs.Sampling.Helpers;
using Open_CDT.Workflows.Jobs.Sampling.Plots;

using System.Diagnostics.CodeAnalysis;

namespace Open_CDT.Workflows.Jobs.Sampling;

internal sealed partial class SamplingJob
{
    /// <inheritdoc/>
    protected override void WriteOutput(RunConfig config, SamplingResult result) =>
        Output.WriteToDisk(config, result);

    /// <inheritdoc/>
    protected override bool TryReadOutput(string directory, [NotNullWhen(true)] out SamplingResult? result) =>
        Output.TryReadFromDisk(directory, out result);

    /// <inheritdoc/>
    protected override PlotPlan BuildPlotPlan(SamplingResult result)
    {
        PlotBase<SamplingResult>[] plots =
        [
            new VolumeProfileEnsemblePlot(),
            new VolumeProfileReplicasPlot(),
            new VolumeProfileHarmonicsPlot(),
            new HausdorffScalingPlot(),
            new HausdorffReplicasPlot(),
            new SpectralDimensionRunningPlot(),
            new SpectralReturnProbabilityPlot(),
            new SpectralReplicasPlot()
        ];

        var sampleCount = result.Samples.Count;
        var source = $"volume_profile.csv, hausdorff.csv, spectral.csv │ {sampleCount} sample{(sampleCount == 1 ? "" : "s")}";
        return new PlotPlan("Plotting Observable Sampling Datasets", source, plots);
    }

    /// <inheritdoc/>
    protected override CompletionSummary BuildCompletionSummary(
        RunConfig config,
        SamplingResult result,
        int replicaCount,
        double totalSeconds)
    {
        var totalSamples = result.Samples.Count;
        var headerTitle = $"Sampling │ {totalSamples} Samples │ {result.SweepsBetweenSamples} sw/sample";

        var samplesByReplica = result.Samples
            .GroupBy(s => s.ReplicaId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var rowCount = Math.Max(1, replicaCount);
        var statuses = new string[rowCount];
        var repDhMeans = new List<double>(rowCount);

        for (var i = 0; i < rowCount; i++)
        {
            if (!samplesByReplica.TryGetValue((uint)i, out var repSamples) || repSamples.Length == 0)
            {
                statuses[i] = "✓ Done";
                continue;
            }

            var n = repSamples.Length;
            var meanVol = repSamples.Average(s => s.CenteredVolumeProfile.Sum());
            var finiteDh = repSamples
                .Select(s => s.Hausdorff.Dimension)
                .Where(double.IsFinite)
                .ToArray();

            var meanDh = double.NaN;
            var semDh = 0.0;
            if (finiteDh.Length > 0)
            {
                (meanDh, _, semDh) = SeriesStatistics.ComputeSample<double>(finiteDh);
                repDhMeans.Add(meanDh);
            }

            (var repUv, var repIr, _) = SpectralFlowAnalysis.SummarizeFlow(repSamples);

            statuses[i] =
                $"✓ {n,2}/{n,-2} samples   <N> = {meanVol,6:N0}   <D_H> = {meanDh:F3} ± {semDh:F3}   D_S = {repUv:F2}→{repIr:F2}";
        }

        return new CompletionSummary(
            HeaderTitle: headerTitle,
            HeaderProgress: 1.0,
            ReplicaStatuses: statuses,
            SummaryLines: BuildSummaryLines(config, result, repDhMeans.ToArray(), totalSeconds));
    }

    private static string[] BuildSummaryLines(
        RunConfig config,
        SamplingResult result,
        ReadOnlySpan<double> repDhMeans,
        double totalSeconds)
    {
        var grandDh = double.NaN;
        var sdDh = 0.0;
        if (!repDhMeans.IsEmpty)
        {
            (grandDh, sdDh, _) = SeriesStatistics.ComputeSample(repDhMeans);
        }

        var rSquared = 0.0;
        var dropletWidth = 0.0;
        if (result.Samples.Count > 0)
        {
            var timeslices = result.Samples[0].CenteredVolumeProfile.Length;
            var meanProfile = new double[timeslices];
            for (var t = 0; t < timeslices; t++)
            {
                var sum = 0.0;
                for (var i = 0; i < result.Samples.Count; i++)
                    sum += result.Samples[i].CenteredVolumeProfile[t];
                meanProfile[t] = sum / result.Samples.Count;
            }

            (_, dropletWidth, _, rSquared) = DropletProfileFit.Fit(
                meanProfile,
                result.SpacelikeDimensions);
        }

        (var uvDs, var irDs, var dInf) = SpectralFlowAnalysis.SummarizeFlow(result.Samples);

        var outDir = string.IsNullOrWhiteSpace(config.Output.Directory)
            ? "in-memory"
            : config.Output.Directory;

        return
        [
            $"Geometry          : Profile R² = {rSquared:F4}  │  Width W = {dropletWidth:F1}  │  D_H = {grandDh:F3} ± {sdDh:F3}",
            $"Spectral Flow     : D_S = {uvDs:F2} → {irDs:F2}  │  D_inf = {dInf:F2}  │  {totalSeconds:F1}s",
            $"Output            : {outDir}"
        ];
    }
}