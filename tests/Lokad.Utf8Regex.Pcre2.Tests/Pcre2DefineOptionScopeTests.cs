using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2DefineOptionScopeTests
{
    [Theory]
    [InlineData("(?(DEFINE)(?i)(?<x>a))b", "B", false)]
    [InlineData("(?i)(?(DEFINE)(?-i)(?<x>a))b", "B", true)]
    [InlineData("(?(DEFINE)(?m)(?<x>a))^b", "q\nb", false)]
    [InlineData("(?(DEFINE)(?s)(?<x>a)).", "\n", false)]
    [InlineData("(?(DEFINE)(?x)(?<x>a)) b", "b", false)]
    [InlineData("(?(DEFINE)(?x)(?<x>a)) b", " b", true)]
    [InlineData("(?(DEFINE)(?xx)(?<x>a))[ b]", " ", true)]
    [InlineData("(?(DEFINE)(?i)(?<x>😀))b", "B", false)]
    [InlineData("(?(DEFINE)(?i)(?<x>a))(?J:(?<y>b)|(?<y>c))", "B", false)]
    [InlineData("(?(DEFINE)(?i)(?<x>a))(?&x)", "A", true)]
    [InlineData("(?i)(?(DEFINE)(?-i)(?<x>a))(?&x)", "A", false)]
    [InlineData("(?x)(?(DEFINE)(?-x)(?<x>a)) b", "b", true)]
    public void DefinitionFlagsStayLocalAndInheritTheOuterSettings(string pattern, string text, bool expected)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(expected, regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token).Success);
    }

    [Theory]
    [InlineData("(?(DEFINE)(?U)(?<x>a))b+", "bbb")]
    [InlineData("(?U)(?(DEFINE)(?-U)(?<x>a))b+", "b")]
    public void DefinitionCannotChangeOuterGreediness(string pattern, string value)
    {
        Assert.Equal(value, new Utf8Pcre2Regex(pattern).MatchDetailed("bbb"u8).GetValueString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturePolicyRestoresWithoutRemovingDefinitionSlots(bool outerNoAutoCapture)
    {
        var regex = new Utf8Pcre2Regex(outerNoAutoCapture
            ? "(?(DEFINE)(?-n)(a))(b)" : "(?(DEFINE)(?n)(?<x>a))(b)",
            outerNoAutoCapture ? Pcre2CompileOptions.NoAutoCapture : Pcre2CompileOptions.None);
        var match = regex.MatchDetailed("b"u8);
        Assert.True(match.Success);
        Assert.Equal(outerNoAutoCapture ? 2 : 3, match.CaptureSlotCount);
        Assert.False(match.GetGroup(1).Success);
        if (!outerNoAutoCapture) Assert.Equal("b", match.GetGroup(2).GetValueString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefinitionLocalFoldsAndOuterScalarCapturesKeepTheirCoordinates(bool controlled)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?(DEFINE)(?i)(?<x>é😀))(?<whole>(?&x))\k<whole>",
            Pcre2CompileOptions.None, default, controlled ?
                new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                    ResultLimitInBytes = 1 << 20 } : default, Timeout.InfiniteTimeSpan);
        var input = "qÉ😀É😀 É😀É😀"u8.ToArray();
        var retained = regex.MatchDetailed(input, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(retained.Success);
        Assert.Equal(3, retained.CaptureSlotCount);
        Assert.Equal((1, 13), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal("É😀", retained.GetGroup(2).GetValueString());
        Assert.False(regex.IsMatch("É😀é😀"u8)); // The following reference retains outer case sensitivity.
        Assert.Equal(2, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("q<É😀> <É😀>", regex.ReplaceToString(input, "<$whole>"));
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal("É😀", retained.GetGroup(2).GetValueString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefinitionCallsKeepTheCursorDeadlineAndOriginalToken(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?(DEFINE)(?i)(?<x>é😀))(?&x)",
            Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 1 << 20,
                ResultLimitInBytes = 1 << 20 }, TimeSpan.FromMilliseconds(20));
        var cursor = regex.EnumerateMatches("É😀É😀"u8, 0, Pcre2MatchOptions.None, cancellation.Token);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        var alias = cursor;
        if (cancel) cancellation.Cancel(); else ticks = 25;
        Exception? failure = null;
        try { _ = alias.MoveNext(); } catch (Exception error) { failure = error; }
        if (cancel)
            Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal("É😀", retained.GetValueString());
        Assert.True(regex.MatchDetailed("É😀"u8).Success);
    }
}
