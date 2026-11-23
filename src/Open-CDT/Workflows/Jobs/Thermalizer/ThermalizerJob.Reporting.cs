using Open_CDT.Config;
using Open_CDT.Statistics;
using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Thermalizer.Data;
using Open_CDT.Workflows.Jobs.Thermalizer.Plots;

using System.Diagnostics.CodeAnalysis;

namespace Open_CDT.Workflows.Jobs.Thermalizer;

internal sealed partial class ThermalizerJob
{
    /// <inheritdoc/>
    protected override void WriteOutput(RunConfig config, ThermalizationResult result) =>
        Output.WriteToDisk(config, result);

    /// <inheritdoc/>
    protected override bool TryReadOutput(string directory, [NotNullWhen(true)] out ThermalizationResult? result) =>
        Output.TryReadFromDisk(directory, out result);

    /// <inheritdoc/>
    protected override PlotPlan BuildPlotPlan(ThermalizationResult result)
    {
        PlotBase<ThermalizationResult>[] plots =
        [
            new AutocorrelationPlot(),
            new EquilibriumReplicasPlot(),
            new CurvatureRatioPlot()
        ];

        var source = $"thermalization.csv │ {result.Replicas.Count} replica{(result.Replicas.Count == 1 ? "" : "s")} │ ceiling τ_int = {result.EnsembleTauInt} sw";
        return new PlotPlan("Plotting Thermalization Datasets", source, plots);
    }

    /// <inheritdoc/>
    protected override CompletionSummary BuildCompletionSummary(
        RunConfig config,
        ThermalizationResult result,
        int replicaCount,
        double totalSeconds)
    {
        var headerTitle = $"Thermalising │ Target N={config.Physics.TargetVolume:N0} │ kd={config.Physics.Kd:F5}";
        var burnInK = config.Thermalization.BurnInSweeps / 1000.0;
        var replicas = result.Replicas;

        var statuses = new string[replicas.Count];
        for (var i = 0; i < replicas.Count; i++)
        {
            var r = replicas[i];
            var tauFlag = r.TauConverged ? "" : " *";
            statuses[i] = $"✓ {burnInK:F1}k sw   <N> = {r.MeanSimplices,6:N0}   <N0> = {r.MeanVertices,5:N0}   τ_int = {r.TauInt,3} sw{tauFlag}";
        }

        return new CompletionSummary(
            HeaderTitle: headerTitle,
            HeaderProgress: 1.0,
            ReplicaStatuses: statuses,
            SummaryLines: BuildSummaryLines(config, result, totalSeconds));
    }

    private static string[] BuildSummaryLines(
        RunConfig config,
        ThermalizationResult result,
        double totalSeconds)
    {
        var reps = result.Replicas;
        var simplices = new double[reps.Count];
        var vertices = new double[reps.Count];

        for (var i = 0; i < reps.Count; i++)
        {
            simplices[i] = reps[i].MeanSimplices;
            vertices[i] = reps[i].MeanVertices;
        }

        (var meanN, var sdN, _) = SeriesStatistics.ComputeSample<double>(simplices);
        (var meanN0, var sdN0, _) = SeriesStatistics.ComputeSample<double>(vertices);

        var meanTau = reps.Average(r => r.TauInt);
        var outPath = string.IsNullOrWhiteSpace(config.Output.Directory)
            ? "in-memory"
            : Path.Combine(config.Output.Directory, "thermalization.csv");

        return
        [
            $"Equilibrium       : <N> = {meanN:N0} ± {sdN:F0}  │  <N0> = {meanN0:N0} ± {sdN0:F0}  │  {totalSeconds:F1}s",
            $"Autocorrelation   : Mean τ_int = {meanTau:F1} sw  │  Ceiling = {result.EnsembleTauInt} sw  │  2τ_int = {2 * result.EnsembleTauInt} sw",
            $"Output            : {outPath}"
        ];
    }
}