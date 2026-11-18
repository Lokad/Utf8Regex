using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class Utf8PythonRegexTests
{
    [Fact]
    public void NullableConstructorDefaultInheritsTimeoutButTypedZeroIsRejected()
    {
        var inherited = new Utf8PythonRegex("a", PythonReCompileOptions.None, default);
        Assert.Equal(Utf8PythonRegex.DefaultMatchTimeout, inherited.MatchTimeout);
        Assert.Equal(inherited.MatchTimeout, new Utf8PythonRegex("a"u8, matchTimeout: null).MatchTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex("a", PythonReCompileOptions.None, default(TimeSpan)));
    }

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
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.True(regex.Match("foo"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.DebugIsScalarFullProgramValueCreated);
        Assert.Equal("foo", PythonReDotNetReference.Render(regex));
        Assert.True(regex.IsMatch("xxfooyy"u8));
        var nonAsciiPrefixMatch = regex.Match("éfoo"u8,mode: Utf8PythonMatchMode.Search);
        Assert.True(nonAsciiPrefixMatch.Success);
        Assert.Equal(2, nonAsciiPrefixMatch.StartOffsetInBytes);
        Assert.True(regex.Match("foo xx"u8).Success);
        Assert.False(regex.Match("xx foo"u8).Success);
        Assert.Equal(2, regex.Count("foo xx foo"u8));
    }

    [Fact]
    public void ExactUnicodeLiteralContainingSpacePreservesCountSemantics()
    {
        var regex = new Utf8PythonRegex("Шерлок Холмс");

        Assert.Equal("Шерлок Холмс", PythonReDotNetReference.Render(regex));
        Assert.Equal("ExactUtf8Literal", regex.DebugUtf8ExecutionKind);
        Assert.Equal(2, regex.Count("Шерлок Холмс и снова Шерлок Холмс"u8));
        Assert.Equal(0, regex.Count("ШерлокХолмс"u8));
    }

    [Fact]
    public void ExactUnicodeLiteralSearchPreservesCoordinatesAndInputContracts()
    {
        var regex = new Utf8PythonRegex("Шерлок");
        var input = "π Шерлок 𝒜"u8.ToArray();

        var search = regex.Match(input,mode: Utf8PythonMatchMode.Search);
        var match = regex.Match("Шерлок далее"u8);

        Assert.True(regex.DebugUsesZeroOffsetUtf8SearchValueFastPath);
        Assert.False(regex.DebugUsesZeroOffsetUtf8ValueFastPath);
        Assert.True(search.Success);
        Assert.Equal("π "u8.Length, search.StartOffsetInBytes);
        Assert.Equal("π Шерлок"u8.Length, search.EndOffsetInBytes);
        Assert.Equal("Шерлок", System.Text.Encoding.UTF8.GetString(search.GetValueBytes()));
        Assert.True(match.Success);
        Assert.Equal("Шерлок"u8.Length, match.EndOffsetInBytes);
        Assert.False(regex.Match("Ватсон"u8,mode: Utf8PythonMatchMode.Search).Success);
        Assert.False(regex.Match("π Шерлок"u8).Success);

    }

    [Fact]
    public void VerboseEscapedSpaceRemainsSignificant()
    {
        var regex = new Utf8PythonRegex(@"(?x)Шерлок\ Холмс");

        Assert.Equal(@"Шерлок\ Холмс", PythonReDotNetReference.Render(regex));
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

        Assert.True(regex.Match("xxcaféyy"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void NamedCapturingGroupTranslatesToDotNetSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)");

        Assert.Equal(@"(?<g1>foo)", PythonReDotNetReference.Render(regex));
        Assert.Contains("word", regex.GetGroupNames(), StringComparer.Ordinal);
        Assert.True(regex.IsMatch("foo"u8));
    }

    [Fact]
    public void NamedBackreferenceTranslatesToDotNetSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        Assert.Equal(@"(?<g1>foo)-\k<g1>", PythonReDotNetReference.Render(regex));
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

        Assert.Equal(@"(?i:[Ff][Oo][Oo])", PythonReDotNetReference.Render(regex));
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
        Assert.True(dotAll.Match("a\nb"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void MultilineCanUseUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex(@"(?m)^foo$", PythonReCompileOptions.Multiline);

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.Match("bar\nfoo\nbaz"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void VerbosePatternUsesOwnedScalarProgram()
    {
        var regex = new Utf8PythonRegex(
            """
            (?x)
            foo \s+ bar
            """);

        Assert.False(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.Match("xxfoo   baryy"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void PossessiveQuantifierAndAtomicGroupArePreserved()
    {
        var possessive = new Utf8PythonRegex(@"a++b");
        var atomic = new Utf8PythonRegex(@"(?>a+)b");

        Assert.Equal(@"(?>a+)b", PythonReDotNetReference.Render(possessive));
        Assert.Equal(@"(?>a+)b", PythonReDotNetReference.Render(atomic));
        Assert.True(possessive.IsMatch("aaab"u8));
        Assert.True(atomic.IsMatch("aaab"u8));
    }

    [Fact]
    public void BackreferencesUseOwnedScalarDiscovery()
    {
        var named = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");
        var numeric = new Utf8PythonRegex(@"(ab)\1");

        Assert.True(named.Match("xx foo-foo yy"u8,mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(numeric.Match("zzababzz"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void AtomicAndPossessiveFormsUseOwnedScalarDiscovery()
    {
        var atomic = new Utf8PythonRegex(@"(?>ab)c");
        var possessive = new Utf8PythonRegex(@"a++b");

        Assert.True(atomic.Match("xxabcyy"u8,mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(possessive.Match("xxaaabyy"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void MatchAndFullMatchCanUseUtf8RegexBackend()
    {
        var matchRegex = new Utf8PythonRegex(@"foo");
        var fullRegex = new Utf8PythonRegex(@"a|ab");

        Assert.True(matchRegex.Match("foo xx"u8).Success);
        Assert.False(matchRegex.Match("xxfoo"u8).Success);
        Assert.True(fullRegex.Match("ab"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(fullRegex.Match("ab"u8,mode: Utf8PythonMatchMode.FullMatch).GetValueBytes()));
    }

    [Fact]
    public void ConditionalAndLookaroundKeepOwnedResultsAfterCoreSelection()
    {
        var conditional = new Utf8PythonRegex(@"(foo)?(?(1)bar|baz)");
        var lookahead = new Utf8PythonRegex(@"foo(?=bar)");

        Assert.False(conditional.DebugUsesUtf8RegexBackend);
        Assert.True(lookahead.DebugUsesUtf8RegexBackend);
        Assert.False(conditional.DebugIsScalarFullProgramValueCreated);
        Assert.False(lookahead.DebugIsScalarFullProgramValueCreated);
        Assert.True(conditional.Match("foobar"u8,mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(lookahead.Match("foobar"u8,mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(conditional.Match("foobar"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(conditional.Match("baz"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(conditional.Match("foo"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(conditional.DebugIsScalarFullProgramValueCreated);
        Assert.False(lookahead.Match("foobar"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(lookahead.Match("foo"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        // The complete single-literal finder checks both full-match endpoints
        // and its positive suffix without preparing a second scalar program.
        Assert.False(lookahead.DebugIsScalarFullProgramValueCreated);
    }

    [Fact]
    public void LazyUtf8FullMatcherSupportsConcurrentNativeAndScalarFirstUse()
    {
        var native = new Utf8PythonRegex("ab[0-9]{2}");
        Assert.False(native.DebugIsScalarFullProgramValueCreated);

        Parallel.For(0, 32, iteration =>
        {
            switch (iteration % 3)
            {
                case 0:
                    Assert.True(native.Match("ab12"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
                    break;
                case 1:
                    Utf8PythonRangeMatch snapshot1 = default;
                    using (var captureCursor1 = native.CreateMatchCursor(("ab12"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
                    {
                        captureCursor1.MoveNext(out snapshot1);
                    }
                    Assert.True(snapshot1.Success);
                    break;
                default:
                    Utf8PythonRangeMatch snapshot2 = default;
                    using (var captureCursor2 = native.CreateMatchCursor(("ab12"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
                    {
                        captureCursor2.MoveNext(out snapshot2);
                    }
                    Assert.True(snapshot2.Success);
                    break;
            }
        });

        Assert.True(native.DebugIsScalarFullProgramValueCreated);

        // The anchored search shape shares a lazily created scalar full
        // matcher across concurrent callers.
        var managed = new Utf8PythonRegex(@"^ab[0-9]{2}");
        Assert.False(managed.DebugIsScalarFullProgramValueCreated);
        Parallel.For(0, 32, _ => Assert.True(managed.Match("ab12"u8,mode: Utf8PythonMatchMode.FullMatch).Success));
        Assert.True(managed.DebugIsScalarFullProgramValueCreated);
    }

    [Fact]
    public void MatchReturnsByteAlignedUtf8ValueMatch()
    {
        var regex = new Utf8PythonRegex("café");

        var match = regex.Match("xxcaféyy"u8,mode: Utf8PythonMatchMode.Search);

        Assert.True(match.Success);
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

        Assert.True(regex.Match("foo"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("foo xx"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void DotAllFullMatchPreservesWholeSuffixCoordinates()
    {
        var regex = new Utf8PythonRegex("start.*end", PythonReCompileOptions.DotAll);
        var input = "πstart\n東京𝒜end"u8.ToArray();
        var startOffsetInBytes = "π"u8.Length;

        var match = regex.Match(input,startOffsetInBytes,mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(match.Success);
        Assert.Equal(startOffsetInBytes, match.StartOffsetInBytes);
        Assert.Equal(input.Length, match.EndOffsetInBytes);
        Assert.Equal("start\n東京𝒜end", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.True(regex.Match("startend"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match("start\0end"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
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

        Assert.False(regex.Match("xstartend"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("startendx"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("start-END"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("starend"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(overlapping.Match("aba"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(overlapping.Match("abaaba"u8,mode: Utf8PythonMatchMode.FullMatch).Success);


        Assert.False(new Utf8PythonRegex("start.*end").Match("start\nend"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(ignoreCase.Match("START\nEND"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot3 = default;
        using (var captureCursor3 = captured.CreateMatchCursor(("startmiddleend"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor3.MoveNext(out snapshot3);
        }
        Assert.Equal("middle", System.Text.Encoding.UTF8.GetString((snapshot3.Groups[1].Success ? snapshot3.GetGroupMemory(snapshot3.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.True(reluctant.Match("start😀endend"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(finiteTimeout.Match("start\n😀end"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void FullMatchUsesOwnedProgramForFallbackClassifiedPattern()
    {
        var regex = new Utf8PythonRegex("(?:Шерлок )+");
        var input = "xxШерлок Шерлок "u8.ToArray();

        var match = regex.Match(input,startOffsetInBytes: 2,mode: Utf8PythonMatchMode.FullMatch);

        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(input.Length, match.EndOffsetInBytes);
        Assert.Equal("Шерлок Шерлок ", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.False(regex.Match("xxШерлок x"u8,startOffsetInBytes: 2,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void OwnedFullMatchPreservesDetailedCapturesAndInputContracts()
    {
        var regex = new Utf8PythonRegex("(?P<word>Шерлок )+");
        var input = "xxШерлок Шерлок "u8.ToArray();

        Utf8PythonRangeMatch detailed = default;

        using (var captureCursor4 = regex.CreateMatchCursor(input, startOffsetInBytes: 2, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor4.MoveNext(out detailed);
        }
        Utf8PythonRangeMatch data = default;
        using (var captureCursor5 = regex.CreateMatchCursor(input, startOffsetInBytes: 2, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor5.MoveNext(out data);
        }

        Assert.True(detailed.Success);
        Assert.Equal("Шерлок Шерлок ", System.Text.Encoding.UTF8.GetString(detailed.GetValueMemory().Span));
        Assert.True(detailed.TryGetFirstSetGroup("word", out var word));
        Assert.Equal("Шерлок ", System.Text.Encoding.UTF8.GetString((word.Success ? detailed.GetGroupMemory(word.Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.True(data.Success);
        Assert.Equal("Шерлок Шерлок ", System.Text.Encoding.UTF8.GetString(data.GetValueMemory().Span));
        Utf8PythonRangeMatch snapshot6 = default;
        using (var captureCursor6 = regex.CreateMatchCursor(("xxШерлок x"u8).ToArray(), startOffsetInBytes: 2, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor6.MoveNext(out snapshot6);
        }
        Assert.False(snapshot6.Success);


    }

    [Fact]
    public void OwnedFullMatchHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");

        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Match(input,mode: Utf8PythonMatchMode.FullMatch));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() =>
        {
            Utf8PythonRangeMatch snapshot7 = default;
            using (var captureCursor7 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
            {
                captureCursor7.MoveNext(out snapshot7);
            }

            _ = snapshot7;
        });
    }
    [Fact]
    public void ScalarGlobalLoopsHonorTimeout()
    {
        // One owned invocation keeps the operation deadline across all discovery steps; CPython defines no timeout oracle.
        var regex = new Utf8PythonRegex("(a*)+$", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");
        // R03 anchor slice: the empty-capable anchor shape now counts on the
        // owned slice, which maps its deadline to the same timeout identity.
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Count(input));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)));
    }



    [Fact]
    public void SearchDetailedExposesNamedCapture()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        Utf8PythonRangeMatch match = default;

        using (var captureCursor8 = regex.CreateMatchCursor(("xx foo-foo yy"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor8.MoveNext(out match);
        }

        Assert.True(match.Success);
        Assert.True(match.TryGetFirstSetGroup("word", out var group));
        Assert.True(group.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString((group.Success ? match.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void DetailedApisRemainCorrectWithOwnedScalarDiscovery()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        Utf8PythonRangeMatch search = default;

        using (var captureCursor9 = regex.CreateMatchCursor(("xx foo-foo yy"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor9.MoveNext(out search);
        }
        Utf8PythonRangeMatch match = default;
        using (var captureCursor10 = regex.CreateMatchCursor(("foo-foo yy"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor10.MoveNext(out match);
        }
        Utf8PythonRangeMatch full = default;
        using (var captureCursor11 = regex.CreateMatchCursor(("foo-foo"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor11.MoveNext(out full);
        }

        Assert.True(search.Success);
        Assert.True(match.Success);
        Assert.True(full.Success);
        Assert.Equal("foo-foo", System.Text.Encoding.UTF8.GetString(full.GetValueMemory().Span));
        Assert.True(search.TryGetFirstSetGroup("word", out var word));
        Assert.True(word.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString((word.Success ? search.GetGroupMemory(word.Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void Utf8BackendReportsContiguousByteRangesForScalarMatches()
    {
        // `.` consumes one scalar, so the native backend reports the
        // whole-scalar byte range. The previous revision pinned the half-match
        // contract (no contiguous range, throwing value bytes); scalar tokenization
        // eliminated that premise, and invention would now fail these pins.
        var regex = new Utf8PythonRegex(".");

        var search = regex.Match("𝒜"u8,mode: Utf8PythonMatchMode.Search);
        Utf8PythonRangeMatch detailed = default;
        using (var captureCursor12 = regex.CreateMatchCursor(("𝒜"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor12.MoveNext(out detailed);
        }

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(search.Success);
        Assert.Equal(0, search.StartOffsetInBytes);
        Assert.Equal(4, search.EndOffsetInBytes);
        Assert.Equal("𝒜"u8.ToArray(), search.GetValueBytes().ToArray());
        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString(search.GetValueBytes()));

        Assert.True(detailed.Success);
        Assert.True(detailed.TryGetGroup(0, out var group));
        Assert.True(group.Success);
        Assert.Equal(0, group.StartOffsetInBytes);
        Assert.Equal(4, group.EndOffsetInBytes);
        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString((group.Success ? detailed.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void BorrowedValueHostDecodingMatchesOwnedUnicodeProjection()
    {
        var regex = new Utf8PythonRegex(".");

        var search = regex.Match("𝒜"u8,mode: Utf8PythonMatchMode.Search);
        Utf8PythonRangeMatch detailed = default;
        using (var captureCursor13 = regex.CreateMatchCursor(("𝒜"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor13.MoveNext(out detailed);
        }
        Utf8PythonRangeMatch detailedData = default;
        using (var captureCursor14 = regex.CreateMatchCursor(("𝒜"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor14.MoveNext(out detailedData);
        }

        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString(search.GetValueBytes()));
        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString(detailed.GetValueMemory().Span));
        Assert.True(detailed.TryGetGroup(0, out var whole));
        Assert.True(whole.Success);
        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString((whole.Success ? detailed.GetGroupMemory(whole.Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString(detailedData.GetValueMemory().Span));
        Assert.True(detailedData.TryGetGroup(0, out var dataGroup));
        Assert.Equal("𝒜", System.Text.Encoding.UTF8.GetString((dataGroup.Success ? detailedData.GetGroupMemory(dataGroup.Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void FindAllReturnsAllNonOverlappingMatches()
    {
        var regex = new Utf8PythonRegex("foo");
        var wholeMatches22 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor22 = regex.CreateMatchCursor(("foo x foo"u8).ToArray(), 0, default))
        {
            while (rangeCursor22.MoveNext(out var rangeMatch22))
            {
                wholeMatches22.Add(rangeMatch22);
            }
        }

        var matches = wholeMatches22.ToArray();

        Assert.Equal(2, matches.Length);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(matches[0].GetValueMemory().Span));
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString(matches[1].GetValueMemory().Span));
    }

    [Fact]
    public void FindAllCanUseUtf8RegexBackendForNonEmptyTranslatedPatterns()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");
        var wholeMatches23 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor23 = regex.CreateMatchCursor(("xx foo-foo yy foo-foo"u8).ToArray(), 3, default))
        {
            while (rangeCursor23.MoveNext(out var rangeMatch23))
            {
                wholeMatches23.Add(rangeMatch23);
            }
        }

        var matches = wholeMatches23.ToArray();

        Assert.Equal(2, matches.Length);
        Assert.Equal("foo-foo", System.Text.Encoding.UTF8.GetString(matches[0].GetValueMemory().Span));
        Assert.Equal("foo-foo", System.Text.Encoding.UTF8.GetString(matches[1].GetValueMemory().Span));
        Assert.Equal(3, matches[0].Value.StartOffsetInBytes);
    }

    [Fact]
    public void FindAllCanUseUtf8RegexBackendForPrefixFreeLiteralAlternation()
    {
        var regex = new Utf8PythonRegex(@"foo|bar");
        var wholeMatches24 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor24 = regex.CreateMatchCursor(("xxfoobaryybar"u8).ToArray(), 0, default))
        {
            while (rangeCursor24.MoveNext(out var rangeMatch24))
            {
                wholeMatches24.Add(rangeMatch24);
            }
        }

        var matches = wholeMatches24.ToArray();

        Assert.Equal(["foo", "bar", "bar"], matches.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)).ToArray());
    }

    [Fact]
    public void ByteFindAllUsesFullMatchShapeWithoutCaptures()
    {
        var regex = new Utf8PythonRegex("a");

        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8("axa"u8));

        Assert.Equal(Utf8PythonFindAllShape.FullMatch, result.Shape);
        Assert.Equal(["a", "a"], result.ScalarValues);
        Assert.Empty(result.TupleValues);
    }

    [Fact]
    public void DirectShapedFindAllProjectsUnicodeAndOwnsReturnedBytes()
    {
        var regex = new Utf8PythonRegex("é+");
        var input = "xéé yé"u8.ToArray();
        var wholeMatches25 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor25 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor25.MoveNext(out var rangeMatch25))
            {
                wholeMatches25.Add(rangeMatch25);
            }
        }

        var structural = wholeMatches25.ToArray();
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
        var utf8 = regex.FindAllToUtf8(input);

        Assert.Equal(["éé", "é"], structural.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
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
                var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
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

        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FindAllToUtf8(input));
    }

    [Fact]
    public void EmptyThenNonEmptyCompanionHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("x*|(?:(a+)+$)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(1));
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");

        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.FindAllToUtf8(input));
    }

    [Fact]
    public void ByteFindAllUsesSingleGroupShapeWithEmptyStringForUnsetOptionalGroups()
    {
        var regex = new Utf8PythonRegex("(a)?b");

        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8("b ab b"u8));

        Assert.Equal(Utf8PythonFindAllShape.SingleGroup, result.Shape);
        Assert.Equal(["", "a", ""], result.ScalarValues);
        Assert.Empty(result.TupleValues);
    }

    [Fact]
    public void ByteFindAllPreservesExactOptionalCaptureValuesAndByteStart()
    {
        var numbered = new Utf8PythonRegex("pre(ab)?c");
        var named = new Utf8PythonRegex("(?P<letter>é)?x");
        var numberedInput = "π preabc prec preabc"u8;
        var namedInput = "skip x éx x"u8;

        var numberedResult = PythonReFindAllAssert.Decode(numbered.FindAllToUtf8(
            numberedInput,
            startOffsetInBytes: "π "u8.Length));
        var namedResult = PythonReFindAllAssert.Decode(named.FindAllToUtf8(
            namedInput,
            startOffsetInBytes: "skip "u8.Length));

        Assert.Equal(["ab", "", "ab"], numberedResult.ScalarValues);
        Assert.Equal(["", "é", ""], namedResult.ScalarValues);
    }

    [Fact]
    public void ByteFindAllPreservesSubjectTextForNonExactOptionalCaptures()
    {
        var ignoreCase = new Utf8PythonRegex("(a)?b", PythonReCompileOptions.IgnoreCase);
        var characterClass = new Utf8PythonRegex("([ab])?c");
        var reluctant = new Utf8PythonRegex("(a)??a");

        Assert.Equal(["A", ""], PythonReFindAllAssert.Decode(ignoreCase.FindAllToUtf8("AB b"u8)).ScalarValues);
        Assert.Equal(["a", "b", ""], PythonReFindAllAssert.Decode(characterClass.FindAllToUtf8("ac bc c"u8)).ScalarValues);
        Assert.Equal(["", "", ""], PythonReFindAllAssert.Decode(reluctant.FindAllToUtf8("aa a"u8)).ScalarValues);
    }

    [Fact]
    public void ByteFindAllUsesTupleShapeForMultipleCaptures()
    {
        var regex = new Utf8PythonRegex("(a)|(x)");

        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8("axa"u8));

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
        var repeated = PythonReFindAllAssert.Decode(new Utf8PythonRegex("(a)+").FindAllToUtf8("aaa aa"u8));
        Assert.Equal(Utf8PythonFindAllShape.SingleGroup, repeated.Shape);
        Assert.Equal(["a", "a"], repeated.ScalarValues);

        var progressive = PythonReFindAllAssert.Decode(new Utf8PythonRegex(@"(\b)|(\w+)").FindAllToUtf8("a::bc"u8));
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

        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
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

        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes));

        Assert.Equal(Utf8PythonFindAllShape.GroupTuple, result.Shape);
        Assert.Equal([["éé", "𝒜𝒜"]], result.TupleValues);
    }

    [Fact]
    public void ShapedFindAllHonorsByteStartAfterUnicodePrefix()
    {
        var regex = new Utf8PythonRegex("[a-z]+");
        var input = "é ignored alpha beta"u8;
        var startOffsetInBytes = "é ignored "u8.Length;

        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes));
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

        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8("a::bc"u8));
        var utf8 = regex.FindAllToUtf8("a::bc"u8);

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
        var wholeMatches26 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor26 = regex.CreateMatchCursor(("y"u8).ToArray(), 0, default))
        {
            while (rangeCursor26.MoveNext(out var rangeMatch26))
            {
                wholeMatches26.Add(rangeMatch26);
            }
        }

        Assert.Equal(expected, wholeMatches26.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(expected, PythonReFindAllAssert.Decode(regex.FindAllToUtf8("y"u8)).ScalarValues);
        Assert.Equal(
            expected,
            regex.FindAllToUtf8("y"u8).ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(("y"u8).ToArray(), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        Assert.Equal(expected, collectedRanges1.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(3, regex.Count("y"u8));
        Assert.Equal("---", System.Text.Encoding.UTF8.GetString(regex.Replace("y"u8, "-")));

        var subn = PythonReSubstitutionAssert.Decode(regex.Subn("y"u8, "-"));
        Assert.Equal("---", subn.ResultText);
        Assert.Equal(3, subn.ReplacementCount);
        Assert.Equal(new string?[] { "", "", "", "" }, PythonReSplitAssert.Decode("y"u8, regex.SplitRanges("y"u8, 0, 0, default)));

        var empty = new Utf8PythonRegex("");
        var wholeMatches27 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor27 = empty.CreateMatchCursor(("𝒜"u8).ToArray(), 0, default))
        {
            while (rangeCursor27.MoveNext(out var rangeMatch27))
            {
                wholeMatches27.Add(rangeMatch27);
            }
        }
        Assert.Equal(["", ""], wholeMatches27.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = empty.CreateMatchCursor(("𝒜"u8).ToArray(), 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        Assert.Equal(["", ""], collectedRanges2.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(2, empty.Count("𝒜"u8));
    }

    [Fact]
    public void GreedyAsciiLiteralStarFindAllPreservesScalarSafeEmptyProgression()
    {
        var regex = new Utf8PythonRegex("x*");

        Assert.Equal(["", "", "", ""], PythonReFindAllAssert.Decode(regex.FindAllToUtf8("yyy"u8)).ScalarValues);
        Assert.Equal(["xx", "", "xx", ""], PythonReFindAllAssert.Decode(regex.FindAllToUtf8("xxyxx"u8)).ScalarValues);
        Assert.Equal(["", "x", "", ""], PythonReFindAllAssert.Decode(regex.FindAllToUtf8("πx𝒜"u8)).ScalarValues);
        Assert.Equal(
            ["x", "", ""],
            PythonReFindAllAssert.Decode(regex.FindAllToUtf8("πx𝒜"u8, "π"u8.Length)).ScalarValues);
        Assert.Equal([""], PythonReFindAllAssert.Decode(regex.FindAllToUtf8(ReadOnlySpan<byte>.Empty)).ScalarValues);

        Assert.Equal(["X", ""], PythonReFindAllAssert.Decode(new Utf8PythonRegex("x*", PythonReCompileOptions.IgnoreCase)
            .FindAllToUtf8("X"u8)).ScalarValues);
        Assert.Equal(["", "x", "", "x", ""], PythonReFindAllAssert.Decode(new Utf8PythonRegex("x*?")
            .FindAllToUtf8("xx"u8)).ScalarValues);

    }

    [Fact]
    public void LiteralReplacementPreservesGreedyExactRepeatProgression()
    {
        var repeated = new Utf8PythonRegex("x*");
        var grouped = new Utf8PythonRegex("(?:ab)*");

        Assert.Equal("-a-b--d-", System.Text.Encoding.UTF8.GetString(repeated.Replace("abxd"u8, "-")));
        Assert.Equal("-a-b--d-", System.Text.Encoding.UTF8.GetString(repeated.Replace("abxd"u8, "-")));
        var repeatedSubn = PythonReSubstitutionAssert.Decode(repeated.Subn("abxd"u8, "-"));
        Assert.Equal("-a-b--d-", repeatedSubn.ResultText);
        Assert.Equal(5, repeatedSubn.ReplacementCount);
        Assert.Equal("_z__q_", System.Text.Encoding.UTF8.GetString(grouped.Replace("zababq"u8, "_")));
        Assert.Equal(
            "π-a-bxd",
            System.Text.Encoding.UTF8.GetString(repeated.Replace("πabxd"u8, "-", count: 2, startOffsetInBytes: "π"u8.Length)));
    }

    [Fact]
    public void LiteralReplacementPreservesAmbiguousEmptyProgression()
    {
        var alternation = new Utf8PythonRegex("x*|y");
        var reluctant = new Utf8PythonRegex("x*?");

        Assert.Equal("---", System.Text.Encoding.UTF8.GetString(alternation.Replace("y"u8, "-")));
        Assert.Equal("-----", System.Text.Encoding.UTF8.GetString(reluctant.Replace("xx"u8, "-")));
    }

    [Fact]
    public void FindAllStringsProjectsSingleTrailingCaptureFromValueMatches()
    {
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        var input = "skip item-12 item-345 item-6"u8;

        var all = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
        var afterFirst = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes: "skip item-12 "u8.Length));

        Assert.Equal(["12", "345", "6"], all.ScalarValues);
        Assert.Equal(["345", "6"], afterFirst.ScalarValues);
    }

    [Fact]
    public void ExactLiteralByteValuesPreserveSuffixOffsets()
    {
        var regex = new Utf8PythonRegex("(?:Шерлок)");
        var input = "skip Шерлок Шерлок Шерлок"u8;

        var all = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
        var afterFirst = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, "skip Шерлок "u8.Length));

        Assert.Equal(["Шерлок", "Шерлок", "Шерлок"], all.ScalarValues);
        Assert.Equal(["Шерлок", "Шерлок"], afterFirst.ScalarValues);
        Assert.Equal(
            ["Шерлок", "Шерлок", "Шерлок"],
            regex.FindAllToUtf8(input).ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void ExactLiteralFindAllFromOffsetPreservesValues()
    {
        var regex = new Utf8PythonRegex("item");
        var input = "item skip item item"u8;
        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, "item skip "u8.Length));

        Assert.Equal(["item", "item"], result.ScalarValues);


    }

    [Fact]
    public void MatchKeepsAsciiLiteralPrefixDigitSemantics()
    {
        var regex = new Utf8PythonRegex("header:[0-9]+");
        var match = regex.Match("header:12345 café"u8);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(12, match.EndOffsetInBytes);
        Assert.Equal("header:12345", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.False(regex.Match("header: tail"u8).Success);
        Assert.False(regex.Match("other:123"u8).Success);

    }

    [Fact]
    public void MatchKeepsUnicodeAndGeneralPrefixDigitSemantics()
    {
        var regex = new Utf8PythonRegex("header:[0-9]+");
        var ignoreCase = new Utf8PythonRegex("header:[0-9]+", PythonReCompileOptions.IgnoreCase);
        var reluctant = new Utf8PythonRegex("header:[0-9]+?");
        var captured = new Utf8PythonRegex("header:([0-9]+)");
        var finiteTimeout = new Utf8PythonRegex("header:[0-9]+", PythonReCompileOptions.None, TimeSpan.FromSeconds(1));
        Assert.Equal("HEADER:123", System.Text.Encoding.UTF8.GetString(ignoreCase.Match("HEADER:123"u8).GetValueBytes()));
        Utf8PythonRangeMatch snapshot15 = default;
        using (var captureCursor15 = captured.CreateMatchCursor(("header:123"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor15.MoveNext(out snapshot15);
        }
        Assert.Equal("123", System.Text.Encoding.UTF8.GetString((snapshot15.Groups[1].Success ? snapshot15.GetGroupMemory(snapshot15.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("header:123", System.Text.Encoding.UTF8.GetString(finiteTimeout.Match("header:123"u8).GetValueBytes()));
        Assert.Equal(1, reluctant.Match("header:123"u8).EndOffsetInBytes - "header:"u8.Length);

        var offsetMatch = regex.Match("skipheader:123 tail"u8,"skip"u8.Length);
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

        Assert.Equal(["SHERLOCK", "Sherlock"], PythonReFindAllAssert.Decode(globalIgnoreCase.FindAllToUtf8(input)).ScalarValues);
        Assert.Equal(["SHERLOCK", "Sherlock"], PythonReFindAllAssert.Decode(scopedIgnoreCase.FindAllToUtf8(input)).ScalarValues);
        Assert.Equal(["sherlock"], PythonReFindAllAssert.Decode(captured.FindAllToUtf8("sherlock"u8)).ScalarValues);
        Assert.Equal(["alpha", "beta"], PythonReFindAllAssert.Decode(characterClass.FindAllToUtf8("alpha beta"u8)).ScalarValues);
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

        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(System.Text.Encoding.UTF8.GetBytes(input)));

        Assert.Equal([first, second], result.ScalarValues);
    }

    [Fact]
    public void FindAllStringsProjectsSeparatedCaptureTuplesFromValueMatches()
    {
        var regex = new Utf8PythonRegex("([a-z]+)-([0-9]+)", PythonReCompileOptions.IgnoreCase);
        var input = "skip abc-12 DEF-345 ghi-6"u8;

        var all = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
        var afterFirst = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes: "skip abc-12 "u8.Length));

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

        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(System.Text.Encoding.UTF8.GetBytes(input)));

        Assert.Equal([[first, second]], result.TupleValues);
    }

    [Fact]
    public void EmptyThenNonEmptyCompanionPreservesCapturesContextAndConcurrentReuse()
    {
        var captured = new Utf8PythonRegex("(?P<word>x*|y)");
        Assert.Equal(["", "y", ""], PythonReFindAllAssert.Decode(captured.FindAllToUtf8("y"u8)).ScalarValues);

        var lookbehind = new Utf8PythonRegex("(?<=a)(?:x*|y)");
        var wholeMatches28 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor28 = lookbehind.CreateMatchCursor(("ay"u8).ToArray(), 1, default))
        {
            while (rangeCursor28.MoveNext(out var rangeMatch28))
            {
                wholeMatches28.Add(rangeMatch28);
            }
        }
        Assert.Equal(
            ["", "y"],
            wholeMatches28.ToArray()
                .Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span))
                .ToArray());

        var anchored = new Utf8PythonRegex("^x*|y");
        var wholeMatches29 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor29 = anchored.CreateMatchCursor(("y"u8).ToArray(), 0, default))
        {
            while (rangeCursor29.MoveNext(out var rangeMatch29))
            {
                wholeMatches29.Add(rangeMatch29);
            }
        }
        Assert.Equal(["", "y"], wholeMatches29.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());

        var unicodeInput = "éy"u8.ToArray();
        var startOffsetInBytes = "é"u8.Length;
        Parallel.For(0, 16, _ =>
        {
            var wholeMatches30 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor30 = captured.CreateMatchCursor(unicodeInput, startOffsetInBytes, default))
            {
                while (rangeCursor30.MoveNext(out var rangeMatch30))
                {
                    wholeMatches30.Add(rangeMatch30);
                }
            }
            Assert.Equal(
                ["", "y", ""],
                wholeMatches30.ToArray()
                    .Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span))
                    .ToArray());
        });
    }

    [Fact]
    public void ShapedFindAllPreservesManagedPrefixAlternationOrder()
    {
        var regex = new Utf8PythonRegex("foo|foobar");
        var wholeMatches31 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor31 = regex.CreateMatchCursor(("foobar foo"u8).ToArray(), 0, default))
        {
            while (rangeCursor31.MoveNext(out var rangeMatch31))
            {
                wholeMatches31.Add(rangeMatch31);
            }
        }

        var matches = wholeMatches31.ToArray();
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8("foobar foo"u8));
        var utf8 = regex.FindAllToUtf8("foobar foo"u8);
        var collectedRanges3 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor3 = regex.CreateMatchCursor(("foobar foo"u8).ToArray(), 0, default))
        {
            while (rangeCursor3.MoveNext(out var rangeMatch3))
                collectedRanges3.Add(rangeMatch3);
        }
        var detailed = collectedRanges3.ToArray();

        Assert.Equal(["foo", "foo"], matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(["foo", "foo"], strings.ScalarValues);
        Assert.Equal(
            strings.ScalarValues,
            utf8.ScalarValues.Select(System.Text.Encoding.UTF8.GetString).ToArray());
        Assert.Equal(["foo", "foo"], detailed.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
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
        var wholeMatches32 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor32 = regex.CreateMatchCursor((input).ToArray(), startOffsetInBytes, default))
        {
            while (rangeCursor32.MoveNext(out var rangeMatch32))
            {
                wholeMatches32.Add(rangeMatch32);
            }
        }

        var matches = wholeMatches32.ToArray();
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes));
        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);
        var collectedRanges4 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor4 = regex.CreateMatchCursor((input).ToArray(), startOffsetInBytes, default))
        {
            while (rangeCursor4.MoveNext(out var rangeMatch4))
                collectedRanges4.Add(rangeMatch4);
        }
        var detailed = collectedRanges4.ToArray();

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
        var wholeMatches33 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor33 = regex.CreateMatchCursor((input).ToArray(), startOffsetInBytes, default))
        {
            while (rangeCursor33.MoveNext(out var rangeMatch33))
            {
                wholeMatches33.Add(rangeMatch33);
            }
        }

        var matches = wholeMatches33.ToArray();
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes));
        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);

        Assert.Equal(expected, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
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
        var collectedRanges5 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor5 = regex.CreateMatchCursor(("xx foo-foo yy foo-foo"u8).ToArray(), 0, default))
        {
            while (rangeCursor5.MoveNext(out var rangeMatch5))
                collectedRanges5.Add(rangeMatch5);
        }

        var matches = collectedRanges5.ToArray();

        Assert.Equal(2, matches.Length);
        Assert.Equal("foo-foo", System.Text.Encoding.UTF8.GetString(matches[0].GetValueMemory().Span));
        Assert.True(matches[0].TryGetFirstSetGroup("word", out var firstWord));
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString((firstWord.Success ? matches[0].GetGroupMemory(firstWord.Number) : default).Span));
        Assert.Equal("foo-foo", System.Text.Encoding.UTF8.GetString(matches[1].GetValueMemory().Span));
    }

    [Fact]
    public void FindIterDetailedReusesUnicodeProjectionAfterStartOffset()
    {
        var regex = new Utf8PythonRegex("(é+)-(𝒜𝒜|𝒜)");
        var input = System.Text.Encoding.UTF8.GetBytes("é-𝒜 xx éé-𝒜𝒜");
        var startOffsetInBytes = "é-𝒜 xx "u8.Length;
        var collectedRanges6 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor6 = regex.CreateMatchCursor(input, startOffsetInBytes, default))
        {
            while (rangeCursor6.MoveNext(out var rangeMatch6))
                collectedRanges6.Add(rangeMatch6);
        }

        var matches = collectedRanges6.ToArray();

        var match = Assert.Single(matches);
        Assert.Equal(startOffsetInBytes, match.Value.StartOffsetInBytes);
        Assert.Equal("éé-𝒜𝒜", System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span));
        Assert.Equal("éé", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal("𝒜𝒜", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
    }

    [Fact]
    public void FindIterDetailedKeepsGeneralEmptyMatchTraversal()
    {
        var collectedRanges7 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor7 = new Utf8PythonRegex("x*").CreateMatchCursor(("xx"u8).ToArray(), 0, default))
        {
            while (rangeCursor7.MoveNext(out var rangeMatch7))
                collectedRanges7.Add(rangeMatch7);
        }
        var matches = collectedRanges7.ToArray();

        Assert.Equal(["xx", ""], matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
    }

    [Fact]
    public void ScalarReplacementKeepsPythonGroupSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)");

        var replaced = System.Text.Encoding.UTF8.GetString(regex.Replace("foo x foo"u8, "<\\g<word>>"));

        Assert.Equal("<foo> x <foo>", replaced);
    }

    [Fact]
    public void ExactLiteralReplacementUsesCountedUtf8PathForUnlimitedZeroOffsetOperations()
    {
        var regex = new Utf8PythonRegex("cat");
        var input = "é cat fox cat 𝒜"u8;
        const string expected = "é $🐈 fox $🐈 𝒜";

        var replacedText = System.Text.Encoding.UTF8.GetString(regex.Replace(input, "$🐈"));
        var replacedUtf8 = regex.Replace(input, "$🐈");
        var subnText = PythonReSubstitutionAssert.Decode(regex.Subn(input, "$🐈"));
        var subnUtf8 = regex.Subn(input, "$🐈");

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

        Assert.Equal("dog cat cat", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "dog", count: 1)));
        Assert.Equal(
            "🐈 🐈 cat",
            System.Text.Encoding.UTF8.GetString(regex.Replace(input, "🐈", count: 2)));
        var subn = PythonReSubstitutionAssert.Decode(regex.Subn(input, "dog", count: 2));
        Assert.Equal(("dog dog cat", 2), (subn.ResultText, subn.ReplacementCount));
        Assert.Equal("dog dog dog", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "dog", count: 99)));
        Assert.Equal("cat dog dog", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "dog", count: 0, startOffsetInBytes: 4)));
        Assert.Equal("<cat> <cat> <cat>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, @"<\g<0>>")));

    }

    [Fact]
    public void ScalarSubnPreservesUnlimitedReplacement()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = PythonReSubstitutionAssert.Decode(regex.Subn("xx foo-foo yy foo-foo"u8, "<\\g<word>>", count: 0, startOffsetInBytes: 3));

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
    public void SubnWithSingleReplacementLimitReportsExactCount()
    {
        // count 1 needs only existence, not a full-subject count.
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = PythonReSubstitutionAssert.Decode(regex.Subn("xx foo-foo yy foo-foo"u8, "<\\g<word>>", count: 1, startOffsetInBytes: 3));

        Assert.Equal("xx <foo> yy foo-foo", replaced.ResultText);
        Assert.Equal(1, replaced.ReplacementCount);

        var missed = PythonReSubstitutionAssert.Decode(regex.Subn("xx no-hit yy"u8, "<\\g<word>>", count: 1, startOffsetInBytes: 0));

        Assert.Equal("xx no-hit yy", missed.ResultText);
        Assert.Equal(0, missed.ReplacementCount);
    }

    [Fact]
    public void SubnManualLoopHonorsLimitsWithEmptyMatches()
    {
        // the managed progression cursor owns empty-match advancement in Subn; oracle is CPython 3.13.2 re.subn.
        var optional = new Utf8PythonRegex("(a)?");

        var one = PythonReSubstitutionAssert.Decode(optional.Subn("ab"u8, "-", 1));
        Assert.Equal("-b", one.ResultText);
        Assert.Equal(1, one.ReplacementCount);

        var all = PythonReSubstitutionAssert.Decode(optional.Subn("ab"u8, "-", 0));
        Assert.Equal("--b-", all.ResultText);
        Assert.Equal(3, all.ReplacementCount);

        var alternation = new Utf8PythonRegex("a*|b");

        var first = PythonReSubstitutionAssert.Decode(alternation.Subn("ba"u8, "-", 1));
        Assert.Equal("-ba", first.ResultText);
        Assert.Equal(1, first.ReplacementCount);

        var every = PythonReSubstitutionAssert.Decode(alternation.Subn("ba"u8, "-", 0));
        Assert.Equal("----", every.ResultText);
        Assert.Equal(4, every.ReplacementCount);
    }

    [Fact]
    public void SplitManagedLoopHonorsLimitsWithEmptyMatches()
    {
        // the managed progression cursor owns empty-match advancement in Split; oracle is CPython 3.13.2 re.split.
        var star = new Utf8PythonRegex("x*");

        Assert.Equal(["", "a", "", "b", "", "c", ""], PythonReSplitAssert.Decode("axbxc"u8, star.SplitRanges("axbxc"u8, 0, 0, default)));
        Assert.Equal(["", "axbxc"], PythonReSplitAssert.Decode("axbxc"u8, star.SplitRanges("axbxc"u8, 1, 0, default)));
        Assert.Equal(["", "a", "bxc"], PythonReSplitAssert.Decode("axbxc"u8, star.SplitRanges("axbxc"u8, 2, 0, default)));

        var optional = new Utf8PythonRegex("(x)?");

        Assert.Equal<string?[]>(["", null, "a", null, "b", null, ""], PythonReSplitAssert.Decode("ab"u8, optional.SplitRanges("ab"u8, 0, 0, default)));
        Assert.Equal<string?[]>(["", null, "ab"], PythonReSplitAssert.Decode("ab"u8, optional.SplitRanges("ab"u8, 1, 0, default)));
    }

    [Fact]
    public void BytesSubnHonorsLimitsWithEmptyMatches()
    {
        var optional = new Utf8PythonRegex("(a)?");

        var one = optional.Subn("ab"u8, "-", 1, 0);
        Assert.Equal("-b", System.Text.Encoding.UTF8.GetString(one.ResultBytes));
        Assert.Equal(1, one.ReplacementCount);

        var all = optional.Subn("ab"u8, "-", 0, 0);
        Assert.Equal("--b-", System.Text.Encoding.UTF8.GetString(all.ResultBytes));
        Assert.Equal(3, all.ReplacementCount);
    }

    [Fact]
    public void TemplateSubnHonorsLimits()
    {
        // non-empty-matching patterns take the single-pass cursor template route; oracle is CPython 3.13.2 re.subn.
        var words = new Utf8PythonRegex("([a-z]+)");

        var all = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>", 0, 0));
        Assert.Equal("<cat> <dog>", all.ResultText);
        Assert.Equal(2, all.ReplacementCount);

        var one = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>", 1, 0));
        Assert.Equal("<cat> dog", one.ResultText);
        Assert.Equal(1, one.ReplacementCount);

        var offset = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>", 0, 4));
        Assert.Equal("cat <dog>", offset.ResultText);
        Assert.Equal(1, offset.ReplacementCount);

        var capped = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>", 2, 0));
        Assert.Equal("<cat> <dog>", capped.ResultText);
        Assert.Equal(2, capped.ReplacementCount);

        var over = PythonReSubstitutionAssert.Decode(words.Subn("cat"u8, "<\\1>", 5, 0));
        Assert.Equal("<cat>", over.ResultText);
        Assert.Equal(1, over.ReplacementCount);

        var groups = new Utf8PythonRegex("([a-z]+)-([0-9]+)");
        var swapped = groups.Subn("ab-12 cd-34"u8, "\\2:\\1", 0, 0);
        Assert.Equal("12:ab 34:cd", System.Text.Encoding.UTF8.GetString(swapped.ResultBytes));
        Assert.Equal(2, swapped.ReplacementCount);
    }

    [Fact]
    public void ManagedCountProgressionMatchesCpython()
    {
        // the managed progression cursor owns empty-match advancement; oracle is CPython 3.13.2 re.findall length.
        Assert.Equal(6, new Utf8PythonRegex("x*").Count("axbxc"u8));
        Assert.Equal(4, new Utf8PythonRegex("x*").Count("a\U0001F600b"u8));
        Assert.Equal(3, new Utf8PythonRegex("(a)?").Count("ab"u8));
        Assert.Equal(3, new Utf8PythonRegex("x*").Count("xax"u8, 1));
        Assert.Equal(2, new Utf8PythonRegex("a*").Count("aa"u8, 1));
    }

    [Fact]
    public void ReplacementPlansArePreparedOncePerTemplate()
    {
        // bounded template preparation shares one parsed plan.
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("xx <foo> yy <foo>", System.Text.Encoding.UTF8.GetString(regex.Replace("xx foo-foo yy foo-foo"u8, "<\\g<word>>")));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("xx <foo> yy <foo>", System.Text.Encoding.UTF8.GetString(regex.Replace("xx foo-foo yy foo-foo"u8, "<\\g<word>>")));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("xx - yy -", System.Text.Encoding.UTF8.GetString(regex.Replace("xx foo-foo yy foo-foo"u8, "-")));
        Assert.Equal(2, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public void InvalidReplacementTemplateIsNeverRetained()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.Throws<PythonRePatternException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("foo"u8, "\\")));
        Assert.Throws<PythonRePatternException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("foo"u8, "\\")));
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public void ScalarReplacementPreservesAlternationOrder()
    {
        var regex = new Utf8PythonRegex(@"(?:b)|(?::+)");

        var replaced = System.Text.Encoding.UTF8.GetString(regex.Replace(":a:b::c"u8, "-"));

        Assert.Equal("-a---c", replaced);
    }

    [Fact]
    public void DetailedConsumersPreserveMixedOptionalCapturesAfterUnicodeByteStart()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>[a-z]+)(?:-([0-9]+))?");
        var input = "π skip cat dog-12"u8.ToArray();
        var startOffsetInBytes = "π skip "u8.Length;
        var collectedRanges8 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor8 = regex.CreateMatchCursor(input, startOffsetInBytes, default))
        {
            while (rangeCursor8.MoveNext(out var rangeMatch8))
                collectedRanges8.Add(rangeMatch8);
        }

        var matches = collectedRanges8.ToArray();
        Assert.Equal(2, matches.Length);
        Assert.Equal("cat", Describe(matches[0]));
        Assert.Equal("dog:12", Describe(matches[1]));

        Parallel.For(0, 16, _ =>
        {
            var collected = new List<Utf8PythonRangeMatch>();
            using (var cursor = regex.CreateMatchCursor(input, startOffsetInBytes))
                while (cursor.MoveNext(out var range)) collected.Add(range);
            var values = collected.ToArray();
            Assert.Equal(new[] { "cat", "dog:12" }, values.Select(Describe));
        });

        static string Describe(Utf8PythonRangeMatch match)
        {
            Assert.True(match.TryGetFirstSetGroup("word", out var word));
            Assert.True(match.TryGetGroup(2, out var optional));
            var value = System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(word.Number).Span);
            return optional.Success
                ? $"{value}:{System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(2).Span)}"
                : value;
        }
    }

    [Fact]
    public void ReplacementPreservesBackslashForNonLetterEscapes()
    {
        var regex = new Utf8PythonRegex("x");

        Assert.Equal(@"\&", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\&")));
        Assert.Equal(@"\-", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\-")));
        Assert.Equal("\\ ", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, "\\ ")));
    }

    [Fact]
    public void ReplacementRejectsTrailingBackslash()
    {
        var regex = new Utf8PythonRegex("x");

        var ex = Assert.Throws<PythonRePatternException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, "\\")));

        Assert.Contains("bad escape (end of pattern)", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SplitRangesPreservesSegmentAndCaptureShape()
    {
        var regex = new Utf8PythonRegex(@"(:+)");

        var parts = regex.SplitRanges(":a:b::c"u8);
        var values = PythonReSplitAssert.Decode(":a:b::c"u8, parts);

        Assert.Equal(7, parts.Length);
        Assert.Equal(0, parts[0].Number);
        Assert.Equal("", values[0]);
        Assert.True(parts[1].Success);
        Assert.Equal(1, parts[1].Number);
        Assert.Equal(":", values[1]);
        Assert.Equal(0, parts[2].Number);
        Assert.Equal("a", values[2]);
        Assert.Equal("::", values[5]);
    }

    [Fact]
    public void ScalarSearchHelpersProvideHostFriendlySurface()
    {
        var regex = new Utf8PythonRegex("foo");
        PythonReMatchAssert.Equal("foo", regex.Match("xxfooyy"u8,mode: Utf8PythonMatchMode.Search));
        PythonReMatchAssert.Equal("foo", regex.Match("fooyy"u8));
        PythonReMatchAssert.Equal("foo", regex.Match("foo"u8,mode: Utf8PythonMatchMode.FullMatch));
        Assert.False(regex.Match("xxfoo"u8).Success);
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

        Assert.True(regex.Match("xxfoo   bar yy"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void InlineCommentGroupIsIgnored()
    {
        var regex = new Utf8PythonRegex(@"foo(?# ignore me)bar");

        Assert.True(regex.Match("xxfoobar"u8,mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void ConditionalGroupReferenceIsSupported()
    {
        var regex = new Utf8PythonRegex(@"(foo)?(?(1)bar|baz)");

        Assert.True(regex.Match("foobar"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match("baz"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("bar"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void ConditionalNamedReferenceIsSupported()
    {
        var regex = new Utf8PythonRegex(@"(?P<seen>foo)?(?(seen)bar|baz)");

        Assert.True(regex.Match("foobar"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match("baz"u8,mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void SplitCanUseUtf8RegexBackendForNonEmptyTranslatedPatterns()
    {
        var regex = new Utf8PythonRegex(@"(:+)");

        var parts = PythonReSplitAssert.Decode(":a:b::c"u8, regex.SplitRanges(":a:b::c"u8, 0, 0, default));

        Assert.Equal<string?[]>(["", ":", "a", ":", "b", "::", "c"], parts);
    }

    [Fact]
    public void ScalarSplitPreservesMandatoryOptionalAndEmptyCaptures()
    {
        var mandatory = new Utf8PythonRegex(@"(:+)");
        Assert.Equal<string?[]>(["", ":", "a", ":", "b::c"], PythonReSplitAssert.Decode(":a:b::c"u8, mandatory.SplitRanges(":a:b::c"u8, 2, 0, default)));
        Assert.Equal<string?[]>(["", ":", "a", ":", "b"], PythonReSplitAssert.Decode("π :a:b"u8, mandatory.SplitRanges("π :a:b"u8, 0, "π "u8.Length, default)));

        var nestedMandatory = new Utf8PythonRegex(@"((?:a|b)+)-([0-9]+)");
        Assert.Equal<string?[]>(["x", "ab", "12", "y"], PythonReSplitAssert.Decode("xab-12y"u8, nestedMandatory.SplitRanges("xab-12y"u8, 0, 0, default)));

        var optional = new Utf8PythonRegex(@"(a)?b");
        Assert.Equal<string?[]>(["", null, " ", "a", ""], PythonReSplitAssert.Decode("b ab"u8, optional.SplitRanges("b ab"u8, 0, 0, default)));

        var alternated = new Utf8PythonRegex(@"(b)|(:+)");
        Assert.Equal<string?[]>(["", null, ":", "a", "b", null, ""], PythonReSplitAssert.Decode(":ab"u8, alternated.SplitRanges(":ab"u8, 0, 0, default)));


        var asciiBoundary = new Utf8PythonRegex(@"\b", PythonReCompileOptions.Ascii);
        Assert.Equal<string?[]>(["", "alpha", " ", "beta", ""], PythonReSplitAssert.Decode("alpha beta"u8, asciiBoundary.SplitRanges("alpha beta"u8, 0, 0, default)));
        Assert.Equal<string?[]>(["", "alpha beta"], PythonReSplitAssert.Decode("alpha beta"u8, asciiBoundary.SplitRanges("alpha beta"u8, 1, 0, default)));
        Assert.Equal<string?[]>(["π", "alpha", " 𝒜", "beta", ""], PythonReSplitAssert.Decode("πalpha 𝒜beta"u8, asciiBoundary.SplitRanges("πalpha 𝒜beta"u8, 0, 0, default)));
        var detailed = asciiBoundary.SplitRanges("alpha beta"u8);
        Assert.All(detailed, static item => Assert.Equal(0, item.Number));
        Assert.Equal<string?[]>(["", "alpha", " ", "beta", ""], PythonReSplitAssert.Decode("alpha beta"u8, detailed));
    }

    [Fact]
    public void SplitCanUseUtf8RegexBackendForPrefixFreeLiteralAlternation()
    {
        var regex = new Utf8PythonRegex(@"foo|bar");

        var parts = PythonReSplitAssert.Decode("xxfoobaryybar"u8, regex.SplitRanges("xxfoobaryybar"u8, 0, 0, default));

        Assert.Equal<string?[]>(["xx", "", "yy", ""], parts);
    }

    [Fact]
    public void Utf8PatternConstructorRejectsMalformedUtf8()
    {
        var malformed = new byte[] { 0xC3, 0x28 };

        Assert.Throws<ArgumentException>(() => new Utf8PythonRegex(malformed));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void RoutesRejectStartOffsetsInsideUtf8Scalars(int startOffsetInBytes)
    {
        var direct = new Utf8PythonRegex(".");
        var empty = new Utf8PythonRegex("");
        var input = "aé𝒜z"u8.ToArray();

        Assert.Throws<ArgumentOutOfRangeException>(() => direct.Match(input,startOffsetInBytes,mode: Utf8PythonMatchMode.Search));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.Match(input,startOffsetInBytes,mode: Utf8PythonMatchMode.FullMatch));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Utf8PythonRangeMatch snapshot16 = default;
            using (var captureCursor16 = direct.CreateMatchCursor(input, startOffsetInBytes, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor16.MoveNext(out snapshot16);
            }

            _ = snapshot16;
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.FindAllToUtf8(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.Count(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => System.Text.Encoding.UTF8.GetString(direct.Replace(input, "_", count: 0, startOffsetInBytes: startOffsetInBytes)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PythonReSplitAssert.Decode(input, direct.SplitRanges(input, 0, startOffsetInBytes, default)));
    }
}
