using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

/// <inheritdoc />
public sealed partial class Utf8PythonRegex
{
    /// <summary>Compiles pattern text under explicit eager construction and execution policies.</summary>
    /// <remarks>Construction admission and interruption cover eager compilation,
    /// independently of later invocation
    /// limits. Replacement preparation, CLR/JIT metadata and caller retention
    /// retain separate scope. Search, Match and FullMatch share one scalar program.
    /// Positive construction limits may tighten fixed backstops; no pattern cache
    /// is consulted. The construction token is not retained for execution.</remarks>
    public static Utf8PythonRegex Compile(string pattern, PythonReCompileOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken)
        => Compile(new Utf8ConstructionPattern(pattern), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken);

    /// <summary>Compiles strict UTF-8 pattern bytes under explicit eager construction and execution policies.</summary>
    /// <remarks>Admission and interruption include strict decoding before parsing.
    /// The construction timeout is separate from the returned expression's match
    /// timeout. One scalar program serves Search, Match and FullMatch;
    /// replacement preparation and unmodeled metadata retain separate scope.</remarks>
    public static Utf8PythonRegex Compile(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken)
        => Compile(new Utf8ConstructionPattern(patternUtf8), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken);

    private static Utf8PythonRegex Compile(Utf8ConstructionPattern pattern, PythonReCompileOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        constructionPolicy.Validate(nameof(constructionPolicy));
        resourcePolicy.Validate(nameof(resourcePolicy));
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(constructionTimeout));
        using var construction = Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken);
        try
        {
            var result = new Utf8PythonRegex(pattern, options, matchTimeout, resourcePolicy, checkPolicy: true);
            construction.Budget.ChargeWork(0);
            construction.Complete();
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            // Byte inputs may not have been decoded when interruption occurs;
            // do not allocate unbounded text merely to populate the exception.
            throw new System.Text.RegularExpressions.RegexMatchTimeoutException(string.Empty,
                pattern.IsUtf8 ? string.Empty : pattern.Text, constructionTimeout);
        }
    }
}
