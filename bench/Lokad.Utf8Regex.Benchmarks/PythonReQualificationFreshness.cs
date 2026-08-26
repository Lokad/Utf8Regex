namespace Lokad.Utf8Regex.Benchmarks;

internal enum PythonReQualificationFreshness
{
    Current,
    NewCase,
    CaseChanged,
    ManagedProductChanged,
    ManagedOperationProtocolChanged,
    CpythonOperationProtocolChanged,
    SharedProtocolChanged,
    RuntimeChanged,
    InterpreterChanged,
}

internal readonly record struct PythonReQualificationFreshnessInput(
    bool HasEvidence,
    string StoredCaseDefinitionSha256,
    string CurrentCaseDefinitionSha256,
    string StoredManagedProductSha256,
    string CurrentManagedProductSha256,
    string StoredManagedOperationProtocolSha256,
    string CurrentManagedOperationProtocolSha256,
    string StoredCpythonOperationProtocolSha256,
    string CurrentCpythonOperationProtocolSha256,
    string StoredSharedProtocolSha256,
    string CurrentSharedProtocolSha256,
    bool ManagedMetadataMatches,
    bool RuntimeMatches,
    bool InterpreterMatches);

internal static class PythonReQualificationFreshnessEvaluator
{
    internal static PythonReQualificationFreshness Evaluate(
        in PythonReQualificationFreshnessInput input)
    {
        if (!input.HasEvidence)
        {
            return PythonReQualificationFreshness.NewCase;
        }

        if (!Equals(input.StoredCaseDefinitionSha256, input.CurrentCaseDefinitionSha256))
        {
            return PythonReQualificationFreshness.CaseChanged;
        }

        if (!Equals(input.StoredManagedProductSha256, input.CurrentManagedProductSha256) ||
            !input.ManagedMetadataMatches)
        {
            return PythonReQualificationFreshness.ManagedProductChanged;
        }

        if (!Equals(
                input.StoredManagedOperationProtocolSha256,
                input.CurrentManagedOperationProtocolSha256))
        {
            return PythonReQualificationFreshness.ManagedOperationProtocolChanged;
        }

        if (!Equals(
                input.StoredCpythonOperationProtocolSha256,
                input.CurrentCpythonOperationProtocolSha256))
        {
            return PythonReQualificationFreshness.CpythonOperationProtocolChanged;
        }

        if (!Equals(input.StoredSharedProtocolSha256, input.CurrentSharedProtocolSha256))
        {
            return PythonReQualificationFreshness.SharedProtocolChanged;
        }

        if (!input.RuntimeMatches)
        {
            return PythonReQualificationFreshness.RuntimeChanged;
        }

        return input.InterpreterMatches
            ? PythonReQualificationFreshness.Current
            : PythonReQualificationFreshness.InterpreterChanged;
    }

    internal static string Format(PythonReQualificationFreshness freshness) => freshness switch
    {
        PythonReQualificationFreshness.Current => "current",
        PythonReQualificationFreshness.NewCase => "new case",
        PythonReQualificationFreshness.CaseChanged => "case changed",
        PythonReQualificationFreshness.ManagedProductChanged => "managed product changed",
        PythonReQualificationFreshness.ManagedOperationProtocolChanged =>
            "managed operation protocol changed",
        PythonReQualificationFreshness.CpythonOperationProtocolChanged =>
            "CPython operation protocol changed",
        PythonReQualificationFreshness.SharedProtocolChanged => "shared protocol changed",
        PythonReQualificationFreshness.RuntimeChanged => "runtime changed",
        PythonReQualificationFreshness.InterpreterChanged => "interpreter changed",
        _ => throw new ArgumentOutOfRangeException(nameof(freshness)),
    };

    private static bool Equals(string left, string right) =>
        left.Equals(right, StringComparison.Ordinal);
}
