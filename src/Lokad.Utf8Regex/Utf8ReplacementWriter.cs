using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;
using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex;

/// <summary>Represents a callback that appends a replacement directly as UTF-8.</summary>
/// <typeparam name="TState">The caller-defined state carried between evaluations.</typeparam>
/// <param name="match">The current match over the original UTF-8 input.</param>
/// <param name="writer">The destination for the replacement value.</param>
/// <param name="state">The caller-defined mutable state.</param>
public delegate void Utf8MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref Utf8ReplacementWriter writer,
    ref TState state);

/// <summary>Represents a callback that creates a UTF-16 replacement string.</summary>
/// <typeparam name="TState">The caller-defined state carried between evaluations.</typeparam>
/// <param name="match">The current match over the original UTF-8 input.</param>
/// <param name="state">The caller-defined mutable state.</param>
/// <returns>The replacement value for the current match.</returns>
/// <remarks>Each returned string is encoded independently as UTF-8. Unpaired
/// surrogates become U+FFFD; valid pairs within that string remain one scalar.</remarks>
public delegate string Utf16MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref TState state);

/// <summary>Accumulates UTF-8 replacement output produced by a <see cref="Utf8MatchEvaluator{TState}"/>.</summary>
/// <remarks>The writer supplied to an evaluator is valid only during that callback.
/// Copies of an initialized writer share its buffer. Resetting it to default clears
/// accumulated output. The consumer validates the completed output as UTF-8.</remarks>
public ref struct Utf8ReplacementWriter
{
    private ArrayBufferWriter<byte>? _buffer;
    private Utf8SliceByteOutputBuffer? _owned;

    internal Utf8ReplacementWriter(ArrayBufferWriter<byte> buffer)
    {
        _buffer = buffer;
        _owned = null;
    }

    internal Utf8ReplacementWriter(Utf8SliceByteOutputBuffer buffer)
    {
        _buffer = null;
        _owned = buffer;
    }

    internal readonly Utf8SliceByteOutputBuffer? OwnedOutput => _owned;

    private Utf8SliceByteOutputBuffer? GetOwnedBuffer()
    {
        if (_owned is not null) return _owned;
        if (_buffer is null && Utf8SliceByteOutputScope.Current is { } scope) _owned = scope.CreateBuffer();
        return _owned;
    }

    internal Utf8SliceByteOutputBuffer GetOwnedOutput(Utf8SliceByteOutputScope scope)
    {
        return _owned ??= scope.CreateBuffer();
    }

    /// <summary>Appends bytes intended to be well-formed UTF-8.</summary>
    /// <remarks>The consumer checks well-formedness after assembling the completed output.</remarks>
    public void Append(ReadOnlySpan<byte> utf8)
    {
        if (GetOwnedBuffer() is { } owned)
        {
            owned.Append(utf8);
            return;
        }
        _buffer ??= new ArrayBufferWriter<byte>();
        var span = _buffer.GetSpan(utf8.Length);
        utf8.CopyTo(span);
        _buffer.Advance(utf8.Length);
    }

    /// <summary>Encodes and appends UTF-16 text as UTF-8.</summary>
    /// <remarks>Each append encodes its fragment independently, replacing lone surrogate halves.</remarks>
    public void Append(ReadOnlySpan<char> utf16)
    {
        if (GetOwnedBuffer() is { } owned)
        {
            owned.Append(utf16);
            return;
        }
        _buffer ??= new ArrayBufferWriter<byte>();
        var byteCount = Encoding.UTF8.GetByteCount(utf16);
        var span = _buffer.GetSpan(byteCount);
        var written = Encoding.UTF8.GetBytes(utf16, span);
        _buffer.Advance(written);
    }

    /// <summary>Appends one ASCII byte.</summary>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is greater than <c>0x7F</c>.</exception>
    public void AppendAsciiByte(byte value)
    {
        if (value > 0x7F)
        {
            throw new ArgumentOutOfRangeException(nameof(value), "Only ASCII bytes are valid with AppendAsciiByte.");
        }

        Span<byte> bytes = stackalloc byte[1];
        bytes[0] = value;
        if (GetOwnedBuffer() is { } owned)
        {
            owned.Append(bytes);
            return;
        }
        _buffer ??= new ArrayBufferWriter<byte>();
        var span = _buffer.GetSpan(1);
        span[0] = value;
        _buffer.Advance(1);
    }

    /// <summary>Encodes and appends one Unicode scalar value as UTF-8.</summary>
    public void Append(Rune value)
    {
        Span<byte> runeBytes = stackalloc byte[4];
        var written = value.EncodeToUtf8(runeBytes);
        if (GetOwnedBuffer() is { } owned)
        {
            owned.Append(runeBytes[..written]);
            return;
        }
        _buffer ??= new ArrayBufferWriter<byte>();
        var span = _buffer.GetSpan(written);
        runeBytes[..written].CopyTo(span);
        _buffer.Advance(written);
    }

    internal ReadOnlySpan<byte> GetValidatedBytes()
    {
        if (_owned is { } owned) return owned.GetValidatedBytes();
        var bytes = _buffer is null ? ReadOnlySpan<byte>.Empty : _buffer.WrittenSpan;
        _ = Utf8Validation.Validate(bytes);
        return bytes;
    }
}
