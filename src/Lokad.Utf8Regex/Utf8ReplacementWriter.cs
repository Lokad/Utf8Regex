using Lokad.Utf8Regex.Internal.Input;
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
public delegate string Utf16MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref TState state);

/// <summary>Accumulates UTF-8 replacement output produced by a <see cref="Utf8MatchEvaluator{TState}"/>.</summary>
/// <remarks>The writer is valid only during its evaluator callback. The completed replacement is validated as UTF-8.</remarks>
public ref struct Utf8ReplacementWriter
{
    private ArrayBufferWriter<byte>? _buffer;

    internal Utf8ReplacementWriter(ArrayBufferWriter<byte> buffer)
    {
        _buffer = buffer;
    }

    /// <summary>Appends bytes intended to be well-formed UTF-8.</summary>
    /// <remarks>Well-formedness is checked after the evaluator completes.</remarks>
    public void Append(ReadOnlySpan<byte> utf8)
    {
        _buffer ??= new ArrayBufferWriter<byte>();
        var span = _buffer.GetSpan(utf8.Length);
        utf8.CopyTo(span);
        _buffer.Advance(utf8.Length);
    }

    /// <summary>Encodes and appends UTF-16 text as UTF-8.</summary>
    public void Append(ReadOnlySpan<char> utf16)
    {
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
        _buffer ??= new ArrayBufferWriter<byte>();
        var span = _buffer.GetSpan(written);
        runeBytes[..written].CopyTo(span);
        _buffer.Advance(written);
    }

    internal string ToValidatedString()
    {
        var bytes = _buffer is null ? ReadOnlySpan<byte>.Empty : _buffer.WrittenSpan;
        _ = Utf8Validation.Validate(bytes);
        return Encoding.UTF8.GetString(bytes);
    }

    internal ReadOnlySpan<byte> GetValidatedBytes()
    {
        var bytes = _buffer is null ? ReadOnlySpan<byte>.Empty : _buffer.WrittenSpan;
        _ = Utf8Validation.Validate(bytes);
        return bytes;
    }
}
