using Open_CDT;
using Open_CDT_CLI.ConsoleWriter.Components;

using System.Diagnostics;

namespace Open_CDT_CLI.ConsoleWriter;

internal sealed class ConsoleRenderer : IDisposable
{
    internal const int LayoutWidth = 108;

    private static readonly TimeSpan RefreshRate = TimeSpan.FromMilliseconds(80);
    private static readonly string[] SpinnerFrames = ["⠋", "⠙", "⠹", "⠸", "⠼", "⠴", "⠦", "⠧", "⠇", "⠏"];

    private readonly Timer? _renderTimer;
    private readonly Lock _stateLock = new();
    private readonly Lock _consoleLock = new();

    private int _cursorTopAnchor = -1;
    private int _lastRenderedLineCount;
    private int _spinnerIndex;
    private long _jobStartTimestamp;

    private JobProgress[] _rows = [];
    private double[] _lastReplicaRates = [];
    private bool[] _active = [];
    private string _headerTitle = "Initialising";
    private double _headerProgress = -1.0;
    private bool _jobComplete;
    private string[]? _summaryLines;
    private volatile bool _disposed;

    public ConsoleRenderer()
    {
        _jobStartTimestamp = Stopwatch.GetTimestamp();

        if (!Console.IsOutputRedirected)
        {
            try
            {
                Console.Write("\x1b[?7l");
                Logo.Print();
                Console.CursorVisible = false;
                _cursorTopAnchor = Console.CursorTop;
                _renderTimer = new Timer(_ => RenderFrame(), null, RefreshRate, RefreshRate);
            }
            catch
            {
            }
        }
    }

    public void OnProgress(JobProgress p)
    {
        if (!p.IsJobComplete)
        {
            lock (_stateLock)
            {
                if (_disposed)
                    return;

                ApplyProgressCore(in p);
            }

            return;
        }

        if (Console.IsOutputRedirected)
        {
            string header;
            string[]? summary;

            lock (_stateLock)
            {
                if (_disposed)
                    return;

                ApplyProgressCore(in p);
                _jobComplete = true;
                _summaryLines = p.SummaryLines;
                header = BuildHeaderLineCore();
                summary = _summaryLines;
            }

            Console.WriteLine(header);
            if (summary is { Length: > 0 })
            {
                foreach (var summaryLine in summary)
                {
                    Console.WriteLine($"  {summaryLine}");
                }
            }

            return;
        }

        lock (_consoleLock)
        {
            List<string> finalLines;
            lock (_stateLock)
            {
                if (_disposed)
                    return;

                ApplyProgressCore(in p);
                _jobComplete = true;
                _summaryLines = p.SummaryLines;
                finalLines = BuildFrameLinesCore(_lastRenderedLineCount);
            }

            FlushLinesToConsole(finalLines);
            _cursorTopAnchor = Console.CursorTop;
            _lastRenderedLineCount = 0;
        }
    }

    private void ApplyProgressCore(in JobProgress p)
    {
        if (_jobComplete)
        {
            _jobComplete = false;
            _summaryLines = null;
            _rows = [];
            _lastReplicaRates = [];
            _active = [];
            _jobStartTimestamp = Stopwatch.GetTimestamp();
        }

        if (_rows.Length != p.TotalReplicas && p.TotalReplicas > 0)
        {
            _rows = new JobProgress[p.TotalReplicas];
            _lastReplicaRates = new double[p.TotalReplicas];
            _active = new bool[p.TotalReplicas];
        }

        _headerTitle = p.HeaderTitle;
        _headerProgress = p.HeaderProgress;

        if (p.ReplicaId < (uint)_rows.Length)
        {
            _rows[p.ReplicaId] = p;
            _active[p.ReplicaId] = true;

            var rate = Eta.ComputeSweepRate(p.SweepsInStep, p.StepElapsedSeconds);
            if (rate > 0.0)
            {
                _lastReplicaRates[p.ReplicaId] = rate;
            }
        }
    }

    private void RenderFrame()
    {
        if (Console.IsOutputRedirected || _disposed)
            return;

        if (!_consoleLock.TryEnter())
            return;

        try
        {
            List<string>? lines;
            lock (_stateLock)
            {
                lines = !_disposed && !_jobComplete && _rows.Length > 0
                    ? BuildFrameLinesCore(_lastRenderedLineCount)
                    : null;
            }

            if (lines is not null)
            {
                FlushLinesToConsole(lines);
            }
        }
        catch
        {
        }
        finally
        {
            _consoleLock.Exit();
        }
    }

