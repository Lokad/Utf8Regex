using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// A finite fixture comparator, not a regex implementation. The recorded shared
// capture corpus uses emoji and a Deseret upper/lower pair. Distinct BMP proxies
// retain its L/Lu/Ll/N/Cs, word/space/digit and BasicLatin predicates and folds.
// Numeric BMP-only ranges explicitly exclude proxies; surrogate atoms cannot
// consume a valid scalar. Direct byte witnesses qualify these transformations.
internal static class Utf8ScalarCaptureFixture
{
    internal sealed class Subject
    {
        internal readonly string Text;
        internal readonly string Projected;
        internal readonly byte[] Input;
        internal readonly int[] Units;
        internal readonly int[] Bytes;

        internal Subject(string text)
        {
            Text = text;
            Input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(text, Encoding.UTF8.GetString(Input));
            var projected = new StringBuilder();
            var units = new List<int> { 0 };
            var bytes = new List<int> { 0 };
            foreach (var rune in text.EnumerateRunes())
            {
                Assert.False(rune.Value is 0xe000 or 0x0531 or 0x0561, "Fixture proxy collision.");
                projected.Append(rune.IsBmp ? (char)rune.Value : rune.Value switch
                {
                    0x1f600 => '\ue000',
                    0x10400 => '\u0531',
                    0x10428 => '\u0561',
                    _ => throw new InvalidOperationException("Unqualified supplementary capture fixture scalar."),
                });
                units.Add(units[^1] + rune.Utf16SequenceLength);
                bytes.Add(bytes[^1] + rune.Utf8SequenceLength);
            }
            Projected = projected.ToString();
            Units = units.ToArray();
            Bytes = bytes.ToArray();
        }

        internal (int Index, int Length) UnitRange(int index, int length) =>
            (Units[index], Units[index + length] - Units[index]);

        internal (int Index, int Length) ByteRange(int index, int length) =>
            (Bytes[index], Bytes[index + length] - Bytes[index]);

        internal string Value(int index, int length)
        {
            var (start, count) = UnitRange(index, length);
            return Text.Substring(start, count);
        }

        internal static string Restore(string text) => text.Replace("\ue000", "\U0001f600", StringComparison.Ordinal)
            .Replace("\u0531", "\U00010400", StringComparison.Ordinal)
            .Replace("\u0561", "\U00010428", StringComparison.Ordinal);
    }

    internal static Regex Project(Regex original)
    {
        var pattern = original.ToString();
        Assert.DoesNotContain("\ue000", pattern);
        Assert.DoesNotContain("\u0531", pattern);
        Assert.DoesNotContain("\u0561", pattern);
        foreach (Match property in Regex.Matches(pattern, @"\\[pP]\{([^}]+)\}"))
            Assert.Contains(property.Groups[1].Value, new[] { "L", "Lu", "Ll", "N", "Cs", "IsBasicLatin" });
        foreach (Match escape in Regex.Matches(pattern, @"\\u([0-9a-fA-F]{4})"))
            Assert.Contains(escape.Groups[1].Value.ToUpperInvariant(),
                new[] { "0000", "0080", "0090", "D800", "DBFF", "DC00", "DFFF", "FFFE", "FFFF" });
        if (pattern.Contains(@"\u0000-\uFFFF", StringComparison.Ordinal) ||
            pattern.Contains(@"\u0080-\uFFFF", StringComparison.Ordinal))
            Assert.Contains(pattern, new[] { @"[\u0000-\uFFFF]+", @"(?i:[\u0080-\uFFFF]+)",
                @"[\u0000-\uFFFF-[\uD800-\uDFFF]]+" });

        // The existing direction fixture spells dot as a complete UTF-16 pair
        // or a non-surrogate BMP atom. One projected character has that meaning.
        const string scalar = @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF\n])";
        pattern = pattern.Replace(scalar, ".", StringComparison.Ordinal);
        // These are the corpus's three BMP-wide class forms. Keep their original
        // BMP members while excluding every supplementary proxy.
        pattern = pattern.Replace(@"[\u0000-\uFFFF-[\uD800-\uDFFF]]",
            @"[\u0000-\uFFFF-[\uD800-\uDFFF\uE000\u0531\u0561]]", StringComparison.Ordinal)
            .Replace(@"[\u0000-\uFFFF]", @"[\u0000-\uFFFF-[\uE000\u0531\u0561]]", StringComparison.Ordinal)
            .Replace(@"[\u0080-\uFFFF]", @"[\u0080-\uFFFF-[\uE000\u0531\u0561]]", StringComparison.Ordinal);
        // The only unpaired raw atoms in the valid comparator corpus are the
        // two alternatives below. Separate atoms cannot combine into emoji.
        if (pattern.Any(char.IsSurrogate) && !pattern.Contains("\U0001f600", StringComparison.Ordinal))
        {
            pattern = pattern switch
            {
                "\ud83d" or "\ude00" => "(?!)",
                "(?<x>\ud83d|\ude00){0,3}?b" => "(?<x>(?!)|(?!)){0,3}?b",
                _ => throw new InvalidOperationException("Unqualified unpaired capture fixture atom."),
            };
        }
        pattern = pattern.Replace("\U0001f600", "\ue000", StringComparison.Ordinal);
        Assert.False(pattern.Any(char.IsSurrogate), "Unqualified supplementary or unpaired pattern atom.");
        // Compiled selects an owned route, not a different scalar contract.
        // The .NET 10.0.12 compiled capture/range bugs are documented at callers;
        // all projected subjects use stable interpreted comparison here.
        var projected = new Regex(pattern, original.Options & ~RegexOptions.Compiled, original.MatchTimeout);
        Assert.Equal(original.GetGroupNames(), projected.GetGroupNames());
        Assert.Equal(original.GetGroupNumbers(), projected.GetGroupNumbers());
        return projected;
    }
}
