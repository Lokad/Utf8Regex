using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ResetOptionExecutionTests
{
    [Theory]
    [InlineData("(?^:a#b)", Pcre2CompileOptions.Extended, "a#b", true)]
    [InlineData("(?^:[ a])", Pcre2CompileOptions.ExtendedMore, " ", true)]
    [InlineData("(?^:a)b", Pcre2CompileOptions.Caseless, "Ab", false)]
    [InlineData("(?^:a)b", Pcre2CompileOptions.Caseless, "aB", true)]
    [InlineData("(?^:a.)", Pcre2CompileOptions.DotAll, "a\n", false)]
    [InlineData("(?^:a).", Pcre2CompileOptions.DotAll, "a\n", true)]
    [InlineData("(?^:^a$)", Pcre2CompileOptions.Multiline, "x\na\n", false)]
    [InlineData("(?^:a)$", Pcre2CompileOptions.Multiline, "a\nx", true)]
    [InlineData("(?^i:a)", Pcre2CompileOptions.None, "A", true)]
    [InlineData("(?^x:a # ignored\n b)", Pcre2CompileOptions.None, "ab", true)]
    [InlineData("(?^n:(a))", Pcre2CompileOptions.None, "a", true)]
    [InlineData("(?^:)", Pcre2CompileOptions.None, "😀", true)]
    [InlineData("(?^:.)", Pcre2CompileOptions.DotAll, "😀", true)]
    public void ScopedResetClearsOnlyItsFlagsAndCanEnableSupportedOptions(string pattern,
        Pcre2CompileOptions options, string text, bool expected)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options, default,
            new() { MatchLimit = 100000, DepthLimit = 256,
                HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(expected, regex.MatchDetailed(bytes, 0, Pcre2MatchOptions.None, cancellation.Token).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResetRestoresCapturePolicyAndPreservesPublishedScalarCaptures(bool controlled)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?^:(😀))(b)", Pcre2CompileOptions.NoAutoCapture | Pcre2CompileOptions.Caseless,
            default, controlled ? new() { MatchLimit = 100000, DepthLimit = 256,
                HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 } : default, Timeout.InfiniteTimeSpan);
        var bytes = "é😀B😀b"u8.ToArray();
        var match = regex.MatchDetailed(bytes, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(match.Success);
        Assert.Equal((2, 7), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal(2, match.CaptureSlotCount); // Outer (b) retains NoAutoCapture.
        var retained = match.GetGroup(1);
        Assert.Equal((2, 6), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal("😀", retained.GetValueString());
        Assert.Equal(2, regex.Count(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("é😀😀", regex.ReplaceToString(bytes, "$1"));
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.Count(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("😀", retained.GetValueString());
    }

    [Theory]
    [InlineData("(?^:a+)", Pcre2CompileOptions.Ungreedy, "a")]
    [InlineData("(?U)(?^:a+)", Pcre2CompileOptions.None, "a")]
    [InlineData("(?^:a+)", Pcre2CompileOptions.None, "aaa")]
    public void ResetPreservesUngreedyPolicy(string pattern, Pcre2CompileOptions options, string value)
    {
        Assert.Equal(value, new Utf8Pcre2Regex(pattern, options).MatchDetailed("aaa"u8).GetValueString());
    }

    [Theory]
    [InlineData("a")]
    [InlineData("b")]
    public void ResetPreservesExistingDuplicateNameMode(string text)
    {
        var regex = new Utf8Pcre2Regex("(?J)(?^:(?<x>a)|(?<x>b))");
        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes(text));
        Assert.True(match.Success);
        Assert.Equal(3, match.CaptureSlotCount);
        Assert.Equal(2, regex.NameEntryCount);
        Assert.True(match.TryGetFirstSetGroup("x", out var group));
        Assert.Equal(text, group.GetValueString());
    }

    [Theory]
    [InlineData("(?^)(a)")]
    [InlineData("(?^n)(?<x>a)")]
    public void GlobalResetExecutesCapturedPrograms(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.NoAutoCapture | Pcre2CompileOptions.Caseless);
        var match = regex.MatchDetailed("a"u8);
        Assert.True(match.Success);
        Assert.Equal(2, match.CaptureSlotCount);
        Assert.False(regex.IsMatch("A"u8));
    }

    [Theory]
    [InlineData("(?^-x:a)")]
    [InlineData("(?^i-x:a)")]
    [InlineData("(?^-x)a")]
    public void ResetRejectsTheDisallowedMinusFormBeforeExecution(string pattern)
    {
        var failure = Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, failure.ErrorKind);
    }

    [Fact]
    public void EmptyResetAdvancesByCompleteScalarsUnderFiniteControls()
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?^:)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256,
                HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(2, regex.Count("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token));
        var cursor = regex.EnumerateMatches("😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token);
        var starts = new List<int>();
        while (cursor.MoveNext()) starts.Add(cursor.Current.StartOffsetInBytes);
        Assert.Equal(new[] { 0, 4 }, starts);
    }

    [Fact]
    public void ResetCaptureIsAvailableToFollowingNumericReferences()
    {
        var regex = new Utf8Pcre2Regex(@"(?^:(😀))\1", Pcre2CompileOptions.NoAutoCapture);
        var match = regex.MatchDetailed("é😀😀"u8);
        Assert.True(match.Success);
        Assert.Equal((2, 10), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
        Assert.Equal("😀", match.GetGroup(1).GetValueString());
        Assert.Equal("é😀", regex.ReplaceToString("é😀😀"u8, "$1"));
    }
}
