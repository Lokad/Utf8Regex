using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class LokadCodeRouteGuardrailTests
{
    [Fact]
    public void TypeTokenFamilyStaysOnLiteralFamilyEngine()
    {
        var regex = new Utf8Regex(@"\b(?:Task|ValueTask|IAsyncEnumerable)\b", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
        Assert.Equal(Utf8CompiledExecutionBackend.EmittedInstruction, regex.Inspection.CompiledExecutionBackend);
        Assert.True(regex.Inspection.DebugPrioritizesBoundaryLiteralFamilyCount);
    }

    [Fact]
    public void MethodTokenFamilyUsesLiteralFamilyEngine()
    {
        var regex = new Utf8Regex(@"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b", options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, regex.Inspection.CompiledEngineKind);
    }

    [Fact]
    public void CompiledMethodTokenFamilyUsesOwnedUnitsForMixedSubjects()
    {
        const string pattern = @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b";
        const string input = "é LogTrace λLogError LogWarning_ LogDebug.";
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var expected = Regex.Count(input, pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.Equal(Utf8CompiledExecutionBackend.EmittedInstruction, regex.Inspection.CompiledExecutionBackend);
        Assert.Equal(expected, regex.Count(bytes));
        Assert.Equal(expected > 0, regex.IsMatch(bytes));
        // Mixed subjects preserve scalar word boundaries and byte coordinates.
        Assert.Equal("<none>", regex.CollectCountDiagnostics(bytes).ExecutionRoute);
        var match = regex.Match(bytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, Regex.Match(input, pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled).Index)), match.StartOffsetInBytes);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
