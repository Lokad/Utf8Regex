using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReQualificationFreshnessTests
{
    [Fact]
    public void IndependentCatalogGrowthAndReorderingPreserveExistingEvidence()
    {
        var currentRows = Enumerable.Range(0, 28)
            .Select(index => Current(caseIdentity: $"case-{index}"))
            .Reverse()
            .ToArray();
        var appendedRow = Current(caseIdentity: "case-28") with { HasEvidence = false };

        Assert.All(
            currentRows,
            row => Assert.Equal(
                PythonReQualificationFreshness.Current,
                PythonReQualificationFreshnessEvaluator.Evaluate(row)));
        Assert.Equal(
            PythonReQualificationFreshness.NewCase,
            PythonReQualificationFreshnessEvaluator.Evaluate(appendedRow));
    }

    [Fact]
    public void CaseChangeStalesOnlyTheChangedCase()
    {
        var rows = new[] { Current("first"), Current("second"), Current("third") };
        rows[1] = rows[1] with { CurrentCaseDefinitionSha256 = "changed" };

        Assert.Equal(
            new[]
            {
                PythonReQualificationFreshness.Current,
                PythonReQualificationFreshness.CaseChanged,
                PythonReQualificationFreshness.Current,
            },
            rows.Select(static row => PythonReQualificationFreshnessEvaluator.Evaluate(row)));
    }

    [Fact]
    public void OperationChangeStalesOnlyDependentCases()
    {
        var rows = new[]
        {
            Current("search-a", managedOperationIdentity: "search"),
            Current("search-b", managedOperationIdentity: "search"),
            Current("count", managedOperationIdentity: "count"),
        };
        rows[0] = rows[0] with { CurrentManagedOperationProtocolSha256 = "search-v2" };
        rows[1] = rows[1] with { CurrentManagedOperationProtocolSha256 = "search-v2" };

        Assert.Equal(
            new[]
            {
                PythonReQualificationFreshness.ManagedOperationProtocolChanged,
                PythonReQualificationFreshness.ManagedOperationProtocolChanged,
                PythonReQualificationFreshness.Current,
            },
            rows.Select(static row => PythonReQualificationFreshnessEvaluator.Evaluate(row)));
    }

    [Theory]
    [InlineData("managed-product", "ManagedProductChanged")]
    [InlineData("cpython-operation", "CpythonOperationProtocolChanged")]
    [InlineData("shared", "SharedProtocolChanged")]
    [InlineData("runtime", "RuntimeChanged")]
    [InlineData("interpreter", "InterpreterChanged")]
    public void SharedDependencyChangesHaveExplicitReasons(
        string dependency,
        string expectedName)
    {
        var expected = Enum.Parse<PythonReQualificationFreshness>(expectedName);
        var input = Current("case") with
        {
            CurrentManagedProductSha256 = dependency == "managed-product" ? "changed" : "product",
            CurrentCpythonOperationProtocolSha256 =
                dependency == "cpython-operation" ? "changed" : "cpython-operation",
            CurrentSharedProtocolSha256 = dependency == "shared" ? "changed" : "shared",
            RuntimeMatches = dependency != "runtime",
            InterpreterMatches = dependency != "interpreter",
        };

        Assert.Equal(expected, PythonReQualificationFreshnessEvaluator.Evaluate(input));
        Assert.False(string.IsNullOrWhiteSpace(PythonReQualificationFreshnessEvaluator.Format(expected)));
    }

    private static PythonReQualificationFreshnessInput Current(
        string caseIdentity,
        string managedOperationIdentity = "managed-operation") => new(
        HasEvidence: true,
        StoredCaseDefinitionSha256: caseIdentity,
        CurrentCaseDefinitionSha256: caseIdentity,
        StoredManagedProductSha256: "product",
        CurrentManagedProductSha256: "product",
        StoredManagedOperationProtocolSha256: managedOperationIdentity,
        CurrentManagedOperationProtocolSha256: managedOperationIdentity,
        StoredCpythonOperationProtocolSha256: "cpython-operation",
        CurrentCpythonOperationProtocolSha256: "cpython-operation",
        StoredSharedProtocolSha256: "shared",
        CurrentSharedProtocolSha256: "shared",
        ManagedMetadataMatches: true,
        RuntimeMatches: true,
        InterpreterMatches: true);
}
