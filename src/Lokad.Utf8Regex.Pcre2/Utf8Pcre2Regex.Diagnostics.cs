namespace Lokad.Utf8Regex.Pcre2;

// PLAN.md D09: diagnostic and debug-projection operations, split from the matching facade along the diagnostics ownership boundary.
/// <summary>Diagnostic and debug-projection operations for <see cref="Utf8Pcre2Regex"/>.</summary>
public sealed partial class Utf8Pcre2Regex
{
    internal string DebugExecutionKindName => _program.Operations.Match.Kind.ToString();

    internal Pcre2CompiledProgram DebugCompiledProgram => _program;

    internal bool DebugUsesUtf8RegexTranslation => _program.PrimaryUtf8 is not null;


    internal string DebugUtf8RegexExecutionKindName => _program.PrimaryUtf8 is { } primary ? primary.Inspection.ExecutionKind.ToString() : "<none>";

    internal int DebugReplacementCacheEntryCount => _replacementCache.Count;

    internal string DebugDescribeExecutionPlan()
        => $"IsMatch={_program.Operations.IsMatch.Kind}, Count={_program.Operations.Count.Kind}, Enumerate={_program.Operations.Enumerate.Kind}, Match={_program.Operations.Match.Kind}, Replace={_program.Operations.Replace.Kind}";
}
