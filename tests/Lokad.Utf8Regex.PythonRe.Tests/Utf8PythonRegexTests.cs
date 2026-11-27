using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class Utf8PythonRegexTests
{
    [Fact]
    public void NullableConstructorDefaultInheritsTimeoutButTypedZeroIsRejected()
    {
        var inherited = new Utf8PythonRegex("a", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = default
        });
        Assert.Equal(Utf8PythonRegex.DefaultMatchTimeout, inherited.MatchTimeout);
        Assert.Equal(inherited.MatchTimeout, new Utf8PythonRegex("a"u8, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = null
        }).MatchTimeout);
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex("a", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = default(TimeSpan)
        }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    [InlineData(int.MaxValue)]
    public void ConstructorRejectsInvalidExplicitMatchTimeout(int milliseconds)
    {
        var timeout = TimeSpan.FromMilliseconds(milliseconds);

        var stringException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8PythonRegex("abc", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = timeout
            }));
        var utf8Exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new Utf8PythonRegex("abc"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = timeout
            }));

        Assert.Equal("policy", stringException.ParamName);
        Assert.Equal("policy", utf8Exception.ParamName);
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
        Assert.True(regex.DebugUsesExactLiteralReplacement);
        Assert.True(regex.Match("foo"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal("foo", PythonReDotNetReference.Render(regex));
        Assert.True(regex.IsMatch("xxfooyy"u8));
        var nonAsciiPrefixMatch = regex.Match("éfoo"u8, mode: Utf8PythonMatchMode.Search);
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

        var search = regex.Match(input, mode: Utf8PythonMatchMode.Search);
        var match = regex.Match("Шерлок далее"u8);

        Assert.True(regex.DebugUsesZeroOffsetUtf8SearchValueFastPath);
        Assert.False(regex.DebugUsesExactLiteralReplacement);
        Assert.True(search.Success);
        Assert.Equal("π "u8.Length, search.StartOffsetInBytes);
        Assert.Equal("π Шерлок"u8.Length, search.EndOffsetInBytes);
        Assert.Equal("Шерлок", System.Text.Encoding.UTF8.GetString(search.GetValueBytes()));
        Assert.True(match.Success);
        Assert.Equal("Шерлок"u8.Length, match.EndOffsetInBytes);
        Assert.False(regex.Match("Ватсон"u8, mode: Utf8PythonMatchMode.Search).Success);
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
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.Multiline);

        Assert.False(regex.DebugUsesExactLiteralReplacement);
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
        var regex = new Utf8PythonRegex(@"\b", options: PythonReCompileOptions.Ascii);

        Assert.Equal(expected, regex.Count(System.Text.Encoding.UTF8.GetBytes(input), startOffsetInBytes));
    }

    [Fact]
    public void AsciiWordBoundaryCountDoesNotSpecializeEmbeddedBoundaryPatterns()
    {
        var regex = new Utf8PythonRegex(@"x\b", options: PythonReCompileOptions.Ascii);

        Assert.Equal(2, regex.Count("x xé"u8));
    }

    [Fact]
    public void Utf8PatternConstructorDecodesUtf8Pattern()
    {
        var regex = new Utf8PythonRegex("café"u8);

        Assert.True(regex.Match("xxcaféyy"u8, mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void NamedCapturingGroupTranslatesToDotNetSyntax()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)");

        Assert.Equal(@"(?<g1>foo)", PythonReDotNetReference.Render(regex));
        Assert.True(regex.TryGetGroupNumber("word"u8, out var groupNumber));
        Assert.Equal(1, groupNumber);
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
        var dotAll = new Utf8PythonRegex(@"a.b", options: PythonReCompileOptions.DotAll);

        Assert.True(dotAll.DebugUsesUtf8RegexBackend);
        Assert.True(dotAll.Match("a\nb"u8, mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void MultilineCanUseUtf8RegexBackend()
    {
        var regex = new Utf8PythonRegex(@"(?m)^foo$", options: PythonReCompileOptions.Multiline);

        Assert.True(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.Match("bar\nfoo\nbaz"u8, mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void VerbosePatternUsesOwnedScalarProgram()
    {
        var regex = new Utf8PythonRegex("""
            (?x)
            foo \s+ bar
            """);

        Assert.False(regex.DebugUsesUtf8RegexBackend);
        Assert.True(regex.Match("xxfoo   baryy"u8, mode: Utf8PythonMatchMode.Search).Success);
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

        Assert.True(named.Match("xx foo-foo yy"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(numeric.Match("zzababzz"u8, mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void AtomicAndPossessiveFormsUseOwnedScalarDiscovery()
    {
        var atomic = new Utf8PythonRegex(@"(?>ab)c");
        var possessive = new Utf8PythonRegex(@"a++b");

        Assert.True(atomic.Match("xxabcyy"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(possessive.Match("xxaaabyy"u8, mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void MatchAndFullMatchCanUseUtf8RegexBackend()
    {
        var matchRegex = new Utf8PythonRegex(@"foo");
        var fullRegex = new Utf8PythonRegex(@"a|ab");

        Assert.True(matchRegex.Match("foo xx"u8).Success);
        Assert.False(matchRegex.Match("xxfoo"u8).Success);
        Assert.True(fullRegex.Match("ab"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(fullRegex.Match("ab"u8, mode: Utf8PythonMatchMode.FullMatch).GetValueBytes()));
    }

    [Fact]
    public void ConditionalAndLookaroundKeepOwnedResultsAfterCoreSelection()
    {
        var conditional = new Utf8PythonRegex(@"(foo)?(?(1)bar|baz)");
        var lookahead = new Utf8PythonRegex(@"foo(?=bar)");

        Assert.False(conditional.DebugUsesUtf8RegexBackend);
        Assert.True(lookahead.DebugUsesUtf8RegexBackend);
        Assert.True(conditional.Match("foobar"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(lookahead.Match("foobar"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(conditional.Match("foobar"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(conditional.Match("baz"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(conditional.Match("foo"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(lookahead.Match("foobar"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(lookahead.Match("foo"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void FullMatchSupportsConcurrentSpanAndCursorUse()
    {
        var native = new Utf8PythonRegex("ab[0-9]{2}");

        Parallel.For(0, 32, iteration =>
        {
            if ((iteration & 1) != 0)
            {
                using var cursor = native.CreateMatchCursor("ab12"u8.ToArray(), mode: Utf8PythonMatchMode.FullMatch);
                Assert.True(cursor.MoveNext(out var snapshot));
                Assert.True(snapshot.Success);
            }
            else
                Assert.True(native.Match("ab12"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        });


        var managed = new Utf8PythonRegex(@"^ab[0-9]{2}");
        Parallel.For(0, 32, _ => Assert.True(managed.Match("ab12"u8, mode: Utf8PythonMatchMode.FullMatch).Success));
    }

    [Fact]
    public void MatchReturnsByteAlignedUtf8ValueMatch()
    {
        var regex = new Utf8PythonRegex("café");

        var match = regex.Match("xxcaféyy"u8, mode: Utf8PythonMatchMode.Search);

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

        Assert.True(regex.Match("foo"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("foo xx"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void DotAllFullMatchPreservesWholeSuffixCoordinates()
    {
        var regex = new Utf8PythonRegex("start.*end", options: PythonReCompileOptions.DotAll);
        var input = "πstart\n東京𝒜end"u8.ToArray();
        var startOffsetInBytes = "π"u8.Length;

        var match = regex.Match(input, startOffsetInBytes, mode: Utf8PythonMatchMode.FullMatch);
        Assert.True(match.Success);
        Assert.Equal(startOffsetInBytes, match.StartOffsetInBytes);
        Assert.Equal(input.Length, match.EndOffsetInBytes);
        Assert.Equal("start\n東京𝒜end", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.True(regex.Match("startend"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match("start\0end"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void DotAllFullMatchPreservesPrefixSuffixAndInputContracts()
    {
        var regex = new Utf8PythonRegex("start.*end", options: PythonReCompileOptions.DotAll);
        var overlapping = new Utf8PythonRegex("aba.*aba", options: PythonReCompileOptions.DotAll);
        var ignoreCase = new Utf8PythonRegex("start.*end", options: PythonReCompileOptions.DotAll | PythonReCompileOptions.IgnoreCase);
        var captured = new Utf8PythonRegex("start(.*)end", options: PythonReCompileOptions.DotAll);
        var reluctant = new Utf8PythonRegex("start.*?end", options: PythonReCompileOptions.DotAll);
        var finiteTimeout = new Utf8PythonRegex("start.*end", options: PythonReCompileOptions.DotAll, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromSeconds(1)
        });

        Assert.False(regex.Match("xstartend"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("startendx"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("start-END"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("starend"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(overlapping.Match("aba"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(overlapping.Match("abaaba"u8, mode: Utf8PythonMatchMode.FullMatch).Success);

        Assert.False(new Utf8PythonRegex("start.*end").Match("start\nend"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(ignoreCase.Match("START\nEND"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Utf8PythonRangeMatch snapshot3 = default;
        using (var captureCursor3 = captured.CreateMatchCursor(("startmiddleend"u8).ToArray(), mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor3.MoveNext(out snapshot3);
        }
        Assert.Equal("middle", System.Text.Encoding.UTF8.GetString((snapshot3.Groups[1].Success ? snapshot3.GetGroupMemory(snapshot3.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.True(reluctant.Match("start😀endend"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(finiteTimeout.Match("start\n😀end"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void FullMatchUsesOwnedProgramForFallbackClassifiedPattern()
    {
        var regex = new Utf8PythonRegex("(?:Шерлок )+");
        var input = "xxШерлок Шерлок "u8.ToArray();

        var match = regex.Match(input, startOffsetInBytes: 2, mode: Utf8PythonMatchMode.FullMatch);

        Assert.True(match.Success);
        Assert.Equal(2, match.StartOffsetInBytes);
        Assert.Equal(input.Length, match.EndOffsetInBytes);
        Assert.Equal("Шерлок Шерлок ", System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.False(regex.Match("xxШерлок x"u8, startOffsetInBytes: 2, mode: Utf8PythonMatchMode.FullMatch).Success);
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
        Assert.True(detailed.TryGetGroup(1, out var word));
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
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!a");

        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Match(input, mode: Utf8PythonMatchMode.FullMatch));
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
        var regex = new Utf8PythonRegex("(a*)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");
        // R03 anchor slice: the empty-capable anchor shape now counts on the
        // owned slice, which maps its deadline to the same timeout identity.
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Count(input));
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() =>
        {
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            return snapshots1;
        });
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
        Assert.True(match.TryGetGroup(1, out var group));
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
        Assert.True(search.TryGetGroup(1, out var word));
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

        var search = regex.Match("𝒜"u8, mode: Utf8PythonMatchMode.Search);
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

        var search = regex.Match("𝒜"u8, mode: Utf8PythonMatchMode.Search);
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
        ReadOnlyMemory<byte> snapshots2Input = ("axa"u8).ToArray();
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        var result = snapshots2;
        Assert.Equal(["a", "a"], result.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Fact]
    public void UnicodeRangesBorrowUnchangedInputAndSurviveProgramClose()
    {
        var regex = new Utf8PythonRegex("é+");
        var input = "xéé yé"u8.ToArray();
        var retained = new List<Utf8PythonRangeMatch>();
        using (var cursor = regex.CreateMatchCursor(input))
            while (cursor.MoveNext(out var match))
                retained.Add(match);
        Assert.Equal(new[] { "éé", "é" }, retained.Select(match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)));
        regex.Dispose();
        Assert.Equal(new[] { "éé", "é" }, retained.Select(match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)));
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
                ReadOnlyMemory<byte> snapshots5Input = input;
                var snapshots5 = new List<Utf8PythonRangeMatch>();
                using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, 0, default))
                {
                    while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                        snapshots5.Add(snapshots5Match);
                }
                var strings = snapshots5;
                Assert.Equal(2_048, strings.Select(captureSnapshot5 => System.Text.Encoding.UTF8.GetString(captureSnapshot5.GetGroupMemory(captureSnapshot5.Groups.Length == 2 ? 1 : 0).Span)).ToArray().Length);
                Assert.All(strings.Select(captureSnapshot6 => System.Text.Encoding.UTF8.GetString(captureSnapshot6.GetGroupMemory(captureSnapshot6.Groups.Length == 2 ? 1 : 0).Span)).ToArray(), static value => Assert.Equal("token", value));
                return;
            }
            ReadOnlyMemory<byte> snapshots6Input = input;
            var snapshots6 = new List<Utf8PythonRangeMatch>();
            using (var snapshots6Cursor = regex.CreateMatchCursor(snapshots6Input, 0, default))
            {
                while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                    snapshots6.Add(snapshots6Match);
            }
            var utf8 = snapshots6;
            Assert.Equal(2_048, utf8.Select(captureSnapshot7 => captureSnapshot7.GetGroupMemory(captureSnapshot7.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Length);
            Assert.All(utf8.Select(captureSnapshot8 => captureSnapshot8.GetGroupMemory(captureSnapshot8.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray(), value => Assert.Equal(expectedBytes, value));
        });
    }

    [Fact]
    public void DirectShapedFindAllHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!a");

        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() =>
        {
            ReadOnlyMemory<byte> snapshots7Input = input;
            var snapshots7 = new List<Utf8PythonRangeMatch>();
            using (var snapshots7Cursor = regex.CreateMatchCursor(snapshots7Input, 0, default))
            {
                while (snapshots7Cursor.MoveNext(out var snapshots7Match))
                    snapshots7.Add(snapshots7Match);
            }
            return snapshots7;
        });
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() =>
        {
            ReadOnlyMemory<byte> snapshots8Input = input;
            var snapshots8 = new List<Utf8PythonRangeMatch>();
            using (var snapshots8Cursor = regex.CreateMatchCursor(snapshots8Input, 0, default))
            {
                while (snapshots8Cursor.MoveNext(out var snapshots8Match))
                    snapshots8.Add(snapshots8Match);
            }
            return snapshots8;
        });
    }

    [Fact]
    public void EmptyThenNonEmptyCompanionHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("x*|(?:(a+)+$)", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        var input = System.Text.Encoding.UTF8.GetBytes(new string('a', 100_000) + "!");

        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() =>
        {
            ReadOnlyMemory<byte> snapshots9Input = input;
            var snapshots9 = new List<Utf8PythonRangeMatch>();
            using (var snapshots9Cursor = regex.CreateMatchCursor(snapshots9Input, 0, default))
            {
                while (snapshots9Cursor.MoveNext(out var snapshots9Match))
                    snapshots9.Add(snapshots9Match);
            }
            return snapshots9;
        });
    }

    [Fact]
    public void ByteFindAllUsesSingleGroupShapeWithEmptyStringForUnsetOptionalGroups()
    {
        var regex = new Utf8PythonRegex("(a)?b");
        ReadOnlyMemory<byte> snapshots10Input = ("b ab b"u8).ToArray();
        var snapshots10 = new List<Utf8PythonRangeMatch>();
        using (var snapshots10Cursor = regex.CreateMatchCursor(snapshots10Input, 0, default))
        {
            while (snapshots10Cursor.MoveNext(out var snapshots10Match))
                snapshots10.Add(snapshots10Match);
        }
        var result = snapshots10;
        Assert.Equal(["", "a", ""], result.Select(captureSnapshot9 => System.Text.Encoding.UTF8.GetString(captureSnapshot9.GetGroupMemory(captureSnapshot9.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Fact]
    public void ByteFindAllPreservesExactOptionalCaptureValuesAndByteStart()
    {
        var numbered = new Utf8PythonRegex("pre(ab)?c");
        var named = new Utf8PythonRegex("(?P<letter>é)?x");
        var numberedInput = "π preabc prec preabc"u8;
        var namedInput = "skip x éx x"u8;
        ReadOnlyMemory<byte> snapshots11Input = (numberedInput).ToArray();
        int snapshots11Start = "π "u8.Length;
        var snapshots11 = new List<Utf8PythonRangeMatch>();
        using (var snapshots11Cursor = numbered.CreateMatchCursor(snapshots11Input, snapshots11Start, default))
        {
            while (snapshots11Cursor.MoveNext(out var snapshots11Match))
                snapshots11.Add(snapshots11Match);
        }
        var numberedResult = snapshots11;
        ReadOnlyMemory<byte> snapshots12Input = (namedInput).ToArray();
        int snapshots12Start = "skip "u8.Length;
        var snapshots12 = new List<Utf8PythonRangeMatch>();
        using (var snapshots12Cursor = named.CreateMatchCursor(snapshots12Input, snapshots12Start, default))
        {
            while (snapshots12Cursor.MoveNext(out var snapshots12Match))
                snapshots12.Add(snapshots12Match);
        }
        var namedResult = snapshots12;

        Assert.Equal(["ab", "", "ab"], numberedResult.Select(captureSnapshot10 => System.Text.Encoding.UTF8.GetString(captureSnapshot10.GetGroupMemory(captureSnapshot10.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(["", "é", ""], namedResult.Select(captureSnapshot11 => System.Text.Encoding.UTF8.GetString(captureSnapshot11.GetGroupMemory(captureSnapshot11.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Fact]
    public void ByteFindAllPreservesSubjectTextForNonExactOptionalCaptures()
    {
        var ignoreCase = new Utf8PythonRegex("(a)?b", options: PythonReCompileOptions.IgnoreCase);
        var characterClass = new Utf8PythonRegex("([ab])?c");
        var reluctant = new Utf8PythonRegex("(a)??a");
        ReadOnlyMemory<byte> snapshots13Input = ("AB b"u8).ToArray();
        var snapshots13 = new List<Utf8PythonRangeMatch>();
        using (var snapshots13Cursor = ignoreCase.CreateMatchCursor(snapshots13Input, 0, default))
        {
            while (snapshots13Cursor.MoveNext(out var snapshots13Match))
                snapshots13.Add(snapshots13Match);
        }
        Assert.Equal(["A", ""], snapshots13.Select(captureSnapshot12 => System.Text.Encoding.UTF8.GetString(captureSnapshot12.GetGroupMemory(captureSnapshot12.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots14Input = ("ac bc c"u8).ToArray();
        var snapshots14 = new List<Utf8PythonRangeMatch>();
        using (var snapshots14Cursor = characterClass.CreateMatchCursor(snapshots14Input, 0, default))
        {
            while (snapshots14Cursor.MoveNext(out var snapshots14Match))
                snapshots14.Add(snapshots14Match);
        }
        Assert.Equal(["a", "b", ""], snapshots14.Select(captureSnapshot13 => System.Text.Encoding.UTF8.GetString(captureSnapshot13.GetGroupMemory(captureSnapshot13.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots15Input = ("aa a"u8).ToArray();
        var snapshots15 = new List<Utf8PythonRangeMatch>();
        using (var snapshots15Cursor = reluctant.CreateMatchCursor(snapshots15Input, 0, default))
        {
            while (snapshots15Cursor.MoveNext(out var snapshots15Match))
                snapshots15.Add(snapshots15Match);
        }
        Assert.Equal(["", "", ""], snapshots15.Select(captureSnapshot14 => System.Text.Encoding.UTF8.GetString(captureSnapshot14.GetGroupMemory(captureSnapshot14.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Fact]
    public void ByteFindAllUsesTupleShapeForMultipleCaptures()
    {
        var regex = new Utf8PythonRegex("(a)|(x)");
        ReadOnlyMemory<byte> snapshots16Input = ("axa"u8).ToArray();
        var snapshots16 = new List<Utf8PythonRangeMatch>();
        using (var snapshots16Cursor = regex.CreateMatchCursor(snapshots16Input, 0, default))
        {
            while (snapshots16Cursor.MoveNext(out var snapshots16Match))
                snapshots16.Add(snapshots16Match);
        }
        var result = snapshots16;
        Assert.Equal(3, result.Select(captureSnapshot15 => Enumerable.Range(1, captureSnapshot15.Groups.Length - 1).Select(captureNumber15 => System.Text.Encoding.UTF8.GetString(captureSnapshot15.GetGroupMemory(captureNumber15).Span)).ToArray()).ToArray().Length);
        Assert.Equal(["a", ""], result.Select(captureSnapshot16 => Enumerable.Range(1, captureSnapshot16.Groups.Length - 1).Select(captureNumber16 => System.Text.Encoding.UTF8.GetString(captureSnapshot16.GetGroupMemory(captureNumber16).Span)).ToArray()).ToArray()[0]);
        Assert.Equal(["", "x"], result.Select(captureSnapshot17 => Enumerable.Range(1, captureSnapshot17.Groups.Length - 1).Select(captureNumber17 => System.Text.Encoding.UTF8.GetString(captureSnapshot17.GetGroupMemory(captureNumber17).Span)).ToArray()).ToArray()[1]);
        Assert.Equal(["a", ""], result.Select(captureSnapshot18 => Enumerable.Range(1, captureSnapshot18.Groups.Length - 1).Select(captureNumber18 => System.Text.Encoding.UTF8.GetString(captureSnapshot18.GetGroupMemory(captureNumber18).Span)).ToArray()).ToArray()[2]);
    }

    [Fact]
    public void CapturedStringFindAllUsesLastCaptureAndPreservesEmptyThenNonEmptyProgression()
    {
        var snapshots17Regex = new Utf8PythonRegex("(a)+");
        ReadOnlyMemory<byte> snapshots17Input = ("aaa aa"u8).ToArray();
        var snapshots17 = new List<Utf8PythonRangeMatch>();
        using (var snapshots17Cursor = snapshots17Regex.CreateMatchCursor(snapshots17Input, 0, default))
        {
            while (snapshots17Cursor.MoveNext(out var snapshots17Match))
                snapshots17.Add(snapshots17Match);
        }
        var repeated = snapshots17;
        Assert.Equal(["a", "a"], repeated.Select(captureSnapshot19 => System.Text.Encoding.UTF8.GetString(captureSnapshot19.GetGroupMemory(captureSnapshot19.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        var snapshots18Regex = new Utf8PythonRegex(@"(\b)|(\w+)");
        ReadOnlyMemory<byte> snapshots18Input = ("a::bc"u8).ToArray();
        var snapshots18 = new List<Utf8PythonRangeMatch>();
        using (var snapshots18Cursor = snapshots18Regex.CreateMatchCursor(snapshots18Input, 0, default))
        {
            while (snapshots18Cursor.MoveNext(out var snapshots18Match))
                snapshots18.Add(snapshots18Match);
        }
        var progressive = snapshots18;
        Assert.Equal(
            [
                ["", ""],
                ["", "a"],
                ["", ""],
                ["", ""],
                ["", "bc"],
                ["", ""],
            ],
            progressive.Select(captureSnapshot20 => Enumerable.Range(1, captureSnapshot20.Groups.Length - 1).Select(captureNumber20 => System.Text.Encoding.UTF8.GetString(captureSnapshot20.GetGroupMemory(captureNumber20).Span)).ToArray()).ToArray());
    }

    [Fact]
    public void CursorKeepsMultipleCaptureValues()
    {
        var regex = new Utf8PythonRegex("(a)|(x)");
        ReadOnlyMemory<byte> snapshots19Input = ("axa"u8).ToArray();
        var snapshots19 = new List<Utf8PythonRangeMatch>();
        using (var snapshots19Cursor = regex.CreateMatchCursor(snapshots19Input, 0, default))
        {
            while (snapshots19Cursor.MoveNext(out var snapshots19Match))
                snapshots19.Add(snapshots19Match);
        }
        var result = snapshots19;
        Assert.Equal(3, result.Select(captureSnapshot21 => Enumerable.Range(1, captureSnapshot21.Groups.Length - 1).Select(captureNumber21 => captureSnapshot21.GetGroupMemory(captureNumber21).ToArray()).ToArray()).ToArray().Length);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(result.Select(captureSnapshot22 => Enumerable.Range(1, captureSnapshot22.Groups.Length - 1).Select(captureNumber22 => captureSnapshot22.GetGroupMemory(captureNumber22).ToArray()).ToArray()).ToArray()[0][0]));
        Assert.Equal("", System.Text.Encoding.UTF8.GetString(result.Select(captureSnapshot23 => Enumerable.Range(1, captureSnapshot23.Groups.Length - 1).Select(captureNumber23 => captureSnapshot23.GetGroupMemory(captureNumber23).ToArray()).ToArray()).ToArray()[0][1]));
        Assert.Equal("", System.Text.Encoding.UTF8.GetString(result.Select(captureSnapshot24 => Enumerable.Range(1, captureSnapshot24.Groups.Length - 1).Select(captureNumber24 => captureSnapshot24.GetGroupMemory(captureNumber24).ToArray()).ToArray()).ToArray()[1][0]));
        Assert.Equal("x", System.Text.Encoding.UTF8.GetString(result.Select(captureSnapshot25 => Enumerable.Range(1, captureSnapshot25.Groups.Length - 1).Select(captureNumber25 => captureSnapshot25.GetGroupMemory(captureNumber25).ToArray()).ToArray()).ToArray()[1][1]));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(result.Select(captureSnapshot26 => Enumerable.Range(1, captureSnapshot26.Groups.Length - 1).Select(captureNumber26 => captureSnapshot26.GetGroupMemory(captureNumber26).ToArray()).ToArray()).ToArray()[2][0]));
    }

    [Fact]
    public void CapturedFindAllProjectsUnicodeUtf8RangesFromOneSubjectMap()
    {
        var regex = new Utf8PythonRegex("(é+)-(𝒜𝒜|𝒜)");
        var input = "xx éé-𝒜𝒜 yy é-𝒜"u8;
        ReadOnlyMemory<byte> snapshots20Input = (input).ToArray();
        var snapshots20 = new List<Utf8PythonRangeMatch>();
        using (var snapshots20Cursor = regex.CreateMatchCursor(snapshots20Input, 0, default))
        {
            while (snapshots20Cursor.MoveNext(out var snapshots20Match))
                snapshots20.Add(snapshots20Match);
        }
        var strings = snapshots20;
        ReadOnlyMemory<byte> snapshots21Input = (input).ToArray();
        var snapshots21 = new List<Utf8PythonRangeMatch>();
        using (var snapshots21Cursor = regex.CreateMatchCursor(snapshots21Input, 0, default))
        {
            while (snapshots21Cursor.MoveNext(out var snapshots21Match))
                snapshots21.Add(snapshots21Match);
        }
        var utf8 = snapshots21;
        Assert.Equal([["éé", "𝒜𝒜"], ["é", "𝒜"]], strings.Select(captureSnapshot27 => Enumerable.Range(1, captureSnapshot27.Groups.Length - 1).Select(captureNumber27 => System.Text.Encoding.UTF8.GetString(captureSnapshot27.GetGroupMemory(captureNumber27).Span)).ToArray()).ToArray());
        Assert.Equal(strings.Select(captureSnapshot28 => Enumerable.Range(1, captureSnapshot28.Groups.Length - 1).Select(captureNumber28 => System.Text.Encoding.UTF8.GetString(captureSnapshot28.GetGroupMemory(captureNumber28).Span)).ToArray()).ToArray(), utf8.Select(captureSnapshot29 => Enumerable.Range(1, captureSnapshot29.Groups.Length - 1).Select(captureNumber29 => captureSnapshot29.GetGroupMemory(captureNumber29).ToArray()).ToArray()).ToArray()
            .Select(static tuple => tuple.Select(System.Text.Encoding.UTF8.GetString).ToArray())
            .ToArray());
    }

    [Fact]
    public void CapturedStringFindAllHonorsByteStartAfterUnicodePrefixAndTranslatedNames()
    {
        var regex = new Utf8PythonRegex(@"(?P<left>é+)-(𝒜𝒜|𝒜)");
        var input = "π skip é-𝒜 xx éé-𝒜𝒜"u8;
        var startOffsetInBytes = "π skip é-𝒜 xx "u8.Length;
        ReadOnlyMemory<byte> snapshots22Input = (input).ToArray();
        var snapshots22 = new List<Utf8PythonRangeMatch>();
        using (var snapshots22Cursor = regex.CreateMatchCursor(snapshots22Input, startOffsetInBytes, default))
        {
            while (snapshots22Cursor.MoveNext(out var snapshots22Match))
                snapshots22.Add(snapshots22Match);
        }
        var result = snapshots22;
        Assert.Equal([["éé", "𝒜𝒜"]], result.Select(captureSnapshot30 => Enumerable.Range(1, captureSnapshot30.Groups.Length - 1).Select(captureNumber30 => System.Text.Encoding.UTF8.GetString(captureSnapshot30.GetGroupMemory(captureNumber30).Span)).ToArray()).ToArray());
    }

    [Fact]
    public void ShapedFindAllHonorsByteStartAfterUnicodePrefix()
    {
        var regex = new Utf8PythonRegex("[a-z]+");
        var input = "é ignored alpha beta"u8;
        var startOffsetInBytes = "é ignored "u8.Length;
        ReadOnlyMemory<byte> snapshots23Input = (input).ToArray();
        var snapshots23 = new List<Utf8PythonRangeMatch>();
        using (var snapshots23Cursor = regex.CreateMatchCursor(snapshots23Input, startOffsetInBytes, default))
        {
            while (snapshots23Cursor.MoveNext(out var snapshots23Match))
                snapshots23.Add(snapshots23Match);
        }
        var strings = snapshots23;
        ReadOnlyMemory<byte> snapshots24Input = (input).ToArray();
        var snapshots24 = new List<Utf8PythonRangeMatch>();
        using (var snapshots24Cursor = regex.CreateMatchCursor(snapshots24Input, startOffsetInBytes, default))
        {
            while (snapshots24Cursor.MoveNext(out var snapshots24Match))
                snapshots24.Add(snapshots24Match);
        }
        var utf8 = snapshots24;

        Assert.Equal(["alpha", "beta"], strings.Select(captureSnapshot31 => System.Text.Encoding.UTF8.GetString(captureSnapshot31.GetGroupMemory(captureSnapshot31.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(
            strings.Select(captureSnapshot32 => System.Text.Encoding.UTF8.GetString(captureSnapshot32.GetGroupMemory(captureSnapshot32.Groups.Length == 2 ? 1 : 0).Span)).ToArray(),
            utf8.Select(captureSnapshot33 => captureSnapshot33.GetGroupMemory(captureSnapshot33.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(System.Text.Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void ShapedFindAllPreservesEmptyThenNonEmptyProgression()
    {
        var regex = new Utf8PythonRegex(@"\b|\w+");
        string[] expected = ["", "a", "", "", "bc", ""];
        ReadOnlyMemory<byte> snapshots25Input = ("a::bc"u8).ToArray();
        var snapshots25 = new List<Utf8PythonRangeMatch>();
        using (var snapshots25Cursor = regex.CreateMatchCursor(snapshots25Input, 0, default))
        {
            while (snapshots25Cursor.MoveNext(out var snapshots25Match))
                snapshots25.Add(snapshots25Match);
        }
        var strings = snapshots25;
        ReadOnlyMemory<byte> snapshots26Input = ("a::bc"u8).ToArray();
        var snapshots26 = new List<Utf8PythonRangeMatch>();
        using (var snapshots26Cursor = regex.CreateMatchCursor(snapshots26Input, 0, default))
        {
            while (snapshots26Cursor.MoveNext(out var snapshots26Match))
                snapshots26.Add(snapshots26Match);
        }
        var utf8 = snapshots26;

        Assert.Equal(expected, strings.Select(captureSnapshot34 => System.Text.Encoding.UTF8.GetString(captureSnapshot34.GetGroupMemory(captureSnapshot34.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(
            expected,
            utf8.Select(captureSnapshot35 => captureSnapshot35.GetGroupMemory(captureSnapshot35.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(System.Text.Encoding.UTF8.GetString).ToArray());
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
        ReadOnlyMemory<byte> snapshots27Input = ("y"u8).ToArray();
        var snapshots27 = new List<Utf8PythonRangeMatch>();
        using (var snapshots27Cursor = regex.CreateMatchCursor(snapshots27Input, 0, default))
        {
            while (snapshots27Cursor.MoveNext(out var snapshots27Match))
                snapshots27.Add(snapshots27Match);
        }
        Assert.Equal(expected, snapshots27.Select(captureSnapshot36 => System.Text.Encoding.UTF8.GetString(captureSnapshot36.GetGroupMemory(captureSnapshot36.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots28Input = ("y"u8).ToArray();
        var snapshots28 = new List<Utf8PythonRangeMatch>();
        using (var snapshots28Cursor = regex.CreateMatchCursor(snapshots28Input, 0, default))
        {
            while (snapshots28Cursor.MoveNext(out var snapshots28Match))
                snapshots28.Add(snapshots28Match);
        }
        Assert.Equal(
                    expected,
                    snapshots28.Select(captureSnapshot37 => captureSnapshot37.GetGroupMemory(captureSnapshot37.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(System.Text.Encoding.UTF8.GetString).ToArray());
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(("y"u8).ToArray(), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        Assert.Equal(expected, collectedRanges1.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(3, regex.Count("y"u8));
        Assert.Equal("---", System.Text.Encoding.UTF8.GetString(regex.Replace("y"u8, "-"u8)));

        var subn = PythonReSubstitutionAssert.Decode(regex.Subn("y"u8, "-"u8));
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
        ReadOnlyMemory<byte> snapshots29Input = ("yyy"u8).ToArray();
        var snapshots29 = new List<Utf8PythonRangeMatch>();
        using (var snapshots29Cursor = regex.CreateMatchCursor(snapshots29Input, 0, default))
        {
            while (snapshots29Cursor.MoveNext(out var snapshots29Match))
                snapshots29.Add(snapshots29Match);
        }
        Assert.Equal(["", "", "", ""], snapshots29.Select(captureSnapshot38 => System.Text.Encoding.UTF8.GetString(captureSnapshot38.GetGroupMemory(captureSnapshot38.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots30Input = ("xxyxx"u8).ToArray();
        var snapshots30 = new List<Utf8PythonRangeMatch>();
        using (var snapshots30Cursor = regex.CreateMatchCursor(snapshots30Input, 0, default))
        {
            while (snapshots30Cursor.MoveNext(out var snapshots30Match))
                snapshots30.Add(snapshots30Match);
        }
        Assert.Equal(["xx", "", "xx", ""], snapshots30.Select(captureSnapshot39 => System.Text.Encoding.UTF8.GetString(captureSnapshot39.GetGroupMemory(captureSnapshot39.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots31Input = ("πx𝒜"u8).ToArray();
        var snapshots31 = new List<Utf8PythonRangeMatch>();
        using (var snapshots31Cursor = regex.CreateMatchCursor(snapshots31Input, 0, default))
        {
            while (snapshots31Cursor.MoveNext(out var snapshots31Match))
                snapshots31.Add(snapshots31Match);
        }
        Assert.Equal(["", "x", "", ""], snapshots31.Select(captureSnapshot40 => System.Text.Encoding.UTF8.GetString(captureSnapshot40.GetGroupMemory(captureSnapshot40.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots32Input = ("πx𝒜"u8).ToArray();
        int snapshots32Start = "π"u8.Length;
        var snapshots32 = new List<Utf8PythonRangeMatch>();
        using (var snapshots32Cursor = regex.CreateMatchCursor(snapshots32Input, snapshots32Start, default))
        {
            while (snapshots32Cursor.MoveNext(out var snapshots32Match))
                snapshots32.Add(snapshots32Match);
        }
        Assert.Equal(
                    ["x", "", ""],
                    snapshots32.Select(captureSnapshot41 => System.Text.Encoding.UTF8.GetString(captureSnapshot41.GetGroupMemory(captureSnapshot41.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots33Input = (ReadOnlySpan<byte>.Empty).ToArray();
        var snapshots33 = new List<Utf8PythonRangeMatch>();
        using (var snapshots33Cursor = regex.CreateMatchCursor(snapshots33Input, 0, default))
        {
            while (snapshots33Cursor.MoveNext(out var snapshots33Match))
                snapshots33.Add(snapshots33Match);
        }
        Assert.Equal([""], snapshots33.Select(captureSnapshot42 => System.Text.Encoding.UTF8.GetString(captureSnapshot42.GetGroupMemory(captureSnapshot42.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        var snapshots34Regex = new Utf8PythonRegex("x*", options: PythonReCompileOptions.IgnoreCase);
        ReadOnlyMemory<byte> snapshots34Input = ("X"u8).ToArray();
        var snapshots34 = new List<Utf8PythonRangeMatch>();
        using (var snapshots34Cursor = snapshots34Regex.CreateMatchCursor(snapshots34Input, 0, default))
        {
            while (snapshots34Cursor.MoveNext(out var snapshots34Match))
                snapshots34.Add(snapshots34Match);
        }
        Assert.Equal(["X", ""], snapshots34.Select(captureSnapshot43 => System.Text.Encoding.UTF8.GetString(captureSnapshot43.GetGroupMemory(captureSnapshot43.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        var snapshots35Regex = new Utf8PythonRegex("x*?");
        ReadOnlyMemory<byte> snapshots35Input = ("xx"u8).ToArray();
        var snapshots35 = new List<Utf8PythonRangeMatch>();
        using (var snapshots35Cursor = snapshots35Regex.CreateMatchCursor(snapshots35Input, 0, default))
        {
            while (snapshots35Cursor.MoveNext(out var snapshots35Match))
                snapshots35.Add(snapshots35Match);
        }
        Assert.Equal(["", "x", "", "x", ""], snapshots35.Select(captureSnapshot44 => System.Text.Encoding.UTF8.GetString(captureSnapshot44.GetGroupMemory(captureSnapshot44.Groups.Length == 2 ? 1 : 0).Span)).ToArray());

    }

    [Fact]
    public void LiteralReplacementPreservesGreedyExactRepeatProgression()
    {
        var repeated = new Utf8PythonRegex("x*");
        var grouped = new Utf8PythonRegex("(?:ab)*");

        Assert.Equal("-a-b--d-", System.Text.Encoding.UTF8.GetString(repeated.Replace("abxd"u8, "-"u8)));
        Assert.Equal("-a-b--d-", System.Text.Encoding.UTF8.GetString(repeated.Replace("abxd"u8, "-"u8)));
        var repeatedSubn = PythonReSubstitutionAssert.Decode(repeated.Subn("abxd"u8, "-"u8));
        Assert.Equal("-a-b--d-", repeatedSubn.ResultText);
        Assert.Equal(5, repeatedSubn.ReplacementCount);
        Assert.Equal("_z__q_", System.Text.Encoding.UTF8.GetString(grouped.Replace("zababq"u8, "_"u8)));
        Assert.Equal(
            "π-a-bxd",
            System.Text.Encoding.UTF8.GetString(repeated.Replace("πabxd"u8, "-"u8, count: 2, startOffsetInBytes: "π"u8.Length)));
    }

    [Fact]
    public void LiteralReplacementPreservesAmbiguousEmptyProgression()
    {
        var alternation = new Utf8PythonRegex("x*|y");
        var reluctant = new Utf8PythonRegex("x*?");

        Assert.Equal("---", System.Text.Encoding.UTF8.GetString(alternation.Replace("y"u8, "-"u8)));
        Assert.Equal("-----", System.Text.Encoding.UTF8.GetString(reluctant.Replace("xx"u8, "-"u8)));
    }

    [Fact]
    public void CursorProjectsSingleTrailingCapture()
    {
        var regex = new Utf8PythonRegex("item-([0-9]+)");
        var input = "skip item-12 item-345 item-6"u8;
        ReadOnlyMemory<byte> snapshots36Input = (input).ToArray();
        var snapshots36 = new List<Utf8PythonRangeMatch>();
        using (var snapshots36Cursor = regex.CreateMatchCursor(snapshots36Input, 0, default))
        {
            while (snapshots36Cursor.MoveNext(out var snapshots36Match))
                snapshots36.Add(snapshots36Match);
        }
        var all = snapshots36;
        ReadOnlyMemory<byte> snapshots37Input = (input).ToArray();
        int snapshots37Start = "skip item-12 "u8.Length;
        var snapshots37 = new List<Utf8PythonRangeMatch>();
        using (var snapshots37Cursor = regex.CreateMatchCursor(snapshots37Input, snapshots37Start, default))
        {
            while (snapshots37Cursor.MoveNext(out var snapshots37Match))
                snapshots37.Add(snapshots37Match);
        }
        var afterFirst = snapshots37;

        Assert.Equal(["12", "345", "6"], all.Select(captureSnapshot45 => System.Text.Encoding.UTF8.GetString(captureSnapshot45.GetGroupMemory(captureSnapshot45.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(["345", "6"], afterFirst.Select(captureSnapshot46 => System.Text.Encoding.UTF8.GetString(captureSnapshot46.GetGroupMemory(captureSnapshot46.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Fact]
    public void ExactLiteralByteValuesPreserveSuffixOffsets()
    {
        var regex = new Utf8PythonRegex("(?:Шерлок)");
        var input = "skip Шерлок Шерлок Шерлок"u8;
        ReadOnlyMemory<byte> snapshots38Input = (input).ToArray();
        var snapshots38 = new List<Utf8PythonRangeMatch>();
        using (var snapshots38Cursor = regex.CreateMatchCursor(snapshots38Input, 0, default))
        {
            while (snapshots38Cursor.MoveNext(out var snapshots38Match))
                snapshots38.Add(snapshots38Match);
        }
        var all = snapshots38;
        ReadOnlyMemory<byte> snapshots39Input = (input).ToArray();
        int snapshots39Start = "skip Шерлок "u8.Length;
        var snapshots39 = new List<Utf8PythonRangeMatch>();
        using (var snapshots39Cursor = regex.CreateMatchCursor(snapshots39Input, snapshots39Start, default))
        {
            while (snapshots39Cursor.MoveNext(out var snapshots39Match))
                snapshots39.Add(snapshots39Match);
        }
        var afterFirst = snapshots39;

        Assert.Equal(["Шерлок", "Шерлок", "Шерлок"], all.Select(captureSnapshot47 => System.Text.Encoding.UTF8.GetString(captureSnapshot47.GetGroupMemory(captureSnapshot47.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(["Шерлок", "Шерлок"], afterFirst.Select(captureSnapshot48 => System.Text.Encoding.UTF8.GetString(captureSnapshot48.GetGroupMemory(captureSnapshot48.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots40Input = (input).ToArray();
        var snapshots40 = new List<Utf8PythonRangeMatch>();
        using (var snapshots40Cursor = regex.CreateMatchCursor(snapshots40Input, 0, default))
        {
            while (snapshots40Cursor.MoveNext(out var snapshots40Match))
                snapshots40.Add(snapshots40Match);
        }
        Assert.Equal(
                    ["Шерлок", "Шерлок", "Шерлок"],
                    snapshots40.Select(captureSnapshot49 => captureSnapshot49.GetGroupMemory(captureSnapshot49.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(System.Text.Encoding.UTF8.GetString).ToArray());
    }

    [Fact]
    public void ExactLiteralFindAllFromOffsetPreservesValues()
    {
        var regex = new Utf8PythonRegex("item");
        var input = "item skip item item"u8;
        ReadOnlyMemory<byte> snapshots41Input = (input).ToArray();
        int snapshots41Start = "item skip "u8.Length;
        var snapshots41 = new List<Utf8PythonRangeMatch>();
        using (var snapshots41Cursor = regex.CreateMatchCursor(snapshots41Input, snapshots41Start, default))
        {
            while (snapshots41Cursor.MoveNext(out var snapshots41Match))
                snapshots41.Add(snapshots41Match);
        }
        var result = snapshots41;

        Assert.Equal(["item", "item"], result.Select(captureSnapshot50 => System.Text.Encoding.UTF8.GetString(captureSnapshot50.GetGroupMemory(captureSnapshot50.Groups.Length == 2 ? 1 : 0).Span)).ToArray());

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
        var ignoreCase = new Utf8PythonRegex("header:[0-9]+", options: PythonReCompileOptions.IgnoreCase);
        var reluctant = new Utf8PythonRegex("header:[0-9]+?");
        var captured = new Utf8PythonRegex("header:([0-9]+)");
        var finiteTimeout = new Utf8PythonRegex("header:[0-9]+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromSeconds(1)
        });
        Assert.Equal("HEADER:123", System.Text.Encoding.UTF8.GetString(ignoreCase.Match("HEADER:123"u8).GetValueBytes()));
        Utf8PythonRangeMatch snapshot15 = default;
        using (var captureCursor15 = captured.CreateMatchCursor(("header:123"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor15.MoveNext(out snapshot15);
        }
        Assert.Equal("123", System.Text.Encoding.UTF8.GetString((snapshot15.Groups[1].Success ? snapshot15.GetGroupMemory(snapshot15.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("header:123", System.Text.Encoding.UTF8.GetString(finiteTimeout.Match("header:123"u8).GetValueBytes()));
        Assert.Equal(1, reluctant.Match("header:123"u8).EndOffsetInBytes - "header:"u8.Length);

        var offsetMatch = regex.Match("skipheader:123 tail"u8, "skip"u8.Length);
        Assert.True(offsetMatch.Success);
        Assert.Equal("skip"u8.Length, offsetMatch.StartOffsetInBytes);
        Assert.Equal("skipheader:123"u8.Length, offsetMatch.EndOffsetInBytes);
    }

    [Fact]
    public void CursorRetainsValuesOutsideExactCaseSensitiveRoute()
    {
        var globalIgnoreCase = new Utf8PythonRegex("sherlock", options: PythonReCompileOptions.IgnoreCase);
        var scopedIgnoreCase = new Utf8PythonRegex("(?i:sherlock)");
        var captured = new Utf8PythonRegex("(sherlock)");
        var characterClass = new Utf8PythonRegex("[a-z]+");
        var input = "SHERLOCK Sherlock"u8;
        ReadOnlyMemory<byte> snapshots42Input = (input).ToArray();
        var snapshots42 = new List<Utf8PythonRangeMatch>();
        using (var snapshots42Cursor = globalIgnoreCase.CreateMatchCursor(snapshots42Input, 0, default))
        {
            while (snapshots42Cursor.MoveNext(out var snapshots42Match))
                snapshots42.Add(snapshots42Match);
        }
        Assert.Equal(["SHERLOCK", "Sherlock"], snapshots42.Select(captureSnapshot51 => System.Text.Encoding.UTF8.GetString(captureSnapshot51.GetGroupMemory(captureSnapshot51.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots43Input = (input).ToArray();
        var snapshots43 = new List<Utf8PythonRangeMatch>();
        using (var snapshots43Cursor = scopedIgnoreCase.CreateMatchCursor(snapshots43Input, 0, default))
        {
            while (snapshots43Cursor.MoveNext(out var snapshots43Match))
                snapshots43.Add(snapshots43Match);
        }
        Assert.Equal(["SHERLOCK", "Sherlock"], snapshots43.Select(captureSnapshot52 => System.Text.Encoding.UTF8.GetString(captureSnapshot52.GetGroupMemory(captureSnapshot52.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots44Input = ("sherlock"u8).ToArray();
        var snapshots44 = new List<Utf8PythonRangeMatch>();
        using (var snapshots44Cursor = captured.CreateMatchCursor(snapshots44Input, 0, default))
        {
            while (snapshots44Cursor.MoveNext(out var snapshots44Match))
                snapshots44.Add(snapshots44Match);
        }
        Assert.Equal(["sherlock"], snapshots44.Select(captureSnapshot53 => System.Text.Encoding.UTF8.GetString(captureSnapshot53.GetGroupMemory(captureSnapshot53.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        ReadOnlyMemory<byte> snapshots45Input = ("alpha beta"u8).ToArray();
        var snapshots45 = new List<Utf8PythonRangeMatch>();
        using (var snapshots45Cursor = characterClass.CreateMatchCursor(snapshots45Input, 0, default))
        {
            while (snapshots45Cursor.MoveNext(out var snapshots45Match))
                snapshots45.Add(snapshots45Match);
        }
        Assert.Equal(["alpha", "beta"], snapshots45.Select(captureSnapshot54 => System.Text.Encoding.UTF8.GetString(captureSnapshot54.GetGroupMemory(captureSnapshot54.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Theory]
    [InlineData("([0-9]+)-item", "12-item 345-item", "12", "345")]
    [InlineData("([0-9])+", "12 345", "2", "5")]
    public void CursorRetainsGeneralCaptureProjection(
        string pattern,
        string input,
        string first,
        string second)
    {
        var regex = new Utf8PythonRegex(pattern);
        ReadOnlyMemory<byte> snapshots46Input = System.Text.Encoding.UTF8.GetBytes(input);
        var snapshots46 = new List<Utf8PythonRangeMatch>();
        using (var snapshots46Cursor = regex.CreateMatchCursor(snapshots46Input, 0, default))
        {
            while (snapshots46Cursor.MoveNext(out var snapshots46Match))
                snapshots46.Add(snapshots46Match);
        }
        var result = snapshots46;

        Assert.Equal([first, second], result.Select(captureSnapshot55 => System.Text.Encoding.UTF8.GetString(captureSnapshot55.GetGroupMemory(captureSnapshot55.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
    }

    [Fact]
    public void CursorProjectsSeparatedCaptures()
    {
        var regex = new Utf8PythonRegex("([a-z]+)-([0-9]+)", options: PythonReCompileOptions.IgnoreCase);
        var input = "skip abc-12 DEF-345 ghi-6"u8;
        ReadOnlyMemory<byte> snapshots47Input = (input).ToArray();
        var snapshots47 = new List<Utf8PythonRangeMatch>();
        using (var snapshots47Cursor = regex.CreateMatchCursor(snapshots47Input, 0, default))
        {
            while (snapshots47Cursor.MoveNext(out var snapshots47Match))
                snapshots47.Add(snapshots47Match);
        }
        var all = snapshots47;
        ReadOnlyMemory<byte> snapshots48Input = (input).ToArray();
        int snapshots48Start = "skip abc-12 "u8.Length;
        var snapshots48 = new List<Utf8PythonRangeMatch>();
        using (var snapshots48Cursor = regex.CreateMatchCursor(snapshots48Input, snapshots48Start, default))
        {
            while (snapshots48Cursor.MoveNext(out var snapshots48Match))
                snapshots48.Add(snapshots48Match);
        }
        var afterFirst = snapshots48;

        Assert.Equal([["abc", "12"], ["DEF", "345"], ["ghi", "6"]], all.Select(captureSnapshot56 => Enumerable.Range(1, captureSnapshot56.Groups.Length - 1).Select(captureNumber56 => System.Text.Encoding.UTF8.GetString(captureSnapshot56.GetGroupMemory(captureNumber56).Span)).ToArray()).ToArray());
        Assert.Equal([["DEF", "345"], ["ghi", "6"]], afterFirst.Select(captureSnapshot57 => Enumerable.Range(1, captureSnapshot57.Groups.Length - 1).Select(captureNumber57 => System.Text.Encoding.UTF8.GetString(captureSnapshot57.GetGroupMemory(captureNumber57).Span)).ToArray()).ToArray());
    }

    [Theory]
    [InlineData("([a-z-]+)-([0-9]+)", "abc--12", "abc-", "12")]
    [InlineData("([a-z]+)--([0-9]+)", "abc--12", "abc", "12")]
    [InlineData("(.*)-([0-9]+)", "abc-12", "abc", "12")]
    public void CursorRetainsAmbiguousCaptureProjection(
        string pattern,
        string input,
        string first,
        string second)
    {
        var regex = new Utf8PythonRegex(pattern);
        ReadOnlyMemory<byte> snapshots49Input = System.Text.Encoding.UTF8.GetBytes(input);
        var snapshots49 = new List<Utf8PythonRangeMatch>();
        using (var snapshots49Cursor = regex.CreateMatchCursor(snapshots49Input, 0, default))
        {
            while (snapshots49Cursor.MoveNext(out var snapshots49Match))
                snapshots49.Add(snapshots49Match);
        }
        var result = snapshots49;

        Assert.Equal([[first, second]], result.Select(captureSnapshot58 => Enumerable.Range(1, captureSnapshot58.Groups.Length - 1).Select(captureNumber58 => System.Text.Encoding.UTF8.GetString(captureSnapshot58.GetGroupMemory(captureNumber58).Span)).ToArray()).ToArray());
    }

    [Fact]
    public void EmptyThenNonEmptyCompanionPreservesCapturesContextAndConcurrentReuse()
    {
        var captured = new Utf8PythonRegex("(?P<word>x*|y)");
        ReadOnlyMemory<byte> snapshots50Input = ("y"u8).ToArray();
        var snapshots50 = new List<Utf8PythonRangeMatch>();
        using (var snapshots50Cursor = captured.CreateMatchCursor(snapshots50Input, 0, default))
        {
            while (snapshots50Cursor.MoveNext(out var snapshots50Match))
                snapshots50.Add(snapshots50Match);
        }
        Assert.Equal(["", "y", ""], snapshots50.Select(captureSnapshot59 => System.Text.Encoding.UTF8.GetString(captureSnapshot59.GetGroupMemory(captureSnapshot59.Groups.Length == 2 ? 1 : 0).Span)).ToArray());

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
        ReadOnlyMemory<byte> snapshots51Input = ("foobar foo"u8).ToArray();
        var snapshots51 = new List<Utf8PythonRangeMatch>();
        using (var snapshots51Cursor = regex.CreateMatchCursor(snapshots51Input, 0, default))
        {
            while (snapshots51Cursor.MoveNext(out var snapshots51Match))
                snapshots51.Add(snapshots51Match);
        }
        var strings = snapshots51;
        ReadOnlyMemory<byte> snapshots52Input = ("foobar foo"u8).ToArray();
        var snapshots52 = new List<Utf8PythonRangeMatch>();
        using (var snapshots52Cursor = regex.CreateMatchCursor(snapshots52Input, 0, default))
        {
            while (snapshots52Cursor.MoveNext(out var snapshots52Match))
                snapshots52.Add(snapshots52Match);
        }
        var utf8 = snapshots52;
        var collectedRanges3 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor3 = regex.CreateMatchCursor(("foobar foo"u8).ToArray(), 0, default))
        {
            while (rangeCursor3.MoveNext(out var rangeMatch3))
                collectedRanges3.Add(rangeMatch3);
        }
        var detailed = collectedRanges3.ToArray();

        Assert.Equal(["foo", "foo"], matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(["foo", "foo"], strings.Select(captureSnapshot60 => System.Text.Encoding.UTF8.GetString(captureSnapshot60.GetGroupMemory(captureSnapshot60.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(
            strings.Select(captureSnapshot61 => System.Text.Encoding.UTF8.GetString(captureSnapshot61.GetGroupMemory(captureSnapshot61.Groups.Length == 2 ? 1 : 0).Span)).ToArray(),
            utf8.Select(captureSnapshot62 => captureSnapshot62.GetGroupMemory(captureSnapshot62.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(System.Text.Encoding.UTF8.GetString).ToArray());
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
        ReadOnlyMemory<byte> snapshots53Input = (input).ToArray();
        var snapshots53 = new List<Utf8PythonRangeMatch>();
        using (var snapshots53Cursor = regex.CreateMatchCursor(snapshots53Input, startOffsetInBytes, default))
        {
            while (snapshots53Cursor.MoveNext(out var snapshots53Match))
                snapshots53.Add(snapshots53Match);
        }
        var strings = snapshots53;
        ReadOnlyMemory<byte> snapshots54Input = (input).ToArray();
        var snapshots54 = new List<Utf8PythonRangeMatch>();
        using (var snapshots54Cursor = regex.CreateMatchCursor(snapshots54Input, startOffsetInBytes, default))
        {
            while (snapshots54Cursor.MoveNext(out var snapshots54Match))
                snapshots54.Add(snapshots54Match);
        }
        var utf8 = snapshots54;
        var collectedRanges4 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor4 = regex.CreateMatchCursor((input).ToArray(), startOffsetInBytes, default))
        {
            while (rangeCursor4.MoveNext(out var rangeMatch4))
                collectedRanges4.Add(rangeMatch4);
        }
        var detailed = collectedRanges4.ToArray();

        Assert.Empty(matches);
        Assert.Empty(strings.Select(captureSnapshot63 => System.Text.Encoding.UTF8.GetString(captureSnapshot63.GetGroupMemory(captureSnapshot63.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Empty(utf8.Select(captureSnapshot64 => captureSnapshot64.GetGroupMemory(captureSnapshot64.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray());
        Assert.Empty(detailed);
        Assert.Equal(0, regex.Count(input, startOffsetInBytes));
        ReadOnlyMemory<byte> snapshots55Input = (input).ToArray();
        var snapshots55 = new List<Utf8PythonRangeMatch>();
        using (var snapshots55Cursor = oneCapture.CreateMatchCursor(snapshots55Input, startOffsetInBytes, default))
        {
            while (snapshots55Cursor.MoveNext(out var snapshots55Match))
                snapshots55.Add(snapshots55Match);
        }
        Assert.Empty(snapshots55.Select(captureSnapshot65 => captureSnapshot65.GetGroupMemory(captureSnapshot65.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray());
        ReadOnlyMemory<byte> snapshots56Input = (input).ToArray();
        var snapshots56 = new List<Utf8PythonRangeMatch>();
        using (var snapshots56Cursor = twoCaptures.CreateMatchCursor(snapshots56Input, startOffsetInBytes, default))
        {
            while (snapshots56Cursor.MoveNext(out var snapshots56Match))
                snapshots56.Add(snapshots56Match);
        }
        Assert.Empty(snapshots56.Select(captureSnapshot66 => Enumerable.Range(1, captureSnapshot66.Groups.Length - 1).Select(captureNumber66 => captureSnapshot66.GetGroupMemory(captureNumber66).ToArray()).ToArray()).ToArray());
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
        ReadOnlyMemory<byte> snapshots57Input = (input).ToArray();
        var snapshots57 = new List<Utf8PythonRangeMatch>();
        using (var snapshots57Cursor = regex.CreateMatchCursor(snapshots57Input, startOffsetInBytes, default))
        {
            while (snapshots57Cursor.MoveNext(out var snapshots57Match))
                snapshots57.Add(snapshots57Match);
        }
        var strings = snapshots57;
        ReadOnlyMemory<byte> snapshots58Input = (input).ToArray();
        var snapshots58 = new List<Utf8PythonRangeMatch>();
        using (var snapshots58Cursor = regex.CreateMatchCursor(snapshots58Input, startOffsetInBytes, default))
        {
            while (snapshots58Cursor.MoveNext(out var snapshots58Match))
                snapshots58.Add(snapshots58Match);
        }
        var utf8 = snapshots58;

        Assert.Equal(expected, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(expected, strings.Select(captureSnapshot67 => System.Text.Encoding.UTF8.GetString(captureSnapshot67.GetGroupMemory(captureSnapshot67.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal(
            expected,
            utf8.Select(captureSnapshot68 => captureSnapshot68.GetGroupMemory(captureSnapshot68.Groups.Length == 2 ? 1 : 0).ToArray()).ToArray().Select(System.Text.Encoding.UTF8.GetString).ToArray());
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
        Assert.True(matches[0].TryGetGroup(1, out var firstWord));
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

        var replaced = System.Text.Encoding.UTF8.GetString(regex.Replace("foo x foo"u8, "<\\g<word>>"u8));

        Assert.Equal("<foo> x <foo>", replaced);
    }

    [Fact]
    public void ExactLiteralReplacementUsesCountedUtf8PathForUnlimitedZeroOffsetOperations()
    {
        var regex = new Utf8PythonRegex("cat");
        var input = "é cat fox cat 𝒜"u8;
        const string expected = "é $🐈 fox $🐈 𝒜";

        var replacedText = System.Text.Encoding.UTF8.GetString(regex.Replace(input, "$🐈"u8));
        var replacedUtf8 = regex.Replace(input, "$🐈"u8);
        var subnText = PythonReSubstitutionAssert.Decode(regex.Subn(input, "$🐈"u8));
        var subnUtf8 = regex.Subn(input, "$🐈"u8);

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

        Assert.Equal("dog cat cat", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "dog"u8, count: 1)));
        Assert.Equal(
            "🐈 🐈 cat",
            System.Text.Encoding.UTF8.GetString(regex.Replace(input, "🐈"u8, count: 2)));
        var subn = PythonReSubstitutionAssert.Decode(regex.Subn(input, "dog"u8, count: 2));
        Assert.Equal(("dog dog cat", 2), (subn.ResultText, subn.ReplacementCount));
        Assert.Equal("dog dog dog", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "dog"u8, count: 99)));
        Assert.Equal("cat dog dog", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "dog"u8, count: 0, startOffsetInBytes: 4)));
        Assert.Equal("<cat> <cat> <cat>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, @"<\g<0>>"u8)));
    }

    [Fact]
    public void ExactLiteralReplacementKeepsUtf8PrefixAndReplaysOnlySuffixMatches()
    {
        var regex = new Utf8PythonRegex("cat");
        var input = System.Text.Encoding.UTF8.GetBytes("caté😀" + string.Concat(Enumerable.Repeat("cat", 10)));
        var start = "caté😀"u8.Length;

        Assert.Equal(("caté😀🐈" + string.Concat(Enumerable.Repeat("cat", 9)), 1),
            PythonReSubstitutionAssert.Decode(regex.Subn(input, "🐈"u8, count: 1, startOffsetInBytes: start)));
        Assert.Equal(("caté😀", 10),
            PythonReSubstitutionAssert.Decode(regex.Subn(input, ""u8, startOffsetInBytes: start)));
        var miss = regex.Subn(input, "🐈"u8, startOffsetInBytes: input.Length);
        Assert.Equal(0, miss.ReplacementCount);
        Assert.Equal(input, miss.ResultBytes);
        Assert.NotSame(input, miss.ResultBytes);
    }

    [Fact]
    public void ScalarSubnPreservesUnlimitedReplacement()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = PythonReSubstitutionAssert.Decode(regex.Subn("xx foo-foo yy foo-foo"u8, "<\\g<word>>"u8, count: 0, startOffsetInBytes: 3));

        Assert.Equal("xx <foo> yy <foo>", replaced.ResultText);
        Assert.Equal(2, replaced.ReplacementCount);
    }

    [Fact]
    public void SubnReturnsUtf8ResultBytesAndCount()
    {
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = regex.Subn("xx foo-foo yy foo-foo"u8, "<\\g<word>>"u8, count: 0, startOffsetInBytes: 3);

        Assert.Equal("xx <foo> yy <foo>", System.Text.Encoding.UTF8.GetString(replaced.ResultBytes));
        Assert.Equal(2, replaced.ReplacementCount);
    }

    [Fact]
    public void SubnWithSingleReplacementLimitReportsExactCount()
    {
        // count 1 needs only existence, not a full-subject count.
        var regex = new Utf8PythonRegex(@"(?P<word>foo)-(?P=word)");

        var replaced = PythonReSubstitutionAssert.Decode(regex.Subn("xx foo-foo yy foo-foo"u8, "<\\g<word>>"u8, count: 1, startOffsetInBytes: 3));

        Assert.Equal("xx <foo> yy foo-foo", replaced.ResultText);
        Assert.Equal(1, replaced.ReplacementCount);

        var missed = PythonReSubstitutionAssert.Decode(regex.Subn("xx no-hit yy"u8, "<\\g<word>>"u8, count: 1, startOffsetInBytes: 0));

        Assert.Equal("xx no-hit yy", missed.ResultText);
        Assert.Equal(0, missed.ReplacementCount);
    }

    [Fact]
    public void SubnManualLoopHonorsLimitsWithEmptyMatches()
    {
        // the managed progression cursor owns empty-match advancement in Subn; oracle is CPython 3.13.2 re.subn.
        var optional = new Utf8PythonRegex("(a)?");

        var one = PythonReSubstitutionAssert.Decode(optional.Subn("ab"u8, "-"u8, 1));
        Assert.Equal("-b", one.ResultText);
        Assert.Equal(1, one.ReplacementCount);

        var all = PythonReSubstitutionAssert.Decode(optional.Subn("ab"u8, "-"u8, 0));
        Assert.Equal("--b-", all.ResultText);
        Assert.Equal(3, all.ReplacementCount);

        var alternation = new Utf8PythonRegex("a*|b");

        var first = PythonReSubstitutionAssert.Decode(alternation.Subn("ba"u8, "-"u8, 1));
        Assert.Equal("-ba", first.ResultText);
        Assert.Equal(1, first.ReplacementCount);

        var every = PythonReSubstitutionAssert.Decode(alternation.Subn("ba"u8, "-"u8, 0));
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

        var one = optional.Subn("ab"u8, "-"u8, 1, 0);
        Assert.Equal("-b", System.Text.Encoding.UTF8.GetString(one.ResultBytes));
        Assert.Equal(1, one.ReplacementCount);

        var all = optional.Subn("ab"u8, "-"u8, 0, 0);
        Assert.Equal("--b-", System.Text.Encoding.UTF8.GetString(all.ResultBytes));
        Assert.Equal(3, all.ReplacementCount);
    }

    [Fact]
    public void TemplateSubnHonorsLimits()
    {
        // non-empty-matching patterns take the single-pass cursor template route; oracle is CPython 3.13.2 re.subn.
        var words = new Utf8PythonRegex("([a-z]+)");

        var all = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>"u8, 0, 0));
        Assert.Equal("<cat> <dog>", all.ResultText);
        Assert.Equal(2, all.ReplacementCount);

        var one = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>"u8, 1, 0));
        Assert.Equal("<cat> dog", one.ResultText);
        Assert.Equal(1, one.ReplacementCount);

        var offset = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>"u8, 0, 4));
        Assert.Equal("cat <dog>", offset.ResultText);
        Assert.Equal(1, offset.ReplacementCount);

        var capped = PythonReSubstitutionAssert.Decode(words.Subn("cat dog"u8, "<\\1>"u8, 2, 0));
        Assert.Equal("<cat> <dog>", capped.ResultText);
        Assert.Equal(2, capped.ReplacementCount);

        var over = PythonReSubstitutionAssert.Decode(words.Subn("cat"u8, "<\\1>"u8, 5, 0));
        Assert.Equal("<cat>", over.ResultText);
        Assert.Equal(1, over.ReplacementCount);

        var groups = new Utf8PythonRegex("([a-z]+)-([0-9]+)");
        var swapped = groups.Subn("ab-12 cd-34"u8, "\\2:\\1"u8, 0, 0);
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
        Assert.Equal("xx <foo> yy <foo>", System.Text.Encoding.UTF8.GetString(regex.Replace("xx foo-foo yy foo-foo"u8, "<\\g<word>>"u8)));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("xx <foo> yy <foo>", System.Text.Encoding.UTF8.GetString(regex.Replace("xx foo-foo yy foo-foo"u8, "<\\g<word>>"u8)));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal("xx - yy -", System.Text.Encoding.UTF8.GetString(regex.Replace("xx foo-foo yy foo-foo"u8, "-"u8)));
        Assert.Equal(2, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public void InvalidReplacementTemplateIsNeverRetained()
    {
        var regex = new Utf8PythonRegex("foo");

        Assert.Throws<PythonRePatternException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("foo"u8, "\\"u8)));
        Assert.Throws<PythonRePatternException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("foo"u8, "\\"u8)));
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public void ScalarReplacementPreservesAlternationOrder()
    {
        var regex = new Utf8PythonRegex(@"(?:b)|(?::+)");

        var replaced = System.Text.Encoding.UTF8.GetString(regex.Replace(":a:b::c"u8, "-"u8));

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
            {
                while (cursor.MoveNext(out var range))
                    collected.Add(range);
            }
            var values = collected.ToArray();
            Assert.Equal(new[] { "cat", "dog:12" }, values.Select(Describe));
        });

        static string Describe(Utf8PythonRangeMatch match)
        {
            Assert.True(match.TryGetGroup(1, out var word));
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

        Assert.Equal(@"\&", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\&"u8)));
        Assert.Equal(@"\-", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\-"u8)));
        Assert.Equal("\\ ", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, "\\ "u8)));
    }

    [Fact]
    public void ReplacementRejectsTrailingBackslash()
    {
        var regex = new Utf8PythonRegex("x");

        var ex = Assert.Throws<PythonRePatternException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, "\\"u8)));

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
        PythonReMatchAssert.Equal("foo", regex.Match("xxfooyy"u8, mode: Utf8PythonMatchMode.Search));
        PythonReMatchAssert.Equal("foo", regex.Match("fooyy"u8));
        PythonReMatchAssert.Equal("foo", regex.Match("foo"u8, mode: Utf8PythonMatchMode.FullMatch));
        Assert.False(regex.Match("xxfoo"u8).Success);
    }

    [Fact]
    public void VerboseModeIgnoresWhitespaceAndComments()
    {
        var regex = new Utf8PythonRegex("""
            (?x)
            foo   # first token
            \s+
            bar
            """);

        Assert.True(regex.Match("xxfoo   bar yy"u8, mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void InlineCommentGroupIsIgnored()
    {
        var regex = new Utf8PythonRegex(@"foo(?# ignore me)bar");

        Assert.True(regex.Match("xxfoobar"u8, mode: Utf8PythonMatchMode.Search).Success);
    }

    [Fact]
    public void ConditionalGroupReferenceIsSupported()
    {
        var regex = new Utf8PythonRegex(@"(foo)?(?(1)bar|baz)");

        Assert.True(regex.Match("foobar"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match("baz"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(regex.Match("bar"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void ConditionalNamedReferenceIsSupported()
    {
        var regex = new Utf8PythonRegex(@"(?P<seen>foo)?(?(seen)bar|baz)");

        Assert.True(regex.Match("foobar"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(regex.Match("baz"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
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

        var asciiBoundary = new Utf8PythonRegex(@"\b", options: PythonReCompileOptions.Ascii);
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

        Assert.Throws<ArgumentOutOfRangeException>(() => direct.Match(input, startOffsetInBytes, mode: Utf8PythonMatchMode.Search));
        Assert.Throws<ArgumentOutOfRangeException>(() => direct.Match(input, startOffsetInBytes, mode: Utf8PythonMatchMode.FullMatch));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            Utf8PythonRangeMatch snapshot16 = default;
            using (var captureCursor16 = direct.CreateMatchCursor(input, startOffsetInBytes, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor16.MoveNext(out snapshot16);
            }

            _ = snapshot16;
        });
        Assert.Throws<ArgumentOutOfRangeException>(() =>
        {
            ReadOnlyMemory<byte> snapshots59Input = input;
            var snapshots59 = new List<Utf8PythonRangeMatch>();
            using (var snapshots59Cursor = direct.CreateMatchCursor(snapshots59Input, startOffsetInBytes, default))
            {
                while (snapshots59Cursor.MoveNext(out var snapshots59Match))
                    snapshots59.Add(snapshots59Match);
            }
            return snapshots59;
        });
        Assert.Throws<ArgumentOutOfRangeException>(() => empty.Count(input, startOffsetInBytes));
        Assert.Throws<ArgumentOutOfRangeException>(() => System.Text.Encoding.UTF8.GetString(direct.Replace(input, "_"u8, count: 0, startOffsetInBytes: startOffsetInBytes)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PythonReSplitAssert.Decode(input, direct.SplitRanges(input, 0, startOffsetInBytes, default)));
    }
}
