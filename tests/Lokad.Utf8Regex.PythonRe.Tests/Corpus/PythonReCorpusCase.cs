namespace Lokad.Utf8Regex.PythonRe.Tests.Corpus;

public sealed class PythonReCorpusCase
{
    public required string Id { get; init; }

    public required string Pattern { get; init; }

    public string[] CompileOptions { get; init; } = [];

    public required string InputText { get; init; }

    public int StartOffsetInBytes { get; init; }

    public string? ReplacementPattern { get; init; }

    public required PythonReCorpusOperationKind Operation { get; init; }

    public required PythonReCorpusExpectedResult Expected { get; init; }

    public required PythonReCorpusCaseStatus Status { get; init; }

    public required string Source { get; init; }

    public string? Notes { get; init; }
}

public sealed class PythonReCorpusExpectedResult
{
    public required PythonReCorpusOutcomeKind Outcome { get; init; }

    public bool? Success { get; init; }

    public int? MatchCount { get; init; }

    public string? ValueText { get; init; }

    public string? ReplacementText { get; init; }

    public string? ErrorContains { get; init; }
}
