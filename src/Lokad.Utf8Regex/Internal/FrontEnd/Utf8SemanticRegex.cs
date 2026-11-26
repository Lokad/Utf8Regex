namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly struct Utf8SemanticRegex
{
    public Utf8SemanticRegex(
        string? pattern,
        RegexOptions options,
        int executionPatternOffset,
        RegexOptions executionOptions,
        Utf8OwnedRegexTree? runtimeTree)
    {
        Pattern = pattern;
        Options = options;
        ExecutionPatternOffset = executionPatternOffset;
        ExecutionOptions = executionOptions;
        RuntimeTree = runtimeTree;
    }

    public string? Pattern { get; }

    public RegexOptions Options { get; }

    public int ExecutionPatternOffset { get; }
    public ReadOnlySpan<char> ExecutionPattern => Pattern.AsSpan(ExecutionPatternOffset);

    public RegexOptions ExecutionOptions { get; }

    public Utf8OwnedRegexTree? RuntimeTree { get; }

}
