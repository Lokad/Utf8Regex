using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
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
        {
            @"\b(?:café|caféteria|niño)\b",
            "caféteria café cafétéria niño"
        },
        {
            @"(?:café|café noir|niño)(?=!)",
            "café noir! café! niño! café gris!"
        },
        {
            @"\b(?:café|caféteria|caféterias|niño)\b",
            "caféterias cafetería café niño cafeteriasx"
        },
        {
            @"\b(?:a-b|b|niño)\b",
            "xa-b a-b b niño"
        },
        {
            @"\b(?:Task|ValueTask|IAsyncEnumerable)\b",
            "Task xTask Taskx ValueTask IAsyncEnumerable éTask Taské 😀Task😀"
        },
        {
            @"\b(?:-a|a-|𐐀|café)\b",
            "-a a- x-a a-x 𐐀 x𐐀x café xcafé caféx"
        },
        {
            @"\B(?:-a|a-|𐐀|café)\B",
            "-a a- x-a a-x 𐐀 x𐐀x café xcafé caféx"
        },
    };

    [Theory]
    [InlineData(@"\b(?:foo|foobar|baz)\b", true)]
    [InlineData(@"\b(?:foobar|foo|baz)\b", true)]
    [InlineData(@"\b(?:foo|bar|baz)\b", false)]
    [InlineData(@"(?:foo|foobar|baz)", false)]
    public void PrefixOverlapIsPreparedOnce(string pattern, bool expected)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(expected, regex.Inspection.SearchPlan.HasAlternateLiteralPrefixOverlap);
    }

    [Theory]
    [InlineData(@"\b(?:a-b|b|niño)\b", true)]
    [InlineData(@"\b(?:abcd|bcde|niño)\b", true)]
    [InlineData(@"\b(?:ababa|niño|résumé)\b", true)]
    [InlineData(@"\b(?:café|niño|résumé)\b", false)]
    public void ProperStartOverlapIsPreparedOnce(string pattern, bool expected)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(expected, regex.Inspection.SearchPlan.HasAlternateLiteralProperStartOverlap);
    }

    [Fact]
    public void CompiledAsciiBoundaryFamiliesUseTheNativeLiteralRuntime()
    {
        var prefix = new Utf8Regex(
            @"\b(?:foo|foobar|baz)\b",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var noPrefix = new Utf8Regex(
            @"\b(?:foo|quux|baz)\b",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        Assert.True(prefix.Inspection.SearchPlan.HasAlternateLiteralPrefixOverlap);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, prefix.Inspection.CompiledEngineKind);
        Assert.False(noPrefix.Inspection.SearchPlan.HasAlternateLiteralPrefixOverlap);
        Assert.Equal(Utf8CompiledEngineKind.LiteralFamily, noPrefix.Inspection.CompiledEngineKind);
    }

    [Theory]
    [MemberData(nameof(BoundaryCases))]
    public void BoundaryLiteralFamilyGlobalOperationsMatchScalarContract(string pattern, string input)
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
    public void BoundaryLiteralFamilyNativeSplitHonorsResultCount(bool compiled, int count)
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

        Assert.True(actual.Inspection.DebugCanUseNativeSplit(bytes));
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

    [Theory]
    [InlineData(@"\b(?:foo|foobar|baz)\b", "foobar foo foobarx baz xfoox")]
    [InlineData(@"(?:foo|foobar|baz)(?=!)", "foobar! foo! baz! foobar?")]
    public void AsciiPrefixLiteralFamilyRequirementsMatchDotNet(string pattern, string input)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            AssertGlobalParity(
                pattern,
                input,
                options,
                Regex.InfiniteMatchTimeout,
                expectedExecutionKind: null);
            AssertGlobalParity(
                pattern,
                input,
                options,
                TimeSpan.FromSeconds(1),
                expectedExecutionKind: null);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Utf8PrefixLiteralFamilyRequirementsMatchDotNetRightToLeft(bool compiled)
    {
        const string pattern = @"\b(?:café|caféteria|niño)\b";
        const string input = "caféteria café cafétéria niño cafetería";
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(expected.IsMatch(input), actual.IsMatch(bytes));
        Assert.Equal(expected.Count(input), actual.Count(bytes));
        var expectedMatch = expected.Match(input);
        var actualMatch = actual.Match(bytes);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
    }

    [Theory]
    [InlineData(@"\b(?:café|foo|𐐀)\b", 0, false)]
    [InlineData(@"\b(?:café|foo|𐐀)\b", 0, true)]
    [InlineData(@"\b(?:-a|a-|𐐀|café)\b", 0, false)]
    [InlineData(@"\b(?:-a|a-|𐐀|café)\b", 0, true)]
    [InlineData(@"\B(?:-a|a-|𐐀|café)\B", 6, false)]
    [InlineData(@"\B(?:-a|a-|𐐀|café)\B", 6, true)]
    public void DeseretLiteralBoundariesUseWordScalar(string pattern, int byteIndex, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        foreach (var timeout in new[] { Regex.InfiniteMatchTimeout, TimeSpan.FromSeconds(1) })
        {
            var regex = new Utf8Regex(pattern, options, timeout);
            Assert.Equal(1, regex.Count("𐐀 x𐐀x"u8));
            var match = regex.Match("𐐀 x𐐀x"u8);
            Assert.True(match.Success);
            Assert.Equal((byteIndex, 4), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal((byteIndex == 0 ? 0 : 4, 2), (match.IndexInUtf16, match.LengthInUtf16));
            Assert.Equal(byteIndex == 0 ? "<> x𐐀x" : "𐐀 x<>x", regex.ReplaceToString("𐐀 x𐐀x"u8, "<>"));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    private static void AssertGlobalParity(
        string pattern,
        string input,
        RegexOptions options,
        TimeSpan timeout,
        NativeExecutionKind? expectedExecutionKind = NativeExecutionKind.ExactUtf8Literals)
    {
        // This frozen literal alphabet uses emoji and Deseret Lu. Distinct unused
        // proxies preserve literal identity and word membership; ranges map back
        // to the original bytes. No classes, numeric ranges or other predicates
        // enter this comparator. Direct Deseret boundary witnesses are below.
        var subject = new Utf8AsciiScalarOperationAssertions.Subject(input);
        foreach (var scalar in pattern.EnumerateRunes())
            Assert.True(scalar.IsBmp || scalar.Value is 0x10400 or 0x1f600);
        Assert.False(pattern.Contains('\ue000') || pattern.Contains('\u0531'));
        var projectedPattern = pattern.Replace("\U00010400", "\u0531", StringComparison.Ordinal)
            .Replace("\U0001f600", "\ue000", StringComparison.Ordinal);
        var expected = new Regex(projectedPattern, options & ~RegexOptions.Compiled, timeout);
        var actual = new Utf8Regex(pattern, options, timeout);
        Assert.Equal(new Regex(pattern, options & ~RegexOptions.Compiled).GetGroupNumbers(), expected.GetGroupNumbers());
        var bytes = subject.Input;

        if (expectedExecutionKind is { } executionKind)
        {
            Assert.Equal(executionKind, actual.Inspection.ExecutionKind);
        }
        Assert.Equal(expected.IsMatch(subject.Projected), actual.IsMatch(bytes));
        Assert.Equal(expected.Count(subject.Projected), actual.Count(bytes));

        var expectedFirst = expected.Match(subject.Projected);
        var actualFirst = actual.Match(bytes);
        Assert.Equal(expectedFirst.Success, actualFirst.Success);
        if (expectedFirst.Success)
        {
            Assert.Equal(subject.Units(expectedFirst.Index, expectedFirst.Length), (actualFirst.IndexInUtf16, actualFirst.LengthInUtf16));
            Assert.Equal(subject.Bytes(expectedFirst.Index, expectedFirst.Length), (actualFirst.IndexInBytes, actualFirst.LengthInBytes));
        }

        var expectedMatches = new List<(int Index, int Length)>();
        foreach (var match in expected.EnumerateMatches(subject.Projected))
        {
            expectedMatches.Add(subject.Units(match.Index, match.Length));
        }

        var actualMatches = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(bytes))
        {
            actualMatches.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal(expectedMatches, actualMatches);

        var expectedSplits = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in expected.EnumerateSplits(subject.Projected))
        {
            var (index, length) = split.GetOffsetAndLength(subject.Projected.Length);
            var (unitIndex, unitLength) = subject.Units(index, length);
            var (byteIndex, byteLength) = subject.Bytes(index, length);
            expectedSplits.Add((unitIndex, unitLength, byteIndex, byteLength, subject.Value(index, length)));
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
            subject.Restore(expected.Replace(subject.Projected, "<$&>")),
            Encoding.UTF8.GetString(actual.Replace(bytes, "<$&>")));
    }
}
