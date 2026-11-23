namespace Open_CDT_CLI.ConsoleWriter.Components;

internal static class Eta
{
    internal static double ComputeSweepRate(int sweepsInStep, double stepElapsedSeconds) =>
        sweepsInStep > 0 && stepElapsedSeconds > 1e-6
            ? sweepsInStep / stepElapsedSeconds
            : 0.0;

    internal static string ShowSweepRate(int sweepsInStep, double stepElapsedSeconds)
    {
        var rate = ComputeSweepRate(sweepsInStep, stepElapsedSeconds);
        return rate > 0.0 ? $"{rate,6:N0} sw/s" : string.Empty;
    }

    internal static string FormatDuration(double totalSeconds)
    {
        if (!double.IsFinite(totalSeconds) || totalSeconds < 0.0)
            return "--:--";

        var totalSec = (int)Math.Round(totalSeconds);
        var minutes = totalSec / 60;
        var seconds = totalSec % 60;

        return minutes >= 60
            ? $"{minutes / 60}:{minutes % 60:D2}:{seconds:D2}"
            : $"{minutes:D2}:{seconds:D2}";
    }
}