using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>
/// Owns the directional core compilation flow: parse, analyze, then prepare.
/// Subject bytes and invocation runtimes are outside this layer.
/// </summary>
internal static class Utf8FrontEnd
{
    public static Utf8PreparedRegex Compile(string pattern, RegexOptions options)
    {
        var analysis = Analyze(pattern, options);
        return Utf8RegexPreparer.Prepare(analysis);
    }

    internal static Utf8RegexAnalysis Analyze(string pattern, RegexOptions options)
    {
        var effectiveOptions = options;
        var executionPattern = Runtime.RegexParser.NormalizeLeadingGlobalOptions(pattern, ref effectiveOptions);
        var runtimeTree = Utf8RuntimeTreeProvider.TryParse(pattern, options);
        var runtimeAnalysis = runtimeTree is null ? null : Runtime.RegexTreeAnalyzer.Analyze(runtimeTree);
        var semanticRegex = new Utf8SemanticRegex(
            pattern,
            options,
            executionPattern,
            effectiveOptions,
            runtimeTree,
            runtimeAnalysis);
        var analysis = Utf8FrontEndAnalyzer.Analyze(semanticRegex, executionPattern, effectiveOptions);
        return analysis;
    }
}
