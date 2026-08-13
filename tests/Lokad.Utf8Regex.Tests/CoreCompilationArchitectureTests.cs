using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class CoreCompilationArchitectureTests
{
    [Fact]
    public void CompilationProducesOneImmutablePreparedRegex()
    {
        var analysis = Utf8FrontEnd.Analyze("ab[0-9]{2}", RegexOptions.CultureInvariant);
        var prepared = Utf8RegexPreparer.Prepare(analysis);

        Assert.IsType<Utf8RegexAnalysis>(analysis);
        Assert.IsType<Utf8PreparedRegex>(prepared);
        Assert.Same(prepared.StructuralLinearProgram.InstructionProgram.Instructions, prepared.StructuralLinearProgram.InstructionProgram.Instructions);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, prepared.ExecutionKind);
    }

    [Fact]
    public void PreparedRegexConstructionIsInertAndLayerOwnersStayDirectional()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var preparedSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Planning", "Utf8PreparedRegex.cs"));
        Assert.DoesNotContain(".Create(", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Select(", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Regex(", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Executor.", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeFactory", preparedSource, StringComparison.Ordinal);

        Assert.True(File.Exists(Path.Combine(sourceRoot, "Internal", "FrontEnd", "Utf8NativeExecutionAnalyzer.cs")));
        Assert.True(File.Exists(Path.Combine(sourceRoot, "Internal", "FrontEnd", "Utf8AsciiSimplePatternLowerer.cs")));
        Assert.True(File.Exists(Path.Combine(sourceRoot, "Internal", "FrontEnd", "Utf8FallbackRegexFamilyAnalyzer.cs")));
        Assert.True(File.Exists(Path.Combine(sourceRoot, "Internal", "Execution", "Utf8SearchExecutor.cs")));
        Assert.False(File.Exists(Path.Combine(sourceRoot, "Utf8Regex.Partials.cs")));

        foreach (var planningSourcePath in Directory.EnumerateFiles(
                     Path.Combine(sourceRoot, "Internal", "Planning"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            var planningSource = File.ReadAllText(planningSourcePath);
            Assert.DoesNotContain("ReadOnlySpan<byte>", planningSource, StringComparison.Ordinal);
            Assert.DoesNotContain("RuntimeFactory", planningSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Executor.", planningSource, StringComparison.Ordinal);
            Assert.DoesNotContain("new Regex(", planningSource, StringComparison.Ordinal);
        }

        foreach (var executionSourcePath in Directory.EnumerateFiles(
                     Path.Combine(sourceRoot, "Internal", "Execution"),
                     "*.cs",
                     SearchOption.AllDirectories))
        {
            var executionSource = File.ReadAllText(executionSourcePath);
            Assert.DoesNotContain("Utf8NativeExecutionAnalyzer", executionSource, StringComparison.Ordinal);
            Assert.DoesNotContain("Utf8FallbackRegexFamilyAnalyzer", executionSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void CompilerFailuresAreRecordedWithoutThreadStaticSideChannels()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var linearSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Execution", "Utf8ByteSafeLinearVerifierProgram.cs"));
        var dfaSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Execution", "Utf8ByteSafeLazyDfaVerifierProgram.cs"));

        Assert.DoesNotContain("[ThreadStatic]", linearSource, StringComparison.Ordinal);
        Assert.DoesNotContain("[ThreadStatic]", dfaSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCompileFailureKind", linearSource, StringComparison.Ordinal);
        Assert.DoesNotContain("GetCompileFailureKind", dfaSource, StringComparison.Ordinal);
        Assert.Contains("Utf8ByteSafeLinearCompileOutcome", linearSource, StringComparison.Ordinal);
        Assert.Contains("Utf8ByteSafeLazyDfaCompileOutcome", dfaSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SpeculativePreparationDeclinesOversizedProductsBeforeAllocation()
    {
        var budget = new Utf8PreparationBudget(4096, 256);
        Assert.False(budget.TryReserveProduct(int.MaxValue, 2, 256, out _));

        const string pattern = "(?:a|b)(?:c|d)(?:e|f)(?:g|h)(?:i|j)(?:k|l)(?:m|n)(?:o|p)(?:q|r)";
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes("xxacegikmoqyy");

        Assert.NotEqual(NativeExecutionKind.ExactUtf8Literals, prepared.ExecutionKind);
        Assert.Equal(Regex.IsMatch(Encoding.UTF8.GetString(input), pattern), regex.IsMatch(input));
    }

    [Fact]
    public void LongFlatLiteralPreparationRemainsACompactExactLiteral()
    {
        var pattern = new string('a', 16_384);
        var prepared = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactAsciiLiteral, prepared.ExecutionKind);
        Assert.Equal(pattern.Length, prepared.LiteralUtf8!.Length);
    }

    private static string FindCoreSourceDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Lokad.Utf8Regex source directory.");
    }
}