    private List<string> BuildFrameLinesCore(int minLineCount)
    {
        var divider = new string('─', LayoutWidth);

        var lines = new List<string>(_rows.Length + 10)
        {
            string.Empty,
            BuildHeaderLineCore(),
            divider
        };

        for (var i = 0; i < _rows.Length; i++)
        {
            if (!_active[i])
            {
                lines.Add($"  #{i,-2}  Waiting...");
                continue;
            }

            lines.Add(BuildReplicaLine(i, in _rows[i]));
        }

        if (_jobComplete)
        {
            lines.Add(divider);

            if (_summaryLines is { Length: > 0 })
            {
                foreach (var summaryLine in _summaryLines)
                {
                    lines.Add($"  {summaryLine}");
                }

                lines.Add(divider);
            }
        }

        while (lines.Count < minLineCount)
        {
            lines.Add(string.Empty);
        }

        return lines;
    }

    private void FlushLinesToConsole(List<string> lines)
    {
        var maxCols = Logo.GetMaxColumns();

        if (_cursorTopAnchor >= 0)
        {
            Console.SetCursorPosition(0, _cursorTopAnchor);
        }

        foreach (var line in lines)
        {
            Display(line, maxCols);
        }

        _lastRenderedLineCount = lines.Count;

        var expectedTop = _cursorTopAnchor + lines.Count;
        if (_cursorTopAnchor >= 0 && Console.CursorTop < expectedTop)
        {
            _cursorTopAnchor = Math.Max(0, Console.CursorTop - lines.Count);
        }
    }

    internal string BuildHeaderLine()
    {
        lock (_stateLock)
        {
            return BuildHeaderLineCore();
        }
    }

    private string BuildHeaderLineCore()
    {
        var icon = _jobComplete
            ? "✓"
            : SpinnerFrames[_spinnerIndex++ % SpinnerFrames.Length];

        var elapsedSec = Stopwatch.GetElapsedTime(_jobStartTimestamp).TotalSeconds;
        var elapsedText = Eta.FormatDuration(elapsedSec);

        var totalSweepRate = 0.0;
        for (var i = 0; i < _lastReplicaRates.Length; i++)
        {
            totalSweepRate += _lastReplicaRates[i];
        }

        var parts = new List<string>(5)
        {
            $"{icon} {_headerTitle}"
        };

        if (_headerProgress >= 0.0)
        {
            parts.Add(Progress.ShowBar(_headerProgress, 1.0, width: 16));
        }

        if (totalSweepRate > 0.0)
        {
            parts.Add($"{totalSweepRate:N0} sw/s");
        }

        parts.Add(elapsedText);

        return string.Join(" │ ", parts);
    }

    internal static string BuildReplicaLine(int index, in JobProgress r)
    {
        var prefix = $"  #{index,-2}  ";
        var barPart = r.ShowProgressBar
            ? $"{Progress.ShowBar(r.CompletedSteps, r.TotalSteps, width: 18)}  "
            : string.Empty;

        var left = $"{prefix}{barPart}{r.Status}";
        var ratePart = Eta.ShowSweepRate(r.SweepsInStep, r.StepElapsedSeconds);
        if (string.IsNullOrEmpty(ratePart))
            return left;

        var gap = Math.Max(2, (LayoutWidth - 2) - left.Length - ratePart.Length);
        return $"{left}{new string(' ', gap)}{ratePart}";
    }

    private static void Display(string line, int maxCols)
    {
        var padded = line.Length > LayoutWidth
            ? line[..LayoutWidth]
            : line.PadRight(LayoutWidth);

        if (padded.Length > maxCols)
        {
            padded = padded[..maxCols];
        }

        Console.WriteLine(padded);
    }

    public void Dispose()
    {
        _disposed = true;
        _renderTimer?.Dispose();

        if (!Console.IsOutputRedirected)
        {
            lock (_consoleLock)
            {
                try
                {
                    List<string>? remainingLines = null;
                    lock (_stateLock)
                    {
                        if (!_jobComplete && _rows.Length > 0)
                        {
                            _jobComplete = true;
                            remainingLines = BuildFrameLinesCore(_lastRenderedLineCount);
                        }
                    }

                    if (remainingLines is not null)
                    {
                        FlushLinesToConsole(remainingLines);
                    }

                    Console.WriteLine();
                    Console.Write("\x1b[?7h");
                    Console.CursorVisible = true;
                }
                catch
                {
                }
            }
        }
    }
}