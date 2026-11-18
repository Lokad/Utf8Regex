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
        var regex = new Utf8Regex(pattern, options: options);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
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
        var regex = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        _ = regex.IsMatch(input);
        _ = regex.Count(input);
        _ = regex.Match(input);
        foreach (var item in regex.EnumerateMatches(input)) { _ = item; }
        foreach (var item in regex.EnumerateSplits(input)) { _ = item; }
        Touch(Utf8MatchSequence.First(regex.CreateMatchCursor(input)));
        _ = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, 0))));

        _ = regex.Replace(input, "X"u8);
        _ = regex.Replace(input, Encoding.UTF8.GetBytes("X"));
        _ = Encoding.UTF8.GetString(regex.Replace(input, "X"u8));
        var destination = new byte[Math.Max(input.Length * 4, 16)];
        _ = regex.TryReplace(input, "X"u8, destination, out _);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog), "pattern=" + pattern + " options=" + options.ToString() + " input=" + inputText);
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
        var regex = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        _ = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset))));
        foreach (var item in regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)))) { _ = item; }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern + " offset=" + offset.ToString());
    }

    [Theory]
    [InlineData("\\b(?:cat|horse)\\b", "CAT horse Cat")]
    public void OriginSensitiveFromOffsetUsesOwnedProgram(string pattern, string inputText)
    {
        // Boundaries resume on the original owned unit subject.
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        _ = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, 1))));
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText, offset);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset))));
        Utf8MatchSequence.AssertCaptures(oracle, actual, input, inputText);
        Assert.Equal(expectedSuccess, actual.Success);
        if (expectedSuccess) Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expectedIndex)), expectedEnd),
            (actual.Value.StartOffsetInBytes, actual.Value.EndOffsetInBytes));
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

            var isMatchRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var enumRegex = new Utf8Regex(pattern, options: options);
            var expected = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (Match expectedMatch in oracle.Matches(text))
            {
                expected.Add((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length))));
            }

            var actual = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (var valueMatch in enumRegex.EnumerateMatches(input))
            {
                actual.Add((valueMatch.StartOffsetInBytes, (valueMatch.EndOffsetInBytes - valueMatch.StartOffsetInBytes)));
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

            var isMatchRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var enumRegex = new Utf8Regex(pattern, options: options);
            var expected = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (Match expectedMatch in oracle.Matches(text))
            {
                expected.Add((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length))));
            }

            var actual = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (var valueMatch in enumRegex.EnumerateMatches(input))
            {
                actual.Add((valueMatch.StartOffsetInBytes, (valueMatch.EndOffsetInBytes - valueMatch.StartOffsetInBytes)));
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

            var isMatchRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var enumRegex = new Utf8Regex(pattern, options: options);
            var expected = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (Match expectedMatch in oracle.Matches(text))
            {
                expected.Add((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length))));
            }

            var actual = new System.Collections.Generic.List<(int Index, int Length)>();
            foreach (var valueMatch in enumRegex.EnumerateMatches(input))
            {
                actual.Add((valueMatch.StartOffsetInBytes, (valueMatch.EndOffsetInBytes - valueMatch.StartOffsetInBytes)));
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
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input);
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Current.Index)), actual.Current.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Current.Index, expected.Current.Length)), (actual.Current.EndOffsetInBytes - actual.Current.StartOffsetInBytes));
            }
            Assert.False(actual.MoveNext());
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
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

            var isMatchRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

            var countRegex = new Utf8Regex(pattern, options: options);
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
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
            for (var number = 0; number < expectedDetailed.Groups.Count; number++)
            {
                var expectedGroup = expectedDetailed.Groups[number];
                Assert.True(actualDetailed.TryGetGroup(number, out var actualGroup));
                Assert.Equal(expectedGroup.Success, actualGroup.Success);
                if (expectedGroup.Success)
                {
                    Assert.Equal(expectedGroup.Value, System.Text.Encoding.UTF8.GetString(actualDetailed.GetGroupMemory(number).Span));
                }
            }
        }
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(detailedRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, detailedRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var splitRegex = new Utf8Regex(pattern, options: options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, int.MaxValue))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], global::System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), global::System.Text.Encoding.UTF8.GetByteCount(text[start..end])));
        }
        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }
        Assert.Equal(expectedSplits, actualSplits);
        Assert.True(splitRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(splitRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, splitRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(bytesRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, bytesRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(stringRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, stringRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(destinationRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, destinationRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        var enumerateRegex = new Utf8Regex(pattern, options: options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text))
        {
            expectedPositions.Add(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)) + ":" + System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)));
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatches(input))
        {
            actualPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.True(enumerateRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(enumerateRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, enumerateRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [InlineData("(a)??b(c)??d(e)??f(g)??h(i)??", "abcdefghi")]
    public void LazyOptionalDetailedStaysOwned(string pattern, string inputText)
    {
        // Single-maximum lazies (??) serve Detailed owned on the compiled lane too
        // (probe-mapped lane agreement); capture-writing finite lazy ranges use
        // stable interpreter unwind too. Greedy six-loop shapes graduate to owned shaping in
        // GroupedLiteralCaptureTests, and lazy shapes stay owned on the ordinary
        // lane through the slice.
        var regex = new Utf8Regex(pattern, options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(expected.Success, actual.Success);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a.c", "xxabc")]
    public void AnchoredMatchUsesOwnedScalarProgram(string pattern, string inputText)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        _ = regex.Match(input);
        // A failed anchored attempt is definitive in the owned scalar program.
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog), "pattern=" + pattern);
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
        var regex = new Utf8Regex(pattern, options: options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern + " options=" + options.ToString());
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.Equal(oracle.IsMatch(inputText, offset), regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []).Success);
        Assert.Equal(oracle.Matches(inputText, offset).Count, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)))));

        var expected = oracle.Match(inputText, offset);
        var actual = regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(inputText.AsSpan(expected.Index, expected.Length)), actual.EndOffsetInBytes - actual.StartOffsetInBytes);
        }

        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset))));
        Utf8MatchSequence.AssertCaptures(expected, detailed, input, inputText);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern + " options=" + options.ToString() + " offset=" + offset.ToString());
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog), "pattern=" + pattern + " options=" + options.ToString() + " offset=" + offset.ToString());
    }

    private static void Touch(Utf8RangeMatch match)
    {

        _ = match.Success;
        _ = match.Value;
        _ = match.GetValueMemory();
        foreach (var number in match.GroupNumbers)
        {
            Assert.True(match.TryGetGroup(number, out _));
            _ = match.GetGroupMemory(number);
            foreach (var capture in match.GetCaptures(number)) _ = capture.StartOffsetInBytes;
        }
    }
}
