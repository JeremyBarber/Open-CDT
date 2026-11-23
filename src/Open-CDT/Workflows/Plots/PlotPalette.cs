using ScottPlot;

namespace Open_CDT.Workflows.Plots;

/// <summary>
/// Centralized color palette shared across all diagnostic and publication plots.
/// </summary>
internal static class PlotPalette
{
    public static readonly Color Canvas = Color.FromHex("#ffffff");
    public static readonly Color GridMajor = Color.FromHex("#000000").WithAlpha(0.12f);
    public static readonly Color Ink = Color.FromHex("#111111");
    public static readonly Color Slate = Color.FromHex("#2c3e50");
    public static readonly Color NeutralGray = Color.FromHex("#7f8c8d");

    public static readonly Color Blue = Color.FromHex("#2980b9");
    public static readonly Color Green = Color.FromHex("#27ae60");
    public static readonly Color Red = Color.FromHex("#e74c3c");
    public static readonly Color Crimson = Color.FromHex("#c0392b");
    public static readonly Color Orange = Color.FromHex("#d35400");
    public static readonly Color Amber = Color.FromHex("#e67e22");
    public static readonly Color Purple = Color.FromHex("#8e44ad");
    public static readonly Color Teal = Color.FromHex("#16a085");
    public static readonly Color Gold = Color.FromHex("#f39c12");

    private static readonly Color[] SeriesCycle =
    [
        Green,
        Blue,
        Purple,
        Orange,
        Teal,
        Crimson,
        Slate,
        Gold
    ];

    /// <summary>
    /// Returns a deterministic color from the 8-color series cycle for slice or replica <paramref name="index"/>.
    /// </summary>
    public static Color Series(int index) =>
        SeriesCycle[((index % SeriesCycle.Length) + SeriesCycle.Length) % SeriesCycle.Length];
}