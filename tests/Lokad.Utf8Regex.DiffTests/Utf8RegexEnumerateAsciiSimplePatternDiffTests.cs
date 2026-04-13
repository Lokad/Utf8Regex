using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexEnumerateAsciiSimplePatternDiffTests
{
    [Fact]
    public void EnumerateMatchesMatchesRuntimeForAsciiSimplePattern()
    {
        const string input = "abXcd zz abYcd";
        var regex = new Utf8Regex("ab.cd", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "ab.cd", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void EnumerateMatchesMatchesRuntimeForAsciiCharacterClassSimplePattern()
    {
        const string input = "ab1d zz ab2d abXd";
        var regex = new Utf8Regex("ab[0-9]d", RegexOptions.CultureInvariant);

        var actual = new List<(int Index, int Length)>();
        foreach (var match in regex.EnumerateMatches(Encoding.UTF8.GetBytes(input)))
        {
            actual.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        var expected = new List<(int Index, int Length)>();
        foreach (var match in Regex.EnumerateMatches(input, "ab[0-9]d", RegexOptions.CultureInvariant))
        {
            expected.Add((match.Index, match.Length));
        }

        Assert.Equal(expected, actual);
    }
}
