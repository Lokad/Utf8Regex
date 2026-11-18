using System.Globalization;
using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2CharacterTests
{
    [Theory]
    [InlineData("é", 1, 3)]
    [InlineData("€", 3, 6)]
    [InlineData("😀", 6, 10)]
    public void DotReportsExactCoordinatesAcrossUtf8ScalarWidths(
        string expected, int startOffsetInBytes, int expectedEndOffsetInBytes)
    {
        var regex = new Utf8Pcre2Regex(".");
        var input = Encoding.UTF8.GetBytes("aé€😀z");

        var match = regex.Match(input, startOffsetInBytes: startOffsetInBytes);

        Assert.True(match.Success);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        Assert.Equal(startOffsetInBytes, match.StartOffsetInBytes);
        Assert.Equal(expectedEndOffsetInBytes, match.EndOffsetInBytes);
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
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
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
        Assert.Equal(inputText, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
    }

    [Fact]
    public void UcpIndependentlyControlsGenericTypesAndBoundaries()
    {
        var asciiWord = new Utf8Pcre2Regex(@"\w");
        var unicodeWord = new Utf8Pcre2Regex(@"\w", options: Pcre2CompileOptions.Ucp);
        var unicodeDigit = new Utf8Pcre2Regex(@"\d", options: Pcre2CompileOptions.Ucp);
        var unicodeSpace = new Utf8Pcre2Regex(@"\s", options: Pcre2CompileOptions.Ucp);
        var asciiBoundary = new Utf8Pcre2Regex(@"\bé");
        var unicodeBoundary = new Utf8Pcre2Regex(@"\bé", options: Pcre2CompileOptions.Ucp);

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
        var literal = new Utf8Pcre2Regex("k", options: Pcre2CompileOptions.Caseless);
        var range = new Utf8Pcre2Regex("[a-c]", options: Pcre2CompileOptions.Caseless);
        var property = new Utf8Pcre2Regex(@"\p{Lu}", options: Pcre2CompileOptions.Caseless);

        Assert.True(literal.IsMatch("K"u8));
        Assert.True(range.IsMatch("B"u8));
        Assert.True(property.IsMatch("b"u8));
        Assert.False(literal.IsMatch("ı"u8));
    }

    [Fact]
    public void UcpPosixClassesUsePcre2SpecificMappings()
    {
        var blank = new Utf8Pcre2Regex("[[:blank:]]", options: Pcre2CompileOptions.Ucp);
        var graph = new Utf8Pcre2Regex("[[:graph:]]", options: Pcre2CompileOptions.Ucp);
        var punct = new Utf8Pcre2Regex("[[:punct:]]", options: Pcre2CompileOptions.Ucp);
        var hexadecimal = new Utf8Pcre2Regex("[[:xdigit:]]", options: Pcre2CompileOptions.Ucp);

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
        var notNewline = new Utf8Pcre2Regex(@"\N", options: Pcre2CompileOptions.DotAll);
        var lineSeparator = Encoding.UTF8.GetBytes("\u2028");

        Assert.False(crlfDot.IsMatch("\r\n"u8, 0, Pcre2MatchOptions.Anchored));
        Assert.False(crlfDot.Match("\r\n"u8).Success);
        Assert.Equal(1, crlfDot.Match("\r\n"u8, startOffsetInBytes: 1).StartOffsetInBytes);
        Assert.False(anyCrlfDot.IsMatch("\r\n"u8));
        Assert.True(unicodeBsr.IsMatch(lineSeparator));
        Assert.False(crlfBsr.IsMatch(lineSeparator));
        Assert.False(notNewline.IsMatch("\n"u8));
    }

    [Fact]
    public void LineSubjectAndRequestedStartAssertionsFollowPcre2Rules()
    {
        var multiline = new Utf8Pcre2Regex("^.", options: Pcre2CompileOptions.Multiline);
        var singleline = new Utf8Pcre2Regex("^.");
        var finalCaret = new Utf8Pcre2Regex("^", options: Pcre2CompileOptions.Multiline);
        var dollar = new Utf8Pcre2Regex("$");
        var strictDollar = new Utf8Pcre2Regex("$", options: Pcre2CompileOptions.DollarEndOnly);
        var absolute = new Utf8Pcre2Regex(@"\A.\Z");
        var requested = new Utf8Pcre2Regex(@"\G.");

        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(multiline.Match("a\nb"u8, startOffsetInBytes: 2).GetValueBytes()));
        Assert.False(singleline.IsMatch("a\nb"u8, 2));
        Assert.Equal(1, finalCaret.Count("\n"u8));
        Assert.Equal(1, dollar.Match("a\n"u8).StartOffsetInBytes);
        Assert.Equal(2, strictDollar.Match("a\n"u8).StartOffsetInBytes);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(absolute.Match("a\n"u8).GetValueBytes()));
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(requested.Match("aé"u8, startOffsetInBytes: 1).GetValueBytes()));
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

    [Theory]
    [InlineData("^", Pcre2CompileOptions.Multiline, false)]
    [InlineData("^a?", Pcre2CompileOptions.Multiline, false)]
    [InlineData("$", Pcre2CompileOptions.DollarEndOnly, true)]
    public void EndAnchorsPreserveProfileRulesAcrossOperations(string pattern, Pcre2CompileOptions options, bool found)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: options);
        var input = "é\n"u8;
        const int floor = 2;
        Assert.Equal(found, regex.IsMatch(input, floor));
        var match = regex.Match(input, startOffsetInBytes: floor);
        Assert.Equal(found, match.Success);
        Assert.Equal(found, regex.Match(input, startOffsetInBytes: floor).Success);
        Assert.Equal(found ? 1 : 0, regex.Count(input, floor));
        var cursor = regex.EnumerateMatches(input, floor);
        Assert.Equal(found, cursor.MoveNext());
        if (found)
        {
            Assert.Equal((3, 3), (match.StartOffsetInBytes, match.EndOffsetInBytes));
            Assert.Equal(3, cursor.Current.StartOffsetInBytes);
            Assert.False(cursor.MoveNext());
        }
    }

    [Fact]
    public void CharacterMembershipDrivesGlobalAndReplacementOperations()
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



        Assert.Equal("<a>-<é>😀", Encoding.UTF8.GetString(regex.Replace(input, "<$0>"u8)));
    }

    [Fact]
    public void SingleCharacterClassCountPreservesStartAndAnchoredSemantics()
    {
        var input = "1aé+"u8;
        var regex = new Utf8Pcre2Regex(@"\p{L}");
        var compileAnchored = new Utf8Pcre2Regex(@"\p{L}", options: Pcre2CompileOptions.Anchored);

        Assert.Equal(2, regex.Count(input, 1));
        Assert.Equal(1, regex.Count(input, 2));
        Assert.Equal(0, compileAnchored.Count(input));
        Assert.Equal(2, compileAnchored.Count(input, 1));
        Assert.Equal(2, regex.Count(input, 1, Pcre2MatchOptions.Anchored));
    }

    [Fact]
    public void SharedMathSymbolCountPreservesUnicodeStartAndRuntimeControls()
    {
        var regex = new Utf8Pcre2Regex(@"\p{Sm}");
        var input = Encoding.UTF8.GetBytes("x+<|~¬⁄X৲𝛁=");
        var suffixStart = Encoding.UTF8.GetByteCount("x+<");
        var limited = new Utf8Pcre2Regex(@"\p{Sm}", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

        Assert.Equal(8, regex.Count(input));
        Assert.Equal(6, regex.Count(input, suffixStart));
        Assert.Equal(0, regex.Count(input, 0, Pcre2MatchOptions.Anchored));
        Assert.Throws<Pcre2MatchException>(() => limited.Count("x+"u8));
    }

    [Fact]
    public void MathSymbolCountIsThreadSafe()
    {
        var regex = new Utf8Pcre2Regex(@"\p{Sm}");
        var input = Encoding.UTF8.GetBytes("--+¬𝛁--");
        for (var warmup = 0; warmup < 128; warmup++)
        {
            _ = regex.Count(input);
        }

        var matched = true;
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            matched &= regex.Count(input) == 3;
        }

        Assert.True(matched);

        var failures = 0;
        Parallel.For(0, 1_000, _ =>
        {
            if (regex.Count(input) != 3)
            {
                Interlocked.Increment(ref failures);
            }
        });

        Assert.Equal(0, failures);
    }

    [Fact]
    public void CharacterSearchHasUsefulWorkAdmission()
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 128));
        var sufficient = new Utf8Pcre2Regex("[0-9]", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var insufficient = new Utf8Pcre2Regex("[0-9]", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });

        Assert.False(sufficient.IsMatch(input));
        var exception = Assert.Throws<Pcre2MatchException>(() => insufficient.IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
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

    private static Utf8Pcre2Regex Create(
        string pattern,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings settings) =>
        new(pattern, options: options, compileSettings: settings, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
}
