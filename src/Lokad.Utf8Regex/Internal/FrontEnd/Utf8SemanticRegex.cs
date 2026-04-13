namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal readonly struct Utf8SemanticRegex
{
    public Utf8SemanticRegex(
        string pattern,
        RegexOptions options,
        Utf8SemanticSource source,
        string executionPattern,
        RegexOptions executionOptions,
        RuntimeFrontEnd.RegexTree? runtimeTree = null,
        RuntimeFrontEnd.AnalysisResults? runtimeAnalysis = null)
    {
        Pattern = pattern;
        Options = options;
        Source = source;
        ExecutionPattern = executionPattern;
        ExecutionOptions = executionOptions;
        RuntimeTree = runtimeTree;
        RuntimeAnalysis = runtimeAnalysis;
    }

    public string Pattern { get; }

    public RegexOptions Options { get; }

    public Utf8SemanticSource Source { get; }

    public string ExecutionPattern { get; }

    public RegexOptions ExecutionOptions { get; }

    public RuntimeFrontEnd.RegexTree? RuntimeTree { get; }

    public RuntimeFrontEnd.AnalysisResults? RuntimeAnalysis { get; }
}
