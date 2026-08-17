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

    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, int.MaxValue)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, int.MaxValue)]
    public void BoundaryLiteralFamilyFallbackSplitHonorsResultCount(bool compiled, int count)
    {
        const string pattern = @"\b(?:café|niño|résumé)\b";
        const string input = "café xcafé caféx niño résumé 😀café😀";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(input);
        var actualValues = new List<string>();

        foreach (var split in actual.EnumerateSplits(bytes, count))
        {
            actualValues.Add(split.GetValueString());
        }

        Assert.False(actual.Inspection.DebugCanUseNativeSplit(bytes));
        Assert.Equal(expected.Split(input, count), actualValues);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryLiteralFamilySplitSelectorExcludesUnsupportedShapes(bool compiled)
    {
        const string input = "café niño résumé café noir";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var bytes = Encoding.UTF8.GetBytes(input);
        (string Pattern, RegexOptions ExtraOptions)[] cases =
        [
            (@"\b(café|niño|résumé)\b", RegexOptions.None),
            (@"(?:café|niño|résumé)(?= noir)", RegexOptions.None),
            (@"café|niño|résumé", RegexOptions.None),
            (@"\b(?:café|niño|résumé)\b", RegexOptions.RightToLeft),
        ];

        foreach (var testCase in cases)
        {
            var caseOptions = options | testCase.ExtraOptions;
            var expected = new Regex(testCase.Pattern, caseOptions);
            var actual = new Utf8Regex(testCase.Pattern, caseOptions);
            var actualValues = new List<string>();

            Assert.False(actual.Inspection.DebugCanUseNativeSplit(bytes));
            foreach (var split in actual.EnumerateSplits(bytes))
            {
                actualValues.Add(split.GetValueString());
            }

            var expectedValues = new List<string>();
            foreach (var split in expected.EnumerateSplits(input))
            {
                var (index, length) = split.GetOffsetAndLength(input.Length);
                expectedValues.Add(input.Substring(index, length));
            }

            Assert.Equal(expectedValues, actualValues);
        }
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

        var expectedSplits = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in expected.EnumerateSplits(input))
        {
            var (index, length) = split.GetOffsetAndLength(input.Length);
            var byteIndex = Encoding.UTF8.GetByteCount(input.AsSpan(0, index));
            var byteLength = Encoding.UTF8.GetByteCount(input.AsSpan(index, length));
            expectedSplits.Add((index, length, byteIndex, byteLength, input.Substring(index, length)));
        }

        var actualSplits = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in actual.EnumerateSplits(bytes))
        {
            actualSplits.Add((
                split.IndexInUtf16,
                split.LengthInUtf16,
                split.IndexInBytes,
                split.LengthInBytes,
                split.GetValueString()));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.Equal(
            expected.Replace(input, "<$&>"),
            Encoding.UTF8.GetString(actual.Replace(bytes, "<$&>")));
    }
}
