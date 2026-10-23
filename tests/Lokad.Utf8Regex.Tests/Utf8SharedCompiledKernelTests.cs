using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SharedCompiledKernelTests
{
    [Theory]
    [InlineData(false, @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class Bravo")]
    [InlineData(true, @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class Bravo")]
    [InlineData(false, @"\b(?:LogError|LogWarning|LogInformation)\s*\(", "LogInformation (")]
    [InlineData(true, @"\b(?:LogError|LogWarning|LogInformation)\s*\(", "LogInformation (")]
    public void OneKernelServesItsOwnersWithoutChangingScalarResultsOrInvocationControls(bool compiled, string pattern, string value)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000, MaxDepth = 2048,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 };
        foreach (var limits in new Utf8ResourcePolicy?[] { null, policy })
        {
            var regex = limits is { } finite
                ? new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, finite)
                : new Utf8Regex(pattern, options);
            var program = (Utf8RegexProgram)typeof(Utf8Regex).GetField("_program", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
            if (limits is not null)
            {
                Assert.Null(program.VerifierRuntime);
                Assert.Null(program.CompiledEngineRuntime);
                Assert.Null(program.DirectStructuralFamilyKernelMatcher);
            }
            else
            {
                var runtime = Assert.IsType<Utf8StructuralLinearAutomatonCompiledEngineRuntime>(program.CompiledEngineRuntime);
                Assert.NotNull(program.DirectStructuralFamilyKernelMatcher);
                if (compiled) Assert.Same(program.DirectStructuralFamilyKernelMatcher, runtime.PreparedKernelMatcher);
                else Assert.Null(runtime.PreparedKernelMatcher);
            }

            var input = Encoding.UTF8.GetBytes("é😀 " + value);
            Assert.True(regex.IsMatch(input));
            Assert.Equal(1, regex.Count(input));
            var first = regex.Match(input);
            Assert.Equal((7, Encoding.UTF8.GetByteCount(value), 4, value.Length),
                (first.IndexInBytes, first.LengthInBytes, first.IndexInUtf16, first.LengthInUtf16));
            var count = 0;
            foreach (var match in regex.EnumerateMatches(input))
            {
                Assert.Equal((first.IndexInBytes, first.LengthInBytes), (match.IndexInBytes, match.LengthInBytes));
                count++;
            }
            Assert.Equal(1, count);
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            var error = Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token));
            Assert.Equal(canceled.Token, error.CancellationToken);
        }
    }

    [Theory]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+")]
    [InlineData(@"\bpublic\s+async\b")]
    public void SelectingPreparedEngineDoesNotAcquireDiscardedKernelBuffers(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 0, ulong.MaxValue, ulong.MaxValue);
        var engine = Utf8CompiledEngineSelector.Select(regex.Inspection.PreparedRegex);
        Assert.Equal(Utf8CompiledEngineKind.StructuralLinearAutomaton, engine.Kind);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
    }
}
