using System.Globalization;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Admitted construction text. Growth includes old/new array overlap;
/// materialized strings and their retaining descriptors share the active owner.</summary>
internal sealed class Utf8ConstructionText(Utf8ConstructionBudget budget, Utf8ConstructionBuffer<char> characters) : IDisposable
{
    internal static Utf8ConstructionText Create()
    {
        var owner = Utf8ConstructionScope.CurrentBudget;
        var buffer = Utf8ConstructionBuffer<char>.Create(owner);
        return owner.AllocateObject((owner, buffer), static state => new Utf8ConstructionText(state.owner, state.buffer));
    }
    internal int Length => characters.Count;
    internal ReadOnlySpan<char> Span => characters.Span;
    internal char this[int index] => characters[index];
    internal void Truncate(int length) => characters.Truncate(length);
    internal Utf8ConstructionText Append(char value)
    {
        budget.ChargeWork(1);
        characters.Add(value);
        return this;
    }
    internal Utf8ConstructionText Append(ReadOnlySpan<char> value)
    {
        characters.AddRange(value);
        return this;
    }
    internal Utf8ConstructionText Append(int value)
    {
        Span<char> formatted = stackalloc char[11];
        budget.ChargeWork((ulong)formatted.Length);
        value.TryFormat(formatted, out var written, provider: CultureInfo.InvariantCulture);
        return Append(formatted[..written]);
    }
    internal Utf8ConstructionText Append(uint value)
    {
        Span<char> formatted = stackalloc char[11];
        budget.ChargeWork((ulong)formatted.Length);
        value.TryFormat(formatted, out var written, provider: CultureInfo.InvariantCulture);
        return Append(formatted[..written]);
    }
    internal Utf8ConstructionText AppendHex(int value)
    {
        Span<char> formatted = stackalloc char[8];
        budget.ChargeWork((ulong)formatted.Length);
        value.TryFormat(formatted, out var written, "X4", CultureInfo.InvariantCulture);
        return Append(formatted[..written]);
    }
    internal Utf8ConstructionText AppendScalar(int scalar)
    {
        if (scalar <= 0xffff) return Append((char)scalar);
        var supplementary = scalar - 0x10000;
        return Append((char)(0xd800 + (supplementary >> 10))).Append((char)(0xdc00 + (supplementary & 1023)));
    }
    internal Utf8ConstructionText AppendEscaped(char value)
    {
        Span<char> escaped = stackalloc char[2];
        return Append(escaped[..WriteEscaped(value, escaped)]);
    }
    private static int WriteEscaped(char value, Span<char> target)
    {
        if (value is '\t' or '\n' or '\f' or '\r')
        {
            target[0] = '\\';
            target[1] = value switch { '\t' => 't', '\n' => 'n', '\f' => 'f', _ => 'r' };
            return 2;
        }
        if (value is ' ' or '#' or '$' or '(' or ')' or '*' or '+' or '.' or '?' or '[' or '\\' or '^' or '{' or '|')
        {
            target[0] = '\\'; target[1] = value;
            return 2;
        }
        target[0] = value;
        return 1;
    }
    internal string ToText() => budget.CopyText(characters.Span);
    internal string ToText(int start, int length) => budget.CopyText(characters.Span.Slice(start, length));
    internal void Clear() => characters.Clear();
    internal byte[] ToAscii()
    {
        var result = budget.AllocateArray<byte>(Length, false);
        try
        {
            budget.ChargeWork((ulong)Length);
            for (var index = 0; index < result.Length; index++) result[index] = (byte)characters[index];
            return result;
        }
        catch { budget.ReleaseArray(result, false); throw; }
    }
    internal static string EscapeScalar(int scalar)
    {
        Span<char> escaped = stackalloc char[2];
        var length = 2;
        if (scalar <= 0xffff) length = WriteEscaped((char)scalar, escaped);
        else
        {
            var supplementary = scalar - 0x10000;
            escaped[0] = (char)(0xd800 + (supplementary >> 10));
            escaped[1] = (char)(0xdc00 + (supplementary & 1023));
        }
        return Utf8ConstructionScope.CurrentBudget.CopyText(escaped[..length]);
    }
    internal static string Replace(string source, string value, string replacement)
    {
        ArgumentException.ThrowIfNullOrEmpty(value);
        var owner = Utf8ConstructionScope.CurrentBudget;
        // Searches resume beyond each match: each candidate start is visited
        // at most once. Charge the pass, conservatively allowing a full needle
        // comparison per candidate, rather than charging overlapping suffixes.
        // Production needles are fixed, so dense normalization remains linear.
        owner.ChargeWork((ulong)source.Length * (ulong)value.Length);
        var index = source.IndexOf(value, StringComparison.Ordinal);
        if (index < 0) return source;
        using var text = Create();
        var start = 0;
        do
        {
            text.Append(source.AsSpan(start, index - start)).Append(replacement);
            start = index + value.Length;
            owner.ChargeWork(0); // Keep interruption checkpoints between searches.
            index = source.IndexOf(value, start, StringComparison.Ordinal);
        } while (index >= 0);
        text.Append(source.AsSpan(start));
        return text.ToText();
    }
    /// <summary>Releases only scratch character storage; created strings stay with their construction owner.</summary>
    public void Dispose() => characters.Dispose();
}
