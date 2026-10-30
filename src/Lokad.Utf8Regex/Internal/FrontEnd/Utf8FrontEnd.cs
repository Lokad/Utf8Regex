using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>
/// Owns the directional core compilation flow: parse, analyze, then prepare.
/// Subject bytes and invocation runtimes are outside this layer.
/// </summary>
internal static class Utf8FrontEnd
{
    public static Utf8PreparedRegex Compile(string pattern, RegexOptions options)
        => Compile(pattern, options, options);

    internal static Utf8PreparedRegex Compile(string pattern, RegexOptions options, RegexOptions scalarOptions)
        => Compile(new Utf8ConstructionPattern(pattern), options, scalarOptions);

    internal static Utf8PreparedRegex Compile(Utf8ConstructionPattern pattern, RegexOptions options, RegexOptions scalarOptions)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var analysis = Analyze(pattern, options);
        var result = Utf8RegexPreparer.Prepare(analysis, scalarOptions);
        owner.Complete();
        return result;
    }

    internal static Utf8RegexAnalysis Analyze(string pattern, RegexOptions options)
        => Analyze(new Utf8ConstructionPattern(pattern), options);

    private static Utf8RegexAnalysis Analyze(Utf8ConstructionPattern pattern, RegexOptions options)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var effectiveOptions = options;
        string? executionPattern = null;
        if (pattern.IsUtf8) _ = Utf8OwnedOptionPrelude.ConsumeLeadingGlobalOptions(pattern, ref effectiveOptions);
        else executionPattern = Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(pattern.Text, ref effectiveOptions);
        Utf8PatternDiagnostic diagnostic;
        var runtimeTree = pattern.IsUtf8 ? Utf8OwnedTreeParser.TryParse(pattern.Utf8, options, out diagnostic)
            : Utf8OwnedTreeParser.TryParse(pattern.Text, options, out diagnostic);
        if (diagnostic.IsSyntaxError) throw diagnostic.ToException();
        if ((options & RegexOptions.NonBacktracking) != 0 && runtimeTree is not null &&
            Utf8NonBacktrackingGrammar.FindUnsupportedConstruct(runtimeTree.Root) is { } unsupported)
            throw new NotSupportedException($"NonBacktracking does not support {unsupported}.");
        var semanticRegex = new Utf8SemanticRegex(
            pattern.IsUtf8 ? null : pattern.Text,
            options,
            executionPattern,
            effectiveOptions,
            runtimeTree);
        var analysis = Utf8FrontEndAnalyzer.Analyze(semanticRegex, executionPattern, effectiveOptions);
        owner.Complete();
        return analysis;
    }
}
