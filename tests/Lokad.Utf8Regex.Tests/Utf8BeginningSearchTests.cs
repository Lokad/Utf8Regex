using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BeginningSearchTests
{
    private static Utf8ResourcePolicy Policy()
        => new() { MaxDepth = 1024, MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 };

    [Theory]
    [InlineData(@"^[a-z][a-z0-9_]*$")]
    [InlineData(@"\A[a-z][a-z0-9_]*\z")]
    public void AnchoredFoldedRepeatPreservesScalarFoldingAndTheEndAnchor(string pattern)
    {
        const RegexOptions options = RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        using var regex = new Utf8Regex(pattern, options, new Utf8RegexPolicy { ResourcePolicy = Policy() });
        using var source = new CancellationTokenSource();
        var oracle = new Regex(pattern, options);
        foreach (var text in new[] { "", "A", "É", "Key_K", "Key\n", "Key\r\n", "A\nB", "A😀", "_0", "Aİ" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var expected = oracle.Match(text);
            var range = expected.Success ? new Utf8CaptureRange(0, Encoding.UTF8.GetByteCount(expected.Value)) : default;
            Assert.Equal(range, regex.Match(input, cancellationToken: source.Token));
            if (input.Length > 0) Assert.False(regex.Match(input, input.Length).Success);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(76)] [InlineData(77)] [InlineData(78)]
    [InlineData(236)] [InlineData(237)] [InlineData(238)]
    public void ContiguousLineMatchConsumesTheFirstNewlineWithinTheSelectedExtent(int padding)
    {
        using var regex = new Utf8Regex(@"\G///[^\n]*\n", RegexOptions.Compiled | RegexOptions.Multiline,
            new Utf8RegexPolicy { ResourcePolicy = Policy() });
        using var source = new CancellationTokenSource();
        const string head = "é";
        var line = "///" + new string('x', padding) + "😀\r\n";
        var input = Encoding.UTF8.GetBytes(head + line + "///next\n");
        var start = Encoding.UTF8.GetByteCount(head);
        var end = start + Encoding.UTF8.GetByteCount(line);
        Assert.Equal(new Utf8CaptureRange(start, end), regex.Match(input, start, cancellationToken: source.Token));
        Assert.False(regex.Match(input, start, endOffsetInBytes: end - 1).Success);
        Assert.False(regex.Match(input).Success);
        Assert.Equal(new Utf8CaptureRange(0, 4), regex.Match("///\n///next\n"u8));
        Assert.Equal(2, regex.Count("///\n///next\n"u8));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2047)] [InlineData(2048)]
    [InlineData(2049)] [InlineData(4095)] [InlineData(4096)] [InlineData(4097)]
    public void ScalarLineStartsOffsetsAndChunkTails(int padding)
    {
        var text = new string('x', padding) + "é😀\r\naaa\r\n\na\n";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var options = mode | RegexOptions.Multiline | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex("^(a+)", options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = Policy()
            });
            var oracle = new Regex("^(a+)", options);
            Assert.Equal(2, regex.Count(input));
            Assert.True(regex.IsMatch(input));
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Utf8MatchSequence.AssertCaptures(oracle.Match(text), actual, input, text);
            Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(1).Span));
            foreach (var offset in new[] { 0, padding, padding + 3, padding + 5, padding + 6, text.Length })
                Assert.Equal(oracle.Matches(text, offset).Count, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)))));
            var positions = new List<int>();
            foreach (var match in regex.EnumerateMatches(input)) positions.Add(match.StartOffsetInBytes);
            Assert.Equal(oracle.Matches(text).Select(match => System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index))).ToArray(), positions);
        }
    }

    [Theory]
    [InlineData("^", "", RegexOptions.Multiline)]
    [InlineData("^", "\n\r\n", RegexOptions.Multiline)]
    [InlineData("^", "a\r\nb\r\nc", RegexOptions.None)]
    [InlineData("\\A(a*)", "aa\naaa", RegexOptions.Multiline)]
    [InlineData("^(a*)", "é\ra\u2028a\u2029a\u0085a\naa\n", RegexOptions.Multiline)]
    [InlineData("^(a+)\\1$", "aaaa\naa\naba\n", RegexOptions.Multiline)]
    [InlineData("^(a+)|b+", "xxbb\naaa", RegexOptions.Multiline)]
    [InlineData("(^)?(a+)", "xxaa\naa", RegexOptions.Multiline)]
    [InlineData("^(a+)", "aaa\naaa", RegexOptions.Multiline | RegexOptions.RightToLeft)]
    [InlineData("^(a+)", "aaa\naaa", RegexOptions.Multiline | RegexOptions.NonBacktracking)]
    [InlineData("(?m)^(a+)", "xx\naa", RegexOptions.None)]
    [InlineData("(?m)(?-m:^(a+))", "xx\naa", RegexOptions.None)]
    public void EmptyAbsoluteInlineAndUnqualifiedShapes(string pattern, string text, RegexOptions mode)
    {
        var options = mode | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Policy()
        });
        var oracle = new Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        var positions = new List<int>();
        foreach (var match in regex.EnumerateMatches(input)) positions.Add(match.StartOffsetInBytes);
        Assert.Equal(oracle.Matches(text).Select(match => System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, match.Index))).ToArray(), positions);
    }

    [Fact]
    public void FilteringStartsDoesNotConstrainTheMatchBodyOrScalarWidth()
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$";
        var text = "xximport \"absent\"\nimport shared \"é\n😀\" tail\nimport \"b\"\n";
        var input = Encoding.UTF8.GetBytes(text);
        var options = RegexOptions.Multiline | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Policy()
        });
        Assert.Equal(2, regex.Count(input));
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal("é\n😀", System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(regex.GroupNumberFromUtf8Name("path"u8)).Span));
        Assert.Equal(new Regex(pattern, options).Match(text).Value, System.Text.Encoding.UTF8.GetString(actual.GetValueMemory().Span));
        var scalar = new Utf8Regex("^(.)", options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Policy()
        });
        var scalarMatch = Utf8MatchSequence.First(scalar.CreateMatchCursor("xx\n😀x"u8.ToArray()));
        Assert.Equal(2, scalar.Count("xx\n😀x"u8));
        Assert.Equal(1, (scalarMatch.Value.EndOffsetInBytes - scalarMatch.Value.StartOffsetInBytes));
        using var cursor = scalar.CreateMatchCursor("xx\n😀x"u8.ToArray(), 2);
        Assert.True(cursor.MoveNext(out var afterFirst));
        Assert.Equal((3, 7), (afterFirst.Value.StartOffsetInBytes, afterFirst.Value.EndOffsetInBytes));
        Assert.Equal("😀"u8.ToArray(), afterFirst.GetGroupMemory(1).ToArray());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OriginalInterruptionCoversLongSearch(bool findFirst, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        var input = new byte[320000]; Array.Fill(input, (byte)'x');
        var regex = new Utf8Regex("^(a+)", options: RegexOptions.CultureInvariant | RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = Policy()
        });
        var triggered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (triggered) return;
            triggered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            void Run()
            {
                if (findFirst) _ = regex.Match(input, cancellationToken: source.Token);
                else _ = regex.Count(input, source.Token);
            }
            if (expire) Assert.Throws<RegexMatchTimeoutException>(Run);
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(triggered);
        }
        finally { diagnostics.Complete(); }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2047)] [InlineData(2048)]
    [InlineData(2049)] [InlineData(4095)] [InlineData(4096)] [InlineData(4097)]
    public void ExactScanEdgesTrailingLfAndNoLf(int padding)
    {
        var regex = new Utf8Regex("^(a*)", options: RegexOptions.Multiline, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = Policy()
        });
        foreach (var ending in new[] { "", "\n", "\na" })
        {
            var text = new string('x', padding) + ending;
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex("^(a*)", RegexOptions.Multiline);
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Assert.Equal(oracle.Matches(text, padding).Count, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, padding)))));
            Assert.Equal(oracle.Match(text, padding).Success, regex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, padding)), []).Success);
        }
    }
}
