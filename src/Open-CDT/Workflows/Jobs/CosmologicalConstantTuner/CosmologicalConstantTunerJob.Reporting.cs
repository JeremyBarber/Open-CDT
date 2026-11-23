using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;
using Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Plots;
using Open_CDT.Workflows.Plots;

using System.Diagnostics.CodeAnalysis;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner;

internal sealed partial class CosmologicalConstantTunerJob
{
    /// <inheritdoc/>
    protected override void WriteOutput(RunConfig config, TuningResult result) =>
        Output.WriteToDisk(config, result);

    /// <inheritdoc/>
    protected override bool TryReadOutput(string directory, [NotNullWhen(true)] out TuningResult? result) =>
        Output.TryReadFromDisk(directory, out result);

    /// <inheritdoc/>
    protected override PlotPlan BuildPlotPlan(TuningResult result)
    {
        var plots = new List<PlotBase<TuningResult>>(3);

        var uniqueK0Count = result.CriticalPoints.Select(c => c.K0).Distinct().Count();
        if (uniqueK0Count >= 2)
        {
            plots.Add(new CriticalSurfacePlot());
        }

        plots.Add(new CalibrationCurvePlot());
        plots.Add(new BracketZoomPlot());

        var coordCount = result.CriticalPoints.Count;
        var source = $"tuning.csv │ {result.History.Count} probes across {coordCount} coordinate{(coordCount == 1 ? "" : "s")}";

        return new PlotPlan("Plotting Tuning Datasets", source, plots);
    }

    /// <inheritdoc/>
    protected override CompletionSummary BuildCompletionSummary(
        RunConfig config,
        TuningResult result,
        int replicaCount,
        double totalSeconds)
    {
        var pts = result.CriticalPoints;
        var totalCoords = pts.Count;

        var header = totalCoords == 1
            ? $"Tuning Critical Coupling │ Locked kd = {result.TunedKd:F6} │ Target N={config.Physics.TargetVolume:N0}"
            : $"Tuning Critical Sheet │ {totalCoords} / {totalCoords} │ Target N={config.Physics.TargetVolume:N0}";

        var statuses = new string[replicaCount];
        for (var i = 0; i < replicaCount; i++)
        {
            var stripStart = (i * totalCoords) / replicaCount;
            var stripEnd = ((i + 1) * totalCoords) / replicaCount;
            statuses[i] = FormatStripStatus(pts, stripStart, stripEnd, result.ToleranceDeltaKd, config.Tuning);
        }

        return new CompletionSummary(
            HeaderTitle: header,
            HeaderProgress: totalCoords == 1 ? -1.0 : 1.0,
            ReplicaStatuses: statuses,
            SummaryLines: BuildSummaryLines(config, result, totalSeconds));
    }

    private static string FormatStripStatus(
        IReadOnlyList<CriticalPointRecord> pts,
        int stripStart,
        int stripEnd,
        double toleranceDeltaKd,
        TuningParameters tuning)
    {
        var count = stripEnd - stripStart;
        if (count <= 0)
            return "✓ Done";

        var strip = pts.Skip(stripStart).Take(count).ToArray();
        if (strip.Length == 1)
        {
            var pt = strip[0];
            var coord = tuning.DeltaSteps > 1
                ? $"(k0={pt.K0,5:F2}, Δ={pt.Delta:F2})"
                : $"k0={pt.K0,5:F2}";

            return $"✓ {coord}  ★ kd={pt.KdCrit:F5} ± {toleranceDeltaKd:F4}  <N>={pt.MeanVolume,6:N0}  <N0/N>={pt.VertexDensity:F3}";
        }

        var axisSpan = tuning.K0Steps == 1 && tuning.DeltaSteps > 1
            ? $"Δ: {strip.Min(c => c.Delta):F2}→{strip.Max(c => c.Delta):F2}"
            : $"k0: {strip.Min(c => c.K0):F2}→{strip.Max(c => c.K0):F2}";

        return $"✓ [{strip.Length,2} pts]  {axisSpan}  kd: {strip.Min(c => c.KdCrit):F4}→{strip.Max(c => c.KdCrit):F4}  <N0/N>={strip.Average(c => c.VertexDensity):F3}";
    }

    private static string[] BuildSummaryLines(RunConfig config, TuningResult result, double totalSeconds)
    {
        var outPath = string.IsNullOrWhiteSpace(config.Output.Directory)
            ? "in-memory"
            : Path.Combine(config.Output.Directory, "tuning.csv");

        var pts = result.CriticalPoints;
        if (pts.Count == 0)
        {
            return
            [
                $"Converged kd      : {result.TunedKd:F6}",
                $"Probes Evaluated  : {result.History.Count} in {totalSeconds:F1}s",
                $"Output            : {outPath}"
            ];
        }

        if (pts.Count == 1)
        {
            var cp = pts[0];
            return
            [
                $"Converged kd      : {result.TunedKd:F6}  │  <N> = {cp.MeanVolume:N0} ± {cp.StdDevVolume:F0}  │  <N0/N> = {cp.VertexDensity:F3}",
                $"Susceptibility    : d<N>/dkd = {result.SusceptibilitySlope:F1}/kd  │  {result.History.Count} probes in {totalSeconds:F1}s",
                $"Output            : {outPath}"
            ];
        }

        var minKdCrit = pts.Min(c => c.KdCrit);
        var maxKdCrit = pts.Max(c => c.KdCrit);
        var meanSigma = pts.Average(c => c.StdDevVolume);

        return
        [
            $"Critical Sheet    : {pts.Count} coordinates locked  │  kd = {minKdCrit:F5} → {maxKdCrit:F5}",
            $"Thermodynamics    : d<N>/dkd = {result.SusceptibilitySlope:F1}/kd  │  Band ±{result.ToleranceDeltaKd:F4}  │  <σ_N> = {meanSigma:F1} vs {result.TheoreticalStdDev:F1}",
            $"Output            : {outPath}"
        ];
    }
}