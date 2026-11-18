using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
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
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("a{2,4}a", "aaaaa", "aaaaa")]
    [InlineData("(?:ab)?b", "abb", "abb")]
    public void OrdinaryQuantifiersComposeAndBacktrack(
        string pattern,
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    // R03 transparent groups: quantified transparent shapes run first-match Match on the shared slice runner with identical PCRE2-ordered results.
    [Theory]
    [InlineData("(?:ab){2,}c", "xabababc", "abababc")]
    [InlineData("(?:ab)??b", "babb", "b")]
    public void TransparentQuantifiersComposeOnSlice(
        string pattern,
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    // R03 dot slice: greedy/lazy dot-star quantifier shapes run first-match
    // Match on the shared slice runner with identical PCRE2-ordered results.
    [Theory]
    [InlineData("a.*b", "a1b2b", "a1b2b")]
    [InlineData("a.*?b", "a1b2b", "a1b")]
    public void DotQuantifiersComposeOnSlice(
        string pattern,
        string inputText,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
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
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Fact]
    public void UnicodeAtomsRetainExactByteCoordinatesInsideRepetition()
    {
        var regex = new Utf8Pcre2Regex("(?:é|😀){2,3}?z");
        var input = Encoding.UTF8.GetBytes("xé😀zq");

        var match = regex.Match(input);

        Assert.True(match.Success);
        Assert.Equal("é😀z", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(1, match.StartOffsetInBytes);
        Assert.Equal(8, match.EndOffsetInBytes);
    }

    [Fact]
    public void GlobalOperationsUseTheGenericBacktrackingCursor()
    {
        var regex = new Utf8Pcre2Regex("(?:cat|horse)+");
        var input = "cat horsehorse cat"u8;

        Assert.Equal(3, regex.Count(input));
        Assert.Equal("<cat> <horsehorse> <cat>", Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));

        var matches = regex.EnumerateMatches(input);
        Assert.True(matches.MoveNext());
        Assert.Equal(0, matches.Current.StartOffsetInBytes);
        Assert.True(matches.MoveNext());
        Assert.Equal(4, matches.Current.StartOffsetInBytes);
        Assert.True(matches.MoveNext());
        Assert.Equal(15, matches.Current.StartOffsetInBytes);
        Assert.False(matches.MoveNext());
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
        var ungreedy = new Utf8Pcre2Regex("a+?", options: Pcre2CompileOptions.Ungreedy);
        var noAutoCapture = new Utf8Pcre2Regex("(a|ab)c", options: Pcre2CompileOptions.NoAutoCapture);

        Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString(ungreedy.Match("aaa"u8).GetValueBytes()));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(noAutoCapture.Match("abc"u8).GetValueBytes()));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(noAutoCapture.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void BacktrackingChargesMatchDepthAndHeapLimits()
    {
        var matchLimited = new Utf8Pcre2Regex(            "a*a*a*z", options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 20
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });
        var depthLimited = new Utf8Pcre2Regex(            "(?:a|b)(?:c|d)z", options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });
        var heapLimited = new Utf8Pcre2Regex(            "a|b", options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1
            },
            MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
        });

        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => matchLimited.IsMatch("aaaaaaaa"u8)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.DepthLimit, Assert.Throws<Pcre2MatchException>(() => depthLimited.IsMatch("acy"u8)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => heapLimited.IsMatch("b"u8)).ErrorKind);
    }

    [Fact]
    public void WarmedOneShotBacktrackingDoesNotAllocate()
    {
        // The transparent shape now rides the slice (which rents slots), so the warmed zero-allocation pin uses the Python-spelled twin that stays on the generic VM.
        var regex = new Utf8Pcre2Regex("(?P<x>(?:ab|a))+z");
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
