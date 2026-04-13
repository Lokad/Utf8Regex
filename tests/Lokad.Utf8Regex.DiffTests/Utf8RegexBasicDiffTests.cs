using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexIsMatchDiffTests
{
    [Theory]
    [InlineData("abc", "xxabcxx")]
    [InlineData("abc", "nomatch")]
    [InlineData("", "hello")]
    [InlineData("needle", "haystack with needle inside")]
    public void IsMatchMatchesRuntimeForNativeLiteralSubset(string pattern, string input)
    {
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        var expected = Regex.IsMatch(input, pattern, RegexOptions.CultureInvariant);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("abc", "xxABCxx")]
    [InlineData("abc", "xxaBcxx")]
    [InlineData("abc", "nomatch")]
    public void IsMatchMatchesRuntimeForInvariantAsciiIgnoreCaseLiteralSubset(string pattern, string input)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("a.*b", "axxxb", RegexOptions.CultureInvariant)]
    [InlineData("abc", "ABC", RegexOptions.IgnoreCase)]
    [InlineData("café", "café", RegexOptions.CultureInvariant)]
    [InlineData(@"\u00E9", "café", RegexOptions.CultureInvariant)]
    [InlineData(@"\cA", "\u0001", RegexOptions.CultureInvariant)]
    [InlineData(@"\0", "\0", RegexOptions.CultureInvariant)]
    [InlineData("(?x) a b # comment", "xxabyy", RegexOptions.CultureInvariant)]
    [InlineData(@"\Aabc", "abc", RegexOptions.CultureInvariant)]
    [InlineData(@"abc\Z", "abc\n", RegexOptions.CultureInvariant)]
    [InlineData(@"abc\z", "abc", RegexOptions.CultureInvariant)]
    [InlineData(@"\p{Lu}", "A", RegexOptions.CultureInvariant)]
    [InlineData(@"\P{Nd}", "A", RegexOptions.CultureInvariant)]
    [InlineData("a(?#comment)b", "xxabyy", RegexOptions.CultureInvariant)]
    public void IsMatchFallsBackAndStillMatchesRuntime(string pattern, string input, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"\bword\B", "a wordplay z", RegexOptions.CultureInvariant)]
    [InlineData(@"\bé", "é z", RegexOptions.CultureInvariant)]
    [InlineData(@"é\B", "éé z", RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForNativeBoundaryWrappedLiteralSubset(string pattern, string input, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("café|niño", "zz niño yy")]
    [InlineData("café|niño", "zz café yy")]
    [InlineData("café|niño", "zz resume yy")]
    [InlineData("niño|ni", "zz niño yy")]
    public void IsMatchMatchesRuntimeForNativeUtf8LiteralAlternationSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("Sherlock Holmes|John Watson|Irene Adler", "xx john watson xx")]
    [InlineData("Sherlock Holmes|John Watson|Irene Adler", "xx IRENE ADLER xx")]
    [InlineData("Sherlock Holmes|John Watson|Irene Adler", "xx professor moriarty xx")]
    public void IsMatchMatchesRuntimeForInvariantAsciiIgnoreCaseLiteralAlternationSubset(string pattern, string input)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", "public Task Run();")]
    [InlineData(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", "public IAsyncEnumerable<int> Stream();")]
    [InlineData(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", "public TaskFactory Factory;")]
    public void IsMatchMatchesRuntimeForBoundaryWrappedAsciiLiteralAlternationSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b", "public Task<User> TryGetCustomerRecordAsync() => default;")]
    [InlineData(@"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b", "private static void GetvalueAsync() {}")]
    [InlineData(@"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b", "var text = CreateReportBuilderAsync;")]
    public void IsMatchMatchesRuntimeForAsciiStructuralIdentifierFamily(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", "préface AKIAABCDEFGHIJKLMNOP suffixe")]
    [InlineData("((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", "préface sans clé")]
    public void IsMatchMatchesRuntimeForUtf8HaystackAgainstUtf8ByteSafeAsciiSimplePattern(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", "préface AKIAABCDEFGHIJKLMNOP suffixe")]
    [InlineData("((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", "préface sans clé")]
    public void CountMatchesRuntimeForUtf8HaystackAgainstUtf8ByteSafeAsciiSimplePattern(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.Count(input, pattern, options);
        var actual = utf8Regex.Count(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?", "préface # NOQA: F401, E501 suffixe")]
    [InlineData("(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?", "préface sans directive")]
    public void CountMatchesRuntimeForUtf8HaystackAgainstUtf8ByteSafeFallbackInterpreterPattern(string pattern, string input)
    {
        var options = RegexOptions.None;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.Count(input, pattern, options);
        var actual = utf8Regex.Count(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "public record CustomerExportJob(int Id);")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "internal sealed worker_state value_holder;")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "public class Worker_State {}")]
    public void IsMatchMatchesRuntimeForAsciiDeclarationIdentifierFamily(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", "logger.LogWarning (\"missing\");")]
    [InlineData(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", "logger.LogDebug(\"skip\");")]
    [InlineData(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", "LogInformation(\"ok\");")]
    public void IsMatchMatchesRuntimeForAsciiLoggingInvocationFamily(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("café(?= noir)", "zz café noir yy")]
    [InlineData("café(?= noir)", "zz café gris yy")]
    public void IsMatchMatchesRuntimeForNativeUtf8LiteralLookaheadSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForDeterministicStructuralLinearRun()
    {
        const string pattern = @"\d{2,4}";
        const string input = "1 12 123 1234";
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, pattern, options))
        {
            expected.Add((match.Index, match.Length));
        }

        var actual = new List<(int Index, int Length)>();
        foreach (var match in utf8Regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForAsciiLoggingInvocationFamily()
    {
        const string pattern = @"\b(?:LogError|LogWarning|LogInformation)\s*\(";
        const string input = "LogError(\"bad\"); LogDebug(\"skip\"); LogInformation (\"ok\");";
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, pattern, options))
        {
            expected.Add((match.Index, match.Length));
        }

        var actual = new List<(int Index, int Length)>();
        foreach (var match in utf8Regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ab.cd", "xxabXcdyy")]
    [InlineData("ab.cd", "xxABxCDyy")]
    [InlineData("a[bc]d", "zzaBdyy")]
    [InlineData(@"a\wb", "A_B")]
    public void IsMatchMatchesRuntimeForInvariantIgnoreCaseAsciiSimplePatternSubset(string pattern, string input)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ab?c", "zzabc yy")]
    [InlineData("ab?c", "zzac yy")]
    [InlineData("ab?c", "zzabbc yy")]
    [InlineData("(?:ab|cd){2}", "zzabcd yy")]
    [InlineData("(?:ab|cd){2}", "zzcdcd yy")]
    [InlineData("(?:ab|cd){2}", "zzab yy")]
    public void IsMatchMatchesRuntimeForFiniteLiteralExpandedSimplePatterns(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("ab?c", "zzabcac yy")]
    [InlineData("(?:ab|cd){2}", "zzabcd cdcd yy")]
    public void MatchMatchesRuntimeForFiniteLiteralExpandedSimplePatterns(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.Match(input, pattern, options);
        var actual = utf8Regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    [Theory]
    [InlineData("ab?c", "zzABC yy")]
    [InlineData("ab?c", "zzAc yy")]
    [InlineData("ab?c", "zzAbbC yy")]
    [InlineData("a[bc]d", "zzABD yy")]
    [InlineData("a[bc]d", "zzacd yy")]
    [InlineData("a[bc]?d", "zzABD yy")]
    [InlineData("a[bc]?d", "zzad yy")]
    public void IsMatchMatchesRuntimeForInvariantIgnoreCaseFiniteLiteralExpandedSimplePatterns(string pattern, string input)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("a.b", "xa\nby", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    [InlineData("^abc", "zzz\nabc\nyyy", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData("abc$", "zzz\nabc\nyyy", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData("(?x)a b", "zzabyy", RegexOptions.CultureInvariant)]
    [InlineData("(ab)", "zzabyy", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForAdditionalNativeOptionFamilies(string pattern, string input, RegexOptions options)
    {
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("(?i)abc", "xxABCxx")]
    [InlineData("(?i)abc", "xxaBcxx")]
    [InlineData("(?i)abc", "nomatch")]
    public void IsMatchMatchesRuntimeForLeadingGlobalInlineIgnoreCaseLiteralSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("foo(?=bar)", "xxFOObaryy")]
    [InlineData("foo(?=bar)", "xxfooquxyy")]
    [InlineData("(?:cat|dog)(?=house)", "xxDOGhouseyy")]
    [InlineData("(?:cat|dog)(?=house)", "xxdogparkyy")]
    public void IsMatchMatchesRuntimeForInvariantIgnoreCasePositiveLiteralLookaheadSubset(string pattern, string input)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("HttpClient(?=\\s+client)", "var HttpClient client = Create();")]
    [InlineData("HttpClient(?=\\s+client)", "var HttpClient\nclient = Create();")]
    [InlineData("HttpClient(?=\\s+client)", "var HttpClientFactory client = Create();")]
    public void MatchMatchesRuntimeForPositiveSeparatorLiteralLookaheadSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expectedIsMatch = Regex.IsMatch(input, pattern, options);
        var expectedMatch = Regex.Match(input, pattern, options);
        var actualIsMatch = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));
        var actualMatch = utf8Regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expectedIsMatch, actualIsMatch);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
    }

    [Fact]
    public void CountMatchesRuntimeForPositiveSeparatorLiteralLookaheadSubset()
    {
        const string input = "HttpClient client HttpClientFactory client HttpClient\nclient";
        const string pattern = "HttpClient(?=\\s+client)";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("(?i)a.c", "xxAbCxx")]
    [InlineData("(?i)a[bc]d", "zzaBdyy")]
    [InlineData("(?i)^ab.cd$", "ABxCD")]
    public void IsMatchMatchesRuntimeForLeadingGlobalInlineIgnoreCaseSimplePatternSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void StaticIsMatchMatchesInstanceBehavior()
    {
        var input = "xxabcxx"u8.ToArray();

        Assert.True(Utf8Regex.IsMatch(input, "abc", RegexOptions.CultureInvariant));
    }

    [Fact]
    public void MatchReturnsByteAlignedCoordinatesForNativeLiteral()
    {
        var match = new Utf8Regex("abc", RegexOptions.CultureInvariant).Match("xxabcxx"u8);

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(2, match.IndexInUtf16);
        Assert.Equal(3, match.LengthInUtf16);
        Assert.Equal(2, match.IndexInBytes);
        Assert.Equal(3, match.LengthInBytes);
    }

    [Fact]
    public void MatchReturnsUtf16AndByteCoordinatesForNativeUtf8Literal()
    {
        var match = new Utf8Regex("😀", RegexOptions.CultureInvariant).Match("x😀y"u8);

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(1, match.IndexInUtf16);
        Assert.Equal(2, match.LengthInUtf16);
        Assert.Equal(1, match.IndexInBytes);
        Assert.Equal(4, match.LengthInBytes);
    }

    [Fact]
    public void MatchReturnsNonAlignedCoordinatesForFallbackSupplementaryCase()
    {
        var match = new Utf8Regex(".", RegexOptions.CultureInvariant).Match("😀"u8);

        Assert.True(match.Success);
        Assert.False(match.IsByteAligned);
        Assert.Equal(0, match.IndexInUtf16);
        Assert.Equal(1, match.LengthInUtf16);
    }

    [Fact]
    public void MatchReturnsCoordinatesForAsciiLoggingInvocationFamily()
    {
        const string input = "prefix LogInformation (\"ok\") suffix";
        const string pattern = @"\b(?:LogError|LogWarning|LogInformation)\s*\(";
        var expected = Regex.Match(input, pattern, RegexOptions.CultureInvariant);
        var actual = new Utf8Regex(pattern, RegexOptions.CultureInvariant).Match(Encoding.UTF8.GetBytes(input));

        Assert.True(expected.Success);
        Assert.True(actual.Success);
        Assert.True(actual.IsByteAligned);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Index, actual.IndexInBytes);
        Assert.Equal(expected.Length, actual.LengthInBytes);
    }

    [Fact]
    public void CountMatchesRuntimeForNativeLiteral()
    {
        const string input = "abcxxabcabc";
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Equal(
            Regex.Count(input, "abc", RegexOptions.CultureInvariant),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForFallbackRegex()
    {
        const string input = "ab abb abbb";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("ab+", options);

        Assert.Equal(
            Regex.Count(input, "ab+", options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForUnicodeCategoryRepeatFallback()
    {
        const string input = "Преступление наказание short слово";
        const string pattern = @"\p{L}{8,13}";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(
        "\"AIDAABCDEFGHIJKLMNOP\"\nctx = 1\nctx = 2\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n\n\"AIDAABCDEFGHIJKLMNOP\"\nctx = 3\nctx = 4\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\n",
        2)]
    [InlineData(
        "\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\nctx = 1\n\"AIDAABCDEFGHIJKLMNOP\"\n\n\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"\nctx = 2\n\"AIDAABCDEFGHIJKLMNOP\"\n",
        2)]
    public void CountMatchesRuntimeForSearchGuidedFallbackPositiveMultilineWindows(string input, int expectedCount)
    {
        const string pattern = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
        var options = RegexOptions.Multiline;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(expectedCount, Regex.Count(input, pattern, options));
        Assert.Equal(expectedCount, regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void LeadingLiteralFallbackMatchMatchesRuntime()
    {
        const string input = "xx foobaz yy foobar zz";
        const string pattern = "foo(?>bar|baz)";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        var expectedIsMatch = Regex.IsMatch(input, pattern, options);
        var expectedMatch = Regex.Match(input, pattern, options);
        var actualIsMatch = regex.IsMatch(Encoding.UTF8.GetBytes(input));
        var actualMatch = regex.Match(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expectedIsMatch, actualIsMatch);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
    }

    [Theory]
    [InlineData("ab.cd", "abXcd")]
    [InlineData("ab.cd", "xxabYcdzz")]
    [InlineData("..a", "12a")]
    [InlineData("a..b", "xa12b")]
    [InlineData("a[bc]d", "zzabdyy")]
    [InlineData("ab[0-9]d", "ab7d")]
    [InlineData("a[^x]d", "zzayd")]
    [InlineData("^abc", "abc")]
    [InlineData("^abc", "xabc")]
    [InlineData("abc$", "xxabc")]
    [InlineData("abc$", "abc\n")]
    [InlineData("^ab.cd$", "abXcd")]
    [InlineData("^ab.cd$", "zzabXcd")]
    [InlineData(@"\d\d", "a12b")]
    [InlineData(@"a\wb", "a_b")]
    [InlineData(@"a\sb", "a b")]
    [InlineData(@"\.", "x.y")]
    [InlineData(@"a{3}", "xxaaayy")]
    [InlineData(@"\d{2}", "x12y")]
    [InlineData(@"[ab]{2}", "ccabdd")]
    [InlineData(@"[\d][A-Z]", "3Z")]
    [InlineData(@"[\w-][\s]", "_ ")]
    [InlineData(@"[^\S]", " \t")]
    [InlineData(@"\n", "a\nb")]
    [InlineData(@"\t", "a\tb")]
    [InlineData(@"[\r\n]", "\r\n")]
    [InlineData(@"\x41", "xAy")]
    [InlineData(@"[\x30A]", "0A")]
    [InlineData("cat|dog", "zzdogyy")]
    [InlineData(@"ab.cd|xy\d\d", "xxxy12yy")]
    [InlineData("cat|horse", "zzhorseyy")]
    [InlineData("foo(?=bar)", "xxfoobarzz")]
    [InlineData("foo(?=bar)", "xxfooquxzz")]
    [InlineData(@"ab?c", "zzacyy")]
    [InlineData(@"ab?c", "zzabcyy")]
    [InlineData(@"a[bc]?d", "zzadyy")]
    [InlineData(@"\d{2,4}", "x1234y")]
    [InlineData("(ab.cd)", "xxabXcdyy")]
    [InlineData("(?:cat|horse)", "zzhorseyy")]
    [InlineData("(?:ab|cd){2}", "xxcdabyy")]
    public void IsMatchMatchesRuntimeForAsciiSimplePatternSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var utf8Regex = new Utf8Regex(pattern, options);

        var expected = Regex.IsMatch(input, pattern, options);
        var actual = utf8Regex.IsMatch(Encoding.UTF8.GetBytes(input));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiSimplePatternSubset()
    {
        const string input = "abXcd abYcd zz";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("ab.cd", options);

        Assert.Equal(
            Regex.Count(input, "ab.cd", options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiCharacterClassSimplePatternSubset()
    {
        const string input = "ab1d ab2d abXd";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("ab[0-9]d", options);

        Assert.Equal(
            Regex.Count(input, "ab[0-9]d", options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAnchoredAsciiSimplePatternSubset()
    {
        const string input = "abc";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("^abc$", options);

        Assert.Equal(
            Regex.Count(input, "^abc$", options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForNativeUtf8LiteralAlternationSubset()
    {
        const string input = "café niño café resume niño";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("café|niño", options);

        Assert.Equal(
            Regex.Count(input, "café|niño", options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForBoundaryWrappedAsciiLiteralAlternationSubset()
    {
        const string input = "Task TaskFactory ValueTask IAsyncEnumerable Task";
        const string pattern = @"\b(?:Task|ValueTask|IAsyncEnumerable)\b";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiStructuralIdentifierFamily()
    {
        const string input = "TryGetCustomerRecordAsync GetvalueAsync CreateBuilderAsync GetAlpha";
        const string pattern = @"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiDeclarationIdentifierFamily()
    {
        const string input = "record CustomerExportJob struct worker_state class Worker_State";
        const string pattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiLoggingInvocationFamily()
    {
        const string input = "LogError(\"bad\"); LogDebug(\"skip\"); LogInformation (\"ok\");";
        const string pattern = @"\b(?:LogError|LogWarning|LogInformation)\s*\(";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiPrefixedFixedRunFamily()
    {
        const string input = "AKIAABCDEFGHIJKLMNOP AROAABCDEFGH12345678 ASIAABCDEFGH123456789";
        const string pattern = @"((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiStructuralTokenWindow()
    {
        const string input = "abcdefghij blah blah Result more words klmnopqrst xx short Result tiny abc";
        const string pattern = @"[A-Za-z]{10}\s+[\s\S]{0,100}Result[\s\S]{0,100}\s+[A-Za-z]{10}";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiStructuralTokenWindowWithUtf8Gap()
    {
        const string input = "abcdefghij préface Result après klmnopqrst";
        const string pattern = @"[A-Za-z]{10}\s+[\s\S]{0,100}Result[\s\S]{0,100}\s+[A-Za-z]{10}";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesRuntimeForAsciiStructuralRepeatedSegment()
    {
        const string input = "Alpha Beta Gamma Delta Epsilon Zeta Eta Theta Iota Kappa Lambda\nalpha beta gamma";
        const string pattern = @"(?:[A-Z][a-z]+\s*){10,100}";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(@"\d\d", "12 34 xx")]
    [InlineData(@"a\sb", "a b axb a\tb")]
    [InlineData(@"a{3}", "aaa aa aaaa")]
    [InlineData(@"\d{2}", "12 3 45")]
    [InlineData(@"[\d][A-Z]", "3Z 9X zz")]
    [InlineData(@"\n", "a\nb\nc")]
    [InlineData(@"\x41", "A BA")]
    [InlineData("cat|dog", "cat dog fish dog")]
    [InlineData("cat|horse", "cat horse fish horse")]
    [InlineData(@"ab?c", "ac abc abbc")]
    [InlineData(@"\d{2,4}", "1 12 123 1234 12345")]
    [InlineData("(?:cat|horse)", "cat horse fish horse")]
    [InlineData("(?:ab|cd){2}", "ab abcd cdab cdcd x")]
    public void CountMatchesRuntimeForEscapedAsciiSimplePatternSubset(string pattern, string input)
    {
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("^abc", "abc\nabc\nx", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData("a.b", "a\nb xa\nb", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    public void CountMatchesRuntimeForAdditionalNativeOptionFamilies(string pattern, string input, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void AsciiSimplePatternFallsBackForNonAsciiInput()
    {
        const string input = "ab😀cd";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex("ab.cd", options);

        Assert.Equal(
            Regex.IsMatch(input, "ab.cd", options),
            regex.IsMatch(Encoding.UTF8.GetBytes(input)));
    }
}

