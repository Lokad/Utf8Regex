using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeLiteralCursorTests
{
    [Theory]
    [InlineData("cat|dog", "é😀dog tail", 0, 6, 9, 3, 6)]
    [InlineData("😀|中", "é😀中", 0, 2, 6, 1, 3)]
    [InlineData("😀|中", "é😀中", 6, 6, 9, 3, 4)]
    [InlineData("a|ab", "éab", 0, 2, 3, 1, 2)]
    [InlineData("ab|a", "éab", 0, 2, 4, 1, 3)]
    public void CaptureFreeFamilySearchKeepsScalarPriorityWithoutVmStorage(string pattern, string text,
        int start, int byteStart, int byteEnd, int utf16Start, int utf16End)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var input = Encoding.UTF8.GetBytes(text);
        var match = regex.Search(input, start);
        Assert.True(match.Success);
        Assert.Equal((byteStart, byteEnd, utf16Start, utf16End),
            (match.StartOffsetInBytes, match.EndOffsetInBytes, match.StartOffsetInUtf16, match.EndOffsetInUtf16));
    }

    [Theory]
    [InlineData("cat|dog", "dog cat", "dog", "cat")]
    [InlineData("a|ab", "aba", "a", "a")]
    [InlineData("ab|a", "aba", "ab", "a")]
    [InlineData("😀|中", "é😀中", "😀", "中")]
    public void GlobalSnapshotsRetainGroupZeroAndNoLastCapture(string pattern, string text,
        string firstValue, string secondValue)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        var cursor = regex.CreateMatchCursor(input);
        var alias = cursor;
        try
        {
            Assert.True(cursor.MoveNext());
            var first = alias.Current;
            Assert.True(alias.MoveNext());
            var second = cursor.Current;
            Assert.False(cursor.MoveNext());
            alias.Dispose();
            input.AsSpan().Fill((byte)'x');
            Assert.Equal(firstValue, first.Value.ValueText);
            Assert.Equal(secondValue, second.Value.ValueText);
            Assert.Single(first.Groups); Assert.Single(second.Groups);
            Assert.Null(first.LastIndex); Assert.Null(second.LastIndex);
            Assert.Null(first.LastGroup); Assert.Null(second.LastGroup);
        }
        finally { cursor.Dispose(); }
    }

    [Fact]
    public void MatchFullMatchValidationAndBudgetFailuresKeepTheirContracts()
    {
        var regex = new Utf8PythonRegex("cat|dog");
        Assert.False(regex.Match("xcat"u8).Success);
        Assert.True(regex.Match("xcat"u8, 1).Success);
        Assert.False(regex.FullMatch("cat tail"u8).Success);
        Assert.True(regex.FullMatch("dog"u8).Success);
        Assert.Throws<ArgumentException>(() => regex.Search(new byte[] { (byte)'c', (byte)'a', (byte)'t', 0x80 }));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("😀cat"u8, 1));
        var bounded = new Utf8PythonRegex("cat|dog", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => bounded.Search("cat"u8)).ResourceKind);
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
        var match = new Utf8PythonRegex(pattern).Search(Encoding.UTF8.GetBytes(text));
        Assert.True(match.Success);
        Assert.Equal((start, end), (match.StartOffsetInBytes, match.EndOffsetInBytes));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LongNativeSearchKeepsOriginalInterruptionAndPriorSnapshot(bool timeout)
    {
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
            Assert.True(cursor.MoveNext());
            var retained = cursor.Current;
            armedAt = cursor.WorkingInvocation.Budget.Steps;
            armed = true;
            var failure = Record.Exception(() => cursor.MoveNext());
            Assert.True(interrupted);
            if (timeout)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal(TimeSpan.FromMilliseconds(20), error.MatchTimeout);
                Assert.Empty(error.Input);
            }
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            cursor.Dispose();
            Assert.Equal("cat", retained.Value.ValueText);
        }
        finally { diagnostics.Complete(); }
    }
}
