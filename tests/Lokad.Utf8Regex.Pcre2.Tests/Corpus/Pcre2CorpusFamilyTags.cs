namespace Lokad.Utf8Regex.Pcre2.Tests.Corpus;

public static class Pcre2CorpusFamilyTags
{
    public const string Compile = "Compile";
    public const string Match = "Match";
    public const string BranchReset = "BranchReset";
    public const string DuplicateNames = "DuplicateNames";
    public const string LookaroundK = "LookaroundK";
    public const string BackslashC = "BackslashC";
    public const string ControlVerb = "ControlVerb";
    public const string Mark = "Mark";
    public const string Partial = "Partial";
    public const string Replace = "Replace";
    public const string Probe = "Probe";
    public const string GlobalIteration = "GlobalIteration";
    public const string Recursion = "Recursion";
    public const string NamedCaptures = "NamedCaptures";

    public static IReadOnlyList<string> GetAll(Pcre2CorpusCase corpusCase)
    {
        var tags = new HashSet<string>(corpusCase.Tags, StringComparer.Ordinal);
        var pattern = corpusCase.Pattern;
        var replacement = corpusCase.ReplacementPattern ?? string.Empty;

        if (corpusCase.Operation == Pcre2CorpusOperationKind.Compile)
        {
            tags.Add(Compile);
        }

        if (corpusCase.Operation is Pcre2CorpusOperationKind.Match or Pcre2CorpusOperationKind.MatchDetailed)
        {
            tags.Add(Match);
        }

        if (pattern.Contains("(?|", StringComparison.Ordinal))
        {
            tags.Add(BranchReset);
        }

        if (corpusCase.CompileSettings.AllowDuplicateNames ||
            pattern.Contains(@"\k<", StringComparison.Ordinal) ||
            pattern.Contains(@"\k'", StringComparison.Ordinal))
        {
            if (pattern.Contains("(?<", StringComparison.Ordinal) || pattern.Contains("(?'", StringComparison.Ordinal) || corpusCase.CompileSettings.AllowDuplicateNames)
            {
                tags.Add(DuplicateNames);
            }
        }

        if (pattern.Contains(@"\K", StringComparison.Ordinal))
        {
            tags.Add(LookaroundK);
        }

        if (pattern.Contains(@"\C", StringComparison.Ordinal))
        {
            tags.Add(BackslashC);
        }

        if (pattern.Contains("(*", StringComparison.Ordinal))
        {
            tags.Add(ControlVerb);
        }

        if (pattern.Contains("(*MARK", StringComparison.Ordinal) ||
            replacement.Contains("*MARK", StringComparison.OrdinalIgnoreCase))
        {
            tags.Add(Mark);
        }

        if (corpusCase.PartialMode != "None" || corpusCase.Operation == Pcre2CorpusOperationKind.Probe)
        {
            tags.Add(Partial);
        }

        if (corpusCase.Operation == Pcre2CorpusOperationKind.Replace)
        {
            tags.Add(Replace);
        }

        if (corpusCase.Operation == Pcre2CorpusOperationKind.Probe)
        {
            tags.Add(Probe);
        }

        if (corpusCase.Operation is Pcre2CorpusOperationKind.Count or Pcre2CorpusOperationKind.EnumerateMatches)
        {
            tags.Add(GlobalIteration);
        }

        if (pattern.Contains("(?R", StringComparison.Ordinal) ||
            pattern.Contains("(?1", StringComparison.Ordinal) ||
            pattern.Contains("(?2", StringComparison.Ordinal) ||
            pattern.Contains("(?3", StringComparison.Ordinal) ||
            pattern.Contains(@"\g{", StringComparison.Ordinal) ||
            pattern.Contains("(?(DEFINE)", StringComparison.Ordinal))
        {
            tags.Add(Recursion);
        }

        if (pattern.Contains("(?<", StringComparison.Ordinal) || pattern.Contains("(?'", StringComparison.Ordinal))
        {
            tags.Add(NamedCaptures);
        }

        return tags.OrderBy(static tag => tag, StringComparer.Ordinal).ToArray();
    }

    public static bool Has(Pcre2CorpusCase corpusCase, string tag)
        => GetAll(corpusCase).Contains(tag, StringComparer.Ordinal);
}
