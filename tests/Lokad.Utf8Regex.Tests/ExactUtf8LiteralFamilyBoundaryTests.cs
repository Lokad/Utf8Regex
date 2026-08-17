using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class ExactUtf8LiteralFamilyBoundaryTests
{
    public static TheoryData<string, string> BoundaryCases => new()
    {
        {
            @"\b(?:café|niño|résumé)\b",
            "café xcafé caféx niño résumé 😀café😀"
        },
        {
            @"\b(?:café|niño|résumé)",
            "café xcafé caféx niño résumé"
        },
        {
            @"(?:café|niño|résumé)\b",
            "café xcafé caféx niño résumé"
        },
        {
            @"\B(?:café|niño|résumé)\B",
            "xcaféx xniñox xrésuméx"
        },
        {
            @"\b(?:café|foo|𐐀)\b",
            "café foo 𐐀 xcaféx xfoox x𐐀x"
        },
        {
            @"\b(?:caféteria|café|niño)\b",
            "caféteria café cafétéria niño"
        },
    };

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void BoundaryLiteralFamilyGlobalOperationsMatchDotNet(string pattern, string input)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            AssertGlobalParity(pattern, input, options, Regex.InfiniteMatchTimeout);
            AssertGlobalParity(pattern, input, options, TimeSpan.FromSeconds(1));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryLiteralFamilyRejectsMalformedUtf8(bool compiled)
    {
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(@"\b(?:café|niño|résumé)\b", options);
        byte[] malformed = [0xFF, .. Encoding.UTF8.GetBytes(" café ")];

        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        Assert.Throws<ArgumentException>(() => regex.EnumerateMatches(malformed));
        Assert.Throws<ArgumentException>(() => regex.EnumerateSplits(malformed));
        Assert.Throws<ArgumentException>(() => regex.Replace(malformed, "<$&>"));
    }

    private static void AssertGlobalParity(
        string pattern,
        string input,
        RegexOptions options,
        TimeSpan timeout)
    {
        var expected = new Regex(pattern, options, timeout);
        var actual = new Utf8Regex(pattern, options, timeout);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, actual.Inspection.ExecutionKind);
        Assert.Equal(expected.IsMatch(input), actual.IsMatch(bytes));
        Assert.Equal(expected.Count(input), actual.Count(bytes));

        var expectedFirst = expected.Match(input);
        var actualFirst = actual.Match(bytes);
        Assert.Equal(expectedFirst.Success, actualFirst.Success);
        Assert.Equal(expectedFirst.Index, actualFirst.IndexInUtf16);
        Assert.Equal(expectedFirst.Length, actualFirst.LengthInUtf16);

        var expectedMatches = new List<(int Index, int Length)>();
        foreach (var match in expected.EnumerateMatches(input))
        {
            expectedMatches.Add((match.Index, match.Length));
        }

        var actualMatches = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(bytes))
        {
            actualMatches.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal(expectedMatches, actualMatches);

        var expectedSplits = expected.Split(input);
        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(bytes))
        {
            actualSplits.Add(split.GetValueString());
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.Equal(
            expected.Replace(input, "<$&>"),
            Encoding.UTF8.GetString(actual.Replace(bytes, "<$&>")));
    }
}
