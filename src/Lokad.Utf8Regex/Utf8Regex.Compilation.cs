using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex;

/// <inheritdoc />
public sealed partial class Utf8Regex
{
    /// <summary>Compiles a strict UTF-8 pattern with default options and subject validation.</summary>
    public static Utf8Regex Compile(ReadOnlySpan<byte> patternUtf8)
        => Compile(patternUtf8, RegexOptions.None, inputValidation: Utf8InputValidation.Validate);

    /// <summary>Compiles with default strict subject validation.</summary>
    public static Utf8Regex Compile(ReadOnlySpan<byte> patternUtf8, RegexOptions options = RegexOptions.None)
        => Compile(patternUtf8, options, inputValidation: Utf8InputValidation.Validate);

    /// <summary>Compiles with default strict subject validation.</summary>
    public static Utf8Regex Compile(ReadOnlySpan<byte> patternUtf8, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout, CancellationToken cancellationToken)
        => Compile(patternUtf8, options, matchTimeout, resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken, inputValidation: Utf8InputValidation.Validate);

    /// <summary>Compiles with default strict subject validation.</summary>
    public static Utf8RegexProgramOwner Compile(ReadOnlySpan<byte> patternUtf8, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance)
        => Compile(patternUtf8, options, matchTimeout, resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance, inputValidation: Utf8InputValidation.Validate);

    /// <summary>Compiles with default strict subject validation.</summary>
    public static Utf8RegexProgramOwner Compile(string pattern, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance)
        => Compile(pattern, options, matchTimeout, resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance, inputValidation: Utf8InputValidation.Validate);

    /// <summary>Compiles with default strict subject validation.</summary>
    public static Utf8Regex Compile(string pattern, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken)
        => Compile(pattern, options, matchTimeout, resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken, inputValidation: Utf8InputValidation.Validate);


    // Adapter construction already owns its synchronous scope. Reuse it rather
    // than entering a second public policy/token owner through Compile.
    internal Utf8Regex(ReadOnlySpan<byte> patternUtf8, RegexOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
        : this(new Utf8ConstructionPattern(patternUtf8), options, matchTimeout, resourcePolicy, true, inputValidation) { }

    /// <summary>Compiles a strict UTF-8 pattern directly with the owned scalar grammar.</summary>
    /// <remarks>The span is borrowed only during this call. Retained source and literal bytes are owned by the expression. Pattern text is projected only by explicit string interoperability or diagnostics.</remarks>
    public static Utf8Regex Compile(ReadOnlySpan<byte> patternUtf8, RegexOptions options = RegexOptions.None,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
        => Compile(patternUtf8, options, DefaultMatchTimeout, default, default, Timeout.InfiniteTimeSpan, default, inputValidation: inputValidation);

    /// <summary>Compiles strict UTF-8 under independent construction and execution controls.</summary>
    /// <remarks>Syntax errors name UTF-8 byte offsets. A construction timeout reports an empty exception Pattern to avoid decoding after expiry.</remarks>
    public static Utf8Regex Compile(ReadOnlySpan<byte> patternUtf8, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout, CancellationToken cancellationToken,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
        => CompileCore(new Utf8ConstructionPattern(patternUtf8), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken, inputValidation: inputValidation);

    /// <summary>Compiles strict UTF-8 under caller-owned program storage admission.</summary>
    /// <remarks>Retained source is copied before publication; subsequent input-buffer reuse cannot change the program. Close and active cursor lifetimes follow the same funded program contract as text compilation.</remarks>
    public static Utf8RegexProgramOwner Compile(ReadOnlySpan<byte> patternUtf8, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
        => CompileFunded(new Utf8ConstructionPattern(patternUtf8), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance, inputValidation: inputValidation);

    /// <summary>Compiles a reusable program under caller-owned storage admission.</summary>
    /// <remarks>Reservations precede substantial compilation acquisition. The returned owner keeps compilation funded until explicit close and the last active call; it does not expose the underlying expression. Matching uses independent cancellation/deadlines and can use a separate working allowance. Null MaxBytes adds no private construction-byte cap; work/depth/instruction backstops remain.</remarks>
    public static Utf8RegexProgramOwner Compile(string pattern, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
        => CompileFunded(new Utf8ConstructionPattern(pattern), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance, inputValidation: inputValidation);

    private static Utf8RegexProgramOwner CompileFunded(Utf8ConstructionPattern pattern, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
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
            var regex = new Utf8Regex(pattern, options, matchTimeout, resourcePolicy, true, inputValidation);
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
        { throw new RegexMatchTimeoutException(string.Empty, pattern.Text, constructionTimeout); }
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
    /// <param name="inputValidation">Immutable subject validation policy; pattern and replacement validation remain independent.</param>
    /// <returns>The independently reusable expression.</returns>
    public static Utf8Regex Compile(string pattern, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
        => CompileCore(new Utf8ConstructionPattern(pattern), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken, inputValidation: inputValidation);

    private static Utf8Regex CompileCore(Utf8ConstructionPattern pattern, RegexOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken,
        Utf8InputValidation inputValidation = Utf8InputValidation.Validate)
    {
        cancellationToken.ThrowIfCancellationRequested();
        constructionPolicy.Validate(nameof(constructionPolicy));
        resourcePolicy.Validate(nameof(resourcePolicy));
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(constructionTimeout));
        using var construction = Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken);
        try
        {
            var result = new Utf8Regex(pattern, options, matchTimeout, resourcePolicy, true, inputValidation);
            construction.Budget.ChargeWork(0); // Check before publication, even after a short route.
            construction.Complete();
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new RegexMatchTimeoutException(string.Empty, pattern.Text, constructionTimeout);
        }
    }
}
