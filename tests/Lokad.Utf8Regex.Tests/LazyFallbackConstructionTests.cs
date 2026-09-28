using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 lazy fallback construction: building a Utf8Regex never constructs the BCL engine,
/// and owned routes across every public operation never construct it either. The first
/// fallback use constructs once. This is the construction half of the R03 engine gate.
/// </summary>
public sealed class LazyFallbackConstructionTests
{
    public static IEnumerable<object?[]> FreshCases()
    {
        string[] patterns =
        [
            "abc",
            "cat|horse",
            "(cat|horse)",
            "(?<w>cat)|(?<v>horse)",
            "(cat)|(horse)",
            "(caf\u00E9)|(ni\u00F1o)",
            "(a)",
            "(?:cat|horse)(?=horse)",
            "\\b(?:cat|horse)\\b",
            "^abc",
            "abc$",
            "\\bword\\b",
            "[a-z]+",
            "(a)(b)",
            "(a)+",
            "a*",
            "\\d+",
            "\U0001F600",
        ];
        RegexOptions[] options =
        [
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.None,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        ];
        foreach (var pattern in patterns)
        {
            foreach (var option in options)
            {
                yield return new object?[] { pattern, option };
            }
        }
    }

    [Theory]
    [MemberData(nameof(FreshCases))]
    public void FreshInstanceConstructsNoFallbackEngine(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> OwnedCases()
    {
        string[] patterns =
        [
            "abc",
            "cat|horse",
            "(cat|horse)",
            "(?<w>cat)|(?<v>horse)",
            "(cat)|(horse)",
            "(caf\u00E9)|(ni\u00F1o)",
            "(a)",
            "(?:cat|horse)(?=horse)",
        ];
        string[] asciiInputs =
        [
            "cat horse fish horse",
            "horsecat",
            "no match here",
            "",
            "CAT horse Cat",
        ];
        // ASCII-literal shapes divert enumeration to BCL verification on non-ASCII
        // input (established R03 projection rule), so only the rest take unicode input.
        string[] unicodeInputs =
        [
            "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3r\u00E9sum\u00E9\u03B4",
        ];
        string[] asciiOnlyPatterns = ["abc", "(a)"];
        foreach (var pattern in patterns)
        {
            foreach (var input in asciiInputs)
            {
                yield return new object?[] { pattern, RegexOptions.CultureInvariant, input };
                yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, input };
            }

            if (System.Array.IndexOf(asciiOnlyPatterns, pattern) < 0)
            {
                foreach (var input in unicodeInputs)
                {
                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, input };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, input };
                }
            }
        }

        yield return new object?[] { "(CAT)|(DOG)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "cat dog fish" };
        yield return new object?[] { "cat|horse", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "CAT horse" };
    }

    [Theory]
    [MemberData(nameof(OwnedCases))]
    public void OwnedOperationsConstructNoFallbackEngine(string pattern, RegexOptions options, string inputText)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        _ = regex.IsMatch(input);
        _ = regex.Count(input);
        _ = regex.Match(input);
        foreach (var item in regex.EnumerateMatches(input)) { _ = item; }
        foreach (var item in regex.EnumerateSplits(input)) { _ = item; }
        Touch(regex.MatchDetailed(input));
        Touch(regex.MatchDetailedFromUtf16Offset(input, 0));

        _ = regex.Replace(input, "X");
        _ = regex.Replace(input, Encoding.UTF8.GetBytes("X"));
        _ = regex.ReplaceToString(input, "X");
        if (inputText.Length > 0)
        {
            // Empty-input evaluator Replace throws on the writer capacity (pre-existing).
            _ = regex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { writer.Append(Encoding.UTF8.GetBytes("X")); });
            _ = regex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "X");
        }

        var destination = new byte[Math.Max(input.Length * 4, 16)];
        _ = regex.TryReplace(input, "X", destination, out _);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated, "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
    }

    public static IEnumerable<object?[]> OwnedFromOffsetCases()
    {
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant, "cat horse fish horse", 1 };
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant, "cat horse fish horse", 5 };
        yield return new object?[] { "(cat|horse)", RegexOptions.CultureInvariant, "cat horse fish horse", 4 };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "cat horse fish horse", 4 };
        yield return new object?[] { "(?<w>cat)|(?<v>horse)", RegexOptions.CultureInvariant, "cat horse fish", 4 };
        yield return new object?[] { "(a)", RegexOptions.CultureInvariant, "baac", 2 };
        yield return new object?[] { "(?:cat|horse)(?=horse)", RegexOptions.CultureInvariant, "cathorsecathorse", 3 };
        yield return new object?[] { "^abc", RegexOptions.CultureInvariant, "xxabc", 1 };
        yield return new object?[] { "^abc", RegexOptions.CultureInvariant, "abcxx", 0 };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", RegexOptions.None, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 2 };
    }

    [Theory]
    [MemberData(nameof(OwnedFromOffsetCases))]
    public void OwnedFromOffsetConstructsNoFallbackEngine(string pattern, RegexOptions options, string inputText, int offset)
    {
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        Touch(regex.MatchDetailedFromUtf16Offset(input, offset));
        foreach (var item in regex.EnumerateMatchesFromUtf16Offset(input, offset)) { _ = item; }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern + " offset=" + offset.ToString());
    }

    [Theory]
    [InlineData("\\b(?:cat|horse)\\b", "CAT horse Cat")]
    public void OriginSensitiveFromOffsetConstructsFallbackEngine(string pattern, string inputText)
    {
        // From-offset origin shapes that the slice cannot serve (boundaries,
        // non-slice lookbehind, non-prefix anchors) divert to full-input BCL
        // verification inside the shared enumerator (established taxonomy).
        // Anchored ASCII literal prefixes (^abc) graduated to the owned
        // prefix-family cursor and live in OwnedFromOffsetCases instead.
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Touch(regex.MatchDetailedFromUtf16Offset(input, 1));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<=a)(b)|(c)", "xabc", 1, true, 2, 3)]
    [InlineData("(?<=a)(b)|(c)", "xabc", 3, true, 3, 4)]
    [InlineData("(?<=a)(b)|(c)", "xab", 3, false, 0, 0)]
    public void OriginSensitiveLookbehindFromOffsetStaysOwned(
        string pattern, string inputText, int offset, bool expectedSuccess, int expectedIndex, int expectedEnd)
    {
        // Slice lookbehind searches backward from the attempt origin with
        // ordinal group metadata, so from-offset detailed matches keep full
        // left context with no fallback construction.
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText, offset);
        var actual = regex.MatchDetailedFromUtf16Offset(input, offset);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.Equal(expectedSuccess, actual.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedIndex, actual.GetGroup(0).IndexInUtf16);
            Assert.Equal(expectedEnd, actual.GetGroup(0).IndexInUtf16 + actual.GetGroup(0).LengthInUtf16);
            for (var group = 1; group <= 2; group++)
            {
                Assert.Equal(oracle.Groups[group].Success, actual.GetGroup(group).Success);
                if (oracle.Groups[group].Success)
                {
                    Assert.Equal(oracle.Groups[group].Value, actual.GetGroup(group).GetValueString());
                }
            }
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)??b", "xxabxx")]
    [InlineData("(a)??b(c)??", "xxabcxx")]
    [InlineData("(a){0,1}?b", "xxabxx")]
    public void LazyOptionalCompiledDiscoveryStaysOwned(string pattern, string text)
    {
        // Single-maximum lazies (??, {0,1}?) agree on both oracle lanes with
        // full group detail (probe-mapped, including inner captures), so the
        // slice serves discovery owned on both lanes with parity and no
        // fallback construction. Finite maxima above one stay BCL-backed on
        // the compiled lane (see FiniteRangeLazyCompiledDiscoveryKeepsBclShaping).
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);

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
        }
    }

    [Theory]
    [InlineData("(a{2}?b)", "xxaabxx")]
    [InlineData("(a{2}?b)", "xxabxx")]
    [InlineData("([0-9]){2}?x", "ab12xcd")]
    [InlineData("(a{2,2}?b)", "xxaabxx")]
    public void ExactCountLazyCompiledDiscoveryStaysOwned(string pattern, string text)
    {
        // Exact-count lazies agree on both oracle lanes with full group detail
        // (probe-mapped, including inner captures, nesting, and backreferences),
        // so the slice serves discovery owned on both lanes with parity and no
        // fallback construction.
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);

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
        }
    }

    [Theory]
    [InlineData("(a{1,3}?b)?c", "aabc")]
    [InlineData("(a{1,3}?b)?c", "xyz")]
    [InlineData("([0-9]{1,3}?x)?", "12x")]
    [InlineData("([0-9]{1,3}?x)?", "123")]
    public void CaptureFreeLazyRangeCompiledDiscoveryStaysOwned(string pattern, string text)
    {
        // Capture-free lazy ranges agree on both oracle lanes with full group
        // detail (probe-mapped, including the abandonment-trigger input "123" that
        // diverges its captured counterpart), so the slice serves discovery owned on
        // both lanes with parity and no fallback construction.
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);

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
        }
    }

    [Theory]
    [InlineData("(([0-9]){1,2}?x)?", "123")]
    [InlineData("(([0-9]){1,2}?x)?", "12x")]
    public void FiniteRangeLazyCompiledDiscoveryKeepsBclShaping(string pattern, string text)
    {
        // Finite lazy maxima above one stay BCL-backed for discovery on the
        // compiled lane: the compiler keeps abandonment progress there while
        // the interpreter abandons fully, and captured ranges even oscillate
        // compiled enumeration forever (probed: 0+0,0+1,...). IsMatch
        // terminates on both lanes so parity is asserted; full enumeration is
        // deliberately not compared because the BCL-compiled lane never finishes.
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var expectConstruction = (options & RegexOptions.Compiled) != 0;
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(expectConstruction, regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }

    [Theory]
    [InlineData("a+?b", "xxaaabxx")]
    public void LazyPlusCompiledDiscoveryStaysOwned(string pattern, string text)
    {
        // Control: lazy-plus shapes execute owned discovery on both lanes, so the
        // remaining compiled debt is specific to capture-writing finite lazy ranges
        // (see FiniteRangeLazyCompiledDiscoveryKeepsBclShaping).
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        }
    }

    [Theory]
    [InlineData("(a)??b(c)??d(e)??f(g)??h(i)??", "abcdefghi")]
    public void LazyOptionalDetailedStaysOwned(string pattern, string inputText)
    {
        // Single-maximum lazies (??) serve Detailed owned on the compiled lane too
        // (probe-mapped lane agreement); finite lazy maxima above one stay BCL-backed
        // there. Greedy six-loop shapes and below graduate to owned shaping in
        // GroupedLiteralDetailedTests, and lazy shapes stay owned on the ordinary
        // lane through the slice.
        var regex = new Utf8Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a.c", "xxabc")]
    public void AnchoredFallbackConstructsAnchoredEngine(string pattern, string inputText)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        _ = regex.Match(input);
        // Match on anchored shapes without a direct prefix family runs verification
        // through the anchored engine only: the primary engine is never constructed by
        // this operation. Bare anchored literals verify directly with neither engine
        // (see Utf8AnchoredLiteralPrefixTests).
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        Assert.True(regex.Inspection.DebugAnchoredFallbackRegexIsCreated, "pattern=" + pattern);
    }


    public static IEnumerable<object?[]> OwnedSliceFromOffsetCases()
    {
        // Slice shapes only: plain literal alternations take specialized literal
        // routes (no slice program); the shapes below are the R02a fallback-kind
        // slice catalog from Utf8SliceWiringTests plus an astral-bodied repeat.
        RegexOptions[] selections =
        [
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        ];
        (string pattern, string input, int[] offsets)[] rows =
        [
            ("(a+)(b+)", "xxaaabbyy", [0, 2, 3]),
            ("(ab|cd)+", "xxabcdcdyy", [0, 2, 4]),
            ("(a|ab)+c", "xxabcabccyy", [0, 2, 5]),
            ("(ab|a)+z", "ababaz", [0, 2]),
            ("a*?b", "xxaaabyy", [0, 3]),
            ("(a{2,3}b+)|(c+d)", "xxaabbbyy", [0, 2]),
            ("(a+)(b+)", "\U0001F600aaabb\U0001F600", [0, 2]),
        ];
        foreach (var (pattern, input, offsets) in rows)
        {
            foreach (var options in selections)
            {
                foreach (var offset in offsets)
                {
                    yield return new object?[] { pattern, options, input, offset };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(OwnedSliceFromOffsetCases))]
    public void OwnedSliceFromOffsetOperationsStayOwned(string pattern, RegexOptions options, string inputText, int offset)
    {
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern + " options=" + options.ToString());
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(oracle.IsMatch(inputText, offset), regex.IsMatchFromUtf16Offset(input, offset));
        Assert.Equal(oracle.Matches(inputText, offset).Count, regex.CountFromUtf16Offset(input, offset));

        var expected = oracle.Match(inputText, offset);
        var actual = regex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Length, actual.LengthInUtf16);
        }

        var detailed = regex.MatchDetailedFromUtf16Offset(input, offset);
        Assert.Equal(expected.Success, detailed.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, detailed.IndexInUtf16);
            Assert.Equal(expected.Length, detailed.LengthInUtf16);
        }

        Touch(detailed);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern + " options=" + options.ToString() + " offset=" + offset.ToString());
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated, "pattern=" + pattern + " options=" + options.ToString() + " offset=" + offset.ToString());
    }

    private static void Touch(Utf8MatchContext match)
    {
        _ = match.Success;
        _ = match.IndexInUtf16;
        _ = match.GetValueString();
        for (var i = 0; i < match.GroupCount; i++)
        {
            var group = match.GetGroup(i);
            _ = group.GetValueString();
            for (var j = 0; j < group.CaptureCount; j++)
            {
                _ = group.GetCapture(j).GetValueString();
            }
        }
    }
}
