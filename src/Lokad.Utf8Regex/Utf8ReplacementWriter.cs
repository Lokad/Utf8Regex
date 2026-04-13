using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex;

public delegate void Utf8MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref Utf8ReplacementWriter writer,
    ref TState state);

public delegate string Utf16MatchEvaluator<TState>(
    in Utf8MatchContext match,
    ref TState state);

public ref struct Utf8ReplacementWriter
{
    private ArrayBufferWriter<byte>? _buffer;

    public void Append(ReadOnlySpan<byte> utf8)
    {
        _buffer ??= new ArrayBufferWriter<byte>();
        var span = _buffer.GetSpan(utf8.Length);
        utf8.CopyTo(span);
        _buffer.Advance(utf8.Length);
    }

    public void Append(ReadOnlySpan<char> utf16)
    {
        _buffer ??= new ArrayBufferWriter<byte>();
        var byteCount = Encoding.UTF8.GetByteCount(utf16);
        var span = _buffer.GetSpan(byteCount);
        var written = Encoding.UTF8.GetBytes(utf16, span);
        _buffer.Advance(written);
    }

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
}
