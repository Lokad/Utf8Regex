using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class CoreCompilationArchitectureTests
{
    [Fact]
    public void CompilationReusesPreparedInstructionStorage()
    {
        var analysis = Utf8FrontEnd.Analyze("ab[0-9]{2}", RegexOptions.CultureInvariant);
        var prepared = Utf8RegexPreparer.Prepare(analysis);

        Assert.IsType<Utf8RegexAnalysis>(analysis);
        Assert.IsType<Utf8PreparedRegex>(prepared);
        // Repeated access borrows the same instruction storage; this is a
        // reference-stability check, not a complete immutability proof.
        Assert.Same(prepared.StructuralLinearProgram.InstructionProgram.Instructions, prepared.StructuralLinearProgram.InstructionProgram.Instructions);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, prepared.ExecutionKind);
    }

    [Fact]
    [Trait("Category", "SourceOnly")]
    public void PreparedRegexConstructionIsInertAndLayerOwnersStayDirectional()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var preparedSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Planning", "Utf8PreparedRegex.cs"));
        Assert.DoesNotContain(".Create(", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".Select(", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new Regex(", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Executor.", preparedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("RuntimeFactory", preparedSource, StringComparison.Ordinal);

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

    [Fact]
    [Trait("Category", "SourceOnly")]
    public void ByteOffsetExecutionUsesNeutralInputOwnedInfrastructure()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var inputSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Input", "Utf8ValidatedInput.cs"));
        var mapSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Input", "Utf8BoundaryMap.cs"));

        Assert.DoesNotContain("bool[]", mapSource, StringComparison.Ordinal);
        Assert.Contains("Utf8ProjectionCursor", inputSource, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "SourceOnly")]
    public void SimplePatternLoweringDoesNotDependOnSearchPlans()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var simpleLowerer = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "FrontEnd", "Utf8AsciiSimplePatternLowerer.cs"));

        Assert.DoesNotContain("Utf8SearchPlan", simpleLowerer, StringComparison.Ordinal);
    }

    [Fact]
    [Trait("Category", "SourceOnly")]
    public void ReusableSearchKernelsDoNotDependOnFlavorFrontEnds()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var searchRoot = Path.Combine(sourceRoot, "Internal", "Search");

        foreach (var searchSourcePath in Directory.EnumerateFiles(searchRoot, "*.cs", SearchOption.AllDirectories))
        {
            var searchSource = File.ReadAllText(searchSourcePath);
            Assert.DoesNotContain("Internal.FrontEnd", searchSource, StringComparison.Ordinal);
            Assert.DoesNotContain("RegexCharClass", searchSource, StringComparison.Ordinal);
        }
    }

    [Fact]
    [Trait("Category", "SourceOnly")]
    public void ReplacementDependsOnExecutionInOneDirectionOnly()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var executionRoot = Path.Combine(sourceRoot, "Internal", "Execution");
        foreach (var path in Directory.EnumerateFiles(executionRoot, "*.cs", SearchOption.AllDirectories))
        {
            Assert.DoesNotContain("Internal.Replacement", File.ReadAllText(path), StringComparison.Ordinal);
        }

        var replacementRoot = Path.Combine(sourceRoot, "Internal", "Replacement");
        Assert.Contains(
            Directory.EnumerateFiles(replacementRoot, "*.cs", SearchOption.AllDirectories),
            path => File.ReadAllText(path).Contains("Internal.Execution", StringComparison.Ordinal));
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
