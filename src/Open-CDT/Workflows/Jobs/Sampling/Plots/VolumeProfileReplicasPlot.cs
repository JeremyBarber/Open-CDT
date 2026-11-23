using Open_CDT.Workflows.Plots;
using Open_CDT.Workflows.Jobs.Sampling.Data;

using ScottPlot;

namespace Open_CDT.Workflows.Jobs.Sampling.Plots;

/// <summary>
/// Renders <c>volume_profile_replicas.png</c> comparing individual replica centered volume profiles
/// against the ensemble grand mean <c>&lt;N(t)&gt;</c>.
/// </summary>
internal sealed class VolumeProfileReplicasPlot : PlotBase<SamplingResult>
{
    private const int MaxLegendReplicas = 10;

    /// <inheritdoc/>
    public override string FileName => "volume_profile_replicas.png";

    /// <inheritdoc/>
    public override string Description => "Per-Replica Volume Profiles";

    /// <inheritdoc/>
    protected override bool TryBuild(Plot plt, SamplingResult result)
    {
        var samples = result.Samples;
        if (samples.Count == 0)
            return false;

        var timeslices = samples[0].CenteredVolumeProfile.Length;
        if (timeslices == 0)
            return false;

        var ts = new double[timeslices];
        for (var t = 0; t < timeslices; t++)
            ts[t] = t;

        var replicaGroups = samples
            .GroupBy(s => s.ReplicaId)
            .OrderBy(g => g.Key)
            .ToArray();

        var maxPeak = 1.0;

        for (var r = 0; r < replicaGroups.Length; r++)
        {
            var group = replicaGroups[r];
            var repSamples = group.ToArray();
            var repMean = new double[timeslices];

            for (var t = 0; t < timeslices; t++)
            {
                repMean[t] = repSamples.Average(s => s.CenteredVolumeProfile[t]);
                if (repMean[t] > maxPeak)
                    maxPeak = repMean[t];
            }

            var line = plt.Add.Scatter(ts, repMean);
            line.Color = PlotPalette.Series(r).WithAlpha(0.65f);
            line.LineWidth = 1.8f;
            line.MarkerSize = 4;

            if (replicaGroups.Length <= MaxLegendReplicas)
                line.LegendText = $"Rep {group.Key} (peak={repMean.Max():F0})";
            else if (r == 0)
                line.LegendText = $"Replica Mean Profiles (n={replicaGroups.Length})";
        }

        var grandMean = new double[timeslices];
        for (var t = 0; t < timeslices; t++)
            grandMean[t] = samples.Average(s => s.CenteredVolumeProfile[t]);

        var grandLine = plt.Add.Scatter(ts, grandMean);
        grandLine.Color = PlotPalette.Ink;
        grandLine.LineWidth = 3.2f;
        grandLine.MarkerSize = 0;
        grandLine.LegendText = "Ensemble Grand Mean <N(t)>";

        plt.Axes.SetLimits(-0.5, timeslices - 0.5, 0.0, maxPeak * 1.18);

        plt.Title("Per-Replica Centered Volume Profiles vs. Ensemble Grand Mean");
        plt.XLabel("Centered Proper Time Slice (t)");
        plt.YLabel("Mean Simplex Volume <N(t)>");
        plt.ShowLegend(Alignment.UpperRight);

        return true;
    }
}