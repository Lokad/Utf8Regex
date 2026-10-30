using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex;

/// <inheritdoc />
public sealed partial class Utf8Regex
{
    /// <summary>Compiles a reusable program under caller-owned storage admission.</summary>
    /// <remarks>Reservations precede substantial compilation acquisition. The returned owner keeps compilation funded until explicit close and the last active call; it does not expose the underlying expression. Matching uses independent cancellation/deadlines and can use a separate working allowance. Null MaxBytes adds no private construction-byte cap; work/depth/instruction backstops remain.</remarks>
    public static Utf8RegexProgramOwner Compile(string pattern, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(programMemoryAllowance);
        constructionPolicy.Validate(nameof(constructionPolicy));
        resourcePolicy.Validate(nameof(resourcePolicy));
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(constructionTimeout));
        using var construction = Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout,
            cancellationToken, programMemoryAllowance);
        try
        {
            var regex = new Utf8Regex(pattern, options, matchTimeout, resourcePolicy);
            var storage = construction.Budget.AllocateObject(programMemoryAllowance,
                static allowance => new Utf8FundedStorage(allowance, nameof(Utf8RegexProgramOwner)));
            var result = construction.Budget.AllocateObject((Regex: regex, Storage: storage),
                static state => new Utf8RegexProgramOwner(state.Regex, state.Storage));
            construction.Budget.ChargeWork(0);
            storage.Retain(construction.Budget.TransferFundedBytes());
            construction.Complete();
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new RegexMatchTimeoutException(string.Empty, pattern, constructionTimeout); }
    }

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
