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
    [Theory]
    [InlineData("(?m:^ab)", "ab\ncd")]
    [InlineData("(?m:^ab)", "xxab")]
    [InlineData("(?m:ab$)", "ab\ncd")]
    [InlineData("(?m:ab$)", "xxab")]
    public void MultilineAnchoredSplitKeepsBclShaping(string pattern, string text)
    {
        // Declared R05 debt: line-anchored shapes are not absolute-anchored direct
        // families, so splits stay BCL-backed. Parity holds; construction is pinned.
        const int count = int.MaxValue;
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
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
            Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
        }
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
    public void FoldedSuffixEnumerationKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: folded suffix enumeration has no anchor-enforcing folded
        // cursor, so it stays BCL-backed on both lanes. Parity holds; construction
        // is pinned.
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

        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
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
    }
}
