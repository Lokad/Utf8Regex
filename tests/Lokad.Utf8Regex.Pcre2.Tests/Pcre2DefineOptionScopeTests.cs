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
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(expected, regex.Match(input, cancellationToken: cancellation.Token).Success);
    }

    [Theory]
    [InlineData("(?(DEFINE)(?U)(?<x>a))b+", "bbb")]
    [InlineData("(?U)(?(DEFINE)(?-U)(?<x>a))b+", "b")]
    public void DefinitionCannotChangeOuterGreediness(string pattern, string value)
    {
        Assert.Equal(value, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex(pattern).Match("bbb"u8).GetValueBytes()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CapturePolicyRestoresWithoutRemovingDefinitionSlots(bool outerNoAutoCapture)
    {
        var regex = new Utf8Pcre2Regex(outerNoAutoCapture
            ? "(?(DEFINE)(?-n)(a))(b)" : "(?(DEFINE)(?n)(?<x>a))(b)", options: outerNoAutoCapture ? Pcre2CompileOptions.NoAutoCapture : Pcre2CompileOptions.None);
        ReadOnlySpan<byte> match3Input = "b"u8;
        var match3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match3Input, captures: match3Captures);
        Assert.True(match.Success);
        Assert.Equal(outerNoAutoCapture ? 2 : 3, (match.Success ? match3Captures.Length : 0));
        Assert.False(match3Captures[1].Success);
        if (!outerNoAutoCapture) Assert.Equal("b", System.Text.Encoding.UTF8.GetString((match3Captures[2].Success ? match3Input.Slice(match3Captures[2].StartOffsetInBytes, match3Captures[2].EndOffsetInBytes - match3Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefinitionLocalFoldsAndOuterScalarCapturesKeepTheirCoordinates(bool controlled)
    {
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(@"(?(DEFINE)(?i)(?<x>é😀))(?<whole>(?&x))\k<whole>", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = controlled ? new()
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20,
            } : Utf8ResourcePolicy.Unlimited,
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = "qÉ😀É😀 É😀É😀"u8.ToArray();
        ReadOnlySpan<byte> retained4Input = input;
        var retained4Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained4Input, captures: retained4Captures, cancellationToken: cancellation.Token);
        Assert.True(retained.Success);
        Assert.Equal(3, (retained.Success ? retained4Captures.Length : 0));
        Assert.Equal((1, 13), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal("É😀", System.Text.Encoding.UTF8.GetString((retained4Captures[2].Success ? retained4Input.Slice(retained4Captures[2].StartOffsetInBytes, retained4Captures[2].EndOffsetInBytes - retained4Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.False(regex.IsMatch("É😀é😀"u8)); // The following reference retains outer case sensitivity.
        Assert.Equal(2, regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal("q<É😀> <É😀>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$whole>"u8)));
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Equal("É😀", System.Text.Encoding.UTF8.GetString((retained4Captures[2].Success ? retained4Input.Slice(retained4Captures[2].StartOffsetInBytes, retained4Captures[2].EndOffsetInBytes - retained4Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DefinitionCallsKeepTheCursorDeadlineAndOriginalToken(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("(?(DEFINE)(?i)(?<x>é😀))(?&x)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
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
        Assert.Equal("É😀", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.True(regex.Match("É😀"u8).Success);
    }
}
