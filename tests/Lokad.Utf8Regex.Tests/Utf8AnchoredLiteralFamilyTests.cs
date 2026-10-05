using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredLiteralFamilyTests
{
    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredSplitCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] texts = ["xxab", "abxx", "ab", "xxabxx", "ab\n", "ab\ncd", "", "éab", "abé"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredSplitCases))]
    public void AnchoredSplitOwnsInput(string pattern, RegexOptions options, string text)
    {
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], start, end - start));
        }

        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> MultilineAnchoredSplitOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?m:^ab$)", "ab\ncd"),
            ("(?m:^ab$)", "ab\nab"),
            ("(?m:^ab$)", "xxab"),
            ("(?m:^ab$)", ""),
            ("(?m:^ab$)", "éab\ncd"),
            ("(?m:^ab$)", "a\U0001F600\nab"),
            ("(?m:^ab)", "ab\ncd"),
            ("(?m:^ab)", "xxab\nab"),
            ("(?m:^ab)", "xxab"),
            ("(?m:ab$)", "ab\ncd"),
            ("(?m:ab$)", "xxab"),
            ("(?m:ab$)", "ab\nab"),
            ("(?m:a.c$)", "abc\nadc"),
            ("(?m:a.c$)", "xxabc"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(MultilineAnchoredSplitOwnedCases))]
    public void MultilineAnchoredSplitStaysOwned(string pattern, RegexOptions options, string text)
    {
        // R05: line-anchored splits iterate line-edge matches through the owned split
        // cursors exactly like enumeration; absolute-anchored shapes keep their
        // dedicated arms and fallbacks.
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], start, end - start));
        }

        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> MultilineMixedSplitDeclineCases()
    {
        // Absolute-start shapes keep BCL splits even with a line-anchored end;
        // parity holds and construction is pinned per lane.
        (string pattern, string text)[] rows =
        [
            ("^(?m:ab$)", "ab\ncd"),
            ("^(?m:ab$)", "xxab\nab"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(MultilineMixedSplitDeclineCases))]
    public void MultilineMixedSplitUsesOwnedShaping(string pattern, RegexOptions options, string text)
    {
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], start, end - start));
        }

        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredReplaceCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] templates = ["x", "<$1>", "$&", "$`", "$\u0027", "$_", "$+", "$$"];
        string[] texts = ["abxx", "xxab", "ab", "ab\n", "éab", "abé", ""];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var text in texts)
                {
                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, text };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredReplaceCases))]
    public void AnchoredReplaceOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredEvaluatorCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] texts = ["abxx", "xxab", "ab", "ab\n", "éab", "abé", ""];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredEvaluatorCases))]
    public void AnchoredEvaluatorOwnsInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "<" + match.Value + ">");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("<"u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append(">"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "<" + context.GetGroup(0).GetValueString() + ">");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> ScopedFoldedAnchoredCases()
    {
        string[] patterns = ["(?i:^ab)", "(?i:ab$)"];
        string[] texts = ["ABxx", "xxAB", "abxx", "xxab"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ScopedFoldedAnchoredCases))]
    public void ScopedFoldedAnchoredDiscoveryOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Scoped folds route through the owned folded-literal machinery (never the
        // absolute direct families), so discovery needs no fallback engine.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var enumRegex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((expectedMatch.Index, expectedMatch.Length));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in enumRegex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        Assert.False(enumRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedPrefixCases()
    {
        string[] patterns = ["^ab", "\\Aab"];
        string[] texts = ["ABxx", "xxAB", "abxx", "xxab", "ABé", "éAB"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(FoldedPrefixCases))]
    public void FoldedPrefixDiscoveryOwnsInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var enumRegex = new Utf8Regex(pattern, options);
        var expectedEnum = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedEnumMatch in oracle.Matches(text))
        {
            expectedEnum.Add((expectedEnumMatch.Index, expectedEnumMatch.Length));
        }

        var actualEnum = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var actualEnumMatch in enumRegex.EnumerateMatches(input))
        {
            actualEnum.Add((actualEnumMatch.IndexInUtf16, actualEnumMatch.LengthInUtf16));
        }

        Assert.Equal(expectedEnum.Count, actualEnum.Count);
        for (var i = 0; i < expectedEnum.Count; i++)
        {
            Assert.Equal(expectedEnum[i].Index, actualEnum[i].Index);
            Assert.Equal(expectedEnum[i].Length, actualEnum[i].Length);
        }

        Assert.False(enumRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FoldedPrefixKeepsBclShapingForKelvin(bool compiled)
    {
        // Declared R05 debt: the Kelvin fence diverts folded-prefix matching to BCL
        // verification when the pattern holds k/K and the input holds U+212A, since
        // byte folding cannot reproduce the invariant fold. Parity holds;
        // construction is pinned.
        const string pattern = "^kil";
        const string text = "\u212Ailo";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
        var enumRegex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((expectedMatch.Index, expectedMatch.Length));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in enumRegex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        Assert.True(enumRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedSuffixCases()
    {
        string[] patterns = ["ab$", "ab\\z"];
        string[] texts = ["xxAB", "ABxx", "ab", "AB\n", "éAB", "ABé"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(FoldedSuffixCases))]
    public void FoldedSuffixDiscoveryOwnsInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(FoldedSuffixCases))]
    public void FoldedSuffixEnumerationOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Folded suffixes enumerate the single tail hit through the folded anchor
        // cursor with per-byte invariant folding and owned UTF-16 start mapping.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((expectedMatch.Index, expectedMatch.Length));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in regex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FoldedSuffixKeepsBclShapingForKelvin(bool compiled)
    {
        // Declared R05 debt: the Kelvin fence diverts folded-suffix matching to BCL
        // verification when the pattern holds k/K and the input holds U+212A, since
        // byte folding cannot reproduce the invariant fold. Parity holds;
        // construction is pinned.
        const string pattern = "k$";
        const string text = "xK";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
        var enumRegex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((expectedMatch.Index, expectedMatch.Length));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in enumRegex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        Assert.True(enumRegex.Inspection.DebugFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> FoldedSplitCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] texts = ["ABxx", "xxAB", "ab", "AB\n", "éAB", "ABé"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(FoldedSplitCases))]
    public void FoldedSplitOwnsInput(string pattern, RegexOptions options, string text)
    {
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], start, end - start));
        }

        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("^kil", "Kilo")]
    [InlineData("kil$", "xKil")]
    public void FoldedSplitUsesOwnedShapingForKelvin(string pattern, string text)
    {
        // Invariant Kelvin folding and split projection use the owned unit cursor.
        const int count = int.MaxValue;
        foreach (var options in new[] { RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);

            var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
            foreach (var range in oracle.EnumerateSplits(text, count))
            {
                var start = range.Start.Value;
                var end = range.End.Value;
                expected.Add((text[start..end], start, end - start));
            }

            var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
            foreach (var split in regex.EnumerateSplits(input, count))
            {
                actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
            }

            Assert.Equal(expected, actual);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }
    public static System.Collections.Generic.IEnumerable<object?[]> FoldedReplaceCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] templates = ["x", "<$1>", "$&", "$`", "$\u0027", "$_", "$+", "$$"];
        string[] texts = ["ABxx", "xxAB", "ab", "AB\n", "éAB", "ABé"];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var text in texts)
                {
                    yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, template, text };
                    yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, template, text };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(FoldedReplaceCases))]
    public void FoldedReplaceOwnsInput(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedDetailedEvaluatorCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] texts = ["ABxx", "xxAB", "ab", "AB\n", "éAB", "ABé"];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, text };
                yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(FoldedDetailedEvaluatorCases))]
    public void FoldedDetailedOwnsInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Value, actual.GetValueString());
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(FoldedDetailedEvaluatorCases))]
    public void FoldedEvaluatorOwnsInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "<" + match.Value + ">");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("<"u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append(">"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "<" + context.GetGroup(0).GetValueString() + ">");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("^kil", "Kilo")]
    [InlineData("kil$", "xKil")]
    public void FoldedReplaceKeepsBclShapingForKelvin(string pattern, string text)
    {
        // Declared R05 debt: the replacement Kelvin fence diverts folded anchored
        // templates to BCL expansion when the pattern holds k/K and the input holds
        // U+212A. Parity holds; construction is pinned.
        foreach (var options in new[] { RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);
            var expected = oracle.Replace(text, "q$&q");
            var actual = regex.ReplaceToString(input, "q$&q");
            Assert.Equal(expected, actual);
            Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }
    public static System.Collections.Generic.IEnumerable<object?[]> FoldedFromOffsetCases()
    {
        string[] patterns = ["^ab", "\\Aab", "ab$", "ab\\z"];
        string[] texts = ["ABxx", "xxAB", "ab"];
        int[] offsets = [0, 1, 2];
        foreach (var pattern in patterns)
        {
            foreach (var text in texts)
            {
                foreach (var offset in offsets)
                {
                    yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, text, offset };
                    yield return new object?[] { pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, text, offset };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(FoldedFromOffsetCases))]
    public void FoldedFromOffsetMatchOwnsInput(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text, utf16Offset);
        var actual = regex.MatchFromUtf16Offset(input, utf16Offset);

        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Length, actual.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    [Theory]
    [MemberData(nameof(FoldedFromOffsetCases))]
    public void FoldedFromOffsetDetailedOwnsInput(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text, utf16Offset);
        var actual = regex.MatchDetailedFromUtf16Offset(input, utf16Offset);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Value, actual.GetValueString());
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("^kil", "Kilo", 1)]
    [InlineData("kil$", "xKil", 1)]
    public void FoldedFromOffsetUsesOwnedShapingForKelvin(string pattern, string text, int utf16Offset)
    {
        // Owned invariant folding includes the Kelvin equivalence at every unit start.
        foreach (var options in new[] { RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);
            var expected = oracle.Match(text, utf16Offset);
            var actual = regex.MatchFromUtf16Offset(input, utf16Offset);
            Assert.Equal(expected.Success, actual.Success);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredDiscoveryOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?m:^ab$)", "ab\ncd"),
            ("(?m:^ab$)", "ab\nab"),
            ("(?m:^ab$)", "xxab"),
            ("(?m:^ab$)", "ab"),
            ("(?m:^ab$)", ""),
            ("(?m:^ab$)", "ab\nab\nab"),
            ("(?m:^ab)", "ab\ncd"),
            ("(?m:^ab)", "xxab\nab"),
            ("(?m:^ab)", "xxab"),
            ("(?m:^ab)", "ab"),
            ("(?m:^ab)", ""),
            ("(?m:^ab)", "ab\nab\nab"),
            ("(?m:^ab)", "ab\nab"),
            ("(?m:ab$)", "ab\ncd"),
            ("(?m:ab$)", "xxab"),
            ("(?m:ab$)", "ab\nab"),
            ("(?m:ab$)", "ab"),
            ("(?m:ab$)", ""),
            ("(?m:ab$)", "ab\ncd\nab"),
            ("(?m:^b)", "\nb"),
            ("(?m:^b)", "\r\nb"),
            ("(?m:a$)", "a\n"),
            ("(?m:a$)", "a\r\n"),
            ("(?m:ab$)", "xxab\nab"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }
    [Theory]
    [MemberData(nameof(LineAnchoredDiscoveryOwnedCases))]
    public void LineAnchoredDiscoveryOwnsInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Index, actualDetailed.IndexInUtf16);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredEnumerationOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?m:^ab$)", "ab\nab"),
            ("(?m:^ab$)", "ab\nab\nab"),
            ("(?m:^ab)", "ab\nab"),
            ("(?m:^ab)", "xxab\nab"),
            ("(?m:ab$)", "ab\nab"),
            ("(?m:ab$)", "xxab\nab"),
            ("(?m:^ab)", "éab\nab\nab"),
            ("(?m:ab$)", "ab\néab"),
            ("(?m:^ab)", "a\U0001F600\nab"),
            ("(?m:ab$)", "ab\n\U0001F600ab"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }
    [Theory]
    [MemberData(nameof(LineAnchoredEnumerationOwnedCases))]
    public void LineAnchoredEnumerationOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: line-anchored enumeration serves owned through the interpreter and
        // linear cursors, which enforce line edges per candidate on every input.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text))
        {
            expected.Add(m.Index + ":" + m.Length);
        }
        var actual = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatches(input))
        {
            actual.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredFromOffsetOwnedCases()
    {
        (string pattern, string text, int utf16Offset)[] rows =
        [
            ("(?m:^ab)", "ab\nab", 0),
            ("(?m:^ab)", "ab\nab", 1),
            ("(?m:^ab)", "ab\nab", 3),
            ("(?m:^ab)", "ab\nab", 5),
            ("(?m:^ab)", "xxab\nab", 0),
            ("(?m:^ab)", "xxab\nab", 1),
            ("(?m:^ab)", "xxab\nab", 3),
            ("(?m:^ab)", "xxab\nab", 5),
            ("(?m:ab$)", "ab\nab", 0),
            ("(?m:ab$)", "ab\nab", 1),
            ("(?m:ab$)", "ab\nab", 3),
            ("(?m:ab$)", "ab\nab", 5),
            ("(?m:ab$)", "xxab\nab", 0),
            ("(?m:ab$)", "xxab\nab", 1),
            ("(?m:ab$)", "xxab\nab", 3),
            ("(?m:ab$)", "xxab\nab", 5),
            ("(?m:^ab$)", "ab\nab", 0),
            ("(?m:^ab$)", "ab\nab", 3),
            ("(?m:^ab$)", "xxab\nab", 0),
            ("(?m:^ab$)", "xxab\nab", 5),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.utf16Offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.utf16Offset };
        }
    }
    [Theory]
    [MemberData(nameof(LineAnchoredFromOffsetOwnedCases))]
    public void LineAnchoredFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        // R05: line-anchored FromOffset discovery serves owned through the shared
        // interpreter from the start unit with whole-input anchor checks.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, utf16Offset), isMatchRegex.IsMatchFromUtf16Offset(input, utf16Offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text, utf16Offset).Count, countRegex.CountFromUtf16Offset(input, utf16Offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, utf16Offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, utf16Offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Index, actualDetailed.IndexInUtf16);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredFromOffsetNonAsciiDeclineCases()
    {
        (string pattern, string text, int utf16Offset)[] rows =
        [
            ("(?m:^ab)", "éab\nab", 4),
            ("(?m:ab$)", "ab\néab", 0),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.utf16Offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.utf16Offset };
        }
    }
    [Theory]
    [MemberData(nameof(LineAnchoredFromOffsetNonAsciiDeclineCases))]
    public void LineAnchoredFromOffsetNonAsciiUsesOwnedShaping(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        // Discovery retains the full subject and UTF-16 origin in the owned VM.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, utf16Offset), isMatchRegex.IsMatchFromUtf16Offset(input, utf16Offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text, utf16Offset).Count, countRegex.CountFromUtf16Offset(input, utf16Offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredFromOffsetEnumerationOwnedCases()
    {
        (string pattern, string text, int utf16Offset)[] rows =
        [
            ("(?m:^ab)", "ab\nab", 1),
            ("(?m:^ab)", "ab\nab", 3),
            ("(?m:^ab)", "ab\nab", 5),
            ("(?m:^ab)", "xxab\nab", 1),
            ("(?m:^ab)", "xxab\nab", 3),
            ("(?m:^ab)", "xxab\nab", 5),
            ("(?m:ab$)", "ab\nab", 1),
            ("(?m:ab$)", "ab\nab", 3),
            ("(?m:ab$)", "ab\nab", 5),
            ("(?m:ab$)", "xxab\nab", 1),
            ("(?m:ab$)", "xxab\nab", 3),
            ("(?m:ab$)", "xxab\nab", 5),
            ("(?m:^ab$)", "ab\nab", 1),
            ("(?m:^ab$)", "ab\nab", 3),
            ("(?m:^ab$)", "xxab\nab", 1),
            ("(?m:^ab$)", "xxab\nab", 5),
            ("(?m:ab$)", "ab\néab", 4),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.utf16Offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.utf16Offset };
        }
    }
    [Theory]
    [MemberData(nameof(LineAnchoredFromOffsetEnumerationOwnedCases))]
    public void LineAnchoredFromOffsetEnumerationOwnsInput(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        // R05: line-anchored FromOffset enumeration scans the full input from the start
        // byte through the floored owned cursor, so anchors keep true left context.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, utf16Offset))
        {
            expected.Add(m.Index + ":" + m.Length);
        }
        var actual = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatchesFromUtf16Offset(input, utf16Offset))
        {
            actual.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredFromOffsetEnumerationNonAsciiDeclineCases()
    {
        (string pattern, string text, int utf16Offset)[] rows =
        [
            ("(?m:^ab)", "éab\nab", 1),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.utf16Offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.utf16Offset };
        }
    }
    [Theory]
    [MemberData(nameof(LineAnchoredFromOffsetEnumerationNonAsciiDeclineCases))]
    public void LineAnchoredFromOffsetEnumerationNonAsciiUsesOwnedShaping(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        // Enumeration resumes on the original owned unit subject.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, utf16Offset))
        {
            expected.Add(m.Index + ":" + m.Length);
        }
        var actual = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatchesFromUtf16Offset(input, utf16Offset))
        {
            actual.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredSeparatorResumeOwnedCases()
    {
        (string pattern, string text, int utf16Offset)[] rows =
        [
            ("(?m:^ab)", "ab\r\nab", 0),
            ("(?m:^ab)", "ab\r\nab", 2),
            ("(?m:^ab)", "ab\r\nab", 3),
            ("(?m:^ab)", "ab\r\nab", 4),
            ("(?m:^ab)", "ab\r\nab", 6),
            ("(?m:^ab)", "ab\rab", 0),
            ("(?m:^ab)", "ab\rab", 2),
            ("(?m:^ab)", "ab\rab", 3),
            ("(?m:^ab)", "ab\rab", 5),
            ("(?m:^ab)", "ab\nab\n", 0),
            ("(?m:^ab)", "ab\nab\n", 3),
            ("(?m:^ab)", "ab\nab\n", 5),
            ("(?m:^ab)", "ab\nab\n", 6),
            ("(?m:^ab)", "\nab\nab", 0),
            ("(?m:^ab)", "\nab\nab", 1),
            ("(?m:^ab)", "\nab\nab", 4),
            ("(?m:^ab)", "\nab\nab", 6),
            ("(?m:^ab)", "ab\n\nab", 0),
            ("(?m:^ab)", "ab\n\nab", 3),
            ("(?m:^ab)", "ab\n\nab", 4),
            ("(?m:^ab)", "ab\n\nab", 6),
            ("(?m:ab$)", "ab\r\nab", 0),
            ("(?m:ab$)", "ab\r\nab", 2),
            ("(?m:ab$)", "ab\r\nab", 3),
            ("(?m:ab$)", "ab\r\nab", 4),
            ("(?m:ab$)", "ab\r\nab", 6),
            ("(?m:ab$)", "ab\rab", 0),
            ("(?m:ab$)", "ab\rab", 2),
            ("(?m:ab$)", "ab\rab", 3),
            ("(?m:ab$)", "ab\rab", 5),
            ("(?m:ab$)", "ab\nab\n", 0),
            ("(?m:ab$)", "ab\nab\n", 3),
            ("(?m:ab$)", "ab\nab\n", 5),
            ("(?m:ab$)", "ab\nab\n", 6),
            ("(?m:ab$)", "\nab\nab", 0),
            ("(?m:ab$)", "\nab\nab", 1),
            ("(?m:ab$)", "\nab\nab", 4),
            ("(?m:ab$)", "\nab\nab", 6),
            ("(?m:ab$)", "ab\n\nab", 0),
            ("(?m:ab$)", "ab\n\nab", 3),
            ("(?m:ab$)", "ab\n\nab", 4),
            ("(?m:ab$)", "ab\n\nab", 6),
            ("(?m:^ab$)", "ab\r\nab", 0),
            ("(?m:^ab$)", "ab\r\nab", 2),
            ("(?m:^ab$)", "ab\r\nab", 3),
            ("(?m:^ab$)", "ab\r\nab", 4),
            ("(?m:^ab$)", "ab\r\nab", 6),
            ("(?m:^ab$)", "ab\rab", 0),
            ("(?m:^ab$)", "ab\rab", 2),
            ("(?m:^ab$)", "ab\rab", 3),
            ("(?m:^ab$)", "ab\rab", 5),
            ("(?m:^ab$)", "ab\nab\n", 0),
            ("(?m:^ab$)", "ab\nab\n", 3),
            ("(?m:^ab$)", "ab\nab\n", 5),
            ("(?m:^ab$)", "ab\nab\n", 6),
            ("(?m:^ab$)", "\nab\nab", 0),
            ("(?m:^ab$)", "\nab\nab", 1),
            ("(?m:^ab$)", "\nab\nab", 4),
            ("(?m:^ab$)", "\nab\nab", 6),
            ("(?m:^ab$)", "ab\n\nab", 0),
            ("(?m:^ab$)", "ab\n\nab", 3),
            ("(?m:^ab$)", "ab\n\nab", 4),
            ("(?m:^ab$)", "ab\n\nab", 6),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.utf16Offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.utf16Offset };
        }
    }

    [Theory]
    [MemberData(nameof(LineAnchoredSeparatorResumeOwnedCases))]
    public void LineAnchoredSeparatorResumeOwnsInput(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        // R05: line-anchored resumption serves owned across the separator taxonomy on
        // ASCII input: CRLF pairs (caret after the pair, dollar before its CR), lone CR
        // (no line break for either anchor), trailing and leading line feeds, and empty
        // lines. Resume offsets cover separator interiors, line starts, and the end;
        // per-row oracle values pin the exact hit sets on all five FromOffset lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, utf16Offset), isMatchRegex.IsMatchFromUtf16Offset(input, utf16Offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text, utf16Offset).Count, countRegex.CountFromUtf16Offset(input, utf16Offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, utf16Offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, utf16Offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Index, actualDetailed.IndexInUtf16);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var enumerateRegex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, utf16Offset))
        {
            expected.Add(m.Index + ":" + m.Length);
        }
        var actual = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatchesFromUtf16Offset(input, utf16Offset))
        {
            actual.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expected, actual);
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredAstralResumeShapingCases()
    {
        (string pattern, string text, int utf16Offset)[] rows =
        [
            ("(?m:^ab)", "a\U0001F600b\nab", 0),
            ("(?m:^ab)", "a\U0001F600b\nab", 3),
            ("(?m:^ab$)", "a\U0001F600b\nab", 0),
            ("(?m:^ab$)", "a\U0001F600b\nab", 5),
            ("(?m:ab$)", "a\U0001F600b\nab", 1),
            ("(?m:ab$)", "a\U0001F600b\nab", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.utf16Offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.utf16Offset };
        }
    }

    [Theory]
    [MemberData(nameof(LineAnchoredAstralResumeShapingCases))]
    public void LineAnchoredAstralResumeUsesOwnedUnitSubject(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        // Discovery, shaping and enumeration resume at every original UTF-16 unit.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, utf16Offset), isMatchRegex.IsMatchFromUtf16Offset(input, utf16Offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text, utf16Offset).Count, countRegex.CountFromUtf16Offset(input, utf16Offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, utf16Offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, utf16Offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Index, actualDetailed.IndexInUtf16);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var enumerateRegex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, utf16Offset))
        {
            expected.Add(m.Index + ":" + m.Length);
        }
        var actual = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatchesFromUtf16Offset(input, utf16Offset))
        {
            actual.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expected, actual);
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> LineAnchoredEmptyAstralResumeOwnedCases()
    {
        (string pattern, string text, int utf16Offset)[] rows =
        [
            ("(?m:^)", "a\U0001F600b\nab", 1),
            ("(?m:^)", "a\U0001F600b\nab", 2),
            ("(?m:^)", "a\U0001F600b\nab", 5),
            ("(?m:$)", "a\U0001F600b\nab", 1),
            ("(?m:$)", "a\U0001F600b\nab", 2),
            ("(?m:$)", "a\U0001F600b\nab", 5),
            ("(?m:^$)", "a\U0001F600b\nab", 1),
            ("(?m:^$)", "a\U0001F600b\nab", 2),
            ("(?m:^$)", "a\U0001F600b\nab", 5),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.utf16Offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.utf16Offset };
        }
    }

    [Theory]
    [MemberData(nameof(LineAnchoredEmptyAstralResumeOwnedCases))]
    public void LineAnchoredEmptyAstralResumeOwnsInput(string pattern, RegexOptions options, string text, int utf16Offset)
    {
        // R05: empty line anchors resume fully owned even on astral-adjacent input,
        // including the half-scalar offset 2: with no literal search there is no
        // byte-start boundary to resolve, so neither the discovery decline nor the
        // resume gate diverts. Parity holds on all five FromOffset lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, utf16Offset), isMatchRegex.IsMatchFromUtf16Offset(input, utf16Offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text, utf16Offset).Count, countRegex.CountFromUtf16Offset(input, utf16Offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, utf16Offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, utf16Offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, utf16Offset);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Index, actualDetailed.IndexInUtf16);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var enumerateRegex = new Utf8Regex(pattern, options);
        var expected = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, utf16Offset))
        {
            expected.Add(m.Index + ":" + m.Length);
        }
        var actual = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatchesFromUtf16Offset(input, utf16Offset))
        {
            actual.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expected, actual);
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
