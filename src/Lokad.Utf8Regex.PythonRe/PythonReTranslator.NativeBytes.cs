using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Lokad.Utf8Regex.Internal;
using ByteBuilder = Lokad.Utf8Regex.Internal.Utf8ConstructionBuffer<byte>;

namespace Lokad.Utf8Regex.PythonRe;

internal static partial class PythonReTranslator
{
    // Emit into the existing admitted byte vector. No second text builder or
    // whole-pattern encoding pass is needed by Core preparation or scalar leaves.
    private static ByteBuilder Append(this ByteBuilder builder, char value)
    {
        if (value <= 0x7f)
        {
            Utf8ConstructionScope.ChargeWork(1);
            builder.Add((byte)value);
            return builder;
        }
        return builder.AppendScalar(value);
    }

    private static ByteBuilder Append(this ByteBuilder builder, string text)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        budget.ChargeWork((ulong)text.Length);
        Span<byte> encoded = stackalloc byte[4];
        for (var offset = 0; offset < text.Length;)
        {
            if ((offset & 255) == 0) budget.ChargeWork(0);
            if (text[offset] <= 0x7f) { builder.Add((byte)text[offset++]); continue; }
            if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var scalar, out var consumed) != OperationStatus.Done)
                throw new InvalidOperationException("A rendered Python atom must contain complete Unicode scalars.");
            builder.AddRange(encoded[..scalar.EncodeToUtf8(encoded)]);
            offset += consumed;
        }
        return builder;
    }

    private static ByteBuilder Append(this ByteBuilder builder, int value)
    {
        Span<byte> formatted = stackalloc byte[11];
        Utf8ConstructionScope.ChargeWork((ulong)formatted.Length);
        if (!Utf8Formatter.TryFormat(value, formatted, out var written)) throw new InvalidOperationException("Integer formatting failed.");
        builder.AddRange(formatted[..written]);
        return builder;
    }

    private static ByteBuilder Append(this ByteBuilder builder, uint value)
    {
        Span<byte> formatted = stackalloc byte[11];
        Utf8ConstructionScope.ChargeWork((ulong)formatted.Length);
        if (!Utf8Formatter.TryFormat(value, formatted, out var written)) throw new InvalidOperationException("Integer formatting failed.");
        builder.AddRange(formatted[..written]);
        return builder;
    }

    private static ByteBuilder AppendHex(this ByteBuilder builder, int value)
    {
        Span<byte> formatted = stackalloc byte[8];
        Utf8ConstructionScope.ChargeWork((ulong)formatted.Length);
        if (!Utf8Formatter.TryFormat(value, formatted, out var written, new StandardFormat('X', 4)))
            throw new InvalidOperationException("Hex formatting failed.");
        builder.AddRange(formatted[..written]);
        return builder;
    }

    private static ByteBuilder AppendScalar(this ByteBuilder builder, int value)
    {
        Span<byte> encoded = stackalloc byte[4];
        var written = new Rune(value).EncodeToUtf8(encoded);
        builder.AddRange(encoded[..written]);
        return builder;
    }

    private static void AppendEscaped(this ByteBuilder builder, char value, bool verbose)
    {
        if (value is '\t' or '\n' or '\f' or '\r')
        {
            builder.Append('\\').Append(value switch { '\t' => 't', '\n' => 'n', '\f' => 'f', _ => 'r' });
            return;
        }
        if (value == ' ' && verbose || value is '#' or '$' or '(' or ')' or '*' or '+' or '.' or '?' or '[' or '\\' or '^' or '{' or '|')
            builder.Append('\\');
        builder.Append(value);
    }
}
