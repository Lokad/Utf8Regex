using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BranchResetCompilerTests
{
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
        Assert.Equal(capture, match.GetGroup(1).GetValueString());
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
        Assert.Equal("c", shortBranch.GetGroup(2).GetValueString());
        Assert.False(shortBranch.GetGroup(3).Success);
        Assert.Equal("d", shortBranch.GetGroup(4).GetValueString());

        Assert.True(wideBranch.Success);
        Assert.Equal("a", wideBranch.GetGroup(2).GetValueString());
        Assert.Equal("b", wideBranch.GetGroup(3).GetValueString());
        Assert.Equal("d", wideBranch.GetGroup(4).GetValueString());
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
        Assert.Equal("b", match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void DuplicateNamedBackreferenceSelectsTheFirstSetSlot()
    {
        var regex = CreateDuplicateNameRegex();

        var foo = regex.MatchDetailed("foofoo"u8);
        var bar = regex.MatchDetailed("barbar"u8);
        Span<int> numbers = stackalloc int[2];

        Assert.True(foo.Success);
        Assert.Equal("foo", foo.GetGroup(1).GetValueString());
        Assert.False(foo.GetGroup(2).Success);
        Assert.True(bar.Success);
        Assert.False(bar.GetGroup(1).Success);
        Assert.Equal("bar", bar.GetGroup(2).GetValueString());
        Assert.True(bar.TryGetFirstSetGroup("n", out var selected));
        Assert.Equal("bar", selected.GetValueString());
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

        Assert.Equal(Pcre2ErrorKinds.InvalidAfterParensQuery, error.ErrorKind);
    }

    private static Utf8Pcre2Regex CreateDuplicateNameRegex() => new(
        "(?:(?<n>foo)|(?<n>bar))\\k<n>",
        Pcre2CompileOptions.None,
        new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
        default,
        System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
}
