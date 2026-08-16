using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class Utf8PythonRegexTests
{
    [Fact]
    public void ExactLiteralUsesUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.False(regex.DebugIsUtf8FullRegexValueCreated);
        Assert.True(regex.DebugHasUtf8FullRegex);
        Assert.True(regex.DebugIsUtf8FullRegexValueCreated);
        Assert.Equal("foo", regex.DebugTranslatedPattern);
        Assert.Equal("Search=Utf8Regex, Match=Utf8Regex, FullMatch=Utf8Regex, Count=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("xxfooyy"u8));
        Assert.Equal(2, regex.Count("foo xx foo"u8));
    }

    [Theory]
    [InlineData("", 0, 0)]
    [InlineData("abc", 0, 2)]
    [InlineData(" abc ", 0, 2)]
    [InlineData("a_b 12", 0, 4)]
    [InlineData("éabc𝒜x", 0, 4)]
    [InlineData("a b", 1, 3)]
    [InlineData("a b", 2, 2)]
    public void AsciiWordBoundaryCountUsesPythonPositions(string input, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8PythonRegex(@"\b", PythonReCompileOptions.Ascii);

        Assert.Equal(expected, regex.Count(System.Text.Encoding.UTF8.GetBytes(input), startOffsetInBytes));
    }

    [Fact]
    public void AsciiWordBoundaryCountDoesNotSpecializeEmbeddedBoundaryPatterns()
    {
        var regex = new Utf8PythonRegex(@"x\b", PythonReCompileOptions.Ascii);

        Assert.Equal(2, regex.Count("x xé"u8));
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
        Assert.Equal("Search=Utf8Regex, Match=Utf8Regex, FullMatch=ManagedRegex, Count=Utf8Regex", regex.DebugDescribeExecutionPlan());
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
        Assert.False(conditional.DebugHasUtf8FullRegex);
        Assert.False(conditional.DebugIsUtf8FullRegexValueCreated);
        Assert.False(lookahead.DebugIsUtf8FullRegexValueCreated);
        Assert.True(lookahead.DebugHasUtf8FullRegex);
        Assert.True(lookahead.DebugIsUtf8FullRegexValueCreated);
        Assert.True(conditional.Search("foobar"u8).Success);
        Assert.True(lookahead.Search("foobar"u8).Success);
        Assert.True(conditional.FullMatch("foobar"u8).Success);
        Assert.True(conditional.FullMatch("baz"u8).Success);
        Assert.False(conditional.FullMatch("foo"u8).Success);
    }

    [Fact]
    public void LazyUtf8FullMatcherSupportsConcurrentFirstUseAndCachesFallback()
    {
        var native = new Utf8PythonRegex("ab[0-9]{2}");
        Assert.False(native.DebugIsUtf8FullRegexValueCreated);

        Parallel.For(0, 32, iteration =>
        {
            switch (iteration % 3)
            {
                case 0:
                    Assert.True(native.FullMatch("ab12"u8).Success);
                    break;
                case 1:
                    Assert.True(native.FullMatchDetailed("ab12"u8).Success);
                    break;
                default:
                    Assert.True(native.FullMatchDetailedData("ab12"u8).Success);
                    break;
            }
        });

        Assert.True(native.DebugIsUtf8FullRegexValueCreated);
        Assert.True(native.DebugHasUtf8FullRegex);

        var managed = new Utf8PythonRegex(@"async\s+Task<");
        Assert.False(managed.DebugIsUtf8FullRegexValueCreated);
        Parallel.For(0, 32, _ => Assert.True(managed.FullMatch("async Task<"u8).Success));
        Assert.True(managed.DebugIsUtf8FullRegexValueCreated);
        Assert.False(managed.DebugHasUtf8FullRegex);
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
    public void FullMatchUsesManagedRegexWhenCoreWouldFallBack()
    {
        var regex = new Utf8PythonRegex("(?:Шерлок )+");
        var input = "xxШерлок Шерлок "u8.ToArray();

        var match = regex.FullMatch(input, startOffsetInBytes: 2);

        Assert.Equal("Search=Utf8Regex, Match=Utf8Regex, FullMatch=ManagedRegex, Count=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(match.Success);
        Assert.True(match.HasContiguousByteRange);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(input.Length, match.EndOffsetInBytes);
        Assert.Equal(2, match.StartOffsetInUtf16);
        Assert.Equal("xxШерлок Шерлок ".Length, match.EndOffsetInUtf16);
        Assert.Equal("Шерлок Шерлок ", match.GetValueString());
        Assert.False(regex.FullMatch("xxШерлок x"u8, startOffsetInBytes: 2).Success);
    }

    [Fact]
    public void ManagedFallbackFullMatchPreservesDetailedCapturesAndInputContracts()
    {
        var regex = new Utf8PythonRegex("(?P<word>Шерлок )+");
        var input = "xxШерлок Шерлок "u8.ToArray();

        var detailed = regex.FullMatchDetailed(input, startOffsetInBytes: 2);
        var data = regex.FullMatchDetailedData(input, startOffsetInBytes: 2);

        Assert.Contains("FullMatch=ManagedRegex", regex.DebugDescribeExecutionPlan());
        Assert.True(detailed.Success);
        Assert.Equal("Шерлок Шерлок ", detailed.Value.GetValueString());
        Assert.True(detailed.TryGetFirstSetGroup("word", out var word));
        Assert.Equal("Шерлок ", word.Value.GetValueString());
        Assert.True(data.Success);
        Assert.Equal("Шерлок Шерлок ", data.Value.ValueText);
        Assert.False(regex.FullMatchDetailedData("xxШерлок x"u8, startOffsetInBytes: 2).Success);
        Assert.Throws<ArgumentException>(() => regex.FullMatch(new byte[] { 0xC3, 0x28 }));
        Assert.Throws<ArgumentException>(() => regex.FullMatchDetailedData(new byte[] { 0xC3, 0x28 }));
    }

    [Fact]
    public void ManagedFallbackFullMatchHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");

        Assert.Contains("FullMatch=ManagedRegex", regex.DebugDescribeExecutionPlan());
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FullMatch(input));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FullMatchDetailedData(input));
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
    public void DirectShapedFindAllProjectsUnicodeAndOwnsReturnedBytes()
    {
        var regex = new Utf8PythonRegex("é+");
        var input = "xéé yé"u8.ToArray();

        var structural = regex.FindAll(input);
        var strings = regex.FindAllToStrings(input);
        var utf8 = regex.FindAllToUtf8(input);

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugFindAllBackend);
        Assert.Equal(["éé", "é"], structural.Select(static match => match.ValueText).ToArray());
        Assert.Equal(["éé", "é"], strings.ScalarValues);
        Assert.Equal(["éé", "é"], utf8.ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());

        input.AsSpan().Clear();
        Assert.Equal(["éé", "é"], utf8.ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void DirectShapedFindAllSupportsConcurrentGrowingRangeBuffers()
    {
        var regex = new Utf8PythonRegex("token");
        var input = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("token ", 2_048)));
        var expectedBytes = "token"u8.ToArray();

        Parallel.For(0, 16, iteration =>
        {
            if ((iteration & 1) == 0)
            {
                var strings = regex.FindAllToStrings(input);
                Assert.Equal(2_048, strings.ScalarValues.Length);
                Assert.All(strings.ScalarValues, static value => Assert.Equal("token", value));
                return;
            }

            var utf8 = regex.FindAllToUtf8(input);
            Assert.Equal(2_048, utf8.ScalarValues.Length);
            Assert.All(utf8.ScalarValues, value => Assert.Equal(expectedBytes, value));
        });
    }

    [Fact]
    public void DirectShapedFindAllHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugFindAllBackend);
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FindAllToStrings(input));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FindAllToUtf8(input));
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
    public void CapturedStringFindAllUsesLastCaptureAndPreservesEmptyThenNonEmptyProgression()
    {
        var repeated = new Utf8PythonRegex("(a)+").FindAllToStrings("aaa aa"u8);
        Assert.Equal(Utf8PythonFindAllShape.SingleGroup, repeated.Shape);
        Assert.Equal(["a", "a"], repeated.ScalarValues);

        var progressive = new Utf8PythonRegex(@"(\b)|(\w+)").FindAllToStrings("a::bc"u8);
        Assert.Equal(Utf8PythonFindAllShape.GroupTuple, progressive.Shape);
        Assert.Equal(
            [
                ["", ""],
                ["", "a"],
                ["", ""],
                ["", ""],
                ["", "bc"],
                ["", ""],
            ],
            progressive.TupleValues);
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
    public void CapturedFindAllProjectsUnicodeUtf8RangesFromOneSubjectMap()
    {
        var regex = new Utf8PythonRegex("(é+)-(𝒜𝒜|𝒜)");
        var input = "xx éé-𝒜𝒜 yy é-𝒜"u8;

        var strings = regex.FindAllToStrings(input);
        var utf8 = regex.FindAllToUtf8(input);

        Assert.Equal(Utf8PythonFindAllShape.GroupTuple, strings.Shape);
        Assert.Equal([["éé", "𝒜𝒜"], ["é", "𝒜"]], strings.TupleValues);
        Assert.Equal(Utf8PythonFindAllShape.GroupTuple, utf8.Shape);
        Assert.Equal(strings.TupleValues, utf8.TupleValues
            .Select(static tuple => tuple.Select(System.Text.Encoding.UTF8.GetString).ToArray())
            .ToArray());
    }

    [Fact]
    public void CapturedStringFindAllHonorsByteStartAfterUnicodePrefixAndTranslatedNames()
    {
        var regex = new Utf8PythonRegex(@"(?P<left>é+)-(𝒜𝒜|𝒜)");
        var input = "π skip é-𝒜 xx éé-𝒜𝒜"u8;
        var startOffsetInBytes = "π skip é-𝒜 xx "u8.Length;

        var result = regex.FindAllToStrings(input, startOffsetInBytes);

        Assert.Equal(Utf8PythonFindAllShape.GroupTuple, result.Shape);
        Assert.Equal([["éé", "𝒜𝒜"]], result.TupleValues);
    }

    [Fact]
    public void ShapedFindAllHonorsByteStartAfterUnicodePrefix()
    {
        var regex = new Utf8PythonRegex("[a-z]+");
        var input = "é ignored alpha beta"u8;
        var startOffsetInBytes = "é ignored "u8.Length;

        var strings = regex.FindAllToStrings(input, startOffsetInBytes);
        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);

        Assert.Equal(["alpha", "beta"], strings.ScalarValues);
        Assert.Equal(
            strings.ScalarValues,
            utf8.ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void ShapedFindAllPreservesEmptyThenNonEmptyProgression()
    {
        var regex = new Utf8PythonRegex(@"\b|\w+");
        string[] expected = ["", "a", "", "", "bc", ""];

        var strings = regex.FindAllToStrings("a::bc"u8);
        var utf8 = regex.FindAllToUtf8("a::bc"u8);

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugFindAllBackend);
        Assert.Equal(expected, strings.ScalarValues);
        Assert.Equal(
            expected,
            utf8.ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void ShapedFindAllPreservesManagedPrefixAlternationOrder()
    {
        var regex = new Utf8PythonRegex("foo|foobar");

        var matches = regex.FindAll("foobar foo"u8);
        var strings = regex.FindAllToStrings("foobar foo"u8);
        var utf8 = regex.FindAllToUtf8("foobar foo"u8);
        var detailed = regex.FindIterDetailed("foobar foo"u8);

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugFindAllBackend);
        Assert.Equal(["foo", "foo"], matches.Select(static match => match.ValueText).ToArray());
        Assert.Equal(["foo", "foo"], strings.ScalarValues);
        Assert.Equal(
            strings.ScalarValues,
            utf8.ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
        Assert.Equal(["foo", "foo"], detailed.Select(static match => match.Value.ValueText).ToArray());
        Assert.Equal(2, regex.Count("foobar foo"u8));
    }

    [Fact]
    public void ManagedGlobalMissesAfterUnicodeStartReturnEmptyShapes()
    {
        var regex = new Utf8PythonRegex("needle|needle-long");
        var input = "é skip haystack"u8;
        var startOffsetInBytes = "é skip "u8.Length;

        var matches = regex.FindAll(input, startOffsetInBytes);
        var strings = regex.FindAllToStrings(input, startOffsetInBytes);
        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);
        var detailed = regex.FindIterDetailed(input, startOffsetInBytes);

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugFindAllBackend);
        Assert.Empty(matches);
        Assert.Empty(strings.ScalarValues);
        Assert.Empty(utf8.ScalarValues);
        Assert.Empty(detailed);
        Assert.Equal(0, regex.Count(input, startOffsetInBytes));
    }

    [Fact]
    public void ManagedGlobalShapesPreserveQualifiedProgressionAfterUnicodeStart()
    {
        var regex = new Utf8PythonRegex(@"\b|\w+");
        var input = "é skip y"u8;
        var startOffsetInBytes = "é skip "u8.Length;
        string[] expected = ["", "y", ""];

        var matches = regex.FindAll(input, startOffsetInBytes);
        var strings = regex.FindAllToStrings(input, startOffsetInBytes);
        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);

        Assert.Equal(expected, matches.Select(static match => match.ValueText).ToArray());
        Assert.Equal(expected, strings.ScalarValues);
        Assert.Equal(
            expected,
            utf8.ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
        Assert.Equal(3, regex.Count(input, startOffsetInBytes));
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
    public void FindIterDetailedReusesUnicodeProjectionAfterStartOffset()
    {
        var regex = new Utf8PythonRegex("(é+)-(𝒜𝒜|𝒜)");
        var input = System.Text.Encoding.UTF8.GetBytes("é-𝒜 xx éé-𝒜𝒜");
        var startOffsetInBytes = "é-𝒜 xx "u8.Length;

        var matches = regex.FindIterDetailed(input, startOffsetInBytes);

        var match = Assert.Single(matches);
        Assert.Equal(startOffsetInBytes, match.Value.StartOffsetInBytes);
        Assert.Equal("éé-𝒜𝒜", match.Value.ValueText);
        Assert.Equal("éé", match.Groups[1].ValueText);
        Assert.Equal("𝒜𝒜", match.Groups[2].ValueText);
    }

    [Fact]
    public void FindIterDetailedKeepsGeneralEmptyMatchTraversal()
    {
        var matches = new Utf8PythonRegex("x*").FindIterDetailed("xx"u8);

        Assert.Equal(["xx", ""], matches.Select(static match => match.Value.ValueText).ToArray());
    }

    [Fact]
    public void ReplaceUsesManagedBackendAndPythonGroupSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)");

        var replaced = regex.ReplaceToString("foo x foo"u8, "<\\g<word>>");

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugReplaceBackend);
        Assert.Equal("<foo> x <foo>", replaced);
    }

    [Fact]
    public void SubnUsesManagedBackendForUnlimitedReplacement()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = regex.SubnToString("xx foo-foo yy foo-foo"u8, "<\\g<word>>", startOffsetInBytes: 3);

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugReplaceBackend);
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
    public void ReplaceCanUseManagedBackendForAlternationPatterns()
    {
        var regex = new Utf8PythonRegex(@"(?:b)|(?::+)");

        var replaced = regex.ReplaceToString(":a:b::c"u8, "-");

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugReplaceBackend);
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
    public void CallableSubnPreservesNamedUnmatchedGroupsAfterUnicodeByteStart()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>[a-z]+)|(é)");
        var input = "é pre | é foo"u8;
        var startOffsetInBytes = "é pre | "u8.Length;

        var text = regex.SubnToString(
            input,
            "unused",
            static (_, match) =>
            {
                Assert.True(match.TryGetFirstSetGroup("word", out var word));
                return word.Success ? $"<{word.ValueText}>" : "𝒜";
            },
            startOffsetInBytes: startOffsetInBytes);
        Utf8PythonUtf8MatchEvaluator<string> utf8Evaluator = static (_, match) =>
        {
            Assert.True(match.TryGetFirstSetGroup("word", out var word));
            return System.Text.Encoding.UTF8.GetBytes(word.Success ? $"<{word.ValueText}>" : "𝒜");
        };
        var utf8 = regex.Subn(input, "unused", utf8Evaluator, startOffsetInBytes: startOffsetInBytes);

        Assert.Equal("é pre | 𝒜 <foo>", text.ResultText);
        Assert.Equal(text.ResultText, System.Text.Encoding.UTF8.GetString(utf8.ResultBytes));
        Assert.Equal(2, text.ReplacementCount);
        Assert.Equal(text.ReplacementCount, utf8.ReplacementCount);
    }

    [Fact]
    public void CallableSubnPreservesLimitedEmptyMatchProgression()
    {
        var regex = new Utf8PythonRegex("");

        var text = regex.SubnToString("ab"u8, 0, static (_, _) => "-", count: 2);
        Utf8PythonUtf8MatchEvaluator<int> evaluator = static (_, _) => "-"u8.ToArray();
        var utf8 = regex.Subn("ab"u8, 0, evaluator, count: 2);

        Assert.Equal("-a-b", text.ResultText);
        Assert.Equal(text.ResultText, System.Text.Encoding.UTF8.GetString(utf8.ResultBytes));
        Assert.Equal(2, text.ReplacementCount);
        Assert.Equal(text.ReplacementCount, utf8.ReplacementCount);
    }

    [Fact]
    public void CallableSubnWritersHandleEmptyAndGrowingMultibyteResults()
    {
        var regex = new Utf8PythonRegex("(a|bb|c)");
        var input = "π a bb c"u8;
        var startOffsetInBytes = "π "u8.Length;
        var large = string.Concat(Enumerable.Repeat("𝒜", 32));

        var text = regex.SubnToString(
            input,
            large,
            static (largeValue, match) => match.Value.ValueText switch
            {
                "a" => string.Empty,
                "bb" => largeValue,
                _ => "x",
            },
            startOffsetInBytes: startOffsetInBytes);
        Utf8PythonUtf8MatchEvaluator<string> evaluator = static (largeValue, match) =>
            System.Text.Encoding.UTF8.GetBytes(match.Value.ValueText switch
            {
                "a" => string.Empty,
                "bb" => largeValue,
                _ => "x",
            });
        var utf8 = regex.Subn(input, large, evaluator, startOffsetInBytes: startOffsetInBytes);

        Assert.Equal($"π  {large} x", text.ResultText);
        Assert.Equal(text.ResultText, System.Text.Encoding.UTF8.GetString(utf8.ResultBytes));
        Assert.Equal(3, text.ReplacementCount);
        Assert.Equal(text.ReplacementCount, utf8.ReplacementCount);
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

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugSplitBackend);
        Assert.True(regex.DebugUsesManagedSplitFastPath);
        Assert.Equal<string?[]>(["", ":", "a", ":", "b", "::", "c"], parts);
    }

    [Fact]
    public void ManagedSplitFastPathRequiresMandatoryUnnamedCaptures()
    {
        var mandatory = new Utf8PythonRegex(@"(:+)");
        Assert.True(mandatory.DebugUsesManagedSplitFastPath);
        Assert.Equal<string?[]>(["", ":", "a", ":", "b::c"], mandatory.SplitToStrings(":a:b::c"u8, maxSplit: 2));
        Assert.Equal<string?[]>(["", ":", "a", ":", "b"], mandatory.SplitToStrings("π :a:b"u8, startOffsetInBytes: "π "u8.Length));

        var nestedMandatory = new Utf8PythonRegex(@"((?:a|b)+)-([0-9]+)");
        Assert.True(nestedMandatory.DebugUsesManagedSplitFastPath);
        Assert.Equal<string?[]>(["x", "ab", "12", "y"], nestedMandatory.SplitToStrings("xab-12y"u8));

        var optional = new Utf8PythonRegex(@"(a)?b");
        Assert.False(optional.DebugUsesManagedSplitFastPath);
        Assert.Equal<string?[]>(["", null, " ", "a", ""], optional.SplitToStrings("b ab"u8));

        var alternated = new Utf8PythonRegex(@"(b)|(:+)");
        Assert.False(alternated.DebugUsesManagedSplitFastPath);
        Assert.Equal<string?[]>(["", null, ":", "a", "b", null, ""], alternated.SplitToStrings(":ab"u8));

        Assert.False(new Utf8PythonRegex(@"(?P<separator>:+)").DebugUsesManagedSplitFastPath);
        Assert.False(new Utf8PythonRegex(@"(:*)").DebugUsesManagedSplitFastPath);
    }

    [Fact]
    public void SplitCanUseUtf8RegexBackendForPrefixFreeLiteralAlternation()
    {
        var regex = new Utf8PythonRegex(@"foo|bar");

        var parts = regex.SplitToStrings("xxfoobaryybar"u8);

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugSplitBackend);
        Assert.Equal<string?[]>(["xx", "", "yy", ""], parts);
    }

    [Fact]
    public void Utf8PatternConstructorRejectsMalformedUtf8()
    {
        var malformed = new byte[] { 0xC3, 0x28 };

        Assert.Throws<ArgumentException>(() => new Utf8PythonRegex(malformed));
    }

    [Fact]
    public void ScalarGlobalAndOutputRoutesRejectMalformedUtf8()
    {
        var direct = new Utf8PythonRegex("x");
        var empty = new Utf8PythonRegex("");
        var malformedSubjects = new[]
        {
            new byte[] { 0xC3, 0x28, (byte)'x' },
            new byte[] { (byte)'x', 0xF0, 0x28, 0x8C, 0xBC },
        };

        foreach (var input in malformedSubjects)
        {
            Assert.Throws<ArgumentException>(() => direct.Search(input));
            Assert.Throws<ArgumentException>(() => direct.FullMatch(input));
            Assert.Throws<ArgumentException>(() => direct.SearchDetailedData(input));
            Assert.Throws<ArgumentException>(() => direct.FindAll(input));
            Assert.Throws<ArgumentException>(() => direct.FindAllToStrings(input));
            Assert.Throws<ArgumentException>(() => direct.FindAllToUtf8(input));
            Assert.Throws<ArgumentException>(() => empty.Count(input));
            Assert.Throws<ArgumentException>(() => direct.ReplaceToString(input, "_"));
            Assert.Throws<ArgumentException>(() => direct.SplitToStrings(input));
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void RoutesRejectStartOffsetsInsideUtf8Scalars(int startOffsetInBytes)
    {
        var direct = new Utf8PythonRegex(".");
        var empty = new Utf8PythonRegex("");
        var input = "aé𝒜z"u8.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => direct.Search(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.FullMatch(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.SearchDetailedData(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.FindAll(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.Count(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.ReplaceToString(input, "_", startOffsetInBytes: startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.SplitToStrings(input, startOffsetInBytes: startOffsetInBytes));
    }
}
