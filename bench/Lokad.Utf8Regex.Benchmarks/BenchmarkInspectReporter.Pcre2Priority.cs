using System.Globalization;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const int Pcre2DiscoveryPriorityLimit = 20;

    public static int RunEmitPcre2PriorityReport(string? modeText)
    {
        if (!TryParseMode(modeText, out var mode))
        {
            Console.Error.WriteLine("PCRE2 priority mode must be 'relative', 'absolute', or 'all'.");
            return 1;
        }

        var snapshot = LoadPcre2BenchmarkSnapshot();
        var rows = snapshot.Sections
            .SelectMany(static section => section.Value.Cases.Select(
                row => new Pcre2NativePriorityRow(section.Key, row.Key, row.Value)))
            .ToArray();
        var statusCounts = rows
            .GroupBy(static row => row.Measurement.PcreNetNativeStatus)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var nativeRows = rows.Where(static row => row.Measurement.PcreNetNative is > 0).ToArray();
        var excludedRows = rows
            .Where(static row => row.Measurement.PcreNetNativeStatus == Pcre2NativeComparisonStatus.Excluded)
            .ToArray();

        Console.WriteLine("# PCRE.NET / PCRE2 NFA Priority Report");
        Console.WriteLine();
        Console.WriteLine($"Queue mode: `{FormatMode(mode)}`");
        Console.WriteLine();
        Console.WriteLine("## Disposition");
        Console.WriteLine();
        Console.WriteLine("| Status | Rows |");
        Console.WriteLine("|---|---:|");
        foreach (var status in Enum.GetValues<Pcre2NativeComparisonStatus>())
        {
            Console.WriteLine($"| {FormatStatus(status)} | {statusCounts.GetValueOrDefault(status)} |");
        }

        Console.WriteLine($"| **Total** | **{rows.Length}** |");
        Console.WriteLine();
        Console.WriteLine($"Numeric comparator rows: {nativeRows.Length}; explicit exclusions: {excludedRows.Length}.");
        Console.WriteLine();

        WriteExclusionReasons(excludedRows);

        if (mode is Pcre2PriorityMode.Relative or Pcre2PriorityMode.All)
        {
            WriteRelativeQueues(nativeRows);
        }

        if (mode is Pcre2PriorityMode.Absolute or Pcre2PriorityMode.All)
        {
            WriteAbsoluteQueues(nativeRows);
        }

        return 0;
    }

    private static void WriteRelativeQueues(Pcre2NativePriorityRow[] rows)
    {
        WritePriorityQueue(
            "Qualified native wins — relative",
            rows.Where(static row => row.Measurement.PcreNetNativeStatus == Pcre2NativeComparisonStatus.NativeFaster)
                .OrderByDescending(static row => row.Ratio)
                .ToArray(),
            discovery: false);
        WritePriorityQueue(
            "Inconclusive native leads — relative",
            rows.Where(static row => row.Measurement.PcreNetNativeStatus == Pcre2NativeComparisonStatus.Inconclusive && row.Ratio > 1)
                .OrderByDescending(static row => row.Ratio)
                .ToArray(),
            discovery: false);
        var discovery = rows
            .Where(static row => row.Measurement.PcreNetNativeStatus == Pcre2NativeComparisonStatus.Unqualified && row.Ratio > 1.02)
            .OrderByDescending(static row => row.Ratio)
            .ToArray();
        WritePriorityQueue(
            $"Unqualified discovery leads — relative (top {Math.Min(Pcre2DiscoveryPriorityLimit, discovery.Length)} of {discovery.Length})",
            discovery.Take(Pcre2DiscoveryPriorityLimit).ToArray(),
            discovery: true);
    }

    private static void WriteAbsoluteQueues(Pcre2NativePriorityRow[] rows)
    {
        WritePriorityQueue(
            "Qualified native wins — absolute",
            rows.Where(static row => row.Measurement.PcreNetNativeStatus == Pcre2NativeComparisonStatus.NativeFaster)
                .OrderByDescending(static row => row.ExcessMicroseconds)
                .ToArray(),
            discovery: false);
        WritePriorityQueue(
            "Inconclusive native leads — absolute",
            rows.Where(static row => row.Measurement.PcreNetNativeStatus == Pcre2NativeComparisonStatus.Inconclusive && row.ExcessMicroseconds > 0)
                .OrderByDescending(static row => row.ExcessMicroseconds)
                .ToArray(),
            discovery: false);
        var discovery = rows
            .Where(static row => row.Measurement.PcreNetNativeStatus == Pcre2NativeComparisonStatus.Unqualified && row.ExcessMicroseconds > 0)
            .OrderByDescending(static row => row.ExcessMicroseconds)
            .ToArray();
        WritePriorityQueue(
            $"Unqualified discovery leads — absolute (top {Math.Min(Pcre2DiscoveryPriorityLimit, discovery.Length)} of {discovery.Length})",
            discovery.Take(Pcre2DiscoveryPriorityLimit).ToArray(),
            discovery: true);
    }

    private static void WritePriorityQueue(
        string title,
        IReadOnlyList<Pcre2NativePriorityRow> rows,
        bool discovery)
    {
        Console.WriteLine($"## {title}");
        Console.WriteLine();
        if (discovery)
        {
            Console.WriteLine("These independently measured rows select future qualification work; they do not determine Status.");
            Console.WriteLine();
        }

        if (rows.Count == 0)
        {
            Console.WriteLine("No rows.");
            Console.WriteLine();
            return;
        }

        Console.WriteLine("| Status | Section | Case | Managed CPU | Comparator CPU | R | E | Paired samples | Managed route |");
        Console.WriteLine("|---|---|---|---:|---:|---:|---:|---:|---|");
        foreach (var row in rows)
        {
            var pair = row.Measurement.PcreNetNativePair;
            Console.WriteLine(
                $"| {FormatStatus(row.Measurement.PcreNetNativeStatus)} | `{row.Section}` | `{row.CaseId}` | " +
                $"{row.ManagedMicroseconds:F3} us | {row.ComparatorMicroseconds:F3} us | {row.Ratio:F2}x | " +
                $"{row.ExcessMicroseconds:+0.000;-0.000;0.000} us | " +
                $"{(pair is null ? "—" : pair.SampleCount.ToString(CultureInfo.InvariantCulture))} | " +
                $"`{pair?.ManagedRoute ?? "—"}` |");
        }

        Console.WriteLine();
    }

    private static void WriteExclusionReasons(IReadOnlyList<Pcre2NativePriorityRow> excludedRows)
    {
        Console.WriteLine("## Exclusion reasons");
        Console.WriteLine();
        Console.WriteLine("| Rows | Reason |");
        Console.WriteLine("|---:|---|");
        foreach (var reason in excludedRows
                     .GroupBy(static row => row.Measurement.PcreNetNativeStatusReason ??
                                            row.Measurement.PcreNetNativeUnavailableReason ??
                                            "No reason recorded.", StringComparer.Ordinal)
                     .OrderByDescending(static group => group.Count())
                     .ThenBy(static group => group.Key, StringComparer.Ordinal))
        {
            Console.WriteLine($"| {reason.Count()} | {EscapeMarkdown(reason.Key)} |");
        }

        Console.WriteLine();
    }

    private static bool TryParseMode(string? text, out Pcre2PriorityMode mode)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            mode = Pcre2PriorityMode.All;
            return true;
        }

        if (text.Equals("relative", StringComparison.OrdinalIgnoreCase))
        {
            mode = Pcre2PriorityMode.Relative;
            return true;
        }

        if (text.Equals("absolute", StringComparison.OrdinalIgnoreCase))
        {
            mode = Pcre2PriorityMode.Absolute;
            return true;
        }

        mode = default;
        return false;
    }

    private static string FormatMode(Pcre2PriorityMode mode) => mode switch
    {
        Pcre2PriorityMode.Relative => "relative",
        Pcre2PriorityMode.Absolute => "absolute",
        Pcre2PriorityMode.All => "all",
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };

    private static string FormatStatus(Pcre2NativeComparisonStatus status) => status switch
    {
        Pcre2NativeComparisonStatus.ManagedFaster => "Managed faster",
        Pcre2NativeComparisonStatus.Equivalent => "Equivalent",
        Pcre2NativeComparisonStatus.NativeFaster => "Native faster",
        Pcre2NativeComparisonStatus.Inconclusive => "Inconclusive",
        Pcre2NativeComparisonStatus.Unqualified => "Unqualified",
        Pcre2NativeComparisonStatus.Excluded => "Excluded",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static string EscapeMarkdown(string value) => value
        .Replace("|", "\\|", StringComparison.Ordinal)
        .Replace("\r", " ", StringComparison.Ordinal)
        .Replace("\n", " ", StringComparison.Ordinal);

    private enum Pcre2PriorityMode : byte
    {
        Relative = 0,
        Absolute = 1,
        All = 2,
    }

    private readonly record struct Pcre2NativePriorityRow(
        string Section,
        string CaseId,
        Pcre2CaseMeasurementJson Measurement)
    {
        internal double ManagedMicroseconds => Measurement.PcreNetNativePair?.ManagedMedianMicroseconds ??
                                               Measurement.Utf8Pcre2;

        internal double ComparatorMicroseconds => Measurement.PcreNetNativePair?.ComparatorMedianMicroseconds ??
                                                  Measurement.PcreNetNative!.Value;

        internal double Ratio => Measurement.PcreNetNativePair?.RatioMedian ??
                                 ManagedMicroseconds / ComparatorMicroseconds;

        internal double ExcessMicroseconds => Measurement.PcreNetNativePair?.ExcessMedianMicroseconds ??
                                              ManagedMicroseconds - ComparatorMicroseconds;
    }
}
