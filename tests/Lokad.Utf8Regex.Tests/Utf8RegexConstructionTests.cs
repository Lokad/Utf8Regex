using System.Text.RegularExpressions;
using System.Text;
using System.Reflection;
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
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(3));

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
        var regex = new Utf8Regex("café", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorClassifiesUtf8LiteralAlternationAsNative()
    {
        var regex = new Utf8Regex("café|niño", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactUtf8Literals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorClassifiesBoundaryWrappedAsciiLiteralAlternationAsNative()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorClassifiesBoundaryWrappedSharedPrefixLiteralAlternationAsStructuralFamily()
    {
        var regex = new Utf8Regex(@"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.StructuralIdentifierFamilyPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.StructuralIdentifierFamilyPlan.TrailingBoundary);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoundaryWrappedSharedPrefixLiteralFamilyCountMatchesRuntime(RegexOptions mode)
    {
        const string pattern = @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b";
        const string input = "LogTrace xLogDebug LogInformationX LogWarning LogError\nLogDebug éLogTrace LogDebugé Log";
        var options = mode | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void ConstructorClassifiesAsciiStructuralIdentifierFamilyAsNative()
    {
        var regex = new Utf8Regex(@"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.None, regex.Inspection.SearchPlan.TrailingBoundary);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiStructuralFamily, regex.Inspection.StructuralLinearProgramKind);
    }

    [Fact]
    public void StructuralIdentifierFamilyMatchesAsyncSuffixCase()
    {
        var regex = new Utf8Regex(@"\b(?:Get|TryGet|Create)[A-Z][A-Za-z0-9]+Async\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("prefix TryGetCustomerAsync suffix");

        Assert.True(regex.IsMatch(input));
    }

    [Fact]
    public void StructuralIdentifierFamilyMatchesIdentifierSuffixWindowCase()
    {
        var regex = new Utf8Regex(@"\b(?:using\s+var|await\s+using\s+var)\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*await\b", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("await using var scope = await resource");

        Assert.True(regex.IsMatch(input));
    }

    [Fact]
    public void ConstructorClassifiesAsciiDeclarationIdentifierFamilyAsNative()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.None, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorCompilesVerifierProgramForDeclarationIdentifierFamily()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var steps = regex.Inspection.StructuralIdentifierFamilyPlan.VerifierProgram.Steps;
        var linearInstructions = regex.Inspection.StructuralIdentifierFamilyPlan.VerifierProgram.LinearProgram.Instructions;

        Assert.Equal(Utf8StructuralVerifierKind.AsciiStructuralProgram, regex.Inspection.StructuralVerifierPlan.Kind);
        Assert.Equal(
            [
                AsciiStructuralVerifierStepKind.ConsumeSeparator,
                AsciiStructuralVerifierStepKind.RequireIdentifierStart,
                AsciiStructuralVerifierStepKind.ConsumeIdentifierTail,
                AsciiStructuralVerifierStepKind.Accept,
            ],
            steps.Select(static step => step.Kind));
        Assert.Equal(
            [
                AsciiStructuralLinearVerifierInstructionKind.ConsumeSetLoop,
                AsciiStructuralLinearVerifierInstructionKind.RequireSetByte,
                AsciiStructuralLinearVerifierInstructionKind.ConsumeSetTail,
                AsciiStructuralLinearVerifierInstructionKind.Accept,
            ],
            linearInstructions.Select(static instruction => instruction.Kind));
    }

    [Fact]
    public void ConstructorDetectsUpperWordTailKernelForDeclarationIdentifierFamily()
    {
        var declaration = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var logging = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant);

        Assert.True(declaration.Inspection.StructuralIdentifierFamilyPlan.HasAsciiUpperWordTailKernel);
        Assert.False(logging.Inspection.StructuralIdentifierFamilyPlan.HasAsciiUpperWordTailKernel);
    }

    [Fact]
    public void ConstructorMakesDeclarationIdentifierKernelEligibleForWhitespaceSeparator()
    {
        var declaration = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);

        Assert.True(Utf8AsciiStructuralIdentifierFamilyExecutor.CanUseUpperWordIdentifierKernel(declaration.Inspection.StructuralIdentifierFamilyPlan));
    }

    [Fact]
    public void DeclarationIdentifierDirectCountMatchesExpected()
    {
        const string input = "record CustomerExportJob struct worker_state class Worker_State";
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(
            2,
            Utf8AsciiStructuralIdentifierFamilyExecutor.CountUpperWordIdentifier(
                bytes,
                regex.Inspection.StructuralIdentifierFamilyPlan,
                regex.Inspection.SearchPlan,
                budget: Utf8ExecutionDeadline.Infinite));
    }

    [Fact]
    public void ConstructorClassifiesAsciiLoggingInvocationFamilyAsNative()
    {
        var regex = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.None, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorClassifiesAsciiPrefixedFixedRunFamilyAsStructuralLinearAutomaton()
    {
        var regex = new Utf8Regex(@"((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiLiteralFamilyRun, regex.Inspection.StructuralLinearProgramKind);
    }

    [Fact]
    public void ConstructorCompilesVerifierProgramForLoggingInvocationFamily()
    {
        var regex = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant);
        var steps = regex.Inspection.StructuralIdentifierFamilyPlan.VerifierProgram.Steps;
        var linearInstructions = regex.Inspection.StructuralIdentifierFamilyPlan.VerifierProgram.LinearProgram.Instructions;

        Assert.Equal(Utf8StructuralVerifierKind.AsciiStructuralProgram, regex.Inspection.StructuralVerifierPlan.Kind);
        Assert.Equal(
            [
                AsciiStructuralVerifierStepKind.ConsumeSeparator,
                AsciiStructuralVerifierStepKind.MatchSuffixAtCurrent,
                AsciiStructuralVerifierStepKind.Accept,
            ],
            steps.Select(static step => step.Kind));
        Assert.Equal(
            [
                AsciiStructuralLinearVerifierInstructionKind.ConsumeSetLoop,
                AsciiStructuralLinearVerifierInstructionKind.MatchSuffixAtCurrent,
                AsciiStructuralLinearVerifierInstructionKind.Accept,
            ],
            linearInstructions.Select(static instruction => instruction.Kind));
    }

    [Fact]
    public void StructuralVerifierProgramMatchesDeclarationIdentifierFamily()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var program = regex.Inspection.StructuralIdentifierFamilyPlan.VerifierProgram;
        var input = System.Text.Encoding.UTF8.GetBytes("record CustomerExportJob");

        Assert.True(program.TryMatch(input, 0, "record".Length, out var matchedLength));
        Assert.Equal(input.Length, matchedLength);
    }

    [Fact]
    public void StructuralVerifierProgramMatchesLoggingInvocationFamily()
    {
        var regex = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant);
        var program = regex.Inspection.StructuralIdentifierFamilyPlan.VerifierProgram;
        var input = System.Text.Encoding.UTF8.GetBytes("LogInformation (");

        Assert.True(program.TryMatch(input, 0, "LogInformation".Length, out var matchedLength));
        Assert.Equal(input.Length, matchedLength);
    }

    [Fact]
    public void ConstructorClassifiesBoundaryWrappedUtf8LiteralAsNative()
    {
        var regex = new Utf8Regex(@"\bé\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorPreservesBoundaryWrappedAsciiLiteralSearchRequirements()
    {
        var regex = new Utf8Regex(@"\bword\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.LeadingBoundary);
        Assert.Equal(Utf8BoundaryRequirement.Boundary, regex.Inspection.SearchPlan.TrailingBoundary);
    }

    [Fact]
    public void ConstructorPreservesOneSidedBoundaryWrappedUtf8LiteralSearchRequirements()
    {
        var leading = new Utf8Regex(@"\bé", RegexOptions.CultureInvariant);
        var trailing = new Utf8Regex(@"é\B", RegexOptions.CultureInvariant);

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
        Assert.Equal(Utf8CompiledEngineKind.SearchGuidedFallback, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorKeepsCompatOnlyImportPatternOffByteSafeLinear()
    {
        const string pattern = "^import\\s+(?<shared>shared\\s+)?\\\"(?<path>(\\.|[^\\\\\\\"]*))\\\".*$";
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("unsupported_loop", regex.Inspection.FallbackReason);
        Assert.NotEqual(Utf8CompiledEngineKind.ByteSafeLinear, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorKeepsUnsupportedOptionFallbackPatternOffByteSafeLinear()
    {
        const string pattern = @"^\s*///(?<title>#.*)$";
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline | RegexOptions.NonBacktracking);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("unsupported_options", regex.Inspection.FallbackReason);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void CompiledMatchUsesDirectIdentifierValidatorPath()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("Alpha_42"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(8, match.LengthInBytes);
        Assert.Equal(8, match.LengthInUtf16);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
    }

    [Fact]
    public void CompiledIdentifierValidatorMatchesBaselineAcrossCorpus()
    {
        AssertCompiledFastPathMatchesBaseline(
            "^[a-z][a-z0-9_]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            expectHandledWithoutValidation: true,
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
        var regex = new Utf8Regex("^[a-f0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = regex.Match("A0f"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(3, match.LengthInBytes);
        Assert.Equal(3, match.LengthInUtf16);
    }

    [Fact]
    public void CompiledAnchoredFixedLengthHexValidatorMatchesWholeSpan()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("A0f"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(3, match.LengthInBytes);
        Assert.Equal(3, match.LengthInUtf16);
    }

    [Fact]
    public void CompiledFixedLengthHexValidatorMatchesBaselineAcrossCorpus()
    {
        AssertCompiledFastPathMatchesBaseline(
            "^[a-f0-9]{3}$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            expectHandledWithoutValidation: true,
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
    public void ConstructorExtractsAnchoredValidatorPlanForRgbHexValidator()
    {
        var regex = new Utf8Regex("^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.All(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.Segments.Where(static s => !s.IsLiteral), static segment =>
            Assert.Equal(AsciiCharClassPredicateKind.AsciiHexDigit, segment.PredicateKind));
    }

    [Fact]
    public void CompiledAnchoredRgbHexValidatorMatchesWholeSpan()
    {
        var regex = new Utf8Regex("^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("A0f1c2"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(6, match.LengthInBytes);
        Assert.Equal(6, match.LengthInUtf16);
    }

    [Fact]
    public void CompiledRgbHexValidatorMatchesBaselineAcrossCorpus()
    {
        AssertCompiledFastPathMatchesBaseline(
            "^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            expectHandledWithoutValidation: true,
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
    public void EmittedAnchoredValidatorMatcherHandlesFixedLengthHexPlan()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(Utf8EmittedWholeMatcher.TryCreate(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan, allowTrailingNewline: true, out var matcher));
        Assert.NotNull(matcher);
        Assert.Equal(3, matcher!.MatchWhole("A0f"u8));
        Assert.Equal(3, matcher.MatchWhole("A0f\n"u8));
        Assert.Equal(-1, matcher.MatchWhole("A0g"u8));
    }

    [Fact]
    public void EmittedAnchoredValidatorMatcherMatchesNativeExecutorAcrossSupportedPlans()
    {
        AssertEmittedAnchoredValidatorMatchesNative(
            "^[a-f0-9]{3}$",
            allowTrailingNewline: true,
            [""u8.ToArray(), "A0f"u8.ToArray(), "A0f\n"u8.ToArray(), "A0g"u8.ToArray(), "A0ff"u8.ToArray()]);

        AssertEmittedAnchoredValidatorMatchesNative(
            "^(?<red>[a-f0-9]{2})(?<green>[a-f0-9]{2})(?<blue>[a-f0-9]{2})$",
            allowTrailingNewline: true,
            [""u8.ToArray(), "A0f1c2"u8.ToArray(), "A0f1c2\n"u8.ToArray(), "A0f1cg"u8.ToArray(), "A0f1c2x"u8.ToArray()]);

        AssertEmittedAnchoredValidatorMatchesNative(
            "^[A-Z][A-Za-z0-9_]{2}$",
            allowTrailingNewline: true,
            ["Ab0"u8.ToArray(), "Ab0\n"u8.ToArray(), "ab0"u8.ToArray(), "A_0"u8.ToArray(), "Ab01"u8.ToArray()]);
    }

    [Fact]
    public void EmittedAnchoredValidatorMatcherRespectsTrailingNewlineSetting()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(Utf8EmittedWholeMatcher.TryCreate(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan, allowTrailingNewline: false, out var matcher));
        Assert.NotNull(matcher);
        Assert.Equal(3, matcher!.MatchWhole("A0f"u8));
        Assert.Equal(-1, matcher.MatchWhole("A0f\n"u8));
    }

    [Fact]
    public void CompiledAnchoredLiteralThenRunValidatorMatchesIgnoreCaseWholeSpan()
    {
        var regex = new Utf8Regex("^ab[a-f0-9]{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("AB0F"u8.ToArray());

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.True(match.Success);
        Assert.Equal(4, match.LengthInBytes);
    }

    [Fact]
    public void CompiledAnchoredValidatorCanMatchWithoutValidationForAsciiIdentifier()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("alpha_123"u8, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(9, match.LengthInBytes);
    }

    [Fact]
    public void CompiledAnchoredValidatorCanRejectWithoutValidationForAsciiIdentifierMiss()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("1alpha"u8, out var match);

        Assert.True(handled);
        Assert.False(match.Success);
    }

    [Fact]
    public void CompiledAnchoredValidatorStillNeedsValidationForNonAsciiIdentifierInput()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("alphé"u8, out _);

        Assert.False(handled);
    }

    [Fact]
    public void CompiledRepeatedDigitGroupCanAnswerIsMatchWithoutValidation()
    {
        var regex = new Utf8Regex(@"([0-9]{4}[- ]){3}[0-9]{3,4}", RegexOptions.Compiled);

        var handled = regex.Inspection.DebugTryIsMatchAsciiSimplePatternWithoutValidation("1234-5678-1234-456"u8, out var isMatch);

        Assert.True(handled);
        Assert.True(isMatch);
    }

    [Fact]
    public void EmittedRepeatedDigitGroupMatchesNativeWholeExecutorAcrossSupportedShapes()
    {
        var regex = new Utf8Regex(@"([0-9]{4}[- ]){3}[0-9]{3,4}", RegexOptions.Compiled);
        var plan = regex.Inspection.SimplePatternPlan.RepeatedDigitGroupPlan;

        Assert.True(plan.HasValue);
        Assert.True(Utf8EmittedWholeMatcher.TryCreate(plan, out var matcher));
        Assert.NotNull(matcher);
        Assert.False(Utf8EmittedWholeMatcher.TryCreate(
            new AsciiSimplePatternRepeatedDigitGroupPlan(
                repeatedGroupCount: 5,
                groupDigitCount: 4,
                trailingMinDigits: 3,
                trailingMaxDigits: 4,
                separatorBytes: [(byte)'-', (byte)' ']),
            out _));

        byte[][] inputs =
        [
            "1234-5678-1234-456"u8.ToArray(),
            "1234 5678 1234 4567"u8.ToArray(),
            "1234-5678 1234-456"u8.ToArray(),
            "1234-5678-1234-45"u8.ToArray(),
            "1234-5678-1234-45678"u8.ToArray(),
            "1234-5678_1234-456"u8.ToArray(),
            "1234-5678-123x-456"u8.ToArray(),
            "x1234-5678-1234-456"u8.ToArray(),
            [ (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'-',
                (byte)'5', (byte)'6', (byte)'7', (byte)'8', (byte)'-',
                (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'-',
                (byte)'4', (byte)'5', 0xC3, 0xA9 ],
        ];
        foreach (var input in inputs)
        {
            var nativeMatch = Utf8AsciiRepeatedDigitGroupExecutor.TryMatchWhole(
                input,
                plan,
                out var nativeLength,
                out _);
            var emittedLength = matcher!.MatchWhole(input);

            Assert.Equal(nativeMatch, emittedLength >= 0);
            Assert.Equal(nativeMatch ? nativeLength : -1, emittedLength);
        }
    }

    [Fact]
    public void CompiledPureRunAnchoredValidatorDoesNotUseEmittedMatcher()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.False(regex.Inspection.DebugUsesEmittedWholeMatcher);
    }

    [Fact]
    public void ConstructorExtractsAnchoredValidatorPlanForPostcodeValidator()
    {
        var regex = new Utf8Regex("^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredOptionalFieldPlan.HasValue);
        Assert.True(regex.Inspection.DebugUsesEmittedWholeMatcher);
        Assert.Equal(6, regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.Segments.Length);
        Assert.Contains(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.Segments, static segment => !segment.IsLiteral && segment.MinLength == 1 && segment.MaxLength == 2);
        Assert.True(
            regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.Segments.Count(static segment =>
                !segment.IsLiteral &&
                segment.MinLength == 0 &&
                segment.MaxLength == 1) >= 2);
        Assert.Contains(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.Segments, static segment => !segment.IsLiteral && segment.MinLength == 2 && segment.MaxLength == 2);
    }

    [Fact]
    public void EmittedAnchoredOptionalFieldMatchesNativeAcrossCompleteShapeProduct()
    {
        var regex = new Utf8Regex("^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", RegexOptions.Compiled);
        var plan = regex.Inspection.SimplePatternPlan.AnchoredOptionalFieldPlan;

        Assert.True(plan.HasValue);
        Assert.True(plan.CanUseShortAsciiWholeMatcher);
        Assert.False(new AsciiSimplePatternAnchoredOptionalFieldPlan(
            plan.HeadClass,
            plan.HeadMinCount,
            plan.HeadMaxCount,
            plan.FirstRequiredClass,
            plan.OptionalClass,
            optionalLiteral: (byte)'A',
            plan.SecondRequiredClass,
            plan.TailClass,
            plan.TailCount).CanUseShortAsciiWholeMatcher);
        Assert.True(Utf8EmittedWholeMatcher.TryCreate(
            plan,
            allowTrailingNewline: true,
            out var matcher));
        Assert.NotNull(matcher);
        Assert.False(Utf8EmittedWholeMatcher.TryCreate(
            new AsciiSimplePatternAnchoredOptionalFieldPlan(
                plan.HeadClass,
                plan.HeadMinCount,
                9,
                plan.FirstRequiredClass,
                plan.OptionalClass,
                plan.OptionalLiteral,
                plan.SecondRequiredClass,
                plan.TailClass,
                plan.TailCount),
            allowTrailingNewline: false,
            out _));

        byte[][] inputs =
        [
            "A01ZZ"u8.ToArray(),
            "A0 1ZZ"u8.ToArray(),
            "A0Z1ZZ"u8.ToArray(),
            "A0Z 1ZZ"u8.ToArray(),
            "AB01ZZ"u8.ToArray(),
            "AB0 1ZZ"u8.ToArray(),
            "AB0Z1ZZ"u8.ToArray(),
            "AB0Z 1ZZ"u8.ToArray(),
            "AB0Z 1ZZ\n"u8.ToArray(),
            "ABC0Z 1ZZ"u8.ToArray(),
            "AB0Z 1Z9"u8.ToArray(),
            "éB0Z 1ZZ"u8.ToArray(),
        ];
        foreach (var input in inputs)
        {
            var nativeMatch = Utf8AsciiAnchoredOptionalFieldExecutor.TryMatchWhole(
                input,
                plan,
                allowTrailingNewline: true,
                out var nativeLength,
                out _);
            var emittedLength = matcher!.MatchWhole(input);

            Assert.Equal(nativeMatch, emittedLength >= 0);
            Assert.Equal(nativeMatch ? nativeLength : -1, emittedLength);
        }
    }

    [Fact]
    public void AnchoredOptionalFieldPlanRejectsIncompleteBranchProduct()
    {
        var regex = new Utf8Regex(
            "^(?:[A-Z][0-9][0-9][A-Z]{2}|[A-Z][0-9][A-Z0-9][0-9][A-Z]{2}|[A-Z][0-9] [0-9][A-Z]{2}|[A-Z]{2}[0-9][A-Z0-9] [0-9][A-Z]{2})$",
            RegexOptions.Compiled);

        Assert.False(regex.Inspection.SimplePatternPlan.AnchoredOptionalFieldPlan.HasValue);
    }

    [Fact]
    public void CompiledEmittedLiteralThenRunValidatorMatchesBaselineAcrossCorpus()
    {
        var compiled = new Utf8Regex("^ab[0-9]{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.True(compiled.Inspection.DebugUsesEmittedWholeMatcher);

        AssertCompiledFastPathMatchesBaseline(
            "^ab[0-9]{2}$",
            RegexOptions.CultureInvariant,
            expectHandledWithoutValidation: true,
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
    public void CompiledIgnoreCaseEmittedLiteralThenRunValidatorMatchesBaselineAcrossCorpus()
    {
        var compiled = new Utf8Regex("^ab[a-f0-9]{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        Assert.True(compiled.Inspection.DebugUsesEmittedWholeMatcher);

        AssertCompiledFastPathMatchesBaseline(
            "^ab[a-f0-9]{2}$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase,
            expectHandledWithoutValidation: true,
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
        var regex = new Utf8Regex("[a-zA-Z][a-zA-Z0-9]*", RegexOptions.Compiled);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("123alpha9"u8, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(3, match.IndexInBytes);
        Assert.Equal(6, match.LengthInBytes);
    }

    [Fact]
    public void CompiledHelperIdentifierCanRejectWithoutValidation()
    {
        var regex = new Utf8Regex("[a-zA-Z][a-zA-Z0-9]*", RegexOptions.Compiled);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("123__"u8, out var match);

        Assert.True(handled);
        Assert.False(match.Success);
    }

    [Fact]
    public void CompiledHelperIdentifierNeedsValidationForNonAsciiBeforeAsciiMatch()
    {
        var regex = new Utf8Regex("[a-zA-Z][a-zA-Z0-9]*", RegexOptions.Compiled);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("éalpha"u8, out _);

        Assert.False(handled);
    }

    [Fact]
    public void CompiledCreditCardMatcherMatchesBaselineAcrossCorpus()
    {
        AssertCompiledFastPathMatchesBaseline(
            "([0-9]{4}[- ]){3}[0-9]{3,4}",
            RegexOptions.None,
            expectHandledWithoutValidation: true,
            [
                [],
                "1234-5678-1234-456"u8.ToArray(),
                "1234 5678 1234 4567"u8.ToArray(),
                "x1234-5678-1234-456y"u8.ToArray(),
                "1234-5678-1234"u8.ToArray(),
                "1234-5678-1234-45x"u8.ToArray(),
                [ (byte)'1', (byte)'2', (byte)'3', (byte)'4', (byte)'-', 0xD9, 0xA1, (byte)'2' ],
            ]);
    }

    [Fact]
    public void CompiledPostcodeValidatorMatchesBaselineAcrossCorpus()
    {
        AssertCompiledFastPathMatchesBaseline(
            "^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$",
            RegexOptions.None,
            expectHandledWithoutValidation: true,
            [
                "SW1 1ZZ"u8.ToArray(),
                "W1A 0AX"u8.ToArray(),
                "M11AE"u8.ToArray(),
                "B338TH"u8.ToArray(),
                "CR2 6XH"u8.ToArray(),
                "DN55 1PT"u8.ToArray(),
                "A1 9A"u8.ToArray(),
                "SW11ZZ"u8.ToArray(),
                "SW1 1Z1"u8.ToArray(),
                "SW1  1ZZ"u8.ToArray(),
                "SW1é1ZZ"u8.ToArray(),
            ]);
    }

    [Fact]
    public void ConstructorExtractsAnchoredBoundedDatePlanForDateValidator()
    {
        var regex = new Utf8Regex("^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$", RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue);
    }

    [Fact]
    public void ConstructorExtractsUnicodeDigitDatePlanWithAsciiInputGuard()
    {
        var regex = new Utf8Regex(@"^\d{1,2}/\d{1,2}/\d{4}$", RegexOptions.Compiled);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.False(regex.Inspection.SimplePatternPlan.IsUtf8ByteSafe);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue);
    }

    [Fact]
    public void EmittedAnchoredBoundedDateMatchesNativeAcrossFieldWidths()
    {
        var regex = new Utf8Regex(@"^[0-9]{1,2}/[0-9]{1,2}/[0-9]{2,4}$", RegexOptions.Compiled);
        var plan = regex.Inspection.SimplePatternPlan.AnchoredBoundedDatePlan;

        Assert.True(plan.HasValue);
        Assert.True(regex.Inspection.DebugUsesEmittedWholeMatcher);
        Assert.True(Utf8EmittedWholeMatcher.TryCreate(
            plan,
            allowTrailingNewline: true,
            out var matcher));
        Assert.NotNull(matcher);
        Assert.False(Utf8EmittedWholeMatcher.TryCreate(
            new AsciiSimplePatternAnchoredBoundedDatePlan(1, 8, 1, 8, 1, 8, (byte)'/', (byte)'/'),
            allowTrailingNewline: false,
            out _));

        byte[][] inputs =
        [
            "1/2/01"u8.ToArray(),
            "1/12/012"u8.ToArray(),
            "12/1/0123"u8.ToArray(),
            "12/12/0123"u8.ToArray(),
            "12/12/0123\n"u8.ToArray(),
            "1/2/0"u8.ToArray(),
            "123/1/0123"u8.ToArray(),
            "12-12-0123"u8.ToArray(),
            "12/xx/0123"u8.ToArray(),
        ];
        foreach (var input in inputs)
        {
            var emittedLength = matcher!.MatchWhole(input);
            var nativeMatch = Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
                input,
                plan,
                allowTrailingNewline: true,
                out var nativeLength,
                out var needsValidation);

            Assert.False(needsValidation);
            Assert.Equal(nativeMatch ? nativeLength : -1, emittedLength);
        }

        Assert.Equal(-1, matcher!.MatchWhole("١/٢/٢٠٠١"u8));
    }

    [Fact]
    public void EmittedBoundedDateTokenMatcherMatchesCompiledDateRuntime()
    {
        var regex = new Utf8Regex(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", RegexOptions.Compiled);

        Assert.True(Utf8EmittedTokenFamilyMatcher.TryCreate(regex.Inspection.PreparedRegex.FallbackDirectFamily, out var matcher));
        Assert.NotNull(matcher);

        Assert.True(matcher!.TryFindNext("Today is 11/18/2019 and tomorrow is 11/19/2019."u8, 0, out var matchIndex, out var matchedLength));
        Assert.Equal("Today is ".Length, matchIndex);
        Assert.Equal("11/18/2019".Length, matchedLength);
        Assert.Equal(2, matcher.Count("Today is 11/18/2019 and tomorrow is 11/19/2019."u8));
        Assert.False(matcher.TryFindNext("Today is xx/yy/zzzz and tomorrow is ??/??/????."u8, 0, out _, out _));
    }

    [Fact]
    public void EmittedUriTokenMatcherMatchesCompiledUriRuntime()
    {
        var regex = new Utf8Regex(@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", RegexOptions.Compiled);

        Assert.True(Utf8EmittedTokenFamilyMatcher.TryCreate(regex.Inspection.PreparedRegex.FallbackDirectFamily, out var matcher));
        Assert.NotNull(matcher);

        Assert.True(matcher!.TryFindNext("https://atlas.example.org/reports/export?id=42"u8, 0, out var matchIndex, out var matchedLength));
        Assert.Equal(0, matchIndex);
        Assert.Equal("https://atlas.example.org/reports/export?id=42".Length, matchedLength);
        Assert.Equal(2, matcher.Count("https://a.example/x ftp://b.example/y"u8));
        Assert.False(matcher.TryFindNext("https://a https://b"u8, 0, out _, out _));
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
        var regex = new Utf8Regex("^#?([a-f0-9]{6}|[a-f0-9]{3})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        var match = regex.Match(Encoding.ASCII.GetBytes(value));

        Assert.Equal(expected, match.Success);
        if (expected)
        {
            Assert.Equal(value.Length, match.LengthInBytes);
        }
    }

    [Fact]
    public void EmittedAnchoredValidatorMatcherHandlesExactCaseLiteralThenRunPlan()
    {
        var regex = new Utf8Regex("^ab[0-9]{2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.True(Utf8EmittedWholeMatcher.TryCreate(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan, allowTrailingNewline: true, out var matcher));
        Assert.NotNull(matcher);
        Assert.Equal(4, matcher!.MatchWhole("ab01"u8));
        Assert.Equal(4, matcher.MatchWhole("ab01\n"u8));
        Assert.Equal(-1, matcher.MatchWhole("AB01"u8));
    }

    [Fact]
    public void EmittedAnchoredValidatorMatcherStillRejectsIgnoreCaseLiteralThenRunPlan()
    {
        var regex = new Utf8Regex("^ab[a-f0-9]{2}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.IgnoreCase);
        Assert.True(Utf8EmittedWholeMatcher.TryCreate(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan, allowTrailingNewline: true, out var matcher));
        Assert.NotNull(matcher);
        Assert.Equal(4, matcher!.MatchWhole("ab0f"u8));
        Assert.Equal(4, matcher.MatchWhole("AB0F"u8));
        Assert.Equal(4, matcher.MatchWhole("Ab0F\n"u8));
        Assert.Equal(-1, matcher.MatchWhole("ax0f"u8));
    }

    [Fact]
    public void EmittedAnchoredValidatorMatcherRejectsVariableTailPlan()
    {
        var regex = new Utf8Regex("^[a-z][a-z0-9_]*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.False(Utf8EmittedWholeMatcher.TryCreate(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan, allowTrailingNewline: true, out _));
    }

    [Fact]
    public void AnchoredValidatorRejectsNonAsciiInputWithoutFallbackMatch()
    {
        var regex = new Utf8Regex("^[a-f0-9]{3}$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = regex.Match("éab"u8.ToArray());

        Assert.False(match.Success);
        Assert.Equal(0, regex.Count("éab"u8.ToArray()));
    }

    [Fact]
    public void CompiledAsciiLiteralFamilyCountStillValidatesInvalidUtf8()
    {
        var regex = new Utf8Regex("tempus|magna|semper", RegexOptions.Compiled);
        byte[] invalid = [0xFF, (byte)'t', (byte)'e', (byte)'m', (byte)'p', (byte)'u', (byte)'s'];

        Assert.Throws<ArgumentException>(() => regex.Count(invalid));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void ExactAsciiLiteralCountMatchesDotNetThroughPrioritizedRoute(RegexOptions options)
    {
        const string pattern = "tempus";
        const string input = "tempus é tempus xtempus";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        Assert.Throws<ArgumentException>(() => regex.Count([(byte)'t', (byte)'e', 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void FiveByteExactAsciiLiteralCountMatchesDotNetOnMixedUtf8Input(RegexOptions options)
    {
        const string pattern = "Twain";
        const string input = "Twain é Twain 夏 MarkTwain";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        Assert.Throws<ArgumentException>(() => regex.Count([.. "Twain"u8, 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void MediumExactAsciiLiteralCountMatchesDotNetOnMixedUtf8Input(RegexOptions options)
    {
        const string pattern = "Sherlock Holmes";
        const string input = "é Sherlock Holmes 夏 Sherlock Holmes Sherlock Watson";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        Assert.Throws<ArgumentException>(() => regex.Count([.. Encoding.UTF8.GetBytes(pattern), 0xFF]));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void EscapedMediumAsciiLiteralCountUsesItsAsciiSearchPlan(RegexOptions options)
    {
        const string literal = "ConfigureAwait(";
        var pattern = Regex.Escape(literal);
        const string input = "é ConfigureAwait( 夏 ConfigureAwait(false";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literal, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        Assert.Throws<ArgumentException>(() => regex.Count([.. Encoding.UTF8.GetBytes(literal), 0xFF]));
    }

    [Theory]
    [InlineData("foo(?=bar)", "fooqux foobar foo")]
    [InlineData("^tempus", "tempus x tempus")]
    [InlineData("tempus$", "tempus x tempus")]
    [InlineData(@"\btempus\b", "xtempus tempus tempusx")]
    public void ConstrainedExactAsciiLiteralCountRetainsSemanticRoute(string pattern, string input)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CompiledMatchUsesDirectDocLinePath()
    {
        var regex = new Utf8Regex("\\G///[^\\n]*\\n", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var match = regex.Match("/// doc line\n"u8.ToArray());

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(13, match.LengthInBytes);
        Assert.Equal(13, match.LengthInUtf16);
    }

    [Fact]
    public void CompiledDocLineDirectHookHandlesMatchWithoutValidation()
    {
        var regex = new Utf8Regex("\\G///[^\\n]*\\n", RegexOptions.Multiline | RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var handled = regex.Inspection.DebugTryMatchWithoutValidation("/// doc line\n"u8, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(13, match.LengthInBytes);
    }

    [Fact]
    public void ConstructorClassifiesAwsKeyWindowPatternAsStructuralLinearAutomaton()
    {
        const string pattern = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.Multiline);

        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiQuotedRelation, regex.Inspection.StructuralLinearProgramKind);
        Assert.Equal(
            [
                Utf8StructuralLinearInstructionKind.QuotedRelation,
                Utf8StructuralLinearInstructionKind.Accept,
            ],
            analysis.StructuralLinearProgram.InstructionProgram.Instructions.Select(static instruction => instruction.Kind));
    }

    [Fact]
    public void AnalysisClassifiesRepeatedCapitalizedWordsLoopAsStructuralLinear()
    {
        var analysis = Utf8FrontEnd.Compile("(?:[A-Z][a-z]+\\s*){10,100}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralRepeatedSegment, analysis.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, Utf8CompiledEngineSelector.Select(analysis).Kind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiRepeatedSegment, analysis.StructuralLinearProgram.Kind);
    }

    [Fact]
    public void ConstructorDoesNotTreatUnicodeCategoryRepeatAsAsciiSimplePattern()
    {
        var regex = new Utf8Regex(@"\p{L}{8,13}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("unsupported_loop", regex.Inspection.FallbackReason);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorAcceptsUnicodeSymbolCategory()
    {
        var regex = new Utf8Regex(@"\p{Sm}", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount, regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void ConstructorClassifiesAsciiUntilByteStarCount()
    {
        var regex = new Utf8Regex(@"[^\n]*", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount, regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
    }

    [Theory]
    [InlineData(@"\w{10,}")]
    [InlineData(@"\b\w{10,}\b")]
    public void ConstructorClassifiesAsciiWordBoundedCount(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount, regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void UnicodeSymbolCategoryMatchesLikeDotNet()
    {
        const string pattern = @"\p{Sm}+";
        const string input = "sum=∑ x + y ≠ z and text";
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), regex.Count(bytes));
        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledUnicodeMathSymbolCountMatchesDotNet()
    {
        const string pattern = @"\p{Sm}";
        const string input = "sum=∑ x + y ≠ z and text ∫ 𐄂 < > | ~";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void ConstructorClassifiesBoundedAsciiCharClassRunAsStructuralLinearAutomaton()
    {
        var regex = new Utf8Regex(@"[A-Za-z]{8,13}", RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Compile(@"[A-Za-z]{8,13}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiCharClassRun, regex.Inspection.StructuralLinearProgramKind);
        Assert.Equal(
            [Utf8StructuralLinearInstructionKind.RunCharClass, Utf8StructuralLinearInstructionKind.Accept],
            analysis.StructuralLinearProgram.InstructionProgram.Instructions.Select(static instruction => instruction.Kind));
    }

    [Fact]
    public void AnalysisCompilesDeterministicProgramForFixedTokenStructuralLinearPattern()
    {
        var analysis = Utf8FrontEnd.Compile(@"ab[0-9]d", RegexOptions.CultureInvariant);
        var deterministicProgram = analysis.StructuralLinearProgram.DeterministicProgram;

        Assert.True(deterministicProgram.HasValue);
        Assert.Equal(4, deterministicProgram.FixedWidthLength);
        Assert.Equal(
            [
                Utf8AsciiDeterministicStepKind.Literal,
                Utf8AsciiDeterministicStepKind.Literal,
                Utf8AsciiDeterministicStepKind.CharClass,
                Utf8AsciiDeterministicStepKind.Literal,
                Utf8AsciiDeterministicStepKind.Accept,
            ],
            deterministicProgram.Steps.Select(static step => step.Kind));
        Assert.Equal(
            [
                Utf8AsciiDeterministicFixedWidthCheckKind.Literal,
                Utf8AsciiDeterministicFixedWidthCheckKind.Literal,
                Utf8AsciiDeterministicFixedWidthCheckKind.CharClass,
                Utf8AsciiDeterministicFixedWidthCheckKind.Literal,
            ],
            deterministicProgram.FixedWidthChecks.Select(static check => check.Kind));
    }

    [Fact]
    public void AnalysisCompilesDeterministicProgramForAsciiRunStructuralLinearPattern()
    {
        var analysis = Utf8FrontEnd.Compile(@"[A-Za-z]{8,13}", RegexOptions.CultureInvariant);
        var deterministicProgram = analysis.StructuralLinearProgram.DeterministicProgram;

        Assert.True(deterministicProgram.HasValue);
        Assert.Equal(
            [
                Utf8AsciiDeterministicStepKind.RunCharClass,
                Utf8AsciiDeterministicStepKind.Accept,
            ],
            deterministicProgram.Steps.Select(static step => step.Kind));
    }

    [Fact]
    public void AnalysisCanCompileEmitMatcherForDeterministicFixedTokenPattern()
    {
        var analysis = Utf8FrontEnd.Compile(@"ab[0-9]d", RegexOptions.CultureInvariant);

        Assert.True(Utf8EmittedDeterministicMatcher.CanCreate(analysis.StructuralLinearProgram));
    }

    [Fact]
    public void AnalysisCanCompileEmitMatcherForAnyByteFixedTokenPattern()
    {
        var analysis = Utf8FrontEnd.Compile(@"ab.[0-9]d", RegexOptions.CultureInvariant);

        Assert.True(Utf8EmittedDeterministicMatcher.CanCreate(analysis.StructuralLinearProgram));
    }

    [Fact]
    public void DeterministicFixedTokenPatternPreservesBehaviorWithEmitBackedRuntime()
    {
        var regex = new Utf8Regex(@"ab[0-9]d", RegexOptions.CultureInvariant);
        var input = "xx ab3d yy ab7d zz"u8;

        Assert.True(regex.IsMatch(input));
        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(3, match.IndexInBytes);
        Assert.Equal(4, match.LengthInBytes);
    }

    [Fact]
    public void AnyByteDeterministicFixedTokenPatternPreservesBehaviorWithEmitBackedRuntime()
    {
        var regex = new Utf8Regex(@"ab.[0-9]d", RegexOptions.CultureInvariant);
        var input = "xx ab_3d yy ab!7d zz"u8;

        Assert.True(regex.IsMatch(input));
        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(3, match.IndexInBytes);
        Assert.Equal(5, match.LengthInBytes);
    }

    [Fact]
    public void ConstructorClassifiesAsciiStructuralTokenWindowAsStructuralLinearAutomaton()
    {
        var regex = new Utf8Regex(@"[A-Za-z]{10}\s+[\s\S]{0,100}Result[\s\S]{0,100}\s+[A-Za-z]{10}", RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Compile(@"[A-Za-z]{10}\s+[\s\S]{0,100}Result[\s\S]{0,100}\s+[A-Za-z]{10}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralTokenWindow, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiTokenWindow, regex.Inspection.StructuralLinearProgramKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(
            [
                Utf8StructuralLinearInstructionKind.TokenWindow,
                Utf8StructuralLinearInstructionKind.Accept,
            ],
            analysis.StructuralLinearProgram.InstructionProgram.Instructions.Select(static instruction => instruction.Kind));
    }

    [Fact]
    public void ConstructorClassifiesAsciiStructuralRepeatedSegmentAsStructuralLinearAutomaton()
    {
        var regex = new Utf8Regex(@"(?:[A-Z][a-z]+\s*){10,100}", RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Compile(@"(?:[A-Z][a-z]+\s*){10,100}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralRepeatedSegment, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiRepeatedSegment, regex.Inspection.StructuralLinearProgramKind);
        Assert.Equal(Utf8SearchKind.None, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(
            [
                Utf8StructuralLinearInstructionKind.RepeatedSegment,
                Utf8StructuralLinearInstructionKind.Accept,
            ],
            analysis.StructuralLinearProgram.InstructionProgram.Instructions.Select(static instruction => instruction.Kind));
    }

    [Fact]
    public void ConstructorClassifiesIgnoreCaseAsFallbackInFirstVersion()
    {
        var regex = new Utf8Regex("abc", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCase, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorClassifiesNonInvariantIgnoreCaseAsFallback()
    {
        var regex = new Utf8Regex("abc", RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
    }

    [Fact]
    public void ConstructorClearsUnsupportedLoopReasonForDirectCellReferenceFamily()
    {
        var regex = new Utf8Regex("^(?<col>[a-z])(?<row>(\\d)+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Null(regex.Inspection.FallbackReason);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole, regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void ConstructorCreatesAsciiTwinForDirectCellReferenceFamily()
    {
        var regex = new Utf8Regex("^(?<col>[a-z])(?<row>(\\d)+)$", RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
        Assert.True(regex.Inspection.DebugHasAsciiCultureInvariantTwin);
    }

    [Fact]
    public void ConstructorCreatesAsciiTwinForDashboardUrlIgnoreCasePattern()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/d/(?<topicId>\\d+)/?\\?t=(?<tab>[^ ?]+)(?<rest>.*)$";
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
        Assert.True(regex.Inspection.DebugHasAsciiCultureInvariantTwin);
    }

    [Fact]
    public void ConstructorCreatesAsciiTwinForDownloadUrlIgnoreCasePattern()
    {
        const string pattern = "^(?<before>.*)(https://go(\\.testing)?\\.lokad.com|~)(?<trigram>/[a-zA-Z0-9]+)?/gateway/BigFiles/Browse/Download\\?hash=(?<hash>[a-fA-F0-9]*)(?<optPath>[?&]path=[^& \\n]+)?[?&]name=(?<name>[^& ]+)(?<optPath2>[?&]path=[^& \\n]+)?(?<rest>.*)$";
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal("culture_sensitive_ignore_case", regex.Inspection.FallbackReason);
        Assert.True(regex.Inspection.DebugHasAsciiCultureInvariantTwin);
    }

    [Fact]
    public void ConstructorClearsUnsupportedLoopReasonForDirectImportCountFamily()
    {
        var regex = new Utf8Regex("^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$", RegexOptions.Multiline);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Null(regex.Inspection.FallbackReason);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount, regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void ConstructorKeepsCompiledFallbackForDirectImportCountFamily()
    {
        var regex = new Utf8Regex("^import\\s+(?<shared>shared\\s+)?\"(?<path>(\\.|[^\\\\\"]*))\".*$", RegexOptions.Multiline | RegexOptions.Compiled);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.Null(regex.Inspection.FallbackReason);
        Assert.Equal(Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount, regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
    }

    [Fact]
    public void ConstructorExposesFallbackReasonForDeferredOptions()
    {
        var regex = new Utf8Regex("abc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Null(regex.Inspection.FallbackReason);
    }

    [Fact]
    public void ConstructorAcceptsCompiledOptionAsNonSemanticHint()
    {
        var regex = new Utf8Regex("abc", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.Equal(RegexOptions.Compiled | RegexOptions.CultureInvariant, regex.Options);
        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.ExactLiteral, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorCanSelectCompiledFallbackForLoopDrivenFallbackPattern()
    {
        var baseline = new Utf8Regex("a.*b", RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex("a.*b", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, compiled.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.SearchGuidedFallback, baseline.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.CompiledFallback, compiled.Inspection.CompiledEngineKind);
        Assert.Equal(baseline.IsMatch("zz a123b xx"u8), compiled.IsMatch("zz a123b xx"u8));
        Assert.Equal(baseline.Count("a1b a22b"u8), compiled.Count("a1b a22b"u8));
    }

    [Fact]
    public void ConstructorKeepsDirectFallbackRoutingForConditionalFallbackPattern()
    {
        const string pattern = "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))";
        var baseline = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = "BEGIN x BEGIN y END z END BEGIN END"u8;

        Assert.Equal(NativeExecutionKind.FallbackRegex, compiled.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, baseline.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, compiled.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount, baseline.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
        Assert.Equal(Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount, compiled.Inspection.PreparedRegex.FallbackDirectFamily.Kind);
        Assert.True(baseline.Inspection.DebugSupportsThrowIfInvalidOnlyCount);
        Assert.True(compiled.Inspection.DebugSupportsThrowIfInvalidOnlyCount);
        Assert.Equal(baseline.IsMatch(input), compiled.IsMatch(input));
        Assert.Equal(baseline.Count(input), compiled.Count(input));
    }

    private static void AssertEmittedAnchoredValidatorMatchesNative(string pattern, bool allowTrailingNewline, byte[][] inputs)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue, pattern);
        Assert.True(Utf8EmittedWholeMatcher.TryCreate(regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan, allowTrailingNewline, out var matcher), pattern);
        Assert.NotNull(matcher);

        foreach (var input in inputs)
        {
            var emittedLength = matcher!.MatchWhole(input);
            var nativeMatch = Utf8AsciiAnchoredValidatorExecutor.TryMatchWhole(
                input,
                regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan,
                allowTrailingNewline,
                out var nativeLength);

            Assert.Equal(nativeMatch ? nativeLength : -1, emittedLength);
        }
    }

    private static void AssertCompiledFastPathMatchesBaseline(
        string pattern,
        RegexOptions options,
        bool expectHandledWithoutValidation,
        byte[][] inputs)
    {
        var baseline = new Utf8Regex(pattern, options);
        var compiled = new Utf8Regex(pattern, options | RegexOptions.Compiled);

        foreach (var input in inputs)
        {
            var baselineMatch = baseline.Match(input);
            var compiledMatch = compiled.Match(input);

            Assert.Equal(baselineMatch.Success, compiledMatch.Success);
            Assert.Equal(baselineMatch.IsByteAligned, compiledMatch.IsByteAligned);
            Assert.Equal(baselineMatch.IndexInUtf16, compiledMatch.IndexInUtf16);
            Assert.Equal(baselineMatch.LengthInUtf16, compiledMatch.LengthInUtf16);
            if (baselineMatch.TryGetByteRange(out var baselineIndexInBytes, out var baselineLengthInBytes) &&
                compiledMatch.TryGetByteRange(out var compiledIndexInBytes, out var compiledLengthInBytes))
            {
                Assert.Equal(baselineIndexInBytes, compiledIndexInBytes);
                Assert.Equal(baselineLengthInBytes, compiledLengthInBytes);
            }
            Assert.Equal(baseline.IsMatch(input), compiled.IsMatch(input));

            var handled = compiled.Inspection.DebugTryMatchWithoutValidation(input, out var directMatch);
            if (!Utf8InputAnalyzer.IsAscii(input) && !handled)
            {
                continue;
            }

            Assert.Equal(expectHandledWithoutValidation, handled);
            if (handled)
            {
                Assert.Equal(compiledMatch.Success, directMatch.Success);
                Assert.Equal(compiledMatch.IsByteAligned, directMatch.IsByteAligned);
                Assert.Equal(compiledMatch.IndexInUtf16, directMatch.IndexInUtf16);
                Assert.Equal(compiledMatch.LengthInUtf16, directMatch.LengthInUtf16);
                if (compiledMatch.TryGetByteRange(out var compiledDirectIndexInBytes, out var compiledDirectLengthInBytes) &&
                    directMatch.TryGetByteRange(out var directIndexInBytes, out var directLengthInBytes))
                {
                    Assert.Equal(compiledDirectIndexInBytes, directIndexInBytes);
                    Assert.Equal(compiledDirectLengthInBytes, directLengthInBytes);
                }
            }
        }
    }

    [Fact]
    public void ConstructorPreservesCompiledOptionOnFallbackVerifierRegex()
    {
        const string pattern = "BEGIN(?:(?<open>BEGIN)|(?<-open>END)|(?:(?!BEGIN|END)[\\s\\S]))*END(?(open)(?!))";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);

        var programField = typeof(Utf8Regex).GetField("_program", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(programField);
        var program = programField!.GetValue(compiled);
        Assert.NotNull(program);
        var verifierRuntimeProperty = program!.GetType().GetProperty("VerifierRuntime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(verifierRuntimeProperty);
        var verifierRuntime = verifierRuntimeProperty!.GetValue(program);
        Assert.NotNull(verifierRuntime);

        var fallbackVerifierProperty = verifierRuntime!.GetType().GetProperty("FallbackCandidateVerifier", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(fallbackVerifierProperty);

        var fallbackVerifier = fallbackVerifierProperty!.GetValue(verifierRuntime);
        Assert.NotNull(fallbackVerifier);

        var fallbackRegexProperty = fallbackVerifier!.GetType().GetProperty("FallbackRegex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(fallbackRegexProperty);

        var fallbackRegex = Assert.IsType<Regex>(fallbackRegexProperty!.GetValue(fallbackVerifier));
        Assert.True((fallbackRegex.Options & RegexOptions.Compiled) != 0);
    }

    [Fact]
    public void CompiledFallbackCanCountUnicodeLetterBoundedRepeatDirectly()
    {
        const string pattern = "\\p{L}{8,13}";
        const string input = "контексты словообразование abcdefghijklmn data123 короткий";
        var baseline = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(NativeExecutionKind.FallbackRegex, compiled.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, compiled.Inspection.CompiledEngineKind);
        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
        Assert.Equal(baseline.Count(bytes), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledFallbackUnicodeLetterBoundedRepeatFallsBackForSupplementaryScalars()
    {
        const string pattern = "\\p{L}{8,13}";
        const string input = "𐐀𐐁𐐂𐐃𐐄𐐅𐐆𐐇 tail";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledFallbackCanCountGenericUnicodeLetterBoundedRepeatDirectly()
    {
        const string pattern = "\\p{L}{3,5}";
        const string input = "контексты alpha beta 世界世界世界 log42";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledHintPrefersDirectFallbackRegexForWeakEmailPrefilterLoop()
    {
        const string pattern = "[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);

        Assert.Equal(NativeExecutionKind.FallbackRegex, compiled.Inspection.ExecutionKind);
        Assert.Equal("Utf8FallbackRegexCompiledEngineRuntime", compiled.Inspection.DebugCompiledEngineRuntimeType);
    }

    [Fact]
    public void CompiledHintPrefersDirectFallbackRegexForWeakUriPrefilterLoop()
    {
        const string pattern = "[\\w]+://[^/\\s?#]+[^\\s?#]+(?:\\?[^\\s#]*)?(?:#[^\\s]*)?";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);

        Assert.Equal(NativeExecutionKind.FallbackRegex, compiled.Inspection.ExecutionKind);
        Assert.Equal("Utf8FallbackRegexCompiledEngineRuntime", compiled.Inspection.DebugCompiledEngineRuntimeType);
    }

    [Fact]
    public void CompiledWeakEmailPrefilterLoopCountMatchesDotNet()
    {
        const string pattern = "[\\w\\.+-]+@[\\w\\.-]+\\.[\\w\\.-]+";
        const string input = "ops.dispatch@northwind-control.net and admin@example.org";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledWeakUriPrefilterLoopCountMatchesDotNet()
    {
        const string pattern = "[\\w]+://[^/\\s?#]+[^\\s?#]+(?:\\?[^\\s#]*)?(?:#[^\\s]*)?";
        const string input = "https://atlas.example.org/reports/export?id=42 and ftp://mirror.example.net/pub";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledBoundedSuffixLiteralCountMatchesDotNet()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " sing  bringing  abcdefghijklming  abcdefghijklmning  ring ";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void BaselineBoundedSuffixLiteralCountMatchesDotNet()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " sing  bringing  abcdefghijklming  abcdefghijklmning  ring ";
        var baseline = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Fact]
    public void BaselineBoundedSuffixLiteralPlanRecognizesSameBoundaryClass()
    {
        var baseline = new Utf8Regex("\\s[a-zA-Z]{0,12}ing\\s", RegexOptions.None);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, baseline.Inspection.ExecutionKind);
        Assert.False(baseline.Inspection.PreparedRegex.SimplePatternPlan.IsUtf8ByteSafe);
        Assert.True(baseline.Inspection.PreparedRegex.SimplePatternPlan.BoundedSuffixLiteralPlan.HasValue);
    }

    [Fact]
    public void BaselineSymmetricLiteralWindowPlanRecognizesHolmesWatsonWindow()
    {
        var baseline = new Utf8Regex("Holmes.{0,25}Watson|Watson.{0,25}Holmes", RegexOptions.None);
        var plan = baseline.Inspection.PreparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan;

        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, baseline.Inspection.ExecutionKind);
        Assert.True(plan.HasValue);
        Assert.True(plan.HasPairedTrailingLiterals);
        Assert.Equal(25, plan.MaxGap);
        Assert.Equal("Holmes"u8.ToArray(), plan.LeadingLiteralsUtf8![0]);
        Assert.Equal("Watson"u8.ToArray(), plan.TrailingLiteralsUtf8[0]);
        Assert.Equal("Watson"u8.ToArray(), plan.LeadingLiteralsUtf8[1]);
        Assert.Equal("Holmes"u8.ToArray(), plan.TrailingLiteralsUtf8[1]);
    }

    [Fact]
    public void BaselineSymmetricLiteralWindowPlanRecognizesRiverWindow()
    {
        var baseline = new Utf8Regex("Tom.{10,25}river|river.{10,25}Tom", RegexOptions.None);
        var plan = baseline.Inspection.PreparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan;

        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, baseline.Inspection.ExecutionKind);
        Assert.True(plan.HasValue);
        Assert.True(plan.HasPairedTrailingLiterals);
        Assert.Equal(25, plan.MaxGap);
        Assert.Equal("Tom"u8.ToArray(), plan.LeadingLiteralsUtf8![0]);
        Assert.Equal("river"u8.ToArray(), plan.TrailingLiteralsUtf8[0]);
        Assert.Equal("river"u8.ToArray(), plan.LeadingLiteralsUtf8[1]);
        Assert.Equal("Tom"u8.ToArray(), plan.TrailingLiteralsUtf8[1]);
    }

    [Fact]
    public void BaselineBoundedSuffixLiteralCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " café sing résumé bringing naïve ring 東京 abcdefghijklmning ";
        var baseline = new Utf8Regex(pattern, RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void BoundedSuffixLiteralCountUsesNativeRouteWithUnicodeWhitespace(RegexOptions options)
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = "\u00A0sing\u2003 café \u0085bringing\u2028 naïve ring 東京";
        var regex = new Utf8Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(input);
        var session = Utf8SearchDiagnosticsSession.Start();

        try
        {
            Assert.Equal(Regex.Count(input, pattern, options), regex.Count(bytes));
            Assert.Equal(Utf8ExecutionRoute.NativeAsciiBoundedSuffixLiteral, session.ExecutionRoute);
        }
        finally
        {
            session.Complete();
        }
    }

    [Fact]
    public void IgnoreCaseBoundedSuffixLiteralCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = "\u00A0SING\u2003 café \u0085BrInGiNg\u2028";
        const RegexOptions options = RegexOptions.IgnoreCase;
        var regex = new Utf8Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern, options), regex.Count(bytes));
    }

    [Fact]
    public void BaselineBoundedSuffixLiteralMatchesWhenPrefixWhitespaceFollowsLetter()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = "word sing next";
        var baseline = new Utf8Regex(pattern, RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Fact]
    public void CompiledBoundedSuffixLiteralMatchRejectsOverlongRun()
    {
        const string pattern = "\\s[a-zA-Z]{0,12}ing\\s";
        const string input = " abcdefghijklmning ";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
            var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
            var bytes = Encoding.UTF8.GetBytes(input);
            Assert.Equal(Regex.IsMatch(input, pattern), compiled.IsMatch(bytes));
        }
    }

    [Fact]
    public void BaselineFallbackCanCountGenericUnicodeLetterBoundedRepeatDirectly()
    {
        const string pattern = "\\p{L}{3,5}";
        const string input = "контексты alpha beta 世界世界世界 log42";
        var baseline = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), baseline.Count(bytes));
    }

    [Fact]
    public void CompiledAsciiIpv4TokenCountMatchesDotNet()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string input = "bad 1.2.3.4 then 25.26.27.28 and 999.0.0.1 ok 012.200.033.199 and 255.10.0.2 end";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledAsciiIpv4TokenMatchMatchesDotNet()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string input = "prefix 012.200.033.199 suffix";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        var match = compiled.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(Regex.Match(input, pattern).Value, Encoding.UTF8.GetString(bytes.AsSpan(match.IndexInBytes, match.LengthInBytes)));
    }

    [Fact]
    public void CompiledAsciiIpv4TokenMissMatchesDotNet()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string input = "999.200.033.199";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.IsMatch(input, pattern), compiled.IsMatch(bytes));
    }

    [Fact]
    public void AsciiIpv4DotAnchoringMatchesDotNetAcrossDenseCandidateText()
    {
        const string pattern = @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])";
        const string alphabet = "0123456789... abcxyz";
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var ordinary = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
                Assert.Equal(expectedMatch.Index, ordinaryMatch.IndexInBytes);
                Assert.Equal(expectedMatch.Length, ordinaryMatch.LengthInBytes);
                Assert.Equal(expectedMatch.Index, compiledMatch.IndexInBytes);
                Assert.Equal(expectedMatch.Length, compiledMatch.LengthInBytes);
            }
        }
    }

    [Fact]
    public void CompiledAsciiUntilByteStarCountMatchesDotNet()
    {
        const string pattern = @"[^\n]*";
        const string input = "alpha\n\nbeta\ngamma";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledAsciiUntilByteStarCountMatchesDotNetOnUtf8Input()
    {
        const string pattern = @"[^\n]*";
        const string input = "alpha\ncafeé\nαβ\n";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledUnicodeLetterCountMatchesDotNet()
    {
        const string pattern = @"\p{L}";
        const string input = "alpha βγ 夏洛克 𐐀 123 _";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledUtf8WordDelimitedTokenEmailCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        const string input = "été alpha@example.org βγ ops.dispatch@northwind-control.net 夏洛克";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledUtf8UriTokenCountMatchesDotNetOnMixedUtf8Input()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        const string input = "été https://atlas.example.org/reports/export?id=42 βγ http://northwind-control.net/a#frag 夏洛克";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Theory]
    [InlineData("é://host/path", 1)]
    [InlineData("https://hôte.example/路径", 1)]
    [InlineData("https://host.example/a\u00A0http://next.example/b", 2)]
    [InlineData("𐐀://host/path https://ok.example/a", 1)]
    [InlineData("x://a x://ab x:////", 1)]
    [InlineData("x://ab?query#fragment x://cd##tail", 2)]
    public void Utf8UriTokenCountMatchesDotNetUnicodeSemantics(string input, int expected)
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected, Regex.Count(input, pattern, options));
            Assert.Equal(expected, regex.Count(bytes));
        }
    }

    [Fact]
    public void Utf8UriTokenCountRejectsMalformedUtf8AfterAValidMatch()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        byte[] input = [.. "https://host.example/path"u8, 0xFF];

        Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern).Count(input));
        Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, RegexOptions.Compiled).Count(input));
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
            var regex = new Utf8Regex(pattern, options);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            Assert.Equal(oracle.Count(input), regex.Count(bytes));
        }
    }

    [Fact]
    public void ExplicitAsciiStructuredTokenCountRemainsEligibleOnMixedInput()
    {
        const string pattern = @"[A-Za-z]+://[A-Za-z0-9.]+[A-Za-z0-9/]+";
        const string input = "é https://atlas.example/path 夏";
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled);

        Assert.Equal(
            nameof(Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount),
            regex.Inspection.DebugFallbackDirectFamilyKind);
        Assert.Equal(Regex.Count(input, pattern), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CompiledAsciiUriTokenMatchMatchesDotNetForAsciiInput()
    {
        const string pattern = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        const string input = "https://atlas.example.org/reports/export?id=42";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.IsMatch(input, pattern), compiled.IsMatch(bytes));
        Assert.Equal(Regex.Match(input, pattern).Value, Encoding.UTF8.GetString(bytes[compiled.Match(bytes).IndexInBytes..(compiled.Match(bytes).IndexInBytes + compiled.Match(bytes).LengthInBytes)]));
    }

    [Fact]
    public void CompiledExactUtf8LiteralCanCountWithFusedValidation()
    {
        const string pattern = "夏洛克·福尔摩斯";
        const string input = "夏洛克·福尔摩斯 abc 夏洛克·福尔摩斯";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
                var regex = new Utf8Regex(pattern, options);
                Assert.Equal(oracle.Count(input), regex.Count(bytes));
            }
        }
    }

    [Fact]
    public void ExactTwoByteLiteralCorrelatedPrefixCountRejectsMalformedSuffix()
    {
        const string pattern = "Шерлок Холмс";
        var bytes = Enumerable.Repeat((byte)'x', 132 * 1024).ToArray();
        Encoding.UTF8.GetBytes(pattern).CopyTo(bytes, 64 * 1024 + 31);
        bytes[^1] = 0xFF;

        Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern).Count(bytes));
        Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, RegexOptions.Compiled).Count(bytes));
    }

    [Fact]
    public void CompiledExactUtf8LiteralFamilyCanCountWithFusedValidation()
    {
        const string pattern = "夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授";
        const string input = "夏洛克·福尔摩斯 约翰华生 x 阿德勒 雷斯垂德 莫里亚蒂教授";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledExactUtf8LiteralFamilyFusedValidationHandlesMixedScalarLengths()
    {
        const string pattern = "阿|阿德勒|雷斯垂德";
        const string input = "𐐀 阿 x 阿德勒 y 雷斯垂德 z 阿";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Count(input, pattern), compiled.Count(bytes));
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyReplaceMatchesDotNet()
    {
        const string pattern = "tempus|magna|semper";
        const string input = "tempus magna semper et tempus";
        const string replacement = "amoveatur";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Regex.Replace(input, pattern, replacement), Encoding.UTF8.GetString(compiled.Replace(bytes, replacement)));
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilySplitMatchesDotNet()
    {
        const string pattern = "tempus|magna|semper";
        const string input = "tempus magna semper et tempus";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = Regex.Split(input, pattern);
        var actual = new List<string>();

        foreach (var split in compiled.EnumerateSplits(bytes))
        {
            actual.Add(split.GetValueString());
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
            var regex = new Utf8Regex(testCase.Pattern, options);
            var bytes = Encoding.UTF8.GetBytes(testCase.Input);
            var actual = new List<string>();

            Assert.True(
                regex.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(bytes),
                $"Expected small ASCII literal-family Split route for '{testCase.Pattern}'.");
            foreach (var split in regex.EnumerateSplits(bytes))
            {
                actual.Add(split.GetValueString());
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
        var regex = new Utf8Regex(pattern, options);
        var managed = new Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(input);

        var actual = new List<string>();
        foreach (var split in regex.EnumerateSplits(bytes, count))
        {
            actual.Add(split.GetValueString());
        }

        Assert.Equal(managed.Split(input, count), actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SmallAsciiLiteralFamilySplitEnumerationDoesNotAllocatePerCall(bool compiled)
    {
        var options = compiled ? RegexOptions.Compiled : RegexOptions.None;
        var regex = new Utf8Regex("tempus|magna|semper", options);
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
            var regex = new Utf8Regex(testCase.Pattern, options);
            var bytes = Encoding.UTF8.GetBytes(testCase.Input);
            var actual = new List<string>();

            Assert.False(regex.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(bytes));
            foreach (var split in regex.EnumerateSplits(bytes))
            {
                actual.Add(split.GetValueString());
            }

            Assert.Equal(Regex.Split(testCase.Input, testCase.Pattern, options), actual);
        }

        Assert.False(new Utf8Regex("(alpha|bravo|charlie)", options)
            .Inspection.DebugCanUseSmallAsciiLiteralFamilySplit("alpha bravo"u8));
        Assert.False(new Utf8Regex(
                "alpha|bravo|charlie",
                options,
                TimeSpan.FromSeconds(1))
            .Inspection.DebugCanUseSmallAsciiLiteralFamilySplit("alpha bravo"u8));
        Assert.False(new Utf8Regex("alpha|bravo|charlie", options | RegexOptions.RightToLeft)
            .Inspection.DebugCanUseSmallAsciiLiteralFamilySplit("alpha bravo"u8));

        var malformed = new byte[] { (byte)'a', 0xC3, 0x28, (byte)'b' };
        var eligible = new Utf8Regex("alpha|bravo|charlie", options);
        Assert.False(eligible.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(malformed));
        Assert.Throws<ArgumentException>(() =>
        {
            _ = eligible.EnumerateSplits(malformed);
        });
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
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
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
        var regex = new Utf8Regex(pattern, options, timeout);
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
        var regex = new Utf8Regex(pattern, options, timeout);
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
            var regex = new Utf8Regex(pattern, options);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            Assert.Equal(oracle.Count(input), regex.Count(bytes));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneByteLiteralPairCountSelectorStillValidatesLargeMalformedUtf8(bool compiled)
    {
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("a|needle", options);
        var input = new byte[8192];
        input.AsSpan().Fill((byte)'a');
        input[^1] = 0xFF;

        Assert.Throws<ArgumentException>(() => regex.Count(input));
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyMatchMatchesDotNet()
    {
        const string pattern = "Tom|Sawyer|Huckleberry|Finn";
        const string input = "noise Sawyer tail";
        var compiled = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var bytes = Encoding.UTF8.GetBytes(input);
        var match = compiled.Match(bytes);

        Assert.True(match.Success);
        Assert.Equal(Regex.Match(input, pattern).Value, Encoding.UTF8.GetString(bytes.AsSpan(match.IndexInBytes, match.LengthInBytes)));
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
        var regex = new Utf8Regex(pattern, options, timeout);
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
        var regex = new Utf8Regex(pattern, options, timeout);
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
        var regex = new Utf8Regex(pattern, options);
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
        var regex = new Utf8Regex(pattern, options);
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
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
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
        var regex = new Utf8Regex(pattern, options);
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
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
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
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes("é " + new string('x', 32_768) + " 夏");
        var sink = false;

        for (var i = 0; i < 64; i++)
        {
            sink ^= regex.IsMatch(input);
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 256; i++)
        {
            sink ^= regex.IsMatch(input);
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        GC.KeepAlive(sink);
        Assert.InRange(allocated, 0, 512);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LiteralFamilyIsMatchStillRejectsMalformedUtf8(bool ignoreCase, bool compiled)
    {
        var options = RegexOptions.CultureInvariant |
            (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("alpha|bravo|charlie|delta|echo", options);
        byte[] malformed = [(byte)'x', 0xC3, 0x28, (byte)'a'];

        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
    }

    [Fact]
    public void CompiledExactAsciiLiteralFamilyCanMatchWithoutValidation()
    {
        var regex = new Utf8Regex("tempus|magna|semper", RegexOptions.None);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("xxx magna yyy"u8, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(4, match.IndexInBytes);
        Assert.Equal(5, match.LengthInBytes);
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

        var regex = new Utf8Regex("tempus|magna|semper", RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(builder.ToString());

        var handled = regex.Inspection.DebugTryMatchWithoutValidation(bytes, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
    }

    [Fact]
    public void ExactAsciiLiteralFamilyDirectMatchValidatesNonAsciiSuffix()
    {
        var regex = new Utf8Regex("tempus|magna|semper", RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes("magna café");

        var handled = regex.Inspection.DebugTryMatchWithoutValidation(bytes, out var match);

        Assert.True(handled);
        Assert.True(match.Success);
        Assert.Equal(0, match.IndexInBytes);
        Assert.Equal(5, match.LengthInBytes);
    }

    [Fact]
    public void ExactAsciiLiteralFamilyDirectMatchDeclinesNonAsciiPrefixProjection()
    {
        var regex = new Utf8Regex("tempus|magna|semper", RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes("café magna");

        Assert.False(regex.Inspection.DebugTryMatchWithoutValidation(bytes, out _));

        var match = regex.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(5, match.IndexInUtf16);
        Assert.Equal(6, match.IndexInBytes);
    }

    [Fact]
    public void ExactAsciiLiteralFamilyDirectMatchStillRejectsMalformedSuffix()
    {
        var regex = new Utf8Regex("tempus|magna|semper", RegexOptions.None);
        byte[] malformed = [.. "magna"u8, 0xFF];

        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
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
        var regex = new Utf8Regex("tempus|magna|semper", options);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("xxx nihil yyy"u8, out var match);

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
        var regex = new Utf8Regex("alpha|bravo|charlie|delta|echo", options);

        var handled = regex.Inspection.DebugTryMatchWithoutValidation("xxx foxtrot yyy"u8, out var match);

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
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(expectedReason, regex.Inspection.FallbackReason);
    }

    [Fact]
    public void ConstructorClassifiesPositiveLiteralLookaheadAsAsciiSimplePattern()
    {
        var regex = new Utf8Regex("foo(?=bar)", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8CompiledEngineKind.ExactLiteral, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void ConstructorKeepsInvariantIgnoreCasePositiveLiteralLookaheadOnFallback()
    {
        var regex = new Utf8Regex("foo(?=bar)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorKeepsInvariantIgnoreCasePositiveLiteralAlternationLookaheadOnFallback()
    {
        var regex = new Utf8Regex("(?:cat|dog)(?=house)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void ConstructorClassifiesPositiveSeparatorLiteralLookaheadAsOrderedLiteralWindow()
    {
        var regex = new Utf8Regex("HttpClient(?=\\s+client)", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
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
            var regex = new Utf8Regex(pattern, options);

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
            Assert.Equal(Regex.Count(input, pattern, options), new Utf8Regex(pattern, options).Count(bytes));
        }
    }

    [Fact]
    public void SeparatorLiteralLookaheadCountStillValidatesAfterAnAsciiMatch()
    {
        var regex = new Utf8Regex("HttpClient(?=\\s+client)", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        byte[] malformed = [.. "HttpClient client"u8, 0xFF];

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Fact]
    public void OrderedLiteralWindowLiteralFamilyCountPreservesBehavior()
    {
        var regex = new Utf8Regex(@"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b", RegexOptions.CultureInvariant);
        var input = """
            public sealed class First
            private readonly struct Skip
            internal partial class Second
            public
            class Third
            """u8;

        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.Inspection.ExecutionKind);
        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(0, match.IndexInBytes);
    }

    [Fact]
    public void OrderedLiteralWindowLiteralFamilyIgnoresInvalidEarlierTrailingLiteral()
    {
        var regex = new Utf8Regex(@"\b(?:public|private|internal)\b\s+.{0,80}\bclass\b", RegexOptions.CultureInvariant);
        var input = "publicclass noise public sealed class Valid"u8;

        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("public sealed class".Length, match.LengthInBytes);
        Assert.Equal("publicclass noise ".Length, match.IndexInBytes);
    }

    [Fact]
    public void SeparatorOnlyIdentifierFamilyIgnoresMissingWhitespace()
    {
        var regex = new Utf8Regex(@"\b(?:public|private|internal)\s+class\b", RegexOptions.CultureInvariant);
        var input = "publicclass public class internal\tclass"u8;

        Assert.Equal(2, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("publicclass ".Length, match.IndexInBytes);
    }

    [Fact]
    public void DeclarationIdentifierFamilySkipsInvalidEarlierPrefixForMatch()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant);
        var input = "class lowercase record CustomerExportJob"u8;

        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("class lowercase ".Length, match.IndexInBytes);
        Assert.Equal("record CustomerExportJob".Length, match.LengthInBytes);
    }

    [Fact]
    public void CompiledDeclarationIdentifierFamilyMatchesExpected()
    {
        var regex = new Utf8Regex(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "class lowercase record CustomerExportJob struct lower"u8;

        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("class lowercase ".Length, match.IndexInBytes);
        Assert.Equal("record CustomerExportJob".Length, match.LengthInBytes);
    }

    [Fact]
    public void CompiledLoggingInvocationFamilyMatchesExpected()
    {
        var regex = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "LogWarningx LogInformation ("u8;

        Assert.Equal(Utf8CompiledEngineKind.EmittedKernel, regex.Inspection.CompiledEngineKind);
        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("LogWarningx ".Length, match.IndexInBytes);
        Assert.Equal("LogInformation (".Length, match.LengthInBytes);
    }

    [Fact]
    public void CompiledOrderedLiteralWindowMatchesExpected()
    {
        var regex = new Utf8Regex(@"\bpublic\s+async\b", RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = "xpublic async public   asyncz public async"u8;

        Assert.Equal(Utf8CompiledEngineKind.EmittedKernel, regex.Inspection.CompiledEngineKind);
        Assert.Equal(1, regex.Count(input));
        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal("xpublic async public   asyncz ".Length, match.IndexInBytes);
        Assert.Equal("public async".Length, match.LengthInBytes);
    }

    [Fact]
    public void CompiledBoundedOrderedLiteralWindowMatchesExpected()
    {
        const string pattern = @"\bawait\b\s+.{0,60}\bConfigureAwait\b";
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var input = """
            awaitx ConfigureAwait
            await ConfigureAwait
            await    foo.ConfigureAwait
            await
            foo.ConfigureAwait
            """;
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = Regex.Match(input, pattern, RegexOptions.CultureInvariant);

        Assert.Equal(Utf8CompiledEngineKind.EmittedKernel, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Regex.Count(input, pattern, RegexOptions.CultureInvariant), regex.Count(bytes));
        var match = regex.Match(bytes);
        Assert.True(match.Success);
        Assert.Equal(expected.Index, match.IndexInBytes);
        Assert.Equal(expected.Length, match.LengthInBytes);
    }

    [Fact]
    public void ConstructorBuildsFixedTokenLinearProgramForDeterministicAsciiSimplePattern()
    {
        var regex = new Utf8Regex("ab[0-9]d", RegexOptions.CultureInvariant);
        var analysis = Utf8FrontEnd.Compile("ab[0-9]d", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern, regex.Inspection.StructuralLinearProgramKind);
        Assert.Equal(
            [
                Utf8StructuralLinearInstructionKind.Literal,
                Utf8StructuralLinearInstructionKind.Literal,
                Utf8StructuralLinearInstructionKind.CharClass,
                Utf8StructuralLinearInstructionKind.Literal,
                Utf8StructuralLinearInstructionKind.Accept,
            ],
            analysis.StructuralLinearProgram.InstructionProgram.Instructions.Select(static instruction => instruction.Kind));
    }

    [Fact]
    public void ConstructorPromotesOptionalAsciiLiteralPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("ab?c", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesFiniteAsciiLiteralAlternationLoopToLiteralFamily()
    {
        var regex = new Utf8Regex("(?:ab|cd){2}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesInvariantIgnoreCaseOptionalAsciiLiteralPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("ab?c", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesInvariantIgnoreCaseFiniteAsciiCharClassPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("a[bc]?d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesInvariantIgnoreCaseFiniteAsciiCharClassPatternWithoutOptionalToLiteralFamily()
    {
        var regex = new Utf8Regex("a[bc]d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesFiniteAsciiCharClassOptionalPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("a[bc]?d", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ConstructorPromotesFiniteAsciiCharClassRepeatPatternToLiteralFamily()
    {
        var regex = new Utf8Regex("[ab]{2}", RegexOptions.CultureInvariant);

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
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("abc", RegexOptions.CultureInvariant, TimeSpan.Zero));
    }

    [Fact]
    public void DirectPrefixFamilyCanMatchDocLineDirectly()
    {
        var regex = new Utf8Regex("\\G///[^\\n]*\\n", RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var match = regex.Match("/// summary line\n"u8);

        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.Null(regex.Inspection.FallbackReason);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(0, match.IndexInBytes);
        Assert.Equal(17, match.LengthInBytes);
    }

    [Fact]
    public void FallbackRegexCanMatchEnvisionQuotedStringDirectly()
    {
        var regex = new Utf8Regex("\\G\"([^\"\\\\]|\\\\.)*\"", RegexOptions.CultureInvariant);
        var match = regex.Match("\"hello\\\"world\""u8);

        Assert.Equal(Utf8CompiledEngineKind.FallbackRegex, regex.Inspection.CompiledEngineKind);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(0, match.IndexInBytes);
        Assert.Equal(14, match.LengthInBytes);
        Assert.Equal(14, match.LengthInUtf16);
    }

    [Fact]
    public void FallbackRegexCanMatchEnvisionQuotedStringDirectlyWithUtf8Content()
    {
        var regex = new Utf8Regex("\\G\"([^\"\\\\]|\\\\.)*\"", RegexOptions.CultureInvariant);
        var match = regex.Match("\"été\""u8);

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(0, match.IndexInBytes);
        Assert.Equal(7, match.LengthInBytes);
        Assert.Equal(5, match.LengthInUtf16);
    }

}

