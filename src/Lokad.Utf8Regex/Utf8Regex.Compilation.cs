using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    /// <summary>Compiles a pattern under explicit eager construction and execution policies.</summary>
    /// <remarks>The construction timeout starts at entry and applies only to eager
    /// compilation. Covered construction limits may tighten fixed backstops;
    /// lazy preparation and CLR/JIT metadata retain their separate backstops.
    /// The returned expression uses the independent match timeout and invocation
    /// policy. No static pattern cache is consulted.</remarks>
    /// <param name="pattern">Pattern text; matching remains scalar-based over UTF-8.</param>
    /// <param name="options">Core compile options.</param>
    /// <param name="matchTimeout">Timeout for each later execution invocation.</param>
    /// <param name="resourcePolicy">Resource limits for each later invocation.</param>
    /// <param name="constructionPolicy">Covered eager construction admission limits.</param>
    /// <param name="constructionTimeout">Eager compilation timeout, or infinite.</param>
    /// <param name="cancellationToken">Cancellation for eager compilation only.</param>
    /// <returns>The independently reusable expression.</returns>
    public static Utf8Regex Compile(string pattern, RegexOptions options,
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
            var result = new Utf8Regex(pattern, options, matchTimeout, resourcePolicy);
            construction.Budget.ChargeWork(0); // Check before publication, even after a short route.
            construction.Complete();
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(string.Empty, pattern, constructionTimeout);
        }
    }
}
