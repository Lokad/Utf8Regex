using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class LokadCodeRouteGuardrailTests
{
    [Fact]
    public void TypeTokenFamilyStaysOnLiteralFamilyEngine()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.SearchGuidedFallback, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledExecutionBackend.EmittedInstruction, regex.Inspection.CompiledExecutionBackend);
    }

    [Fact]
    public void MethodTokenFamilyStaysOnStructuralFamilyEngine()
    {
        var regex = new Utf8Regex(@"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void CompiledMethodTokenFamilyUsesEmittedBoundaryLiteralKernel()
    {
        const string pattern = @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b";
        const string input = "é LogTrace λLogError LogWarning_ LogDebug.";
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var expected = Regex.Count(input, pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.Equal(Utf8CompiledExecutionBackend.EmittedInstruction, regex.Inspection.CompiledExecutionBackend);
        Assert.Equal(expected, regex.Count(bytes));
        Assert.Equal(expected > 0, regex.IsMatch(bytes));
        Assert.Equal("native_structural_family_emit", regex.CollectCountDiagnostics(bytes).ExecutionRoute);
    }

    [Fact]
    public void CompiledMethodTokenFamilyStillRejectsMalformedUtf8()
    {
        var regex = new Utf8Regex(
            @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
        byte[] malformed = [0xFF, .. System.Text.Encoding.UTF8.GetBytes(" LogTrace")];

        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
    }

    [Fact]
    public void OrderedIdentifierWindowStaysOnStructuralEngine()
    {
        var regex = new Utf8Regex(@"\bHttpClient\b[\s\S]{0,80}\bSendAsync\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiOrderedLiteralWindow, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiteral, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void ModifierToTypePairStaysOnStructuralEngine()
    {
        var regex = new Utf8Regex(@"\b(?:public|private|internal)\s+class\b", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void MethodFamilyGenericCallStaysOnStructuralEngine()
    {
        var regex = new Utf8Regex(@"\b(?:AddSingleton|AddScoped|AddTransient)\s*<", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void MethodFamilyCallBuildsSharedPrefixStructuralKernel()
    {
        var regex = new Utf8Regex(@"\b(?:LogError|LogWarning|LogInformation)\s*\(", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, regex.Inspection.CompiledEngineKind);
    }
}
