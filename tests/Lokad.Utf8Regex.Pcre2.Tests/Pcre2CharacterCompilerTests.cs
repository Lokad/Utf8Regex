using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2CharacterCompilerTests
{
    [Theory]
    [InlineData("é", 1, 3, 1, 2)]
    [InlineData("€", 3, 6, 2, 3)]
    [InlineData("😀", 6, 10, 3, 5)]
    public void DotReportsExactCoordinatesAcrossUtf8ScalarWidths(
        string expected,
        int startOffsetInBytes,
        int expectedEndOffsetInBytes,
        int expectedStartOffsetInUtf16,
        int expectedEndOffsetInUtf16)
    {
        var regex = new Utf8Pcre2Regex(".");
        var input = Encoding.UTF8.GetBytes("aé€😀z");

        var match = regex.Match(input, startOffsetInBytes);

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.Equal(startOffsetInBytes, match.StartOffsetInBytes);
        Assert.Equal(expectedEndOffsetInBytes, match.EndOffsetInBytes);
        Assert.Equal(expectedStartOffsetInUtf16, match.StartOffsetInUtf16);
        Assert.Equal(expectedEndOffsetInUtf16, match.EndOffsetInUtf16);
        Assert.IsType<Pcre2CharacterDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
        Assert.Equal(Pcre2SyntaxNodeKind.CharacterProgram, regex.DebugCompiledProgram.SyntaxTree.RootKind);
    }

    [Theory]
    [InlineData("[a-c]", "--b--", "b")]
    [InlineData("[^a-c]", "b-é", "-")]
    [InlineData("[é-ê]", "xêy", "ê")]
    [InlineData("[[:digit:]]", "x7y", "7")]
    [InlineData("[[:punct:]]", "x+y", "+")]
    [InlineData("[[:^digit:]]", "7a", "a")]
    public void CharacterAndPosixClassesCompileGenerically(string pattern, string inputText, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);

        var match = regex.Match(Encoding.UTF8.GetBytes(inputText));

        Assert.True(match.Success);
        Assert.Equal(expected, match.GetValueString());
        Assert.IsType<Pcre2CharacterDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Theory]
    [InlineData(@"\a", "\a")]
    [InlineData(@"\c;", "{")]
    [InlineData(@"\077", "?")]
    [InlineData(@"\o{141}", "a")]
    [InlineData(@"\xA", "\n")]
    [InlineData(@"\x{1F600}", "😀")]
    [InlineData(@"\N{U+20AC}", "€")]
    [InlineData(@"\p{Sm}", "+")]
    [InlineData(@"[\x{e9}]", "é")]
    [InlineData(@"[\b]", "\b")]
    public void CharacterEscapesCompileToScalarSemantics(string pattern, string inputText)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(inputText);

        var match = regex.Match(input);

        Assert.True(match.Success);
        Assert.Equal(inputText, match.GetValueString());
        Assert.IsType<Pcre2CharacterDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void UcpIndependentlyControlsGenericTypesAndBoundaries()
    {
        var asciiWord = new Utf8Pcre2Regex(@"\w");
        var unicodeWord = new Utf8Pcre2Regex(@"\w", Pcre2CompileOptions.Ucp);
        var unicodeDigit = new Utf8Pcre2Regex(@"\d", Pcre2CompileOptions.Ucp);
        var unicodeSpace = new Utf8Pcre2Regex(@"\s", Pcre2CompileOptions.Ucp);
        var asciiBoundary = new Utf8Pcre2Regex(@"\bé");
        var unicodeBoundary = new Utf8Pcre2Regex(@"\bé", Pcre2CompileOptions.Ucp);

        Assert.False(asciiWord.IsMatch("é"u8));
        Assert.True(unicodeWord.IsMatch("é"u8));
        Assert.True(unicodeDigit.IsMatch("٣"u8));
        Assert.True(unicodeSpace.IsMatch("᠎"u8));
        Assert.False(asciiBoundary.IsMatch("é"u8));
        Assert.True(unicodeBoundary.IsMatch("é"u8));
    }

    [Fact]
    public void CaselessMatchingUsesCultureIndependentPcre2Semantics()
    {
        var literal = new Utf8Pcre2Regex("k", Pcre2CompileOptions.Caseless);
        var range = new Utf8Pcre2Regex("[a-c]", Pcre2CompileOptions.Caseless);
        var property = new Utf8Pcre2Regex(@"\p{Lu}", Pcre2CompileOptions.Caseless);

        Assert.True(literal.IsMatch("K"u8));
        Assert.True(range.IsMatch("B"u8));
        Assert.True(property.IsMatch("b"u8));
        Assert.False(literal.IsMatch("ı"u8));
    }

    [Fact]
    public void UcpPosixClassesUsePcre2SpecificMappings()
    {
        var blank = new Utf8Pcre2Regex("[[:blank:]]", Pcre2CompileOptions.Ucp);
        var graph = new Utf8Pcre2Regex("[[:graph:]]", Pcre2CompileOptions.Ucp);
        var punct = new Utf8Pcre2Regex("[[:punct:]]", Pcre2CompileOptions.Ucp);
        var hexadecimal = new Utf8Pcre2Regex("[[:xdigit:]]", Pcre2CompileOptions.Ucp);

        Assert.True(blank.IsMatch("᠎"u8));
        Assert.True(graph.IsMatch(Encoding.UTF8.GetBytes("\u200E")));
        Assert.False(graph.IsMatch(Encoding.UTF8.GetBytes("\u061C")));
        Assert.True(punct.IsMatch("+"u8));
        Assert.True(hexadecimal.IsMatch("Ｆ"u8));
        Assert.False(new Utf8Pcre2Regex("[[:xdigit:]]").IsMatch("Ｆ"u8));
    }

    [Fact]
    public void NewlineAndBsrConventionsRemainIndependent()
    {
        var crlfSettings = new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Crlf };
        var anyCrlfSettings = new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.AnyCrlf };
        var crlfDot = Create(".", Pcre2CompileOptions.None, crlfSettings);
        var anyCrlfDot = Create(".", Pcre2CompileOptions.None, anyCrlfSettings);
        var unicodeBsr = Create(@"\R", Pcre2CompileOptions.None, new Utf8Pcre2CompileSettings { Bsr = Pcre2BsrConvention.Unicode });
        var crlfBsr = Create(@"\R", Pcre2CompileOptions.None, new Utf8Pcre2CompileSettings { Bsr = Pcre2BsrConvention.AnyCrlf });
        var notNewline = new Utf8Pcre2Regex(@"\N", Pcre2CompileOptions.DotAll);
        var lineSeparator = Encoding.UTF8.GetBytes("\u2028");

        Assert.False(crlfDot.IsMatch("\r\n"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.Equal(1, crlfDot.Match("\r\n"u8).StartOffsetInBytes);
        Assert.False(anyCrlfDot.IsMatch("\r\n"u8));
        Assert.True(unicodeBsr.IsMatch(lineSeparator));
        Assert.False(crlfBsr.IsMatch(lineSeparator));
        Assert.False(notNewline.IsMatch("\n"u8));
    }

    [Fact]
    public void LineSubjectAndRequestedStartAssertionsFollowPcre2Rules()
    {
        var multiline = new Utf8Pcre2Regex("^.", Pcre2CompileOptions.Multiline);
        var singleline = new Utf8Pcre2Regex("^.");
        var finalCaret = new Utf8Pcre2Regex("^", Pcre2CompileOptions.Multiline);
        var dollar = new Utf8Pcre2Regex("$");
        var strictDollar = new Utf8Pcre2Regex("$", Pcre2CompileOptions.DollarEndOnly);
        var absolute = new Utf8Pcre2Regex(@"\A.\Z");
        var requested = new Utf8Pcre2Regex(@"\G.");

        Assert.Equal("b", multiline.Match("a\nb"u8, 2).GetValueString());
        Assert.False(singleline.IsMatch("a\nb"u8, 2));
        Assert.Equal(1, finalCaret.Count("\n"u8));
        Assert.Equal(1, dollar.Match("a\n"u8).StartOffsetInBytes);
        Assert.Equal(2, strictDollar.Match("a\n"u8).StartOffsetInBytes);
        Assert.Equal("a", absolute.Match("a\n"u8).GetValueString());
        Assert.Equal("é", requested.Match("aé"u8, 1).GetValueString());
    }

    [Fact]
    public void GlobalInlineAndExtendedMoreOptionsAreCompiledIntoTokens()
    {
        var scopedSequence = new Utf8Pcre2Regex("(?i)a(?-i)b");
        var extended = new Utf8Pcre2Regex("(?x) a # comment\n b");
        var extendedMore = new Utf8Pcre2Regex("(?xx)[ a ]");

        Assert.True(scopedSequence.IsMatch("Ab"u8));
        Assert.False(scopedSequence.IsMatch("AB"u8));
        Assert.True(extended.IsMatch("ab"u8));
        Assert.True(extendedMore.IsMatch("a"u8));
        Assert.False(extendedMore.IsMatch(" "u8));
    }

    [Fact]
    public void CharacterProgramDrivesGlobalAndReplacementOperations()
    {
        var regex = new Utf8Pcre2Regex(@"\p{L}");
        var input = "a-é😀"u8;

        Assert.Equal(2, regex.Count(input));
        var matches = regex.EnumerateMatches(input);
        Assert.True(matches.MoveNext());
        Assert.Equal(0, matches.Current.StartOffsetInBytes);
        Assert.True(matches.MoveNext());
        Assert.Equal(2, matches.Current.StartOffsetInBytes);
        Assert.Equal(4, matches.Current.EndOffsetInBytes);
        Assert.False(matches.MoveNext());

        Span<Utf8Pcre2MatchData> destination = stackalloc Utf8Pcre2MatchData[1];
        Assert.Equal(1, regex.MatchMany(input, destination, out var isMore));
        Assert.True(isMore);
        Assert.Equal(0, destination[0].StartOffsetInBytes);

        Assert.Equal("<a>-<é>😀", Encoding.UTF8.GetString(regex.Replace(input, "<$0>")));
        Assert.IsType<Pcre2CharacterDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
    }

    [Fact]
    public void CharacterSearchChargesEveryMeteredCandidate()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 128));
        var sufficient = new Utf8Pcre2Regex(
            "[0-9]",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 129 },
            Timeout.InfiniteTimeSpan);
        var insufficient = new Utf8Pcre2Regex(
            "[0-9]",
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = 128 },
            Timeout.InfiniteTimeSpan);

        Assert.False(sufficient.IsMatch(input));
        var exception = Assert.Throws<Pcre2MatchException>(() => insufficient.IsMatch(input));
        Assert.Equal("MatchLimit", exception.ErrorKind);
    }

    [Fact]
    public void CompiledCharacterProgramsAreSafeForConcurrentCalls()
    {
        var regex = new Utf8Pcre2Regex(@"\p{L}");
        var input = Encoding.UTF8.GetBytes("--é--");
        var failures = 0;

        Parallel.For(0, 1_000, _ =>
        {
            if (!regex.IsMatch(input) || regex.Count(input) != 1)
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.Equal(0, failures);
    }

    [Fact]
    public void CharacterOneShotAndCountAreAllocationFreeAfterWarmup()
    {
        var regex = new Utf8Pcre2Regex(@"\p{L}");
        var input = "--é--"u8.ToArray();
        for (var warmup = 0; warmup < 128; warmup++)
        {
            _ = regex.IsMatch(input);
            _ = regex.Match(input);
            _ = regex.Count(input);
        }

        var matched = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            matched &= regex.IsMatch(input);
            matched &= regex.Match(input).Success;
            matched &= regex.Count(input) == 1;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(matched);
        Assert.Equal(0, allocated);
    }

    private static Utf8Pcre2Regex Create(
        string pattern,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings settings) =>
        new(pattern, options, settings, default, Timeout.InfiniteTimeSpan);
}
