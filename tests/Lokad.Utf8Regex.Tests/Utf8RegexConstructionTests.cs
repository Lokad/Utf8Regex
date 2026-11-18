using System.Text.RegularExpressions;
using System.Text;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexConstructionTests
{
    [Fact]
    public void ConstructorExposesConfiguration()
    {
        var regex = new Utf8Regex("abc", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(3)
        });

        Assert.Equal("abc", regex.Pattern);
        Assert.Equal(RegexOptions.CultureInvariant, regex.Options);
        Assert.Equal(TimeSpan.FromSeconds(3), regex.MatchTimeout);
    }

    [Fact]
    public void ConstructorClassifiesPlainAsciiLiteralAsNative()
    {
        var regex = new Utf8Regex("abc");

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.ExactLiteral, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorClassifiesPlainUtf8LiteralAsNative()
    {
        var regex = new Utf8Regex("café", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorClassifiesUtf8LiteralAlternationAsNative()
    {
        var regex = new Utf8Regex("café|niño", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorClassifiesBoundaryWrappedAsciiLiteralAlternationAsNative()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorKeepsBoundaryWrappedSharedPrefixAlternationOnLiteralExecution()
    {
        var regex = new Utf8Regex(@"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoundaryWrappedSharedPrefixLiteralFamilyCountMatchesRuntime(RegexOptions mode)
    {
        const string pattern = @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b";
        const string input = "LogTrace xLogDebug LogInformationX LogWarning LogError\nLogDebug éLogTrace LogDebugé Log";
        var options = mode | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void StructuralIdentifierFamilyMatchesAsyncSuffixCase()
    {
        var regex = new Utf8Regex(@"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("prefix TryGetCustomerAsync suffix");

        Assert.True(regex.IsMatch(input));
    }

    [Fact]
    public void StructuralIdentifierFamilyMatchesIdentifierSuffixWindowCase()
    {
        var regex = new Utf8Regex(@"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b", options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("await using var scope = await resource");

        Assert.True(regex.IsMatch(input));
    }

    [Fact]
    public void ConstructorClassifiesBoundaryWrappedUtf8LiteralAsNative()
    {
        var regex = new Utf8Regex(@"\bé\b", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorPreservesBoundaryWrappedAsciiLiteralSearchRequirements()
    {
        var regex = new Utf8Regex(@"\bword\b", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorPreservesOneSidedBoundaryWrappedUtf8LiteralSearchRequirements()
    {
        var leading = new Utf8Regex(@"\bé", options: RegexOptions.CultureInvariant);
        var trailing = new Utf8Regex(@"é\B", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, leading.Inspection.ExecutionKind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, leading.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.None, leading.Inspection.SearchPlan.TrailingBoundary);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, trailing.Inspection.ExecutionKind);
        Assert.Equal(Utf8BoundaryRequirement.None, trailing.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.NonBoundary, trailing.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorClassifiesRegexMetacharactersAsFallback()
    {
        var regex = new Utf8Regex("a.*b");

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("unsupported_loop", regex.Inspection.FallbackReason);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorQualifiesEscapedQuoteImportAlias()
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\\\"(?<path>(\\.|[^\\\\\\\"]*))\\\".*$";
        var regex = new Utf8Regex(pattern, options: RegexOptions.Multiline);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(1, regex.Count("import \"😀\nx\"tail"u8));
        Assert.True(Utf8MatchSequence.First(regex.CreateMatchCursor("import \"😀\nx\"tail"u8.ToArray())).TryGetGroup("path", out var path));
        Assert.Equal((8, 6), (path.StartOffsetInBytes, (path.EndOffsetInBytes - path.StartOffsetInBytes)));
    }

    [Fact]
    public void ConstructorKeepsNonBacktrackingScalarCaptures()
    {
        const string pattern = @"^\s*///(?<title>#.*)$";
        var regex = new Utf8Regex(pattern, options: RegexOptions.Multiline | RegexOptions.NonBacktracking);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("unsupported_options", regex.Inspection.FallbackReason);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.Equal("#😀", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor("///#😀"u8.ToArray())).GetGroupMemory(regex.GroupNumberFromUtf8Name("title"u8)).Span));
    }

    [Fact]
    public void CompiledIdentifierPreservesWholeByteMatch()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("Alpha_42"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(8, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(8, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledIdentifierValidatorMatchesBaselineAcrossCorpus()
    {
        AssertCompiledMatchesBaseline(
            "^[a-z][a-z0-9_]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            [
                [],
                "A"u8.ToArray(),
                "Alpha_42"u8.ToArray(),
                "Alpha_42\n"u8.ToArray(),
                "1alpha"u8.ToArray(),
                "Alpha-é"u8.ToArray(),
                "ALPHA__"u8.ToArray(),
            ]);
    }

    [Fact]
    public void NonCompiledAnchoredFixedLengthHexValidatorMatchesWholeSpan()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = regex.Match("A0f"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledAnchoredFixedLengthHexValidatorMatchesWholeSpan()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("A0f"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(3, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledFixedLengthHexValidatorMatchesBaselineAcrossCorpus()
    {
        AssertCompiledMatchesBaseline(
            "^[a-f0-9]{3}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            [
                [],
                "A0f"u8.ToArray(),
                "A0f\n"u8.ToArray(),
                "A0g"u8.ToArray(),
                "A0ff"u8.ToArray(),
                [ (byte)'A', (byte)'0', 0xC3, 0xA9 ],
            ]);
    }

    [Fact]
    public void CapturedRgbHexPreservesItsByteGroups()
    {
        var regex = new Utf8Regex("^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[4];
        Assert.Equal(new Utf8CaptureRange(0, 6), regex.Match("A0f1c2"u8, 0, slots));
        Assert.Equal(new Utf8CaptureRange(0, 2), slots[regex.GroupNumberFromUtf8Name("red"u8)]);
        Assert.Equal(new Utf8CaptureRange(2, 4), slots[regex.GroupNumberFromUtf8Name("green"u8)]);
        Assert.Equal(new Utf8CaptureRange(4, 6), slots[regex.GroupNumberFromUtf8Name("blue"u8)]);
    }

    [Fact]
    public void CompiledAnchoredRgbHexValidatorMatchesWholeSpan()
    {
        var regex = new Utf8Regex("^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("A0f1c2"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(6, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(6, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledRgbHexValidatorMatchesBaselineAcrossCorpus()
    {
        AssertCompiledMatchesBaseline(
            "^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            [
                [],
                "A0f1c2"u8.ToArray(),
                "A0f1c2\n"u8.ToArray(),
                "A0f1cg"u8.ToArray(),
                "A0f1c2x"u8.ToArray(),
                [ (byte)'A', (byte)'0', (byte)'f', (byte)'1', 0xC3, 0xA9, ],
            ]);
    }

    [Fact]
    public void CompiledAnchoredLiteralThenRunValidatorMatchesIgnoreCaseWholeSpan()
    {
        var regex = new Utf8Regex("^ab[a-f0-9]{2}$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("AB0F"u8.ToArray());
        Assert.True(match.Success);
        Assert.Equal(4, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledIdentifierMatchesValidAscii()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var match = regex.Match("alpha_123"u8);

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(9, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledIdentifierRejectsInvalidLeadingDigit()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var match = regex.Match("1alpha"u8);

        Assert.False(match.Success);
    }

    [Fact]
    public void CompiledAsciiIdentifierRejectsNonAsciiScalar()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.False(regex.Match("alphé"u8).Success);
    }

    [Fact]
    public void CompiledLiteralThenRunValidatorMatchesBaselineAcrossCorpus()
    {
        var compiled = new Utf8Regex("^ab[0-9]{2}$", options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        AssertCompiledMatchesBaseline(
            "^ab[0-9]{2}$",
            RegexOptions.CultureInvariant,
            [
                [],
                "ab01"u8.ToArray(),
                "ab01\n"u8.ToArray(),
                "AB01"u8.ToArray(),
                "ab0x"u8.ToArray(),
                "ab001"u8.ToArray(),
                [ (byte)'a', (byte)'b', (byte)'0', 0xC3, 0xA9 ],
            ]);
    }

    [Fact]
    public void CompiledIgnoreCaseLiteralThenRunValidatorMatchesBaselineAcrossCorpus()
    {
        var compiled = new Utf8Regex("^ab[a-f0-9]{2}$", options: RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        AssertCompiledMatchesBaseline(
            "^ab[a-f0-9]{2}$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            [
                [],
                "ab0f"u8.ToArray(),
                "AB0F"u8.ToArray(),
                "Ab0F\n"u8.ToArray(),
                "abfg"u8.ToArray(),
                "ax0f"u8.ToArray(),
                "ab0é"u8.ToArray(),
            ]);
    }

    [Fact]
    public void CompiledHelperIdentifierCanMatchWithoutValidation()
    {
        var regex = new Utf8Regex("[a-zA-Z][a-zA-Z0-9]*", options: RegexOptions.Compiled);

        var match = regex.Match("123alpha9"u8);

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(3, match.StartOffsetInBytes);
        Assert.Equal(6, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledHelperIdentifierCanRejectWithoutValidation()
    {
        var regex = new Utf8Regex("[a-zA-Z][a-zA-Z0-9]*", options: RegexOptions.Compiled);

        var match = regex.Match("123__"u8);

        Assert.False(match.Success);
    }

    [Fact]
    public void CompiledHelperIdentifierNeedsValidationForNonAsciiBeforeAsciiMatch()
    {
        var regex = new Utf8Regex("[a-zA-Z][a-zA-Z0-9]*", options: RegexOptions.Compiled);

        var handled = regex.TryMatchDirectWithoutValidation("éalpha"u8, out _);

        Assert.False(handled);
    }

    [Fact]
    public void DateTokensPreservePublicByteRanges()
    {
        var regex = new Utf8Regex(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", options: RegexOptions.Compiled);

        var match = regex.Match("Today is 11/18/2019 and tomorrow is 11/19/2019."u8);
        Assert.True(match.Success);
        Assert.Equal(("Today is ".Length, System.Text.Encoding.UTF8.GetByteCount("11/18/2019")), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(2, regex.Count("Today is 11/18/2019 and tomorrow is 11/19/2019."u8));
        Assert.False(regex.IsMatch("Today is xx/yy/zzzz and tomorrow is ??/??/????."u8));
    }

    [Fact]
    public void UriTokensPreservePublicByteRanges()
    {
        var regex = new Utf8Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", options: RegexOptions.Compiled);

        var input = "https://atlas.example.org/reports/export?id=42"u8;
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal((0, input.Length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(2, regex.Count("https://a.example/x ftp://b.example/y"u8));
        Assert.False(regex.IsMatch("https://a https://b"u8));
    }

    [Theory]
    [InlineData("#abc", true)]
    [InlineData("abc", true)]
    [InlineData("#a1b2c3", true)]
    [InlineData("a1b2c3", true)]
    [InlineData("#ab", false)]
    [InlineData("#abcd", false)]
    [InlineData("#abcdex", false)]
    public void HexColorAlternationPlanMatchesWholeSpan(string value, bool expected)
    {
        var regex = new Utf8Regex("^#?([a-f0-9]{6}|[a-f0-9]{3})$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var match = regex.Match(Encoding.ASCII.GetBytes(value));

        Assert.Equal(expected, match.Success);
        if (expected)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(value), (match.EndOffsetInBytes - match.StartOffsetInBytes));
        }
    }

    [Fact]
    public void AnchoredHexRejectsNonAsciiInput()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = regex.Match("éab"u8.ToArray());

        Assert.False(match.Success);
        Assert.Equal(0, regex.Count("éab"u8.ToArray()));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void ExactAsciiLiteralCountMatchesDotNetThroughPrioritizedRoute(RegexOptions options)
    {
        const string pattern = "tempus";
        const string input = "tempus é tempus xtempus";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void FiveByteExactAsciiLiteralCountMatchesDotNetOnMixedUtf8Input(RegexOptions options)
    {
        const string pattern = "Twain";
        const string input = "Twain é Twain 夏 MarkTwain";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void MediumExactAsciiLiteralCountMatchesDotNetOnMixedUtf8Input(RegexOptions options)
    {
        const string pattern = "Sherlock Holmes";
        const string input = "é Sherlock Holmes 夏 Sherlock Holmes Sherlock Watson";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void EscapedMediumAsciiLiteralCountUsesItsAsciiSearchPlan(RegexOptions options)
    {
        const string literal = "ConfigureAwait(";
        var pattern = Regex.Escape(literal);
        const string input = "é ConfigureAwait( 夏 ConfigureAwait(false";
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("foo(?=bar)", "fooqux foobar foo")]
    [InlineData("^tempus", "tempus x tempus")]
    [InlineData("tempus$", "tempus x tempus")]
    [InlineData(@"\btempus\b", "xtempus tempus tempusx")]
    public void ConstrainedExactAsciiLiteralCountRetainsSemanticRoute(string pattern, string input)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CompiledMatchUsesDirectDocLinePath()
    {
        var regex = new Utf8Regex("\\G///[^\\n]*\\n", options: RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("/// doc line\n"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(13, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(13, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledDocLineDirectHookHandlesMatchWithoutValidation()
    {
        var regex = new Utf8Regex("\\G///[^\\n]*\\n", options: RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("/// doc line\n"u8);

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(13, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void ConstructorTreatsUnicodeCategoryRepeatAsOwnedSlice()
    {
        const string pattern = "\\p{L}{8,13}";
        const string input = "abcdefghijklmnop";
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(Regex.Count(input, pattern), regex.Count(bytes));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void ConstructorAcceptsUnicodeSymbolCategory()
    {
        var regex = new Utf8Regex(@"\p{Sm}", options: RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorClassifiesAsciiUntilByteStarCount()
    {
        var regex = new Utf8Regex(@"[^\n]*", options: RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }

    [Theory]
    [InlineData(@"\w{10,}")]
    [InlineData(@"\b\w{10,}\b")]
    public void ConstructorClassifiesWordRunCount(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void UnicodeSymbolCategoryMatchesLikeDotNet()
    {
        const string pattern = @"\p{Sm}+";
        const string input = "sum=∑ x + y ≠ z and text";
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), regex.Count(bytes));
        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledUnicodeMathSymbolCountMatchesDotNet()
    {
        const string pattern = @"\p{Sm}";
        const string input = "sum=∑ x + y ≠ z and text ∫ 𐄂 < > | ~";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void DeterministicFixedTokenPatternPreservesPublicBehavior()
    {
        var regex = new Utf8Regex(@"ab[0-9]d", options: RegexOptions.CultureInvariant);
        var input = "xx ab3d yy ab7d zz"u8;

        Assert.True(regex.IsMatch(input));
        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(3, match.StartOffsetInBytes);
        Assert.Equal(4, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void AnyByteDeterministicFixedTokenPatternPreservesPublicBehavior()
    {
        var regex = new Utf8Regex(@"ab.[0-9]d", options: RegexOptions.CultureInvariant);
        var input = "xx ab_3d yy ab!7d zz"u8;

        Assert.True(regex.IsMatch(input));
        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(3, match.StartOffsetInBytes);
        Assert.Equal(5, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void ConstructorClassifiesIgnoreCaseAsFallbackInFirstVersion()
    {
        var regex = new Utf8Regex("abc", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCase, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorClassifiesNonInvariantIgnoreCaseAsFallback()
    {
        var regex = new Utf8Regex("abc", options: RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
    }

    [Fact]
    public void ConstructorClearsUnsupportedLoopReasonForDirectCellReferenceFamily()
    {
        var regex = new Utf8Regex("^(?<col>[a-z])(?<row>(\\d)+)$", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorUsesOwnedCultureProgramForDirectCellReferenceFamily()
    {
        var regex = new Utf8Regex("^(?<col>[a-z])(?<row>(\\d)+)$", options: RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
        Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void ConstructorUsesOwnedCultureProgramForDashboardUrlIgnoreCasePattern()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\\d+)/?\\?t=(?<tab>[^ ?]+)(?<rest>.*)$";
        var regex = new Utf8Regex(pattern, options: RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
        Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void ConstructorUsesOwnedCultureProgramForDownloadUrlIgnoreCasePattern()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \\n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \\n]+)?(?<rest>.*)$";
        var regex = new Utf8Regex(pattern, options: RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
        Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void ConstructorExposesFallbackReasonForDeferredOptions()
    {
        var regex = new Utf8Regex("abc", options: RegexOptions.RightToLeft | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Null(regex.Inspection.FallbackReason);
    }

    [Fact]
    public void ConstructorAcceptsCompiledOptionAsNonSemanticHint()
    {
        var regex = new Utf8Regex("abc", options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.Equal(RegexOptions.Compiled | RegexOptions.CultureInvariant, regex.Options);
        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.ExactLiteral, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void CompiledHintPreservesScalarLoopOperations()
    {
        var baseline = new Utf8Regex("a.*b", options: RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex("a.*b", options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, compiled.Inspection.ExecutionKind);
        Assert.True(baseline.IsMatch("😀 a𐐀b"u8));
        Assert.True(compiled.IsMatch("😀 a𐐀b"u8));
        Assert.Equal(baseline.IsMatch("zz a123b xx"u8), compiled.IsMatch("zz a123b xx"u8));
        Assert.Equal(baseline.Count("a1b a22b"u8), compiled.Count("a1b a22b"u8));
    }

    [Fact]
    public void BalancingConditionalsKeepPublicResultsAcrossCompilationOptions()
    {
        const string pattern = "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))";
        var baseline = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = "BEGIN x BEGIN y END z END BEGIN END"u8;

        Assert.Equal(NativeExecutionKind.FallbackRegex, compiled.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, baseline.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, compiled.Inspection.CompiledEngineKind);
        Assert.Equal(baseline.IsMatch(input), compiled.IsMatch(input));
        Assert.Equal(baseline.Count(input), compiled.Count(input));
    }

    private static void AssertCompiledMatchesBaseline(
        string pattern,
        RegexOptions options,
        byte[][] inputs)
    {
        var baseline = new Utf8Regex(pattern, options: options);
        var compiled = new Utf8Regex(pattern, options: options | RegexOptions.Compiled);

        foreach (var input in inputs)
        {
            var baselineMatch = baseline.Match(input);
            var compiledMatch = compiled.Match(input);

            Assert.Equal(baselineMatch.Success, compiledMatch.Success);
            Assert.Equal(baselineMatch.Success, compiledMatch.Success);
            Assert.Equal(baselineMatch.StartOffsetInBytes, compiledMatch.StartOffsetInBytes);
            Assert.Equal((baselineMatch.EndOffsetInBytes - baselineMatch.StartOffsetInBytes), (compiledMatch.EndOffsetInBytes - compiledMatch.StartOffsetInBytes));
            var baselineIndexInBytes = baselineMatch.StartOffsetInBytes;
            var baselineLengthInBytes = (baselineMatch.EndOffsetInBytes - baselineMatch.StartOffsetInBytes);
            var compiledIndexInBytes = compiledMatch.StartOffsetInBytes;
            var compiledLengthInBytes = (compiledMatch.EndOffsetInBytes - compiledMatch.StartOffsetInBytes);
            if (baselineMatch.Success &&
                compiledMatch.Success)
            {
                Assert.Equal(baselineIndexInBytes, compiledIndexInBytes);
                Assert.Equal(baselineLengthInBytes, compiledLengthInBytes);
            }
            Assert.Equal(baseline.IsMatch(input), compiled.IsMatch(input));

            var oracle = new Regex(pattern, options | RegexOptions.Compiled);
            var text = Encoding.UTF8.GetString(input);
            Assert.Equal(oracle.IsMatch(text), compiled.IsMatch(input));
            Assert.Equal(oracle.Count(text), compiled.Count(input));

        }
    }

    [Fact]
    public void ConstructorPreservesCompiledOptionOnOwnedBalancingProgram()
    {
        const string pattern = "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.True((compiled.Options & RegexOptions.Compiled) != 0);
        Assert.True(compiled.Inspection.DebugUsesOwnedSliceProgram);
        var input = Encoding.UTF8.GetBytes("BEGINBEGIN\U0001f600ENDEND");
        var match = Utf8MatchSequence.First(compiled.CreateMatchCursor(input));
        Assert.True(match.Success);
        Assert.Equal((0, input.Length), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
        Assert.False((match.TryGetGroup("open", out var range1) ? range1.Success : default));
        Assert.Equal(1, compiled.Count(input));
        Assert.True(compiled.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(compiled.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, compiled.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Fact]
    public void CompiledOwnedCanCountUnicodeLetterBoundedRepeatDirectly()
    {
        const string pattern = "\\p{L}{8,13}";
        const string input = "контексты словообразование abcdefghijklmn data123 короткий";
        var baseline = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.True(compiled.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
        Assert.Equal(baseline.Count(bytes), compiled.Count(bytes));
        Assert.True(compiled.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void CompiledOwnedUnicodeLetterBoundedRepeatConsumesSupplementaryScalars()
    {
        const string pattern = "\\p{L}{8,13}";
        const string input = "𐐀𐐁𐐂𐐃𐐄𐐅𐐆𐐇 tail";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(1, compiled.Count(bytes));
        Assert.True(compiled.IsMatch(bytes));
        var match = compiled.Match(bytes);
        Assert.Equal((0, 32), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.True(compiled.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void CompiledOwnedCanCountGenericUnicodeLetterBoundedRepeatDirectly()
    {
        const string pattern = "\\p{L}{3,5}";
        const string input = "контексты alpha beta 世界世界世界 log42";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledWeakEmailPrefilterLoopCountMatchesDotNet()
    {
        const string pattern = "[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+";
        const string input = "ops.dispatch@northwind-control.net and admin@example.org";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledWeakUriPrefilterLoopCountMatchesDotNet()
    {
        const string pattern = "[\\w]+://[^/\\s?#]+[^\\s?#]+(?:\\?[^\\s#]*)?(?:#[^\\s]*)?";
        const string input = "https://atlas.example.org/reports/export?id=42 and ftp://mirror.example.net/pub";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledBoundedSuffixLiteralCountMatchesDotNet()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " sing  bringing  abcdefghijklming  abcdefghijklmning  ring ";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void BaselineBoundedSuffixLiteralCountMatchesDotNet()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " sing  bringing  abcdefghijklming  abcdefghijklmning  ring ";
        var baseline = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Fact]
    public void BaselineBoundedSuffixLiteralCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " café sing résumé bringing naïve ring 東京 abcdefghijklmning ";
        var baseline = new Utf8Regex(pattern, options: RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoundedSuffixLiteralCountKeepsUnicodeWhitespace(RegexOptions options)
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = "\u00A0sing\u2003 café \u0085bringing\u2028 naïve ring 東京";
        var regex = new Utf8Regex(pattern, options: options);
        var bytes = Encoding.UTF8.GetBytes(input);
        Assert.Equal(Regex.Count(input, pattern, options), regex.Count(bytes));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoundedSuffixScalarWhitespaceKeepsResultsAndCancellation(RegexOptions options)
    {
        const string pattern = @"\s[a-zA-Z]{0,12}ing\s";
        var input = Encoding.UTF8.GetBytes("😀\u00a0sing\u2003𐐀");
        var regex = new Utf8Regex(pattern, options: options);
        using var source = new CancellationTokenSource();
        Assert.Equal(1, regex.Count(input));
        Assert.Equal(1, regex.Count(input, source.Token));
        Assert.Equal(1, new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1)
        }).Count(input));
        source.Cancel();
        Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token));
        Assert.Throws<OperationCanceledException>(() => regex.Count(new byte[] { 0xff }, source.Token));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void IgnoreCaseBoundedSuffixLiteralCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = "\u00A0SING\u2003 café \u0085BrInGiNg\u2028";
        const RegexOptions options = RegexOptions.IgnoreCase;
        var regex = new Utf8Regex(pattern, options: options);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern, options), regex.Count(bytes));
    }

    [Fact]
    public void BaselineBoundedSuffixLiteralMatchesWhenPrefixWhitespaceFollowsLetter()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = "word sing next";
        var baseline = new Utf8Regex(pattern, options: RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Fact]
    public void CompiledBoundedSuffixLiteralMatchRejectsOverlongRun()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " abcdefghijklmning ";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.False(compiled.Match(bytes).Success);
    }

    [Fact]
    public void CompiledAnchoredFixedAlternationSimplePatternMatchesDotNet()
    {
        const string pattern = "^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$";
        string[] inputs = ["SW1 1ZZ", "M11AE", "A1B 2CD", "ABC 123", "S1 1Z9"];

        foreach (var input in inputs)
        {
            var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var bytes = Encoding.UTF8.GetBytes(input);
            Assert.Equal(Regex.IsMatch(input, pattern), compiled.IsMatch(bytes));
        }
    }

    [Fact]
    public void BaselineOwnedCanCountGenericUnicodeLetterBoundedRepeatDirectly()
    {
        const string pattern = "\\p{L}{3,5}";
        const string input = "контексты alpha beta 世界世界世界 log42";
        var baseline = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Fact]
    public void CompiledAsciiIpv4TokenCountMatchesDotNet()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string input = "bad 1.2.3.4 then 25.26.27.28 and 999.0.0.1 ok 012.200.033.199 and 255.10.0.2 end";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledAsciiIpv4TokenMatchMatchesDotNet()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string input = "prefix 012.200.033.199 suffix";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        var match = compiled.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(Regex.Match(input, pattern).Value, Encoding.UTF8.GetString(bytes.AsSpan(match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))));
    }

    [Fact]
    public void CompiledAsciiIpv4TokenMissMatchesDotNet()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string input = "999.200.033.199";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.IsMatch(input, pattern), compiled.IsMatch(bytes));
    }

    [Fact]
    public void AsciiIpv4DotAnchoringMatchesDotNetAcrossDenseCandidateText()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string alphabet = "0123456789... abcxyz";
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var ordinary = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var random = new Random(0x4950_7634);

        for (var sample = 0; sample < 256; sample++)
        {
            var chars = new char[128];
            for (var i = 0; i < chars.Length; i++)
            {
                chars[i] = alphabet[random.Next(alphabet.Length)];
            }

            if (sample % 4 == 0)
            {
                const string inserted = "255.10.00.199";
                inserted.CopyTo(0, chars, random.Next(chars.Length - inserted.Length), inserted.Length);
            }

            var input = new string(chars);
            var bytes = Encoding.UTF8.GetBytes(input);
            var expectedCount = oracle.Count(input);
            var expectedMatch = oracle.Match(input);
            var ordinaryMatch = ordinary.Match(bytes);
            var compiledMatch = compiled.Match(bytes);

            Assert.Equal(expectedCount, ordinary.Count(bytes));
            Assert.Equal(expectedCount, compiled.Count(bytes));
            Assert.Equal(expectedMatch.Success, ordinaryMatch.Success);
            Assert.Equal(expectedMatch.Success, compiledMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedMatch.Index)), ordinaryMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expectedMatch.Index, expectedMatch.Length)), (ordinaryMatch.EndOffsetInBytes - ordinaryMatch.StartOffsetInBytes));
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedMatch.Index)), compiledMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expectedMatch.Index, expectedMatch.Length)), (compiledMatch.EndOffsetInBytes - compiledMatch.StartOffsetInBytes));
            }
        }
    }

    [Fact]
    public void CompiledAsciiUntilByteStarCountMatchesDotNet()
    {
        const string pattern = @"[^\n]*";
        const string input = "alpha\n\nbeta\ngamma";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledAsciiUntilByteStarCountMatchesDotNetOnUtf8Input()
    {
        const string pattern = @"[^\n]*";
        const string input = "alpha\ncafeé\nαβ\n";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledUnicodeLetterCountUsesScalarCategories()
    {
        const string pattern = @"\p{L}";
        const string input = "alpha βγ 夏洛克 𐐀 123 _";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        Assert.Equal(11, compiled.Count(Encoding.UTF8.GetBytes(input)));
        Utf8AsciiScalarOperationAssertions.Operations(pattern, RegexOptions.Compiled, input);
    }

    [Fact]
    public void CompiledUtf8WordDelimitedTokenEmailCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        const string input = "été alpha@example.org βγ ops.dispatch@northwind-control.net 夏洛克";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledUtf8UriTokenCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        const string input = "été https://atlas.example.org/reports/export?id=42 βγ http://northwind-control.net/a#frag 夏洛克";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Theory]
    [InlineData("é://host/path", 1)]
    [InlineData("https://hôte.example/路径", 1)]
    [InlineData("https://host.example/a\u00A0http://next.example/b", 2)]
    [InlineData("𐐀://host/path https://ok.example/a", 2)]
    [InlineData("x://a x://ab x:////", 1)]
    [InlineData("x://ab?query#fragment x://cd##tail", 2)]
    public void Utf8UriTokenCountUsesScalarWordSemantics(string input, int expected)
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Utf8AsciiScalarOperationAssertions.Operations(pattern, options, input);
            Assert.Equal(expected, regex.Count(bytes));
        }
    }

    [Theory]
    [InlineData(@"[\w\.+-]+@[\w\.-]+\.[\w\.-]+", "été@example.org alpha@example.org")]
    [InlineData(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", "é://host.example/path https://atlas.example/path")]
    [InlineData(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", "https://hôte.example/路径 https://atlas.example/path")]
    [InlineData(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", "١١/١٨/٢٠١٩ 11/18/2019")]
    [InlineData(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", "é11/18/2019é 11/18/2019")]
    public void UnicodeSensitiveDirectFamilyCountMatchesDotNetOnMixedInput(string pattern, string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            Assert.Equal(oracle.Count(input), regex.Count(bytes));
        }
    }

    [Fact]
    public void ExplicitAsciiStructuredTokenCountRemainsEligibleOnMixedInput()
    {
        const string pattern = @"[A-Za-z]+://[A-Za-z0-9.]+[A-Za-z0-9/]+";
        const string input = "é https://atlas.example/path 夏";
        var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        Assert.Equal(Regex.Count(input, pattern), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CompiledAsciiUriTokenMatchMatchesDotNetForAsciiInput()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        const string input = "https://atlas.example.org/reports/export?id=42";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.IsMatch(input, pattern), compiled.IsMatch(bytes));
        Assert.Equal(Regex.Match(input, pattern).Value, Encoding.UTF8.GetString(bytes[compiled.Match(bytes).StartOffsetInBytes..(compiled.Match(bytes).StartOffsetInBytes + (compiled.Match(bytes).EndOffsetInBytes - compiled.Match(bytes).StartOffsetInBytes))]));
    }

    [Fact]
    public void CompiledExactUtf8LiteralCanCountWithFusedValidation()
    {
        const string pattern = "夏洛克·福尔摩斯";
        const string input = "夏洛克·福尔摩斯 abc 夏洛克·福尔摩斯";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, compiled.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.ExactLiteral, compiled.Inspection.CompiledEngineKind);
        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledExactUtf8LiteralFusedValidationHandlesSupplementaryScalars()
    {
        const string pattern = "夏洛克·福尔摩斯";
        const string input = "𐐀𐐁 夏洛克·福尔摩斯 𐐂";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void ExactTwoByteLiteralCorrelatedPrefixCountCoversEveryVectorLane()
    {
        const string pattern = "Шерлок Холмс";
        var literal = Encoding.UTF8.GetBytes(pattern);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        foreach (var lane in Enumerable.Range(0, 32))
        {
            var bytes = Enumerable.Repeat((byte)'x', 132 * 1024).ToArray();
            var matchOffset = 64 * 1024 + lane;
            literal.CopyTo(bytes, matchOffset);
            var input = Encoding.UTF8.GetString(bytes);

            foreach (var options in new[]
                     {
                         RegexOptions.CultureInvariant,
                         RegexOptions.CultureInvariant | RegexOptions.Compiled,
                     })
            {
                var regex = new Utf8Regex(pattern, options: options);
                Assert.Equal(oracle.Count(input), regex.Count(bytes));
            }
        }
    }

    [Fact]
    public void CompiledExactUtf8LiteralFamilyCanCountWithFusedValidation()
    {
        const string pattern = "夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授";
        const string input = "夏洛克·福尔摩斯 约翰华生 x 阿德勒 雷斯垂德 莫里亚蒂教授";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, compiled.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, compiled.Inspection.CompiledEngineKind);
        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledExactUtf8LiteralFamilyFusedValidationHandlesSupplementaryScalars()
    {
        const string pattern = "夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授";
        const string input = "𐐀 夏洛克·福尔摩斯 𐐁 约翰华生";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledExactUtf8LiteralFamilyFusedValidationHandlesMixedScalarLengths()
    {
        const string pattern = "阿|阿德勒|雷斯垂德";
        const string input = "𐐀 阿 x 阿德勒 y 雷斯垂德 z 阿";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyReplaceMatchesDotNet()
    {
        const string pattern = "tempus|magna|semper";
        const string input = "tempus magna semper et tempus";
        const string replacement = "amoveatur";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Replace(input, pattern, replacement), Encoding.UTF8.GetString(compiled.Replace(bytes, replacement)));
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilySplitMatchesDotNet()
    {
        const string pattern = "tempus|magna|semper";
        const string input = "tempus magna semper et tempus";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = Regex.Split(input, pattern);
        var actual = new List<string>();

        foreach (var split in compiled.EnumerateSplits(bytes))
        {
            actual.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        }

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallAsciiLiteralFamilySplitShapesMatchDotNet(bool compiled)
    {
        var options = compiled ? RegexOptions.Compiled : RegexOptions.None;
        (string Pattern, string Input)[] cases =
        [
            ("tempus|magna|semper", "tempus magna semper et tempus"),
            ("Sherlock Holmes|John Watson|Mycroft Holmes|Mary Morstan|Mrs Hudson", "John Watson and Mrs Hudson met Sherlock Holmes"),
            ("alpha|alphabet|bravo", "alphabet bravo alpha"),
            ("alpha|bravo|charlie", "no matching tokens here"),
        ];

        foreach (var testCase in cases)
        {
            var regex = new Utf8Regex(testCase.Pattern, options: options);
            var bytes = Encoding.UTF8.GetBytes(testCase.Input);
            var actual = new List<string>();

            Assert.True(
                regex.TryGetSmallAsciiLiteralFamilySearch(bytes, out _),
                $"Expected small ASCII literal-family Split route for '{testCase.Pattern}'.");
            foreach (var split in regex.EnumerateSplits(bytes))
            {
                actual.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
            }

            Assert.Equal(Regex.Split(testCase.Input, testCase.Pattern, options), actual);
        }
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, int.MaxValue)]
    public void SmallAsciiLiteralFamilySplitHonorsResultCount(bool compiled, int count)
    {
        const string pattern = "tempus|magna|semper";
        const string input = "tempus magna semper et tempus";
        var options = compiled ? RegexOptions.Compiled : RegexOptions.None;
        var regex = new Utf8Regex(pattern, options: options);
        var managed = new Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(input);

        var actual = new List<string>();
        foreach (var split in regex.EnumerateSplits(bytes, count))
        {
            actual.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        }

        Assert.Equal(managed.Split(input, count), actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallAsciiLiteralFamilySplitEnumerationDoesNotAllocatePerCall(bool compiled)
    {
        var options = compiled ? RegexOptions.Compiled : RegexOptions.None;
        var regex = new Utf8Regex("tempus|magna|semper", options: options);
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("tempus magna semper ", 64)));

        var count = 0;
        // Keep tier promotion out of the allocation window. A short 96-call warmup can
        // still trigger dynamic-PGO work during the measured loop in the full test suite.
        for (var i = 0; i < 2_048; i++)
        {
            count = CountSplitValues(regex, bytes);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_024; i++)
        {
            count = CountSplitValues(regex, bytes);
        }

        Assert.Equal(193, count);
        Assert.InRange(GC.GetAllocatedBytesForCurrentThread() - before, 0, 512);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void IneligibleLiteralFamilySplitShapesKeepFallbackSemantics(bool compiled)
    {
        var options = compiled ? RegexOptions.Compiled : RegexOptions.None;
        (string Pattern, string Input)[] cases =
        [
            ("foo|bar", "foo bar foobar"),
            ("alpha|bravo|charlie|deltaa|echoo|foxtrot|golfx", "golfx alpha foxtrot"),
            ("éclair|bravo|charlie", "éclair bravo charlie"),
        ];

        foreach (var testCase in cases)
        {
            var regex = new Utf8Regex(testCase.Pattern, options: options);
            var bytes = Encoding.UTF8.GetBytes(testCase.Input);
            var actual = new List<string>();

            Assert.False(regex.TryGetSmallAsciiLiteralFamilySearch(bytes, out _));
            foreach (var split in regex.EnumerateSplits(bytes))
            {
                actual.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
            }

            Assert.Equal(Regex.Split(testCase.Input, testCase.Pattern, options), actual);
        }

        Assert.False(new Utf8Regex("(alpha|bravo|charlie)", options: options)            .TryGetSmallAsciiLiteralFamilySearch("alpha bravo"u8, out _));
        Assert.False(new Utf8Regex(                "alpha|bravo|charlie", options:                 options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1)
        })            .TryGetSmallAsciiLiteralFamilySearch("alpha bravo"u8, out _));
        Assert.False(new Utf8Regex("alpha|bravo|charlie", options: options | RegexOptions.RightToLeft)            .TryGetSmallAsciiLiteralFamilySearch("alpha bravo"u8, out _));

        var malformed = new byte[] { (byte)'a', 0xC3, 0x28, (byte)'b' };
        var eligible = new Utf8Regex("alpha|bravo|charlie", options: options);
        Assert.False(eligible.TryGetSmallAsciiLiteralFamilySearch(malformed, out _));
    }

    private static int CountSplitValues(Utf8Regex regex, byte[] input)
    {
        var count = 0;
        foreach (var _ in regex.EnumerateSplits(input))
        {
            count++;
        }

        return count;
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyCountMatchesDotNet()
    {
        const string pattern = "Tom|Sawyer|Huckleberry|Finn";
        const string input = "Tom Finn Sawyer Huckleberry and Tom again";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ThreeByteAsciiLiteralFamilyCountMatchesDotNet(bool compiled, bool finiteTimeout)
    {
        const string pattern = "cat|dog|yak";
        const string input = "cat caq dog yakcat xdog yak é cat";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var timeout = finiteTimeout ? TimeSpan.FromSeconds(1) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout
        });
        var oracle = new Regex(pattern, options, timeout);

        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void DistinctFirstByteLiteralFamilyCountMatchesDotNetAcrossSelectorBoundary(
        bool compiled,
        bool finiteTimeout)
    {
        const string pattern = "a|needle";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var timeout = finiteTimeout ? TimeSpan.FromSeconds(1) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout
        });
        var oracle = new Regex(pattern, options, timeout);
        string[] inputs =
        [
            new('a', 4095),
            new('a', 4096),
            new('a', 8192),
            new('n', 8192),
            new string('x', 5000) + "needle" + new string('x', 4000),
            "needle" + new string('a', 8192),
            new string('x', 8190) + "need",
        ];

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        foreach (var input in inputs)
        {
            Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneByteLiteralPairCountSelectorLeavesExcludedFamiliesOnDotNetSemantics(bool compiled)
    {
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        const string inputPrefix = "é ";
        var input = inputPrefix + new string('a', 4096) +
            " needle token alpha bravo able ax " + new string('n', 4096) + " 夏";
        string[] patterns =
        [
            "alpha|needle",
            "a|needle|token",
            "a|able",
            "alpha|bravo",
            "alpha|able",
            @"\b(?:a|needle)\b",
            "(?:a|needle)x",
        ];

        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var pattern in patterns)
        {
            var regex = new Utf8Regex(pattern, options: options);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            Assert.Equal(oracle.Count(input), regex.Count(bytes));
        }
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyMatchMatchesDotNet()
    {
        const string pattern = "Tom|Sawyer|Huckleberry|Finn";
        const string input = "noise Sawyer tail";
        var compiled = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);
        var match = compiled.Match(bytes);

        Assert.True(match.Success);
        Assert.Equal(Regex.Match(input, pattern).Value, Encoding.UTF8.GetString(bytes.AsSpan(match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, false)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(true, true, true)]
    public void LiteralFamilyIsMatchMatchesDotNetAcrossExecutionModes(
        bool ignoreCase,
        bool compiled,
        bool finiteTimeout)
    {
        const string pattern = "alpha|alphabet|bravo|charlie|delta";
        var options = RegexOptions.CultureInvariant |
            (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var timeout = finiteTimeout ? TimeSpan.FromSeconds(1) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout
        });
        var oracle = new Regex(pattern, options, timeout);
        string[] inputs =
        [
            string.Empty,
            "alpha",
            "xx alphabet yy",
            "xx ALPHA yy",
            "é xx bravo yy 夏",
            "xx charlie",
            "delt",
            "alphab",
            "xx foxtrot yy",
        ];

        Assert.Equal(
            ignoreCase ? NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals : NativeExecutionKind.ExactUtf8Literals,
            regex.Inspection.ExecutionKind);
        foreach (var input in inputs)
        {
            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExactAutomatonLiteralFamilyIsMatchMatchesDotNet(
        bool compiled,
        bool finiteTimeout)
    {
        const string pattern =
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var timeout = finiteTimeout ? TimeSpan.FromSeconds(1) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout
        });
        var oracle = new Regex(pattern, options, timeout);
        string[] inputs =
        [
            "noise Professor Moriarty tail",
            "no member of the literal family appears here",
            new string('x', 8_192),
            "é Inspector Lestrade 夏",
        ];

        foreach (var input in inputs)
        {
            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodeLiteralFamilyIsMatchMatchesDotNet(bool compiled)
    {
        const string pattern = "кошка|собака|лисица|медведь|волк";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        string[] inputs =
        [
            string.Empty,
            "кот",
            "ёж и лисица",
            "кошка у окна",
            "медвед",
            "犬と猫",
        ];

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        foreach (var input in inputs)
        {
            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LiteralFamilyIsMatchPreservesTrailingLookahead(
        bool ignoreCase,
        bool compiled)
    {
        const string pattern = "(?:cat|dog)(?=house)";
        var options = RegexOptions.CultureInvariant |
            (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        string[] inputs =
        [
            "doghouse",
            "xx cathouse yy",
            "dog shed",
            "DOGhouse",
            "DOGHOUSE",
            "é cathouse 夏",
        ];

        Assert.Equal(
            ignoreCase ? NativeExecutionKind.FallbackRegex : NativeExecutionKind.ExactUtf8Literals,
            regex.Inspection.ExecutionKind);
        foreach (var input in inputs)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var expectedMatch = oracle.Match(input);
            var actualMatch = regex.Match(bytes);

            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(bytes));
            Assert.Equal(oracle.Count(input), regex.Count(bytes));
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RightToLeftFallbackScalarOperationsMatchDotNet(bool compiled)
    {
        const string pattern = "alpha|alphabet|bravo|charlie|delta";
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        string[] inputs =
        [
            string.Empty,
            "prefix alpha xx delta suffix",
            "é alpha xx delta 夏",
            "xx foxtrot yy",
        ];

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        foreach (var input in inputs)
        {
            var bytes = Encoding.UTF8.GetBytes(input);
            var expectedMatch = oracle.Match(input);
            var actualMatch = regex.Match(bytes);

            Assert.Equal(oracle.IsMatch(input), regex.IsMatch(bytes));
            Assert.Equal(oracle.Count(input), regex.Count(bytes));
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LiteralFamilyIsMatchDoesNotAllocatePerCall(bool compiled)
    {
        const string pattern = "alpha|alphabet|bravo|charlie|delta";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes("é " + new string('x', 32_768) + " 夏");
        // Warm the measured call site through delayed tiering, rather than
        // switching from a short warmup loop to a different measured loop.
        var warmup = System.Diagnostics.Stopwatch.StartNew();
        do { _ = MeasureLiteralFamilyIsMatchBatch(regex, input, out var warmedCorrect); Assert.True(warmedCorrect); }
        while (warmup.ElapsedMilliseconds < 1_000);
        var allocated = MeasureLiteralFamilyIsMatchBatch(regex, input, out var correct);
        Assert.True(correct);
        Assert.InRange(allocated, 0, 512);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static long MeasureLiteralFamilyIsMatchBatch(Utf8Regex regex, byte[] input, out bool correct)
    {
        correct = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++) correct &= !regex.IsMatch(input);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyCanMatchWithoutValidation()
    {
        var regex = new Utf8Regex("tempus|magna|semper", options: RegexOptions.None);

        var handled = regex.TryMatchDirectWithoutValidation("xxx magna yyy"u8, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(4, match.StartOffsetInBytes);
        Assert.Equal(5, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyCanMatchWithoutValidationOnLongCorpus()
    {
        const string paragraph =
            "Vitae magna tempus nibh, sed semper arcu posuere a. " +
            "Tempus erat at magna aliquet, non feugiat nunc semper. " +
            "Aliquam magna vel lectus tempus placerat, sed semper mi dictum. ";

        var builder = new StringBuilder(8_192);
        while (builder.Length < 8_192)
        {
            builder.Append(paragraph);
        }

        var regex = new Utf8Regex("tempus|magna|semper", options: RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());

        var handled = regex.TryMatchDirectWithoutValidation(bytes, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.True(match.Success);
    }

    [Fact]
    public void ExactAsciiLiteralFamilyDirectMatchValidatesNonAsciiSuffix()
    {
        var regex = new Utf8Regex("tempus|magna|semper", options: RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes("magna café");

        var handled = regex.TryMatchDirectWithoutValidation(bytes, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(5, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void ExactAsciiLiteralFamilyDirectMatchDeclinesNonAsciiPrefixProjection()
    {
        var regex = new Utf8Regex("tempus|magna|semper", options: RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes("café magna");

        Assert.False(regex.TryMatchDirectWithoutValidation(bytes, out _));

        var match = regex.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(6, match.StartOffsetInBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AsciiLiteralFamilyCanReportDefinitiveUnvalidatedMiss(bool ignoreCase, bool compiled)
    {
        var options = RegexOptions.CultureInvariant |
            (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("tempus|magna|semper", options: options);

        var handled = regex.TryMatchDirectWithoutValidation("xxx nihil yyy"u8, out var match);

        Assert.True(handled);
        Assert.False(match.Success);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LargerAsciiLiteralFamilyCanReportDefinitiveUnvalidatedMiss(bool ignoreCase, bool compiled)
    {
        var options = RegexOptions.CultureInvariant |
            (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("alpha|bravo|charlie|delta|echo", options: options);

        var handled = regex.TryMatchDirectWithoutValidation("xxx foxtrot yyy"u8, out var match);

        Assert.True(handled);
        Assert.False(match.Success);
    }

    [Theory]
    [InlineData(@"(?<word>ab)\k<word>", "unsupported_backreference")]
    [InlineData("(?!ab)ab", "unsupported_lookaround")]
    [InlineData("(?>ab)c", "unsupported_atomic")]
    [InlineData("(a)?(?(1)ab|cd)", "unsupported_conditional")]
    [InlineData("(?(?=a)b|c)", "unsupported_conditional")]
    [InlineData("ab+", "unsupported_loop")]
    public void ConstructorExposesSemanticFallbackReasonForUnsupportedPatternFamilies(string pattern, string expectedReason)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(expectedReason, regex.Inspection.FallbackReason);
    }

    [Fact]
    public void ConstructorClassifiesPositiveLiteralLookaheadAsAsciiSimplePattern()
    {
        var regex = new Utf8Regex("foo(?=bar)", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8CompiledEngineKind.ExactLiteral, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorKeepsInvariantIgnoreCasePositiveLiteralLookaheadOnFallback()
    {
        var regex = new Utf8Regex("foo(?=bar)", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorKeepsInvariantIgnoreCasePositiveLiteralAlternationLookaheadOnFallback()
    {
        var regex = new Utf8Regex("(?:cat|dog)(?=house)", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(31)]
    [InlineData(32)]
    [InlineData(47)]
    [InlineData(63)]
    public void SeparatorLiteralLookaheadCountRetainsCandidatesAcrossVectorLanes(int matchIndex)
    {
        const string pattern = "HttpClient(?=\\s+client)";
        var input = Encoding.UTF8.GetBytes($"{new string('x', matchIndex)}HttpClient client suffix");

        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options);

            Assert.Equal(1, regex.Count(input));
        }
    }

    [Theory]
    [InlineData("HttpClient\u00A0client")]
    [InlineData("HttpClient\u2003client")]
    [InlineData("HttpClient\u2028client")]
    public void SeparatorLiteralLookaheadCountFallsBackForUnicodeWhitespace(string input)
    {
        const string pattern = "HttpClient(?=\\s+client)";
        var bytes = Encoding.UTF8.GetBytes(input);

        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            Assert.Equal(Regex.Count(input, pattern, options), new Utf8Regex(pattern, options: options).Count(bytes));
        }
    }

    [Fact]
    public void OrderedLiteralWindowLiteralFamilyCountPreservesBehavior()
    {
        var regex = new Utf8Regex(@"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b", options: RegexOptions.CultureInvariant);
        var input = """
            public sealed class First
            private readonly struct Skip
            internal partial class Second
            public
            class Third
            """u8;
        // The separator may consume the newline in "public\nclass" before dot
        // begins; the old trailing-anchor shortcut incorrectly discarded it.
        var oracle = new Regex(regex.Pattern, RegexOptions.CultureInvariant);
        var text = Encoding.UTF8.GetString(input);
        Assert.Equal(3, oracle.Count(text));
        Assert.Equal(oracle.Count(text), regex.Count(input));
        var actual = regex.EnumerateMatches(input);
        foreach (var expected in oracle.EnumerateMatches(text))
        {
            Assert.True(actual.MoveNext());
            Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length))), (actual.Current.StartOffsetInBytes, (actual.Current.EndOffsetInBytes - actual.Current.StartOffsetInBytes)));
        }
        Assert.False(actual.MoveNext());
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void OrderedLiteralWindowLiteralFamilyIgnoresInvalidEarlierTrailingLiteral()
    {
        var regex = new Utf8Regex(@"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b", options: RegexOptions.CultureInvariant);
        var input = "publicclass noise public sealed class Valid"u8;

        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("public sealed class"), (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal("publicclass noise ".Length, match.StartOffsetInBytes);
    }

    [Fact]
    public void SeparatorOnlyIdentifierFamilyIgnoresMissingWhitespace()
    {
        var regex = new Utf8Regex(@"\b(?:public|private|internal)\s+class\b", options: RegexOptions.CultureInvariant);
        var input = "publicclass public class internal\tclass"u8;

        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("publicclass ".Length, match.StartOffsetInBytes);
    }

    [Fact]
    public void DeclarationIdentifierFamilySkipsInvalidEarlierPrefixForMatch()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", options: RegexOptions.CultureInvariant);
        var input = "class lowercase record CustomerExportJob"u8;

        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("class lowercase ".Length, match.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("record CustomerExportJob"), (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledDeclarationIdentifierFamilyMatchesExpected()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "class lowercase record CustomerExportJob struct lower"u8;

        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("class lowercase ".Length, match.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("record CustomerExportJob"), (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledLoggingInvocationFamilyMatchesExpected()
    {
        var regex = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "LogWarningx LogInformation ("u8;
        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("LogWarningx ".Length, match.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("LogInformation ("), (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledOrderedLiteralWindowMatchesExpected()
    {
        var regex = new Utf8Regex(@"\bpublic\s+async\b", options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "xpublic async public   asyncz public async"u8;
        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("xpublic async public   asyncz ".Length, match.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount("public async"), (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void CompiledBoundedOrderedLiteralWindowMatchesExpected()
    {
        const string pattern = @"\bawait\b\s+.{0,60}\bConfigureAwait\b";
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = """
            awaitx ConfigureAwait
            await ConfigureAwait
            await    foo.ConfigureAwait
            await
            foo.ConfigureAwait
            """;
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = Regex.Match(input, pattern, RegexOptions.CultureInvariant);
        Assert.Equal(Regex.Count(input, pattern, RegexOptions.CultureInvariant), regex.Count(bytes));
        var match = regex.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expected.Index)), match.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expected.Index, expected.Length)), (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void ConstructorPromotesOptionalAsciiLiteralPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("ab?c", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesFiniteAsciiLiteralAlternationLoopToLiteralFamily()
    {
        var regex = new Utf8Regex("(?:ab|cd){2}", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesInvariantIgnoreCaseOptionalAsciiLiteralPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("ab?c", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesInvariantIgnoreCaseFiniteAsciiCharClassPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("a[bc]?d", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesInvariantIgnoreCaseFiniteAsciiCharClassPatternWithoutOptionalToLiteralFamily()
    {
        var regex = new Utf8Regex("a[bc]d", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesFiniteAsciiCharClassOptionalPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("a[bc]?d", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesFiniteAsciiCharClassRepeatPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("[ab]{2}", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void DefaultTimeoutUsesInfiniteMatchTimeoutInitially()
    {
        Assert.Equal(Regex.InfiniteMatchTimeout, Utf8Regex.DefaultMatchTimeout);
    }

    [Fact]
    public void ConstructorDoesNotTreatZeroTimeoutAsDefaultSentinel()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("abc", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.Zero
        }));
    }

    [Fact]
    public void DirectPrefixFamilyCanMatchDocLineDirectly()
    {
        var regex = new Utf8Regex("\\G///[^\\n]*\\n", options: RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var match = regex.Match("/// summary line\n"u8);

        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(17, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void FallbackRegexCanMatchEnvisionQuotedStringDirectly()
    {
        var regex = new Utf8Regex("\\G\"([^\"\\\\]|\\\\.)*\"", options: RegexOptions.CultureInvariant);
        var match = regex.Match("\"hello\\\"world\""u8);

        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(14, (match.EndOffsetInBytes - match.StartOffsetInBytes));
        Assert.Equal(14, (match.EndOffsetInBytes - match.StartOffsetInBytes));
    }

    [Fact]
    public void FallbackRegexCanMatchEnvisionQuotedStringDirectlyWithUtf8Content()
    {
        var regex = new Utf8Regex("\\G\"([^\"\\\\]|\\\\.)*\"", options: RegexOptions.CultureInvariant);
        var match = regex.Match("\"été\""u8);

        Assert.True(match.Success);
        Assert.True(match.Success);
        Assert.Equal(0, match.StartOffsetInBytes);
        Assert.Equal(7, match.EndOffsetInBytes - match.StartOffsetInBytes);
    }

}
