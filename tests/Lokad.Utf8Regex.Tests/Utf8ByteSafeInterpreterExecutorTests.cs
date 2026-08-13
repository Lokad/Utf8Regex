using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ByteSafeInterpreterExecutorTests
{
    [Fact]
    public void CanExecuteAcceptsDeterministicLeadingAsciiPrefixFallbackPattern()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.ExecutionKind);
        Assert.True(Utf8ByteSafeInterpreterExecutor.CanExecute(analysis));
        Assert.Equal(Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram, analysis.StructuralVerifier.Kind);
    }

    [Fact]
    public void ByteSafeInterpreterCompilesDeterministicVerifierGuards()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);

        Assert.True(analysis.DeterministicGuards.HasValue);
        Assert.True(analysis.DeterministicGuards.PrefixGuards is { Length: > 0 });
        Assert.True(analysis.DeterministicGuards.MinRequiredLength > 0);
        Assert.Equal(analysis.SearchPlan.Distance, analysis.DeterministicGuards.FixedLiteralOffset);
        Assert.Equal(analysis.SearchPlan.LiteralUtf8, analysis.DeterministicGuards.FixedLiteralUtf8);
    }

    [Fact]
    public void ByteSafeInterpreterCompilesDeterministicAnchorSearcher()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);

        Assert.True(analysis.DeterministicAnchor.HasValue);
        Assert.Equal(0, analysis.DeterministicAnchor.Offset);
        Assert.Equal(PreparedSearcherKind.IgnoreCaseLiteral, analysis.DeterministicAnchor.Searcher.Kind);
        Assert.Equal(0, analysis.DeterministicAnchor.Searcher.FindFirst("# noqa: F401"u8));
    }

    [Fact]
    public void DeterministicVerifierGuardsRejectCandidatesThatMissCompiledChecks()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);
        var guards = analysis.DeterministicGuards;
        var length = Math.Max(
            guards.MinRequiredLength,
            guards.FixedLiteralOffset + (guards.FixedLiteralUtf8?.Length ?? 1));
        var input = new byte[length];
        Array.Fill(input, (byte)'x');

        Assert.False(guards.Matches(input, 0));
    }

    [Fact]
    public void CanExecuteAcceptsSelectiveStructuralStartFallbackPattern()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)",
            RegexOptions.None);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.ExecutionKind);
        Assert.True(Utf8ByteSafeInterpreterExecutor.CanExecute(analysis));
        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, Utf8CompiledEngineSelector.Select(analysis).Kind);
    }

    [Fact]
    public void CanExecuteRejectsFallbackLoopPatternsWithoutDeterministicLeadingAnchor()
    {
        var analysis = Utf8FrontEnd.Compile("ab+", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, analysis.ExecutionKind);
        Assert.False(Utf8ByteSafeInterpreterExecutor.CanExecute(analysis));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileDeterministicBoundedSubset()
    {
        var analysis = Utf8FrontEnd.Compile("^ab[0-9]{2,4}$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("ab123"u8, 0, out var matchedLength));
        Assert.Equal(5, matchedLength);
        Assert.False(program.TryMatch("ab1x"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLazyDfaVerifierProgramCanCompileAnchoredDeterministicSubset()
    {
        var analysis = Utf8FrontEnd.Compile("^ab[0-9][0-9]$", RegexOptions.CultureInvariant);
        var linearProgram = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;
        var lazyDfaProgram = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linearProgram).Program;

        Assert.True(lazyDfaProgram.HasValue);
        Assert.True(lazyDfaProgram.TryMatch("ab12"u8, 0, out var matchedLength));
        Assert.Equal(4, matchedLength);
        Assert.False(lazyDfaProgram.TryMatch("ab1x"u8, 0, out _));
        Assert.False(lazyDfaProgram.TryMatch("ab123"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileOptionalDeterministicTail()
    {
        var analysis = Utf8FrontEnd.Compile("^ab(?:cd)?$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("ab"u8, 0, out var shortLength));
        Assert.Equal(2, shortLength);
        Assert.True(program.TryMatch("abcd"u8, 0, out var longLength));
        Assert.Equal(4, longLength);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileBoundaryChecks()
    {
        var analysis = Utf8FrontEnd.Compile(@"\bab[0-9]{2}\b", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("ab12 "u8, 0, out var matchedLength));
        Assert.Equal(4, matchedLength);
        Assert.False(program.TryMatch("xab12 "u8, 1, out _));
        Assert.False(program.TryMatch("ab12x"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileTerminalAlternationTail()
    {
        var analysis = Utf8FrontEnd.Compile("^ab(?:cd|ef)$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("abcd"u8, 0, out var firstLength));
        Assert.Equal(4, firstLength);
        Assert.True(program.TryMatch("abef"u8, 0, out var secondLength));
        Assert.Equal(4, secondLength);
        Assert.False(program.TryMatch("abgh"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileOptionalTerminalAlternationTail()
    {
        var analysis = Utf8FrontEnd.Compile("^ab(?:cd|ef)?$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("ab"u8, 0, out var shortLength));
        Assert.Equal(2, shortLength);
        Assert.True(program.TryMatch("abcd"u8, 0, out var firstLength));
        Assert.Equal(4, firstLength);
        Assert.True(program.TryMatch("abef"u8, 0, out var secondLength));
        Assert.Equal(4, secondLength);
        Assert.False(program.TryMatch("abgh"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileDeterministicInlineAlternation()
    {
        var analysis = Utf8FrontEnd.Compile("^ab(?:cd|ef)gh$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("abcdgh"u8, 0, out var firstLength));
        Assert.Equal(6, firstLength);
        Assert.True(program.TryMatch("abefgh"u8, 0, out var secondLength));
        Assert.Equal(6, secondLength);
        Assert.False(program.TryMatch("abgh"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramRejectsAmbiguousInlineAlternation()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:a|ab)c$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.False(program.HasValue);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileSafeOptionalInlineAlternation()
    {
        var analysis = Utf8FrontEnd.Compile("^ab(?:cd|ef)?gh$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("abgh"u8, 0, out var shortLength));
        Assert.Equal(4, shortLength);
        Assert.True(program.TryMatch("abcdgh"u8, 0, out var firstLength));
        Assert.Equal(6, firstLength);
        Assert.True(program.TryMatch("abefgh"u8, 0, out var secondLength));
        Assert.Equal(6, secondLength);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramRejectsUnsafeOptionalInlineAlternation()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:e)?ef$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.False(program.HasValue);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileFixedCountAlternationLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:ab|cd){2}ef$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("ababef"u8, 0, out var firstLength));
        Assert.Equal(6, firstLength);
        Assert.True(program.TryMatch("abcdef"u8, 0, out var secondLength));
        Assert.Equal(6, secondLength);
        Assert.True(program.TryMatch("cdcdef"u8, 0, out var thirdLength));
        Assert.Equal(6, thirdLength);
        Assert.False(program.TryMatch("abef"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramRejectsAmbiguousAlternationLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:a|ab){2}$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.False(program.HasValue);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileTerminalVariableAlternationLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:ab|cd){1,3}$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("ab"u8, 0, out var firstLength));
        Assert.Equal(2, firstLength);
        Assert.True(program.TryMatch("abcd"u8, 0, out var secondLength));
        Assert.Equal(4, secondLength);
        Assert.True(program.TryMatch("abcdab"u8, 0, out var thirdLength));
        Assert.Equal(6, thirdLength);
        Assert.False(program.TryMatch(""u8, 0, out _));
        Assert.False(program.TryMatch("abcdabcd"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileSafeNonTerminalVariableAlternationLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:ab|cd){1,3}ef$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("abef"u8, 0, out var firstLength));
        Assert.Equal(4, firstLength);
        Assert.True(program.TryMatch("abcdabef"u8, 0, out var secondLength));
        Assert.Equal(8, secondLength);
        Assert.False(program.TryMatch("ef"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramRejectsUnsafeNonTerminalVariableAlternationLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:ab|cd){1,3}ab$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.False(program.HasValue);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileSafeNonTerminalVariableByteLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^a{0,3}b$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("b"u8, 0, out var firstLength));
        Assert.Equal(1, firstLength);
        Assert.True(program.TryMatch("aaab"u8, 0, out var secondLength));
        Assert.Equal(4, secondLength);
        Assert.False(program.TryMatch("aaaaab"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileSafeNonTerminalVariableSetLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^[A-Z]{0,3}:$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch(":"u8, 0, out var firstLength));
        Assert.Equal(1, firstLength);
        Assert.True(program.TryMatch("ABC:"u8, 0, out var secondLength));
        Assert.Equal(4, secondLength);
        Assert.False(program.TryMatch("ABCD:"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramRejectsUnsafeNonTerminalVariableByteLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^a{0,3}a$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.False(program.HasValue);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramRejectsLazyVariableLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^a+?b$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.False(program.HasValue);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileRepeatedDeterministicSegmentLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:[A-Z][a-z]+ *){2,4}$", RegexOptions.CultureInvariant);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("Alpha Beta"u8, 0, out var firstLength));
        Assert.Equal(10, firstLength);
        Assert.True(program.TryMatch("Alpha Beta Gamma "u8, 0, out var secondLength));
        Assert.Equal(17, secondLength);
        Assert.False(program.TryMatch("alpha Beta"u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearRuntimeSupportsMatchAndEnumerateMatches()
    {
        var regex = new Utf8Regex(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);
        var input = "# NOQA: F401"u8.ToArray();

        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, regex.CompiledEngineKind);

        var match = regex.Match(input);
        Assert.True(match.Success);
        Assert.Equal(0, match.IndexInBytes);

        var enumerator = regex.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(0, enumerator.Current.IndexInBytes);
        Assert.False(enumerator.MoveNext());
    }

    [Fact]
    public void ByteSafeVerifierPlanPrefersLazyDfaWhenAvailable()
    {
        var analysis = Utf8FrontEnd.Compile("^ab[0-9][0-9]$", RegexOptions.CultureInvariant);
        var program = Assert.IsType<Utf8ExecutionProgram>(analysis.ExecutionProgram);
        var verifierPlan = Utf8RegexPreparer.PrepareByteSafeStructuralVerifier(
            analysis.ExecutionTree,
            program,
            analysis.DeterministicGuards);

        Assert.Equal(Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram, verifierPlan.Kind);
        Assert.True(verifierPlan.ByteSafeLazyDfaProgram.HasValue);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileRuffNoqaShape()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);

        Assert.Equal(
            Utf8ByteSafeLinearCompileFailureKind.None,
            Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).FailureKind);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileProjectedAsciiWhitespaceSet()
    {
        var analysis = Utf8FrontEnd.Compile("^\\s+$", RegexOptions.None);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch(" \t"u8, 0, out var matchedLength, out var requiresCompatibilityFallback));
        Assert.False(requiresCompatibilityFallback);
        Assert.Equal(2, matchedLength);
        Assert.False(program.TryMatch("A"u8, 0, out _, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileNoqaInnerSegment()
    {
        var analysis = Utf8FrontEnd.Compile("^[A-Z]+[0-9]+(?:[,\\s]+)?$", RegexOptions.None);
        Assert.Equal(
            Utf8ByteSafeLinearCompileFailureKind.None,
            Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).FailureKind);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("F401"u8, 0, out var firstLength, out var firstFallback));
        Assert.False(firstFallback);
        Assert.Equal(4, firstLength);
        Assert.True(program.TryMatch("F401, "u8, 0, out var secondLength, out var secondFallback));
        Assert.False(secondFallback);
        Assert.Equal(6, secondLength);
    }

    [Fact]
    public void ByteSafeLazyDfaVerifierProgramCanCompileNoqaInnerSegment()
    {
        var analysis = Utf8FrontEnd.Compile("^[A-Z]+[0-9]+(?:[,\\s]+)?$", RegexOptions.None);
        var linearProgram = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;
        var lazyDfaProgram = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linearProgram).Program;

        Assert.True(lazyDfaProgram.HasValue);
        Assert.Equal(
            Utf8ByteSafeLazyDfaCompileFailureKind.None,
            Utf8ByteSafeLazyDfaVerifierProgram.Compile(linearProgram).FailureKind);
        Assert.True(lazyDfaProgram.TryMatch("F401"u8, 0, out var firstLength));
        Assert.Equal(4, firstLength);
        Assert.True(lazyDfaProgram.TryMatch("F401, "u8, 0, out var secondLength));
        Assert.Equal(6, secondLength);
        Assert.False(lazyDfaProgram.TryMatch("F, "u8, 0, out _));
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileAsciiLettersThenDigits()
    {
        var analysis = Utf8FrontEnd.Compile("^[A-Z]+[0-9]+$", RegexOptions.None);

        Assert.Equal(
            Utf8ByteSafeLinearCompileFailureKind.None,
            Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).FailureKind);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileDigitsWithOptionalSeparatorTail()
    {
        var analysis = Utf8FrontEnd.Compile("^[0-9]+(?:[,\\s]+)?$", RegexOptions.None);

        Assert.Equal(
            Utf8ByteSafeLinearCompileFailureKind.None,
            Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).FailureKind);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramMatchesAsciiCommaWhitespaceSetLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^[,\\s]+$", RegexOptions.None);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.Equal(Utf8ByteSafeLinearVerifierStepKind.LoopProjectedAsciiSet, program.Steps[1].Kind);
        Assert.NotNull(program.Steps[1].ProjectedAsciiCharClass);
        Assert.True(program.Steps[1].ProjectedAsciiCharClass!.Contains((byte)','));
        Assert.True(program.Steps[1].ProjectedAsciiCharClass!.Contains((byte)' '));
        Assert.Equal(1, program.Steps[1].Min);
        Assert.True(program.Steps[1].Max > 1 || program.Steps[1].Max < 0);
        Assert.True(program.TryMatch(", "u8, 0, out var matchedLength, out var requiresCompatibilityFallback));
        Assert.False(requiresCompatibilityFallback);
        Assert.Equal(2, matchedLength);
    }

    [Fact]
    public void ByteSafeLinearVerifierProgramCanCompileNoqaRepeatedSegmentLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:[A-Z]+[0-9]+(?:[,\\s]+)?)+$", RegexOptions.None);
        Assert.Equal(
            Utf8ByteSafeLinearCompileFailureKind.None,
            Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).FailureKind);
        var program = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;

        Assert.True(program.HasValue);
        Assert.True(program.TryMatch("F401"u8, 0, out var firstLength, out var firstFallback));
        Assert.False(firstFallback);
        Assert.Equal(4, firstLength);
        Assert.True(program.TryMatch("F401, E501"u8, 0, out var secondLength, out var secondFallback));
        Assert.False(secondFallback);
        Assert.Equal(10, secondLength);
    }

    [Fact]
    public void ByteSafeLazyDfaVerifierProgramCanCompileNoqaRepeatedSegmentLoop()
    {
        var analysis = Utf8FrontEnd.Compile("^(?:[A-Z]+[0-9]+(?:[,\\s]+)?)+$", RegexOptions.None);
        var linearProgram = Utf8ByteSafeLinearVerifierProgram.Compile(analysis.ExecutionTree).Program;
        var lazyDfaProgram = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linearProgram).Program;

        Assert.True(lazyDfaProgram.HasValue);
        Assert.Equal(
            Utf8ByteSafeLazyDfaCompileFailureKind.None,
            Utf8ByteSafeLazyDfaVerifierProgram.Compile(linearProgram).FailureKind);
        Assert.True(lazyDfaProgram.TryMatch("F401"u8, 0, out var firstLength));
        Assert.Equal(4, firstLength);
        Assert.True(lazyDfaProgram.TryMatch("F401, E501"u8, 0, out var secondLength));
        Assert.Equal(10, secondLength);
    }

    [Fact]
    public void ByteSafeVerifierPlanPrefersLazyDfaForRuffNoqaTweakedShape()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);
        var verifierPlan = analysis.StructuralVerifier;

        Assert.Equal(Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram, verifierPlan.Kind);
        Assert.True(verifierPlan.ByteSafeLazyDfaProgram.HasValue);
    }

    [Fact]
    public void ByteSafeVerifierPlanKeepsLinearVerifierForRuffNoqaRealShape()
    {
        var analysis = Utf8FrontEnd.Compile(
            "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)",
            RegexOptions.None);
        var verifierPlan = analysis.StructuralVerifier;

        Assert.Equal(Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram, verifierPlan.Kind);
        Assert.True(verifierPlan.ByteSafeLazyDfaProgram.HasValue);
    }

    [Fact]
    public void Utf8RegexUsesLazyDfaVerifierForRuffNoqaTweakedShape()
    {
        var regex = new Utf8Regex(
            "(?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?",
            RegexOptions.None);

        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, regex.CompiledEngineKind);
        Assert.Equal(Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram, regex.StructuralVerifierPlan.Kind);
        Assert.True(regex.StructuralVerifierPlan.ByteSafeLazyDfaProgram.HasValue);
    }

    [Fact]
    public void Utf8RegexUsesLazyDfaVerifierForRuffNoqaRealShape()
    {
        var regex = new Utf8Regex(
            "(\\s*)((?:# [Nn][Oo][Qq][Aa])(?::\\s?(([A-Z]+[0-9]+(?:[,\\s]+)?)+))?)",
            RegexOptions.None);

        Assert.Equal(Utf8CompiledEngineKind.ByteSafeLinear, regex.CompiledEngineKind);
        Assert.Equal(Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram, regex.StructuralVerifierPlan.Kind);
        Assert.True(regex.StructuralVerifierPlan.ByteSafeLazyDfaProgram.HasValue);
    }
}
