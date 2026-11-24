using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// Supplementary text uses the scalar oracle, including empty-match progress.
public sealed class Utf8RegexSliceSplitDiffTests
{
    [Theory]
    [InlineData(",", "a\U0001F600,b")]
    [InlineData("a+", "\U0001F600aa\U0001F600")]
    [InlineData("x*|y", "a\U0001F600b")]
    public void SliceSplitPreservesSupplementaryScalars(string pattern, string input)
    {
        var subject = new Utf8ScalarDiffSubject(input);
        var oracle = subject.Comparator(pattern, RegexOptions.CultureInvariant);
        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(subject.Projected, int.MaxValue))
        {
            var (index, length) = range.GetOffsetAndLength(subject.Projected.Length);
            var (start, units) = subject.Bytes(index, length);
            expected.Add((subject.Value(index, length), start, units));
        }
        using var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var actual = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }
        Assert.Equal(expected, actual);
    }
}
