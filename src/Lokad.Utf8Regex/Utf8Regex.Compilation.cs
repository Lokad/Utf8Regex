using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    // Adapter construction inherits its existing authority, with no second
    // public construction owner or unadmitted source conversion.
    internal Utf8Regex(ReadOnlySpan<byte> patternUtf8, RegexOptions options, Utf8RegexPolicy? policy)
        : this(new Utf8ConstructionPattern(patternUtf8), options, policy, default, explicitCompile: false) { }

    /// <summary>Compiles text eagerly with one immutable execution/construction policy and an independent token.</summary>
    /// <remarks>ProgramMemoryAllowance funds retained storage until close plus admitted calls/cursors. The token is checked before policy/pattern work and is not retained for matching. Null match timeout uses DefaultMatchTimeout; null construction timeout is infinite.</remarks>
    public static Utf8Regex Compile(string pattern, RegexOptions options = RegexOptions.CultureInvariant,
        Utf8RegexPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new Utf8ConstructionPattern(pattern), options, policy, cancellationToken, explicitCompile: true);
    }

    /// <summary>Compiles strict UTF-8 bytes eagerly with one immutable policy and an independent token.</summary>
    /// <remarks>Retained source is owned before publication. Byte syntax errors retain byte coordinates; timeout diagnostics do not decode the pattern. Preserve the UTF8 factory's existing None options default.</remarks>
    public static Utf8Regex Compile(ReadOnlySpan<byte> patternUtf8, RegexOptions options = RegexOptions.None,
        Utf8RegexPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(new Utf8ConstructionPattern(patternUtf8), options, policy, cancellationToken, explicitCompile: true);
    }
}
