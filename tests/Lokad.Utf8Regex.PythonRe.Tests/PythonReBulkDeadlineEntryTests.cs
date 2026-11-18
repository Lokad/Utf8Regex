using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBulkDeadlineEntryTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExpiredEntryPrecedesDiscovery(int start)
    {
        foreach (var pattern in new[] { "cat|horse", "cat" })
        {
            foreach (var input in new[] { "xcat"u8.ToArray(), "xcat😀"u8.ToArray() })
            {
                var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = TimeSpan.FromMilliseconds(1)
                });
                long reads = 0;
                using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000);
                var error = Assert.Throws<RegexMatchTimeoutException>(() =>
                {
                    ReadOnlyMemory<byte> snapshots1Input = input;
                    var snapshots1 = new List<Utf8PythonRangeMatch>();
                    using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, start, default))
                    {
                        while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                            snapshots1.Add(snapshots1Match);
                    }
                    return string.Join("|", snapshots1.Select(captureSnapshot1 => captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
                });
                Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
                Assert.Equal(regex.Pattern, error.Pattern);
                Assert.True(reads >= 2);
            }
        }
    }

    [Fact]
    public void RetainedValuesAndScalarCoordinatesSurviveTimeoutAndFreshCalls()
    {
        var regex = new Utf8PythonRegex("cat|horse", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var input = Encoding.UTF8.GetBytes("π😀 cat horse cat");
        using var steadyClock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => 0);
        var wholeMatches2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = regex.CreateMatchCursor(input, 7, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
            {
                wholeMatches2.Add(rangeMatch2);
            }
        }
        var detailed = wholeMatches2.ToArray();
        Assert.Equal(new[] { "cat", "horse", "cat" }, detailed.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));
        ReadOnlyMemory<byte> snapshots2Input = input;
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 7, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        var retained = string.Join("|", snapshots2.Select(captureSnapshot2 => captureSnapshot2.GetGroupMemory(captureSnapshot2.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
        long reads = 0;
        using (Utf8ExecutionDeadline.UseClockForDiagnostics(() => reads++ == 0 ? 0 : 1000))
        {
            Assert.Throws<RegexMatchTimeoutException>(() =>
            {
                ReadOnlyMemory<byte> snapshots3Input = input;
                var snapshots3 = new List<Utf8PythonRangeMatch>();
                using (var snapshots3Cursor = regex.CreateMatchCursor(snapshots3Input, 7, default))
                {
                    while (snapshots3Cursor.MoveNext(out var snapshots3Match))
                        snapshots3.Add(snapshots3Match);
                }
                return string.Join("|", snapshots3.Select(captureSnapshot4 => captureSnapshot4.GetGroupMemory(captureSnapshot4.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
            });
        }
        ReadOnlyMemory<byte> snapshots4Input = input;
        var snapshots4 = new List<Utf8PythonRangeMatch>();
        using (var snapshots4Cursor = regex.CreateMatchCursor(snapshots4Input, 7, default))
        {
            while (snapshots4Cursor.MoveNext(out var snapshots4Match))
                snapshots4.Add(snapshots4Match);
        }
        Assert.Equal(retained, string.Join("|", snapshots4.Select(captureSnapshot5 => captureSnapshot5.GetGroupMemory(captureSnapshot5.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString)));
        Assert.Equal(new[] { "cat", "horse", "cat" }, detailed.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));
        ReadOnlyMemory<byte> snapshots5Input = input;
        int snapshots5Start = input.Length;
        var snapshots5 = new List<Utf8PythonRangeMatch>();
        using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, snapshots5Start, default))
        {
            while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                snapshots5.Add(snapshots5Match);
        }
        Assert.Equal(0, snapshots5.Count);

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
        {
            ReadOnlyMemory<byte> snapshots6Input = input;
            var snapshots6 = new List<Utf8PythonRangeMatch>();
            using (var snapshots6Cursor = regex.CreateMatchCursor(snapshots6Input, 7, canceled.Token))
            {
                while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                    snapshots6.Add(snapshots6Match);
            }
            return string.Join("|", snapshots6.Select(captureSnapshot6 => captureSnapshot6.GetGroupMemory(captureSnapshot6.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString));
        });
        Assert.Equal(canceled.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData("ab[0-9]+", "π😀 ab123 ab4", 7, "ab123|ab4")]
    [InlineData("^cat", "π😀 cat", 7, "")]
    [InlineData("cat$", "π😀 cat", 7, "cat")]
    [InlineData("(?i)k", "π😀 k K K", 7, "k|K|K")]
    [InlineData("[α-ω]+", "π😀 αβ γδ", 7, "αβ|γδ")]
    public void NativeAnchoredAndScalarFallbacksKeepTheWholeSubject(
        string pattern, string text, int start, string expected)
    {
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => 0);
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var input = Encoding.UTF8.GetBytes(text);
        ReadOnlyMemory<byte> snapshots7Input = input;
        var snapshots7 = new List<Utf8PythonRangeMatch>();
        using (var snapshots7Cursor = regex.CreateMatchCursor(snapshots7Input, start, default))
        {
            while (snapshots7Cursor.MoveNext(out var snapshots7Match))
                snapshots7.Add(snapshots7Match);
        }
        Assert.Equal(expected, string.Join("|", snapshots7.Select(captureSnapshot7 => captureSnapshot7.GetGroupMemory(captureSnapshot7.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(Encoding.UTF8.GetString)));
    }
}
