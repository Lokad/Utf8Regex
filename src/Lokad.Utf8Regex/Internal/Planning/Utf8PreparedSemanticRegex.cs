using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Planning;

// Retained semantic metadata after lowering. The parse tree and its culture
// are construction inputs; prepared operations borrow the shared capture catalog
// directly and use their own lowered programs and case behavior.
internal readonly struct Utf8PreparedSemanticRegex
{
    internal Utf8PreparedSemanticRegex(string? pattern, RegexOptions options,
        string? executionPattern, RegexOptions executionOptions, Utf8CaptureCatalog? captureCatalog)
    {
        Pattern = pattern;
        Options = options;
        ExecutionPattern = executionPattern;
        ExecutionOptions = executionOptions;
        CaptureCatalog = captureCatalog;
    }

    public string? Pattern { get; }
    public RegexOptions Options { get; }
    public string? ExecutionPattern { get; }
    public RegexOptions ExecutionOptions { get; }
    public Utf8CaptureCatalog? CaptureCatalog { get; }
}
