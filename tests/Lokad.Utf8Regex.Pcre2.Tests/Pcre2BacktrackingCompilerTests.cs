using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BacktrackingCompilerTests
{
    [Theory]
    [InlineData("cat|horse", "xxhorse cat", "horse")]
    [InlineData("(?:a|ab)c", "zabc", "abc")]
    [InlineData("(?:ab|a)b", "zabb", "abb")]
    [InlineData("(?:a|(?:bc|b))d", "xbdy", "bd")]
    public void AlternationAndNoncapturingGroupsBacktrackInPcre2Order(
        string pattern,
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal(Pcre2SyntaxNodeKind.BacktrackingProgram, regex.DebugCompiledProgram.SyntaxTree.RootKind);
    }

    [Theory]
    [InlineData("a.*b", "a1b2b", "a1b2b")]
    [InlineData("a.*?b", "a1b2b", "a1b")]
    [InlineData("a{2,4}a", "aaaaa", "aaaaa")]
    [InlineData("a{2,4}?a", "aaaaa", "aaa")]
    [InlineData("(?:ab){2,}c", "xabababc", "abababc")]
    [InlineData("(?:ab)?b", "abb", "abb")]
    [InlineData("(?:ab)??b", "babb", "b")]
    public void OrdinaryQuantifiersComposeAndBacktrack(
        string pattern,
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("(?:|a)*b", "aaab", "aaab")]
    [InlineData("(?:)*", "abc", "")]
    [InlineData("(?:a|)*?b", "aaab", "aaab")]
    public void EmptyBranchesAndBodiesTerminate(string pattern, string inputText, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
    }

    [Fact]
    public void UnicodeAtomsRetainExactByteCoordinatesInsideRepetition()
    {
        var regex = new Utf8Pcre2Regex("(?:é|😀){2,3}?z");
        var input = Encoding.UTF8.GetBytes("xé😀zq");

        var match = regex.Match(input);

        Assert.True(match.Success);
        Assert.Equal("é😀z", match.GetValueString());
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(8, match.EndOffsetInBytes);
        Assert.Equal(1, match.StartOffsetInUtf16);
        Assert.Equal(5, match.EndOffsetInUtf16);
    }

    [Fact]
    public void GlobalOperationsUseTheGenericBacktrackingCursor()
    {
        var regex = new Utf8Pcre2Regex("(?:cat|horse)+");
        var input = "cat horsehorse cat"u8;

        Assert.Equal(3, regex.Count(input));
        Assert.Equal("<cat> <horsehorse> <cat>", Encoding.UTF8.GetString(regex.Replace(input, "<$0>")));

        Span<Utf8Pcre2MatchData> matches = stackalloc Utf8Pcre2MatchData[3];
        Assert.Equal(3, regex.MatchMany(input, matches, out var isMore));
        Assert.False(isMore);
        Assert.Equal(0, matches[0].StartOffsetInBytes);
        Assert.Equal(4, matches[1].StartOffsetInBytes);
        Assert.Equal(15, matches[2].StartOffsetInBytes);
    }

    [Fact]
    public void EmptyGlobalProgressFollowsTheExistingPcre2CursorRule()
    {
        var regex = new Utf8Pcre2Regex("a*");

        Assert.Equal(2, regex.Count("b"u8));
    }

    [Fact]
    public void UngreedyAndNoAutoCaptureOptionsApplyToComposedPatterns()
    {
        var ungreedy = new Utf8Pcre2Regex("a+?", Pcre2CompileOptions.Ungreedy);
        var noAutoCapture = new Utf8Pcre2Regex("(a|ab)c", Pcre2CompileOptions.NoAutoCapture);

        Assert.Equal("aaa", ungreedy.Match("aaa"u8).GetValueString());
        Assert.Equal("abc", noAutoCapture.Match("abc"u8).GetValueString());
        Assert.IsType<Pcre2BacktrackingDirectProgram>(noAutoCapture.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void BacktrackingChargesMatchDepthAndHeapLimits()
    {
        var matchLimited = new Utf8Pcre2Regex(
            "a*a*a*z",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 20 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var depthLimited = new Utf8Pcre2Regex(
            "(?:a|b)(?:c|d)z",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { DepthLimit = 1 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var heapLimited = new Utf8Pcre2Regex(
            "a|b",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 },
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);

        Assert.Equal("MatchLimit", Assert.Throws<Pcre2MatchException>(() => matchLimited.IsMatch("aaaaaaaa"u8)).ErrorKind);
        Assert.Equal("DepthLimit", Assert.Throws<Pcre2MatchException>(() => depthLimited.IsMatch("acy"u8)).ErrorKind);
        Assert.Equal("HeapLimit", Assert.Throws<Pcre2MatchException>(() => heapLimited.IsMatch("b"u8)).ErrorKind);
    }

    [Fact]
    public void WarmedOneShotBacktrackingDoesNotAllocate()
    {
        var regex = new Utf8Pcre2Regex("(?:ab|a)+z");
        var input = "ababababaz"u8;
        for (var i = 0; i < 32; i++)
        {
            Assert.True(regex.IsMatch(input));
        }

        // Cross the tiered-compilation threshold with a complete batch before
        // taking the steady-state allocation sample. Test ordering must not be
        // what happens to warm this operation in repository runs.
        for (var i = 0; i < 256; i++)
        {
            Assert.True(regex.IsMatch(input));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++)
        {
            Assert.True(regex.IsMatch(input));
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }
}
