using CDT_Agnostic;
using Open_CDT.Config;
using Open_CDT.Statistics;
using Open_CDT.Workflows.Jobs.Thermalizer.Data;
using Open_CDT.Workflows.Jobs.Thermalizer.Helpers;

using System.Diagnostics;

namespace Open_CDT.Workflows.Jobs.Thermalizer;

/// <summary>
/// Executes a deterministic burn-in across all live replicas, then evaluates
/// equilibrium means and Madras-Sokal integrated autocorrelation time (<c>tau_int</c>)
/// over the trailing equilibrium sweep window.
/// </summary>
internal sealed partial class ThermalizerJob : JobBase<ThermalizationResult>
{
    /// <inheritdoc/>
    public override IEnumerable<string> Validate(RunConfig config)
    {
        var t = config.Thermalization;

        if (t.WindowSweeps < MadrasSokalAutocorrelation.MinWindowSweeps)
            yield return $"Thermalization.WindowSweeps must be >= {MadrasSokalAutocorrelation.MinWindowSweeps}.";
        if (t.BurnInSweeps < t.WindowSweeps)
            yield return "Thermalization.BurnInSweeps must be >= Thermalization.WindowSweeps.";
    }

    /// <summary>
    /// Evolves all <paramref name="harnesses"/> in parallel through <see cref="ThermalizationParameters.BurnInSweeps"/>
    /// and measures per-replica <c>tau_int</c> over the trailing <see cref="ThermalizationParameters.WindowSweeps"/>.
    /// </summary>
    public ThermalizationResult Execute(
        ISimulationHarness[] harnesses,
        RunConfig config,
        Action<JobProgress>? onProgress = null)
    {
        ArgumentNullException.ThrowIfNull(harnesses);
        ArgumentOutOfRangeException.ThrowIfZero(harnesses.Length);

        var startTimestamp = Stopwatch.GetTimestamp();
        var thermal = config.Thermalization;
        var replicaCount = harnesses.Length;

        var totalEnsembleSweeps = Math.Max(1L, (long)replicaCount * thermal.BurnInSweeps);
        var completedGlobalSweeps = 0L;
        var headerTitle =
            $"Thermalising │ Target N={config.Physics.TargetVolume:N0} │ kd={config.Physics.Kd:F5}";

        var records = new ReplicaThermalizationRecord[replicaCount];

        Parallel.For(0, replicaCount, i =>
        {
            records[i] = ReplicaThermalizer.RunReplica(
                harnesses[i],
                (uint)i,
                replicaCount,
                thermal.BurnInSweeps,
                thermal.WindowSweeps,
                headerTitle,
                totalEnsembleSweeps,
                ref completedGlobalSweeps,
                onProgress);
        });

        var result = new ThermalizationResult(config.Physics.TargetVolume, records);
        return Complete(config, result, replicaCount, startTimestamp, onProgress);
    }
}