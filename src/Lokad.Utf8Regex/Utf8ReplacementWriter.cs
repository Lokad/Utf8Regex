using Lokad.Utf8Regex.Internal.Execution;
using System.Text;

namespace Lokad.Utf8Regex;

// PCRE2-INTEGRATION-POINT: The adapter still consumes the shared callback writer.
/// <summary>Accumulates UTF-8 replacement output for adapter byte callbacks.</summary>
/// <remarks>The writer supplied to an evaluator is valid only during that callback.
/// Copies of an initialized writer share its buffer. Resetting it to default clears
/// accumulated output. Default/reset writers join the active callback's invocation.
/// Appending outside a replacement callback throws <see cref="InvalidOperationException"/>.
/// The consumer validates the completed output as UTF-8.</remarks>
public ref struct Utf8ReplacementWriter
{
    private Utf8SliceByteOutputBuffer? _owned;

    internal Utf8ReplacementWriter(Utf8SliceByteOutputBuffer buffer)
    {
        _owned = buffer;
    }

    internal readonly Utf8SliceByteOutputBuffer? OwnedOutput => _owned;

    private Utf8SliceByteOutputBuffer GetOwnedBuffer()
        => _owned ??= (Utf8SliceByteOutputScope.Current ??
            throw new InvalidOperationException("The writer requires an active replacement callback.")).CreateBuffer();

    internal Utf8SliceByteOutputBuffer GetOwnedOutput(Utf8SliceByteOutputScope scope)
    {
        return _owned ??= scope.CreateBuffer();
    }

    /// <summary>Appends bytes intended to be well-formed UTF-8.</summary>
    /// <remarks>Copies the span before returning, including stack-allocated input. The consumer checks well-formedness after assembling the completed output.</remarks>
    /// <exception cref="InvalidOperationException">No replacement callback is active.</exception>
    public void Append(scoped ReadOnlySpan<byte> utf8)
    {
        GetOwnedBuffer().Append(utf8);
    }

    /// <summary>Encodes and appends UTF-16 text as UTF-8.</summary>
    /// <remarks>Encodes the span before returning, including stack-allocated input. Each append encodes its fragment independently, replacing lone surrogate halves.</remarks>
    /// <exception cref="InvalidOperationException">No replacement callback is active.</exception>
    public void Append(scoped ReadOnlySpan<char> utf16)
    {
        GetOwnedBuffer().Append(utf16);
    }

    /// <summary>Appends one ASCII byte.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is greater than <c>0x7F</c>.</exception>
    /// <exception cref="InvalidOperationException">No replacement callback is active.</exception>
    public void AppendAsciiByte(byte value)
    {
        if (value > 0x7F)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Only ASCII bytes are valid with AppendAsciiByte.");
        }

        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        GetOwnedBuffer().Append(bytes);
    }

    /// <summary>Encodes and appends one Unicode scalar value as UTF-8.</summary>
    /// <exception cref="InvalidOperationException">No replacement callback is active.</exception>
    public void Append(Rune value)
    {
        Span<byte> runeBytes = stackalloc byte[4];
        var written = value.EncodeToUtf8(runeBytes);
        GetOwnedBuffer().Append(runeBytes[..written]);
    }
}
