using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// Owned preparation publishes one scalar program and canonical capture catalog.
/// The operation and offset corpus below checks that both remain available after use.
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
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
        // Preserve this corpus; broader mixed scalar/native literal coverage lives
        // in the scalar and native projection fixtures.
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
            // Empty-input evaluator cases are covered by scalar replacement fixtures.
            _ = regex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { writer.Append(Encoding.UTF8.GetBytes("X")); });
            _ = regex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "X");
        }

        var destination = new byte[Math.Max(input.Length * 4, 16)];
        _ = regex.TryReplace(input, "X", destination, out _);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog), "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern + " offset=" + offset.ToString());
    }

    [Theory]
    [InlineData("\\b(?:cat|horse)\\b", "CAT horse Cat")]
    public void OriginSensitiveFromOffsetUsesOwnedProgram(string pattern, string inputText)
    {
        // Boundaries resume on the original owned unit subject.
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Touch(regex.MatchDetailedFromUtf16Offset(input, 1));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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
        // fallback construction. Capture-writing finite ranges also stay owned,
        // following interpreter unwind (see FiniteRangeLazyCompiledDiscoveryUsesStableOwnedUnwind).
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);

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

            Assert.True(enumRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);

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

            Assert.True(enumRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);

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

            Assert.True(enumRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Theory]
    [InlineData("(([0-9]){1,2}?x)?", "123")]
    [InlineData("(([0-9]){1,2}?x)?", "12x")]
    [InlineData("((a){1,3}?x)?", "aaa")]
    [InlineData("((a){0,2}?x)?", "aaa")]
    public void FiniteRangeLazyCompiledDiscoveryUsesStableOwnedUnwind(string pattern, string text)
    {
        // The diagnosed .NET 10 compiled defect retains abandoned progress and
        // may never finish enumeration. Both owned lanes use the interpreter's
        // correct abandonment, including Count and global cursor termination.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
                Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
            }
            Assert.False(actual.MoveNext());
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
    }

    [Theory]
    [InlineData("a+?b", "xxaaabxx")]
    public void LazyPlusCompiledDiscoveryStaysOwned(string pattern, string text)
    {
        // Control: unbounded lazy-plus shapes still execute owned discovery on
        // both lanes and agree with the compiled oracle as well as the interpreter.
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.Compiled | RegexOptions.CultureInvariant })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DivergenceFreeLazyShapingOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a)??b", "xxabxx", "<$1>"),
            ("(a)??b(c)??", "xxabcxx", "<$1$2>"),
            ("(a{1,3}?b)?c", "aabc", "<$1>"),
            ("(a{1,3}?b)?c", "xyz", "<$1>"),
            ("(a)??b", "xa\U0001F600b", "<$1>"),
            ("([0-9]){2}?x", "ab12xcd", "<$1>"),
            ("(a{2}?b)", "xxaabxx", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(DivergenceFreeLazyShapingOwnedCases))]
    public void DivergenceFreeLazyShapingStaysOwned(string pattern, RegexOptions options, string text, string template)
    {
        // R05: divergence-free lazy shapes (single maxima, exact counts, and
        // capture-free ranges) serve shaping owned on both lanes with full group
        // detail: Detailed projects every group with oracle coordinates and values,
        // splits enumerate owned gaps, and all three replacement cores shape from
        // owned spans, on ASCII and astral input alike. These shapes agree with
        // the compiled oracle as well as the interpreter. Capture-writing true
        // ranges also stay owned with the documented interpreted-semantics disposition.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
            for (var number = 0; number < expectedDetailed.Groups.Count; number++)
            {
                var expectedGroup = expectedDetailed.Groups[number];
                var actualGroup = actualDetailed.GetGroup(number);
                Assert.Equal(expectedGroup.Success, actualGroup.Success);
                if (expectedGroup.Success)
                {
                    Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
                    Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
                    Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
                }
            }
        }
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(detailedRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, detailedRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, int.MaxValue))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }
        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }
        Assert.Equal(expectedSplits, actualSplits);
        Assert.True(splitRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(splitRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, splitRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(bytesRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, bytesRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(stringRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, stringRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(destinationRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, destinationRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var enumerateRegex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatches(input))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.True(enumerateRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(enumerateRegex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, enumerateRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [InlineData("(a)??b(c)??d(e)??f(g)??h(i)??", "abcdefghi")]
    public void LazyOptionalDetailedStaysOwned(string pattern, string inputText)
    {
        // Single-maximum lazies (??) serve Detailed owned on the compiled lane too
        // (probe-mapped lane agreement); capture-writing finite lazy ranges use
        // stable interpreter unwind too. Greedy six-loop shapes graduate to owned shaping in
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a.c", "xxabc")]
    public void AnchoredMatchUsesOwnedScalarProgram(string pattern, string inputText)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        _ = regex.Match(input);
        // A failed anchored attempt is definitive in the owned scalar program.
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog), "pattern=" + pattern);
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern + " options=" + options.ToString() + " offset=" + offset.ToString());
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.RuntimeTree!.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog), "pattern=" + pattern + " options=" + options.ToString() + " offset=" + offset.ToString());
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
