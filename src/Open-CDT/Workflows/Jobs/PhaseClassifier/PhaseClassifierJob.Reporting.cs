using Open_CDT.Config;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Data;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Helpers;
using Open_CDT.Workflows.Jobs.PhaseClassifier.Plots;
using Open_CDT.Workflows.Plots;

using System.Diagnostics.CodeAnalysis;

namespace Open_CDT.Workflows.Jobs.PhaseClassifier;

internal sealed partial class PhaseClassifierJob
{
    /// <inheritdoc/>
    protected override void WriteOutput(RunConfig config, PhaseSpaceResult result) =>
        Output.WriteToDisk(config, result);

    /// <inheritdoc/>
    protected override bool TryReadOutput(string directory, [NotNullWhen(true)] out PhaseSpaceResult? result) =>
        Output.TryReadFromDisk(directory, out result);

    /// <inheritdoc/>
    protected override PlotPlan BuildPlotPlan(PhaseSpaceResult result)
    {
        var uniqueK0s = result.Points.Select(p => p.K0).Distinct().Count();
        if (uniqueK0s < 2)
            return new PlotPlan("Plotting Phase Space Datasets", string.Empty, []);

        var uniqueDeltas = result.Points.Select(p => p.Delta).Distinct().Count();

        var plots = new List<PlotBase<PhaseSpaceResult>>(4);
        if (uniqueDeltas >= 2)
        {
            plots.Add(new PhaseDiagramPlot());
        }

        plots.Add(new PhaseVertexDensityPlot());
        plots.Add(new PhaseDropletProfilePlot());
        plots.Add(new PhaseSliceContinuityPlot());

        var source = $"phase_space.csv │ {result.Points.Count} classified coordinates";
        return new PlotPlan("Plotting Phase Space Datasets", source, plots);
    }

    /// <inheritdoc/>
    protected override CompletionSummary BuildCompletionSummary(
        RunConfig config,
        PhaseSpaceResult result,
        int replicaCount,
        double totalSeconds)
    {
        var pts = result.Points;
        var totalCoords = pts.Count;
        var sweepsPerPoint = config.Classification.SweepsPerCoordinate;
        var header = $"Classifying Phases │ {totalCoords} / {totalCoords} │ {sweepsPerPoint / 1000.0:F1}k sw/pt";

        var hasMultipleDeltas = pts.Select(p => p.Delta).Distinct().Count() > 1;
        var hasSingleK0 = pts.Select(p => p.K0).Distinct().Count() <= 1;

        var statuses = new string[replicaCount];
        for (var i = 0; i < replicaCount; i++)
        {
            var stripStart = (i * totalCoords) / replicaCount;
            var stripEnd = ((i + 1) * totalCoords) / replicaCount;
            statuses[i] = FormatStripStatus(pts, stripStart, stripEnd, hasMultipleDeltas, hasSingleK0);
        }

        return new CompletionSummary(
            HeaderTitle: header,
            HeaderProgress: 1.0,
            ReplicaStatuses: statuses,
            SummaryLines: BuildSummaryLines(config, pts, totalSeconds));
    }

    private static string FormatStripStatus(
        IReadOnlyList<PhasePointRecord> pts,
        int stripStart,
        int stripEnd,
        bool hasMultipleDeltas,
        bool hasSingleK0)
    {
        var count = stripEnd - stripStart;
        if (count <= 0)
            return "✓ Done";

        var strip = pts.Skip(stripStart).Take(count).ToArray();
        var badge = FormatStripPhaseBadge(strip);

        if (strip.Length == 1)
        {
            var pt = strip[0];
            var coord = hasMultipleDeltas
                ? $"(k0={pt.K0,5:F2}, Δ={pt.Delta:F2})"
                : $"k0={pt.K0,5:F2}";

            return $"✓ {coord}  kd={pt.KdCrit:F5}  <N>={pt.MeanVolume,6:N0}  <N0/N>={pt.VertexDensity:F3}  {badge}";
        }

        var axisSpan = hasSingleK0 && hasMultipleDeltas
            ? $"Δ: {strip.Min(p => p.Delta):F2}→{strip.Max(p => p.Delta):F2}"
            : $"k0: {strip.Min(p => p.K0):F2}→{strip.Max(p => p.K0):F2}";

        return $"✓ [{strip.Length,2} pts]  {axisSpan}  kd: {strip.Min(p => p.KdCrit):F4}→{strip.Max(p => p.KdCrit):F4}  <N0/N>={strip.Average(p => p.VertexDensity):F3}  {badge}";
    }

    private static string FormatStripPhaseBadge(PhasePointRecord[] strip)
    {
        var cCount = strip.Count(p => p.Phase == SpacetimePhase.PhaseC);
        var aCount = strip.Count(p => p.Phase == SpacetimePhase.PhaseA);
        var bCount = strip.Count(p => p.Phase == SpacetimePhase.PhaseB);

        if (cCount == strip.Length)
            return "★ Phase C";
        if (aCount == strip.Length)
            return "◆ Phase A";
        if (bCount == strip.Length)
            return "▼ Phase B";

        return $"◆ C:{cCount} A:{aCount} B:{bCount}";
    }

    private static string[] BuildSummaryLines(
        RunConfig config,
        IReadOnlyList<PhasePointRecord> pts,
        double totalSeconds)
    {
        var cCount = pts.Count(p => p.Phase == SpacetimePhase.PhaseC);
        var aCount = pts.Count(p => p.Phase == SpacetimePhase.PhaseA);
        var bCount = pts.Count(p => p.Phase == SpacetimePhase.PhaseB);

        var outPath = string.IsNullOrWhiteSpace(config.Output.Directory)
            ? "in-memory"
            : Path.Combine(config.Output.Directory, "phase_space.csv");

        var rec = PhaseCentroid.SelectTarget(pts);
        var recLine = rec is not null
            ? $"k0 = {rec.K0:F4}  │  Δ = {rec.Delta:F4}  │  kd = {rec.KdCrit:F6}"
            : "None detected in current window";

        return
        [
            $"Classified Grid   : {pts.Count} pts in {totalSeconds:F1}s  │  Phase C = {cCount}  │  Phase A = {aCount}  │  Phase B = {bCount}",
            $"Centroid Target   : {recLine}",
            $"Output            : {outPath}"
        ];
    }
}