namespace Open_CDT_CLI.ConsoleWriter.Components;

internal static class Progress
{
    private const int DefaultBarWidth = 20;

    internal static string ShowBar(double completed, double total, int width = DefaultBarWidth)
    {
        var ratio = total > 0 ? Math.Clamp(completed / total, 0.0, 1.0) : 0.0;
        var filled = (int)Math.Round(ratio * width, MidpointRounding.AwayFromZero);
        var empty = width - filled;

        return $"[{new string('█', filled)}{new string('░', empty)}]";
    }

    internal static string ShowProgress(double completed, double total, int width = DefaultBarWidth)
    {
        var ratio = total > 0 ? Math.Clamp(completed / total, 0.0, 1.0) : 0.0;
        return $"{ShowBar(completed, total, width)} {ratio * 100,3:F0}%";
    }
}