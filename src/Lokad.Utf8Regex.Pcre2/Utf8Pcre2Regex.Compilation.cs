using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

/// <inheritdoc />
public sealed partial class Utf8Pcre2Regex
{
    /// <summary>Compiles pattern text under explicit eager construction and execution policies.</summary>
    /// <remarks>Construction admission and interruption use one synchronous owner,
    /// independently of later invocation limits. Positive construction limits may
    /// tighten fixed backstops. The construction token is not retained. Limits do
    /// not cover every allocation, CLR/JIT metadata, lazy replacement preparation,
    /// caller retention or process memory.</remarks>
    /// <exception cref="OperationCanceledException">Construction cancellation was requested.</exception>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The construction timeout expired.</exception>
    /// <exception cref="Utf8ResourceLimitExceededException">A covered construction limit was exceeded.</exception>
    public static Utf8Pcre2Regex Compile(string pattern, Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings, TimeSpan matchTimeout,
        Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken)
        => Compile(new Utf8ConstructionPattern(pattern), options, compileSettings, matchTimeout,
            resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken);

    /// <summary>Compiles strict UTF-8 pattern bytes under explicit eager construction and execution policies.</summary>
    /// <remarks>Admission and interruption include bounded strict validation and
    /// decoding before parsing. The construction timeout is separate from the
    /// returned expression's match timeout. Timeout exceptions have empty Input
    /// and Pattern, avoiding decoding merely to report interruption. Covered
    /// construction uses the same scope and exclusions as the text factory.</remarks>
    /// <exception cref="ArgumentException">The pattern bytes are not valid UTF-8.</exception>
    /// <exception cref="OperationCanceledException">Construction cancellation was requested.</exception>
    /// <exception cref="System.Text.RegularExpressions.RegexMatchTimeoutException">The construction timeout expired.</exception>
    /// <exception cref="Utf8ResourceLimitExceededException">A covered construction limit was exceeded.</exception>
    public static Utf8Pcre2Regex Compile(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings, TimeSpan matchTimeout,
        Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken)
        => Compile(new Utf8ConstructionPattern(patternUtf8), options, compileSettings, matchTimeout,
            resourcePolicy, constructionPolicy, constructionTimeout, cancellationToken);

    private static Utf8Pcre2Regex Compile(Utf8ConstructionPattern pattern, Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings, TimeSpan matchTimeout,
        Utf8ResourcePolicy resourcePolicy, Utf8ConstructionPolicy constructionPolicy,
        TimeSpan constructionTimeout, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        constructionPolicy.Validate(nameof(constructionPolicy));
        var executionLimits = ToExecutionLimits(resourcePolicy);
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(constructionTimeout));
        using var construction = Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken);
        try
        {
            var result = new Utf8Pcre2Regex(pattern, options, compileSettings, executionLimits, matchTimeout);
            construction.Budget.ChargeWork(0);
            construction.Complete();
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new System.Text.RegularExpressions.RegexMatchTimeoutException(string.Empty,
                pattern.IsUtf8 ? string.Empty : pattern.Text, constructionTimeout);
        }
    }

    // The original whole-input charge precedes this pass. Checkpoints bound
    // validation without changing work units or the existing UTF-8 error identity.
    private static void ValidatePatternUtf8(ReadOnlySpan<byte> bytes, Utf8ConstructionBudget budget)
    {
        for (var offset = 0; offset < bytes.Length;)
        {
            if (offset != 0) budget.ChargeWork(0);
            var count = Utf8SliceBufferWork.Utf8BlockLength(bytes[offset..]);
            if (!Utf8ValidationCore.TryValidate(bytes.Slice(offset, count), false, out _, out var error))
                throw Utf8Validation.CreateInvalidUtf8Exception(offset + error);
            offset += count;
        }
    }
}
