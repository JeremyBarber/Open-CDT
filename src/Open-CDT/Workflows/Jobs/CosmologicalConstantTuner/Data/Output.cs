using Open_CDT.Config;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;

namespace Open_CDT.Workflows.Jobs.CosmologicalConstantTuner.Data;

/// <summary>
/// Terminal classification outcome of a single <c>k_d</c> tuning probe.
/// </summary>
internal enum ProbeOutcome
{
    BelowTarget,
    AboveTarget,
    LockedOnTarget
}

/// <summary>
/// Telemetry record for a single evaluated cosmological constant probe.
/// </summary>
internal readonly record struct ProbeRecord(
    double K0,
    double Delta,
    double Kd,
    double MeanVolume,
    double StdDevVolume,
    double MeanVertices,
    ProbeOutcome Outcome)
{
    /// <summary>Gets the intensive vertex-to-simplex ratio <c>&lt;N_0&gt; / &lt;N_d&gt;</c>.</summary>
    public double VertexDensity => MeanVolume > 0.0 ? MeanVertices / MeanVolume : 0.0;
}

/// <summary>
/// Converged critical cosmological constant <c>k_d^c(k_0, Delta)</c> and equilibrium state
/// at a single bare coupling coordinate.
/// </summary>
internal readonly record struct CriticalPointRecord(
    double K0,
    double Delta,
    double KdCrit,
    double MeanVolume,
    double StdDevVolume,
    double MeanVertices)
{
    /// <summary>Gets the intensive vertex-to-simplex ratio <c>&lt;N_0&gt; / &lt;N_d&gt;</c>.</summary>
    public double VertexDensity => MeanVolume > 0.0 ? MeanVertices / MeanVolume : 0.0;
}

/// <summary>
/// Complete output dataset produced by <see cref="CosmologicalConstantTunerJob"/>.
/// </summary>
internal sealed record TuningResult(
    uint TargetVolume,
    double ToleranceRatio,
    double Epsilon,
    IReadOnlyList<CriticalPointRecord> CriticalPoints,
    IReadOnlyList<ProbeRecord> History)
{
    /// <summary>Gets the primary calibrated critical coupling (<c>CriticalPoints[0].KdCrit</c>).</summary>
    public double TunedKd => CriticalPoints.Count > 0 ? CriticalPoints[0].KdCrit : 0.0;

    /// <summary>Gets the analytical saddle-point volume susceptibility <c>d&lt;N_d&gt;/dk_d = -1 / (2ε)</c>.</summary>
    public double SusceptibilitySlope => -1.0 / (2.0 * Epsilon);

    /// <summary>Gets the critical coupling half-width <c>δk_d = 2ε · N_target · ToleranceRatio</c> corresponding to the target volume band.</summary>
    public double ToleranceDeltaKd => 2.0 * Epsilon * TargetVolume * ToleranceRatio;

    /// <summary>Gets the Gaussian saddle-point volume fluctuation width <c>σ_N = 1 / √(2ε)</c>.</summary>
    public double TheoreticalStdDev => 1.0 / Math.Sqrt(2.0 * Epsilon);
}

/// <summary>
/// Persists and reloads <c>tuning.csv</c> critical-coupling calibration datasets.
/// </summary>
internal static class Output
{
    private const string FileName = "tuning.csv";
    private const string TargetHeaderPrefix = "# TargetVolume=";
    private const string ToleranceHeaderPrefix = "# ToleranceRatio=";
    private const string EpsilonHeaderPrefix = "# Epsilon=";

    private const string ColK0 = "K0";
    private const string ColDelta = "Delta";
    private const string ColKd = "Kd";
    private const string ColMeanVolume = "MeanVolume";
    private const string ColStdDevVolume = "StdDevVolume";
    private const string ColMeanVertices = "MeanVertices";
    private const string ColOutcome = "Outcome";

    public static void WriteToDisk(RunConfig config, TuningResult result)
    {
        Directory.CreateDirectory(config.Output.Directory);
        using var writer = new StreamWriter(Path.Combine(config.Output.Directory, FileName));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{TargetHeaderPrefix}{result.TargetVolume}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{ToleranceHeaderPrefix}{result.ToleranceRatio:G17}"));
        writer.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{EpsilonHeaderPrefix}{result.Epsilon:G17}"));

        csv.WriteField(ColK0);
        csv.WriteField(ColDelta);
        csv.WriteField(ColKd);
        csv.WriteField(ColMeanVolume);
        csv.WriteField(ColStdDevVolume);
        csv.WriteField(ColMeanVertices);
        csv.WriteField(ColOutcome);
        csv.NextRecord();

        foreach (var p in result.History)
        {
            csv.WriteField(p.K0);
            csv.WriteField(p.Delta);
            csv.WriteField(p.Kd);
            csv.WriteField(p.MeanVolume);
            csv.WriteField(p.StdDevVolume);
            csv.WriteField(p.MeanVertices);
            csv.WriteField(p.Outcome.ToString());
            csv.NextRecord();
        }
    }

