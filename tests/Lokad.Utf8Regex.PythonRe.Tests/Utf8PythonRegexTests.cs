using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class Utf8PythonRegexTests
{
    [Fact]
    public void ExactLiteralUsesUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.Equal("foo", regex.DebugTranslatedPattern);
        Assert.Equal("Search=Utf8Regex, Match=Utf8Regex, FullMatch=Utf8Regex, Count=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));
    }

    [Fact]
    public void Utf8PatternConstructorDecodesUtf8Pattern()
    {
        var regex = new Utf8PythonRegex("café"u8);

        Assert.True(regex.Search("xxcaféyy"u8).Success);
    }

    [Fact]
    public void NamedCapturingGroupTranslatesToDotNetSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)");

        Assert.Equal(@"(?<word>foo)", regex.DebugTranslatedPattern);
        Assert.Contains("word", regex.GetGroupNames(), StringComparer.Ordinal);
        Assert.True(regex.IsMatch("foo"u8));
    }

    [Fact]
    public void NamedBackreferenceTranslatesToDotNetSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        Assert.Equal(@"(?<word>foo)-\k<word>", regex.DebugTranslatedPattern);
        Assert.True(regex.IsMatch("foo-foo"u8));
        Assert.False(regex.IsMatch("foo-bar"u8));
    }

    [Fact]
    public void FixedWidthLookbehindIsAccepted()
    {
        var regex = new Utf8PythonRegex(@"(?<=ab)c");

        Assert.True(regex.IsMatch("abc"u8));
        Assert.False(regex.IsMatch("axc"u8));
    }

    [Fact]
    public void VariableWidthLookbehindIsRejected()
    {
        var ex = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(@"(?<=a+)b"));

        Assert.Contains("fixed-width", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopedInlineFlagsWork()
    {
        var regex = new Utf8PythonRegex(@"(?i:foo)");

        Assert.Equal(@"(?i:foo)", regex.DebugTranslatedPattern);
        Assert.True(regex.IsMatch("FOO"u8));
    }

    [Fact]
    public void LeadingInlineFlagsAreApplied()
    {
        var regex = new Utf8PythonRegex(@"(?im)^foo$");

        Assert.True(regex.IsMatch("bar\nFOO"u8));
    }

    [Fact]
    public void DotAllCanUseUtf8RegexBackend()
    {
        var dotAll = new Utf8PythonRegex(@"a.b", PythonReCompileOptions.DotAll);

        Assert.True(dotAll.DebugUsesUtf8RegexBackend);
        Assert.True(dotAll.Search("a\nb"u8).Success);
    }

    [Fact]
    public void MultilineCanUseUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex(@"(?m)^foo$", PythonReCompileOptions.Multiline);

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.Equal("Search=Utf8Regex, Match=Utf8Regex, FullMatch=Utf8Regex, Count=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.Search("bar\nfoo\nbaz"u8).Success);
    }

    [Fact]
    public void VerbosePatternCanUseUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex(
            """
            (?x)
            foo \s+ bar
            """);

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.Search("xxfoo   baryy"u8).Success);
    }

    [Fact]
    public void PossessiveQuantifierAndAtomicGroupArePreserved()
    {
        var possessive = new Utf8PythonRegex(@"a++b");
        var atomic = new Utf8PythonRegex(@"(?>a+)b");

        Assert.Equal(@"(?>a+)b", possessive.DebugTranslatedPattern);
        Assert.Equal(@"(?>a+)b", atomic.DebugTranslatedPattern);
        Assert.True(possessive.IsMatch("aaab"u8));
        Assert.True(atomic.IsMatch("aaab"u8));
    }

    [Fact]
    public void BackreferencesCanUseUtf8RegexBackend()
    {
        var named = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");
        var numeric = new Utf8PythonRegex(@"(ab)\1");

        Assert.True(named.DebugUsesUtf8RegexBackend);
        Assert.True(numeric.DebugUsesUtf8RegexBackend);
        Assert.True(named.Search("xx foo-foo yy"u8).Success);
        Assert.True(numeric.Search("zzababzz"u8).Success);
    }

    [Fact]
    public void AtomicAndPossessiveFormsCanUseUtf8RegexBackend()
    {
        var atomic = new Utf8PythonRegex(@"(?>ab)c");
        var possessive = new Utf8PythonRegex(@"a++b");

        Assert.True(atomic.DebugUsesUtf8RegexBackend);
        Assert.True(possessive.DebugUsesUtf8RegexBackend);
        Assert.True(atomic.Search("xxabcyy"u8).Success);
        Assert.True(possessive.Search("xxaaabyy"u8).Success);
    }

    [Fact]
    public void MatchAndFullMatchCanUseUtf8RegexBackend()
    {
        var matchRegex = new Utf8PythonRegex(@"foo");
        var fullRegex = new Utf8PythonRegex(@"a|ab");

        Assert.True(matchRegex.Match("foo xx"u8).Success);
        Assert.False(matchRegex.Match("xxfoo"u8).Success);
        Assert.True(fullRegex.FullMatch("ab"u8).Success);
        Assert.Equal("ab", fullRegex.FullMatch("ab"u8).GetValueString());
    }

    [Fact]
    public void ConditionalAndLookaroundCanUseUtf8RegexBackendWhenTranslatedPatternCompiles()
    {
        var conditional = new Utf8PythonRegex(@"(foo)?(?(1)bar|baz)");
        var lookahead = new Utf8PythonRegex(@"foo(?=bar)");

        Assert.True(conditional.DebugUsesUtf8RegexBackend);
        Assert.True(lookahead.DebugUsesUtf8RegexBackend);
        Assert.True(conditional.Search("foobar"u8).Success);
        Assert.True(lookahead.Search("foobar"u8).Success);
    }

    [Fact]
    public void MatchReturnsByteAlignedUtf8ValueMatch()
    {
        var regex = new Utf8PythonRegex("café");

        var match = regex.Search("xxcaféyy"u8);

        Assert.True(match.Success);
        Assert.True(match.HasContiguousByteRange);
        Assert.Equal(2, match.StartOffsetInUtf16);
        Assert.Equal(6, match.EndOffsetInUtf16);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(2 + "café"u8.Length, match.EndOffsetInBytes);
    }

    [Fact]
    public void PrefixMatchUsesPythonMatchSemantics()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.False(regex.Match("xxfoo"u8).Success);
        Assert.True(regex.Match("foo xx"u8).Success);
    }

    [Fact]
    public void FullMatchRequiresWholeRemainingInput()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.FullMatch("foo"u8).Success);
        Assert.False(regex.FullMatch("foo xx"u8).Success);
    }

    [Fact]
    public void SearchDetailedExposesNamedCapture()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var match = regex.SearchDetailed("xx foo-foo yy"u8);

        Assert.True(match.Success);
        Assert.True(match.TryGetFirstSetGroup("word", out var group));
        Assert.True(group.Success);
        Assert.Equal("foo", group.Value.GetValueString());
    }

    [Fact]
    public void DetailedApisRemainCorrectWithUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var search = regex.SearchDetailed("xx foo-foo yy"u8);
        var match = regex.MatchDetailed("foo-foo yy"u8);
        var full = regex.FullMatchDetailed("foo-foo"u8);

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(search.Success);
        Assert.True(match.Success);
        Assert.True(full.Success);
        Assert.Equal("foo-foo", full.Value.GetValueString());
        Assert.True(search.TryGetFirstSetGroup("word", out var word));
        Assert.True(word.Success);
        Assert.Equal("foo", word.Value.GetValueString());
    }

    [Fact]
    public void Utf8BackendDoesNotInventByteRangesForNonAlignedMatches()
    {
        var regex = new Utf8PythonRegex(".");

        var search = regex.Search("𝒜"u8);
        var detailed = regex.SearchDetailed("𝒜"u8);

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(search.Success);
        Assert.False(search.HasContiguousByteRange);
        Assert.ThrowsAny<InvalidOperationException>(static () => ThrowForNonContiguousMatchBytes());

        Assert.True(detailed.Success);
        Assert.True(detailed.TryGetGroup(0, out var group));
        Assert.True(group.Success);
        Assert.False(group.Value.HasContiguousByteRange);
        Assert.ThrowsAny<InvalidOperationException>(static () => ThrowForNonContiguousGroupBytes());

        static void ThrowForNonContiguousMatchBytes()
        {
            var localRegex = new Utf8PythonRegex(".");
            var localSearch = localRegex.Search("𝒜"u8);
            _ = localSearch.GetValueBytes();
        }

        static void ThrowForNonContiguousGroupBytes()
        {
            var localRegex = new Utf8PythonRegex(".");
            var localDetailed = localRegex.SearchDetailed("𝒜"u8);
            _ = localDetailed.TryGetGroup(0, out var localGroup);
            _ = localGroup.Value.GetValueBytes();
        }
    }

    [Fact]
    public void StringFirstAccessorsWorkForNonContiguousUnicodeMatches()
    {
        var regex = new Utf8PythonRegex(".");

        var search = regex.Search("𝒜"u8);
        var detailed = regex.SearchDetailed("𝒜"u8);
        var detailedData = regex.SearchDetailedData("𝒜"u8);

        Assert.Equal("𝒜", search.GetValueString());
        Assert.Equal("𝒜", detailed.GetValueString());
        Assert.True(detailed.TryGetGroupValueString(0, out var whole));
        Assert.Equal("𝒜", whole);
        Assert.Equal("𝒜", detailedData.Value.ValueText);
        Assert.True(detailedData.TryGetGroup(0, out var dataGroup));
        Assert.Equal("𝒜", dataGroup.ValueText);
    }

    [Fact]
    public void FindAllReturnsAllNonOverlappingMatches()
    {
        var regex = new Utf8PythonRegex("foo");

        var matches = regex.FindAll("foo x foo"u8);

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugFindAllBackend);
        Assert.Equal(2, matches.Length);
        Assert.Equal("foo", matches[0].ValueText);
        Assert.Equal("foo", matches[1].ValueText);
    }

    [Fact]
    public void FindAllCanUseUtf8RegexBackendForNonEmptyTranslatedPatterns()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var matches = regex.FindAll("xx foo-foo yy foo-foo"u8, startOffsetInBytes: 3);

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugFindAllBackend);
        Assert.Equal(2, matches.Length);
        Assert.Equal("foo-foo", matches[0].ValueText);
        Assert.Equal("foo-foo", matches[1].ValueText);
        Assert.Equal(3, matches[0].StartOffsetInBytes);
    }

    [Fact]
    public void FindAllCanUseUtf8RegexBackendForPrefixFreeLiteralAlternation()
    {
        var regex = new Utf8PythonRegex(@"foo|bar");

        var matches = regex.FindAll("xxfoobaryybar"u8);

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugFindAllBackend);
        Assert.Equal(["foo", "bar", "bar"], matches.Select(x => x.ValueText).ToArray());
    }

    [Fact]
    public void FindAllToStringsUsesFullMatchShapeWithoutCaptures()
    {
        var regex = new Utf8PythonRegex("a");

        var result = regex.FindAllToStrings("axa"u8);

        Assert.Equal(Utf8PythonFindAllShape.FullMatch, result.Shape);
        Assert.Equal(["a", "a"], result.ScalarValues);
        Assert.Empty(result.TupleValues);
    }

    [Fact]
    public void FindAllToStringsUsesSingleGroupShapeWithEmptyStringForUnsetOptionalGroups()
    {
        var regex = new Utf8PythonRegex("(a)?b");

        var result = regex.FindAllToStrings("b ab b"u8);

        Assert.Equal(Utf8PythonFindAllShape.SingleGroup, result.Shape);
        Assert.Equal(["", "a", ""], result.ScalarValues);
        Assert.Empty(result.TupleValues);
    }

    [Fact]
    public void FindAllToStringsUsesTupleShapeForMultipleCaptures()
    {
        var regex = new Utf8PythonRegex("(a)|(x)");

        var result = regex.FindAllToStrings("axa"u8);

        Assert.Equal(Utf8PythonFindAllShape.GroupTuple, result.Shape);
        Assert.Empty(result.ScalarValues);
        Assert.Equal(3, result.TupleValues.Length);
        Assert.Equal(["a", ""], result.TupleValues[0]);
        Assert.Equal(["", "x"], result.TupleValues[1]);
        Assert.Equal(["a", ""], result.TupleValues[2]);
    }

    [Fact]
    public void FindAllToUtf8UsesTupleShapeForMultipleCaptures()
    {
        var regex = new Utf8PythonRegex("(a)|(x)");

        var result = regex.FindAllToUtf8("axa"u8);

        Assert.Equal(Utf8PythonFindAllShape.GroupTuple, result.Shape);
        Assert.Empty(result.ScalarValues);
        Assert.Equal(3, result.TupleValues.Length);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(result.TupleValues[0][0]));
        Assert.Equal("", System.Text.Encoding.UTF8.GetString(result.TupleValues[0][1]));
        Assert.Equal("", System.Text.Encoding.UTF8.GetString(result.TupleValues[1][0]));
        Assert.Equal("x", System.Text.Encoding.UTF8.GetString(result.TupleValues[1][1]));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(result.TupleValues[2][0]));
    }

    [Fact]
    public void FindIterDetailedReturnsHostFriendlySnapshots()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var matches = regex.FindIterDetailed("xx foo-foo yy foo-foo"u8);

        Assert.Equal(2, matches.Length);
        Assert.Equal("foo-foo", matches[0].Value.ValueText);
        Assert.True(matches[0].TryGetFirstSetGroup("word", out var firstWord));
        Assert.Equal("foo", firstWord.ValueText);
        Assert.Equal("foo-foo", matches[1].Value.ValueText);
    }

    [Fact]
    public void ReplaceUsesPythonGroupSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)");

        var replaced = regex.ReplaceToString("foo x foo"u8, "<\\g<word>>");

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugReplaceBackend);
        Assert.Equal("<foo> x <foo>", replaced);
    }

    [Fact]
    public void SubnCanUseUtf8RegexBackendForUnlimitedReplacement()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = regex.SubnToString("xx foo-foo yy foo-foo"u8, "<\\g<word>>", startOffsetInBytes: 3);

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugReplaceBackend);
        Assert.Equal("xx <foo> yy <foo>", replaced.ResultText);
        Assert.Equal(2, replaced.ReplacementCount);
    }

    [Fact]
    public void SubnReturnsUtf8ResultBytesAndCount()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = regex.Subn("xx foo-foo yy foo-foo"u8, "<\\g<word>>", startOffsetInBytes: 3);

        Assert.Equal("xx <foo> yy <foo>", System.Text.Encoding.UTF8.GetString(replaced.ResultBytes));
        Assert.Equal(2, replaced.ReplacementCount);
    }

    [Fact]
    public void ReplaceCanUseUtf8RegexBackendForAlternationPatterns()
    {
        var regex = new Utf8PythonRegex(@"(?:b)|(?::+)");

        var replaced = regex.ReplaceToString(":a:b::c"u8, "-");

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugReplaceBackend);
        Assert.Equal("-a---c", replaced);
    }

    [Fact]
    public void CallableReplacementCanUseDetailedHostSnapshot()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = regex.ReplaceToString(
            "xx foo-foo yy foo-foo"u8,
            "<",
            static (prefix, match) =>
            {
                Assert.True(match.TryGetFirstSetGroup("word", out var word));
                return prefix + word.ValueText + ">";
            },
            startOffsetInBytes: 3);

        Assert.Equal("xx <foo> yy <foo>", replaced);
    }

    [Fact]
    public void CallableSubnHonorsReplacementCount()
    {
        var regex = new Utf8PythonRegex("foo");

        var result = regex.SubnToString("foo foo foo"u8, "-", static (prefix, match) => prefix + match.Value.ValueText, count: 2);

        Assert.Equal("-foo -foo foo", result.ResultText);
        Assert.Equal(2, result.ReplacementCount);
    }

    [Fact]
    public void CallableSubnReturnsUtf8ResultBytesAndCount()
    {
        var regex = new Utf8PythonRegex("foo");

        var result = regex.Subn("foo foo"u8, "-", static (prefix, match) => prefix + match.Value.ValueText);

        Assert.Equal("-foo -foo", System.Text.Encoding.UTF8.GetString(result.ResultBytes));
        Assert.Equal(2, result.ReplacementCount);
    }

    [Fact]
    public void CallableUtf8SubnReturnsUtf8ResultBytesAndCount()
    {
        var regex = new Utf8PythonRegex("foo");
        Utf8PythonUtf8MatchEvaluator<string> evaluator = static (prefix, match) =>
            System.Text.Encoding.UTF8.GetBytes(prefix + match.Value.ValueText);

        var result = regex.Subn("foo foo"u8, "-", evaluator);

        Assert.Equal("-foo -foo", System.Text.Encoding.UTF8.GetString(result.ResultBytes));
        Assert.Equal(2, result.ReplacementCount);
    }

    [Fact]
    public void ReplacementPreservesBackslashForNonLetterEscapes()
    {
        var regex = new Utf8PythonRegex("x");

        Assert.Equal(@"\&", regex.ReplaceToString("x"u8, @"\&"));
        Assert.Equal(@"\-", regex.ReplaceToString("x"u8, @"\-"));
        Assert.Equal("\\ ", regex.ReplaceToString("x"u8, "\\ "));
    }

    [Fact]
    public void ReplacementRejectsTrailingBackslash()
    {
        var regex = new Utf8PythonRegex("x");

        var ex = Assert.Throws<PythonRePatternException>(() => regex.ReplaceToString("x"u8, "\\"));

        Assert.Contains("bad escape (end of pattern)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SplitDetailedPreservesSegmentAndCaptureShape()
    {
        var regex = new Utf8PythonRegex(@"(:+)");

        var parts = regex.SplitDetailed(":a:b::c"u8);

        Assert.Equal(7, parts.Length);
        Assert.False(parts[0].IsCapture);
        Assert.Equal("", parts[0].ValueText);
        Assert.True(parts[1].IsCapture);
        Assert.Equal(1, parts[1].CaptureGroupNumber);
        Assert.Equal(":", parts[1].ValueText);
        Assert.False(parts[2].IsCapture);
        Assert.Equal("a", parts[2].ValueText);
        Assert.Equal("::", parts[5].ValueText);
    }

    [Fact]
    public void ScalarSearchHelpersProvideHostFriendlySurface()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.Equal("foo", regex.SearchToString("xxfooyy"u8));
        Assert.Equal("foo", regex.MatchToString("fooyy"u8));
        Assert.Equal("foo", regex.FullMatchToString("foo"u8));
        Assert.Null(regex.MatchToString("xxfoo"u8));
    }

    [Fact]
    public void VerboseModeIgnoresWhitespaceAndComments()
    {
        var regex = new Utf8PythonRegex(
            """
            (?x)
            foo   # first token
            \s+
            bar
            """);

        Assert.True(regex.Search("xxfoo   bar yy"u8).Success);
    }

    [Fact]
    public void InlineCommentGroupIsIgnored()
    {
        var regex = new Utf8PythonRegex(@"foo(?# ignore me)bar");

        Assert.True(regex.Search("xxfoobar"u8).Success);
    }

    [Fact]
    public void ConditionalGroupReferenceIsSupported()
    {
        var regex = new Utf8PythonRegex(@"(foo)?(?(1)bar|baz)");

        Assert.True(regex.FullMatch("foobar"u8).Success);
        Assert.True(regex.FullMatch("baz"u8).Success);
        Assert.False(regex.FullMatch("bar"u8).Success);
    }

    [Fact]
    public void ConditionalNamedReferenceIsSupported()
    {
        var regex = new Utf8PythonRegex(@"(?P<seen>foo)?(?(seen)bar|baz)");

        Assert.True(regex.FullMatch("foobar"u8).Success);
        Assert.True(regex.FullMatch("baz"u8).Success);
    }

    [Fact]
    public void SplitCanUseUtf8RegexBackendForNonEmptyTranslatedPatterns()
    {
        var regex = new Utf8PythonRegex(@"(:+)");

        var parts = regex.SplitToStrings(":a:b::c"u8);

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugSplitBackend);
        Assert.Equal<string?[]>(["", ":", "a", ":", "b", "::", "c"], parts);
    }

    [Fact]
    public void SplitCanUseUtf8RegexBackendForPrefixFreeLiteralAlternation()
    {
        var regex = new Utf8PythonRegex(@"foo|bar");

        var parts = regex.SplitToStrings("xxfoobaryybar"u8);

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugSplitBackend);
        Assert.Equal<string?[]>(["xx", "", "yy", ""], parts);
    }
}
