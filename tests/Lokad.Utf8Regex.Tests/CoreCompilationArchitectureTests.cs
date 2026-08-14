using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using System.Reflection;
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

    [Fact]
    public void ByteOffsetExecutionUsesNeutralInputOwnedInfrastructure()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var facadeSource = File.ReadAllText(Path.Combine(sourceRoot, "Utf8Regex.cs"));
        var inputSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Input", "Utf8ValidatedInput.cs"));
        var mapSource = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "Input", "Utf8BoundaryMap.cs"));

        Assert.DoesNotContain("Pcre2CountAtByteOffset", facadeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Pcre2EnumerateMatchesAtByteOffset", facadeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Utf8Utf16BoundaryResolver", facadeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("bool[]", mapSource, StringComparison.Ordinal);
        Assert.Contains("Utf8ProjectionCursor", inputSource, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(sourceRoot, "Internal", "Execution", "Utf8PreparedValueMatchEnumerator.cs")));
        Assert.False(File.Exists(Path.Combine(sourceRoot, "Utf8PreparedValueMatchEnumerator.cs")));
        Assert.False(File.Exists(Path.Combine(sourceRoot, "Internal", "Input", "Utf8Utf16BoundaryResolver.cs")));
    }

    [Fact]
    public void SearchPreparationUsesOneFactsModelAndOneOperationDisposition()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var planningRoot = Path.Combine(sourceRoot, "Internal", "Planning");
        var simpleLowerer = File.ReadAllText(Path.Combine(sourceRoot, "Internal", "FrontEnd", "Utf8AsciiSimplePatternLowerer.cs"));

        Assert.True(File.Exists(Path.Combine(planningRoot, "Utf8SearchFacts.cs")));
        Assert.True(File.Exists(Path.Combine(planningRoot, "Utf8CandidateSearchPlan.cs")));
        Assert.True(File.Exists(Path.Combine(planningRoot, "Utf8SearchOperationPlan.cs")));
        Assert.False(File.Exists(Path.Combine(planningRoot, "Utf8NativeSearchPlan.cs")));
        Assert.False(File.Exists(Path.Combine(planningRoot, "Utf8SearchEnginePlan.cs")));
        Assert.False(File.Exists(Path.Combine(planningRoot, "Utf8SearchMetaStrategyPlan.cs")));
        Assert.False(File.Exists(Path.Combine(planningRoot, "Utf8ExecutablePipelinePlan.cs")));
        Assert.False(File.Exists(Path.Combine(planningRoot, "Utf8BackendInstructionProgram.cs")));
        Assert.DoesNotContain("Utf8SearchPlan", simpleLowerer, StringComparison.Ordinal);
    }

    [Fact]
    public void ReusableSearchKernelsHaveOneOwnerAndNoFlavorFrontEndDependency()
    {
        var sourceRoot = FindCoreSourceDirectory();
        var searchRoot = Path.Combine(sourceRoot, "Internal", "Search");
        var utilitiesRoot = Path.Combine(sourceRoot, "Internal", "Utilities");
        var structuralRuntime = File.ReadAllText(
            Path.Combine(sourceRoot, "Internal", "Execution", "Utf8StructuralLinearProgram.cs"));

        Assert.True(Directory.Exists(searchRoot));
        Assert.Empty(Directory.EnumerateFiles(utilitiesRoot, "*.cs", SearchOption.AllDirectories));
        Assert.True(File.Exists(Path.Combine(searchRoot, "PreparedSearcher.cs")));
        Assert.True(File.Exists(Path.Combine(searchRoot, "PreparedWindowSearch.cs")));
        Assert.True(File.Exists(Path.Combine(searchRoot, "Utf8LiteralEquality.cs")));
        Assert.True(File.Exists(Path.Combine(searchRoot, "Utf8SearchKernel.cs")));

        foreach (var searchSourcePath in Directory.EnumerateFiles(searchRoot, "*.cs", SearchOption.AllDirectories))
        {
            var searchSource = File.ReadAllText(searchSourcePath);
            Assert.DoesNotContain("Internal.FrontEnd", searchSource, StringComparison.Ordinal);
            Assert.DoesNotContain("RegexCharClass", searchSource, StringComparison.Ordinal);
        }

        Assert.Contains("AsciiOrderedLiteralWindowExecutor.FindNext", structuralRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("FindNextOrderedLiteralFamilyWindow", structuralRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("FindNextPairedOrderedLiteralFamilyWindow", structuralRuntime, StringComparison.Ordinal);
    }

    [Fact]
    public void OwnedInternalContractsUseMandatoryParametersAndNoNullSuppression()
    {
        var assembly = typeof(Utf8Regex).Assembly;
        const BindingFlags declaredMembers = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var optionalParameters = assembly.GetTypes()
            .Where(static type =>
                type.Namespace?.StartsWith("Lokad.Utf8Regex.Internal", StringComparison.Ordinal) == true &&
                !type.Namespace.StartsWith("Lokad.Utf8Regex.Internal.FrontEnd.Runtime", StringComparison.Ordinal))
            .SelectMany(type =>
                type.GetMethods(declaredMembers).Cast<MethodBase>()
                    .Concat(type.GetConstructors(declaredMembers)))
            .SelectMany(method => method.GetParameters()
                .Where(static parameter => parameter.IsOptional || parameter.HasDefaultValue)
                .Select(parameter => $"{method.DeclaringType?.FullName}.{method.Name}({parameter.Name})"))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.True(optionalParameters.Length == 0, string.Join(Environment.NewLine, optionalParameters));

        var sourceRoot = FindCoreSourceDirectory();
        var copiedRuntimeRoot = Path.Combine(sourceRoot, "Internal", "FrontEnd", "Runtime") + Path.DirectorySeparatorChar;
        var nullSuppression = new Regex(@"(?<=[A-Za-z0-9_\)\]])!(?=[\.,;\)\]\[])", RegexOptions.CultureInvariant);
        var suppressions = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.StartsWith(copiedRuntimeRoot, StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => File.ReadLines(path)
                .Select((line, lineNumber) => (path, line, lineNumber))
                .Where(item => nullSuppression.IsMatch(item.line)))
            .Select(item => $"{item.path}:{item.lineNumber + 1}")
            .ToArray();
        Assert.Empty(suppressions);
    }

    [Fact]
    public void SearchAndPlanningContractsDoNotExposeTupleDomains()
    {
        const BindingFlags declaredMembers = BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        var offenders = typeof(Utf8Regex).Assembly.GetTypes()
            .Where(static type =>
                type.Namespace is "Lokad.Utf8Regex.Internal.Planning" or "Lokad.Utf8Regex.Internal.Search")
            .SelectMany(type =>
                type.GetFields(declaredMembers).Select(field => (Member: field.Name, Type: field.FieldType))
                    .Concat(type.GetProperties(declaredMembers).Select(property => (Member: property.Name, Type: property.PropertyType)))
                    .Concat(type.GetMethods(declaredMembers).Select(method => (Member: method.Name, Type: method.ReturnType)))
                    .Concat(type.GetMethods(declaredMembers).SelectMany(method =>
                        method.GetParameters().Select(parameter => (Member: $"{method.Name}({parameter.Name})", Type: parameter.ParameterType))))
                    .Where(item => ContainsTuple(item.Type))
                    .Select(item => $"{type.FullName}.{item.Member}: {item.Type}"))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(offenders);
    }

    [Fact]
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

    private static bool ContainsTuple(Type type)
    {
        if (type.IsGenericType && type.GetGenericTypeDefinition().FullName?.StartsWith("System.ValueTuple`", StringComparison.Ordinal) == true)
        {
            return true;
        }

        return type.GetElementType() is { } elementType && ContainsTuple(elementType) ||
            type.IsGenericType && type.GetGenericArguments().Any(ContainsTuple);
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
