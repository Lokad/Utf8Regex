using System.Diagnostics;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2GraphemeInvocationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("a\u0301b")]
    [InlineData("\u0301")]
    [InlineData("👩🏽‍💻")]
    [InlineData("🇫🇷🇩")]
    [InlineData("\r\n")]
    [InlineData("\0")]
    public void LoneGraphemeExistenceKeepsEveryScalarFloorAndOffsetSpan(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var storage = new byte[bytes.Length + 5]; bytes.CopyTo(storage, 2);
        var direct = new Utf8Pcre2Regex(@"\X");
        var finite = new Utf8Pcre2Regex(@"\X", options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        for (var start = 0; start <= bytes.Length; start++)
        {
            if (start < bytes.Length && (bytes[start] & 0xc0) == 0x80) continue;
            Assert.Equal(start < bytes.Length, direct.IsMatch(storage.AsSpan(2, bytes.Length), start));
            Assert.Equal(start < bytes.Length, finite.IsMatch(storage.AsSpan(2, bytes.Length), start));
            var cursor = direct.EnumerateMatches(storage.AsSpan(2, bytes.Length), start);
            var count = 0;
            while (cursor.MoveNext()) count++;
            Assert.Equal(count, direct.Count(storage.AsSpan(2, bytes.Length), start));
            Assert.Equal(count, finite.Count(storage.AsSpan(2, bytes.Length), start));
        }
    }

    [Fact]
    public void RegionalIndicatorPairsKeepSubjectContextAtNonzeroStarts()
    {
        using var regex = new Utf8Pcre2Regex(@"\X");
        ReadOnlyMemory<byte> input = "🇦🇧🇨🇩🇪"u8.ToArray();
        Assert.Equal(3, regex.Count(input.Span, 4));
        Assert.Equal(8, regex.Match(input.Span, 4).EndOffsetInBytes);
        Assert.Equal(16, regex.Match(input.Span, 8).EndOffsetInBytes);

        var span = regex.EnumerateMatches(input.Span, 4);
        using var memory = regex.CreateMatchCursor(input, 4);
        foreach (var end in new[] { 8, 16, 20 })
        {
            Assert.True(span.MoveNext());
            Assert.True(memory.MoveNext(out var range));
            Assert.Equal(end, span.Current.EndOffsetInBytes);
            Assert.True(range.TryGetGroup(0, out var whole));
            Assert.Equal(end, whole.EndOffsetInBytes);
        }
        Assert.False(span.MoveNext());
        Assert.False(memory.MoveNext(out _));

        using var capture = new Utf8Pcre2Regex(@"\A🇦(?<g>\X)");
        Span<Utf8Pcre2CaptureRange> slots = stackalloc Utf8Pcre2CaptureRange[2];
        var captured = capture.Match(input.Span, captures: slots);
        Assert.True(captured.Success);
        Assert.Equal((4, 8), (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
    }

    [Fact]
    public void RegionalIndicatorPairsRestartAtMalformedFragmentBoundaries()
    {
        using var regex = new Utf8Pcre2Regex(@"\X", Pcre2CompileOptions.MatchInvalidUtf8);
        byte[] input = [.."🇦🇧"u8, 0xff, .."🇦🇧🇨"u8];
        Assert.Equal(3, regex.Count(input));
        Assert.Equal(17, regex.Match(input, 9).EndOffsetInBytes);
        Assert.Equal(17, regex.Match(input, 13).EndOffsetInBytes);
        Assert.Equal(2, regex.Count(input, 13));
    }

    [Fact]
    public void CancellationDuringRegionalPrefixScanKeepsOriginalToken()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("🇦", 2048)));
        using var regex = new Utf8Pcre2Regex(@"\X", policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromHours(1),
        });
        using var source = new CancellationTokenSource();
        var checkpoints = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (InsideGrapheme() && ++checkpoints == 3) source.Cancel();
            return 0;
        });
        var failure = Assert.Throws<OperationCanceledException>(() => regex.Count(
            input, input.Length - 8, cancellationToken: source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
    }

    [Fact]
    public void BooleanGraphemeProofKeepsValidationFlagsAndReturnedClusterEndpoints()
    {
        var regex = new Utf8Pcre2Regex(@"\X");
        var input = "áb"u8.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch(input, input.Length + 1));
        Assert.False(regex.IsMatch("ab"u8, 0, Pcre2MatchOptions.Anchored | Pcre2MatchOptions.EndAnchored));
        Assert.False(new Utf8Pcre2Regex(@"\X", options: Pcre2CompileOptions.Anchored | Pcre2CompileOptions.EndAnchored).IsMatch("ab"u8));
        Assert.False(new Utf8Pcre2Regex(@"\X\X").IsMatch("a"u8));
        var match = regex.Match(input);
        Assert.Equal("á", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(3, match.EndOffsetInBytes);
        Assert.Equal(2, regex.Count(input));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(input, 0, default, canceled.Token)).CancellationToken);
    }

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
        var regex = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromHours(1)
        });
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
        var regex = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var checkpoints = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => InsideGrapheme() && ++checkpoints >= 3 ? 100 : 0))
        {
            var failure = Assert.Throws<Pcre2MatchException>(() => Run(regex, input, route, default));
            Assert.Equal(Pcre2ErrorKind.Timeout, failure.ErrorKind);
            Assert.Equal(3, checkpoints);
        }
        // Recovery uses the same short timeout under controlled time, including
        // when a loaded CI runner pauses this invocation for more than 20 ms.
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(static () => 0))
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
                var regex = new Utf8Pcre2Regex(@"\X", options: default, compileSettings: default, policy: new Utf8RegexPolicy
                {
                    ResourcePolicy = finite ? new()
                    {
                        MaxDepth = 1024,
                        MaxWorkingBytes = 16 << 20,
                        MaxResultBytes = 16 << 20,
                    } : Utf8ResourcePolicy.Unlimited,
                    MatchTimeout = Timeout.InfiniteTimeSpan
                });
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
            case "Match": _ = regex.Match(input, startOffsetInBytes: 0, matchOptions: default, cancellationToken: token); break;
            case "Enumerate": var cursor = regex.EnumerateMatches(input, 0, default, token); _ = cursor.MoveNext(); break;
            case "Replace": _ = regex.Replace(input, "Z"u8, 0, default, default, token); break;
            case "Destination": _ = regex.TryReplace(input, "Z"u8, new byte[4], out _, 0, default, default, token); break;
            default: throw new ArgumentOutOfRangeException(nameof(route));
        }
    }
}
