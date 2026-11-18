using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BranchResetCompilerTests
{
    [Theory]
    [InlineData("(?|(a)|(b))", "b", true, 2)]
    [InlineData("(?|(a)(b)|(c))", "c", true, 3)]
    [InlineData("(?|(?<x>a)|(?<x>b))\\k<x>", "bb", true, 2)]
    [InlineData("(?|a|b)", "a", true, 1)]
    [InlineData("(?|)", "", true, 1)]
    [InlineData("(?=(?|(a)|(b)))a", "a", true, 2)]
    [InlineData("(?|(?|(a)|(b))|(c))\\1", "cc", true, 2)]
    [InlineData("(a)|(b)", "b", false, 3)]
    [InlineData(@"\(\?\|a\)", "(?|a)", false, 1)]
    [InlineData(@"\Q(?|a)\E", "(?|a)", false, 1)]
    [InlineData("[?|()]", "|", false, 1)]
    [InlineData("(?# (?| )a", "a", false, 1)]
    [InlineData("(?x)a # (?| ignored\n", "a", false, 1)]
    public void AnalysisUsesParsedBranchResetSyntax(
        string pattern, string input, bool usesBranchReset, int captureSlotCount)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var regex in new[] { new Utf8Pcre2Regex(pattern), new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern))})
        {
            Assert.Equal(usesBranchReset, regex.Analyze().UsesBranchReset);
            var match = regex.MatchDetailed(bytes);
            Assert.True(match.Success);
            Assert.Equal(captureSlotCount, match.CaptureSlotCount);
        }
    }

    [Theory]
    [InlineData("(?|(abc)|(xyz))\\1", "abcabc", "abc")]
    [InlineData("(?|(abc)|(xyz))\\1", "xyzxyz", "xyz")]
    [InlineData("(?|(?|(a)|(b))|(c))\\1", "cc", "c")]
    public void BranchResetAlternativesShareNumericSlots(
        string pattern,
        string input,
        string capture)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString(match.GetGroup(1).GetValueBytes()));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void FollowupNumberingUsesTheWidestBranch()
    {
        var regex = new Utf8Pcre2Regex("(x)(?|(a)(b)|(c))(d)");

        var shortBranch = regex.MatchDetailed("xcd"u8);
        var wideBranch = regex.MatchDetailed("xabd"u8);

        Assert.True(shortBranch.Success);
        Assert.Equal(5, shortBranch.CaptureSlotCount);
        Assert.Equal("c", System.Text.Encoding.UTF8.GetString(shortBranch.GetGroup(2).GetValueBytes()));
        Assert.False(shortBranch.GetGroup(3).Success);
        Assert.Equal("d", System.Text.Encoding.UTF8.GetString(shortBranch.GetGroup(4).GetValueBytes()));

        Assert.True(wideBranch.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(wideBranch.GetGroup(2).GetValueBytes()));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(wideBranch.GetGroup(3).GetValueBytes()));
        Assert.Equal("d", System.Text.Encoding.UTF8.GetString(wideBranch.GetGroup(4).GetValueBytes()));
    }

    [Fact]
    public void SameNameAndNumberProducesOneNameTableEntryWithoutDuplicateNameMode()
    {
        var regex = new Utf8Pcre2Regex("(?|(?<x>a)|(?<x>b))\\k<x>");
        var match = regex.MatchDetailed("bb"u8);
        var entries = new Pcre2NameEntry[1];

        Assert.True(match.Success);
        Assert.Equal(1, regex.CopyNameEntries(entries, out var isMore));
        Assert.False(isMore);
        Assert.Equal("x", entries[0].Name);
        Assert.Equal(1, entries[0].Number);
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(match.GetGroup(1).GetValueBytes()));
    }

    [Fact]
    public void DuplicateNamedBackreferenceSelectsTheFirstSetSlot()
    {
        var regex = CreateDuplicateNameRegex();

        var foo = regex.MatchDetailed("foofoo"u8);
        var bar = regex.MatchDetailed("barbar"u8);
        Span<int> numbers = stackalloc int[2];

        Assert.True(foo.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(foo.GetGroup(1).GetValueBytes()));
        Assert.False(foo.GetGroup(2).Success);
        Assert.True(bar.Success);
        Assert.False(bar.GetGroup(1).Success);
        Assert.Equal("bar", System.Text.Encoding.UTF8.GetString(bar.GetGroup(2).GetValueBytes()));
        Assert.True(bar.TryGetFirstSetGroup("n", out var selected));
        Assert.Equal("bar", System.Text.Encoding.UTF8.GetString(selected.GetValueBytes()));
        Assert.Equal(2, regex.CopyNumbersForName("n", numbers, out var isMore));
        Assert.False(isMore);
        Assert.Equal([1, 2], numbers.ToArray());
    }

    [Fact]
    public void DuplicateNamesWorkAcrossGlobalAndReplacementOperations()
    {
        var regex = CreateDuplicateNameRegex();
        var input = "foofoo barbar"u8;

        Assert.Equal(2, regex.Count(input));
        Assert.Equal("<foo> <bar>", Encoding.UTF8.GetString(regex.Replace(input, "<$n>")));
    }

    [Fact]
    public void InlineDuplicateNameOptionEnablesTheSameGenericSlotSet()
    {
        var regex = new Utf8Pcre2Regex("(?J)(?:(?<n>foo)|(?<n>bar))\\k<n>");

        Assert.True(regex.IsMatch("barbar"u8));
        Assert.Equal(2, regex.NameEntryCount);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void DifferentNamesForOneBranchResetNumberAreRejected()
    {
        var error = Assert.Throws<Pcre2CompileException>(
            () => new Utf8Pcre2Regex("(?|(?<x>a)|(?<y>b))"));

        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, error.ErrorKind);
    }

    private static Utf8Pcre2Regex CreateDuplicateNameRegex() => new(        "(?:(?<n>foo)|(?<n>bar))\\k<n>", options:         Pcre2CompileOptions.None, compileSettings:         new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }, policy: new Utf8RegexPolicy
    {
        MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
    });
}
