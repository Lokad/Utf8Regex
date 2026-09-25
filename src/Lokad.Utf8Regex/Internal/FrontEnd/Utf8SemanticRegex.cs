namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly struct Utf8SemanticRegex
{
    public Utf8SemanticRegex(
        string pattern,
        RegexOptions options,
        string executionPattern,
        RegexOptions executionOptions,
        Utf8OwnedRegexTree? runtimeTree,
        Utf8OwnedTreeAnalysis? runtimeAnalysis)
    {
        Pattern = pattern;
        Options = options;
        ExecutionPattern = executionPattern;
        ExecutionOptions = executionOptions;
        RuntimeTree = runtimeTree;
        RuntimeAnalysis = runtimeAnalysis;
    }

    public string Pattern { get; }

    public RegexOptions Options { get; }

    public string ExecutionPattern { get; }

    public RegexOptions ExecutionOptions { get; }

    public Utf8OwnedRegexTree? RuntimeTree { get; }

    public Utf8OwnedTreeAnalysis? RuntimeAnalysis { get; }
}
