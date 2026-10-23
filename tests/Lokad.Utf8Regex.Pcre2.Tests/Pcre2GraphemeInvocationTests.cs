using System.Diagnostics;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2GraphemeInvocationTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var pattern in new[] { @"\X", @"(?=a)\X" })
        foreach (var route in new[] { "IsMatch", "Count", "Match", "Enumerate", "Replace", "Destination" })
            yield return [pattern, route];
    }

    [Theory] [MemberData(nameof(Routes))]
    public void CancellationInsideLongClusterKeepsOriginalToken(string pattern, string route)
    {
        var input = Encoding.UTF8.GetBytes("a" + new string('\u0301', 8192));
        var regex = new Utf8Pcre2Regex(pattern, default, default, default, TimeSpan.FromHours(1));
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => {
            if (InsideGrapheme() && ++checkpoints == 3) source.Cancel();
            return 0;
        }))
        {
            var failure = Assert.Throws<OperationCanceledException>(() => Run(regex, input, route, source.Token));
            Assert.Equal(source.Token, failure.CancellationToken);
            Assert.InRange(checkpoints, 3, 4);
        }
        Assert.True(regex.IsMatch("a"u8));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void DeadlineExpiresInsideLongCluster(string pattern, string route)
    {
        var input = Encoding.UTF8.GetBytes("a" + new string('\u0301', 8192));
        var regex = new Utf8Pcre2Regex(pattern, default, default, default, TimeSpan.FromMilliseconds(20));
        var checkpoints = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => InsideGrapheme() && ++checkpoints >= 3 ? 100 : 0))
        {
            var failure = Assert.Throws<Pcre2MatchException>(() => Run(regex, input, route, default));
            Assert.Equal(Pcre2ErrorKind.Timeout, failure.ErrorKind);
            Assert.Equal(3, checkpoints);
        }
        Assert.True(regex.IsMatch("a"u8));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void FiniteWorkDeniesInsideScanAfterValidation(string pattern, string route)
    {
        var input = Encoding.UTF8.GetBytes("a" + new string('\u0301', 65536));
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 128, DepthLimit = 1024,
            HeapLimitInBytes = 16 << 20, ResultLimitInBytes = 16 << 20 };
        Assert.True(new Utf8Pcre2Regex("a", default, default, limits, Timeout.InfiniteTimeSpan).IsMatch(input));
        var regex = new Utf8Pcre2Regex(pattern, default, default, limits, Timeout.InfiniteTimeSpan);
        var failure = Assert.Throws<Pcre2MatchException>(() => Run(regex, input, route, default));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, failure.ErrorKind);
        Assert.Contains(nameof(Pcre2GraphemeClusterSemantics), failure.StackTrace);
        Assert.True(regex.IsMatch("a"u8));
    }

    [Fact]
    public void ControlledClustersKeepCombiningZwjRegionalAndScalarBoundaries()
    {
        foreach (var (text, expected) in new[] {
            ("a" + new string('\u0301', 8192) + "x", 2),
            ("👩" + string.Concat(Enumerable.Repeat("\u200D👩", 1024)) + "x", 2),
            ("🇫🇷🇩", 2), ("각x", 2), ("😀\u0301", 1), ("", 0) })
        {
            var input = Encoding.UTF8.GetBytes(text);
            List<(int, int)>? original = null;
            foreach (var finite in new[] { false, true })
            {
                var regex = new Utf8Pcre2Regex(@"\X", default, default,
                    finite ? new() { MatchLimit = 1_000_000, DepthLimit = 1024, HeapLimitInBytes = 16 << 20, ResultLimitInBytes = 16 << 20 } : default,
                    Timeout.InfiniteTimeSpan);
                var ranges = new List<(int, int)>();
                var cursor = regex.EnumerateMatches(input);
                while (cursor.MoveNext()) ranges.Add((cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
                Assert.Equal(expected, ranges.Count);
                Assert.Equal(expected, regex.Count(input));
                if (original is null) original = ranges; else Assert.Equal(original, ranges);
                if (ranges.Count != 0) { Assert.Equal(0, ranges[0].Item1); Assert.Equal(input.Length, ranges[^1].Item2); }
            }
        }
    }

    private static bool InsideGrapheme() => new StackTrace().GetFrames()
        .Any(frame => frame.GetMethod()?.DeclaringType == typeof(Pcre2GraphemeClusterSemantics));

    private static void Run(Utf8Pcre2Regex regex, byte[] input, string route, CancellationToken token)
    {
        switch (route)
        {
            case "IsMatch": _ = regex.IsMatch(input, 0, default, token); break;
            case "Count": _ = regex.Count(input, 0, default, token); break;
            case "Match": _ = regex.Match(input, 0, default, token); break;
            case "Enumerate": var cursor = regex.EnumerateMatches(input, 0, default, token); _ = cursor.MoveNext(); break;
            case "Replace": _ = regex.Replace(input, "Z", 0, default, default, token); break;
            case "Destination": _ = regex.TryReplace(input, "Z"u8, new byte[4], out _, 0, default, default, token); break;
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
    }
}
