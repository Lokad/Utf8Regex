using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// CPython 3.13.2 oracle receipt: artifacts/python-brace-quantifier-admission-20261017.
public sealed class PythonReBraceQuantifierTests
{
    [Theory]
    [InlineData("{", "{")]
    [InlineData("{}", "{}")]
    [InlineData("{foo}", "{foo}")]
    [InlineData("{1", "{1")]
    [InlineData("{1x", "{1x")]
    [InlineData("a{", "a{")]
    [InlineData("a{}", "a{}")]
    [InlineData("a{foo}", "a{foo}")]
    [InlineData("a{1", "a{1")]
    [InlineData("a{1x", "a{1x")]
    [InlineData("a{1,2x", "a{1,2x")]
    [InlineData("a{2147483648x", "a{2147483648x")]
    [InlineData("\ud83d\ude00{foo}", "\ud83d\ude00{foo}")]
    [InlineData("a*{foo}", "aaa{foo}")]
    [InlineData("a+{1x", "aaa{1x")]
    [InlineData("a{,2}", "aa")]
    [InlineData("a{,2}", "")]
    [InlineData("a{,}", "aaa")]
    [InlineData("\ud83d\ude00{,2}", "\ud83d\ude00\ud83d\ude00")]
    [InlineData("(\ud83d\ude00){,2}?\u00e9", "\ud83d\ude00\ud83d\ude00\u00e9")]
    [InlineData("(?P<g>a){,2}+b", "aab")]
    [InlineData("(?x)a {2}", "aa")]
    [InlineData("(?x)(a) #comment\n{2}", "aa")]
    [InlineData("(?x)a +? b", "aab")]
    [InlineData("(?x)a{ 2}", "a{2}")]
    [InlineData("(?x:a {,2})", "aa")]
    [InlineData("(?x:a) {2}", "a  ")]
    [InlineData("(?x)a* #comment\n{foo}", "aaa{foo}")]
    public void LiteralBracesAndQuantifierAttachmentFollowPython(string pattern, string text)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxDepth = 256,
            MaxWorkingBytes = 1000000,
            MaxResultBytes = 1000000
        };
        using var cancellation = new CancellationTokenSource();
        foreach (var bytes in new[] { false, true })
        {
            var regex = bytes
                ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = Regex.InfiniteMatchTimeout,
                    ResourcePolicy = policy
                }) : new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = Regex.InfiniteMatchTimeout,
                    ResourcePolicy = policy
                });
            var input = Encoding.UTF8.GetBytes(text);
            Utf8PythonRangeMatch actual = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, 0, cancellation.Token, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor1.MoveNext(out actual);
            }
            Assert.True(actual.Success);
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(actual.GetValueMemory().Span));
            Assert.Equal(0, actual.Value.StartOffsetInBytes);
            Assert.Equal(input.Length, actual.Value.EndOffsetInBytes);
            Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.FullMatch).Success);
        }
    }

    [Theory]
    [InlineData("{1}", "nothing to repeat", 0)]
    [InlineData("{,}", "nothing to repeat", 0)]
    [InlineData("{,2}", "nothing to repeat", 0)]
    [InlineData("a*{2}", "multiple repeat", 2)]
    [InlineData("a{2}{,3}", "multiple repeat", 4)]
    [InlineData("a{2,1}", "min repeat greater than max repeat", 2)]
    [InlineData("(?x)a* ?", "multiple repeat", 7)]
    [InlineData("(?x)a+ #comment\n*", "multiple repeat", 16)]
    public void ActualQuantifierErrorsKeepTheirMeaningAndPosition(string pattern, string message, int position)
    {
        var error = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Equal(message, error.Message);
        Assert.Equal(position, error.Position);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData("(😀){,2}?é", "😀😀é", "😀", 4)]
    [InlineData("(?P<g>a){,2}+b", "aab", "a", 1)]
    public void OmittedLowerBoundsKeepLastCompleteScalarCapture(string pattern, string text, string capture, int startByte)
    {
        var regex = new Utf8PythonRegex(pattern);
        Utf8PythonRangeMatch actual = default;
        using (var captureCursor2 = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out actual);
        }
        Assert.True(actual.Success);
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString((actual.Groups[1].Success ? actual.GetGroupMemory(actual.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(1, actual.LastIndex);
        Assert.Equal(startByte, actual.Groups[1].StartOffsetInBytes);
        Assert.Equal(1, regex.Count(Encoding.UTF8.GetBytes(text)));
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString((actual.Groups[1].Success ? actual.GetGroupMemory(actual.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("😀{,2}", 2)]
    [InlineData("😀{,2}?", 5)]
    [InlineData("😀{,2}+", 2)]
    public void OmittedLowerBoundsKeepScalarGlobalEmptyProgression(string pattern, int expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, regex.Count("😀😀"u8));
        ReadOnlyMemory<byte> snapshots1Input = ("😀😀"u8).ToArray();
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(expected == 5 ? ["", "😀", "", "😀", ""] : new[] { "😀😀", "" },
                    snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }
}
