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
