using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeLiteralCursorTests
{
    [Theory]
    [InlineData("cat|dog","é😀dog tail",0,6,9)]
    [InlineData("😀|中","é😀中",0,2,6)]
    [InlineData("😀|中","é😀中",6,6,9)]
    [InlineData("a|ab","éab",0,2,3)]
    [InlineData("ab|a","éab",0,2,4)]
    [InlineData("foo(?=bar)","é😀foobar",0,6,9)]
    [InlineData("foo(?=bar)","éfoobar foobar",6,9,12)]
    [InlineData("(?:a|ab)(?=c)","éabc",0,2,4)]
    [InlineData("(?:ab|a)(?=bc)","éabc",0,2,3)]
    [InlineData("(?:😀|中)(?=é)","é😀é中é",8,8,11)]
    public void CaptureFreeFamilySearchKeepsScalarPriorityWithoutVmStorage(string pattern,string text,        int start,int byteStart,int byteEnd)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(text);
        var match = regex.Match(input,start,mode: Utf8PythonMatchMode.Search);
        Assert.True(match.Success);
        Assert.Equal((byteStart,byteEnd),            (match.StartOffsetInBytes,match.EndOffsetInBytes));
    }

    [Theory]
    [InlineData("cat|dog", "dog cat", "dog", "cat")]
    [InlineData("a|ab", "aba", "a", "a")]
    [InlineData("ab|a", "aba", "ab", "a")]
    [InlineData("😀|中", "é😀中", "😀", "中")]
    [InlineData("foo(?=bar)", "foobar foobar", "foo", "foo")]
    public void GlobalSnapshotsRetainGroupZeroAndNoLastCapture(string pattern, string text,
        string firstValue, string secondValue)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        try
        {
            Assert.True(cursor.MoveNext(out cursorRange));
            var first = cursorRange;
            Assert.True(alias.MoveNext(out cursorRange));
            var second = cursorRange;
            Assert.False(cursor.MoveNext(out cursorRange));
            alias.Dispose();

            Assert.Equal(firstValue, System.Text.Encoding.UTF8.GetString(first.GetValueMemory().Span));
            Assert.Equal(secondValue, System.Text.Encoding.UTF8.GetString(second.GetValueMemory().Span));
            Assert.Single(first.Groups.ToArray()); Assert.Single(second.Groups.ToArray());
            Assert.Null(first.LastIndex); Assert.Null(second.LastIndex);
            Assert.Null(first.LastGroup); Assert.Null(second.LastGroup);
        }
        finally { cursor.Dispose(); }
    }

    [Fact]
    public void MatchFullMatchAndBudgetFailuresKeepTheirContracts()
    {
        var regex = new Utf8PythonRegex("cat|dog");
        Assert.False(regex.Match("xcat"u8).Success);
        Assert.True(regex.Match("xcat"u8,1).Success);
        Assert.False(regex.Match("cat tail"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match("dog"u8,mode: Utf8PythonMatchMode.FullMatch).Success);

        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("😀cat"u8,1,mode: Utf8PythonMatchMode.Search));
        var bounded = new Utf8PythonRegex("cat|dog", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.Match("cat"u8,mode: Utf8PythonMatchMode.Search)).ResourceKind);
    }

    [Theory]
    [InlineData("cat(?=!)|dog", "cat? dog", 5, 8)]
    [InlineData(@"cat\b|dog", "catch dog", 6, 9)]
    [InlineData("cat$|dog", "catx dog", 5, 8)]
    [InlineData("(?i:cat)|dog", "CAT dog", 0, 3)]
    [InlineData("(?i:k)|dog", "Kdog", 0, 3)]
    [InlineData("(?:cat)?|dog", "dog", 0, 0)]
    [InlineData("a|ab|b|ba", "aba", 0, 1)]
    public void NecessaryHintsDoNotOverrideAssertionsFoldingEmptyOrLargerFamilies(
        string pattern, string text, int start, int end)
    {
        var match = new Utf8PythonRegex(pattern).Match(Encoding.UTF8.GetBytes(text),mode: Utf8PythonMatchMode.Search);
        Assert.True(match.Success);
        Assert.Equal((start, end), (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LongNativeSearchKeepsOriginalInterruptionAndPriorSnapshot(bool timeout)
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var armed = false; var interrupted = false; ulong armedAt = 0;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            // Hooks are sampled, not called for each byte block. Wait for
            // additional search work after construction/first publication.
            if (!armed || steps <= armedAt + 64) return;
            interrupted = true;
            if (timeout) ticks = 25; else cancellation.Cancel();
        });
        try
        {
            var regex = new Utf8PythonRegex("cat|dog", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20),
                new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 4000000, MaxResultBytes = 4000000 });
            var input = Encoding.UTF8.GetBytes("cat" + new string('x', 262144) + "dog");
            using var cursor = regex.CreateMatchCursor(input, 0, cancellation.Token);
            Assert.True(cursor.MoveNext(out cursorRange));
            var retained = cursorRange;
            armedAt = cursor.WorkingInvocation.Budget.Steps;
            armed = true;
            var failure = Record.Exception(() => cursor.MoveNext(out _));
            Assert.True(interrupted);
            if (timeout)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
                Assert.Empty(error.Input);
            }
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            cursor.Dispose();
            Assert.Equal("cat", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
        }
        finally { diagnostics.Complete(); }
    }
}