    public static bool TryReadFromDisk(
        string directory,
        [NotNullWhen(true)] out TuningResult? result)
    {
        result = null;
        var filePath = Path.Combine(directory, FileName);
        if (!File.Exists(filePath))
            return false;

        uint targetVolume = 0;
        var toleranceRatio = 0.0;
        var epsilon = 0.0;

        foreach (var line in File.ReadLines(filePath))
        {
            if (!line.StartsWith('#'))
                break;

            if (line.StartsWith(TargetHeaderPrefix, StringComparison.Ordinal) &&
                uint.TryParse(line.AsSpan(TargetHeaderPrefix.Length), CultureInfo.InvariantCulture, out var parsedTarget))
            {
                targetVolume = parsedTarget;
            }
            else if (line.StartsWith(ToleranceHeaderPrefix, StringComparison.Ordinal) &&
                double.TryParse(line.AsSpan(ToleranceHeaderPrefix.Length), CultureInfo.InvariantCulture, out var parsedTol))
            {
                toleranceRatio = parsedTol;
            }
            else if (line.StartsWith(EpsilonHeaderPrefix, StringComparison.Ordinal) &&
                double.TryParse(line.AsSpan(EpsilonHeaderPrefix.Length), CultureInfo.InvariantCulture, out var parsedEps))
            {
                epsilon = parsedEps;
            }
        }

        if (targetVolume == 0 ||
            !double.IsFinite(toleranceRatio) || toleranceRatio <= 0.0 || toleranceRatio >= 1.0 ||
            !double.IsFinite(epsilon) || epsilon <= 0.0)
        {
            return false;
        }

        var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            AllowComments = true,
            Comment = '#'
        };

        using var reader = new StreamReader(filePath);
        using var csv = new CsvReader(reader, csvConfig);

        if (!csv.Read() || !csv.ReadHeader())
            return false;

        var history = new List<ProbeRecord>();
        while (csv.Read())
        {
            if (!Enum.TryParse<ProbeOutcome>(csv.GetField(ColOutcome), out var outcome))
                return false;

            history.Add(new ProbeRecord(
                K0: csv.GetField<double>(ColK0),
                Delta: csv.GetField<double>(ColDelta),
                Kd: csv.GetField<double>(ColKd),
                MeanVolume: csv.GetField<double>(ColMeanVolume),
                StdDevVolume: csv.GetField<double>(ColStdDevVolume),
                MeanVertices: csv.GetField<double>(ColMeanVertices),
                Outcome: outcome));
        }

        if (history.Count == 0)
            return false;

        var criticalPoints = BuildCriticalPoints(history, targetVolume, epsilon);
        result = new TuningResult(targetVolume, toleranceRatio, epsilon, criticalPoints, history);
        return true;
    }

    public static List<CriticalPointRecord> BuildCriticalPoints(
            IReadOnlyList<ProbeRecord> history,
            uint targetVolume,
            double epsilon)
    {
        var criticalPoints = new List<CriticalPointRecord>();

        var groups = history
            .GroupBy(p => (p.K0, p.Delta))
            .OrderBy(g => g.Key.Delta)
            .ThenBy(g => g.Key.K0);

        foreach (var group in groups)
        {
            var lowBound = double.NegativeInfinity;
            var highBound = double.PositiveInfinity;

            foreach (var p in group)
            {
                if (p.Outcome == ProbeOutcome.AboveTarget && p.Kd > lowBound)
                    lowBound = p.Kd;
                else if (p.Outcome == ProbeOutcome.BelowTarget && p.Kd < highBound)
                    highBound = p.Kd;
            }

            var best = group
                .OrderBy(p => p.Outcome == ProbeOutcome.LockedOnTarget ? 0 : 1)
                .ThenBy(p => Math.Abs(p.MeanVolume - targetVolume))
                .First();

            // Saddle-point projection around N_target, safeguarded by observed [lowBound, highBound] bounds.
            var kdCrit = best.Kd + (2.0 * epsilon * (best.MeanVolume - targetVolume));
            if (double.IsFinite(lowBound) && double.IsFinite(highBound) && (kdCrit <= lowBound || kdCrit >= highBound))
                kdCrit = 0.5 * (lowBound + highBound);

            criticalPoints.Add(new CriticalPointRecord(
                K0: best.K0,
                Delta: best.Delta,
                KdCrit: kdCrit,
                MeanVolume: best.MeanVolume,
                StdDevVolume: best.StdDevVolume,
                MeanVertices: best.MeanVertices));
        }

        return criticalPoints;
    }
}