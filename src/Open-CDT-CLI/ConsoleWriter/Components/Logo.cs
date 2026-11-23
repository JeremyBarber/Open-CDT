using System.Text;

namespace Open_CDT_CLI.ConsoleWriter.Components;

internal static class Logo
{
    private static readonly string[] GlyphLines =
    [
        "      ::::::::  :::::::::  :::::::::: ::::    :::          ::::::::  ::::::::: :::::::::::",
        "    :+:    :+: :+:    :+: :+:        :+:+:   :+:         :+:    :+: :+:    :+:    :+:     ",
        "   +:+    +:+ +:+    +:+ +:+        :+:+:+  +:+         +:+        +:+    +:+    +:+      ",
        "  +#+    +:+ +#++:++#+  +#++:++#   +#+ +:+ +#+         +#+        +#+    +:+    +#+       ",
        " +#+    +#+ +#+        +#+        +#+  +#+#+#         +#+        +#+    +#+    +#+        ",
        "#+#    #+# #+#        #+#        #+#   #+#+#         #+#    #+# #+#    #+#    #+#         ",
        "########  ###        ########## ###    ####          ########  #########     ###          "
    ];

    private static readonly string[] BannerLines = BuildBannerLines();

    private static string[] BuildBannerLines()
    {
        var pad = new string(' ', Math.Max(0, (ConsoleRenderer.LayoutWidth - GlyphLines[0].Length) / 2));
        var lines = new string[GlyphLines.Length + 2];

        for (var i = 0; i < GlyphLines.Length; i++)
        {
            lines[i] = pad + GlyphLines[i];
        }

        lines[^2] = string.Empty;
        lines[^1] = new string('═', ConsoleRenderer.LayoutWidth);
        return lines;
    }

    internal static void Print()
    {
        var maxCols = GetMaxColumns();

        Console.WriteLine();
        var logoTop = Console.CursorTop;

        const int totalFrames = 48;
        var startWave = -10.0;
        var endWave = ConsoleRenderer.LayoutWidth + 40.0;

        for (var frame = 0; frame <= totalFrames; frame++)
        {
            Console.SetCursorPosition(0, logoTop);
            var waveCenter = startWave + (frame * ((endWave - startWave) / totalFrames));
            var isFinalFrame = frame == totalFrames;

            for (var r = 0; r < BannerLines.Length; r++)
            {
                Console.WriteLine(RenderRow(BannerLines[r], r, maxCols, waveCenter, isFinalFrame));
            }

            var expectedTop = logoTop + BannerLines.Length;
            if (Console.CursorTop < expectedTop)
            {
                logoTop = Math.Max(0, Console.CursorTop - BannerLines.Length);
            }

            if (!isFinalFrame)
            {
                Thread.Sleep(25);
            }
        }
    }

    internal static string RenderRow(
        string row,
        int rowIndex,
        int maxCols,
        double waveCenter,
        bool isFinalFrame)
    {
        if (row.Length == 0)
            return string.Empty;

        var sb = new StringBuilder(512);
        var visibleLimit = Math.Min(row.Length, maxCols);

        var tRow = (double)rowIndex / (BannerLines.Length - 1);
        var restR = (int)(90 - (55 * tRow));
        var restG = (int)(210 - (105 * tRow));
        var restB = (int)(255 - (50 * tRow));

        const int dimR = 30, dimG = 45, dimB = 65;

        for (var c = 0; c < visibleLimit; c++)
        {
            var ch = row[c];
            if (ch == ' ')
            {
                sb.Append(' ');
                continue;
            }

            int r = restR, g = restG, b = restB;

            if (!isFinalFrame)
            {
                var pos = c + (rowIndex * 2.5);
                var delta = waveCenter - pos;

                if (delta < -6.0)
                {
                    r = dimR;
                    g = dimG;
                    b = dimB;
                }
                else if (delta < 0.0)
                {
                    var t = (delta + 6.0) / 6.0;
                    r = (int)(dimR + ((255 - dimR) * t));
                    g = (int)(dimG + ((255 - dimG) * t));
                    b = (int)(dimB + ((255 - dimB) * t));
                }
                else if (delta < 18.0)
                {
                    var t = delta / 18.0;
                    r = (int)(255 + ((restR - 255) * t));
                    g = (int)(255 + ((restG - 255) * t));
                    b = (int)(255 + ((restB - 255) * t));
                }
            }

            sb.Append($"\x1b[38;2;{r};{g};{b}m{ch}");
        }

        sb.Append("\x1b[0m");
        return sb.ToString();
    }

    internal static int GetMaxColumns()
    {
        try
        {
            return Console.WindowWidth > 1 ? Console.WindowWidth - 1 : ConsoleRenderer.LayoutWidth;
        }
        catch
        {
            return ConsoleRenderer.LayoutWidth;
        }
    }
}