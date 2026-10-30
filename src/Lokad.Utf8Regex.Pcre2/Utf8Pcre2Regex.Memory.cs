using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2;

public sealed partial class Utf8Pcre2Regex
{
    /// <summary>Compiles a PCRE2 text pattern under caller-owned program admission.</summary>
    /// <remarks>The returned owner keeps compilation funded until explicit close and the final active call. Null construction MaxBytes adds no private byte cap. Construction work/depth/instruction backstops and independent invocation controls remain.</remarks>
    public static Utf8Pcre2RegexProgramOwner Compile(string pattern, Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings, TimeSpan matchTimeout,
        Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance)
        => CompileFunded(new Utf8ConstructionPattern(pattern), options, compileSettings, matchTimeout,
            resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance);

    /// <summary>Compiles strict UTF-8 pattern bytes under caller-owned program admission.</summary>
    /// <remarks>The current parser still decodes pattern bytes within construction admission. Borrowed bytes are not retained; this is not direct native UTF-8 parsing. The returned owner has the text factory's lifetime/resource contract.</remarks>
    public static Utf8Pcre2RegexProgramOwner Compile(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings, TimeSpan matchTimeout,
        Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken, IUtf8MemoryAllowance programMemoryAllowance)
        => CompileFunded(new Utf8ConstructionPattern(patternUtf8), options, compileSettings, matchTimeout,
            resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance);

    private static Utf8Pcre2RegexProgramOwner CompileFunded(Utf8ConstructionPattern pattern, Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings settings, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy,
        Utf8ConstructionPolicy constructionPolicy, TimeSpan constructionTimeout, CancellationToken token,
        IUtf8MemoryAllowance programMemoryAllowance)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(programMemoryAllowance);
        constructionPolicy.Validate(nameof(constructionPolicy));
        var limits = ToExecutionLimits(resourcePolicy);
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(constructionTimeout));
        using var construction = Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, token, programMemoryAllowance);
        try
        {
            var regex = new Utf8Pcre2Regex(pattern, options, settings, limits, matchTimeout);
            var storage = construction.Budget.AllocateObject(programMemoryAllowance,
                static allowance => new Utf8FundedStorage(allowance, nameof(Utf8Pcre2RegexProgramOwner)));
            var owner = construction.Budget.AllocateObject((Regex: regex, Storage: storage),
                static state => new Utf8Pcre2RegexProgramOwner(state.Regex, state.Storage));
            construction.Budget.ChargeWork(0);
            storage.Retain(construction.Budget.TransferFundedBytes());
            construction.Complete();
            return owner;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new System.Text.RegularExpressions.RegexMatchTimeoutException(string.Empty, pattern.IsUtf8 ? string.Empty : pattern.Text, constructionTimeout); }
    }

    /// <summary>Tests for a match with separate caller-owned working admission.</summary>
    /// <remarks>Compilation and borrowed input retain separate owners. Existing PCRE2 limits and cancellation/deadline apply. Covered temporary loans refund synchronously; denial does not retry under another allowance.</remarks>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken, IUtf8MemoryAllowance workingMemoryAllowance)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(workingMemoryAllowance);
        return IsMatchCore(input, startOffsetInBytes, matchOptions, cancellationToken, workingMemoryAllowance);
    }

    /// <summary>Counts matches with separate caller-owned working admission.</summary>
    /// <remarks>Uses the existing PCRE2 global driver and numeric limits. Covered temporary loans refund synchronously after completion or failure; result traffic remains a distinct cumulative policy.</remarks>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions,
        CancellationToken cancellationToken, IUtf8MemoryAllowance workingMemoryAllowance)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(workingMemoryAllowance);
        return CountCore(input, startOffsetInBytes, matchOptions, cancellationToken, workingMemoryAllowance);
    }
}
