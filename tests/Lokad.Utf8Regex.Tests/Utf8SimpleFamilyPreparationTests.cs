using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SimpleFamilyPreparationTests
{
    [Theory]
    [InlineData("a?b[0-9]{2}")]
    [InlineData("a??b[0-9]{2}")]
    [InlineData("a{1,2}b[0-9]{2}")]
    [InlineData("(?<g>a?b[0-9]{2})")]
    [InlineData("(?m:^a?b[0-9]{2}$)")]
    [InlineData("(?:a|c)?b[0-9]{2}")]
    [InlineData("(?:ab|ac)?d[0-9]")]
    public void CandidateFamiliesKeepOrderedPublicRangesAtOffsetsAndTails(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.NonBacktracking, RegexOptions.IgnoreCase })
        {
            // The interpreted ASCII oracle avoids the known .NET10 NB
            // optional-prefix search discrepancy; scalar NB still keeps both hits.
            var oracleOptions = (mode & ~RegexOptions.NonBacktracking) | RegexOptions.CultureInvariant;
            var oracle = new Regex(pattern, oracleOptions);
            var regex = new Utf8Regex(pattern, mode | RegexOptions.CultureInvariant);
            foreach (var text in new[] { "", "a", "b1", "b12", "ab12", "aab12",
                "abxx b12 ab34", "b12ab34b56", "Ab12 B34", "\nab12\nb34\n",
                "ad9 acd8", new string('a', 4096) + "b12" })
                Compare(oracle, regex, text);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(16)]
    public void ShortAndLongPrefixesKeepTheirFullVerification(int prefixLength)
    {
        var prefix = new string('a', prefixLength);
        var pattern = $"(?:{prefix})?b[0-9]";
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var full = "xx " + prefix + "b7 b8 " + prefix + "x9";
        for (var length = 0; length <= full.Length; length++)
            Compare(oracle, regex, full[..length]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RepeatedCountDoesNotRebuildShortFamilyMaps(bool compiled)
    {
        var regex = new Utf8Regex("a?b[0-9]{2}", RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("xx ab12 b34 ", 256)));
        for (var i = 0; i < 32; i++) Assert.Equal(512, regex.Count(input));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var count = 0;
        for (var i = 0; i < 64; i++) count += regex.Count(input);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(64 * 512, count);
        Assert.InRange(allocated, 0, 8192);
        Assert.Throws<ArgumentException>(() => regex.Count([.. input, 255]));
    }

    private static void Compare(Regex oracle, Utf8Regex regex, string text)
    {
        var expected = oracle.Matches(text).Select(x => (x.Index, x.Length)).ToArray();
        var bytes = Encoding.UTF8.GetBytes(text);
        for (var offset = 0; offset < 5; offset++)
        {
            var storage = new byte[offset + bytes.Length + 3];
            bytes.CopyTo(storage, offset);
            var input = storage.AsSpan(offset, bytes.Length);
            Assert.Equal(expected.Length, regex.Count(input));
            var first = regex.Match(input);
            Assert.Equal(expected.Length != 0, first.Success);
            if (first.Success) Assert.Equal(expected[0], (first.IndexInBytes, first.LengthInBytes));
            var actual = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(input))
                actual.Add((match.IndexInBytes, match.LengthInBytes));
            Assert.Equal(expected, actual);
        }
    }
}
