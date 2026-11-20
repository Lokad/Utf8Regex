using System.Text;
using System.Text.RegularExpressions;
using static System.Text.RegularExpressions.RegexOptions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class AsciiSimplePatternTests
{

    [Fact]
    public void SupportAnalyzerClassifiesInvariantIgnoreCaseDotPatternAsAsciiSimplePattern()
    {
        var regex = new Utf8Regex("ab.cd", options: RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiteral, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void SupportAnalyzerPreservesPredefinedClassInsideOuterNegation()
    {
        var regex = new Utf8Regex(@"[^\S]", options: RegexOptions.CultureInvariant);
        Assert.Equal(Regex.IsMatch("\u2003", @"[^\S]"), regex.IsMatch("\u2003"u8));
    }

    [Theory]
    [InlineData(@"^\d{1,3}$", "123")]
    [InlineData(@"^\d{1,3}$", "١٢٣")]
    [InlineData(@"^\D{1,3}$", "é")]
    [InlineData(@"^\w{1,3}$", "Ж")]
    [InlineData(@"^\W{1,3}$", "😀")]
    [InlineData(@"^\s{1,3}$", "\u00A0")]
    [InlineData(@"^\s{1,3}$", "\u2003")]
    [InlineData(@"^\S{1,3}$", "Ж")]
    [InlineData(@"^[^\D]{1,3}$", "١٢٣")]
    [InlineData(@"^[^\W]{1,3}$", "Ж")]
    [InlineData(@"^[^\S]{1,3}$", "\u2003")]
    [InlineData(@"^[^x]{1,3}$", "é")]
    public void UnicodeSensitiveAsciiIntersectionPlansMatchDotNet(string pattern, string input)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var expected = new Regex(pattern, options);
            var actual = new Utf8Regex(pattern, options: options);
            var utf8 = System.Text.Encoding.UTF8.GetBytes(input);
            Assert.Equal(expected.IsMatch(input), actual.IsMatch(utf8));
            Assert.Equal(expected.Count(input), actual.Count(utf8));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodeSensitiveAsciiIntersectionPlansPreserveGlobalOperations(bool compiled)
    {
        const string pattern = @"\d{2}";
        const string input = "a12 b١٢ c३४";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options: options);
        var utf8 = System.Text.Encoding.UTF8.GetBytes(input);
        var actualMatches = new List<string>();
        var actualSplits = new List<string>();

        foreach (var match in actual.EnumerateMatches(utf8))
        {
            actualMatches.Add(System.Text.Encoding.UTF8.GetString(
                utf8.AsSpan(match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))));
        }

        foreach (var split in actual.EnumerateSplits(utf8))
        {
            actualSplits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        }

        Span<byte> destination = stackalloc byte[utf8.Length];
        var replaceStatus = actual.TryReplace(utf8, "#"u8, destination, out var bytesWritten);
        var expectedReplacement = expected.Replace(input, "#");
        Assert.Equal(expected.Count(input), actual.Count(utf8));
        Assert.Equal(expected.Matches(input).Select(static match => match.Value), actualMatches);
        Assert.Equal(expectedReplacement, System.Text.Encoding.UTF8.GetString(actual.Replace(utf8, "#"u8)));
        Assert.Equal(System.Buffers.OperationStatus.Done, replaceStatus);
        Assert.Equal(expectedReplacement, System.Text.Encoding.UTF8.GetString(destination[..bytesWritten]));
        Assert.Equal(expected.Split(input), actualSplits);
    }

    [Fact]
    public void SupportAnalyzerPromotesIgnoreCaseLiteralBranchesFromOptionalAsciiPattern()
    {
        var analysis = Utf8FrontEnd.Compile("ab?c", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, analysis.ExecutionKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, analysis.SearchPlan.Kind);
    }

    [Fact]
    public void SupportAnalyzerPromotesIgnoreCaseLiteralBranchesFromFiniteAsciiCharClassPattern()
    {
        var analysis = Utf8FrontEnd.Compile("a[bc]?d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, analysis.ExecutionKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, analysis.SearchPlan.Kind);
    }

    [Fact]
    public void SupportAnalyzerPromotesIgnoreCaseLiteralBranchesFromFiniteAsciiCharClassPatternWithoutOptional()
    {
        var analysis = Utf8FrontEnd.Compile("a[bc]d", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, analysis.ExecutionKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiterals, analysis.SearchPlan.Kind);
    }

    [Theory]
    [InlineData("a[bc]d")]
    [InlineData("ab?c")]
    [InlineData("a[bc]?d")]
    [InlineData(@"[ab]{2}")]
    [InlineData(@"[\r\n]")]
    [InlineData(@"[\x30A]")]
    [InlineData("(?:ab|cd){2}")]
    public void SupportAnalyzerPromotesFiniteLiteralBranchesToLiteralFamily(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    [Theory]
    [InlineData(@"foo(?=bar)")]
    [InlineData(@"\.")]
    [InlineData(@"\n")]
    [InlineData(@"\t")]
    [InlineData(@"\x41")]
    [InlineData(@"a{3}")]
    public void SupportAnalyzerPromotesSingleLiteralSimplePatternsToLiteralFastPaths(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.True(
            regex.Inspection.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal,
            $"Expected literal fast path for '{pattern}', got {regex.Inspection.ExecutionKind}.");
    }

    [Fact]
    public void CountMatchesRuntimeForRepeatedAsciiCharClassRun()
    {
        const string pattern = @"[A-Za-z]{8,13}";
        const string input = "abcd efghijklmnopqrstu vwxyz";
        var options = RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options: options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(System.Text.Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void PublicFixedWidthScanPreservesDenseOffsets()
{ var regex = new Utf8Regex("ab[0-9]d", options: RegexOptions.CultureInvariant); var offsets = new List<int>(); foreach(var match in regex.EnumerateMatches("ab1d-ab2d-ab3d"u8)) { offsets.Add(match.StartOffsetInBytes); Assert.Equal(4, match.EndOffsetInBytes - match.StartOffsetInBytes); } Assert.Equal([0,5,10], offsets); }
    [Theory]
    [InlineData("ab[0-9]d", "ab1d-ab2d-xx-ab9d-ab0d-abXd-ab4d-ab5d-ab6d-ab7d-")]
    [InlineData("x[A-Za-z]y", "xAy-xzy-x0y-xBy-x_y-xCy-xdy-xEy-xfy-xGy-xhy-")]
    [InlineData("u[A-Za-z0-9]v", "uAv-u0v-u_v-uzv-u9v-u-v-uBv-u3v-uCv-u7v-")]
    [InlineData("q[0-9A-Fa-f]z", "q0z-qAz-qgz-qfz-q9z-qFz-q-z-qbz-qCz-q8z-")]
    [InlineData("a[A-Za-z]a", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("ab[0-9]cd", "ab1cd-ab2cd-ab3cd-ab4cd-ab5cd-ab6cd-ab7cd-")]
    [InlineData("ab[0-9]cde", "ab1cde-ab2cde-ab3cde-ab4cde-ab5cde-ab6cde-")]
    [InlineData("abc[0-9]def", "abc1def-abc2def-abc3def-abc4def-abc5def-")]
    [InlineData("abc[0-9]defg", "abc1defg-abc2defg-abc3defg-abc4defg-abc5defg-")]
    public void VectorizedFixedWidthCountMatchesDotNet(string pattern, string input)
    {

        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

        var actual = new Utf8Regex(analysis.SemanticRegex.Pattern, options: analysis.SemanticRegex.Options).Count(inputBytes);
        Assert.Equal(Regex.Count(input, pattern, RegexOptions.CultureInvariant), actual);
    }

    [Fact]
    public void VectorizedFixedWidthCountMatchesDotNetAcrossVectorBoundaries()
    {

        var patterns = new (string Pattern, string DensePrefix)[]
        {
            ("ab[0-9]d", "ab1d-ab2d-"),
            ("x[A-Za-z]y", "xAy-xzy-"),
            ("u[A-Za-z0-9]v", "uAv-u7v-"),
            ("q[0-9A-Fa-f]z", "qAz-q8z-"),
        };
        var alphabet = "abdxuyqz01239AFaf_G-";
        var random = new Random(1729);
        foreach (var patternCase in patterns)
        {
            var analysis = Utf8FrontEnd.Compile(patternCase.Pattern, RegexOptions.CultureInvariant);
            for (var length = 35; length <= 257; length++)
            {
                var chars = new char[length];
                for (var i = 0; i < chars.Length; i++)
                {
                    chars[i] = alphabet[random.Next(alphabet.Length)];
                }
                patternCase.DensePrefix.AsSpan().CopyTo(chars);

                var input = new string(chars);
                var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
                var actual = new Utf8Regex(analysis.SemanticRegex.Pattern, options: analysis.SemanticRegex.Options).Count(inputBytes);
                Assert.Equal(Regex.Count(input, patternCase.Pattern, RegexOptions.CultureInvariant), actual);
            }
        }
    }

    [Fact]
    public void VectorizedFixedWidthCountPreservesBorrowedOffsetsAndOverlappingTails()
    {

        for (var width = 3; width <= 8; width++)
        {
            var prefix = new string('a', width - 2);
            var pattern = prefix + "[0-9]b";
            var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
            var corpus = string.Concat(Enumerable.Repeat(prefix + "1b-" + prefix + "Xb-", 16));
            for (var length = width + 31; length <= width + 95; length++)
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(corpus[..length]);
                var expected = Regex.Count(corpus[..length], pattern, RegexOptions.CultureInvariant);
                foreach (var offset in new[] { 0, 1, 7, 17, 31 })
                {
                    var storage = Enumerable.Repeat((byte)'x', offset + length + 33).ToArray();
                    bytes.CopyTo(storage.AsSpan(offset));
                    var actual = new Utf8Regex(analysis.SemanticRegex.Pattern, options: analysis.SemanticRegex.Options).Count(storage.AsSpan(offset, length));
                    Assert.Equal(expected, actual);
                }
            }
        }
    }

    [Fact]
    public void VectorizedFixedWidthCountHandsLongBarrenSuffixToScalarSearch()
    {

        const string pattern = "ab[0-9]d";
        var input = string.Concat(
            "ab1d-ab2d-ab3d-ab4d-",
            new string('x', 512),
            "-ab7d");
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

        var actual = new Utf8Regex(analysis.SemanticRegex.Pattern, options: analysis.SemanticRegex.Options).Count(inputBytes);
        Assert.Equal(Regex.Count(input, pattern, RegexOptions.CultureInvariant), actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicFixedWidthCountMatchesDotNetAcrossAsciiAndUnicode(bool compiled)
    {
        const string pattern = "ab[0-9]d";
        var input = "é-ab1d-Ж-ab2d-😀-abXd-" + new string('x', 64) + "-ab9d";
        var options = RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            new Utf8Regex(pattern, options: options).Count(inputBytes));
        Assert.Equal(
            Regex.Count(input, pattern, options, TimeSpan.FromSeconds(10)),
            new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromSeconds(10)
            }).Count(inputBytes));
    }

    [Theory]
    [InlineData("ab[0-9]d", "ab1d-")]
    [InlineData("ab[0-9]cd", "ab1cd-")]
    [InlineData("abc[0-9]defg", "abc1defg-")]
    public void CompiledFixedWidthCountMatchesDotNetWhenVectorRouteIsEligible(
        string pattern,
        string segment)
    {
        var input = string.Concat(segment, segment, segment, segment, segment, segment, segment, segment);
        var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;

        Assert.Equal(
            Regex.Count(input, pattern, options),
            new Utf8Regex(pattern, options: options).Count(System.Text.Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("^ab[0-9]d", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d$", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    [InlineData("ab.d", RegexOptions.CultureInvariant)]
    [InlineData("ab[02468]d", RegexOptions.CultureInvariant)]
    [InlineData("a[0-9]", RegexOptions.CultureInvariant)]
    [InlineData("abcd[0-9]efgh", RegexOptions.CultureInvariant)]
    public void FixedWidthNeighborsKeepPublicCounts(string pattern, RegexOptions options)
{ var text = new string('x',64) + "ab2d"; Assert.Equal(Regex.Count(text, pattern, options), new Utf8Regex(pattern, options: options).Count(System.Text.Encoding.UTF8.GetBytes(text))); }
    [Theory]
    [InlineData("cat|dog")]
    [InlineData("cat|horse")]
    [InlineData("(?:cat|horse)")]
    public void SupportAnalyzerClassifiesAsciiLiteralAlternationsAsNativeLiteralFamilies(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.ExactAsciiLiterals, regex.Inspection.SearchPlan.Kind);
    }

    public static IEnumerable<object?[]> AbsoluteStartAnchoredLinearCases()
    {
        // Absolute `^`/`\A` anchor every operation at offset 0. Oracles are
        // runtime-computed BCL expectations; rows cover hits, misses, trailing
        // newlines, and the former phantom positions (mid-string `ab` ending at
        // the input end verified while `^` went unchecked on linear paths).
        yield return new object?[] { "^ab$", "ab" };
        yield return new object?[] { "^ab$", "abab" };
        yield return new object?[] { "^ab$", "xxab" };
        yield return new object?[] { "^ab$", "abxx" };
        yield return new object?[] { "^ab$", "aab" };
        yield return new object?[] { "^ab$", "" };
        yield return new object?[] { "^ab$", "ab\n" };
        yield return new object?[] { "^ab$", "\nab" };
        yield return new object?[] { "^ab$", "ab\nab" };
        yield return new object?[] { "\\Aab\\z", "ab" };
        yield return new object?[] { "\\Aab\\z", "abab" };
        yield return new object?[] { "\\Aab\\z", "xxab" };
        yield return new object?[] { "\\Aab\\z", "ab\n" };
    }

    [Theory]
    [MemberData(nameof(AbsoluteStartAnchoredLinearCases))]
    [MemberData(nameof(LineEndAnchorScanCases))]
    public void AnchoredLinearOperationsMatchOracle(string pattern, string inputText)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options: options);

            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));

            var expectedMatch = oracle.Match(inputText);
            var actualMatch = regex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
            }

            Assert.Equal(oracle.Match(inputText).Success, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Success);

            var expectedPositions = new List<string>();
            foreach (Match m in oracle.Matches(inputText))
            {
                expectedPositions.Add(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, m.Index)) + ":" + System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(m.Index, m.Length)));
            }

            var actualPositions = new List<string>();
            foreach (var m in regex.EnumerateMatches(input))
            {
                actualPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
            }

            Assert.Equal(expectedPositions, actualPositions);

            foreach (var offset in new[] { 0, 1, 2 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                Assert.Equal(oracle.IsMatch(inputText, offset), regex.Match(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), []).Success);
                Assert.Equal(oracle.Matches(inputText, offset).Count, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)))));
                Assert.Equal(oracle.Match(inputText, offset).Success, Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)))).Success);

                var expectedOffsetPositions = new List<string>();
                foreach (Match m in oracle.Matches(inputText, offset))
                {
                    expectedOffsetPositions.Add(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, m.Index)) + ":" + System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(m.Index, m.Length)));
                }

                var actualOffsetPositions = new List<string>();
                foreach (var m in regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset))))
                {
                    actualOffsetPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
                }

                Assert.Equal(expectedOffsetPositions, actualOffsetPositions);
            }
        }
    }

    public static IEnumerable<object?[]> LineAnchoredLinearControls()
    {
        // Multiline `^` legitimately matches after `\n`: these post-line-start
        // Count/Enumerate rows agree today and must not regress. (Multiline
        // IsMatch/Match misses and `$`-before-`\n` are separate follow-ups.)
        yield return new object?[] { "(?m:^ab$)", "a\nab", 1, "2:2" };
        yield return new object?[] { "(?m:^ab$)", "ab", 1, "0:2" };
    }

    [Theory]
    [MemberData(nameof(LineAnchoredLinearControls))]
    public void LineAnchoredLinearCountAndEnumerationHoldPostLineStartHits(string pattern, string inputText, int expectedCount, string expectedPositions)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options: options);

            Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));

            var actualPositions = new List<string>();
            foreach (var m in regex.EnumerateMatches(input))
            {
                actualPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
            }

            Assert.Equal(new List<string> { expectedPositions }, actualPositions);
        }
    }

    public static IEnumerable<object?[]> LineStartAnchorInterpreterCases()
    {
        // Multiline `^` matches at offset 0 and after `\n`. Oracles are
        // runtime-computed BCL expectations. `\A` stays absolute even under
        // the Multiline option and pins offset 0 exactly like `^` does.
        yield return new object?[] { "(?m:^ab$)", "a\nab" };
        yield return new object?[] { "(?m:^ab$)", "ab" };
        yield return new object?[] { "(?m:^ab$)", "xab" };
        yield return new object?[] { "(?m:^ab$)", "abx" };
        yield return new object?[] { "(?m:^ab)", "xab\nab" };
        yield return new object?[] { "(?m:\\Aab)", "ab" };
        yield return new object?[] { "(?m:\\Aab)", "x\nab" };
    }

    [Theory]
    [MemberData(nameof(LineStartAnchorInterpreterCases))]
    public void LineStartAnchorInterpreterOperationsMatchOracle(string pattern, string inputText)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options: options);

            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));

            var expectedMatch = oracle.Match(inputText);
            var actualMatch = regex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
            }

            Assert.Equal(oracle.Match(inputText).Success, Utf8MatchSequence.First(regex.CreateMatchCursor(input)).Success);

            var expectedPositions = new List<string>();
            foreach (Match m in oracle.Matches(inputText))
            {
                expectedPositions.Add(System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, m.Index)) + ":" + System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(m.Index, m.Length)));
            }

            var actualPositions = new List<string>();
            foreach (var m in regex.EnumerateMatches(input))
            {
                actualPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
            }

            Assert.Equal(expectedPositions, actualPositions);
        }
    }

    public static IEnumerable<object?[]> LineEndAnchorScanCases()
    {
        // Multiline `$` matches before any line feed (and at the end); absolute
        // `$`/`\Z`/`\z` never match before an interior line feed. Oracles are
        // runtime-computed BCL expectations across every operation and offsets.
        yield return new object?[] { "(?m:^ab$)", "ab\nab" };
        yield return new object?[] { "^(?m:ab$)", "ab\ncd" };
        yield return new object?[] { "^(?m:ab$)", "ab" };
        yield return new object?[] { "(?m:ab$)", "xab\ny" };
        yield return new object?[] { "(?m:^(ab|cd)$)", "ab\ncd" };
        yield return new object?[] { "ab$", "xxab" };
        yield return new object?[] { "ab$", "ab\n" };
    }

    public static IEnumerable<object?[]> GrouplessSimpleDetailedCases()
    {

        yield return new object?[] { "\\d{2,4}", "xx12yy", true };
        yield return new object?[] { "[0-9]{3}", "ab123", true };
        yield return new object?[] { "ab[0-9]d", "xab2dxx", true };
        yield return new object?[] { "a[^x]d", "xabd", true };
        yield return new object?[] { "a.c", "xabc", false };
    }

    [Theory]
    [MemberData(nameof(GrouplessSimpleDetailedCases))]
    public void GrouplessSimpleDetailedMatchesOracle(string pattern, string inputText, bool expectOwned)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options: options);
            var expected = oracle.Match(inputText);

            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Utf8MatchSequence.AssertCaptures(expected, actual, input, inputText);


            if (expectOwned && (options & RegexOptions.Compiled) == 0)
            {
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            }

            foreach (var offset in new[] { 0, 1, 2 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                var expectedOffset = oracle.Match(inputText, offset);
                var actualOffset = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset))));
                Assert.Equal(expectedOffset.Success, actualOffset.Success);
                if (expectedOffset.Success)
                {
                    Utf8MatchSequence.AssertCaptures(expectedOffset, actualOffset, input, inputText);
                }
            }
        }
    }

    public static IEnumerable<object?[]> OwnedRouteZeroConstructionCases()
    {

        yield return new object?[] { "ab[0-9]d", RegexOptions.CultureInvariant, "xab2dxx" };
        yield return new object?[] { "a[^x]d", RegexOptions.CultureInvariant, "xabd" };
        yield return new object?[] { "\\d{2,4}", RegexOptions.CultureInvariant, "xx12yy" };
        yield return new object?[] { "\\d{2,4}", RegexOptions.CultureInvariant | RegexOptions.Compiled, "xx12yy" };
        yield return new object?[] { "[0-9]{3}", RegexOptions.CultureInvariant, "ab123" };
        yield return new object?[] { "[0-9]{3}", RegexOptions.CultureInvariant | RegexOptions.Compiled, "ab123" };
        yield return new object?[] { "a.c", RegexOptions.CultureInvariant, "xabc" };
        yield return new object?[] { "a.c", RegexOptions.CultureInvariant | RegexOptions.Compiled, "xabc" };
    }

    [Theory]
    [MemberData(nameof(OwnedRouteZeroConstructionCases))]
    public void OwnedRoutesNeverConstructFallbackEngine(string pattern, RegexOptions options, string inputText)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(inputText), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(inputText).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);

        var matchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Match(inputText).Success, matchRegex.Match(input).Success);
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);

        var detailedRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Match(inputText).Success, Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input)).Success);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);

        var enumerateRegex = new Utf8Regex(pattern, options: options);
        var enumerated = 0;
        foreach (var _ in enumerateRegex.EnumerateMatches(input))
        {
            enumerated++;
        }

        var expectedEnumerated = 0;
        foreach (Match _ in oracle.Matches(inputText))
        {
            expectedEnumerated++;
        }

        Assert.Equal(expectedEnumerated, enumerated);
        Assert.True(enumerateRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NonAsciiSplitCases()
    {
        string[] patterns = ["ab[0-9]d", "[0-9]{3}", "[0-9]{2,4}"];
        string[] texts = ["xxab1dxx123", "ééab1dé123é", "123ééab1d", "nomatchhere", ""];
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
    [MemberData(nameof(NonAsciiSplitCases))]
    public void EnumerateSplitsOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], global::System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), global::System.Text.Encoding.UTF8.GetByteCount(text[start..end])));
        }

        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }

        Assert.Equal(expected, actual);
        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }

    [Theory]
    [MemberData(nameof(NonAsciiSplitCases))]
    public void EnumerateMatchesOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length))));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in regex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.StartOffsetInBytes, (valueMatch.EndOffsetInBytes - valueMatch.StartOffsetInBytes)));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NonAsciiReplaceCases()
    {
        string[] patterns = ["ab[0-9]d", "[0-9]{3}", "[0-9]{2,4}"];
        string[] templates = ["x", "<$1>", "$&", "$`", "$\u0027", "$_", "$+", "$$"];
        string[] texts = ["xxab1dxx123", "ééab1dé123é", "123ééab1d", "nomatchhere"];
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
    [MemberData(nameof(NonAsciiReplaceCases))]
    public void ReplaceOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Utf8AsciiScalarOperationAssertions.Owned(bytesRegex);

        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> ScopedFoldingCases()
    {
        (string Pattern, string[] Texts)[] shapes =
        [
            ("a(?i:b)c", ["xxabcxx", "xxaBcxx", "xxabCxx"]),
            ("(?i:ab)cd", ["xxabcdxx", "xxABcdxx", "xxabcDxx"]),
            ("(?i:[ab])x", ["xxaxxx", "xxBxx", "xxcxx"]),
            ("x(?i:[ab]+)y", ["xxAByxx", "xxayxx", "xxcyxx"]),
            ("(?i:ab)|cd", ["xxABxx", "xxcdxx", "xxefxx"]),
            ("(?i:[ab]){2}x", ["xxBBxx", "xxabxx", "xxccxx"]),
        ];
        foreach (var shape in shapes)
        {
            foreach (var text in shape.Texts)
            {
                yield return new object?[] { shape.Pattern, RegexOptions.CultureInvariant, text };
                yield return new object?[] { shape.Pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(ScopedFoldingCases))]
    public void ScopedFoldingMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(regex);

        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));

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

    }

    public static System.Collections.Generic.IEnumerable<object?[]> ScalarDiscoveryOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "éabcé"),
            ("a.c", "xyz"),
            ("a.c", "éxyzé"),
            ("a.c", "abcaéc"),
            ("a.c", ""),
            ("a[^x]d", "éabde"),
            ("a[^x]d", "xyz"),
            ("a[^x]d", "éaxdé"),
            ("a[^x]d", "abdabd"),
            ("a[^x]d", ""),
            ("(a.c)", "éabcé"),
            ("(a.c)", "xyz"),
            (".[0-9]", "é1a2"),
            (".[0-9]", "xyz"),
            ("[^x][^y]", "éabé"),
            ("[^x][^y]", "xy"),
            (".{2,4}", "éabcdé"),
            (".{2,4}", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(ScalarDiscoveryOwnedCases))]
    [MemberData(nameof(FoldedScalarDiscoveryOwnedCases))]
    [MemberData(nameof(DoublyNegatedShorthandOwnedCases))]
    public void NonAsciiDiscoveryMatchesOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);

        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedScalarDiscoveryOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?i)a.c", "éABCé"),
            ("(?i)a.c", "éabcé"),
            ("(?i)a.c", "éxyzé"),
            ("(?i)a.c", ""),
            ("(?i)a[^x]d", "éaQdé"),
            ("(?i)a[^x]d", "éAXDé"),
            ("(?i)a[^k]b", "axbb"),
            ("(?i)a[^k]b", "aKb"),
            ("(?i)a[^k]b", "a\u212Ab"),
            ("(?i)k.c", "\u212Abc"),
            ("(?i)k.c", "xKbc"),
            ("(?i)k.c", "xabc"),
            ("(?i)i.i", "x\u0130x"),
            ("(?i)s.s", "x\u017Fs"),
            ("(?i)a\\db", "éa1bé"),
            ("(?i)a\\db", "xa\u0663bx"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
    }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> ScalarMatchOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "éabcé"),
            ("a.c", "xyz"),
            ("a.c", "éxyzé"),
            ("a.c", "abcaéc"),
            ("a.c", ""),
            ("a[^x]d", "éabde"),
            ("a[^x]d", "xyz"),
            ("a[^x]d", "éaxdé"),
            ("a[^x]d", "abdabd"),
            ("a[^x]d", ""),
            ("(a.c)", "éabcé"),
            ("(a.c)", "xyz"),
            (".[0-9]", "é1a2"),
            (".[0-9]", "xyz"),
            ("[^x][^y]", "éabé"),
            ("[^x][^y]", "xy"),
            (".{2,4}", "éabcdé"),
            (".{2,4}", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(ScalarMatchOwnedCases))]
    public void ScalarMatchLightOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);
        var expected = oracle.Match(text);
        var actual = regex.Match(input);

        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length)), (actual.EndOffsetInBytes - actual.StartOffsetInBytes));
        }

        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> ScalarDetailedOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "éabcé"),
            ("a.c", "xyz"),
            ("a.c", "éxyzé"),
            ("a.c", "abcaéc"),
            ("a.c", ""),
            ("a[^x]d", "éabde"),
            ("a[^x]d", "xyz"),
            ("a[^x]d", "éaxdé"),
            ("a[^x]d", "abdabd"),
            ("a[^x]d", ""),
            (".[0-9]", "é1a2"),
            (".[0-9]", "xyz"),
            ("[^x][^y]", "éabé"),
            ("[^x][^y]", "xy"),
            (".{2,4}", "éabcdé"),
            (".{2,4}", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(ScalarDetailedOwnedCases))]
    public void ScalarDetailedOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);
        var expected = oracle.Match(text);
        var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);

        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> ScalarEnumerationOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "éabcé"),
            ("a.c", "xyz"),
            ("a.c", "éxyzé"),
            ("a.c", "abcaéc"),
            ("a.c", ""),
            ("a[^x]d", "éabde"),
            ("a[^x]d", "xyz"),
            ("a[^x]d", "éaxdé"),
            ("a[^x]d", "abdabd"),
            ("a[^x]d", ""),
            ("(a.c)", "éabcé"),
            ("(a.c)", "xyz"),
            (".[0-9]", "é1a2"),
            (".[0-9]", "xyz"),
            ("[^x][^y]", "éabé"),
            ("[^x][^y]", "xy"),
            (".{2,4}", "éabcdé"),
            // .{2,4} on ASCII input keeps its pre-existing anchored split route
            // (the scalar hooks decline ASCII input by construction), so the miss
            // row below uses non-ASCII input to stay on the owned split path.
            (".{2,4}", "é"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(ScalarEnumerationOwnedCases))]
    public void ScalarEnumerationOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        AssertEnumerationAndSplits(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedScalarEnumerationOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?i)a.c", "éABCé"),
            ("(?i)a.c", "éxyzé"),
            ("(?i)a[^x]d", "éaQdé"),
            ("(?i)a[^k]b", "a\u212Ab"),
            ("(?i)k.c", "\u212Abc"),
            ("(?i)a\\db", "éa1bé"),
            ("(?i)a.c", ""),
            ("(?i)a[^x]d", "éAXDé"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedScalarEnumerationOwnedCases))]
    public void ScalarEnumerationOwnsFoldedNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        AssertEnumerationAndSplits(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> ScalarGrouplessTemplateOwnedCases()
    {
        string[] patterns = ["a.c", "a[^x]d", ".[0-9]"];
        string[] templates = ["x", "$&", "<$&>", "$`", "$'", "$_", "$$"];
        string[] texts = ["éabcé", "xyz"];
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
    [MemberData(nameof(ScalarGrouplessTemplateOwnedCases))]
    [MemberData(nameof(FoldedScalarTemplateOwnedCases))]
    [MemberData(nameof(DigitTemplateCases))]
    [MemberData(nameof(WordSpaceTemplateCases))]
    [MemberData(nameof(NegatedShorthandTemplateCases))]
    [MemberData(nameof(SinglelineDotTemplateCases))]
    public void NonAsciiTemplateReplacementMatchesOracle(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);
        var expected = oracle.Replace(text, template);
        var actual = Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(template)));

        Assert.Equal(expected, actual);
        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedScalarTemplateOwnedCases()
    {
        string[] patterns = ["(?i)a.c", "(?i)a[^x]d"];
        string[] templates = ["x", "$&"];
        string[] texts = ["éABCé", "xyz"];
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

    public static System.Collections.Generic.IEnumerable<object?[]> DigitDiscoveryMatchDetailedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("\\d{2,4}", "xx12xx"),
            ("\\d{2,4}", "é12é"),
            ("\\d{2,4}", "x٣٤x"),
            ("\\d{2,4}", "éé123é"),
            ("\\d{2,4}", "é12345é"),
            ("\\d{2,4}", "12345"),
            ("\\d{2,4}", "xyz"),
            ("\\d{2,4}", ""),
            ("a\\db", "xa1bx"),
            ("a\\db", "éa1bé"),
            ("a\\db", "xa٣bx"),
            ("a\\db", "xyz"),
            ("\\d", "x7x"),
            ("\\d", "é٣é"),
            ("\\d", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(DigitDiscoveryMatchDetailedCases))]
    [MemberData(nameof(WordSpaceDiscoveryMatchDetailedCases))]
    [MemberData(nameof(NegatedShorthandDiscoveryMatchDetailedCases))]
    [MemberData(nameof(SinglelineDotDiscoveryMatchDetailedCases))]
    public void NonAsciiDiscoveryAndCapturesMatchOracle(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);

        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));

        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }

        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
    }

    [Theory]
    [MemberData(nameof(DigitDiscoveryMatchDetailedCases))]
    [MemberData(nameof(WordSpaceDiscoveryMatchDetailedCases))]
    [MemberData(nameof(NegatedShorthandDiscoveryMatchDetailedCases))]
    [MemberData(nameof(SinglelineDotDiscoveryMatchDetailedCases))]
    public void NonAsciiEnumerationAndSplitsMatchOracle(string pattern, RegexOptions options, string text)
    {
        AssertEnumerationAndSplits(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DigitTemplateCases()
    {
        string[] patterns = ["\\d{2,4}", "a\\db"];
        string[] templates = ["x", "$&", "<$1>", "$`", "$$"];
        (string pattern, string text)[] texts =
        [
            ("\\d{2,4}", "xx12xx"),
            ("\\d{2,4}", "é12é"),
            ("\\d{2,4}", "xyz"),
            ("a\\db", "xa1bx"),
            ("a\\db", "éa1bé"),
            ("a\\db", "xyz"),
        ];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var row in texts)
                {
                    if (row.pattern != pattern)
                    {
                        continue;
                    }

                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, row.text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, row.text };
                }
            }
        }
    }

    [Theory]
    [MemberData(nameof(DigitTemplateCases))]
    [MemberData(nameof(WordSpaceTemplateCases))]
    [MemberData(nameof(NegatedShorthandTemplateCases))]
    [MemberData(nameof(SinglelineDotTemplateCases))]
    public void NonAsciiDestinationReplacementMatchesOracle(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);
        var expected = oracle.Replace(text, template);
        var destination = new byte[4096];
        var status = regex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);

        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, bytesWritten)));
        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> WordSpaceDiscoveryMatchDetailedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("\\s", "x \tx"),
            ("\\s", "x x"),
            ("\\s", "xyz"),
            ("\\s", ""),
            ("\\w", "xax"),
            ("\\w", "xéx"),
            ("\\w", "x.x"),
            ("\\w", ""),
            ("\\s{2}", "x  x"),
            ("\\s{2}", "x 　x"),
            ("\\s{2}", "xyz"),
            ("\\w{2}", "xabx"),
            ("\\w{2}", "xéαx"),
            ("\\w{2}", "x. "),
            ("a\\sb", "xa bx"),
            ("a\\sb", "xa bx"),
            ("a\\sb", "xyz"),
            ("a\\wb", "xabbx"),
            ("a\\wb", "xaébx"),
            ("a\\wb", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> WordSpaceTemplateCases()
    {
        string[] patterns = ["a\\sb", "a\\wb"];
        string[] templates = ["x", "$&"];
        (string pattern, string text)[] texts =
        [
            ("a\\sb", "xa bx"),
            ("a\\sb", "xa bx"),
            ("a\\sb", "xyz"),
            ("a\\wb", "xabbx"),
            ("a\\wb", "xaébx"),
            ("a\\wb", "xyz"),
        ];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var row in texts)
                {
                    if (row.pattern != pattern)
                    {
                        continue;
                    }

                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, row.text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, row.text };
                }
            }
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NegatedShorthandDiscoveryMatchDetailedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("[^\\d]", "éabé"),
            ("[^\\d]", "١٢٣"),
            ("[^\\d]", "xyz"),
            ("[^\\s]", "éabé"),
            ("[^\\s]", "  "),
            ("[^\\s]", "xyz"),
            ("[^\\w]", "é.é"),
            ("[^\\w]", "éab"),
            ("[^\\w]", "xyz"),
            ("\\D", "éabé"),
            ("\\D", "١٢٣"),
            ("\\D", "xyz"),
            ("\\S", "é a"),
            ("\\S", "  "),
            ("\\S", "xyz"),
            ("\\W", "é.é"),
            ("\\W", "éab"),
            ("\\W", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NegatedShorthandTemplateCases()
    {
        string[] patterns = ["[^\\d]", "\\D"];
        string[] templates = ["x", "$&"];
        (string pattern, string text)[] texts =
        [
            ("[^\\d]", "éabé"),
            ("[^\\d]", "١٢٣"),
            ("[^\\d]", "xyz"),
            ("\\D", "éabé"),
            ("\\D", "١٢٣"),
            ("\\D", "xyz"),
        ];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var row in texts)
                {
                    if (row.pattern != pattern)
                    {
                        continue;
                    }

                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, row.text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, row.text };
                }
            }
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DoublyNegatedShorthandOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("[^\\D]", "éabé"),
            ("[^\\W]", "é.é"),
            ("[^\\S]", "é a"),
            ("[^\\D]", "é3é"),
            ("[^\\W]", "12"),
            ("[^\\W]", " ."),
            ("[^\\S]", "éab"),
            ("a[^\\D]b", "xa3b"),
            ("a[^\\D]b", "xyz"),
            ("(?i)[^\\D]", "é3É"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SinglelineDotDiscoveryMatchDetailedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?s:.)", "é"),
            ("(?s:.)", "\n"),
            ("(?s:.)", ""),
            ("(?s:a.c)", "éaécé"),
            ("(?s:a.c)", "a\nc"),
            ("(?s:a.c)", "xyz"),
            ("(?s:\\d{2})", "é12é"),
            ("(?s:\\d{2})", "xyz"),
            ("(?s:a[^x]c)", "éabcé"),
            ("(?s:a[^x]c)", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SinglelineDotTemplateCases()
    {
        string[] patterns = ["(?s:a.c)"];
        string[] templates = ["x", "$&"];
        (string pattern, string text)[] texts =
        [
            ("(?s:a.c)", "éaécé"),
            ("(?s:a.c)", "a\nc"),
            ("(?s:a.c)", "xyz"),
        ];
        foreach (var pattern in patterns)
        {
            foreach (var template in templates)
            {
                foreach (var row in texts)
                {
                    if (row.pattern != pattern)
                    {
                        continue;
                    }

                    yield return new object?[] { pattern, RegexOptions.CultureInvariant, template, row.text };
                    yield return new object?[] { pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, template, row.text };
                }
            }
        }
    }

    [Theory]
    [InlineData("^$")]
    [InlineData("^")]
    [InlineData("$")]
    [InlineData("(ab)+")]
    [InlineData("(?=ab)")]
    [InlineData("(?!ab)[a-z]")]
    public void SupportAnalyzerKeepsAnchorOnlyPatternsInFallback(string pattern)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> ScalarLiteralTemplateOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a.c", "éabcé", "Q"),
            ("a.c", "éabcé", "$$"),
            ("a[^x]d", "éabde", "Q"),
            (@"\d+", "éab12cdé", "Q"),
            (@"(\d+)-(\d+)", "é12-34é", "Q"),
            (@"(\d+)-(\d+)", "é12-34é", "$$"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }
    [Theory]
    [MemberData(nameof(ScalarLiteralTemplateOwnedCases))]
    public void ScalarLiteralTemplateOwnsNonAsciiInput(string pattern, RegexOptions options, string text, string template)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Utf8AsciiScalarOperationAssertions.Owned(bytesRegex);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));
    }
    public static System.Collections.Generic.IEnumerable<object?[]> NegatedCategoryOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            (@"[^\p{Nd}]", "éa1bé"),
            (@"[^\p{Nd}]", "abc"),
            (@"[\P{Nd}]", "éa1bé"),
            (@"[^\p{L}]", "éa1bé"),
            (@"([^\p{Nd}]+)", "éa1bé"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }
    [Theory]
    [MemberData(nameof(NegatedCategoryOwnedCases))]
    public void NegatedCategoryOwnsAllOperations(string pattern, RegexOptions options, string text)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
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
        var splitRegex = new Utf8Regex(pattern, options: options);
        var expectedSplits = new System.Collections.Generic.List<string>();
        foreach (var range in oracle.EnumerateSplits(text, int.MaxValue))
        {
            expectedSplits.Add(Encoding.UTF8.GetByteCount(text.AsSpan(0, range.Start.Value)) + ":" + Encoding.UTF8.GetByteCount(text.AsSpan(0, range.End.Value)) + ":" + text[range]);
        }
        var actualSplits = new System.Collections.Generic.List<string>();
        foreach (var s in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add(s.IndexInBytes + ":" + (s.IndexInBytes + s.LengthInBytes) + ":" + Encoding.UTF8.GetString(s.GetValueBytes()));
        }
        Assert.Equal(expectedSplits, actualSplits);
        var templateRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Replace(text, "<$1>"), Encoding.UTF8.GetString(templateRegex.Replace(input, "<$1>"u8)));
    }
    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarDiscoveryOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "a\U0001F600c"),
            ("a.c", "abc"),
            (".", "\U0001F600"),
            ("..", "\U0001F600"),
            ("a.", "a\U0001F600"),
            ("a[^x]d", "a\U0001F600d"),
            ("[^x]", "\U0001F600"),
            (@"\d\d", "a12\U0001F600"),
            (@"\d{2,4}", "12\U0001F60034"),
            (@"(\d\d)", "a12\U0001F600"),
            (@"(a.c)", "xa\U0001F600cy"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }
    [Theory]
    [MemberData(nameof(SupplementaryScalarDiscoveryOwnedCases))]
    public void SupplementaryScalarDiscoveryOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
    }
    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarDiscoveryDeclineCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?m:^a.c$)", "ab\nac\U0001F600"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }
    [Theory]
    [MemberData(nameof(SupplementaryScalarDiscoveryDeclineCases))]
    public void SupplementaryScalarDiscoveryMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarMatchOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "a\U0001F600c"),
            ("a.c", "abc"),
            (".", "\U0001F600"),
            ("..", "\U0001F600"),
            ("a.", "a\U0001F600"),
            ("a[^x]d", "a\U0001F600d"),
            ("[^x]", "\U0001F600"),
            ("[^x]a", "\U0001F600a"),
            (@"\d\d", "a12\U0001F600"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }
    [Theory]
    [MemberData(nameof(SupplementaryScalarMatchOwnedCases))]
    public void SupplementaryScalarMatchOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        Utf8AsciiScalarOperationAssertions.Owned(matchRegex);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarEnumerationOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a.c", "a\U0001F600c"),
            ("a.c", "abc"),
            (".", "\U0001F600"),
            ("..", "\U0001F600"),
            ("a.", "a\U0001F600"),
            ("a[^x]d", "a\U0001F600d"),
            ("[^x]", "\U0001F600"),
            (@"\d\d", "a12\U0001F600"),
            (@"(\d\d)", "a12\U0001F600"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }
    [Theory]
    [MemberData(nameof(SupplementaryScalarEnumerationOwnedCases))]
    public void SupplementaryScalarEnumerationOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var regex = new Utf8Regex(pattern, options: options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text))
        {
            expectedPositions.Add(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)) + ":" + System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)));
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatches(input))
        {
            actualPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
        }
        Assert.Equal(expectedPositions, actualPositions);
        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarSplitOwnedCases()
    {
        (string pattern, string text, int count)[] rows =
        [
            ("a.c", "a\U0001F600c", int.MaxValue),
            ("a.c", "abc", int.MaxValue),
            (".", "\U0001F600", int.MaxValue),
            ("..", "\U0001F600", int.MaxValue),
            ("a.", "a\U0001F600", int.MaxValue),
            ("a[^x]d", "a\U0001F600d", int.MaxValue),
            ("[^x]", "\U0001F600", int.MaxValue),
            (@"\d\d", "a12\U0001F600", int.MaxValue),
            (@"(\d\d)", "a12\U0001F600", int.MaxValue),
            (".", "a\U0001F600c", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.count };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.count };
        }
    }
    [Theory]
    [MemberData(nameof(SupplementaryScalarSplitOwnedCases))]
    public void SupplementaryScalarSplitOwnsInput(string pattern, RegexOptions options, string text, int count)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var regex = new Utf8Regex(pattern, options: options);
        var expected = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], global::System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), global::System.Text.Encoding.UTF8.GetByteCount(text[start..end])));
        }
        var actual = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }
        Assert.Equal(expected, actual);
        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarLiteralTemplateOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a.c", "a\U0001F600c", "Q"),
            ("a.c", "a\U0001F600c", "$$"),
            (".", "\U0001F600", "Q"),
            ("..", "\U0001F600", "Q"),
            ("a[^x]d", "a\U0001F600d", "Q"),
            ("[^x]", "\U0001F600", "Q"),
            (@"\d\d", "a12\U0001F600", "Q"),
            (@"\d{2,4}", "12\U0001F60034", "Q"),
            (@"(\d\d)", "a12\U0001F600", "Q"),
            ("ab|a.c", "a\U0001F600zz", "Q"),
            ("a.c", "abc", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }
    [Theory]
    [MemberData(nameof(SupplementaryScalarLiteralTemplateOwnedCases))]
    public void SupplementaryScalarLiteralTemplateOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var expected = oracle.Replace(text, template);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Utf8AsciiScalarOperationAssertions.Owned(bytesRegex);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));
    }
    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarLiteralTemplateDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(?m:^a.c$)", "ab\nac\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }
    [Theory]
    [MemberData(nameof(SupplementaryScalarLiteralTemplateDeclineCases))]
    public void SupplementaryScalarLiteralTemplateReplacementIsOwned(string pattern, RegexOptions options, string text, string template)
    {
        // The owned unit driver preserves template fragments and gaps in UTF-16
        // through expansion, then encodes the completed byte result.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var expected = oracle.Replace(text, template);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(expected, Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template))));
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarGroupTemplateOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(a.c)", "xa\U0001F600cy"),
            ("(a.)", "xa\U0001F600"),
            (@"(a|.)", "\U0001F600"),
            ("a.c", "a\U0001F600c"),
            (@"(a.c)", "xabcy"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(SupplementaryScalarGroupTemplateOwnedCases))]
    public void SupplementaryScalarGroupTemplateOwnsInput(string pattern, RegexOptions options, string text)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var expected = oracle.Replace(text, "<$1>");
        var bytesRegex = new Utf8Regex(pattern, options: options);
        // Byte-level oracle: lone surrogates cannot exist in well-formed UTF-8 bytes,
        // so both sides encode (BCL substitutes identically through the same encoder).
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes("<$1>"));
        Assert.Equal(expectedBytes, bytesActual);
        Utf8AsciiScalarOperationAssertions.Owned(bytesRegex);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, "<$1>"u8));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        // Group templates can expand output past the input length on multi-hit rows.
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, "<$1>"u8, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarBranchOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("((a|.)c)", "a\U0001F600c", "<$1>"),
            ("((a|.)c)", "a\U0001F600c", "<$2>"),
            ("((a|.)c)", "a\U0001F600c", "<$0>"),
            ("((a|.)c)", "a\U0001F600c", "Q"),
            ("((a|.)c)", "xabc", "<$1>"),
            ("((a|.)c)", "xabc", "<$2>"),
            ("(a(.)c)", "xayc", "<$1>"),
            ("(a(.)c)", "xayc", "<$2>"),
            ("((.|b)c)", "a\U0001F600c", "<$2>"),
            ("((?<x>a|.)c)", "a\U0001F600c", "<${x}>"),
            ("((a|.)c)", "xyz", "<$1>"),
            ("(a|(.)c)", "x\U0001F600c", "<$1>"),
            ("(a|(.)c)", "x\U0001F600c", "<$2>"),
            ("(a|(.)c)", "xabc", "<$1>"),
            ("(a|(.)c)", "xabc", "<$2>"),
            ("(a|(.)c)", "xabcxabc", "<$2>"),
            ("((a)|(.)c)", "x\U0001F600c", "<$2>"),
            ("((a)|(.)c)", "x\U0001F600c", "<$3>"),
            ("((a)|(.)c)", "xabcx\U0001F600c", "<$1>"),
            ("((x)|(.))", "y", "<$2>"),
            ("((x)|(.))", "x", "<$2>"),
            ("((x)|(.))", "x", "<$3>"),
            ("((x)|(.))", "x", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(SupplementaryScalarBranchOwnedCases))]
    public void SupplementaryScalarBranchOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        Utf8AsciiScalarOperationAssertions.Owned(matchRegex);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarBranchDeclineCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("((a|.)c)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a\U0001F600c", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(SupplementaryScalarBranchDeclineCases))]
    public void SupplementaryScalarBranchReplacementIsOwned(string pattern, RegexOptions options, string text, string template)
    {
        // The owned unit driver preserves template fragments and gaps in UTF-16
        // through expansion, then encodes the completed byte result.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var stringRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Replace(text, template), Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template))));
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarFromOffsetOwnedCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("((a|.)c)", "a\U0001F600c", 0),
            ("((a|.)c)", "a\U0001F600c", 2),
            ("((a|.)c)", "a\U0001F600c", 3),
            ("((a|.)c)", "a\U0001F600c", 4),
            ("(a|(.)c)", "x\U0001F600c", 0),
            ("(a|(.)c)", "x\U0001F600c", 2),
            ("(a.c)", "xa\U0001F600cy", 0),
            ("(a.c)", "xa\U0001F600cy", 2),
            ("a.c", "xa\U0001F600cy", 0),
            ("a.c", "xa\U0001F600cy", 2),
            ("[^x]", "a\U0001F600b", 0),
            ("[^x]", "a\U0001F600b", 1),
            ("[^x]", "a\U0001F600b", 2),
            ("((.|b)c)", "a\U0001F600c", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(SupplementaryScalarFromOffsetOwnedCases))]
    public void SupplementaryScalarFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        if (AssertInteriorSupplementaryStartRejected(pattern, options, text, offset)) return;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)), []).Success);
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, Utf8MatchSequence.Count(countRegex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)))));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)), []);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes);
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset))));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);

    }

    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarFromOffsetDeclineCases()
    {
        (string pattern, RegexOptions options, string text, int offset)[] rows =
        [
            ("((a|.)c)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a\U0001F600c", 0),
            ("((a|.)c)", RegexOptions.CultureInvariant, "xabcxabc", 1),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.offset };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(SupplementaryScalarFromOffsetDeclineCases))]
    public void SupplementaryScalarFromOffsetMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, int offset)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        if (AssertInteriorSupplementaryStartRejected(pattern, options, text, offset)) return;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)), []).Success);
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var countRegex = new Utf8Regex(pattern, options: options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, Utf8MatchSequence.Count(countRegex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)))));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)), []);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes);
        }
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset))));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarFromOffsetEnumOwnedCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("((a|.)c)", "a\U0001F600c", 0),
            ("((a|.)c)", "a\U0001F600c", 1),
            ("((a|.)c)", "a\U0001F600c", 2),
            ("((a|.)c)", "a\U0001F600c", 3),
            ("((a|.)c)", "a\U0001F600c", 4),
            ("(a|(.)c)", "x\U0001F600c", 0),
            ("(a|(.)c)", "x\U0001F600c", 2),
            ("(a.c)", "xa\U0001F600cy", 0),
            ("(a.c)", "xa\U0001F600cy", 1),
            ("(a.c)", "xa\U0001F600cy", 2),
            ("[^x]", "a\U0001F600b", 1),
            ("[^x]", "a\U0001F600b", 2),
            ("((a|.)c)", "xabcxabc", 1),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(SupplementaryScalarFromOffsetEnumOwnedCases))]
    public void SupplementaryScalarFromOffsetEnumOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        if (AssertInteriorSupplementaryStartRejected(pattern, options, text, offset)) return;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var regex = new Utf8Regex(pattern, options: options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedPositions.Add(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)) + ":" + System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)));
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset))))
        {
            actualPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
        }
        Assert.Equal(expectedPositions, actualPositions);
        Utf8AsciiScalarOperationAssertions.Owned(regex);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SupplementaryScalarFromOffsetEnumDeclineCases()
    {
        (string pattern, RegexOptions options, string text, int offset)[] rows =
        [
            ("((a|.)c)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a\U0001F600c", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.offset };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(SupplementaryScalarFromOffsetEnumDeclineCases))]
    public void SupplementaryScalarFromOffsetEnumMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, int offset)
    {
        // Compare complete scalars through explicit overlapping BCL atoms.
        if (AssertInteriorSupplementaryStartRejected(pattern, options, text, offset)) return;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = CreateSupplementaryScalarOverlapOracle(pattern, options);
        var regex = new Utf8Regex(pattern, options: options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedPositions.Add(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, m.Index)) + ":" + System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(m.Index, m.Length)));
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset))))
        {
            actualPositions.Add(m.StartOffsetInBytes + ":" + (m.EndOffsetInBytes - m.StartOffsetInBytes));
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.NotNull(regex.Inspection.PreparedRegex.ScalarProgram);
    }

    // This portfolio contains ordinary unescaped dots and the ASCII [^x]
    // class. Spell those two atoms as complete scalars in the comparator;
    // noncapturing wrappers preserve numbering, branch priority and repetition.
    // This deliberately is not a general regex translator or semantic oracle.
    private static Regex CreateSupplementaryScalarOverlapOracle(string pattern, RegexOptions options)
    {
        var dot = (options & RegexOptions.Singleline) != 0
            ? @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF])"
            : @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF\n])";
        var translated = pattern.Replace(".", dot, StringComparison.Ordinal)
            .Replace("[^x]", @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^x\uD800-\uDFFF])", StringComparison.Ordinal);
        return new Regex(translated, options, Regex.InfiniteMatchTimeout);
    }

    private static bool AssertInteriorSupplementaryStartRejected(string pattern, RegexOptions options, string text, int offset)
    {
        if (offset <= 0 || offset >= text.Length || !char.IsHighSurrogate(text[offset - 1]) || !char.IsLowSurrogate(text[offset]))
            return false;
        var regex = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset - 1)) + 1, []).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset - 1)) + 1)); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset - 1)) + 1); });
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset - 1)) + 1, [], source.Token).Success);
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset - 1)) + 1, source.Token)); });
        Assert.Throws<ArgumentOutOfRangeException>(() => { _ = regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset - 1)) + 1, source.Token); });
        Utf8AsciiScalarOperationAssertions.Owned(regex);
        return true;
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralWholeLoopOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(.+)", "\U0001F600yy", "Q"),
            ("(.+)", "\U0001F600yy", "<$&>"),
            ("([^x]+)", "\U0001F600yy", "Q"),
            ("([^x]+)", "\U0001F600x\U0001F600", "Q"),
            (".+", "\U0001F600yy", "Q"),
            ("[^x]+", "\U0001F600x\U0001F600", "Q"),
            ("(?:a|[^x])+", "xa\U0001F600y", "Q"),
            ("(?:.|a)+", "\U0001F600b", "Q"),
            ("(?:ab|[^x])+", "xab\U0001F600", "Q"),
            ("(a|b)+", "xaba\U0001F600bab", "Q"),
            ("((a|b)+)", "xaba\U0001F600bab", "<$1>"),
            ("(a|ab)+", "xaab\U0001F600ab", "Q"),
            ("(a|ab)+", "xaabab", "Q"),
            ("(a|ab)+?", "xaabab", "Q"),
            ("((a|ab)+)", "xaab\U0001F600ab", "<$1>"),
            ("(ab|a)+", "xaab\U0001F600ab", "Q"),
            ("(a|b)+?", "xaba\U0001F600bab", "Q"),
            ("(([^x]+))", "\U0001F600yy", "<$1>"),
            ("((a)+)", "xaa\U0001F600aa", "Q"),
            ("((a)+?)", "xaa\U0001F600aa", "Q"),
            ("(?<w>[^x]+)", "\U0001F600yy", "<${w}>"),
            ("(.+)", "abc", "Q"),
            ("([^x]+)", "abc", "Q"),
            ("([^x]+)", "xxx", "Q"),
            ("([\\Da]+)", "a\U0001F600", "Q"),
            ("([^\\da]+)", "a\U0001F600b", "Q"),
            ("(\\D+)", "a\U000104A0b", "Q"),
            ("(\\W+)", "a\U00010400b", "Q"),
            ("(\\S+)", " \U00010400 ", "Q"),
            ("([^a-z]+)", "a\U0001F600b", "Q"),
            ("([^a-z]+)", "x\U0001F600y", "Q"),
            ("\\ba[^x]+b\\b", "x a\U00010400b!", "Q"),
            ("\\ba[^x]+b\\b", "x a\U0001F600b!", "Q"),
            ("\\b.+b\\b", "x a\U0001F600b ", "Q"),
            ("\\ba.+?b\\b", "a\U0001F600b", "Q"),
            ("(?i)\\bA+B\\b", " AAB\U0001F600", "Q"),
            ("(a+?)", "xaay\U0001F600", "Q"),
            ("([0-9]{2,})", "x12\U0001F600", "Q"),
            ("(a{2,})", "xaa\U0001F600", "Q"),
            ("([0-9]{2}|ab)+", "x12ab\U0001F600", "Q"),
            ("(ab|cd)+", "xabcd\U0001F600", "Q"),
            ("(ab|a)+", "xaab\U0001F600", "Q"),
            ("\\ba+\\b", "xaay\U0001F600", "Q"),
            ("\\ba[0-9]+\\b", "xa12\U0001F600y", "Q"),
            ("\\Ba+b\\B", "xaab\U0001F600y", "Q"),
            ("\\Ba+", "xa\U0001F600", "Q"),
            ("a+\\B", "xaa\U0001F600", "Q"),
            ("(ab|ac)+", "xabac\U0001F600", "Q"),
            ("(a[bc])+", "xab\U0001F600", "Q"),
            ("(ba|ca)+", "xbaca\U0001F600", "Q"),
            ("(ab|cb)+", "xabcb\U0001F600", "Q"),
            ("(a{2,}?)", "xaa\U0001F600", "Q"),
            ("(a+?b)+?", "xab\U0001F600", "Q"),
            ("([0-9]+?)", "x12\U0001F600", "Q"),
            ("(a{1,}?)", "xaa\U0001F600", "Q"),
            ("((a|b)c)+", "xacbc\U0001F600", "Q"),
            ("(a(b|c)d)+", "xabd\U0001F600", "Q"),
            ("((ab|cd)e)+", "xabe\U0001F600", "Q"),
            ("(a+|b+)+", "xaabb\U0001F600", "Q"),
            ("(?>a|ab)+", "xaab\U0001F600", "Q"),
            ("(?>a+)b", "xaa\U0001F600b", "Q"),
            ("(?>ab|a)+z", "xabab\U0001F600z", "Q"),
            ("(?>a|ab)c", "xaab\U0001F600c", "Q"),
            ("(a+)+b", "xaab", "Q"),
            ("(a+)+b", "xaa\U0001F600bb", "Q"),
            ("(a+)+", "xaa\U0001F600aa", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralWholeLoopOwnedCases))]
    public void AstralWholeLoopOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralWholeLoopDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("([^x]b)+", "\U0001F600b", "Q"),
            ("(.+?)", "\U0001F600yy", "Q"),
            ("((.)+)", "\U0001F600", "Q"),
            ("(\\d|[^x])+", "a\U0001F600", "Q"),
            ("(a|[^x])+", "xa\U0001F600y", "Q"),
            ("(.|a)+", "\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralWholeLoopDeclineCases))]
    public void AstralWholeLoopReplacementIsOwned(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> TaillessLazyAstralEnumerateDeclineCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(.+?)", "a\U0001F600b"),
            ("([^x]+?)", "a\U0001F600b"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(TaillessLazyAstralEnumerateDeclineCases))]
    public void TaillessLazyAstralEnumerateMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> TaillessLazyAstralDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(.+?)", "a\U0001F600b", "Q"),
            ("([^x]+?)", "a\U0001F600b", "Q"),
            ("(.+?)", "\U0001F600yy", "Q"),
            ("(\\D+?)", "a\U0001F600b", "Q"),
            ("((.+?))", "a\U0001F600b", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(TaillessLazyAstralDeclineCases))]
    public void TaillessLazyAstralMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }
            public static System.Collections.Generic.IEnumerable<object?[]> WordBoundaryLoopDeclineCases()
            {
                (string pattern, string text)[] rows =
                [
                    ("a.+\\B", "xa\U0001F600b"),
                    ("\\B.+\\B", "xa\U0001F600b"),
                ];
                foreach (var row in rows)
                {
                    yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
                    yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
                }
            }

            [Theory]
            [MemberData(nameof(WordBoundaryLoopDeclineCases))]
            public void WordBoundaryLoopMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
            {
                Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
            }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedWholeLoopOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(?i)(.+)", "\U0001F600yy", "Q"),
            ("(?i)(.+)", "\U0001F600yy", "<$&>"),
            ("(?i).+", "\U0001F600yy", "Q"),
            ("(?i)([^x]+)", "\U0001F600yy", "Q"),
            ("(?i)[^x]+", "A\U0001F600C", "Q"),
            ("(?i)[^x]+", "\U0001F600x\U0001F600", "Q"),
            ("(?i)(([^x]+))", "\U0001F600yy", "<$1>"),
            ("(?i)(?<w>[^x]+)", "\U0001F600yy", "<${w}>"),
            ("(?i)(?:a|[^x])+", "xA\U0001F600y", "Q"),
            ("(?i)(?:.|a)+", "\U0001F600B", "Q"),
            ("(?i)(?:ab|[^x])+", "xAB\U0001F600", "Q"),
            ("(?i)[^x]+?B", "x\U0001F600B", "Q"),
            ("(?i)([\\Da]+)", "A\U0001F600", "Q"),
            ("(?i)([^\\da]+)", "A\U0001F600B", "Q"),
            ("(?i)(\\D+)", "A\U000104A0B", "Q"),
            ("(?i)(\\W+)", "A\U00010400B", "Q"),
            ("(?i)(\\S+)", " \U00010400 ", "Q"),
            ("(?i)([^a-z]+)", "A\U0001F600B", "Q"),
            ("(?i)([a-z]+)", "A\U0001F600", "Q"),
            ("(?i)[a-z]+", "xA\U0001F600", "Q"),
            ("(?i)(k+)", "k\u212AK\U0001F600", "Q"),
            ("(?i)(A+)", "aA\U0001F600", "Q"),
            ("(?i)(.+)", "ABC", "Q"),
            ("(?i)([^x]+)", "ABC", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedWholeLoopOwnedCases))]
    public void FoldedWholeLoopOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedWholeLoopDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("(?i)([^x]b)+", RegexOptions.CultureInvariant, "\U0001F600B"),
            ("(?i)(.+?)", RegexOptions.CultureInvariant, "\U0001F600yy"),
            ("(?i)((.)+)", RegexOptions.CultureInvariant, "\U0001F600"),
            ("(?i)(\\d|[^x])+", RegexOptions.CultureInvariant, "A\U0001F600"),
            ("(?i)(a|[^x])+", RegexOptions.CultureInvariant, "xA\U0001F600y"),
            ("(?i)(.|a)+", RegexOptions.CultureInvariant, "\U0001F600B"),
            ("(?i)[^x]*", RegexOptions.CultureInvariant, "\U0001F600yy"),
            ("(?i).+", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "A\U0001F600C"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedWholeLoopDeclineCases))]
    public void FoldedWholeLoopMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> TailedWholeLoopOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]+b", "xa\U0001F600b", "Q"),
            ("a[^x]+b", "xa\U0001F600bxb", "Q"),
            ("[^x]+b", "\U0001F600b", "Q"),
            ("[^x]+b", "x\U0001F600b", "Q"),
            ("a[^x]+", "xa\U0001F600", "Q"),
            (".+b", "\U0001F600b", "Q"),
            ("a[\\D]+b", "xaa\U0001F600b", "Q"),
            ("a\\D+b", "xa\U0001F600b", "Q"),
            ("a\\D+b", "xa12b", "Q"),
            ("a\\d{2,}b", "xa\U0001D7CE\U0001D7CEb", "Q"),
            ("a\\S+b", "xa\U0001F600b", "Q"),
            ("a\\S+b", "xayzb", "Q"),
            ("a\\P{Nd}+b", "xa\U0001F600b", "Q"),
            ("a\\P{Nd}+b", "xa12b", "Q"),
            ("a\\W+b", "xa\U0001F600b", "Q"),
            ("a\\W+b", "xa12b", "Q"),
            ("a\\d{2,}?b", "xa12b", "Q"),
            ("a\\w{2,}?b", "xa12b", "Q"),
            ("a\\w{2,}b", "xa12b", "Q"),
            ("a[^x]{2,}?b", "xayzb", "Q"),
            ("ab[^x]+cd", "xab\U0001F600cd", "Q"),
            ("(a[^x]+b)", "xa\U0001F600b", "<$1>"),
            ("(?<w>a[^x]+b)", "xa\U0001F600b", "<${w}>"),
            ("(?:ab|[^x])+c", "xab\U0001F600c", "Q"),
            ("[^x]+?b", "x\U0001F600b", "Q"),
            ("a\\D+?b", "xa\U0001F600b", "Q"),
            ("a\\S+?b", "xa\U0001F600b", "Q"),
            ("a\\P{Nd}+?b", "xa\U0001F600b", "Q"),
            ("a\\p{L}+?b", "xa\U00010400b", "Q"),
            ("a\\w+?b", "xa12\U0001F600b", "Q"),
            ("(?i)A[^x]+B", "xa\U0001F600b", "Q"),
            ("(?i)[^x]+B", "x\U0001F600B", "Q"),
            ("(?i)A.+B", "xA\U0001F600b", "Q"),
            ("a[^x]+b", "xaxxb", "Q"),
            ("[0-9]+b", "x12b\U0001F600", "Q"),
            ("a[0-9]+b", "xa12b\U0001F600", "Q"),
            ("(ab|cd)+e", "xabcde\U0001F600", "Q"),
            ("(a|ab)+c", "xaabc\U0001F600", "Q"),
            ("((ab)+c)", "xababc\U0001F600", "<$1>"),
            ("x(ab|cd)+", "xxabcdab\U0001F600", "Q"),
            ("a(ab)+b", "xaabb\U0001F600", "Q"),
            ("(?<=x)a+", "xaay\U0001F600", "Q"),
            ("a+(?=y)", "xaay\U0001F600", "Q"),
            ("(?<=x)[0-9]+", "x12y\U0001F600", "Q"),
            ("(?<=x)a+b", "xaab\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(TailedWholeLoopOwnedCases))]
    public void TailedWholeLoopOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> TailedLazyPlusAtomOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\D+?b", "xayzb", "Q"),
            ("a\\S+?b", "xayzb", "Q"),
            ("a\\S+?b", "xa  b", "Q"),
            ("a\\W+?b", "xayzb", "Q"),
            ("a\\P{Nd}+?b", "xayzb", "Q"),
            ("[^x]+?b", "xayzb", "Q"),
            ("a\\d+?b", "xa12b", "Q"),
            ("a\\w+?b", "xa_b", "Q"),
            ("a\\p{L}+?b", "xayb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(TailedLazyPlusAtomOwnedCases))]
    public void TailedLazyPlusAtomOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> TailedLazySpaceShapingOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\s+?b", "xa b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(TailedLazySpaceShapingOwnedCases))]
    [MemberData(nameof(SpaceMinBoundShapingOwnedCases))]
    public void BoundedSpaceOperationsMatchOracle(string pattern, RegexOptions options, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> TailedWholeLoopDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("([^x]b)+c", RegexOptions.CultureInvariant, "\U0001F600bc"),
            ("a[^x]+b", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "xa\U0001F600b"),
            ("a\\D{2,}?b", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "xa\U0001F600b"),
            ("a\\W{2,}?b", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "xa\U0001F600b"),
            ("a[^x]{2,}?b", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "xa\U0001F600b"),
            ("a\\s{2,}b", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            (".a[^x]+b", RegexOptions.CultureInvariant, "xca\U0001F600b"),
            ("(a[^x]+b|c)+", RegexOptions.CultureInvariant, "xa\U0001F600bc"),
            (".a[0-9]+b", RegexOptions.CultureInvariant, "xa12b\U0001F600"),
            ("(?<=x)[^x]+", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            ("(?<=\U0001F600)a+", RegexOptions.CultureInvariant, "\U0001F600aay"),
            ("a+(?=\U0001F600)", RegexOptions.CultureInvariant, "xaay\U0001F600"),
            ("[^x]{2,}?b", RegexOptions.CultureInvariant, "x\U0001F600b"),
            ("\\D{2,}?b", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            (".{2,}?b", RegexOptions.CultureInvariant, "x\U0001F600b"),
            ("(?i)A[^x]{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            ("(?i)A\\D{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            ("(?i)A\\S{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            ("(?i)A\\W{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(TailedWholeLoopDeclineCases))]
    public void TailedWholeLoopMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedHeadedRangeAsciiMissCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?i)A\\D{2,}?B", "xa12b"),
            ("(?i)A\\S{2,}?B", "xa  b"),
            ("(?i)A\\W{2,}?B", "xayzb"),
            ("(?i)A[^x]{2,}?B", "xaxb"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedHeadedRangeAsciiMissCases))]
    public void FoldedHeadedRangeAsciiMissOwnsDiscovery(string pattern, RegexOptions options, string text)
    {
        // Folded headed ranges miss ASCII gaps with zero fallback construction:
        // the folded head matches, the gap rejects, and no candidate survives
        // to verification on either lane. Parity holds and construction is
        // pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.False(oracle.IsMatch(text));
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.False(isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(0, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        Assert.False(matchRegex.Match(input).Success);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedAtomOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(?i)a\\D+?b", "xayZb", "Q"),
            ("(?i)a\\S+?b", "xayZb", "Q"),
            ("(?i)a\\W+?b", "xa, b", "Q"),
            ("(?i)a\\P{Nd}+?b", "xayZb", "Q"),
            ("(?i)[^x]+?b", "xayZb", "Q"),
            ("(?i)a\\d+?b", "xa12b", "Q"),
            ("(?i)a\\w+?b", "xa_Bb", "Q"),
            ("(?i)A[^x]{2,}B", "xayZB", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedAtomOwnedCases))]
    public void FoldedAtomOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> RtlCategoryRangeAsciiCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a\\D{2,}?b", "xayzb"),
            ("a\\W{2,}?b", "xa12b"),
            ("a[^x]{2,}?b", "xaxb"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.RightToLeft | RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(RtlCategoryRangeAsciiCases))]
    public void RtlCategoryRangeAsciiOwnsDiscovery(string pattern, RegexOptions options, string text)
    {
        // Right-to-left category ranges serve ASCII input fully owned on
        // both lanes: right-to-left candidate search needs no verification
        // and constructs no fallback on IsMatch, Count, or Match. Parity
        // holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedRangeAstralMissCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a\\d{2,}?b", "xa\U0001F600b"),
            ("a\\d{2,5}b", "xa\U0001F600b"),
            ("a\\w{2,}?b", "xa\U0001F600b"),
            ("a\\s{2,}?b", "xa12b"),
            ("a\\p{Nd}{2,}?b", "xayzb"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedRangeAstralMissCases))]
    public void HeadedRangeAstralMissOwnsDiscovery(string pattern, RegexOptions options, string text)
    {
        // Sensitive headed ranges miss astral-adjacent input with zero fallback
        // construction: the positive bodies (digit, word, space, Nd) reject the
        // gap content on every lane, and no candidate survives to verification.
        // Parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.False(oracle.IsMatch(text));
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.False(isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(0, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        Assert.False(matchRegex.Match(input).Success);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DotTailedLoopDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("a[0-9]+.", RegexOptions.CultureInvariant, "xa12b\U0001F600"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(DotTailedLoopDeclineCases))]
    public void DotTailedLoopMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        // R05 unit subject: matching and split projection own admitted UTF-16
        // units and completed UTF-16 replacement output.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        if (!compiled)
        {
            Assert.True(ReferenceEquals(matchRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, matchRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, "Q");
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes("Q"));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, "Q"u8));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, "Q"u8, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredLoopOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("^a[^x]+b", "a\U0001F600b", "Q"),
            ("^[^x]+b", "\U0001F600b", "Q"),
            ("^.+b", "\U0001F600b", "Q"),
            ("a[^x]+b$", "xa\U0001F600b", "Q"),
            ("^a[^x]+b$", "a\U0001F600b", "Q"),
            ("^a[^x]+?b$", "xa\U0001F600b", "Q"),
            ("^a[^x]+?b$", "a\U0001F600b", "Q"),
            ("\\Ga[^x]+b", "xa\U0001F600b", "Q"),
            ("\\Ga[^x]+b", "a\U0001F600b", "Q"),
            ("\\Ga.+?b", "a\U0001F600b", "Q"),
            ("\\G(?:ab)+", "ab\U0001F600ab", "Q"),
            ("^.+?b$", "\U0001F600b", "Q"),
            ("^a.+?b$", "a\U0001F600b", "Q"),
            ("^[^x]+$", "\U0001F600", "Q"),
            ("^.+$", "\U0001F600yy", "Q"),
            ("(?m)^a[^x]+b", "q\na\U0001F600b", "Q"),
            ("(?m)a[^x]+b$", "xa\U0001F600b\nzz", "Q"),
            ("^(a[^x]+b)$", "a\U0001F600b", "<$1>"),
            ("(?i)^A[^x]+B$", "a\U0001F600b", "Q"),
            ("(?i)\\Ga+b", "AAb\U0001F600", "Q"),
            ("^\\D+b$", "a\U0001F600b", "Q"),
            ("^[a-z]+$", "abc\U0001F600", "Q"),
            ("^a[^x]+b$", "axxb", "Q"),
            ("^[0-9]+$", "12\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredLoopOwnedCases))]
    public void AnchoredLoopOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedMatchingAndOutput(pattern, options, text, template, 4, 16);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredLoopDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("^a[^x]+b", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a\U0001F600b"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredLoopDeclineCases))]
    public void AnchoredLoopMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        // R05 unit subject: matching and split projection own admitted UTF-16
        // units and completed UTF-16 replacement output.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Replace(text, "Q"), Encoding.UTF8.GetString(stringRegex.Replace(input, "Q"u8)));
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredLoopAstralOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("^a[^x]*b$", "axa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredLoopAstralOwnedCases))]
    public void AnchoredLoopAstralOwnsInput(string pattern, RegexOptions options, string text, string template)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> WholeLoopFromOffsetOwnedCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("[^x]+", "x\U0001F600y", 0),
            ("[^x]+", "x\U0001F600y", 1),
            ("[^x]+", "x\U0001F600y", 2),
            ("[^x]+", "x\U0001F600y", 3),
            ("a[^x]+b", "xa\U0001F600b", 0),
            ("a[^x]+b", "xa\U0001F600b", 1),
            ("a[^x]+b", "xa\U0001F600b", 2),
            ("a[^x]+b", "xa\U0001F600b", 3),
            ("^a[^x]+b$", "a\U0001F600b", 0),
            ("^a[^x]+b$", "a\U0001F600b", 1),
            ("^a[^x]+b$", "a\U0001F600b", 2),
            ("(?i)[^x]+B", "x\U0001F600B", 0),
            ("(?i)[^x]+B", "x\U0001F600B", 2),
            ("(?i)[^x]+B", "x\U0001F600B", 3),
            (".+b", "\U0001F600b", 0),
            (".+b", "\U0001F600b", 1),
            (".+b", "\U0001F600b", 2),
            ("\\p{L}+", "a\U0001F600b", 0),
            ("\\p{L}+", "a\U0001F600b", 1),
            ("\\p{L}+", "a\U0001F600b", 2),
            ("[\\p{L}_]+", "a_b\U0001F600c", 0),
            ("[\\p{L}_]+", "a_b\U0001F600c", 4),
            ("(?>[^x]+)", "a\U0001F600b", 0),
            ("(?>[^x]+)", "a\U0001F600b", 2),
            ("a.+b", "xa\U0001F600b", 0),
            ("a.+b", "xa\U0001F600b", 2),
            ("a.+?b", "xa\U0001F600b", 0),
            ("a.+?b", "xa\U0001F600b", 2),
            ("\\ba[^x]+b\\b", "x a\U0001F600b!", 0),
            ("\\ba[^x]+b\\b", "x a\U0001F600b!", 2),
            ("[\\p{Cs}a]+", "a\U0001F600b", 0),
            ("[\\p{Cs}a]+", "a\U0001F600b", 1),
            ("\\Ga[^x]+b", "xa\U0001F600b", 0),
            ("\\Ga[^x]+b", "xa\U0001F600b", 1),
            ("\\Ga[^x]+b", "a\U0001F600b", 0),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(WholeLoopFromOffsetOwnedCases))]
    public void WholeLoopFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        Utf8AsciiScalarOperationAssertions.Offset(pattern, options, text, offset);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> WholeLoopStarFromOffsetOwnedCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("[^x]*", "ax\U0001F600b", 0),
            ("[^x]*", "ax\U0001F600b", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(WholeLoopStarFromOffsetOwnedCases))]
    public void WholeLoopStarFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)), []).Success);
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var countRegex = new Utf8Regex(pattern, options: options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, Utf8MatchSequence.Count(countRegex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)))));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)), []);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes);
        }
        Utf8AsciiScalarOperationAssertions.Owned(matchRegex);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset))));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);

    }

    public static System.Collections.Generic.IEnumerable<object?[]> WholeLoopFromOffsetDeclineCases()
    {
        (string pattern, RegexOptions options, string text, int offset)[] rows =
        [
            ("(.+?)", RegexOptions.CultureInvariant, "\U0001F600yy", 0),
            ("(.+?)", RegexOptions.CultureInvariant, "\U0001F600yy", 1),
            ("((.)+)", RegexOptions.CultureInvariant, "\U0001F600", 0),
            ("((.)+)", RegexOptions.CultureInvariant, "\U0001F600", 1),
            ("[^x]+", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "x\U0001F600y", 1),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.offset };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(WholeLoopFromOffsetDeclineCases))]
    public void WholeLoopFromOffsetMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, int offset)
    {
        Utf8AsciiScalarOperationAssertions.Offset(pattern, options, text, offset);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableFromOffsetOwnedCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("x*", "ax\U0001F600b", 0),
            ("x*", "ax\U0001F600b", 2),
            ("x*", "ax\U0001F600b", 3),
            ("a*", "a\U0001F600a", 0),
            ("a*", "a\U0001F600a", 1),
            ("a*", "xa\U0001F600", 0),
            ("a*", "xa\U0001F600", 2),
            ("x{0,3}", "ax\U0001F600b", 0),
            ("x{0,3}", "ax\U0001F600b", 2),
            ("x{0,3}", "ax\U0001F600b", 3),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableFromOffsetOwnedCases))]
    public void EmptyCapableFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        Utf8AsciiScalarOperationAssertions.Offset(pattern, options, text, offset);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableReplaceOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("x*", "ax\U0001F600b", "Q"),
            ("x*", "ax\U0001F600b", "<$&>"),
            ("x*", "\U0001F600", "Q"),
            ("a*", "a\U0001F600a", "Q"),
            ("a*", "a\U0001F600a", "<$&>"),
            ("(?:ab)*", "ab\U0001F600ab", "Q"),
            ("(x*)", "ax\U0001F600b", "<$1>"),
            ("(?i)X*", "ax\U0001F600b", "Q"),
            ("\\d*", "a1\U0001F600b", "Q"),
            ("x*", "ax\U0001F600b", "<$`|$&|$'>"),
            ("x*", "abc", "Q"),
            ("a*", "aa", "<$&>"),
            ("a*", "xa\U0001F600", "Q"),
            ("x*", "x\U0001F600", "Q"),
            ("a*", "\U0001F600", "Q"),
            ("(a*)", "xa\U0001F600", "<$1>"),
            ("a*b*", "xa\U0001F600b", "Q"),
            ("(a|b)*", "xab\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableReplaceOwnedCases))]
    public void EmptyCapableReplaceOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableHalvesClosedOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("[^x]*", "a\U0001F600b", "Q"),
            ("(.*)", "\U0001F600", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableHalvesClosedOwnedCases))]
    public void EmptyCapableHalvesClosedOwnsInput(string pattern, RegexOptions options, string text, string template)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Utf8AsciiScalarOperationAssertions.Owned(bytesRegex);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        var utf8Regex = new Utf8Regex(pattern, options: options);
        var utf8Actual = utf8Regex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, utf8Actual);
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
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableExtraOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("^x*", "x\U0001F600x", "Q"),
            ("a*$", "xa\U0001F600", "Q"),
            ("^a*$", "xa\U0001F600", "Q"),
            ("x{0,3}", "ax\U0001F600b", "Q"),
            ("(?:ab){0,2}", "ab\U0001F600ab", "Q"),
            ("(?i)X{0,2}", "ax\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableExtraOwnedCases))]
    public void EmptyCapableExtraOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableDiscoveryOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a*)", "xa\U0001F600", "<$1>"),
            ("a*b*", "xa\U0001F600b", "Q"),
            ("(a|b)*", "xab\U0001F600", "Q"),
            ("a*", "xa\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableDiscoveryOwnedCases))]
    public void EmptyCapableDiscoveryOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableStringDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("x*", "ax\U0001F600b", "Q"),
            ("x*", "\U0001F600", "Q"),
            ("a*", "a\U0001F600a", "<$&>"),
            ("(?:ab)*", "ab\U0001F600ab", "Q"),
            ("(x*)", "ax\U0001F600b", "<$1>"),
            ("(?i)X*", "ax\U0001F600b", "Q"),
            ("\\d*", "a1\U0001F600b", "Q"),
            ("x*", "ax\U0001F600b", "<$`|$&|$'>"),
            ("^x*", "x\U0001F600x", "Q"),
            ("a*$", "xa\U0001F600", "Q"),
            ("^a*$", "xa\U0001F600", "Q"),
            ("x{0,3}", "ax\U0001F600b", "Q"),
            ("(?:ab){0,2}", "ab\U0001F600ab", "Q"),
            ("(?i)X{0,2}", "ax\U0001F600b", "Q"),
            ("\\d{0,2}", "a1\U0001F600b", "Q"),
            ("(x{0,3})", "ax\U0001F600b", "<$1>"),
            ("a*", "xa\U0001F600", "Q"),
            ("x*", "x\U0001F600", "Q"),
            ("a*", "\U0001F600", "Q"),
            ("^[0-9]*$", "12\U0001F60034", "Q"),
            ("(a*)", "xa\U0001F600", "<$1>"),
            ("a*b*", "xa\U0001F600b", "Q"),
            ("(a|b)*", "xab\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableStringDeclineCases))]
    public void EmptyCapableStringReplacementIsOwned(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableScalarOperationCases()
    {
        (string pattern, string text)[] rows =
        [
            ("x*", "ax\U0001F600b"),
            ("a*", "a\U0001F600a"),
            ("a*", "xa\U0001F600"),
            ("x*", "x\U0001F600"),
            ("a*", "\U0001F600"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableScalarOperationCases))]
    public void EmptyCapableOperationsPreserveScalarBoundaries(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> BoundedNonEmptyAstralOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("x{2,4}", "axxx\U0001F600b", "Q"),
            ("\\d{2,4}", "a12\U0001F600b", "Q"),
            ("[0-9]{2,4}", "a12\U0001F600b", "Q"),
            ("(?:ab){1,3}", "xab\U0001F600ab", "Q"),
            ("(x{2,4})", "axxx\U0001F600b", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(BoundedNonEmptyAstralOwnedCases))]
    public void BoundedNonEmptyAstralOwnsInput(string pattern, RegexOptions options, string text, string template)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        // Bounded repeats with nonzero minima route to dedicated owned families
        // (literal-family kernels here), not the slice, so there is no slice
        // program to assert; the lanes below pin owned routing with parity.
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        var utf8Regex = new Utf8Regex(pattern, options: options);
        var utf8Actual = utf8Regex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, utf8Actual);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> BoundedEmptyAstralOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("\\d{0,2}", "a1\U0001F600b", "Q"),
            ("(x{0,3})", "ax\U0001F600b", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(BoundedEmptyAstralOwnedCases))]
    public void BoundedEmptyAstralOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

        public static System.Collections.Generic.IEnumerable<object?[]> PositiveCategoryLoopOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("\\p{L}+", "a\U0001F600b", "Q"),
            ("\\p{Nd}+", "a12\U0001F600b", "Q"),
            ("\\p{Lu}+", "aA\U0001F600B", "Q"),
            ("(\\p{L}+)", "a\U0001F600b", "<$1>"),
            ("(?i)\\p{Lu}+", "aA\U0001F600", "Q"),
            ("(?i)\\p{Lu}+", "a\u212Ab\U0001F600", "Q"),
            ("\\p{L}+", "abc", "Q"),
            ("\\p{IsGreek}+", "a\u03B1\U00010400b", "Q"),
            ("\\P{L}+", "a\U0001F600b", "Q"),
            ("\\P{Lu}+", "a\U00010400b", "Q"),
            ("\\P{L}+", "\U00010400", "Q"),
            ("a\\P{L}+b", "xa\U0001F600b", "Q"),
            ("a\\P{L}+?b", "xa\U0001F600b", "Q"),
            ("(\\P{L}+)", "a\U0001F600b", "<$1>"),
            ("(?i)\\P{Lu}+", "aA\U0001F600B", "Q"),
            ("(?i)\\P{IsBasicLatin}+", "\U0001F600", "Q"),
            ("\\P{Nd}+", "x\U0001F600yz", "Q"),
            ("\\p{L}+?b", "\U0001F600aab", "Q"),
            ("\\d{2,}", "x12\U0001F600", "Q"),
            ("\\d+", "x12\U0001F600", "Q"),
            ("\\D+", "xa\U0001F600b", "Q"),
            ("\\d{2,}?", "x12\U0001F600", "Q"),
            ("a\\S+b", "xa\U0001F600b", "Q"),
            ("a\\P{Nd}+b", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(PositiveCategoryLoopOwnedCases))]
    public void PositiveCategoryLoopOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> PositiveCategoryLoopDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("(\\p{L})+", RegexOptions.CultureInvariant, "\U0001F600"),
            ("\\p{L}+", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a\U0001F600b"),
            ("\\p{L}+?", RegexOptions.CultureInvariant, "\U0001F600ab"),
            ("\\P{Cs}+", RegexOptions.CultureInvariant, "a\U0001F600b"),
            ("\\p{Cs}+", RegexOptions.CultureInvariant, "a\U0001F600b"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(PositiveCategoryLoopDeclineCases))]
    public void PositiveCategoryLoopMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> ComposedPositiveLoopOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\p{L}+b", "\U0001F600xabb", "Q"),
            ("a\\p{Nd}+b", "\U0001F600a12b", "Q"),
            ("[\\da]+", "a1\U0001F600b", "Q"),
            ("([\\da]+)", "a1\U0001F600b", "<$1>"),
            ("(?i)[\\da]+", "A1\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(ComposedPositiveLoopOwnedCases))]
    public void ComposedPositiveLoopOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedMatchingAndOutput(pattern, options, text, template, 16, 128);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> PositiveCategorySetOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("[\\p{L}_]+", "a_b\U0001F600c", "Q"),
            ("[\\p{Nd}_]+", "a1_b\U0001F600", "Q"),
            ("[\\d\\p{L}]+", "a1b\U0001F600c", "Q"),
            ("([\\p{L}_]+)", "a_b\U0001F600c", "<$1>"),
            ("(?i)[\\p{L}_]+", "A_B\U0001F600c", "Q"),
            ("[\\p{L}_]+", "a\U0001F600b", "Q"),
            ("[\\p{L}_]+", "abc", "Q"),
            ("[^\\p{L}_]+", "a_b\U0001F600c", "Q"),
            ("[\\P{Nd}_]+", "a1\U0001F600b", "Q"),
            ("[\\p{Cs}_]+", "a\U0001F600b", "Q"),
            ("(?i)[^\\p{L}_]+", "A_B\U0001F600c", "Q"),
            ("[^\\p{L}_]+?b", "x_\U0001F600xb", "Q"),
            ("([^\\p{L}_]+)", "a_b\U0001F600c", "<$1>"),
            ("[\\P{L}]+", "1\U0001F6002", "Q"),
            ("[^\\P{L}]+", "a\U0001F600b", "Q"),
            ("[\\p{L}_]+?b", "\U0001F600aab", "Q"),
            ("[\\P{L}a]+", "a1\U0001F600b", "Q"),
            ("[^\\P{L}a]+", "a1\U0001F600b", "Q"),
            ("[\\p{Cs}a]+", "a\U0001F600b", "Q"),
            ("[^\\p{Cs}a]+", "1\U0001F6002", "Q"),
            ("(?i)[\\p{Cs}a]+", "A\U0001F600b", "Q"),
            ("[\\D_]+", "a_\U0001F60012", "Q"),
            ("[^\\D_]+", "12a\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(PositiveCategorySetOwnedCases))]
    public void PositiveCategorySetOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> PositiveCategorySetDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("([\\p{L}])+", RegexOptions.CultureInvariant, "\U0001F600"),
            ("[\\p{IsHighSurrogates}a]+", RegexOptions.CultureInvariant, "a\U0001F600b"),
            ("[^\\P{Cs}]+", RegexOptions.CultureInvariant, "a\U0001F600b"),
            ("[\\p{L}_]+", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a_b\U0001F600c"),
            ("[\\p{L}_]+?", RegexOptions.CultureInvariant, "a_b\U0001F600c"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(PositiveCategorySetDeclineCases))]
    public void PositiveCategorySetMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> ComplexFeatureShapingOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a)\\1", "xaax", "Q"),
            ("(?>a+)b", "xaab", "Q"),
            ("(a)?(?(1)ab|cd)", "xaabx", "Q"),
            ("(a)?(?(1)ab|cd)", "xcdx", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(ComplexFeatureShapingOwnedCases))]
    public void ComplexFeatureShapingStaysOwned(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AtomicLoopOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(?>[^x]+)", "a\U0001F600b", "Q"),
            ("(?>a[^x]+b)", "xa\U0001F600b", "Q"),
            ("((?>[^x]+))", "a\U0001F600b", "<$1>"),
            ("(?>(?:ab|[^x])+)", "xab\U0001F600", "Q"),
            ("(?i)(?>a[^x]+b)", "xA\U0001F600b", "Q"),
            ("(?>a+)", "aA", "Q"),
            ("(?>a+)", "aA\U0001F600", "Q"),
            ("(?>.+?b)", "x\U0001F600b", "Q"),
            ("(?>a.+?b)bc", "xa\U0001F600bbc", "Q"),
            ("(?>a.+?b)c", "xa\U0001F600abc", "Q"),
            ("(?>a+?)", "xaay\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AtomicLoopOwnedCases))]
    public void AtomicLoopOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedMatchingAndOutput(pattern, options, text, template, 16, 128);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AtomicLoopDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("(?>[^x]b)+", RegexOptions.CultureInvariant, "\U0001F600b"),
            ("(?>a|[^x])+", RegexOptions.CultureInvariant, "xA\U0001F600y"),
            ("(?>.+?)", RegexOptions.CultureInvariant, "\U0001F600yy"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(AtomicLoopDeclineCases))]
    public void AtomicLoopMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DotGapOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a.+b", "xaab", "Q"),
            ("a.+b", "xa\nb", "Q"),
            ("ab.+cd", "xabcdx", "Q"),
            ("a.+?b", "xaab", "Q"),
            ("a.+?b", "xa\U0001F600b", "Q"),
            ("a.+?b", "xa\U0001F600bxb", "Q"),
            ("a[^x]+?b", "xa\U0001F600b", "Q"),
            ("(?i)a.+?b", "xa\U0001F600b", "Q"),
            ("(a.+?b)", "xa\U0001F600b", "Q"),
            ("(.+?b)", "x\U0001F600b", "Q"),
            ("a[^\n]+b", "xaab", "Q"),
            ("a[^\n]+b", "xa\nb", "Q"),
            ("a.+b", "xa\U0001F600b", "Q"),
            ("ab.+cd", "xab\U0001F600cd", "Q"),
            ("a[^\n]+b", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(DotGapOwnedCases))]

    public void DotGapOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedMatchingAndOutput(pattern, options, text, template, 16, 128);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DirectedAsciiOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            (".a[0-9]+b", "xca12b", "Q"),
            (".aab", "xcaab", "Q"),
            (".a[^x]+b", "xcaayb", "Q"),
            (".ab", "xcab", "Q"),
            ("a[0-9]+.", "xa12b", "Q"),
            ("a[^x]+.", "xaayb", "Q"),
            ("a.+.", "xaayb", "Q"),
            ("ab.+", "xabxy", "Q"),
            ("a.b", "xaab", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(DirectedAsciiOwnedCases))]
    public void DirectedAsciiOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DirectedAstralDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            (".a[^x]+b", "xca\U0001F600b", "Q"),
            (".a[0-9]+b", "xa12b\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(DirectedAstralDeclineCases))]
    public void DirectedAstralMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        // R05 unit subject: matching and split projection own admitted UTF-16
        // units and completed UTF-16 replacement output.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> OwnedAsciiTractCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a[^x]+b|c)+", "xaabbc", "Q"),
            ("(?i)A[^x]{2,}?B", "xaxb", "Q"),
            ("a\\W{2,}b", "xa, b", "Q"),
            ("\\b\\w+\\b", "a foo b", "Q"),
            ("a\\d{2,}b", "xa12b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(OwnedAsciiTractCases))]
    public void OwnedAsciiTractsServeFullyOwned(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SpaceMinBoundShapingOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\s{2,}b", "xa  b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> PositiveCategoryRangeAstralCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\p{L}{2,5}b", "xa\U00010400b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(PositiveCategoryRangeAstralCases))]
    public void PositiveCategoryRangeAstralMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        // R05 unit subject: matching and split projection own admitted UTF-16
        // units and completed UTF-16 replacement output.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        if (!compiled)
        {
            Assert.True(ReferenceEquals(matchRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, matchRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> PositiveCategoryRangeAsciiOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\p{L}{2,}b", "xayzb", "Q"),
            ("a\\p{Nd}{2,}b", "xa12b", "Q"),
            ("a\\p{L}+b", "xa\U00010400b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(PositiveCategoryRangeAsciiOwnedCases))]
    public void PositiveCategoryRangeAsciiOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralLiteralLoopDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("\U0001F600+b", "x\U0001F600b", "Q"),
            ("\U0001F600*b", "x\U0001F600b", "Q"),
            ("a\U0001F600+b", "xa\U0001F600b", "Q"),
            ("(\U0001F600)+", "x\U0001F600b", "<$1>"),
            ("\U0001F600+b", "xab", "Q"),
            ("a\U0001F600+b", "xaab", "Q"),
            ("[\U0001F600a]+b", "x\U0001F600ab", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralLiteralLoopDeclineCases))]
    public void AstralLiteralLoopsUseOwnedUnits(string pattern, RegexOptions options, string text, string template)
    {
        // UTF-16 pattern atoms now own these former astral-pattern declines.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options: options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EcmaScriptLaneParityCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("ab", "xxabxx", "Q"),
            ("a+b", "xaab", "Q"),
            ("a\\w+b", "xa_b", "Q"),
            ("a\\d+b", "xa12b", "Q"),
            ("a\\s+b", "xa b", "Q"),
            ("a[^x]+b", "xayzb", "Q"),
            ("(a)(b)", "xxabxx", "<$1$2>"),
            ("^a+$", "xaab", "Q"),
            ("[a-z]+", "xxabcxx", "Q"),
            ("(ab|cd)ef", "xxcdefxx", "Q"),
            ("a.+b", "xayb", "Q"),
            ("a.+b", "xa\nb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(EcmaScriptLaneParityCases))]
    public void EcmaScriptLanesMatchOracle(string pattern, string text, string template)
    {
        // ECMAScript lanes now use the shared owned VM and replacement grammar.
        var input = Encoding.UTF8.GetBytes(text);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        foreach (var options in new[] { RegexOptions.ECMAScript, RegexOptions.ECMAScript | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var isMatchRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            var countRegex = new Utf8Regex(pattern, options: options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            var matchRegex = new Utf8Regex(pattern, options: options);
            var expectedMatch = oracle.Match(text);
            var actualMatch = matchRegex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
            }
            Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            var detailedRegex = new Utf8Regex(pattern, options: options);
            var expectedDetailed = oracle.Match(text);
            var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
            Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
            Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
            var expected = oracle.Replace(text, template);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            var bytesRegex = new Utf8Regex(pattern, options: options);
            var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
            Assert.Equal(expectedBytes, bytesActual);
            Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            var stringRegex = new Utf8Regex(pattern, options: options);
            var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
            Assert.Equal(expected, stringActual);
            Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
            var destinationRegex = new Utf8Regex(pattern, options: options);
            var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
            Assert.Equal(System.Buffers.OperationStatus.Done, status);
            Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
            Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> RtlFoldedLiteralShapingOwnedCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("(?i)ab", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "xxABxx", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(RtlFoldedLiteralShapingOwnedCases))]
    public void RtlFoldedLiteralShapingOwnsInput(string pattern, RegexOptions options, string text, string template)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AlternationRtlSinglelineOwnedCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("(a.+|zb)", RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xa\nzb", "Q"),
            ("(a.+|zb)", RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
            ("(a.+|zb)", RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xz", "Q"),
            ("(zb|z.+)", RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AlternationRtlSinglelineOwnedCases))]
    public void AlternationRtlSinglelineOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AlternationExplicitSinglelineOwnedCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("(a.+|zb)", RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xa\nzb", "Q"),
            ("(a.+|zb)", RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
            ("(a.+|zb)", RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xz", "Q"),
            ("(zb|z.+)", RegexOptions.ExplicitCapture | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AlternationExplicitSinglelineOwnedCases))]
    public void AlternationExplicitSinglelineOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AlternationExplicitRtlSinglelineOwnedCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("(a.+|zb)", RegexOptions.ExplicitCapture | RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xa\nzb", "Q"),
            ("(a.+|zb)", RegexOptions.ExplicitCapture | RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
            ("(a.+|zb)", RegexOptions.ExplicitCapture | RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xz", "Q"),
            ("(zb|z.+)", RegexOptions.ExplicitCapture | RegexOptions.RightToLeft | RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AlternationExplicitRtlSinglelineOwnedCases))]
    public void AlternationExplicitRtlSinglelineOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> RtlWordSplitCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("\\bword\\b", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "xx word xx", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(RtlWordSplitCases))]
    public void RtlWordKeepsDiscoveryAndOwnsResults(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AlternationSinglelineOwnedCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("(a.+|zb)", RegexOptions.Singleline | RegexOptions.CultureInvariant, "xa\nzb", "Q"),
            ("(a.+|zb)", RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
            ("(a.+|zb)", RegexOptions.Singleline | RegexOptions.CultureInvariant, "xz", "Q"),
            ("(zb|z.+)", RegexOptions.Singleline | RegexOptions.CultureInvariant, "xzb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AlternationSinglelineOwnedCases))]
    public void AlternationSinglelineOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> BoundedSplitOwnedCases()
    {
        (string pattern, RegexOptions options, string text, int count)[] rows =
        [
            ("a+", RegexOptions.CultureInvariant, "xaaxbbx", 2),
            ("a+", RegexOptions.CultureInvariant, "xaaxbbx", 3),
            ("(a+)", RegexOptions.CultureInvariant, "xaaxbbx", 2),
            ("(a+)", RegexOptions.CultureInvariant, "xaaxbbx", 3),
            ("a+(?=b)", RegexOptions.CultureInvariant, "xaabxb", 2),
            ("a+(?=b)", RegexOptions.CultureInvariant, "xaabxb", 3),
            ("\\bword\\b", RegexOptions.CultureInvariant, "xx word yy word zz", 2),
            ("\\bword\\b", RegexOptions.CultureInvariant, "xx word yy word zz", 3),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.count };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.count };
        }
    }

    [Theory]
    [MemberData(nameof(BoundedSplitOwnedCases))]
    public void BoundedSplitOwnsInput(string pattern, RegexOptions options, string text, int count)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var splitRegex = new Utf8Regex(pattern, options: options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], global::System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), global::System.Text.Encoding.UTF8.GetByteCount(text[start..end])));
        }
        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }
        Assert.Equal(expectedSplits, actualSplits);
        Utf8AsciiScalarOperationAssertions.Owned(splitRegex);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> MinBoundHeadedStarOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a.*b", "xa\U0001F600b", "Q"),
            ("a[^x]*b", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(MinBoundHeadedStarOwnedCases))]
    public void MinBoundHeadedStarServesOwnedDiscovery(string pattern, RegexOptions options, string text, string template)
    {

        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> MinBoundHeadedAstralCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]{2,}?b", "xa\U0001F600b", "Q"),
            ("a.{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\D{2,}?b", "xa\U0001F600b", "Q"),
            ("a.{2,5}?b", "xa\U0001F600b", "Q"),
            ("a[^x]{2,5}?b", "xa\U0001F600b", "Q"),
            ("a\\D{2,5}?b", "xa\U0001F600b", "Q"),
            ("a.*?b", "xa\U0001F600b", "Q"),
            ("a[^x]*?b", "xa\U0001F600b", "Q"),
            ("a\\D*?b", "xa\U0001F600b", "Q"),
            ("a\\D*b", "xa\U0001F600b", "Q"),
            ("a\\S{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\P{Nd}{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\P{Lu}{2,}?b", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(MinBoundHeadedAstralCases))]
    public void MinBoundHeadedAstralMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void HeadedGroupCategoryRangeAstralMatchesOracleAndPinsEngineUse(RegexOptions options)
    {
        foreach (var pattern in new[] { @"a\W{2,}?b", @"a\P{L}{2,}?b" })
            Utf8AsciiScalarOperationAssertions.Operations(pattern, options, "xa\U0001f600b");
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedGroupCategoryVerifierUseCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a\\W{2,}?b", "xa\U0001F600b"),
            ("a\\W{2,}b", "xa\U0001F600b"),
            ("a\\P{L}{2,}?b", "xa\U0001F600b"),
            ("a\\P{L}{2,}b", "xa\U0001F600b"),
            ("a\\W{2,}?b", "xa\U0001F600c"),
            ("a\\P{L}{2,}b", "xa\U0001F600c"),
            ("a\\P{L}{2,}?b", "xa12b"),
            ("a\\W{2,}?b", "xa12b"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.None, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedGroupCategoryVerifierUseCases))]
    public void HeadedGroupCategoryPreservesResultsWithoutCandidateSearch(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
        var subject = new Utf8AsciiScalarOperationAssertions.Subject(text);
        using var regex = new Utf8Regex(pattern, options: options);
        Assert.Equal(0, regex.CollectIsMatchDiagnostics(subject.Input).SearchCandidates);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedWordGroupMinBoundDivertCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(?i)a\\W*b", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedWordGroupMinBoundDivertCases))]
    public void FoldedWordGroupMinBoundMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        // R05 unit subject: matching and split projection own admitted UTF-16
        // units and completed UTF-16 replacement output.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.Equal(1, countRegex.Count(input));
        Assert.True(countRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        Assert.True(matchRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> BoundedExactDivergentOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a|ab){2}", "xaab", "Q"),
            ("(a|ab){2}", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(BoundedExactDivergentOwnedCases))]
    public void BoundedExactDivergentOwnsInput(string pattern, RegexOptions options, string text, string template)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
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
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
    }

    public static System.Collections.Generic.IEnumerable<object?[]> RangedDivergentCountOrderedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(a|ab){2,3}", "xaabab", "Q"),
            ("(a|ab){2,3}", "xaabab\U0001F600", "Q"),
            ("(a|ab){1,3}", "xaabab", "Q"),
            ("(a|ab){2,3}c", "xaababc", "Q"),
            ("(a|ab){3}", "xaababab", "Q"),
            ("(?i)(a|ab){2,3}", "xaabab", "Q"),
            ("(?i)(a|ab){2,3}", "xaABab", "Q"),
            ("(?i)(a|ab){2,3}c", "xaababc", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(RangedDivergentCountOrderedCases))]
    public void RangedDivergentCountServesOrderedSpans(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedMatchingAndOutput(pattern, options, text, template, 4, 16);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedCategoryRangeAstralCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]{1,3}?b", "xa\U0001F600b", "Q"),
            ("a\\S{2,3}?b", "xa\U0001F600b", "Q"),
            ("a\\p{L}{2,3}?b", "xa\U00010400b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedCategoryRangeAstralCases))]
    public void HeadedCategoryRangeAstralMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedUnboundedLazyRangeAstralCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\D{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\S{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\W{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\p{L}{2,}?b", "xa\U00010400b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedUnboundedLazyRangeAstralCases))]
    public void HeadedUnboundedLazyRangeAstralMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template, requireSlice: true);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedUnboundedLazyRangeAstralMissCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]{2,}?b", "xa\U0001F600c", "Q"),
            ("a\\D{2,}?b", "xa\U0001F600c", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedUnboundedLazyRangeAstralMissCases))]
    [MemberData(nameof(HeadedUnboundedGreedyRangeAstralMissCases))]
    public void UnboundedHeadedRangeAstralMissMatchesOracle(string pattern, RegexOptions options, string text, string template)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var oracleHit = oracle.IsMatch(text);
        Assert.False(oracleHit);
        var sliceRegex = new Utf8Regex(pattern, options: options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.False(isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(0, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        Assert.False(matchRegex.Match(input).Success);
        var detailedRegex = new Utf8Regex(pattern, options: options);
        Assert.False(Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input)).Success);
        Assert.True(detailedRegex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var splitRegex = new Utf8Regex(pattern, options: options);
        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }
        Assert.Equal(new System.Collections.Generic.List<(string Value, int Index, int Length)> { (text, 0, input.Length) }, actualSplits);
        Assert.True(splitRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedUnboundedGreedyRangeAsciiOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]{2,}b", "xayzb", "Q"),
            ("a\\D{2,}b", "xayzb", "Q"),
            ("a\\S{2,}b", "xayzb", "Q"),
            ("a\\W{2,}b", "xayzb", "Q"),
            ("a\\P{Nd}{2,}b", "xayzb", "Q"),
            ("a\\P{L}{2,}b", "xa12b", "Q"),
            ("a[^x]{2,}b", "xaxb", "Q"),
            ("a\\D{2,}b", "xa12b", "Q"),
            ("a\\S{2,}b", "xa  b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedUnboundedGreedyRangeAsciiOwnedCases))]
    public void HeadedUnboundedGreedyRangeAsciiOwnsInput(string pattern, RegexOptions options, string text, string template)
    {

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options: options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedDetailed, actualDetailed, input, text);
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
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, Encoding.UTF8.GetBytes(template), destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedUnboundedGreedyRangeAstralMissCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]{2,}b", "xa\U0001F600c", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> MinBoundAsciiClosedOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\d{2,}b", "xa12\U0001F600", "Q"),
            ("a[0-9]{2,}b", "xa12b\U0001F600", "Q"),
            ("\\d{2,}b", "x12b\U0001F600", "Q"),
            ("a\\d{2,}", "xa12\U0001F600", "Q"),
            ("(\\d{2,})", "x12\U0001F600", "<$1>"),
            ("a\\d{2,}?b", "xa12b\U0001F600", "Q"),
            ("a\\w{2,3}?b", "xa12b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(MinBoundAsciiClosedOwnedCases))]
    public void MinBoundAsciiClosedOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedMatchingAndOutput(pattern, options, text, template, 4, 16);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> MinBoundBareAstralDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(.{2,}?)", "a\U0001F600b", "Q"),
            ("([^x]{2,}?)", "a\U0001F600b", "Q"),
            ("(\\D{2,}?)", "a\U0001F600b", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(MinBoundBareAstralDeclineCases))]
    public void MinBoundBareAstralMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedScalarOwnedCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(?i).c", "A\U0001F600C", "Q"),
            ("(?i)[^x]c", "A\U0001F600C", "Q"),
            ("(?i)(a|.)c", "A\U0001F600C", "<$1>"),
            ("(?i)(a|.)c", "A\U0001F600C", "<$2>"),
            ("(?i)a[^x]d", "\U0001F600AAD", "Q"),
            ("(?i)[^x]", "a\U0001F600b", "Q"),
            ("(?i)[^x]", "\U0001F600", "Q"),
            ("(?i)(a.c)", "\U0001F600AYC", "<$1>"),
            ("(?i)a.c", "\U0001F600xyz", "Q"),
            ("(?i)((a|.)c)", "a\U0001F600c", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedScalarOwnedCases))]
    public void FoldedScalarOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text, template);
        Utf8AsciiScalarOperationAssertions.Offset(pattern, options, text, 0);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedScalarFromOffsetOwnedCases()
    {
        (string pattern, string text, int offset)[] rows =
        [
            ("(?i)((a|.)c)", "a\U0001F600c", 0),
            ("(?i)((a|.)c)", "a\U0001F600c", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.offset };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedScalarFromOffsetOwnedCases))]
    public void FoldedScalarFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        Utf8AsciiScalarOperationAssertions.Offset(pattern, options, text, offset);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedScalarDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("(?i)k", RegexOptions.CultureInvariant, "\u212A"),
            ("(?i)a.c", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "A\U0001F600C"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedScalarDeclineCases))]
    public void FoldedScalarMatchesOracleAndPinsEngineUse(string pattern, RegexOptions options, string text)
    {
        Utf8AsciiScalarOperationAssertions.Operations(pattern, options, text);
    }

    public static IEnumerable<object?[]> OwnedOperationCases()
    {
        (string Pattern, RegexOptions Options, string Text, string Template)[] rows =
        [
            // AnchoredLoopAscii
            ("^a[^x]*b$", CultureInvariant, "axaayb", "Q"),
            ("^a[^x]*b$", CultureInvariant, "ab", "Q"),
            ("^a[^x]+b$", CultureInvariant, "axaayb", "Q"),
            ("(?<=x)a+", CultureInvariant, "xaay", "Q"),
            ("a+(?=y)", CultureInvariant, "xaay", "Q"),
            ("\\Ba+\\B", CultureInvariant, "xaay", "Q"),
            ("\\ba+\\b", CultureInvariant, "xaay", "Q"),
            ("^a[^x]+b", CultureInvariant | RightToLeft, "axaayb", "Q"),

            // ComboAscii
            ("(?i)a+b", CultureInvariant | RightToLeft, "xxAAB", "Q"),
            ("(?m:^ab)", CultureInvariant | IgnoreCase, "xx\nAB", "Q"),
            ("(?m:ab$)", CultureInvariant | IgnoreCase, "ab\nxx", "Q"),
            ("(?m:^ab$)", CultureInvariant | IgnoreCase, "AB", "Q"),
            ("(?i)(a)(b)", CultureInvariant, "xxABxx", "<$1$2>"),
            ("(?i)a[^x]+b", CultureInvariant, "xAYZb", "Q"),

            // ModeAscii
            ("(?s)a.*b", CultureInvariant, "xa\nb", "Q"),
            ("(a)(b)", CultureInvariant | ExplicitCapture, "xxabxx", "<$1$2>"),
            ("a b", CultureInvariant | IgnorePatternWhitespace, "xabx", "Q"),
            ("ab\\z", CultureInvariant, "xxab", "Q"),
            ("\\Aab", CultureInvariant, "abxx", "Q"),
            ("a*", CultureInvariant, "", "Q"),
            ("a*", CultureInvariant, "bbb", "Q"),

            // ContiguousAnchorSplit
            ("\\Gab", CultureInvariant, "abxx", "Q"),

            // BackrefAscii
            ("(a+)\\1", CultureInvariant, "xxaaax", "Q"),
            ("(a+)\\1", CultureInvariant, "xxabx", "Q"),
            ("(a+)\\1", CultureInvariant, "xaabbbaabbx", "Q"),
            ("(ab)c\\1", CultureInvariant, "xxabcabxx", "Q"),
            ("(ab)c\\1", CultureInvariant, "xxabx", "Q"),
            ("(a*)(b*)\\1\\2", CultureInvariant, "xxabx", "Q"),

            // BackrefSingleline
            ("(a.+)\\1", CultureInvariant | Singleline, "xab\nab", "Q"),
            ("(a.+)\\1", CultureInvariant | Singleline, "xabxab", "Q"),
            ("(ab.+)\\1", CultureInvariant | Singleline, "xab\ncab", "Q"),
            ("(a.+)\\1$", CultureInvariant | Singleline, "xab\nab", "Q"),

            // BackrefMultiline
            ("^(ab)\\1$", CultureInvariant | Multiline, "xx\nabab", "Q"),
            ("^(ab)\\1$", CultureInvariant | Multiline, "xx\nabxb", "Q"),
            ("(a+)\\1$", CultureInvariant | Multiline, "xx\naabb", "Q"),
            ("(a+)\\1$", CultureInvariant | Multiline, "xx\naaaa", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline, "xx\nabab\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline, "xx\nabcb", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline, "abab", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline, "xabc", "Q"),

            // ExplicitBackrefMultiline
            ("^(?<w>ab)\\1$", CultureInvariant | ExplicitCapture | Multiline, "abab", "Q"),
            ("^(?<w>ab)\\1$", CultureInvariant | ExplicitCapture | Multiline, "xx\nabab", "Q"),
            ("^(?<w>ab)\\1$", CultureInvariant | ExplicitCapture | Multiline, "xx\nabxb", "Q"),

            // LookaroundAscii
            ("a+(?=b)", CultureInvariant, "xaabx", "Q"),
            ("a+(?=b)", CultureInvariant, "xaax", "Q"),
            ("a+(?!b)", CultureInvariant, "xaax", "Q"),
            ("a+(?!b)", CultureInvariant, "xaabx", "Q"),
            ("(?<=x)a+", CultureInvariant, "xxaax", "Q"),
            ("(?<=x)a+", CultureInvariant, "xbbb", "Q"),
            ("(?<!x)a+", CultureInvariant, "bbaaa", "Q"),
            ("(?<!x)a+", CultureInvariant, "xxaax", "Q"),

            // AlternationAscii
            ("(ab|cd)ef", CultureInvariant, "xxcdefxx", "Q"),
            ("(ab|cd)ef", CultureInvariant, "xxabefxx", "Q"),
            ("(ab|cd)ef", CultureInvariant, "xxabxx", "Q"),
            ("(a|bc)+d", CultureInvariant, "xxbcdxx", "Q"),
            ("(a|bc)+d", CultureInvariant, "xxabcdxx", "Q"),
            ("(a|bc)+d", CultureInvariant, "xxabx", "Q"),
            ("x(a|bc)?y", CultureInvariant, "xxay", "Q"),
            ("x(a|bc)?y", CultureInvariant, "xxy", "Q"),
            ("(ab|a)b", CultureInvariant, "xxabb", "Q"),
            ("(ab|a)b", CultureInvariant, "xxab", "Q"),

            // WordBoundaryAscii
            ("\\bword\\b", CultureInvariant, "xx word xx", "Q"),
            ("\\bword\\b", CultureInvariant, "xxwordxx", "Q"),
            ("\\bword\\b", CultureInvariant, "xxwords", "Q"),
            ("\\Bword\\B", CultureInvariant, "xwordy", "Q"),
            ("\\Bword\\B", CultureInvariant, "xxwordxx", "Q"),
            ("(\\b\\w+\\b)", CultureInvariant, "xxhi therexx", "Q"),
            ("(\\b\\w+\\b)", CultureInvariant, "  ", "Q"),
            ("\\b(a+)\\b", CultureInvariant, "xx aa xx", "Q"),
            ("\\b(a+)\\b", CultureInvariant, "xxaaaxx", "Q"),

            // WordBoundaryMultiline
            ("\\bword\\b", CultureInvariant | Multiline, "xx\nword\nxx", "Q"),
            ("\\bword", CultureInvariant | Multiline, "xx\nwordxx", "Q"),
            ("word\\b", CultureInvariant | Multiline, "xx\nxxword", "Q"),
            ("\\bword\\b", CultureInvariant | Multiline, "xx\nsword\nxx", "Q"),
            ("\\b.+\\b", CultureInvariant | Multiline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | Multiline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Multiline, "xabcby", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Multiline, " ab ", "Q"),

            // FoldedFamilyAscii
            ("(ab|cd)ef", CultureInvariant | IgnoreCase, "xxCDefxx", "Q"),
            ("(ab|cd)ef", CultureInvariant | IgnoreCase, "xxABE Fxx", "Q"),
            ("(ab|cd)ef", CultureInvariant | IgnoreCase, "xxabxx", "Q"),
            ("(a+)\\1", CultureInvariant | IgnoreCase, "xxAaax", "Q"),
            ("(a+)\\1", CultureInvariant | IgnoreCase, "xxabx", "Q"),
            ("a+(?=B)", CultureInvariant | IgnoreCase, "xaaBx", "Q"),
            ("a+(?=B)", CultureInvariant | IgnoreCase, "xaabx", "Q"),
            ("\\bWORD\\b", CultureInvariant | IgnoreCase, "xx word xx", "Q"),
            ("\\bWORD\\b", CultureInvariant | IgnoreCase, "xxwordxx", "Q"),
            ("(?<=X)a+", CultureInvariant | IgnoreCase, "xXaax", "Q"),
            ("(?<=X)a+", CultureInvariant | IgnoreCase, "xxaax", "Q"),

            // NamedAscii
            ("(?<x>ab|cd)ef", CultureInvariant, "xxcdefxx", "Q"),
            ("(?<x>ab|cd)ef", CultureInvariant, "xxabxx", "Q"),

            // NamedBackref
            ("(?<w>ab)c\\k<w>", CultureInvariant, "xxabcabxx", "Q"),
            ("(?<w>ab)c\\k<w>", CultureInvariant, "xxabx", "Q"),
            ("(?<a>a+)(?<b>b+)\\k<a>", CultureInvariant, "xxaabax", "Q"),
            ("(?<a>a+)(?<b>b+)\\k<a>", CultureInvariant, "xxaabbx", "Q"),
            ("(?'w'ab)c\\k'w'", CultureInvariant, "xxabcabxx", "Q"),
            ("(?<x>a)?b\\k<x>", CultureInvariant, "xbxx", "Q"),
            ("(?<x>A+)\\k<x>", CultureInvariant | IgnoreCase | RightToLeft, "xxAAax", "Q"),
            ("(?<w>a.+)\\k<w>", CultureInvariant | Multiline, "xx\naabaab\nzz", "Q"),
            ("(?<w>a.+)\\k<w>", CultureInvariant | Multiline, "xx\naabc\nzz", "Q"),
            ("^(?<w>a.+)\\k<w>$", CultureInvariant | Multiline, "xx\naabaab", "Q"),
            ("^(?<w>a.+)\\k<w>$", CultureInvariant | Multiline, "xx\naabc", "Q"),

            // MultilineAscii
            ("^ab", CultureInvariant | Multiline, "xx\nabxx", "Q"),
            ("^ab", CultureInvariant | Multiline, "ab\nxx", "Q"),
            ("ab$", CultureInvariant | Multiline, "xxab\nyy", "Q"),
            ("ab$", CultureInvariant | Multiline, "abxx\nyy", "Q"),
            ("^ab$", CultureInvariant | Multiline, "ab\nab", "Q"),
            ("^ab$", CultureInvariant | Multiline, "xx\nab\nab\nyy", "Q"),

            // AtomicAscii
            ("(?>a+)b", CultureInvariant, "xaab", "Q"),
            ("(?>a+)b", CultureInvariant, "xaac", "Q"),
            ("(?>ab|a)b", CultureInvariant, "xxabb", "Q"),
            ("(?>ab|a)b", CultureInvariant, "xxab", "Q"),
            ("(?>a+)(b+)", CultureInvariant, "xaabb", "Q"),
            ("(?>a|ab)c", CultureInvariant, "xabcx", "Q"),
            ("(?>a|ab)c", CultureInvariant, "xacx", "Q"),

            // AtomicLookaheadSingleline
            ("^(?>a.+)b$", CultureInvariant | Singleline, "aax\nab", "Q"),
            ("^(?>a.+)b$", CultureInvariant | Singleline, "aaxb", "Q"),
            ("a(?=.+b)c", CultureInvariant | Singleline, "xay\nzbc", "Q"),
            ("a(?=.+b)c", CultureInvariant | Singleline, "xayzc", "Q"),

            // ConditionalAscii
            ("(a)?b(?(1)c|d)", CultureInvariant, "xabcd", "Q"),
            ("(a)?b(?(1)c|d)", CultureInvariant, "xbd", "Q"),
            ("(a)?b(?(1)c|d)", CultureInvariant, "xad", "Q"),
            ("(a)(?(1)b|c)", CultureInvariant, "xabx", "Q"),
            ("(a)(?(1)b|c)", CultureInvariant, "xacx", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant, "xb", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant, "xc", "Q"),

            // ConditionalSingleline
            ("(a)?(?(1).+|b)", CultureInvariant | Singleline, "xay\nz", "Q"),
            ("(a)?(?(1).+|b)", CultureInvariant | Singleline, "xy", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Singleline, "ax\ny", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Singleline, "xb", "Q"),

            // BalancingSingleline
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | Singleline, "xab\nab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | Singleline, "xab", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | Singleline, "a\nb", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | Singleline, "a\n1", "Q"),

            // ShorthandSingleline
            ("(\\d.+\\d)", CultureInvariant | Singleline, "x1\n2y", "Q"),
            ("(\\d.+\\d)", CultureInvariant | Singleline, "x1y", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Singleline, "xa\nbz", "Q"),
            ("(\\s.+\\s)", CultureInvariant | Singleline, "ab", "Q"),

            // WordBoundarySingleline
            ("\\b.+\\b", CultureInvariant | Singleline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | Singleline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Singleline, "xab\ncd", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Singleline, " ab ", "Q"),

            // AnchorSingleline
            ("^a.+b$", CultureInvariant | Singleline, "a\nb", "Q"),
            ("^a.+b$", CultureInvariant | Singleline, "xa\nb", "Q"),
            ("^a.+b", CultureInvariant | Singleline, "a\nbxx", "Q"),
            ("\\Aa.+\\z", CultureInvariant | Singleline, "a\nbx", "Q"),

            // FoldSingleline
            ("(A.+B)", CultureInvariant | IgnoreCase | Singleline, "xa\nbz", "Q"),
            ("(A.+B)", CultureInvariant | IgnoreCase | Singleline, "xa\nBZ", "Q"),
            ("(A.+B)", CultureInvariant | IgnoreCase | Singleline, "xaz", "Q"),
            ("(a.+b)", CultureInvariant | IgnoreCase | Singleline, "xA\nBz", "Q"),

            // MultilineSingleline
            ("^a.+b$", CultureInvariant | Multiline | Singleline, "xx\na\nb\nyy", "Q"),
            ("^a.+b$", CultureInvariant | Multiline | Singleline, "xx\nab\nyy", "Q"),
            ("^.+b", CultureInvariant | Multiline | Singleline, "xx\na\nzb", "Q"),
            ("\\Aa.+\\z", CultureInvariant | Multiline | Singleline, "a\nbx", "Q"),

            // ScopedSingleline
            ("(?s:a.+)b", CultureInvariant, "xa\nzb", "Q"),
            ("(?s:a.+)b", CultureInvariant, "xab", "Q"),
            ("(?s:.+)b", CultureInvariant, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant, "xaz", "Q"),

            // NegLookaheadSingleline
            ("a(?!b.+c)d", CultureInvariant | Singleline, "xabd", "Q"),
            ("a(?!b.+c)d", CultureInvariant | Singleline, "xab\ncd", "Q"),
            ("(?!a.+)b", CultureInvariant | Singleline, "xbc", "Q"),
            ("(?!a.+)b", CultureInvariant | Singleline, "a", "Q"),

            // DotLookbehindSingleline
            ("(?<=a.c)b", CultureInvariant | Singleline, "xa\ncb", "Q"),
            ("(?<=a.c)b", CultureInvariant | Singleline, "xacb", "Q"),
            ("(?<=a.c)b", CultureInvariant | Singleline, "xab", "Q"),
            ("(?<!a.c)b", CultureInvariant | Singleline, "xab", "Q"),

            // LazySingleline
            ("(a.+?b)", CultureInvariant | Singleline, "xa\nbqb", "Q"),
            ("(a.+?b)", CultureInvariant | Singleline, "xabqb", "Q"),
            ("(a.+?b)", CultureInvariant | Singleline, "xaq", "Q"),
            ("(a.*?b)", CultureInvariant | Singleline, "xa\nb", "Q"),

            // ContiguousSingleline
            ("\\Ga.+b", CultureInvariant | Singleline, "a\nbxx", "Q"),
            ("\\Ga.+b", CultureInvariant | Singleline, "xa\nb", "Q"),
            ("\\Ga.+", CultureInvariant | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | Singleline, "xa", "Q"),

            // CategoryRangeSingleline
            ("([a-z].+[0-9])", CultureInvariant | Singleline, "xa\nz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | Singleline, "xaz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | Singleline, "xazq", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | Singleline, "xA\nZ9q", "Q"),

            // FoldBackrefSingleline
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Singleline, "xA\nba\nb", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Singleline, "xabxab", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Singleline, "xabxcd", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Singleline, "xA\nbxc", "Q"),

            // BalancingMultiline
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | Multiline, "xab\nab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | Multiline, "xab", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | Multiline, "a\nb", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | Multiline, "a\n1", "Q"),

            // VerboseSingleline
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Singleline, "xa\nbq", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Singleline, "xaq", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Singleline, "xayb", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Singleline, "a\nb", "Q"),

            // ExplicitMultilineDot
            ("^(?<w>a.+b)$", CultureInvariant | ExplicitCapture | Multiline, "aab", "Q"),
            ("^(?<w>a.+b)$", CultureInvariant | ExplicitCapture | Multiline, "xx\naab\nyy", "Q"),
            ("^(?<w>a.+b)$", CultureInvariant | ExplicitCapture | Multiline, "xx\nab", "Q"),

            // AtomicMultiline
            ("(?>a.+)b", CultureInvariant | Multiline, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | Multiline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline, "xx\nabx", "Q"),

            // BackrefRtlSingleline
            ("(a.+)\\1", CultureInvariant | RightToLeft | Singleline, "xabab", "Q"),
            ("(a.+)\\1", CultureInvariant | RightToLeft | Singleline, "xab\nab", "Q"),
            ("(a.+)\\1", CultureInvariant | RightToLeft | Singleline, "xa\nba\nb", "Q"),
            ("(a.+)\\1", CultureInvariant | RightToLeft | Singleline, "xabxcd", "Q"),

            // AtomicRtlSingleline
            ("(?>a.+)b", CultureInvariant | RightToLeft | Singleline, "xa\nbxx", "Q"),
            ("(?>a.+)b", CultureInvariant | RightToLeft | Singleline, "xay", "Q"),
            ("(?>ab+)c", CultureInvariant | RightToLeft | Singleline, "xabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | RightToLeft | Singleline, "xa\nbbc", "Q"),

            // WordBoundaryRtlSingleline
            ("\\b.+\\b", CultureInvariant | RightToLeft | Singleline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | RightToLeft | Singleline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | RightToLeft | Singleline, "xab\ncd", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | RightToLeft | Singleline, " ab ", "Q"),

            // LookaheadRtlSingleline
            ("a(?=.+b)c", CultureInvariant | RightToLeft | Singleline, "xay\nzbc", "Q"),
            ("a(?=.+b)c", CultureInvariant | RightToLeft | Singleline, "xayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | RightToLeft | Singleline, "xbc", "Q"),
            ("(?!a.+)b", CultureInvariant | RightToLeft | Singleline, "a", "Q"),

            // LazyRtlSingleline
            ("(a.+?b)", CultureInvariant | RightToLeft | Singleline, "xa\nbqb", "Q"),
            ("(a.+?b)", CultureInvariant | RightToLeft | Singleline, "xabqb", "Q"),
            ("(a.+?b)", CultureInvariant | RightToLeft | Singleline, "xaq", "Q"),
            ("(a.*?b)", CultureInvariant | RightToLeft | Singleline, "xa\nb", "Q"),

            // LookbehindRtlSingleline
            ("(?<=a.c)b", CultureInvariant | RightToLeft | Singleline, "xa\ncb", "Q"),
            ("(?<=a.c)b", CultureInvariant | RightToLeft | Singleline, "xacb", "Q"),
            ("(?<=a.c)b", CultureInvariant | RightToLeft | Singleline, "xab", "Q"),
            ("(?<!a.c)b", CultureInvariant | RightToLeft | Singleline, "xa\ncb", "Q"),

            // ConditionalRtlSingleline
            ("(a)?(?(1).+|b)", CultureInvariant | RightToLeft | Singleline, "xay\nz", "Q"),
            ("(a)?(?(1).+|b)", CultureInvariant | RightToLeft | Singleline, "xy", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | RightToLeft | Singleline, "ax\ny", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | RightToLeft | Singleline, "xb", "Q"),

            // CategoryRtlSingleline
            ("\\p{L}.+\\p{L}", CultureInvariant | RightToLeft | Singleline, "a\nb", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | RightToLeft | Singleline, "a\n1", "Q"),

            // VerboseRtlSingleline
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | RightToLeft | Singleline, "xa\nbq", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | RightToLeft | Singleline, "xaq", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | RightToLeft | Singleline, "xayb", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | RightToLeft | Singleline, "a\nb", "Q"),

            // ContiguousRtlSingleline
            ("\\Ga.+b", CultureInvariant | RightToLeft | Singleline, "a\nbxx", "Q"),
            ("\\Ga.+b", CultureInvariant | RightToLeft | Singleline, "xa\nb", "Q"),
            ("\\Ga.+", CultureInvariant | RightToLeft | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | RightToLeft | Singleline, "xa", "Q"),

            // ScopedRtlSingleline
            ("(?s:a.+)b", CultureInvariant | RightToLeft | Singleline, "xa\nzb", "Q"),
            ("(?s:a.+)b", CultureInvariant | RightToLeft | Singleline, "xaz", "Q"),
            ("(?s:.+)b", CultureInvariant | RightToLeft | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | RightToLeft | Singleline, "xab", "Q"),

            // ExplicitRtlMultiline
            ("^(?<w>a.+b)$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naab\nyy", "Q"),
            ("^(?<w>a.+b)$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(?<w>a.+b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naab\nyy", "Q"),
            ("(?<w>a.+b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nay", "Q"),

            // FoldRtlMultiline
            ("(A.+B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naab\nyy", "Q"),
            ("(A.+B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nay", "Q"),
            ("(a.+b)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nAAB\nYY", "Q"),
            ("^a.+b$", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naabb\nyy", "Q"),

            // BackrefRtlMultiline
            ("(a.+)\\1", CultureInvariant | Multiline | RightToLeft, "xx\nabab\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline | RightToLeft, "xx\nabcb\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline | RightToLeft, "abab", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline | RightToLeft, "xx\nabcb", "Q"),

            // WordBoundaryRtlMultiline
            ("\\b.+\\b", CultureInvariant | Multiline | RightToLeft, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | Multiline | RightToLeft, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Multiline | RightToLeft, "xabcby", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Multiline | RightToLeft, " ab ", "Q"),

            // ShorthandRtlMultiline
            ("(\\d.+\\d)", CultureInvariant | Multiline | RightToLeft, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | Multiline | RightToLeft, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline | RightToLeft, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline | RightToLeft, "xx\na", "Q"),

            // AlternationRtlMultiline
            ("(a.+|zb)", CultureInvariant | Multiline | RightToLeft, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline | RightToLeft, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline | RightToLeft, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | Multiline | RightToLeft, "xx\nzzb", "Q"),

            // LookbehindRtlMultiline
            ("(?<=a.c)b", CultureInvariant | Multiline | RightToLeft, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline | RightToLeft, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline | RightToLeft, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | Multiline | RightToLeft, "xx\naxb\nzz", "Q"),

            // LookaheadRtlMultiline
            ("a(?=.+b)c", CultureInvariant | Multiline | RightToLeft, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | Multiline | RightToLeft, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\na", "Q"),

            // ContiguousMultiline
            ("\\Ga.+b", CultureInvariant | Multiline, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | Multiline, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline, "xa", "Q"),

            // AlternationMultiline
            ("(a.+|zb)", CultureInvariant | Multiline, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | Multiline, "xx\nzzb", "Q"),

            // ConditionalMultiline
            ("(a)?(?(1)b|c)", CultureInvariant | Multiline, "xx\nab", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant | Multiline, "xx\nax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline, "xx\nxax", "Q"),

            // DotLookbehindMultiline
            ("(?<=a.c)b", CultureInvariant | Multiline, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | Multiline, "xx\naxb\nzz", "Q"),

            // LazyMultiline
            ("(a.+?b)", CultureInvariant | Multiline, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | Multiline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline, "xx\naq", "Q"),

            // LookaheadMultiline
            ("a(?=.+b)c", CultureInvariant | Multiline, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | Multiline, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline, "xx\na", "Q"),

            // FoldMultiline
            ("(A.+B)", CultureInvariant | IgnoreCase | Multiline, "xx\naab\nzz", "Q"),
            ("(A.+B)", CultureInvariant | IgnoreCase | Multiline, "xx\nay", "Q"),
            ("(a.+b)", CultureInvariant | IgnoreCase | Multiline, "xx\nAAB\nYY", "Q"),
            ("^a.+b$", CultureInvariant | IgnoreCase | Multiline, "xx\naabb\nyy", "Q"),

            // VerboseMultiline
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline, "xx\nax\nzz", "Q"),

            // ScopedMultiline
            ("(?s:a.+)b", CultureInvariant | Multiline, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | Multiline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline, "xx\naz", "Q"),

            // CategoryRangeMultiline
            ("([a-z].+[0-9])", CultureInvariant | Multiline, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | Multiline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline, "xx\n12a\nzz", "Q"),

            // AtomicExplicitMultiline
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline, "xx\nabbc\nzz", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline, "xx\nabx", "Q"),

            // NamedMultiline
            ("(?<w>a.+)b", CultureInvariant | Multiline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | Multiline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline, "xx\nabx", "Q"),

            // AlternationExplicitMultiline
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | ExplicitCapture | Multiline, "xx\nzzb", "Q"),

            // ShorthandExplicitMultiline
            ("^\\d+$", CultureInvariant | ExplicitCapture | Multiline, "12\n34", "Q"),
            ("^\\d+$", CultureInvariant | ExplicitCapture | Multiline, "a1\nb2", "Q"),
            ("\\w+$", CultureInvariant | ExplicitCapture | Multiline, "ab12\nxy", "Q"),
            ("\\w+$", CultureInvariant | ExplicitCapture | Multiline, "ab \nxy ", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline, "xx\na", "Q"),

            // LazyExplicitMultiline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline, "xx\naq", "Q"),

            // LookaheadExplicitMultiline
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\na", "Q"),

            // ContiguousExplicitMultiline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline, "xa", "Q"),

            // ConditionalExplicitMultiline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline, "xx\nxax", "Q"),

            // VerboseExplicitMultiline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline, "xx\nax\nzz", "Q"),

            // CategoryExplicitMultiline
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | ExplicitCapture | Multiline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | ExplicitCapture | Multiline, "ab\nCD", "Q"),

            // BalancingExplicitMultiline
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | ExplicitCapture | Multiline, "xab\nab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | ExplicitCapture | Multiline, "xab", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | ExplicitCapture | Multiline, "a\nb", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | ExplicitCapture | Multiline, "a\n1", "Q"),

            // DotLookbehindExplicitMultiline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naxb\nzz", "Q"),

            // WordBoundaryExplicitMultiline
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline, "xx\nword\nxx", "Q"),
            ("\\bword", CultureInvariant | ExplicitCapture | Multiline, "xx\nwordxx", "Q"),
            ("word\\b", CultureInvariant | ExplicitCapture | Multiline, "xx\nxxword", "Q"),
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline, "xx\nsword\nxx", "Q"),
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | Multiline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | Multiline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | Multiline, "xabcby", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | Multiline, " ab ", "Q"),

            // NamedExplicitMultiline
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Multiline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Multiline, "xx\nabx", "Q"),

            // ScopedExplicitMultiline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | Multiline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naz", "Q"),

            // CategoryRangeExplicitMultiline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline, "xx\n12a\nzz", "Q"),

            // FoldExplicitMultiline
            ("(A.+B)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xx\naab\nzz", "Q"),
            ("(A.+B)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xx\nay", "Q"),
            ("(a.+b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xx\nAAB\nYY", "Q"),
            ("^a.+b$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xx\naabb\nyy", "Q"),

            // ConditionalAtomicExplicitMultiline
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline, "xx\nbxx", "Q"),
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline, "xx\naax", "Q"),

            // LookbehindExplicitMultiline
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nabxx", "Q"),
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Multiline, "xx\nabxx", "Q"),

            // SinglelineIgnoreCaseExplicitMultiline
            ("^a.+b$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | Singleline, "xa\nABBB\nzz", "Q"),
            ("^ab", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | Singleline, "xx\nABxx", "Q"),
            ("ab$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | Singleline, "xxAB\nyy", "Q"),
            ("^a.+b$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | Singleline, "qa\nb\nzz", "Q"),

            // GroupedSinglelineExplicitMultiline
            ("^(a.+)b$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xa\nabb\nzz", "Q"),
            ("^(a.+)b$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xa\naxx\nzz", "Q"),
            ("^(a.+)(b+)$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xa\nabbb\nzz", "Q"),
            ("^(a.+)(b+)$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xa\nab\nzz", "Q"),

            // ShorthandRtlExplicitMultiline
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\na", "Q"),

            // ShorthandRtlSingleline
            ("(\\d.+\\d)", CultureInvariant | RightToLeft | Singleline, "x1\n2y", "Q"),
            ("(\\d.+\\d)", CultureInvariant | RightToLeft | Singleline, "x1y", "Q"),
            ("(\\w.+\\w)", CultureInvariant | RightToLeft | Singleline, "xa\nbz", "Q"),
            ("(\\s.+\\s)", CultureInvariant | RightToLeft | Singleline, "ab", "Q"),

            // DotLookbehindRtlSingleline
            ("(?<!a.c)b", CultureInvariant | RightToLeft | Singleline, "xab", "Q"),

            // NegLookaheadRtlSingleline
            ("a(?!b.+c)d", CultureInvariant | RightToLeft | Singleline, "xabd", "Q"),
            ("a(?!b.+c)d", CultureInvariant | RightToLeft | Singleline, "xab\ncd", "Q"),

            // AnchorRtlSingleline
            ("^a.+b$", CultureInvariant | RightToLeft | Singleline, "a\nb", "Q"),
            ("^a.+b$", CultureInvariant | RightToLeft | Singleline, "xa\nb", "Q"),
            ("^a.+b", CultureInvariant | RightToLeft | Singleline, "a\nbxx", "Q"),
            ("\\Aa.+\\z", CultureInvariant | RightToLeft | Singleline, "a\nbx", "Q"),

            // CategoryRangeRtlSingleline
            ("([a-z].+[0-9])", CultureInvariant | RightToLeft | Singleline, "xa\nz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | RightToLeft | Singleline, "xaz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | RightToLeft | Singleline, "xazq", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | RightToLeft | Singleline, "xA\nZ9q", "Q"),

            // NamedRtlSingleline
            ("(?<w>a.+)b", CultureInvariant | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | RightToLeft | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | RightToLeft | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | RightToLeft | Singleline, "xx\nabx", "Q"),

            // SinglelineFoldRtl
            ("(?s)a.+b", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "x\naAB\nbx", "Q"),
            ("A.+B", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "x\naab\nx", "Q"),
            ("(?s)(a+).(b+)", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "x\naA\nbBx", "Q"),
            (".+", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "x\nab\nx", "Q"),
            ("(.+)", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "x\nab\nx", "Q"),

            // ContiguousExplicitSingleline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Singleline, "a\nab", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Singleline, "xa\nab", "Q"),
            ("\\Ga+", CultureInvariant | ExplicitCapture | Singleline, "a\naa", "Q"),
            ("\\Ga+", CultureInvariant | ExplicitCapture | Singleline, "xaa", "Q"),

            // AtomicLookaheadExplicitSingleline
            ("^(?>a.+)b$", CultureInvariant | ExplicitCapture | Singleline, "aax\nab", "Q"),
            ("^(?>a.+)b$", CultureInvariant | ExplicitCapture | Singleline, "aaxb", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Singleline, "xay\nzbc", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Singleline, "xayzc", "Q"),

            // NegLookaheadExplicitSingleline
            ("a(?!b.+c)d", CultureInvariant | ExplicitCapture | Singleline, "xabd", "Q"),
            ("a(?!b.+c)d", CultureInvariant | ExplicitCapture | Singleline, "xab\ncd", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Singleline, "xbc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Singleline, "a", "Q"),

            // DotLookbehindExplicitSingleline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Singleline, "xa\ncb", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Singleline, "xacb", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Singleline, "xab", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | Singleline, "xab", "Q"),

            // LazyExplicitSingleline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Singleline, "xa\nbqb", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Singleline, "xabqb", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Singleline, "xaq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Singleline, "xa\nb", "Q"),

            // ShorthandExplicitSingleline
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Singleline, "x1\n2y", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Singleline, "x1y", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Singleline, "xa\nbz", "Q"),
            ("(\\s.+\\s)", CultureInvariant | ExplicitCapture | Singleline, "ab", "Q"),

            // WordBoundaryExplicitSingleline
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | Singleline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | Singleline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | Singleline, "xab\ncd", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | Singleline, " ab ", "Q"),

            // CategoryRangeExplicitSingleline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Singleline, "xa\nz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Singleline, "xaz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Singleline, "xazq", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Singleline, "xA\nZ9q", "Q"),

            // BalancingExplicitSingleline
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | ExplicitCapture | Singleline, "xab\nab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | ExplicitCapture | Singleline, "xab", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | ExplicitCapture | Singleline, "a\nb", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | ExplicitCapture | Singleline, "a\n1", "Q"),

            // SinglelineFoldExplicit
            ("(?s)a.+b", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "x\naAB\nbx", "Q"),
            ("A.+B", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "x\naab\nx", "Q"),
            ("(?s)(a+).(b+)", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "x\naA\nbBx", "Q"),
            (".+", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "x\nab\nx", "Q"),
            ("(.+)", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "x\nab\nx", "Q"),

            // NamedExplicitSingleline
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Singleline, "xa\nab", "Q"),
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Singleline, "xab", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Singleline, "xab\nc", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Singleline, "xabx", "Q"),

            // VerboseExplicitSingleline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Singleline, "xa\nbq", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Singleline, "xaq", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Singleline, "xayb", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Singleline, "a\nb", "Q"),

            // AnchorExplicitSingleline
            ("^a.+b$", CultureInvariant | ExplicitCapture | Singleline, "a\nb", "Q"),
            ("^a.+b$", CultureInvariant | ExplicitCapture | Singleline, "xa\nb", "Q"),
            ("^a.+b", CultureInvariant | ExplicitCapture | Singleline, "a\nbxx", "Q"),
            ("\\Aa.+\\z", CultureInvariant | ExplicitCapture | Singleline, "a\nbx", "Q"),

            // ScopedExplicitSingleline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Singleline, "xa\nzb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Singleline, "xab", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Singleline, "xaz", "Q"),

            // ConditionalExplicitSingleline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Singleline, "ax\ny", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Singleline, "xb", "Q"),

            // AnchoredLookbehindExplicitSingleline
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Singleline, "abxx", "Q"),
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Singleline, "xabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Singleline, "xabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Singleline, "abxx", "Q"),

            // AtomicExplicitSingleline
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Singleline, "aabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Singleline, "aaxx", "Q"),

            // ShortGuardExplicitSingleline
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Singleline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Singleline, "xb", "Q"),

            // LazyRtlMultiline
            ("(a.+?b)", CultureInvariant | Multiline | RightToLeft, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | Multiline | RightToLeft, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline | RightToLeft, "xx\naq", "Q"),

            // AtomicRtlMultiline
            ("(?>a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline | RightToLeft, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline | RightToLeft, "xx\nabx", "Q"),

            // NamedRtlMultiline
            ("(?<w>a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline | RightToLeft, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline | RightToLeft, "xx\nabx", "Q"),

            // ContiguousRtlMultiline
            ("\\Ga.+b", CultureInvariant | Multiline | RightToLeft, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | Multiline | RightToLeft, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline | RightToLeft, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline | RightToLeft, "xa", "Q"),

            // VerboseRtlMultiline
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nax\nzz", "Q"),

            // ScopedRtlMultiline
            ("(?s:a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | Multiline | RightToLeft, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline | RightToLeft, "xx\naz", "Q"),

            // CategoryRtlMultiline
            ("^\\p{L}+$", CultureInvariant | Multiline | RightToLeft, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | Multiline | RightToLeft, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | Multiline | RightToLeft, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | Multiline | RightToLeft, "ab\nCD", "Q"),

            // CategoryRangeRtlMultiline
            ("([a-z].+[0-9])", CultureInvariant | Multiline | RightToLeft, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | Multiline | RightToLeft, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline | RightToLeft, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline | RightToLeft, "xx\n12a\nzz", "Q"),

            // WordBoundaryExplicitRtlMultiline
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nword\nxx", "Q"),
            ("\\bword", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nwordxx", "Q"),
            ("word\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nxxword", "Q"),
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nsword\nxx", "Q"),
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xabcby", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, " ab ", "Q"),

            // LazyExplicitRtlMultiline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naq", "Q"),

            // AtomicExplicitRtlMultiline
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nabx", "Q"),

            // LookaheadExplicitRtlMultiline
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\na", "Q"),

            // ContiguousExplicitRtlMultiline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xa", "Q"),

            // CategoryExplicitRtlMultiline
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "ab\nCD", "Q"),

            // CategoryRangeExplicitRtlMultiline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\n12a\nzz", "Q"),

            // VerboseExplicitRtlMultiline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nax\nzz", "Q"),

            // ScopedExplicitRtlMultiline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naz", "Q"),

            // AlternationExplicitRtlMultiline
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nzzb", "Q"),

            // DotLookbehindExplicitRtlMultiline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naxb\nzz", "Q"),

            // GuardConditionalExplicitRtlMultiline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\nxax", "Q"),

            // AtomicQuantExplicitRtlMultiline
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xx\naax", "Q"),

            // ShortGuardExplicitRtlMultiline
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft, "xb", "Q"),

            // LookaheadExplicitRtlSingleline
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xay\nzbc", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xbc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "a", "Q"),

            // ShorthandExplicitRtlSingleline
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "x1\n2y", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "x1y", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nbz", "Q"),
            ("(\\s.+\\s)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "ab", "Q"),

            // LazyExplicitRtlSingleline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nbqb", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xabqb", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xaq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nb", "Q"),

            // AtomicExplicitRtlSingleline
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nbxx", "Q"),
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xay", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nbbc", "Q"),

            // ContiguousExplicitRtlSingleline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "a\nab", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nab", "Q"),
            ("\\Ga+", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "a\naa", "Q"),
            ("\\Ga+", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xaa", "Q"),

            // CategoryExplicitRtlSingleline
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "ab\nCD", "Q"),

            // CategoryRangeExplicitRtlSingleline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xaz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xazq", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xA\nZ9q", "Q"),

            // NamedExplicitRtlSingleline
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xx\nabx", "Q"),

            // VerboseExplicitRtlSingleline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | RightToLeft | Singleline, "xa\nbq", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | RightToLeft | Singleline, "xaq", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | RightToLeft | Singleline, "xayb", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | RightToLeft | Singleline, "a\nb", "Q"),

            // ScopedExplicitRtlSingleline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nzb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xaz", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab", "Q"),

            // LookbehindExplicitRtlSingleline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\ncb", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xacb", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\ncb", "Q"),

            // GuardConditionalExplicitRtlSingleline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "ax\ny", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xb", "Q"),

            // WordBoundaryExplicitRtlSingleline
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab\ncd", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, " ab ", "Q"),

            // DotLookbehindExplicitRtlSingleline
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab", "Q"),

            // NegLookaheadExplicitRtlSingleline
            ("a(?!b.+c)d", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xabd", "Q"),
            ("a(?!b.+c)d", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab\ncd", "Q"),

            // AnchorExplicitRtlSingleline
            ("^a.+b$", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "a\nb", "Q"),
            ("^a.+b$", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xa\nb", "Q"),
            ("^a.+b", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "a\nbxx", "Q"),
            ("\\Aa.+\\z", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "a\nbx", "Q"),

            // FoldExplicitRtlSingleline
            ("(?<w>.+)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xxAB\nCDxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xxAXXBxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xxaxxbxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xxAxx", "Q"),

            // FoldShorthandExplicitRtlSingleline
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "x1\n2y", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "x1y", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nbz", "Q"),
            ("(\\s.+\\s)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "ab", "Q"),

            // FoldAlternationExplicitRtlSingleline
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nzb", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xzb", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xz", "Q"),
            ("(zb|z.+)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xzb", "Q"),

            // FoldLazyExplicitRtlSingleline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nbqb", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xabqb", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xaq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nb", "Q"),

            // FoldAtomicExplicitRtlSingleline
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nbxx", "Q"),
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xay", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nbbc", "Q"),

            // FoldLookaheadExplicitRtlSingleline
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xay\nzbc", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xbc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a", "Q"),

            // FoldLookbehindExplicitRtlSingleline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\ncb", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xacb", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xab", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\ncb", "Q"),

            // FoldGuardConditionalExplicitRtlSingleline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "ax\ny", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xb", "Q"),

            // FoldWordBoundaryExplicitRtlSingleline
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xab\ncd", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, " ab ", "Q"),

            // FoldAnchorExplicitRtlSingleline
            ("^a.+b$", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a\nb", "Q"),
            ("^a.+b$", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nb", "Q"),
            ("^a.+b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a\nbxx", "Q"),
            ("\\Aa.+\\z", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a\nbx", "Q"),

            // FoldContiguousExplicitRtlSingleline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a\nbxx", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nb", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa", "Q"),

            // FoldScopedExplicitRtlSingleline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nzb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xaz", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xab", "Q"),

            // FoldNamedExplicitRtlSingleline
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xx\nabx", "Q"),

            // FoldVerboseExplicitRtlSingleline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | RightToLeft | Singleline, "xa\nbq", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | RightToLeft | Singleline, "xaq", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | RightToLeft | Singleline, "xayb", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | RightToLeft | Singleline, "a\nb", "Q"),

            // FoldCategoryExplicitRtlSingleline
            ("\\p{L}.+\\p{L}", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a\nb", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "a\n1", "Q"),

            // FoldCategoryRangeExplicitRtlSingleline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xa\nz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xaz9q", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xazq", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xA\nZ9q", "Q"),

            // FoldNegLookaheadExplicitRtlSingleline
            ("a(?!b.+c)d", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xabd", "Q"),
            ("a(?!b.+c)d", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xab\ncd", "Q"),

            // FoldDotLookbehindExplicitRtlSingleline
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft | Singleline, "xab", "Q"),

            // AlternationSinglelineIgnoreCaseMultiline
            ("(a.+|zb)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nzx\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nAZB\nzz", "Q"),

            // ShorthandSinglelineIgnoreCaseMultiline
            ("^\\d+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "12\n34", "Q"),
            ("^\\d+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "a1\nb2", "Q"),
            ("\\w+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab12\nxy", "Q"),
            ("\\w+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab \nxy ", "Q"),
            ("(\\d.+\\d)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\na", "Q"),

            // LazySinglelineIgnoreCaseMultiline
            ("(a.+?b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naq", "Q"),

            // LookaheadSinglelineIgnoreCaseMultiline
            ("a(?=.+b)c", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\na", "Q"),

            // LookbehindSinglelineIgnoreCaseMultiline
            ("(?<=^a)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabxx", "Q"),
            ("(?<=^a)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabxx", "Q"),

            // ConditionalSinglelineIgnoreCaseMultiline
            ("(a)?(?(1)b|c)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxax", "Q"),

            // AtomicSinglelineIgnoreCaseMultiline
            ("(?>a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabx", "Q"),

            // WordBoundarySinglelineIgnoreCaseMultiline
            ("\\bword\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nword\nxx", "Q"),
            ("\\bword", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nwordxx", "Q"),
            ("word\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxxword", "Q"),
            ("\\bword\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nsword\nxx", "Q"),
            ("\\b.+\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xabcby", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | IgnoreCase | Multiline | Singleline, " ab ", "Q"),

            // BackrefSinglelineIgnoreCaseMultiline
            ("^(ab)\\1$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabab", "Q"),
            ("^(ab)\\1$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabxb", "Q"),
            ("(a+)\\1$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naabb", "Q"),
            ("(a+)\\1$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naaaa", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabab\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabcb", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | Singleline, "abab", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | Singleline, "xabc", "Q"),

            // CategorySinglelineIgnoreCaseMultiline
            ("^\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab\nCD", "Q"),

            // NamedSinglelineIgnoreCaseMultiline
            ("(?<w>a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabx", "Q"),

            // VerboseSinglelineIgnoreCaseMultiline
            ("(a .+ b)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\nax\nzz", "Q"),

            // ScopedSinglelineIgnoreCaseMultiline
            ("(?s:a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naz", "Q"),

            // ContiguousSinglelineIgnoreCaseMultiline
            ("\\Ga.+b", CultureInvariant | IgnoreCase | Multiline | Singleline, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | IgnoreCase | Multiline | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | IgnoreCase | Multiline | Singleline, "xa", "Q"),

            // CategoryRangeSinglelineIgnoreCaseMultiline
            ("([a-z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\n12a\nzz", "Q"),

            // BalancingSinglelineIgnoreCaseMultiline
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xab\nab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xab", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | Multiline | Singleline, "a\nb", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | Multiline | Singleline, "a\n1", "Q"),

            // DotLookbehindSinglelineIgnoreCaseMultiline
            ("(?<=a.c)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naxb\nzz", "Q"),

            // NegLookaheadSinglelineIgnoreCaseMultiline
            ("a(?!b.+c)d", CultureInvariant | IgnoreCase | Multiline | Singleline, "xabd", "Q"),
            ("a(?!b.+c)d", CultureInvariant | IgnoreCase | Multiline | Singleline, "xab\ncd", "Q"),
            ("(?!a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xbc", "Q"),
            ("(?!a.+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "a", "Q"),

            // FoldDotSinglelineIgnoreCaseMultiline
            ("(A.+B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(A.+B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nay", "Q"),
            ("(a.+b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nAAB\nYY", "Q"),
            ("^a.+b$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naabb\nyy", "Q"),

            // ConditionalAtomicSinglelineIgnoreCaseMultiline
            ("^(?(?=a)a|b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nbxx", "Q"),
            ("^(?>a+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naax", "Q"),

            // FoldAlternationRtlMultiline
            ("(a.+|zb)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nzzb", "Q"),

            // FoldShorthandRtlMultiline
            ("(\\d.+\\d)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\na", "Q"),

            // FoldLazyRtlMultiline
            ("(a.+?b)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naq", "Q"),

            // FoldAtomicRtlMultiline
            ("(?>a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nabx", "Q"),

            // FoldLookaheadRtlMultiline
            ("a(?=.+b)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\na", "Q"),

            // FoldLookbehindRtlMultiline
            ("(?<=a.c)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naxb\nzz", "Q"),

            // FoldNamedRtlMultiline
            ("(?<w>a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nabx", "Q"),

            // FoldContiguousRtlMultiline
            ("\\Ga.+b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xa", "Q"),

            // FoldWordBoundaryRtlMultiline
            ("\\b.+\\b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xabcby", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | IgnoreCase | Multiline | RightToLeft, " ab ", "Q"),

            // FoldBackrefRtlMultiline
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nabab\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nabcb\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "abab", "Q"),
            ("(a.+)\\1", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nabcb", "Q"),

            // FoldCategoryRtlMultiline
            ("^\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "ab\nCD", "Q"),

            // FoldCategoryRangeRtlMultiline
            ("([a-z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\n12a\nzz", "Q"),

            // FoldVerboseRtlMultiline
            ("(a .+ b)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nABqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nAAB\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nax\nzz", "Q"),

            // FoldScopedRtlMultiline
            ("(?s:a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nAAB\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "x\nYB", "Q"),
            ("(?s:a.+)b", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "xx\naz", "Q"),

            // FoldExplicitRtlMultiline
            ("^(?<w>a.+b)$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAAB\nyy", "Q"),
            ("^(?<w>a.+b)$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(?<w>a.+b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAAB\nyy", "Q"),
            ("(?<w>a.+b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nay", "Q"),

            // FoldShorthandExplicitRtlMultiline
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\n1A2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAB\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\na", "Q"),

            // FoldAlternationExplicitRtlMultiline
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAZB\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nZZB", "Q"),

            // FoldLazyExplicitRtlMultiline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nABqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAB", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\naq", "Q"),

            // FoldAtomicExplicitRtlMultiline
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAABxx", "Q"),
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nABBC", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nabx", "Q"),

            // FoldLookaheadExplicitRtlMultiline
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAYZBC\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nXBC", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\na", "Q"),

            // FoldContiguousExplicitRtlMultiline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "AAB\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "A\nB", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xa", "Q"),

            // FoldCategoryExplicitRtlMultiline
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "ab\nCD", "Q"),

            // FoldCategoryRangeExplicitRtlMultiline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\na1B2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\n1Ab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\n12a\nzz", "Q"),

            // FoldVerboseExplicitRtlMultiline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nABqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nAAB\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft, "xx\nax\nzz", "Q"),

            // FoldScopedExplicitRtlMultiline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAAB\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "x\nYB", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\naz", "Q"),

            // FoldDotLookbehindExplicitRtlMultiline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nACB\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nACB", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAXB\nzz", "Q"),

            // FoldGuardConditionalExplicitRtlMultiline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAAX", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nxax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nB", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nx", "Q"),

            // FoldPossessiveExplicitRtlMultiline
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAABxx", "Q"),
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\naax", "Q"),
            ("^(?>a+)b$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAAB", "Q"),
            ("^(?>a+)b$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAAX", "Q"),

            // FoldShortGuardExplicitRtlMultiline
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "ABxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xb", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nAB", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nx", "Q"),

            // FoldWordBoundaryExplicitRtlMultiline
            ("\\bword\\b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nWORD\nxx", "Q"),
            ("\\bword", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nWORDxx", "Q"),
            ("word\\b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nxxWORD", "Q"),
            ("\\bword\\b", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline | RightToLeft, "xx\nsword\nxx", "Q"),

            // FoldAlternationSinglelineIgnoreCaseMultiline
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nazb\nzz", "Q"),
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nzzb\nzz", "Q"),
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nzx\nzz", "Q"),
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nAZB\nzz", "Q"),

            // FoldShorthandSinglelineIgnoreCaseMultiline
            ("(\\d.+\\d)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\n1A2\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nAB\nzz", "Q"),

            // FoldLazySinglelineIgnoreCaseMultiline
            ("(A.+?B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(A.+?B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nAB", "Q"),

            // FoldAtomicSinglelineIgnoreCaseMultiline
            ("(?>A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naabxx", "Q"),
            ("(?>A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nABBC", "Q"),

            // FoldLookaheadSinglelineIgnoreCaseMultiline
            ("A(?=.+B)C", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nayzbc\nzz", "Q"),
            ("A(?=.+B)C", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nayzc", "Q"),
            ("(?!A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxbc", "Q"),
            ("(?!A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\na", "Q"),

            // FoldLookbehindSinglelineIgnoreCaseMultiline
            ("(?<=^A)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabxx", "Q"),
            ("(?<=^A)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^A)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^A)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabxx", "Q"),

            // FoldConditionalSinglelineIgnoreCaseMultiline
            ("(A)?(?(1)B|C)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab", "Q"),
            ("(A)?(?(1)B|C)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nax", "Q"),
            ("^(?(?=A.+)A|B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naax", "Q"),
            ("^(?(?=A.+)A|B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxax", "Q"),

            // FoldNamedSinglelineIgnoreCaseMultiline
            ("(?<w>A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naxb", "Q"),
            ("(?<w>AB)C", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>AB)C", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabx", "Q"),

            // FoldContiguousSinglelineIgnoreCaseMultiline
            ("\\GA.+B", CultureInvariant | IgnoreCase | Multiline | Singleline, "aab\nzz", "Q"),
            ("\\GA.+B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab", "Q"),
            ("\\GA.+", CultureInvariant | IgnoreCase | Multiline | Singleline, "A\nB", "Q"),
            ("\\GA.+", CultureInvariant | IgnoreCase | Multiline | Singleline, "xa", "Q"),

            // FoldWordBoundarySinglelineIgnoreCaseMultiline
            ("\\bWORD\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nword\nxx", "Q"),
            ("\\bWORD", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nwordxx", "Q"),
            ("WORD\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nxxword", "Q"),
            ("\\bWORD\\b", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nsword\nxx", "Q"),

            // FoldBackrefSinglelineIgnoreCaseMultiline
            ("^(AB)\\1$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabab", "Q"),
            ("^(AB)\\1$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabxb", "Q"),
            ("(A.+)\\1", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabab\nzz", "Q"),
            ("(A.+)\\1", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nabcb", "Q"),

            // FoldCategorySinglelineIgnoreCaseMultiline
            ("^AB\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab\nCD", "Q"),
            ("A\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab\nCD", "Q"),
            ("\\p{L}+CD$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab\nzzCD", "Q"),
            ("\\p{L}+CD$", CultureInvariant | IgnoreCase | Multiline | Singleline, "ab\ncd", "Q"),

            // FoldCategoryRangeSinglelineIgnoreCaseMultiline
            ("([A-Z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\na1b2\nzz", "Q"),
            ("([A-Z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^A-Z])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^A-Z])", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\n12a\nzz", "Q"),

            // FoldVerboseSinglelineIgnoreCaseMultiline
            ("(A .+ B)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(A .+ B)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\naq", "Q"),
            ("^A .+ B$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("^A .+ B$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | Singleline, "xx\nax\nzz", "Q"),

            // FoldScopedSinglelineIgnoreCaseMultiline
            ("(?s:A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?s:A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nab", "Q"),
            ("(?s:.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "x\nyb", "Q"),
            ("(?s:A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naz", "Q"),

            // FoldDotLookbehindSinglelineIgnoreCaseMultiline
            ("(?<=A.C)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nacb\nzz", "Q"),
            ("(?<=A.C)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naxb\nzz", "Q"),
            ("(?<=A.C)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nACB", "Q"),
            ("(?<!A.C)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nAXB\nzz", "Q"),

            // FoldNegLookaheadSinglelineIgnoreCaseMultiline
            ("A(?!B.+C)D", CultureInvariant | IgnoreCase | Multiline | Singleline, "xabd", "Q"),
            ("A(?!B.+C)D", CultureInvariant | IgnoreCase | Multiline | Singleline, "xab\ncd", "Q"),
            ("(?!A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xbc", "Q"),
            ("(?!A.+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "a", "Q"),

            // FoldConditionalAtomicSinglelineIgnoreCaseMultiline
            ("^(?(?=A)A|B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "ABxx", "Q"),
            ("^(?(?=A)A|B)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nbxx", "Q"),
            ("^(?>A+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naabxx", "Q"),
            ("^(?>A+)B", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naax", "Q"),

            // FoldBalancingSinglelineIgnoreCaseMultiline
            ("(?<b>A.+)(?<-b>B.+)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab\nabb", "Q"),
            ("(?<b>A.+)(?<-b>B.+)", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\naab", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | Multiline | Singleline, "A\nB", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | Multiline | Singleline, "A\n1", "Q"),

            // AlternationSinglelineMultiline
            ("(a.+|zb)", CultureInvariant | Multiline | Singleline, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline | Singleline, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline | Singleline, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | Multiline | Singleline, "xx\nzzb", "Q"),

            // ShorthandSinglelineMultiline
            ("(\\d.+\\d)", CultureInvariant | Multiline | Singleline, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | Multiline | Singleline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline | Singleline, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline | Singleline, "xx\na", "Q"),

            // LazySinglelineMultiline
            ("(a.+?b)", CultureInvariant | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | Multiline | Singleline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline | Singleline, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline | Singleline, "xx\naq", "Q"),

            // AtomicSinglelineMultiline
            ("(?>a.+)b", CultureInvariant | Multiline | Singleline, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | Multiline | Singleline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline | Singleline, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline | Singleline, "xx\nabx", "Q"),

            // LookaheadSinglelineMultiline
            ("a(?=.+b)c", CultureInvariant | Multiline | Singleline, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | Multiline | Singleline, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline | Singleline, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline | Singleline, "xx\na", "Q"),

            // LookbehindSinglelineMultiline
            ("(?<=^a)b", CultureInvariant | Multiline | Singleline, "xx\nabxx", "Q"),
            ("(?<=^a)b", CultureInvariant | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | Multiline | Singleline, "xx\nabxx", "Q"),

            // ConditionalSinglelineMultiline
            ("(a)?(?(1)b|c)", CultureInvariant | Multiline | Singleline, "xx\nab", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant | Multiline | Singleline, "xx\nax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | Singleline, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | Singleline, "xx\nxax", "Q"),

            // NamedSinglelineMultiline
            ("(?<w>a.+)b", CultureInvariant | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | Multiline | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline | Singleline, "xx\nabx", "Q"),

            // ContiguousSinglelineMultiline
            ("\\Ga.+b", CultureInvariant | Multiline | Singleline, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | Multiline | Singleline, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline | Singleline, "xa", "Q"),

            // WordBoundarySinglelineMultiline
            ("\\bword\\b", CultureInvariant | Multiline | Singleline, "xx\nword\nxx", "Q"),
            ("\\bword", CultureInvariant | Multiline | Singleline, "xx\nwordxx", "Q"),
            ("word\\b", CultureInvariant | Multiline | Singleline, "xx\nxxword", "Q"),
            ("\\bword\\b", CultureInvariant | Multiline | Singleline, "xx\nsword\nxx", "Q"),

            // BackrefSinglelineMultiline
            ("^(ab)\\1$", CultureInvariant | Multiline | Singleline, "xx\nabab", "Q"),
            ("^(ab)\\1$", CultureInvariant | Multiline | Singleline, "xx\nabxb", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline | Singleline, "xx\nabab\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline | Singleline, "xx\nabcb", "Q"),

            // CategorySinglelineMultiline
            ("^\\p{L}+$", CultureInvariant | Multiline | Singleline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | Multiline | Singleline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | Multiline | Singleline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | Multiline | Singleline, "ab\nCD", "Q"),

            // CategoryRangeSinglelineMultiline
            ("([a-z].+[0-9])", CultureInvariant | Multiline | Singleline, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | Multiline | Singleline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline | Singleline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline | Singleline, "xx\n12a\nzz", "Q"),

            // VerboseSinglelineMultiline
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline | Singleline, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline | Singleline, "xx\nax\nzz", "Q"),

            // ScopedSinglelineMultiline
            ("(?s:a.+)b", CultureInvariant | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline | Singleline, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | Multiline | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline | Singleline, "xx\naz", "Q"),

            // DotLookbehindSinglelineMultiline
            ("(?<=a.c)b", CultureInvariant | Multiline | Singleline, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline | Singleline, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline | Singleline, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | Multiline | Singleline, "xx\naxb\nzz", "Q"),

            // GuardConditionalSinglelineMultiline
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | Singleline, "xx\nb", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | Singleline, "xx\nx", "Q"),

            // PossessiveSinglelineMultiline
            ("^(?>a+)b", CultureInvariant | Multiline | Singleline, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | Multiline | Singleline, "xx\naax", "Q"),
            ("^(?>a+)b$", CultureInvariant | Multiline | Singleline, "xx\naab", "Q"),
            ("^(?>a+)b$", CultureInvariant | Multiline | Singleline, "xx\naax", "Q"),

            // ShortGuardSinglelineMultiline
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | Singleline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | Singleline, "xb", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | Singleline, "xx\nab", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | Singleline, "xx\nx", "Q"),

            // AlternationExplicitSinglelineMultiline
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nzzb", "Q"),

            // ShorthandExplicitSinglelineMultiline
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\na", "Q"),

            // LazyExplicitSinglelineMultiline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naq", "Q"),

            // AtomicExplicitSinglelineMultiline
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nabx", "Q"),

            // LookaheadExplicitSinglelineMultiline
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\na", "Q"),

            // LookbehindExplicitSinglelineMultiline
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nabxx", "Q"),
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nabxx", "Q"),

            // ConditionalExplicitSinglelineMultiline
            ("(?<g>a)?(?(g)b|c)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nab", "Q"),
            ("(?<g>a)?(?(g)b|c)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nxax", "Q"),

            // NamedExplicitSinglelineMultiline
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nabx", "Q"),

            // ContiguousExplicitSinglelineMultiline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xa", "Q"),

            // WordBoundaryExplicitSinglelineMultiline
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nword\nxx", "Q"),
            ("\\bword", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nwordxx", "Q"),
            ("word\\b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nxxword", "Q"),
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nsword\nxx", "Q"),

            // CategoryExplicitSinglelineMultiline
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "ab\nCD", "Q"),

            // CategoryRangeExplicitSinglelineMultiline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\n12a\nzz", "Q"),

            // VerboseExplicitSinglelineMultiline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | Singleline, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | Singleline, "xx\nax\nzz", "Q"),

            // ScopedExplicitSinglelineMultiline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naz", "Q"),

            // DotLookbehindExplicitSinglelineMultiline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naxb\nzz", "Q"),

            // GuardConditionalExplicitSinglelineMultiline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nb", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nx", "Q"),

            // PossessiveExplicitSinglelineMultiline
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naax", "Q"),
            ("^(?>a+)b$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naab", "Q"),
            ("^(?>a+)b$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naax", "Q"),

            // ShortGuardExplicitSinglelineMultiline
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xb", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nab", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\nx", "Q"),

            // AlternationRtlSinglelineMultiline
            ("(a.+|zb)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nzzb", "Q"),

            // ShorthandRtlSinglelineMultiline
            ("(\\d.+\\d)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\na", "Q"),

            // LazyRtlSinglelineMultiline
            ("(a.+?b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),

            // AtomicRtlSinglelineMultiline
            ("(?>a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabx", "Q"),

            // LookaheadRtlSinglelineMultiline
            ("a(?=.+b)c", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\na", "Q"),

            // LookbehindRtlSinglelineMultiline
            ("(?<=^a)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabxx", "Q"),
            ("(?<=^a)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabxx", "Q"),

            // ConditionalRtlSinglelineMultiline
            ("(a)?(?(1)b|c)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nxax", "Q"),

            // NamedRtlSinglelineMultiline
            ("(?<w>a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabx", "Q"),

            // ContiguousRtlSinglelineMultiline
            ("\\Ga.+b", CultureInvariant | Multiline | RightToLeft | Singleline, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline | RightToLeft | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | Multiline | RightToLeft | Singleline, "xa", "Q"),

            // WordBoundaryRtlSinglelineMultiline
            ("\\b.+\\b", CultureInvariant | Multiline | RightToLeft | Singleline, "xab\ncd", "Q"),
            ("\\b.+\\b", CultureInvariant | Multiline | RightToLeft | Singleline, "   ", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Multiline | RightToLeft | Singleline, "xabcby", "Q"),
            ("\\Ba.+b\\B", CultureInvariant | Multiline | RightToLeft | Singleline, " ab ", "Q"),

            // BackrefRtlSinglelineMultiline
            ("^(ab)\\1$", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabab", "Q"),
            ("^(ab)\\1$", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabxb", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabab\nzz", "Q"),
            ("(a.+)\\1", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabcb", "Q"),

            // CategoryRtlSinglelineMultiline
            ("^\\p{L}+$", CultureInvariant | Multiline | RightToLeft | Singleline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | Multiline | RightToLeft | Singleline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | Multiline | RightToLeft | Singleline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | Multiline | RightToLeft | Singleline, "ab\nCD", "Q"),

            // CategoryRangeRtlSinglelineMultiline
            ("([a-z].+[0-9])", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\n12a\nzz", "Q"),

            // VerboseRtlSinglelineMultiline
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\nax\nzz", "Q"),

            // ScopedRtlSinglelineMultiline
            ("(?s:a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naz", "Q"),

            // DotLookbehindRtlSinglelineMultiline
            ("(?<=a.c)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naxb\nzz", "Q"),

            // GuardConditionalRtlSinglelineMultiline
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nb", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nx", "Q"),

            // AlternationExplicitRtlSinglelineMultiline
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nazb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nzzb\nzz", "Q"),
            ("(a.+|zb)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nzx\nzz", "Q"),
            ("(zb|z.+)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nzzb", "Q"),

            // ShorthandExplicitRtlSinglelineMultiline
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\na", "Q"),

            // LazyExplicitRtlSinglelineMultiline
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a.+?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(a.*?b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),

            // AtomicExplicitRtlSinglelineMultiline
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naabxx", "Q"),
            ("(?>a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabbc", "Q"),
            ("(?>ab+)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabx", "Q"),

            // LookaheadExplicitRtlSinglelineMultiline
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nayzbc\nzz", "Q"),
            ("a(?=.+b)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nayzc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nxbc", "Q"),
            ("(?!a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\na", "Q"),

            // LookbehindExplicitRtlSinglelineMultiline
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabxx", "Q"),
            ("(?<=^a)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabxx", "Q"),

            // ConditionalExplicitRtlSinglelineMultiline
            ("(?<g>a)?(?(g)b|c)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(?<g>a)?(?(g)b|c)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nxax", "Q"),

            // NamedExplicitRtlSinglelineMultiline
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naxb", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>ab)c", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabx", "Q"),

            // ContiguousExplicitRtlSinglelineMultiline
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "aab\nzz", "Q"),
            ("\\Ga.+b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naab", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "a\nb", "Q"),
            ("\\Ga.+", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xa", "Q"),

            // WordBoundaryExplicitRtlSinglelineMultiline
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nword\nxx", "Q"),
            ("\\bword", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nwordxx", "Q"),
            ("word\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nxxword", "Q"),
            ("\\bword\\b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nsword\nxx", "Q"),

            // BackrefExplicitRtlSinglelineMultiline
            ("^(?<w>ab)\\1$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabab", "Q"),
            ("^(?<w>ab)\\1$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabxb", "Q"),
            ("(?<w>a.+)\\1", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabab\nzz", "Q"),
            ("(?<w>a.+)\\1", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nabcb", "Q"),

            // CategoryExplicitRtlSinglelineMultiline
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "ab\nCD", "Q"),

            // CategoryRangeExplicitRtlSinglelineMultiline
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\na1b2\nzz", "Q"),
            ("([a-z].+[0-9])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^a-z])", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\n12a\nzz", "Q"),

            // VerboseExplicitRtlSinglelineMultiline
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\nabqb\nzz", "Q"),
            ("(a .+ b)", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("^a .+ b$", CultureInvariant | ExplicitCapture | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\nax\nzz", "Q"),

            // ScopedExplicitRtlSinglelineMultiline
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(?s:.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "x\nyb", "Q"),
            ("(?s:a.+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naz", "Q"),

            // DotLookbehindExplicitRtlSinglelineMultiline
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nacb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naxb\nzz", "Q"),
            ("(?<=a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nacb", "Q"),
            ("(?<!a.c)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naxb\nzz", "Q"),

            // GuardConditionalExplicitRtlSinglelineMultiline
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nb", "Q"),
            ("^(?(?=a.+)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nx", "Q"),

            // PossessiveRtlSinglelineMultiline
            ("^(?>a+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("^(?>a+)b$", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naab", "Q"),
            ("^(?>a+)b$", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),

            // ShortGuardRtlSinglelineMultiline
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xb", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nx", "Q"),

            // PossessiveExplicitRtlSinglelineMultiline
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("^(?>a+)b$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naab", "Q"),
            ("^(?>a+)b$", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),

            // ShortGuardExplicitRtlSinglelineMultiline
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xb", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | ExplicitCapture | Multiline | RightToLeft | Singleline, "xx\nx", "Q"),

            // FoldAlternationRtlSinglelineMultiline
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nazb\nzz", "Q"),
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nzzb\nzz", "Q"),
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nzx\nzz", "Q"),
            ("(A.+|ZB)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nAZB\nzz", "Q"),

            // FoldShorthandRtlSinglelineMultiline
            ("(\\d.+\\d)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\n1A2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nAB\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\na", "Q"),

            // FoldLazyRtlSinglelineMultiline
            ("(A.+?B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabqb\nzz", "Q"),
            ("(A.+?B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),
            ("(a.*?b)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nAB", "Q"),
            ("(a.*?b)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),

            // FoldAtomicRtlSinglelineMultiline
            ("(?>A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naabxx", "Q"),
            ("(?>A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nABBC", "Q"),
            ("(?>ab+)c", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabx", "Q"),

            // FoldLookaheadRtlSinglelineMultiline
            ("A(?=.+B)C", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nayzbc\nzz", "Q"),
            ("A(?=.+B)C", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nayzc", "Q"),
            ("(?!A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nxbc", "Q"),
            ("(?!A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\na", "Q"),

            // FoldLookbehindRtlSinglelineMultiline
            ("(?<=^A)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabxx", "Q"),
            ("(?<=^A)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^A)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nxabxx", "Q"),
            ("(?<!^A)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabxx", "Q"),

            // FoldConditionalRtlSinglelineMultiline
            ("(A)?(?(1)B|C)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(A)?(?(1)B|C)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nax", "Q"),
            ("^(?(?=A.+)A|B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naax", "Q"),
            ("^(?(?=A.+)A|B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nxax", "Q"),

            // FoldNamedRtlSinglelineMultiline
            ("(?<w>A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?<w>A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naxb", "Q"),
            ("(?<w>AB)C", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nababc\nzz", "Q"),
            ("(?<w>AB)C", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabx", "Q"),

            // FoldContiguousRtlSinglelineMultiline
            ("\\GA.+B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "aab\nzz", "Q"),
            ("\\GA.+B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naab", "Q"),
            ("\\GA.+", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "A\nB", "Q"),
            ("\\GA.+", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xa", "Q"),

            // FoldWordBoundaryRtlSinglelineMultiline
            ("\\bWORD\\b", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nword\nxx", "Q"),
            ("\\bWORD", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nwordxx", "Q"),
            ("WORD\\b", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nxxword", "Q"),
            ("\\bWORD\\b", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nsword\nxx", "Q"),

            // FoldBackrefRtlSinglelineMultiline
            ("^(AB)\\1$", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabab", "Q"),
            ("^(AB)\\1$", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabxb", "Q"),
            ("(A.+)\\1", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabab\nzz", "Q"),
            ("(A.+)\\1", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nabcb", "Q"),

            // FoldCategoryRtlSinglelineMultiline
            ("^AB\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "ab\nCD", "Q"),
            ("A\\p{L}+$", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "ab\nCD", "Q"),
            ("\\p{L}+CD$", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "ab\nzzCD", "Q"),
            ("\\p{L}+CD$", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "ab\ncd", "Q"),

            // FoldCategoryRangeRtlSinglelineMultiline
            ("([A-Z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\na1b2\nzz", "Q"),
            ("([A-Z].+[0-9])", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nab\nzz", "Q"),
            ("([^0-9].+[^A-Z])", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\n1aB\nzz", "Q"),
            ("([^0-9].+[^A-Z])", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\n12a\nzz", "Q"),

            // FoldVerboseRtlSinglelineMultiline
            ("(A .+ B)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\nabqb\nzz", "Q"),
            ("(A .+ B)", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\naq", "Q"),
            ("^A .+ B$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("^A .+ B$", CultureInvariant | IgnoreCase | IgnorePatternWhitespace | Multiline | RightToLeft | Singleline, "xx\nax\nzz", "Q"),

            // FoldScopedRtlSinglelineMultiline
            ("(?s:A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naab\nzz", "Q"),
            ("(?s:A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nab", "Q"),
            ("(?s:.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "x\nyb", "Q"),
            ("(?s:A.+)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naz", "Q"),

            // FoldDotLookbehindRtlSinglelineMultiline
            ("(?<=A.C)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nacb\nzz", "Q"),
            ("(?<=A.C)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\naxb\nzz", "Q"),
            ("(?<=A.C)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nACB", "Q"),
            ("(?<!A.C)B", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nAXB\nzz", "Q"),

            // FoldGuardConditionalRtlSinglelineMultiline
            ("^(?(?=A.+)A|B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nb", "Q"),
            ("^(?(?=A.+)A|B)", CultureInvariant | IgnoreCase | Multiline | RightToLeft | Singleline, "xx\nx", "Q"),

            // FixedQuantifiedLookbehind
            ("(?<=ab{2})c", CultureInvariant, "xxabbc", "Q"),
            ("(?<=ab{2})c", CultureInvariant, "xxabc", "Q"),

            // ConditionalAtomicMultiline
            ("^(?(?=a)a|b)", CultureInvariant | Multiline, "abxx", "Q"),
            ("^(?(?=a)a|b)", CultureInvariant | Multiline, "xx\nbxx", "Q"),
            ("^(?>a+)b", CultureInvariant | Multiline, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | Multiline, "xx\naax", "Q"),

            // SubtractionAscii
            ("[a-z-[aeiou]]+", CultureInvariant, "xbcdfx", "Q"),
            ("[a-z-[aeiou]]+", CultureInvariant, "xabeix", "Q"),
            ("[a-z-[aeiou]]+", CultureInvariant, "xaeix", "Q"),
            ("[A-Z-[AEIOU]]+", CultureInvariant, "xBCDFx", "Q"),
            ("[0-9-[13579]]+", CultureInvariant, "x246x", "Q"),
            ("[0-9-[13579]]+", CultureInvariant, "x135x", "Q"),
            ("[\\w-[\\d_]]+", CultureInvariant, "xabcx", "Q"),

            // BalancingAscii
            ("^(?<o><)(?<c>>)(?<-o>)(?<-c>)$", CultureInvariant, "<>", "Q"),
            ("^(?<o><)(?<c>>)(?<-o>)(?<-c>)$", CultureInvariant, "<", "Q"),
            ("(?<a>a+)(?<-a>b+)", CultureInvariant, "xaabbx", "Q"),
            ("(?<a>a+)(?<-a>b+)", CultureInvariant, "xaabx", "Q"),
            ("(?<d>[0-9])(?<-d>.)", CultureInvariant, "x1ax", "Q"),

            // SensitiveFoldAscii
            ("(ab|cd)ef", IgnoreCase, "xxCDefxx", "Q"),
            ("(ab|cd)ef", IgnoreCase, "xxABE Fxx", "Q"),
            ("(a+)\\1", IgnoreCase, "xxAaax", "Q"),
            ("a+(?=B)", IgnoreCase, "xaaBx", "Q"),
            ("\\bWORD\\b", IgnoreCase, "xx word xx", "Q"),
            ("[a-z]+", IgnoreCase, "xxabcxx", "Q"),
            ("\\w+", IgnoreCase, "xxabcxx", "Q"),

            // RtlFamily
            ("(ab|cd)ef", CultureInvariant | RightToLeft, "xxcdefxx", "Q"),
            ("(ab|cd)ef", CultureInvariant | RightToLeft, "xxabxx", "Q"),
            ("(a+)\\1", CultureInvariant | RightToLeft, "xxaaax", "Q"),
            ("(a+)\\1", CultureInvariant | RightToLeft, "xxabx", "Q"),
            ("(?<=x)a+", CultureInvariant | RightToLeft, "xxaax", "Q"),
            ("(?<=x)a+", CultureInvariant | RightToLeft, "xbbb", "Q"),
            ("a+(?=b)", CultureInvariant | RightToLeft, "xaabx", "Q"),

            // SinglelineAnchor
            ("^ab", CultureInvariant, "ab\nxx", "Q"),
            ("^ab", CultureInvariant, "xx\nabxx", "Q"),
            ("ab$", CultureInvariant, "xxab\nyy", "Q"),
            ("ab$", CultureInvariant, "abxx\nyy", "Q"),
            ("\\Aab", CultureInvariant, "ab\nxx", "Q"),
            ("\\Aab", CultureInvariant, "xx\nab", "Q"),
            ("ab\\z", CultureInvariant, "xx\nab", "Q"),
            ("ab\\z", CultureInvariant, "xxab\nyy", "Q"),

            // NestedLoopAscii
            ("(a+)+b", CultureInvariant, "xaab", "Q"),
            ("(a+)+b", CultureInvariant, "xaac", "Q"),
            ("(a|ab)+c", CultureInvariant, "xababc", "Q"),
            ("(a|ab)+c", CultureInvariant, "xabab", "Q"),
            ("(a+)*b", CultureInvariant, "xaab", "Q"),
            ("(a+)*b", CultureInvariant, "xbb", "Q"),

            // LazyGroupAscii
            ("(a+?)b", CultureInvariant, "xaab", "<$1$2>"),
            ("(a+?)b", CultureInvariant, "xbb", "<$1$2>"),
            ("(a??)b", CultureInvariant, "xab", "<$1$2>"),
            ("(a??)b", CultureInvariant, "xb", "<$1$2>"),
            ("(a+?)(b+?)", CultureInvariant, "xaabb", "<$1$2>"),
            ("(a+?)(b+?)", CultureInvariant, "xab", "<$1$2>"),
            ("(a{2,4}?)b", CultureInvariant, "xaaab", "<$1$2>"),
            ("(a{2,4}?)b", CultureInvariant, "xab", "<$1$2>"),

            // EmptyMatch
            ("()", CultureInvariant, "xx", "Q"),
            ("x?", CultureInvariant, "axb", "Q"),
            ("(a)?", CultureInvariant, "xab", "Q"),

            // EmptyPatternSplit
            ("", CultureInvariant, "", "Q"),

            // ScopedInline
            ("(?i:ab)c", CultureInvariant, "xxABcxx", "Q"),
            ("(?i:ab)c", CultureInvariant, "xxabcxx", "Q"),
            ("(?i:ab)c", CultureInvariant, "xxabCxx", "Q"),
            ("(?m:^ab)", CultureInvariant, "xx\nabxx", "Q"),
            ("(?-i:ab)", CultureInvariant, "xxABxx", "Q"),
            ("(?i:(ab|cd))ef", CultureInvariant, "xxCDefxx", "Q"),
            ("(?i:(a+))\\1", CultureInvariant, "xxAaax", "Q"),

            // BareNone
            ("(ab|cd)ef", None, "xxcdefxx", "Q"),
            ("(a+)\\1", None, "xxaaax", "Q"),
            ("a+(?=b)", None, "xaabx", "Q"),
            ("(?<=x)a+", None, "xxaax", "Q"),
            ("\\bword\\b", None, "xx word xx", "Q"),
            ("[a-z]+", None, "xxabcxx", "Q"),
            ("(?>a+)b", None, "xaab", "Q"),

            // BareNoneAnchorSplit
            ("^ab$", None, "ab", "Q"),

            // StackedCombo
            ("(ab|cd)ef", CultureInvariant | IgnoreCase | RightToLeft, "xxCDefxx", "Q"),
            ("(ab|cd)ef", CultureInvariant | IgnoreCase | RightToLeft, "xxabxx", "Q"),
            ("(a+)\\1", CultureInvariant | IgnoreCase | RightToLeft, "xxAaax", "Q"),
            ("^ab", CultureInvariant | IgnoreCase | Multiline, "xx\nABxx", "Q"),
            ("ab$", CultureInvariant | IgnoreCase | Multiline, "xxAB\nyy", "Q"),
            ("\\bWORD\\b", CultureInvariant | IgnoreCase | RightToLeft, "xx word xx", "Q"),

            // VerboseComment
            ("a b  # tail", CultureInvariant | IgnorePatternWhitespace, "xabx", "Q"),
            ("a(?#mid)b", CultureInvariant | IgnorePatternWhitespace, "xabx", "Q"),
            ("(?#lead)a b", CultureInvariant | IgnorePatternWhitespace, "xabx", "Q"),
            ("a # c1\nb # c2", CultureInvariant | IgnorePatternWhitespace, "xabx", "Q"),

            // AnyClass
            ("[\\s\\S]+", CultureInvariant, "xxabxx", "Q"),
            ("[\\s\\S]+", CultureInvariant, "xx\nab", "Q"),
            ("[^x]+", CultureInvariant, "xxabxx", "Q"),
            ("[^x]+", CultureInvariant, "xx\nab", "Q"),
            ("[\\D\\S]+", CultureInvariant, "xxabxx", "Q"),
            ("[\\D\\S]+", CultureInvariant, "xx\nab", "Q"),
            ("[\\s\\S]+", CultureInvariant | Singleline, "xxabxx", "Q"),
            ("[\\s\\S]+", CultureInvariant | Singleline, "xx\nab", "Q"),
            ("[^x]+", CultureInvariant | Singleline, "xxabxx", "Q"),
            ("[^x]+", CultureInvariant | Singleline, "xx\nab", "Q"),
            ("[\\D\\S]+", CultureInvariant | Singleline, "xxabxx", "Q"),
            ("[\\D\\S]+", CultureInvariant | Singleline, "xx\nab", "Q"),

            // EscapeAscii
            ("\\x41+", CultureInvariant, "xxAAax", "Q"),
            ("\\u0041+", CultureInvariant, "xxAAax", "Q"),
            ("\\t+", CultureInvariant, "x\tx", "Q"),
            ("\\n+", CultureInvariant, "x\nx", "Q"),
            ("\\x41\\x42", CultureInvariant, "xxABxx", "Q"),
            ("\\u0041\\u0042", CultureInvariant, "xxABxx", "Q"),
            ("\\a\\e", CultureInvariant, "x\a\u001bx", "Q"),

            // Octal
            ("\\101\\102", CultureInvariant, "xxABxx", "Q"),
            ("\\0", CultureInvariant, "xa\0bx", "Q"),
            ("(a)\\101", CultureInvariant, "xxaAax", "Q"),

            // OctalQuantifiedDecline
            ("\\101+", CultureInvariant, "xxAAax", "Q"),

            // Explicit
            ("(ab|cd)ef", CultureInvariant | ExplicitCapture, "xxcdefxx", "<$1$2>"),
            ("((a)b)", CultureInvariant | ExplicitCapture, "xxabxx", "<$1$2>"),
            ("(?<x>ab)(?<y>cd)", CultureInvariant | ExplicitCapture, "xxabcdxx", "<$1$2>"),
            ("(?<x>ab)(?<y>cd)", CultureInvariant | ExplicitCapture, "xxabxx", "<$1$2>"),

            // ExplicitNamedBackref
            ("(?<x>ab)(c)\\k<x>", CultureInvariant | ExplicitCapture, "xxabcabxx", "Q"),
            ("(?<x>AB)(c)\\k<x>", CultureInvariant | ExplicitCapture | IgnoreCase, "xxabcabxx", "Q"),
            ("(?<x>ab)(c)\\k<x>", CultureInvariant | ExplicitCapture | RightToLeft, "xxabcabxx", "Q"),
            ("(?<x>ab)(c)\\k<x>", CultureInvariant | ExplicitCapture | Singleline, "xxabc\nabxx", "Q"),
            ("(?<x>ab)(c)\\k<x>", CultureInvariant | ExplicitCapture | Singleline, "xxabc\naxxx", "Q"),
            ("(?<w>a.+)\\k<w>", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naabaab\nzz", "Q"),
            ("(?<w>a.+)\\k<w>", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xx\naabc\nzz", "Q"),

            // ScopedExplicit
            ("(?n:(?<x>ab)(?<y>cd))", CultureInvariant, "xxabcdxx", "<$1$2>"),
            ("(?n:(?<x>ab)(?<y>cd))", CultureInvariant, "xxabxx", "<$1$2>"),
            ("(?n:(a)(?<y>b))", CultureInvariant, "xxabcxx", "<$1$2>"),
            ("(?<x>(?n:a))", CultureInvariant, "xxaxx", "<$1$2>"),
            ("(?n:(?<x>a))(b)", CultureInvariant, "xxabx", "<$1$2>"),

            // ExplicitFold
            ("(?<x>AB)(?<y>CD)", CultureInvariant | ExplicitCapture | IgnoreCase, "xxabcdxx", "<$1$2>"),
            ("(?<x>AB)(?<y>CD)", CultureInvariant | ExplicitCapture | IgnoreCase, "xxabxx", "<$1$2>"),
            ("(a)(b)", CultureInvariant | ExplicitCapture | IgnoreCase, "xxabxx", "<$1$2>"),

            // RtlMultiline
            ("^ab", CultureInvariant | Multiline | RightToLeft, "ab\nxx", "Q"),
            ("^ab", CultureInvariant | Multiline | RightToLeft, "xx\nabxx", "Q"),
            ("ab$", CultureInvariant | Multiline | RightToLeft, "xxab\nyy", "Q"),
            ("ab$", CultureInvariant | Multiline | RightToLeft, "abxx\nyy", "Q"),
            ("^ab$", CultureInvariant | Multiline | RightToLeft, "ab\nab", "Q"),
            ("^ab$", CultureInvariant | Multiline | RightToLeft, "xx\nab\nyy", "Q"),

            // RtlMultilineDot
            ("a.+b", CultureInvariant | Multiline | RightToLeft, "xa\nbxx", "Q"),
            ("a.+b", CultureInvariant | Multiline | RightToLeft, "xayb", "Q"),
            ("^a.+b$", CultureInvariant | Multiline | RightToLeft, "xx\naabb\nyy", "Q"),
            ("^ab$", CultureInvariant | Multiline | RightToLeft, "xx\naxxb", "Q"),

            // RtlSinglelineMultiline
            ("a.+b", CultureInvariant | Multiline | RightToLeft | Singleline, "x\naab\nx", "Q"),
            ("^ab", CultureInvariant | Multiline | RightToLeft | Singleline, "xx\nabxx", "Q"),
            ("(?<w>.+)", CultureInvariant | Multiline | RightToLeft | Singleline, "xxAB\nCDxx", "Q"),

            // SinglelineFold
            ("(?s)a.+b", CultureInvariant | IgnoreCase | Singleline, "x\naAB\nbx", "Q"),
            ("A.+B", CultureInvariant | IgnoreCase | Singleline, "x\naab\nx", "Q"),
            ("(?s)(a+).(b+)", CultureInvariant | IgnoreCase | Singleline, "x\naA\nbBx", "Q"),
            (".+", CultureInvariant | IgnoreCase | Singleline, "x\nab\nx", "Q"),
            ("(.+)", CultureInvariant | IgnoreCase | Singleline, "x\nab\nx", "Q"),

            // VarLookbehind
            ("(?<=a+)b", CultureInvariant, "xxaabx", "Q"),
            ("(?<=a+)b", CultureInvariant, "xxbx", "Q"),
            ("(?<=a|bc)d", CultureInvariant, "xxabcd", "Q"),
            ("(?<=a|bc)d", CultureInvariant, "xxd", "Q"),
            ("(?<!a+)b", CultureInvariant, "xaabx", "Q"),
            ("(?<=ab+)c", CultureInvariant, "xxabbc", "Q"),
            ("(?<=a{1,3})b", CultureInvariant, "xxaab", "Q"),
            ("(?<=a{1,3})b", CultureInvariant, "xxb", "Q"),
            ("(?<=a.+)b", CultureInvariant | Singleline, "xa\nzb", "Q"),
            ("(?<=a.+)b", CultureInvariant | Singleline, "xzb", "Q"),
            ("(?<!a.+)b", CultureInvariant | Singleline, "xb", "Q"),
            ("(?<!a.+)b", CultureInvariant | Singleline, "xa\nzb", "Q"),

            // VarLookahead
            ("a(?=b+)", CultureInvariant, "xaabx", "Q"),
            ("a(?=b+)", CultureInvariant, "xaax", "Q"),
            ("a(?=b|cd)", CultureInvariant, "xabcdx", "Q"),
            ("a(?=b|cd)", CultureInvariant, "xabx", "Q"),
            ("(?=(ab)+)c", CultureInvariant, "xxabc", "Q"),
            ("a(?!b+)", CultureInvariant, "xaabx", "Q"),

            // UnmatchedBackref
            ("(a)?b\\1", CultureInvariant, "xbxx", "Q"),
            ("(a)?b\\1", CultureInvariant, "xabx", "Q"),
            ("(a)|(b)\\1", CultureInvariant, "xbcx", "Q"),
            ("(a)?(b)?c\\1\\2", CultureInvariant, "xcxx", "Q"),

            // RepeatedCapture
            ("(a|b)+", CultureInvariant, "xxabbax", "<$1>"),
            ("(a+)+", CultureInvariant, "xxaaax", "<$1>"),
            ("(ab|cd)+", CultureInvariant, "xxabcdabxx", "<$1>"),
            ("(?<x>a|b)+", CultureInvariant, "xxabax", "<$1>"),

            // EmptyBranch
            ("(a|)", CultureInvariant, "xxax", "Q"),
            ("(a|)", CultureInvariant, "xxbx", "Q"),
            ("(a|b|)", CultureInvariant, "xxax", "Q"),
            ("(|a)", CultureInvariant, "xxax", "Q"),
            ("(a|)b", CultureInvariant, "xxabx", "Q"),

            // SinglelineMultiline
            ("^a.+b$", CultureInvariant | Multiline | Singleline, "xa\nabbb\nzz", "Q"),
            ("^a.+b$", CultureInvariant | Multiline | Singleline, "qa\nb\nzz", "Q"),
            ("^ab", CultureInvariant | Multiline | Singleline, "xx\nabxx", "Q"),
            ("ab$", CultureInvariant | Multiline | Singleline, "xxab\nyy", "Q"),

            // SinglelineContiguous
            (@"\Ga.+b", CultureInvariant | Singleline, "a\nab", "Q"),
            (@"\Ga.+b", CultureInvariant | Singleline, "xa\nab", "Q"),
            (@"\Ga+", CultureInvariant | Singleline, "a\naa", "Q"),
            (@"\Ga+", CultureInvariant | Singleline, "xaa", "Q"),

            // GroupedSinglelineMultiline
            ("^(a.+)b$", CultureInvariant | Multiline | Singleline, "xa\nabb\nzz", "Q"),
            ("^(a.+)b$", CultureInvariant | Multiline | Singleline, "xa\naxx\nzz", "Q"),
            ("^(a.+)(b+)$", CultureInvariant | Multiline | Singleline, "xa\nabbb\nzz", "Q"),
            ("^(a.+)(b+)$", CultureInvariant | Multiline | Singleline, "xa\nab\nzz", "Q"),

            // CategoryMultiline
            ("^\\p{L}+$", CultureInvariant | Multiline, "ab\nCD", "Q"),
            ("^\\p{L}+$", CultureInvariant | Multiline, "a1\nb2", "Q"),
            ("\\p{Nd}+$", CultureInvariant | Multiline, "ab12\nxy", "Q"),
            ("\\p{IsBasicLatin}+$", CultureInvariant | Multiline, "ab\nCD", "Q"),

            // ShorthandMultiline
            ("^\\d+$", CultureInvariant | Multiline, "12\n34", "Q"),
            ("^\\d+$", CultureInvariant | Multiline, "a1\nb2", "Q"),
            ("\\w+$", CultureInvariant | Multiline, "ab12\nxy", "Q"),
            ("\\w+$", CultureInvariant | Multiline, "ab \nxy ", "Q"),
            ("(\\d.+\\d)", CultureInvariant | Multiline, "xx\n1a2\nzz", "Q"),
            ("(\\d.+\\d)", CultureInvariant | Multiline, "xx\n1a\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline, "xx\nab\nzz", "Q"),
            ("(\\w.+\\w)", CultureInvariant | Multiline, "xx\na", "Q"),

            // NestedLookaround
            ("(?=(?<=x)a)a+", CultureInvariant, "xxaax", "Q"),
            ("(a)(?=(b))", CultureInvariant, "xxabbx", "Q"),
            ("(?<=a(?=b))c", CultureInvariant, "xabc", "Q"),
            ("(?!(a)b)\\w+", CultureInvariant, "xxcxxx", "Q"),
            ("(?<=x(?=y))a", CultureInvariant, "xxyax", "Q"),

            // FoldedLookbehind
            ("(?<=X)A+", CultureInvariant | IgnoreCase, "xxAaX", "Q"),
            ("(?<=X)A+", CultureInvariant | IgnoreCase, "xxx", "Q"),
            ("(?<!X)A+", CultureInvariant | IgnoreCase, "xxaaX", "Q"),
            ("(?<=AB)C", CultureInvariant | IgnoreCase, "xxabC", "Q"),

            // ExplicitMultilineSplit
            ("^(?<w>ab)", CultureInvariant | ExplicitCapture | Multiline, "xx\nabxx", "<${w}>"),
            ("^(?<w>ab)", CultureInvariant | ExplicitCapture | Multiline, "ab\nxx", "<${w}>"),
            ("(?<w>ab)$", CultureInvariant | ExplicitCapture | Multiline, "xxab\nyy", "<${w}>"),
            ("(?<w>ab)$", CultureInvariant | ExplicitCapture | Multiline, "abxx\nyy", "<${w}>"),

            // FoldedVarLookbehind
            ("(?<=A+)B", CultureInvariant | IgnoreCase, "xxAABx", "Q"),
            ("(?<=A+)B", CultureInvariant | IgnoreCase, "xxBx", "Q"),
            ("(?<=A|BC)D", CultureInvariant | IgnoreCase, "xxABCD", "Q"),

            // FoldedVarLookahead
            ("A(?=B+)", CultureInvariant | IgnoreCase, "xAABx", "Q"),

            // FoldedBalancing
            ("(?<a>A+)(?<-a>B+)", CultureInvariant | IgnoreCase, "xxAABbx", "Q"),
            ("(?<a>A+)(?<-a>B+)", CultureInvariant | IgnoreCase, "xxAABx", "Q"),
            ("^(?<o><)(?<c>>)(?<-o>)(?<-c>)$", CultureInvariant | IgnoreCase, "<>", "Q"),

            // BalancingOptionCombo
            ("(?<b>A+)(?<-b>B)", CultureInvariant | IgnoreCase, "xxaabbxx", "Q"),
            ("(?<b>A+)(?<-b>B)", CultureInvariant | IgnoreCase, "xxaabxx", "Q"),
            ("^(?<b>a+)(?<-b>b)$", CultureInvariant | Multiline, "aabb", "Q"),
            ("^(?<b>a+)(?<-b>b)$", CultureInvariant | Multiline, "xx\naabb", "Q"),

            // FoldedAtomic
            ("(?>A+)B", CultureInvariant | IgnoreCase, "xxAABx", "Q"),
            ("(?>A+)B", CultureInvariant | IgnoreCase, "xxAACx", "Q"),
            ("(?>AB|A)B", CultureInvariant | IgnoreCase, "xxABBx", "Q"),
            ("(?>A|AB)C", CultureInvariant | IgnoreCase, "xxABCx", "Q"),

            // FoldedConditional
            ("(A)?B(?(1)C|D)", CultureInvariant | IgnoreCase, "xABCD", "Q"),
            ("(A)?B(?(1)C|D)", CultureInvariant | IgnoreCase, "xBD", "Q"),
            ("(A)(?(1)B|C)", CultureInvariant | IgnoreCase, "xABx", "Q"),
            ("(A)?(?(1)B|C)", CultureInvariant | IgnoreCase, "xC", "Q"),

            // RtlExplicit
            ("(?<x>ab)(?<y>cd)", CultureInvariant | ExplicitCapture | RightToLeft, "xxabcdxx", "<$1$2>"),
            ("(?<x>ab)(?<y>cd)", CultureInvariant | ExplicitCapture | RightToLeft, "xxabxx", "<$1$2>"),
            ("(a)(b)", CultureInvariant | ExplicitCapture | RightToLeft, "xxabxx", "<$1$2>"),

            // RtlSingleline
            ("a.+b", CultureInvariant | RightToLeft | Singleline, "x\naab\nx", "Q"),
            ("a.+b", CultureInvariant | RightToLeft | Singleline, "xx\nyy", "Q"),
            ("(a+).+(b+)", CultureInvariant | RightToLeft | Singleline, "x\naab\nx", "Q"),
            ("^ab", CultureInvariant | RightToLeft | Singleline, "xx\nabxx", "Q"),
            ("(a.+)b", CultureInvariant | RightToLeft | Singleline, "xa\nbz", "Q"),
            ("\\d.+\\d", CultureInvariant | RightToLeft | Singleline, "x1\n2y", "Q"),
            ("(\\w.+\\w)", CultureInvariant | RightToLeft | Singleline, "xa", "Q"),
            ("(\\s.+\\s)", CultureInvariant | RightToLeft | Singleline, "a \n b", "Q"),

            // RtlSinglelineExplicit
            ("(?<w>.+)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xxAB\nCDxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xxAxxBxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xxAxx", "Q"),
            ("(A.+)B", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xxAAxxBxx", "Q"),

            // FoldSinglelineRtl
            ("(?<w>.+)", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "xxAB\nCDxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "xxAXXBxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "xxaxxbxx", "Q"),
            ("(?<w>A.+B)", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "xxAxx", "Q"),

            // RtlFold
            ("[a-z]+", CultureInvariant | IgnoreCase | RightToLeft, "xxABCxx", "Q"),
            ("a+b", CultureInvariant | IgnoreCase | RightToLeft, "xxaabx", "Q"),
            ("\\w+", CultureInvariant | IgnoreCase | RightToLeft, "xxaB1xx", "Q"),

            // RtlFoldLiteralSplit
            ("ab", CultureInvariant | IgnoreCase | RightToLeft, "xxABxx", "Q"),

            // SinglelineExplicit
            ("(?s)(a+)(b+)", CultureInvariant | ExplicitCapture | Singleline, "x\naab\nx", "<$1$2>"),
            ("(?<x>.+)(?<y>.+)", CultureInvariant | ExplicitCapture | Singleline, "x\naab\nx", "<${x}${y}>"),
            ("(a).+(b)", CultureInvariant | ExplicitCapture | Singleline, "x\na\nb\nx", "<$1$2>"),

            // ExplicitSinglelineMultiline
            ("^(?<w>.+)$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xa\nAB\nzz", "Q"),
            ("^(?<w>.+)$", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xa\nax\nzz", "Q"),
            ("(?<w>.+)", CultureInvariant | ExplicitCapture | Multiline | Singleline, "xxAB\nCDxx", "Q"),

            // ScopedFold
            ("(?n:(?<x>AB)(?<y>CD))", CultureInvariant | IgnoreCase, "xxabcdxx", "<$1$2>"),
            ("(?n:(A)(?<y>B))", CultureInvariant | IgnoreCase, "xxabxx", "<$1$2>"),
            ("(?<x>(?n:A))", CultureInvariant | IgnoreCase, "xxaxx", "<$1$2>"),

            // RtlWordFold
            ("\\bWORD\\b", CultureInvariant | IgnoreCase | RightToLeft, "xxwordxx", "Q"),
            ("\\BWORD\\B", CultureInvariant | IgnoreCase | RightToLeft, "xwordy", "Q"),
            ("(\\b\\w+\\b)", CultureInvariant | IgnoreCase | RightToLeft, "xxhi therexx", "Q"),

            // FoldedNested
            ("((A)+)+B", CultureInvariant | IgnoreCase, "xxAABx", "Q"),
            ("((A)+)+B", CultureInvariant | IgnoreCase, "xxAACx", "Q"),
            ("((A|AB)+)C", CultureInvariant | IgnoreCase, "xxABABCx", "Q"),
            ("((A)*)+B", CultureInvariant | IgnoreCase, "xxBx", "Q"),

            // FoldedLazy
            ("(A+?)B", CultureInvariant | IgnoreCase, "xxAABx", "<$1$2>"),
            ("(A+?)B", CultureInvariant | IgnoreCase, "xxBx", "<$1$2>"),
            ("(A??)B", CultureInvariant | IgnoreCase, "xxABx", "<$1$2>"),
            ("(A+?)(B+?)", CultureInvariant | IgnoreCase, "xxAABBx", "<$1$2>"),

            // RtlAtomic
            ("(?>a+)b", CultureInvariant | RightToLeft, "xaab", "Q"),
            ("(?>a+)b", CultureInvariant | RightToLeft, "xaac", "Q"),
            ("(?>ab|a)b", CultureInvariant | RightToLeft, "xxabb", "Q"),
            ("(?>a+)(b+)", CultureInvariant | RightToLeft, "xaabb", "Q"),

            // RtlConditional
            ("(a)?b(?(1)c|d)", CultureInvariant | RightToLeft, "xabcd", "Q"),
            ("(a)?b(?(1)c|d)", CultureInvariant | RightToLeft, "xbd", "Q"),
            ("(a)(?(1)b|c)", CultureInvariant | RightToLeft, "xabx", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant | RightToLeft, "xc", "Q"),

            // RtlConditionalAtomicMultiline
            ("^(?(?=a)a|b)", CultureInvariant | Multiline | RightToLeft, "abxx", "Q"),
            ("(a)?(?(1)b|c)", CultureInvariant | Multiline | RightToLeft, "xx\nab", "Q"),
            ("^(?>a+)b", CultureInvariant | Multiline | RightToLeft, "xx\naabxx", "Q"),
            ("^(?>a+)b", CultureInvariant | Multiline | RightToLeft, "xx\naax", "Q"),
            ("(?>ab+)c", CultureInvariant | Multiline | RightToLeft, "xx\nabbc\nzz", "Q"),

            // RtlBalancing
            ("(?<a>a+)(?<-a>b+)", CultureInvariant | RightToLeft, "xaabbx", "Q"),
            ("(?<a>a+)(?<-a>b+)", CultureInvariant | RightToLeft, "xaabx", "Q"),
            ("^(?<o><)(?<c>>)(?<-o>)(?<-c>)$", CultureInvariant | RightToLeft, "<>", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | RightToLeft | Singleline, "xab\nab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | RightToLeft | Singleline, "xab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab\nab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | ExplicitCapture | RightToLeft | Singleline, "xab", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | Multiline | RightToLeft, "xx\naab\nzz", "Q"),
            ("(?<b>.+)(?<-b>.+)", CultureInvariant | Multiline | RightToLeft, "xx\nab", "Q"),

            // RtlFoldLetterRun
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "A\nB", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "AB", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | RightToLeft | Singleline, "A\n1", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "A\nB", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "AB", "Q"),
            ("\\p{L}.+\\p{L}", CultureInvariant | IgnoreCase | Multiline | RightToLeft, "A\n1", "Q"),

            // FoldedRtlAtomic
            ("(?>A+)B", CultureInvariant | IgnoreCase | RightToLeft, "xxAABx", "Q"),
            ("(?>A+)B", CultureInvariant | IgnoreCase | RightToLeft, "xxAACx", "Q"),
            ("(?>AB|A)B", CultureInvariant | IgnoreCase | RightToLeft, "xxABBx", "Q"),

            // FoldedRtlConditional
            ("(A)?B(?(1)C|D)", CultureInvariant | IgnoreCase | RightToLeft, "xABCD", "Q"),
            ("(A)?B(?(1)C|D)", CultureInvariant | IgnoreCase | RightToLeft, "xBD", "Q"),
            ("(A)(?(1)B|C)", CultureInvariant | IgnoreCase | RightToLeft, "xABx", "Q"),

            // FoldedRtlBackref
            ("(A+)\\1", CultureInvariant | IgnoreCase | RightToLeft, "xxAAax", "Q"),
            ("(AB)C\\1", CultureInvariant | IgnoreCase | RightToLeft, "xxABABx", "Q"),

            // FoldedRtlLookaround
            ("(?<=X)A+", CultureInvariant | IgnoreCase | RightToLeft, "xxAaX", "Q"),
            ("(?<=X)A+", CultureInvariant | IgnoreCase | RightToLeft, "xxx", "Q"),
            ("A(?=B)", CultureInvariant | IgnoreCase | RightToLeft, "xaaBx", "Q"),
            ("(?<!X)A+", CultureInvariant | IgnoreCase | RightToLeft, "xxaaX", "Q"),

            // FoldedRtlLookbehindGroup
            ("(?<=(?<w>AB))CD", CultureInvariant | IgnoreCase | RightToLeft, "xxabcdxx", "Q"),
            ("(?<=(?<w>AB))CD", CultureInvariant | IgnoreCase | RightToLeft, "xxabxx", "Q"),
            ("(?<=AB)(?<w>CD)", CultureInvariant | IgnoreCase | RightToLeft, "xxabcdxx", "Q"),
            ("(?<=(A))B", CultureInvariant | IgnoreCase | RightToLeft, "xxaabxx", "Q"),

            // TripleCombo
            ("(?<X>AB)(?<Y>CD)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft, "xxabcdxx", "<$1$2>"),
            ("(?<X>AB)(?<Y>CD)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft, "xxabxx", "<$1$2>"),
            ("(A)(B)", CultureInvariant | ExplicitCapture | IgnoreCase | RightToLeft, "xxabxx", "<$1$2>"),

            // FoldExplicitMultilineSplit
            ("^(?<w>AB)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xx\nabxx", "Q"),
            ("^(?<w>AB)", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xx\naxxx", "Q"),
            ("(?<w>AB)$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xxab\nyy", "Q"),
            ("(?<w>AB)$", CultureInvariant | ExplicitCapture | IgnoreCase | Multiline, "xxabx\nyy", "Q"),

            // FoldedContiguousDecline
            (@"\Gab", CultureInvariant | IgnoreCase, "ABxx", "Q"),
            (@"\Gab", CultureInvariant | IgnoreCase, "abxx", "Q"),
            (@"\Gab", CultureInvariant | IgnoreCase, "xxABxx", "Q"),

            // SinglelineIgnoreCaseMultiline
            ("^a.+b$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xa\nABBB\nzz", "Q"),
            ("^ab", CultureInvariant | IgnoreCase | Multiline | Singleline, "xx\nABxx", "Q"),
            ("ab$", CultureInvariant | IgnoreCase | Multiline | Singleline, "xxAB\nyy", "Q"),
            ("^a.+b$", CultureInvariant | IgnoreCase | Multiline | Singleline, "qa\nb\nzz", "Q"),

            // SinglelineIgnoreCaseExplicit
            ("(?<w>.+)", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "xxABxx", "Q"),
            ("(?<w>.+)", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "xx\nABxx", "Q"),
            ("(?<x>A+)(?<w>B+)", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "xxAABBxx", "Q"),
            ("(?<x>A+)(?<w>B+)", CultureInvariant | ExplicitCapture | IgnoreCase | Singleline, "xxAAxx", "Q"),

            // LookbehindExplicit
            ("(?<=ab)(c)", CultureInvariant | ExplicitCapture, "xxabcxx", "Q"),
            ("(?<=(?<w>ab))(c)", CultureInvariant | ExplicitCapture, "xxabcxx", "Q"),
            ("(?<=a)(?<w>b)", CultureInvariant | ExplicitCapture, "xxabxx", "Q"),
            ("(?<=ab)(c)", CultureInvariant | ExplicitCapture, "xxabxx", "Q"),

            // LookbehindMultiline
            ("(?<=^a)b", CultureInvariant | Multiline, "xx\nabxx", "Q"),
            ("(?<=^a)b", CultureInvariant | Multiline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | Multiline, "xx\nxabxx", "Q"),
            ("(?<!^a)b", CultureInvariant | Multiline, "xx\nabxx", "Q"),

            // ContiguousMultilineLaneSplit
            (@"\Gab", CultureInvariant | Multiline, "abxx", "Q"),
            (@"\Gab", CultureInvariant | Multiline, "xx\nabxx", "Q"),
            (@"\Gab", CultureInvariant | Multiline, "ab\nab", "Q"),
            (@"(\Gab)", CultureInvariant | Multiline, "xxabxx", "Q"),
            (@"(\Gab)", CultureInvariant | Multiline, "abxx", "Q"),
            (@"(?m:\Gab)", CultureInvariant | Multiline, "xx\nabxx", "Q"),
            (@"(?m:\Gab)", CultureInvariant | Multiline, "ab\nab", "Q"),

            // ScopedExplicitMultilineSplit
            ("(?n:^(?<w>ab))", CultureInvariant | Multiline, "xx\nabxx", "Q"),
            ("(?n:^(?<w>ab))", CultureInvariant | Multiline, "xx\nxabxx", "Q"),
            ("(?n:(?<w>ab)$)", CultureInvariant | Multiline, "xxab\nyy", "Q"),
            ("(?n:(?<w>ab)$)", CultureInvariant | Multiline, "xxabx\nyy", "Q"),

            // AnchoredMultigroup
            ("^(?<x>a)(?<w>b)", CultureInvariant, "abxx", "Q"),
            ("^(?<x>a)(?<w>b)", CultureInvariant, "xxabxx", "Q"),
            ("^(?<x>a)(?<w>b)", CultureInvariant, "ab", "Q"),
            ("^(?<x>a)(?<w>b)", CultureInvariant, "xab", "Q"),

            // AnchoredMultigroupMultilineSplit
            ("^(?<x>a)(?<w>b)", CultureInvariant | Multiline, "xx\nabxx", "Q"),
            ("^(?<x>a)(?<w>b)", CultureInvariant | Multiline, "ab\nxx", "Q"),
            ("^(?<x>a)(?<w>b)", CultureInvariant | Multiline, "xx\nxabxx", "Q"),

            // DoublyAnchoredDetailed
            ("^(?<x>ab)(?<w>cd)$", CultureInvariant | Multiline, "xx\nabcd", "Q"),
            ("^(?<x>ab)(?<w>cd)$", CultureInvariant | Multiline, "xx\nabcx", "Q"),
            ("^(?<x>ab)(?<w>cd)$", CultureInvariant | Multiline, "abcd\nxx", "Q"),

            // DoublyAnchoredSplit
            ("^(?<x>ab)(?<w>cd)$", CultureInvariant, "abcd", "Q"),
            ("^(?<x>ab)(?<w>cd)$", CultureInvariant, "xabcd", "Q"),
            ("^(?<x>ab)(?<w>cd)$", CultureInvariant, "abc", "Q"),
        ];
        foreach (var (pattern, options, text, template) in rows)
        {
            yield return [pattern, options, text, template];
            yield return [pattern, options | Compiled, text, template];
        }
    }

    [Theory]
    [MemberData(nameof(OwnedOperationCases))]
    public void OwnedOperationsMatchOracle(string pattern, RegexOptions options, string text, string template)
    {
        AssertOwnedOperations(pattern, options, text, template);
    }

    // Compare public operations on independent cold instances. Each row
    // keeps the original BCL comparator and expected byte coordinates.
    private static void AssertOwnedOperations(string pattern, RegexOptions options, string text, string template,
        int capacityFactor = 16, int capacityPadding = 128)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Utf8AsciiScalarOperationAssertions.Owned(isMatchRegex);
        var countRegex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        var matchRegex = new Utf8Regex(pattern, options: options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
            Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        }
        var detailedRegex = new Utf8Regex(pattern, options: options);
        var actualDetailed = Utf8MatchSequence.First(detailedRegex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expectedMatch, actualDetailed, input, text);
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
        var templateBytes = Encoding.UTF8.GetBytes(template);
        var expectedBytes = Encoding.UTF8.GetBytes(oracle.Replace(text, template));
        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = bytesRegex.Replace(input, templateBytes);
        Assert.Equal(expectedBytes, bytesActual);
        var destinationRegex = new Utf8Regex(pattern, options: options);
        Span<byte> destination = stackalloc byte[input.Length * capacityFactor + capacityPadding];
        var status = destinationRegex.TryReplace(input, templateBytes, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
    }

    private static void AssertEnumerationAndSplits(string pattern, RegexOptions options, string text)
    {
        // These fixture cases have overlapping BCL semantics. Compare ordered
        // enumeration/split coordinates while retaining the owned-route check.
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        var expected = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length))));
        }

        var actual = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in regex.EnumerateMatches(input))
        {
            actual.Add((valueMatch.StartOffsetInBytes, (valueMatch.EndOffsetInBytes - valueMatch.StartOffsetInBytes)));
        }

        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Index, actual[i].Index);
            Assert.Equal(expected[i].Length, actual[i].Length);
        }

        Utf8AsciiScalarOperationAssertions.Owned(regex);

        var splitRegex = new Utf8Regex(pattern, options: options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], global::System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), global::System.Text.Encoding.UTF8.GetByteCount(text[start..end])));
        }

        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }

        Assert.Equal(expectedSplits, actualSplits);
    }

    private static void AssertOwnedMatchingAndOutput(string pattern, RegexOptions options, string text, string template,
        int capacityFactor, int capacityPadding)
    {
        var sliceRegex = new Utf8Regex(pattern, options: options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        AssertOwnedOperations(pattern, options, text, template, capacityFactor, capacityPadding);
    }

}
