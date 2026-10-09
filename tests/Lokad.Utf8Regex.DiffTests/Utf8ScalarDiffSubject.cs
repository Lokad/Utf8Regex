using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// Finite comparator for the differential fixtures' two supplementary symbols.
// Both are distinct non-word, non-letter, non-digit, non-space scalars. The
// private-use proxies have the same predicates in this frozen pattern corpus.
// Numeric ranges and other Unicode properties require separate qualification.
internal sealed class Utf8ScalarDiffSubject
{
    internal readonly string Text;
    internal readonly string Projected;
    private readonly int[] _units;
    private readonly int[] _bytes;

    internal Utf8ScalarDiffSubject(string text)
    {
        Text = text;
        Assert.Equal(text, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(text)));
        var projected = new StringBuilder();
        var units = new List<int> { 0 };
        var bytes = new List<int> { 0 };
        foreach (var scalar in text.EnumerateRunes())
        {
            Assert.False(scalar.Value is 0xe000 or 0xe001);
            projected.Append(scalar.IsBmp ? (char)scalar.Value : scalar.Value switch
            {
                0x1f600 => '\ue000',
                0x1f620 => '\ue001',
                _ => throw new InvalidOperationException("Unqualified differential fixture scalar."),
            });
            units.Add(units[^1] + scalar.Utf16SequenceLength);
            bytes.Add(bytes[^1] + scalar.Utf8SequenceLength);
        }
        Projected = projected.ToString();
        _units = units.ToArray();
        _bytes = bytes.ToArray();
    }

    internal Regex Comparator(string pattern, RegexOptions options)
    {
        Assert.False(pattern.Contains('\ue000') || pattern.Contains('\ue001'));
        foreach (Match property in Regex.Matches(pattern, @"\\[pP]\{([^}]+)\}"))
            Assert.Contains(property.Groups[1].Value, new[] { "Lu", "Nd" });
        foreach (Match escape in Regex.Matches(pattern, @"\\[uUx]([0-9A-Fa-f]+)"))
            Assert.Equal("00E9", escape.Groups[1].Value);
        pattern = pattern.Replace("\U0001f600", "\ue000", StringComparison.Ordinal)
            .Replace("\U0001f620", "\ue001", StringComparison.Ordinal);
        Assert.False(pattern.Any(char.IsSurrogate));
        return new Regex(pattern, options & ~RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
    }

    internal (int Index, int Length) Units(int index, int length) =>
        (_units[index], _units[index + length] - _units[index]);

    internal (int Index, int Length) Bytes(int index, int length) =>
        (_bytes[index], _bytes[index + length] - _bytes[index]);

    internal string Value(int index, int length)
    {
        var (start, count) = Units(index, length);
        return Text.Substring(start, count);
    }
}
