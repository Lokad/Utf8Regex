using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

public sealed partial class Utf8Pcre2Regex
{
    /// <summary>Compiles text eagerly with one immutable execution/construction policy and an independent token.</summary>
    /// <remarks>Program funding prepays bounded replacement-cache capacity and remains reserved until close plus active calls/cursors. Construction cancellation precedes policy/pattern work and is not retained for matching. Null construction timeout is infinite; null match timeout uses DefaultMatchTimeout.</remarks>
    public static Utf8Pcre2Regex Compile(string pattern, Pcre2CompileOptions options = Pcre2CompileOptions.None,
        Utf8Pcre2CompileSettings compileSettings = default, Utf8RegexPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new Utf8ConstructionPattern(pattern), options, compileSettings, policy, cancellationToken, explicitCompile: true);
    }

    /// <summary>Compiles strict UTF-8 pattern bytes eagerly with one immutable policy and an independent token.</summary>
    /// <remarks>Source bytes are copied under admission before publication. Timeout diagnostics keep empty pattern/input rather than decoding the source. Subjects remain unchanged and must be valid UTF-8 unless <see cref="Pcre2CompileOptions.MatchInvalidUtf8"/> is selected.</remarks>
    public static Utf8Pcre2Regex Compile(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options = Pcre2CompileOptions.None,
        Utf8Pcre2CompileSettings compileSettings = default, Utf8RegexPolicy? policy = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new Utf8ConstructionPattern(patternUtf8), options, compileSettings, policy, cancellationToken, explicitCompile: true);
    }

    // Checkpoints bound pattern validation and preserve absolute error offsets.
    private static void ValidatePatternUtf8(ReadOnlySpan<byte> bytes, Utf8ConstructionBudget budget)
    {
        for (var offset = 0; offset < bytes.Length;)
        {
            if (offset != 0) budget.Checkpoint();
            var count = Utf8SliceBufferWork.Utf8BlockLength(bytes[offset..]);
            Utf8Validation.ThrowIfInvalidOnly(bytes.Slice(offset, count), offset);
            offset += count;
        }
    }
}
