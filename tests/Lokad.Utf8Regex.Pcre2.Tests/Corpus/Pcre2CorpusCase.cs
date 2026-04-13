namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public sealed class Pcre2CorpusCase
{
    public required string Id { get; init; }

    public string Pattern { get; init; } = string.Empty;

    public string? PatternRef { get; init; }

    public string PatternEncoding { get; init; } = "Utf16Text";

    public string[] CompileOptions { get; init; } = [];

    public Pcre2CorpusCompileSettings CompileSettings { get; init; } = new();

    public string[] MatchOptions { get; init; } = [];

    public required string InputText { get; init; }

    public int StartOffsetInBytes { get; init; }

    public string? ReplacementPattern { get; init; }

    public string[] SubstitutionOptions { get; init; } = [];

    public string PartialMode { get; init; } = "None";

    public required Pcre2CorpusOperationKind Operation { get; init; }

    public required Pcre2CorpusExpectedResult Expected { get; init; }

    public required Pcre2CorpusCaseStatus Status { get; init; }

    public string[] Tags { get; init; } = [];

    public required string Source { get; init; }

    public string? Notes { get; init; }
}

public sealed class Pcre2CorpusCompileSettings
{
    public bool AllowDuplicateNames { get; init; }

    public string BackslashC { get; init; } = "Forbid";

    public bool AllowLookaroundBackslashK { get; init; }

    public string Newline { get; init; } = "Default";

    public string Bsr { get; init; } = "Default";
}

public sealed class Pcre2CorpusExpectedResult
{
    public required Pcre2CorpusOutcomeKind Outcome { get; init; }

    public int? MatchCount { get; init; }

    public int? StartOffsetInBytes { get; init; }

    public int? EndOffsetInBytes { get; init; }

    public bool? HasContiguousByteRange { get; init; }

    public string? ValueText { get; init; }

    public string? Mark { get; init; }

    public string? ErrorKind { get; init; }

    public string? ReplacementText { get; init; }

    public Pcre2CorpusExpectedGroup[] Groups { get; init; } = [];

    public Pcre2CorpusExpectedNameEntry[] NameEntries { get; init; } = [];

    public Pcre2CorpusExpectedMatch[] Matches { get; init; } = [];
}

public sealed class Pcre2CorpusExpectedGroup
{
    public required int Number { get; init; }

    public required bool Success { get; init; }

    public int? StartOffsetInBytes { get; init; }

    public int? EndOffsetInBytes { get; init; }

    public string? ValueText { get; init; }
}

public sealed class Pcre2CorpusExpectedNameEntry
{
    public required string Name { get; init; }

    public required int Number { get; init; }
}

public sealed class Pcre2CorpusExpectedMatch
{
    public required bool Success { get; init; }

    public required int StartOffsetInBytes { get; init; }

    public required int EndOffsetInBytes { get; init; }

    public required string ValueText { get; init; }
}
