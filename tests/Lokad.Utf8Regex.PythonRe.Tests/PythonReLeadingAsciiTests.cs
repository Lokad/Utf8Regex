using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLeadingAsciiTests
{
    private static Utf8ResourcePolicy Generous => new()
    {
        MaxWorkSteps = 1_000_000,
        MaxDepth = 1024,
        MaxWorkingBytes = 1 << 20,
        MaxResultBytes = 1 << 20
    };

    [Theory]
    [InlineData(2047)]
    [InlineData(2048)]
    [InlineData(2049)]
    [InlineData(4095)]
    [InlineData(4096)]
    public void LateUnicodeDigitsKeepByteFloorsAndDetailedCoordinates(int padding)
    {
        var text = "π😀" + new string('x', padding) + "😀needle१२ needle\U0001D7CE";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(@"needle\d+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Generous
        });
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input, 6, cancellation.Token));
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, 6, cancellation.Token))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        Assert.Equal(new[] { "needle१२", "needle\U0001D7CE" }, matches.Select(m => System.Text.Encoding.UTF8.GetString(m.GetValueMemory().Span)));
        Assert.Equal(1, regex.Count(input, 23 + padding));
        Assert.Equal(0, regex.Count(input, input.Length));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count(input, 1));

    }

    [Theory]
    [InlineData(@"needle\d+", "n needleX needle12 needle\U00010D40", 1)]
    [InlineData(@"n\d+", "nn12n३n\U0001D7CE", 3)]
    [InlineData("\0\\d+", "x\0१२ \0x \0\U0001D7CE", 2)]
    [InlineData(@"(?i:needle)\d+", "NEEDLE12 needle३", 2)]
    [InlineData(@"(?i:n)\d+", "N12 n३", 2)]
    [InlineData(@"^needle\d+", "xneedle12", 0)]
    [InlineData(@"(?m:^needle\d+)", "xneedle12\nneedle३", 1)]
    [InlineData(@"(?:needle\d+|x)", "x needle12", 2)]
    [InlineData(@"needle\d*|", "needle", 2)]
    public void NecessaryLiteralNeverReplacesProfileVerification(string pattern, string text, int expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Generous
        });
        Assert.Equal(expected, regex.Count(input));
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        Assert.Equal(expected, collectedRanges2.ToArray().Length);
    }

    [Fact]
    public void SkippedStartsNeedNoVmWorkspaceAndKeepRealWorkAdmission()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 4096));
        var miss = new Utf8PythonRegex(@"needle\d+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 2000,
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(0, miss.Count(input));
        var hit = new Utf8PythonRegex(@"needle\d+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Generous with
            {
                MaxWorkSteps = 2000,
                MaxResultBytes = 1
            }
        });
        Assert.Equal(1, hit.Count(Encoding.UTF8.GetBytes(new string('x', 4096) + "needle१२")));
        var denied = new Utf8PythonRegex(@"needle\d+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Generous with
            {
                MaxWorkSteps = 1
            }
        });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        Assert.Equal(1UL, error.Limit); Assert.True(error.Requested > error.Limit);
        Assert.Equal(0, miss.Count(input));
    }

    [Fact]
    public void MatchAndFullMatchStayAtTheRequestedPosition()
    {
        var regex = new Utf8PythonRegex(@"needle\d+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Generous
        });
        var input = "π😀xneedle१२"u8;
        Assert.True(regex.IsMatch(input, 6));
        Assert.False(regex.Match(input, 6).Success);
        Assert.False(regex.Match(input, 6, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match(input, 7).Success);
        Assert.True(regex.Match(input, 7, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void CursorAliasesAndRetainedCapturesSurviveSkippedStartsAndExhaustion()
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes("π😀" + new string('x', 2047) + "😀needle१२ xx needle\U0001D7CE");
        var regex = new Utf8PythonRegex(@"needle(?P<n>\d+)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Generous
        });
        var cursor = regex.CreateMatchCursor(input, 6);
        var alias = cursor;
        try
        {
            Assert.True(cursor.MoveNext(out cursorRange)); var first = cursorRange;
            Assert.Equal(2057, first.Value.StartOffsetInBytes);
            Assert.True(alias.MoveNext(out cursorRange)); var second = cursorRange;
            Assert.False(cursor.MoveNext(out cursorRange)); Assert.False(alias.MoveNext(out cursorRange));
            alias.Dispose();
            Assert.True(first.TryGetFirstSetGroup("n", out var group)); Assert.Equal("१२", System.Text.Encoding.UTF8.GetString((group.Success ? first.GetGroupMemory(group.Number) : default).Span));
            Assert.True(second.TryGetFirstSetGroup("n", out group)); Assert.Equal("\U0001D7CE", System.Text.Encoding.UTF8.GetString((group.Success ? second.GetGroupMemory(group.Number) : default).Span));
            Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        }
        finally { cursor.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongMissKeepsOriginalInterruptionDuringSearch(bool expire)
    {
        var regex = new Utf8PythonRegex(@"needle\d+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = Generous
        });
        var input = Encoding.UTF8.GetBytes(new string('x', 256 * 1024));
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < 64) return;
            entered = true;
            if (expire) ticks = 50; else cancellation.Cancel();
        });
        try
        {
            if (expire) Assert.Empty(Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input)).Input);
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, 0, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered); ticks = 0;
        Assert.Equal(0, regex.Count(input));
    }
}
