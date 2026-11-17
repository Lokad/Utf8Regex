using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

// Consumer-owned projections for equal-output .NET/CPython comparisons.
// Strings/containers and their final publication are outside regex admission.
public readonly record struct PythonReHostCapture
{
    public int Number { get; init; }
    public bool Success { get; init; }
    public int StartOffsetInBytes { get; init; }
    public int EndOffsetInBytes { get; init; }
    public string ValueText { get => field ?? string.Empty; init; }
}

public readonly record struct PythonReHostDetailedMatch
{
    public PythonReHostCapture[] Groups { get => field ?? []; init; }
    public PythonReNameEntry[] NameEntries { get => field ?? []; init; }
    public int? LastIndex { get; init; }
    public bool Success => Groups.Length > 0 && Groups[0].Success;
    public PythonReHostCapture Value => Success ? Groups[0] : default;
    public string? LastGroup
    {
        get
        {
            if (LastIndex is int number)
                foreach (var entry in NameEntries) if (entry.Number == number) return entry.Name;
            return null;
        }
    }
}

internal static partial class PythonReBenchmarkReporter
{
    internal static PythonReHostDetailedMatch MaterializeDetailedSearch(Utf8PythonRegex regex,
        ReadOnlySpan<byte> input, int start = 0, CancellationToken token = default)
    {
        var result = ProjectDetailed(regex.SearchDetailed(input, start, token));
        token.ThrowIfCancellationRequested();
        return result;
    }

    internal static PythonReHostDetailedMatch MaterializeDetailedFullMatch(Utf8PythonRegex regex,
        ReadOnlySpan<byte> input, int start = 0, CancellationToken token = default)
    {
        var result = ProjectDetailed(regex.FullMatchDetailed(input, start, token));
        token.ThrowIfCancellationRequested();
        return result;
    }

    internal static PythonReHostDetailedMatch[] MaterializeDetailedMatches(Utf8PythonRegex regex,
        ReadOnlyMemory<byte> input, int start = 0, CancellationToken token = default)
    {
        var matches = new List<PythonReHostDetailedMatch>();
        using var cursor = regex.CreateMatchCursor(input, start, token);
        while (cursor.MoveNext(out var match))
        {
            var groups = new PythonReHostCapture[match.Groups.Length];
            for (var number = 0; number < groups.Length; number++)
            {
                var range = match.Groups[number];
                groups[number] = new() { Number = number, Success = range.Success,
                    StartOffsetInBytes = range.StartOffsetInBytes, EndOffsetInBytes = range.EndOffsetInBytes,
                    ValueText = Encoding.UTF8.GetString(match.GetGroupMemory(number).Span) };
            }
            matches.Add(new() { Groups = groups, NameEntries = match.NameEntries.ToArray(), LastIndex = match.LastIndex });
        }
        var result = matches.ToArray();
        token.ThrowIfCancellationRequested();
        return result;
    }

    private static PythonReHostDetailedMatch ProjectDetailed(Utf8PythonMatchContext match)
    {
        var groups = new PythonReHostCapture[match.CaptureSlotCount];
        for (var number = 0; number < groups.Length; number++)
        {
            var group = match.GetGroup(number);
            groups[number] = new() { Number = number, Success = group.Success,
                StartOffsetInBytes = group.Value.StartOffsetInBytes, EndOffsetInBytes = group.Value.EndOffsetInBytes,
                ValueText = Encoding.UTF8.GetString(group.Value.GetValueBytes()) };
        }
        return new() { Groups = groups, NameEntries = match.GetNameEntries(), LastIndex = match.LastIndex };
    }
}
