using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class Utf8PythonRegexTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(int.MaxValue)]
    public void ConstructorRejectsInvalidExplicitMatchTimeout(int milliseconds)
    {
        var timeout = TimeSpan.FromMilliseconds(milliseconds);

        var stringException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8PythonRegex("abc", PythonReCompileOptions.None, timeout));
        var utf8Exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8PythonRegex("abc"u8, PythonReCompileOptions.None, timeout));

        Assert.Equal("matchTimeout", stringException.ParamName);
        Assert.Equal("matchTimeout", utf8Exception.ParamName);
    }

    [Fact]
    public void DefaultMatchTimeoutRejectsInvalidValueWithoutMutation()
    {
        var previous = Utf8PythonRegex.DefaultMatchTimeout;
        try
        {
            var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
                Utf8PythonRegex.DefaultMatchTimeout = TimeSpan.Zero);

            Assert.Equal("value", exception.ParamName);
            Assert.Equal(previous, Utf8PythonRegex.DefaultMatchTimeout);
        }
        finally
        {
            Utf8PythonRegex.DefaultMatchTimeout = previous;
        }
    }

    [Fact]
    public void OptionalUtf8BackendFallbackIsLimitedToExpectedConstructionFailures()
    {
        Assert.True(Utf8PythonRegex.IsOptionalUtf8BackendUnavailableException(new ArgumentException()));
        Assert.True(Utf8PythonRegex.IsOptionalUtf8BackendUnavailableException(new NotSupportedException()));
        Assert.False(Utf8PythonRegex.IsOptionalUtf8BackendUnavailableException(new InvalidOperationException()));
        Assert.False(Utf8PythonRegex.IsOptionalUtf8BackendUnavailableException(new OutOfMemoryException()));
    }

    [Fact]
    public void ExactLiteralUsesUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.DebugUsesZeroOffsetUtf8ValueFastPath);
        Assert.False(regex.DebugIsUtf8FullRegexValueCreated);
        Assert.True(regex.DebugHasUtf8FullRegex);
        Assert.True(regex.DebugIsUtf8FullRegexValueCreated);
        Assert.Equal("foo", regex.DebugTranslatedPattern);
        Assert.Equal("Search=Utf8Regex, Match=Utf8Regex, FullMatch=Utf8Regex, Count=Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("xxfooyy"u8));
        var nonAsciiPrefixMatch = regex.Search("éfoo"u8);
        Assert.True(nonAsciiPrefixMatch.Success);
        Assert.Equal(2, nonAsciiPrefixMatch.StartOffsetInBytes);
        Assert.Equal(1, nonAsciiPrefixMatch.StartOffsetInUtf16);
        Assert.True(regex.Match("foo xx"u8).Success);
        Assert.False(regex.Match("xx foo"u8).Success);
        Assert.Equal(2, regex.Count("foo xx foo"u8));
    }

    [Fact]
    public void ExactUnicodeLiteralContainingSpacePreservesCountSemantics()
    {
        var regex = new Utf8PythonRegex("Шерлок Холмс");

        Assert.Equal("Шерлок Холмс", regex.DebugTranslatedPattern);
        Assert.Equal("ExactUtf8Literal", regex.DebugUtf8ExecutionKind);
        Assert.Equal(2, regex.Count("Шерлок Холмс и снова Шерлок Холмс"u8));
        Assert.Equal(0, regex.Count("ШерлокХолмс"u8));
    }

    [Fact]
    public void ExactUnicodeLiteralSearchPreservesCoordinatesAndInputContracts()
    {
        var regex = new Utf8PythonRegex("Шерлок");
        var input = "π Шерлок 𝒜"u8.ToArray();

        var search = regex.Search(input);
        var match = regex.Match("Шерлок далее"u8);

        Assert.True(regex.DebugUsesZeroOffsetUtf8SearchValueFastPath);
        Assert.False(regex.DebugUsesZeroOffsetUtf8ValueFastPath);
        Assert.True(search.Success);
        Assert.Equal("π ".Length, search.StartOffsetInUtf16);
        Assert.Equal("π Шерлок".Length, search.EndOffsetInUtf16);
        Assert.Equal("π "u8.Length, search.StartOffsetInBytes);
        Assert.Equal("π Шерлок"u8.Length, search.EndOffsetInBytes);
        Assert.Equal("Шерлок", search.GetValueString());
        Assert.True(match.Success);
        Assert.Equal("Шерлок"u8.Length, match.EndOffsetInBytes);
        Assert.False(regex.Search("Ватсон"u8).Success);
        Assert.False(regex.Match("π Шерлок"u8).Success);

        var malformedTail = "Шерлок"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Search(malformedTail));
        Assert.Throws<ArgumentException>(() => regex.Match(malformedTail));
    }

    [Fact]
    public void VerboseEscapedSpaceRemainsSignificant()
    {
        var regex = new Utf8PythonRegex(@"(?x)Шерлок\ Холмс");

        Assert.Equal(@"Шерлок\ Холмс", regex.DebugTranslatedPattern);
        Assert.Equal(1, regex.Count("Шерлок Холмс"u8));
        Assert.Equal(0, regex.Count("ШерлокХолмс"u8));
    }

    [Theory]
    [InlineData(@"(?<=ab)c")]
    [InlineData(@"(?m)^foo$")]
    [InlineData(@"[\U0001D49C]")]
    public void ZeroOffsetValueFastPathExcludesPatternsThatRequireFallbackSemantics(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.Multiline);

        Assert.False(regex.DebugUsesZeroOffsetUtf8ValueFastPath);
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
    public void DotAllFullMatchPreservesWholeSuffixCoordinates()
    {
        var regex = new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll);
        var input = "πstart\n東京𝒜end"u8.ToArray();
        var startOffsetInBytes = "π"u8.Length;

        var match = regex.FullMatch(input, startOffsetInBytes);

        Assert.True(regex.DebugUsesAsciiDotAllFullMatchFastPath);
        Assert.True(match.Success);
        Assert.True(match.HasContiguousByteRange);
        Assert.Equal(startOffsetInBytes, match.StartOffsetInBytes);
        Assert.Equal(input.Length, match.EndOffsetInBytes);
        Assert.Equal("π".Length, match.StartOffsetInUtf16);
        Assert.Equal("πstart\n東京𝒜end".Length, match.EndOffsetInUtf16);
        Assert.Equal("start\n東京𝒜end", match.GetValueString());
        Assert.True(regex.FullMatch("startend"u8).Success);
        Assert.True(regex.FullMatch("start\0end"u8).Success);
    }

    [Fact]
    public void DotAllFullMatchPreservesPrefixSuffixAndInputContracts()
    {
        var regex = new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll);
        var overlapping = new Utf8PythonRegex("aba.*aba", PythonReCompileOptions.DotAll);
        var ignoreCase = new Utf8PythonRegex(
            "start.*end",
            PythonReCompileOptions.DotAll | PythonReCompileOptions.IgnoreCase);
        var captured = new Utf8PythonRegex("start(.*)end", PythonReCompileOptions.DotAll);
        var reluctant = new Utf8PythonRegex("start.*?end", PythonReCompileOptions.DotAll);
        var finiteTimeout = new Utf8PythonRegex(
            "start.*end",
            PythonReCompileOptions.DotAll,
            TimeSpan.FromSeconds(1));

        Assert.True(regex.DebugUsesAsciiDotAllFullMatchFastPath);
        Assert.True(overlapping.DebugUsesAsciiDotAllFullMatchFastPath);
        Assert.False(ignoreCase.DebugUsesAsciiDotAllFullMatchFastPath);
        Assert.False(captured.DebugUsesAsciiDotAllFullMatchFastPath);
        Assert.False(reluctant.DebugUsesAsciiDotAllFullMatchFastPath);
        Assert.False(finiteTimeout.DebugUsesAsciiDotAllFullMatchFastPath);

        Assert.False(regex.FullMatch("xstartend"u8).Success);
        Assert.False(regex.FullMatch("startendx"u8).Success);
        Assert.False(regex.FullMatch("start-END"u8).Success);
        Assert.False(regex.FullMatch("starend"u8).Success);
        Assert.False(overlapping.FullMatch("aba"u8).Success);
        Assert.True(overlapping.FullMatch("abaaba"u8).Success);
        Assert.Throws<ArgumentException>(() => regex.FullMatch(
            new byte[] { (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', 0xC3, 0x28, (byte)'e', (byte)'n', (byte)'d' }));

        Assert.False(new Utf8PythonRegex("start.*end").FullMatch("start\nend"u8).Success);
        Assert.True(ignoreCase.FullMatch("START\nEND"u8).Success);
        Assert.Equal("middle", captured.FullMatchDetailed("startmiddleend"u8)
            .GetGroup(1).Value.GetValueString());
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
    public void EmptyThenNonEmptyCompanionHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("x*|(?:(a+)+$)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");

        Assert.False(regex.DebugIsManagedNonEmptyAtSamePositionRegexValueCreated);
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FindAll(input));
        Assert.True(regex.DebugIsManagedNonEmptyAtSamePositionRegexValueCreated);
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
    public void FindAllToStringsPreservesExactOptionalCaptureValuesAndByteStart()
    {
        var numbered = new Utf8PythonRegex("pre(ab)?c");
        var named = new Utf8PythonRegex("(?P<letter>é)?x");
        var numberedInput = "π preabc prec preabc"u8;
        var namedInput = "skip x éx x"u8;

        var numberedResult = numbered.FindAllToStrings(
            numberedInput,
            startOffsetInBytes: "π "u8.Length);
        var namedResult = named.FindAllToStrings(
            namedInput,
            startOffsetInBytes: "skip "u8.Length);

        Assert.True(numbered.DebugUsesOptionalExactCaptureFindAllFastPath);
        Assert.True(named.DebugUsesOptionalExactCaptureFindAllFastPath);
        Assert.Equal(["ab", "", "ab"], numberedResult.ScalarValues);
        Assert.Same(numberedResult.ScalarValues[0], numberedResult.ScalarValues[2]);
        Assert.Equal(["", "é", ""], namedResult.ScalarValues);
    }

    [Fact]
    public void FindAllToStringsPreservesSubjectTextForNonExactOptionalCaptures()
    {
        var ignoreCase = new Utf8PythonRegex("(a)?b", PythonReCompileOptions.IgnoreCase);
        var characterClass = new Utf8PythonRegex("([ab])?c");
        var reluctant = new Utf8PythonRegex("(a)??a");

        Assert.False(ignoreCase.DebugUsesOptionalExactCaptureFindAllFastPath);
        Assert.False(characterClass.DebugUsesOptionalExactCaptureFindAllFastPath);
        Assert.False(reluctant.DebugUsesOptionalExactCaptureFindAllFastPath);
        Assert.Equal(["A", ""], ignoreCase.FindAllToStrings("AB b"u8).ScalarValues);
        Assert.Equal(["a", "b", ""], characterClass.FindAllToStrings("ac bc c"u8).ScalarValues);
        Assert.Equal(["", "", ""], reluctant.FindAllToStrings("aa a"u8).ScalarValues);
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
    public void EmptyThenNonEmptyAlternativePreservesAllGlobalResultShapes()
    {
        var regex = new Utf8PythonRegex("x*|y");
        string[] expected = ["", "y", ""];

        Assert.False(regex.DebugIsManagedNonEmptyAtSamePositionRegexValueCreated);
        Assert.Equal(expected, regex.FindAll("y"u8).Select(static match => match.ValueText).ToArray());
        Assert.True(regex.DebugIsManagedNonEmptyAtSamePositionRegexValueCreated);
        Assert.Equal(expected, regex.FindAllToStrings("y"u8).ScalarValues);
        Assert.Equal(
            expected,
            regex.FindAllToUtf8("y"u8).ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
        Assert.Equal(expected, regex.FindIterDetailed("y"u8).Select(static match => match.Value.ValueText).ToArray());
        Assert.Equal(3, regex.Count("y"u8));
        Assert.Equal("---", regex.ReplaceToString("y"u8, "-"));

        var subn = regex.SubnToString("y"u8, "-");
        Assert.Equal("---", subn.ResultText);
        Assert.Equal(3, subn.ReplacementCount);
        Assert.Equal(new string?[] { "", "", "", "" }, regex.SplitToStrings("y"u8));

        var empty = new Utf8PythonRegex("");
        Assert.Equal(["", ""], empty.FindAll("𝒜"u8).Select(static match => match.ValueText).ToArray());
        Assert.Equal(["", ""], empty.FindIterDetailed("𝒜"u8).Select(static match => match.Value.ValueText).ToArray());
        Assert.Equal(2, empty.Count("𝒜"u8));
    }

    [Fact]
    public void GreedyAsciiLiteralStarFindAllPreservesScalarSafeEmptyProgression()
    {
        var regex = new Utf8PythonRegex("x*");

        Assert.Equal(["", "", "", ""], regex.FindAllToStrings("yyy"u8).ScalarValues);
        Assert.Equal(["xx", "", "xx", ""], regex.FindAllToStrings("xxyxx"u8).ScalarValues);
        Assert.Equal(["", "x", "", ""], regex.FindAllToStrings("πx𝒜"u8).ScalarValues);
        Assert.Equal(
            ["x", "", ""],
            regex.FindAllToStrings("πx𝒜"u8, "π"u8.Length).ScalarValues);
        Assert.Equal([""], regex.FindAllToStrings(ReadOnlySpan<byte>.Empty).ScalarValues);

        Assert.Equal(["X", ""], new Utf8PythonRegex("x*", PythonReCompileOptions.IgnoreCase)
            .FindAllToStrings("X"u8).ScalarValues);
        Assert.Equal(["", "x", "", "x", ""], new Utf8PythonRegex("x*?")
            .FindAllToStrings("xx"u8).ScalarValues);

        var malformedTail = new byte[] { (byte)'x', 0xF0, 0x28, 0x8C, 0xBC };
        Assert.Throws<ArgumentException>(() => regex.FindAllToStrings(malformedTail));
    }

    [Fact]
    public void LiteralReplacementPreservesGreedyExactRepeatProgression()
    {
        var repeated = new Utf8PythonRegex("x*");
        var grouped = new Utf8PythonRegex("(?:ab)*");

        Assert.True(repeated.DebugUsesManagedEmptyReplacementFastPath);
        Assert.True(grouped.DebugUsesManagedEmptyReplacementFastPath);
        Assert.Equal("-a-b--d-", repeated.ReplaceToString("abxd"u8, "-"));
        Assert.Equal("-a-b--d-", System.Text.Encoding.UTF8.GetString(repeated.Replace("abxd"u8, "-")));
        var repeatedSubn = repeated.SubnToString("abxd"u8, "-");
        Assert.Equal("-a-b--d-", repeatedSubn.ResultText);
        Assert.Equal(5, repeatedSubn.ReplacementCount);
        Assert.Equal("_z__q_", grouped.ReplaceToString("zababq"u8, "_"));
        Assert.Equal(
            "π-a-bxd",
            repeated.ReplaceToString("πabxd"u8, "-", count: 2, startOffsetInBytes: "π"u8.Length));
    }

    [Fact]
    public void LiteralReplacementPreservesAmbiguousEmptyProgression()
    {
        var alternation = new Utf8PythonRegex("x*|y");
        var reluctant = new Utf8PythonRegex("x*?");

        Assert.False(alternation.DebugUsesManagedEmptyReplacementFastPath);
        Assert.False(reluctant.DebugUsesManagedEmptyReplacementFastPath);
        Assert.Equal("---", alternation.ReplaceToString("y"u8, "-"));
        Assert.Equal("-----", reluctant.ReplaceToString("xx"u8, "-"));
    }

    [Fact]
    public void FindAllStringsProjectsSingleTrailingCaptureFromValueMatches()
    {
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        var input = "skip item-12 item-345 item-6"u8;

        var all = regex.FindAllToStrings(input);
        var afterFirst = regex.FindAllToStrings(input, startOffsetInBytes: "skip item-12 "u8.Length);

        Assert.True(regex.DebugUsesSingleTrailingCaptureFindAllFastPath);
        Assert.Equal(["12", "345", "6"], all.ScalarValues);
        Assert.Equal(["345", "6"], afterFirst.ScalarValues);
    }

    [Fact]
    public void FindAllStringsReusesCaseSensitiveExactLiteralValue()
    {
        var regex = new Utf8PythonRegex("(?:Шерлок)");
        var input = "skip Шерлок Шерлок Шерлок"u8;

        var all = regex.FindAllToStrings(input);
        var afterFirst = regex.FindAllToStrings(input, "skip Шерлок "u8.Length);

        Assert.True(regex.DebugUsesRepeatedExactStringFindAllFastPath);
        Assert.True(regex.DebugUsesCountedRepeatedExactStringFindAllFastPath);
        Assert.Equal(["Шерлок", "Шерлок", "Шерлок"], all.ScalarValues);
        Assert.Same(all.ScalarValues[0], all.ScalarValues[1]);
        Assert.Equal(["Шерлок", "Шерлок"], afterFirst.ScalarValues);
        Assert.Same(afterFirst.ScalarValues[0], afterFirst.ScalarValues[1]);
        Assert.Equal(
            ["Шерлок", "Шерлок", "Шерлок"],
            regex.FindAllToUtf8(input).ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void ExactLiteralFindAllFromOffsetPreservesValueIdentityAndWholeInputValidation()
    {
        var regex = new Utf8PythonRegex("item");
        var input = "item skip item item"u8;
        var result = regex.FindAllToStrings(input, "item skip "u8.Length);

        Assert.Equal(["item", "item"], result.ScalarValues);
        Assert.Same(result.ScalarValues[0], result.ScalarValues[1]);

        var malformedTail = new byte[]
        {
            (byte)'s', (byte)'k', (byte)'i', (byte)'p', (byte)' ',
            (byte)'i', (byte)'t', (byte)'e', (byte)'m', 0xC3, 0x28,
        };
        Assert.Throws<ArgumentException>(() =>
            regex.FindAllToStrings(malformedTail, "skip "u8.Length));
    }

    [Fact]
    public void MatchUsesValidatedAsciiLiteralPrefixDigitRoute()
    {
        var regex = new Utf8PythonRegex("header:[0-9]+");
        var match = regex.Match("header:12345 café"u8);

        Assert.True(regex.DebugUsesAsciiLiteralPrefixDigitMatchFastPath);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(12, match.EndOffsetInBytes);
        Assert.Equal(0, match.StartOffsetInUtf16);
        Assert.Equal(12, match.EndOffsetInUtf16);
        Assert.Equal("header:12345", match.GetValueString());
        Assert.False(regex.Match("header: tail"u8).Success);
        Assert.False(regex.Match("other:123"u8).Success);

        var invalid = "header:123"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.Match(invalid));
    }

    [Fact]
    public void MatchRetainsFallbackOutsideAsciiLiteralPrefixDigitRoute()
    {
        var regex = new Utf8PythonRegex("header:[0-9]+");
        var ignoreCase = new Utf8PythonRegex("header:[0-9]+", PythonReCompileOptions.IgnoreCase);
        var reluctant = new Utf8PythonRegex("header:[0-9]+?");
        var captured = new Utf8PythonRegex("header:([0-9]+)");
        var finiteTimeout = new Utf8PythonRegex("header:[0-9]+", PythonReCompileOptions.None, TimeSpan.FromSeconds(1));

        Assert.False(ignoreCase.DebugUsesAsciiLiteralPrefixDigitMatchFastPath);
        Assert.False(reluctant.DebugUsesAsciiLiteralPrefixDigitMatchFastPath);
        Assert.False(captured.DebugUsesAsciiLiteralPrefixDigitMatchFastPath);
        Assert.False(finiteTimeout.DebugUsesAsciiLiteralPrefixDigitMatchFastPath);
        Assert.Equal(1, reluctant.Match("header:123"u8).EndOffsetInBytes - "header:"u8.Length);

        var offsetMatch = regex.Match("skipheader:123 tail"u8, "skip"u8.Length);
        Assert.True(offsetMatch.Success);
        Assert.Equal("skip"u8.Length, offsetMatch.StartOffsetInBytes);
        Assert.Equal("skipheader:123"u8.Length, offsetMatch.EndOffsetInBytes);
    }

    [Fact]
    public void FindAllStringsRetainsMatchedValuesOutsideExactCaseSensitiveRoute()
    {
        var globalIgnoreCase = new Utf8PythonRegex("sherlock", PythonReCompileOptions.IgnoreCase);
        var scopedIgnoreCase = new Utf8PythonRegex("(?i:sherlock)");
        var captured = new Utf8PythonRegex("(sherlock)");
        var characterClass = new Utf8PythonRegex("[a-z]+");
        var input = "SHERLOCK Sherlock"u8;

        Assert.False(globalIgnoreCase.DebugUsesRepeatedExactStringFindAllFastPath);
        Assert.False(scopedIgnoreCase.DebugUsesRepeatedExactStringFindAllFastPath);
        Assert.False(captured.DebugUsesRepeatedExactStringFindAllFastPath);
        Assert.False(characterClass.DebugUsesRepeatedExactStringFindAllFastPath);
        Assert.Equal(["SHERLOCK", "Sherlock"], globalIgnoreCase.FindAllToStrings(input).ScalarValues);
        Assert.Equal(["SHERLOCK", "Sherlock"], scopedIgnoreCase.FindAllToStrings(input).ScalarValues);
        Assert.Equal(["sherlock"], captured.FindAllToStrings("sherlock"u8).ScalarValues);
        Assert.Equal(["alpha", "beta"], characterClass.FindAllToStrings("alpha beta"u8).ScalarValues);
    }

    [Theory]
    [InlineData("([0-9]+)-item", "12-item 345-item", "12", "345")]
    [InlineData("([0-9])+", "12 345", "2", "5")]
    public void FindAllStringsRetainsGeneralCaptureProjection(
        string pattern,
        string input,
        string first,
        string second)
    {
        var regex = new Utf8PythonRegex(pattern);

        var result = regex.FindAllToStrings(System.Text.Encoding.UTF8.GetBytes(input));

        Assert.False(regex.DebugUsesSingleTrailingCaptureFindAllFastPath);
        Assert.Equal([first, second], result.ScalarValues);
    }

    [Fact]
    public void FindAllStringsProjectsSeparatedCaptureTuplesFromValueMatches()
    {
        var regex = new Utf8PythonRegex("([a-z]+)-([0-9]+)", PythonReCompileOptions.IgnoreCase);
        var input = "skip abc-12 DEF-345 ghi-6"u8;

        var all = regex.FindAllToStrings(input);
        var afterFirst = regex.FindAllToStrings(input, startOffsetInBytes: "skip abc-12 "u8.Length);

        Assert.True(regex.DebugUsesSeparatedCaptureTupleFindAllFastPath);
        Assert.Equal([["abc", "12"], ["DEF", "345"], ["ghi", "6"]], all.TupleValues);
        Assert.Equal([["DEF", "345"], ["ghi", "6"]], afterFirst.TupleValues);
    }

    [Theory]
    [InlineData("([a-z-]+)-([0-9]+)", "abc--12", "abc-", "12")]
    [InlineData("([a-z]+)--([0-9]+)", "abc--12", "abc", "12")]
    [InlineData("(.*)-([0-9]+)", "abc-12", "abc", "12")]
    public void FindAllStringsRetainsGeneralAmbiguousTupleProjection(
        string pattern,
        string input,
        string first,
        string second)
    {
        var regex = new Utf8PythonRegex(pattern);

        var result = regex.FindAllToStrings(System.Text.Encoding.UTF8.GetBytes(input));

        Assert.False(regex.DebugUsesSeparatedCaptureTupleFindAllFastPath);
        Assert.Equal([[first, second]], result.TupleValues);
    }

    [Fact]
    public void EmptyThenNonEmptyCompanionPreservesCapturesContextAndConcurrentReuse()
    {
        var captured = new Utf8PythonRegex("(?P<word>x*|y)");
        Assert.Equal(["", "y", ""], captured.FindAllToStrings("y"u8).ScalarValues);

        var lookbehind = new Utf8PythonRegex("(?<=a)(?:x*|y)");
        Assert.Equal(
            ["", "y"],
            lookbehind.FindAll("ay"u8, startOffsetInBytes: 1)
                .Select(static match => match.ValueText)
                .ToArray());

        var anchored = new Utf8PythonRegex("^x*|y");
        Assert.Equal(["", "y"], anchored.FindAll("y"u8).Select(static match => match.ValueText).ToArray());

        var unicodeInput = "éy"u8.ToArray();
        var startOffsetInBytes = "é"u8.Length;
        Parallel.For(0, 16, _ =>
        {
            Assert.Equal(
                ["", "y", ""],
                captured.FindAll(unicodeInput, startOffsetInBytes)
                    .Select(static match => match.ValueText)
                    .ToArray());
        });
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
        var oneCapture = new Utf8PythonRegex("(needle|needle-long)");
        var twoCaptures = new Utf8PythonRegex("(needle)|(needle-long)");
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
        Assert.Empty(oneCapture.FindAllToUtf8(input, startOffsetInBytes).ScalarValues);
        Assert.Empty(twoCaptures.FindAllToUtf8(input, startOffsetInBytes).TupleValues);
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
    public void ExactLiteralReplacementUsesCountedUtf8PathForUnlimitedZeroOffsetOperations()
    {
        var regex = new Utf8PythonRegex("cat");
        var input = "é cat fox cat 𝒜"u8;
        const string expected = "é $🐈 fox $🐈 𝒜";

        var replacedText = regex.ReplaceToString(input, "$🐈");
        var replacedUtf8 = regex.Replace(input, "$🐈");
        var subnText = regex.SubnToString(input, "$🐈");
        var subnUtf8 = regex.Subn(input, "$🐈");

        Assert.Equal(PythonReDirectBackendKind.Utf8Regex, regex.DebugReplaceBackend);
        Assert.Equal(expected, replacedText);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(replacedUtf8));
        Assert.Equal((expected, 2), (subnText.ResultText, subnText.ReplacementCount));
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(subnUtf8.ResultBytes));
        Assert.Equal(2, subnUtf8.ReplacementCount);
    }

    [Fact]
    public void ExactLiteralReplacementHonorsLimitsOffsetsAndGroups()
    {
        var regex = new Utf8PythonRegex("cat");
        var input = "cat cat cat"u8;

        Assert.Equal("dog cat cat", regex.ReplaceToString(input, "dog", count: 1));
        Assert.Equal(
            "🐈 🐈 cat",
            System.Text.Encoding.UTF8.GetString(regex.Replace(input, "🐈", count: 2)));
        var subn = regex.SubnToString(input, "dog", count: 2);
        Assert.Equal(("dog dog cat", 2), (subn.ResultText, subn.ReplacementCount));
        Assert.Equal("dog dog dog", regex.ReplaceToString(input, "dog", count: 99));
        Assert.Equal("cat dog dog", regex.ReplaceToString(input, "dog", count: 0, startOffsetInBytes: 4));
        Assert.Equal("<cat> <cat> <cat>", regex.ReplaceToString(input, @"<\g<0>>"));

        var malformedTail = new byte[] { (byte)'c', (byte)'a', (byte)'t', 0xC3, 0x28 };
        Assert.Throws<ArgumentException>(() =>
            regex.ReplaceToString(malformedTail, "dog", count: 1));
    }

    [Fact]
    public void SubnUsesManagedBackendForUnlimitedReplacement()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = regex.SubnToString("xx foo-foo yy foo-foo"u8, "<\\g<word>>", count: 0, startOffsetInBytes: 3);

        Assert.Equal(PythonReDirectBackendKind.ManagedRegex, regex.DebugReplaceBackend);
        Assert.Equal("xx <foo> yy <foo>", replaced.ResultText);
        Assert.Equal(2, replaced.ReplacementCount);
    }

    [Fact]
    public void SubnReturnsUtf8ResultBytesAndCount()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = regex.Subn("xx foo-foo yy foo-foo"u8, "<\\g<word>>", count: 0, startOffsetInBytes: 3);

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
            count: 0,
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
            count: 0,
            startOffsetInBytes: startOffsetInBytes);
        Utf8PythonUtf8MatchEvaluator<string> utf8Evaluator = static (_, match) =>
        {
            Assert.True(match.TryGetFirstSetGroup("word", out var word));
            return System.Text.Encoding.UTF8.GetBytes(word.Success ? $"<{word.ValueText}>" : "𝒜");
        };
        var utf8 = regex.Subn(input, "unused", utf8Evaluator, count: 0, startOffsetInBytes: startOffsetInBytes);

        Assert.Equal("é pre | 𝒜 <foo>", text.ResultText);
        Assert.Equal(text.ResultText, System.Text.Encoding.UTF8.GetString(utf8.ResultBytes));
        Assert.Equal(2, text.ReplacementCount);
        Assert.Equal(text.ReplacementCount, utf8.ReplacementCount);
    }

    [Fact]
    public void DetailedConsumersPreserveMixedOptionalCapturesAfterUnicodeByteStart()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>[a-z]+)(?:-([0-9]+))?");
        var input = "π skip cat dog-12"u8.ToArray();
        var startOffsetInBytes = "π skip "u8.Length;

        var matches = regex.FindIterDetailed(input, startOffsetInBytes);
        Assert.Equal(2, matches.Length);
        Assert.Equal("cat", Describe(matches[0]));
        Assert.Equal("dog:12", Describe(matches[1]));

        var text = regex.SubnToString(
            input,
            0,
            static (_, match) => $"<{Describe(match)}>",
            count: 0,
            startOffsetInBytes: startOffsetInBytes);
        Utf8PythonUtf8MatchEvaluator<int> utf8Evaluator = static (_, match) =>
            System.Text.Encoding.UTF8.GetBytes($"<{Describe(match)}>");
        var utf8 = regex.Subn(input, 0, utf8Evaluator, count: 0, startOffsetInBytes: startOffsetInBytes);

        Assert.Equal("π skip <cat> <dog:12>", text.ResultText);
        Assert.Equal(text.ResultText, System.Text.Encoding.UTF8.GetString(utf8.ResultBytes));
        Assert.Equal(2, text.ReplacementCount);
        Assert.Equal(text.ReplacementCount, utf8.ReplacementCount);

        Parallel.For(0, 16, _ =>
        {
            var result = regex.SubnToString(
                input,
                0,
                static (_, match) => $"<{Describe(match)}>",
                count: 0,
                startOffsetInBytes: startOffsetInBytes);
            Assert.Equal(text.ResultText, result.ResultText);
        });

        static string Describe(Utf8PythonDetailedMatchData match)
        {
            Assert.True(match.TryGetFirstSetGroup("word", out var word));
            var optional = match.Groups.FirstOrDefault(group =>
                group.Number != 0 && group.Number != word.Number && group.Success);
            return optional.Success
                ? $"{word.ValueText}:{optional.ValueText}"
                : word.ValueText;
        }
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
            count: 0,
            startOffsetInBytes: startOffsetInBytes);
        Utf8PythonUtf8MatchEvaluator<string> evaluator = static (largeValue, match) =>
            System.Text.Encoding.UTF8.GetBytes(match.Value.ValueText switch
            {
                "a" => string.Empty,
                "bb" => largeValue,
                _ => "x",
            });
        var utf8 = regex.Subn(input, large, evaluator, count: 0, startOffsetInBytes: startOffsetInBytes);

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
        Assert.Equal<string?[]>(["", ":", "a", ":", "b"], mandatory.SplitToStrings("π :a:b"u8, maxSplit: 0, startOffsetInBytes: "π "u8.Length));

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
        Assert.True(new Utf8PythonRegex("").DebugUsesManagedSplitFastPath);
        Assert.True(new Utf8PythonRegex(@"(?=:)").DebugUsesManagedSplitFastPath);
        Assert.False(new Utf8PythonRegex(@"\b|:+").DebugUsesManagedSplitFastPath);
        Assert.False(new Utf8PythonRegex("x*|y").DebugUsesManagedSplitFastPath);

        var asciiBoundary = new Utf8PythonRegex(@"\b", PythonReCompileOptions.Ascii);
        Assert.True(asciiBoundary.DebugUsesAsciiWordBoundarySplit);
        Assert.Equal<string?[]>(["", "alpha", " ", "beta", ""], asciiBoundary.SplitToStrings("alpha beta"u8));
        Assert.Equal<string?[]>(["", "alpha beta"], asciiBoundary.SplitToStrings("alpha beta"u8, maxSplit: 1));
        Assert.Equal<string?[]>(["π", "alpha", " 𝒜", "beta", ""], asciiBoundary.SplitToStrings("πalpha 𝒜beta"u8));
        var detailed = asciiBoundary.SplitDetailed("alpha beta"u8);
        Assert.All(detailed, static item => Assert.False(item.IsCapture));
        Assert.Equal<string?[]>(["", "alpha", " ", "beta", ""], detailed.Select(static item => item.ValueText).ToArray());
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
            Assert.Throws<ArgumentException>(() => direct.Match(input));
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
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.ReplaceToString(input, "_", count: 0, startOffsetInBytes: startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.SplitToStrings(input, maxSplit: 0, startOffsetInBytes: startOffsetInBytes));
    }
}
