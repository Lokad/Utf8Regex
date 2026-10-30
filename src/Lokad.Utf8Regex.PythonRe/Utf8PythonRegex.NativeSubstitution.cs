using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

public sealed partial class Utf8PythonRegex
{
    /// <summary>Replaces matches with a strict UTF-8 Python template and returns owned UTF-8 bytes.</summary>
    /// <remarks>Zero count replaces all matches. The byte start preserves prefix assertion context. Template validation precedes matching, including on no match. Native templates are parsed and cached as bytes; they do not select Python bytes-pattern semantics.</remarks>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        int count = 0, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
        => Subn(input, replacementUtf8, count, startOffsetInBytes, cancellationToken).ResultBytes;

    /// <summary>Substitutes a strict UTF-8 Python template and returns owned bytes and the replacement count.</summary>
    /// <remarks>Retains scalar semantics, capture expansion, synchronous cancellation and the original deadline/resource policy. Template errors retain their UTF-16 Position; malformed UTF-8 is rejected before template grammar. A cold template copies an admitted immutable key; warm lookups borrow the supplied span.</remarks>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        int count = 0, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return SubnTemplateCore(input, new Utf8ConstructionPattern(replacementUtf8), count,
            startOffsetInBytes, cancellationToken, out _);
    }

    /// <summary>Substitutes a strict UTF-8 template into owned bytes with caller-funded retained admission.</summary>
    /// <remarks>Requires funded Compile. The receipt uses the same lifetime and release contract as the text-template overload; output remains valid after expression disposal. Template keys and preparation remain funded until eviction/disposal and their last active use.</remarks>
    /// <exception cref="InvalidOperationException">This expression has no caller-owned program allowance.</exception>
    public Utf8PythonRetainedSubnResult SubnRetained(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementUtf8,
        int count = 0, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_programMemory is null)
            throw new InvalidOperationException("Retained substitution requires caller-funded compilation.");
        _ = SubnTemplateCore(input, new Utf8ConstructionPattern(replacementUtf8), count,
            startOffsetInBytes, cancellationToken, out var retained, retain: true);
        return retained!;
    }
}
