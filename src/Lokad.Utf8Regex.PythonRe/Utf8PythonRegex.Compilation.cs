using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonRegex
{
    /// <summary>Compiles text eagerly under one immutable policy and an independent construction token.</summary>
    /// <remarks>Pre-cancellation wins before policy/flags/pattern work. Construction completes the full-match program before publication, independently of later match deadlines. Funded instances own program/cache admission until explicit close and their last active call/cursor. An explicit compilation cannot replace an active construction owner.</remarks>
    public static Utf8PythonRegex Compile(string pattern, PythonReCompileOptions options = PythonReCompileOptions.None,
        Utf8RegexPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new Utf8ConstructionPattern(pattern), options, policy, cancellationToken, eager: true);
    }

    /// <summary>Compiles strict UTF-8 pattern bytes eagerly under one immutable policy.</summary>
    /// <remarks>Source validation/copy, eager preparation and publication use the same construction admission/token/deadline as text compilation. Match operations retain independent invocation controls and consume already-valid subjects. No subject-validation policy is introduced.</remarks>
    public static Utf8PythonRegex Compile(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options = PythonReCompileOptions.None,
        Utf8RegexPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new Utf8ConstructionPattern(patternUtf8), options, policy, cancellationToken, eager: true);
    }
}
