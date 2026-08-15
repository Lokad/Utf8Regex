using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CaptureBackreferenceTests
{
    [Fact]
    public void DetailedMatchProjectsNestedCapturesOnceFromUtf8Coordinates()
    {
        var regex = new Utf8Pcre2Regex("(é)(😀)");
        var input = Encoding.UTF8.GetBytes("xé😀y");

        var match = regex.MatchDetailed(input);

        Assert.True(match.Success);
        Assert.Equal(3, match.CaptureSlotCount);
        AssertGroup(match.GetGroup(0), "é😀", 1, 7, 1, 4);
        AssertGroup(match.GetGroup(1), "é", 1, 3, 1, 2);
        AssertGroup(match.GetGroup(2), "😀", 3, 7, 2, 4);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("(a|ab)\\1", "abab", "abab")]
    [InlineData("(a)\\g{1}", "aa", "aa")]
    [InlineData("(a)\\g{-1}", "aa", "aa")]
    [InlineData("(?<x>a)\\k<x>", "aa", "aa")]
    [InlineData("(?'x'a)\\k'x'", "aa", "aa")]
    [InlineData("(?P<x>a)(?P=x)", "aa", "aa")]
    public void NumericRelativeAndNamedBackreferencesUseTheGenericVm(
        string pattern,
        string input,
        string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData("(a|b\\1)+", "aba")]
    [InlineData("(\\2a|(b))+", "bba")]
    [InlineData("(\\g{+1}a|(b))+", "bba")]
    [InlineData("(?<outer>\\k<later>a|(?<later>b))+", "bba")]
    public void RepeatedGroupsExposeTheirLastCompletedValueToPermittedForwardReferences(
        string pattern,
        string input)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.True(match.Success);
        Assert.Equal(input, match.GetValueString());
    }

    [Theory]
    [InlineData("(a)\\11", "a\t")]
    [InlineData("(a)\\40", "a ")]
    [InlineData("(a)\\1134", "aK4")]
    public void AmbiguousLongDigitEscapesFollowPcre2BackreferenceThenOctalRules(
        string pattern,
        string input)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void CaptureJournalRestoresUnsetAndFinalRepeatedCaptures()
    {
        var rollback = new Utf8Pcre2Regex("(?:(a)c|(b))\\2").MatchDetailed("bb"u8);
        var repeated = new Utf8Pcre2Regex("(?<x>a|b)+").MatchDetailed("ab"u8);

        Assert.True(rollback.Success);
        Assert.False(rollback.GetGroup(1).Success);
        AssertGroup(rollback.GetGroup(2), "b", 0, 1, 0, 1);
        Assert.True(repeated.Success);
        AssertGroup(repeated.GetGroup(1), "b", 1, 2, 1, 2);
    }

    [Fact]
    public void UnsetBackreferenceFailsAndCaselessUnicodeBackreferenceMatches()
    {
        Assert.False(new Utf8Pcre2Regex("(a)?b\\1").IsMatch("b"u8));

        var regex = new Utf8Pcre2Regex("(é)\\1", Pcre2CompileOptions.Caseless);
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("Éé")));
    }

    [Fact]
    public void NamedCaptureMetadataComesFromTheGenericCompiler()
    {
        var regex = new Utf8Pcre2Regex("(?P<word>é+)-(?P=word)");
        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes("éé-éé"));
        var entries = new Pcre2NameEntry[1];
        Span<int> numbers = stackalloc int[1];

        Assert.True(match.Success);
        Assert.Equal(1, regex.NameEntryCount);
        Assert.Equal(1, regex.CopyNameEntries(entries, out var hasMoreEntries));
        Assert.False(hasMoreEntries);
        Assert.Equal("word", entries[0].Name);
        Assert.Equal(1, entries[0].Number);
        Assert.Equal(1, match.CopyNumbersForName("word", numbers, out var hasMoreNumbers));
        Assert.False(hasMoreNumbers);
        Assert.Equal(1, numbers[0]);
        Assert.True(match.TryGetFirstSetGroup("word", out var word));
        Assert.Equal("éé", word.GetValueString());
    }

    [Theory]
    [InlineData("名")]
    [InlineData("𐐀2")]
    public void UtfModeCaptureNamesAcceptUnicodeLettersAndDecimalDigits(string name)
    {
        var regex = new Utf8Pcre2Regex($"(?<{name}>é)\\k<{name}>");
        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes("éé"));

        Assert.True(match.Success);
        Assert.True(match.TryGetFirstSetGroup(name, out var capture));
        Assert.Equal("é", capture.GetValueString());
    }

    [Fact]
    public void CaptureReplacementSupportsNumericNamedAndEvaluatorForms()
    {
        var regex = new Utf8Pcre2Regex("(?<left>a)(?<right>b)");
        var input = "ab ab"u8;

        Assert.Equal("b-a b-a", Encoding.UTF8.GetString(regex.Replace(input, "${right}-$1")));

        var evaluatorResult = regex.Replace(
            input,
            0,
            static (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) =>
            {
                writer.Append(match.GetGroup(2).GetValueBytes());
                writer.Append("/"u8);
                writer.Append(match.GetGroup(1).GetValueBytes());
                state++;
            });
        Assert.Equal("b/a b/a", Encoding.UTF8.GetString(evaluatorResult));
    }

    [Fact]
    public void GlobalCapturePatternUsesGroupZeroCursorWithoutMaterializingCaptures()
    {
        var regex = new Utf8Pcre2Regex("(a|ab)\\1");
        var input = "aa abab aa"u8;

        Assert.Equal(3, regex.Count(input));
        Assert.Equal("<aa> <abab> <aa>", Encoding.UTF8.GetString(regex.Replace(input, "<$0>")));
    }

    [Fact]
    public void WarmedBackreferenceIsMatchReusesCaptureState()
    {
        var regex = new Utf8Pcre2Regex("(ab|a)+\\1z");
        var input = "abababz"u8;
        for (var index = 0; index < 32; index++)
        {
            _ = regex.IsMatch(input);
        }

        for (var index = 0; index < 256; index++)
        {
            _ = regex.IsMatch(input);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 256; index++)
        {
            _ = regex.IsMatch(input);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void AssertGroup(
        Utf8Pcre2GroupContext group,
        string value,
        int startInBytes,
        int endInBytes,
        int startInUtf16,
        int endInUtf16)
    {
        Assert.True(group.Success);
        Assert.Equal(value, group.GetValueString());
        Assert.Equal(startInBytes, group.StartOffsetInBytes);
        Assert.Equal(endInBytes, group.EndOffsetInBytes);
        Assert.Equal(startInUtf16, group.StartOffsetInUtf16);
        Assert.Equal(endInUtf16, group.EndOffsetInUtf16);
    }
}
