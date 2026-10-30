using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Borrowed construction input. Native parsing reads scalars directly;
/// string projection of a selected lexeme uses the current construction owner.</summary>
internal readonly ref struct Utf8ConstructionPattern
{
    internal Utf8ConstructionPattern(string text)
    {
        Text = text;
        Utf8 = [];
        IsUtf8 = false;
    }

    internal Utf8ConstructionPattern(ReadOnlySpan<byte> utf8)
    {
        Text = string.Empty;
        Utf8 = utf8;
        IsUtf8 = true;
    }

    internal string Text { get; }
    internal ReadOnlySpan<byte> Utf8 { get; }
    internal bool IsUtf8 { get; }

    internal int Length => IsUtf8 ? Utf8.Length : Text.Length;

    // Grammar punctuation is ASCII. Byte peeks deliberately do not decode or
    // acquire storage; literal/escape/class consumers must use ReadScalar.
    internal char this[int position] => IsUtf8 ? (char)Utf8[position] : Text[position];

    internal void ValidateUtf8()
    {
        if (!IsUtf8) return;
        var budget = Utf8ConstructionScope.CurrentBudget;
        for (var position = 0; position < Utf8.Length;)
        {
            budget.ChargeWork(1);
            if (Rune.DecodeFromUtf8(Utf8[position..], out _, out var consumed) != OperationStatus.Done)
                throw Utf8Validation.CreateInvalidUtf8Exception(position);
            position += consumed;
        }
        budget.ChargeWork(0);
    }

    internal int ReadScalar(ref int position)
    {
        // Text input keeps its legacy unpaired-surrogate grammar. The parser
        // combines adjacent/escaped pairs; UTF-8 has already rejected surrogates.
        if (!IsUtf8) return Text[position++];
        if (Rune.DecodeFromUtf8(Utf8[position..], out var scalar, out var consumed) != OperationStatus.Done)
            throw Utf8Validation.CreateInvalidUtf8Exception(position);
        position += consumed;
        return scalar.Value;
    }

    internal int IndexOf(char character, int start)
    {
        if (!IsUtf8) return Text.IndexOf(character, start);
        if (character > 0x7f) throw new ArgumentOutOfRangeException(nameof(character));
        var offset = Utf8[start..].IndexOf((byte)character);
        return offset < 0 ? -1 : start + offset;
    }

    internal string CopyText(int start, int length)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        return IsUtf8 ? budget.DecodeUtf8(Utf8.Slice(start, length), Encoding.UTF8)
            : budget.CopyText(Text.AsSpan(start, length));
    }
}
