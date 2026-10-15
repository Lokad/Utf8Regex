using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ExtendedModeExecutionTests
{
    public static IEnumerable<object[]> Modes()
    {
        yield return ["(?xx:[ a])", Pcre2CompileOptions.None, true, false, false, false];
        yield return ["(?xx:[\ta])", Pcre2CompileOptions.None, true, false, false, false];
        yield return ["(?x:[ a])", Pcre2CompileOptions.None, true, true, false, false];
        yield return ["(?xx)(?x)[ a]", Pcre2CompileOptions.None, true, true, false, false];
        yield return ["(?x)(?x)[ a]", Pcre2CompileOptions.None, true, true, false, false];
        yield return ["(?-x:[ a])", Pcre2CompileOptions.ExtendedMore, true, true, false, false];
        yield return ["(?xx)(?-x:[ a])", Pcre2CompileOptions.None, true, true, false, false];
        yield return ["(?x:[ a])", Pcre2CompileOptions.ExtendedMore, true, true, false, false];
        yield return ["(?xxix:[ a])", Pcre2CompileOptions.None, true, false, false, true];
        yield return ["(?xx:[ a])|[ b]", Pcre2CompileOptions.None, true, true, false, false];
        yield return ["(?xx)[ a]", Pcre2CompileOptions.None, true, false, false, false];
        yield return ["(?xx)(?-x)[ a]", Pcre2CompileOptions.None, true, true, false, false];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void PerDirectiveModesMatchClassesConsistentlyAcrossPublicOperations(string pattern,
        Pcre2CompileOptions options, bool letter, bool space, bool tab, bool capital)
    {
        using var cancellation = new CancellationTokenSource();
        var finite = new Utf8Pcre2Regex(pattern, options, default,
            new() { MatchLimit = 100000, DepthLimit = 256,
                HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        foreach (var regex in new[] { new Utf8Pcre2Regex(pattern, options), finite })
        foreach (var (text, expected) in new[] { ("a", letter), (" ", space), ("\t", tab), ("A", capital) })
        {
            var bytes = Encoding.UTF8.GetBytes("😀" + text);
            Assert.Equal(expected, regex.IsMatch(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
            Assert.Equal(expected ? 1 : 0, regex.Count(bytes, 0, Pcre2MatchOptions.None, cancellation.Token));
            var match = regex.MatchDetailed(bytes, 0, Pcre2MatchOptions.None, cancellation.Token);
            Assert.Equal(expected, match.Success);
            if (expected)
            {
                Assert.Equal((4, 5), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
                Assert.Equal(text, match.GetValueString());
            }
        }
    }

    [Fact]
    public void ScopedClassWhitespaceKeepsCapturesAndRestoresOuterFlags()
    {
        foreach (var controlled in new[] { false, true })
        {
            using var cancellation = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex("(?<g>(?xx:[ a]+))(?<h>(?x:[ b]+))", Pcre2CompileOptions.None, default,
                controlled ? new() { MatchLimit = 100000, DepthLimit = 256,
                    HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 } : default, Timeout.InfiniteTimeSpan);
            var match = regex.MatchDetailed("😀 aa bb\t"u8, 0, Pcre2MatchOptions.None, cancellation.Token);
            Assert.True(match.Success);
            Assert.Equal((5, 10), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
            Assert.True(match.TryGetFirstSetGroup("g", out var first));
            Assert.Equal("aa", first.GetValueString());
            Assert.Equal((5, 7), (first.StartOffsetInBytes, first.EndOffsetInBytes));
            Assert.True(match.TryGetFirstSetGroup("h", out var second));
            Assert.Equal(" bb", second.GetValueString());
            Assert.Equal((7, 10), (second.StartOffsetInBytes, second.EndOffsetInBytes));
            Assert.Equal("😀 <aa| bb>\t", regex.ReplaceToString("😀 aa bb\t"u8, "<$1|$2>"));
            Assert.Equal(1, regex.Count("😀 aa bb\t"u8, 0, Pcre2MatchOptions.None, cancellation.Token));
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.Count("😀 aa bb\t"u8, 0, Pcre2MatchOptions.None, cancellation.Token));
            Assert.Equal("aa", first.GetValueString());
        }
    }

    [Theory]
    [InlineData("(?xx:[ a]+)(?-x:[ ]+)(?x:[ b]+)", "aa bb", true)]
    [InlineData("(?xx:[ a]+)(?-x:[ ]+)(?x:[ b]+)", " aa bb", true)]
    [InlineData("(?xx:[ a]+)(?-x:[ ]+)(?x:[ b]+)", "aa\tbb", false)]
    [InlineData("(?xx:[ a])(?x:[ ])", "a ", true)]
    [InlineData("(?xx:[ a])(?x:[ ])", "  ", false)]
    public void EnabledAndDisabledScopesDoNotLeak(string pattern, string text, bool expected)
    {
        Assert.Equal(expected, new Utf8Pcre2Regex(pattern).IsMatch(Encoding.UTF8.GetBytes(text)));
    }
}
