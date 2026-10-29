using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

/// <inheritdoc />
public sealed partial class Utf8PythonRegex
{
    /// <summary>Compiles pattern text under explicit eager construction and execution policies.</summary>
    /// <remarks>Construction admission and interruption cover eager compilation,
    /// including the derived FullMatch program, independently of later invocation
    /// limits. Replacement preparation, CLR/JIT metadata and caller retention
    /// retain separate scope. FullMatch preparation is eager even if unused.
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
    /// timeout. The derived FullMatch program is prepared before publication;
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
        CancellationToken cancellationToken, IUtf8MemoryAllowance? programMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        constructionPolicy.Validate(nameof(constructionPolicy));
        resourcePolicy.Validate(nameof(resourcePolicy));
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(constructionTimeout));
        using var construction = programMemoryAllowance is null
            ? Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken)
            : Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance);
        try
        {
            var result = new Utf8PythonRegex(pattern, options, matchTimeout, resourcePolicy, checkPolicy: true);
            // Explicit compilation finishes both operation programs under this
            // one owner. Do not retain its policy/token for later invocations.
            _ = result._lazyScalarFullProgram.Value;
            if (programMemoryAllowance is not null)
            {
                var owner = construction.Budget.AllocateObject(programMemoryAllowance,
                    static allowance => new Utf8FundedStorage(allowance, nameof(Utf8PythonRegex)));
                construction.Budget.ChargeWork(0);
                result._replacementPlanCache.EnableFunding(programMemoryAllowance);
                owner.Retain(construction.Budget.TransferFundedBytes());
                result._programMemory = owner;
            }
            else construction.Budget.ChargeWork(0);
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

    /// <summary>Compiles and retains an eager program under caller-owned construction/program admission.</summary>
    /// <remarks>Dispose the expression to close new uses and refund after existing operations/cursors finish. The shared allowance covers modeled construction, a conservative completed-program charge and replacement preparation/retention, not construction peak. Null MaxBytes imposes no additional construction-byte cap on this overload; work/depth/instruction backstops remain. Working/result and caller retention keep their separately documented scopes.</remarks>
    public static Utf8PythonRegex Compile(string pattern, PythonReCompileOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(programMemoryAllowance);
        return Compile(new Utf8ConstructionPattern(pattern), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance);
    }

    /// <summary>Compiles strict UTF-8 pattern bytes with caller-owned construction/program funding.</summary>
    /// <remarks>Admission precedes decoding, parsing and eager FullMatch preparation. Replacement preparation/cache retention uses the same account. Dispose closes matching use; existing snapshots remain valid. Working/result and caller retention are separate scopes.</remarks>
    public static Utf8PythonRegex Compile(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options,
        TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout,
        CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(programMemoryAllowance);
        return Compile(new Utf8ConstructionPattern(patternUtf8), options, matchTimeout, resourcePolicy,
            constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance);
    }
}
