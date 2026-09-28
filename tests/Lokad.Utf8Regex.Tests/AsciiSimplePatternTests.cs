using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class AsciiSimplePatternTests
{
    [Fact]
    public void SupportAnalyzerClassifiesDotConcatenationAsAsciiSimplePattern()
    {
        var regex = new Utf8Regex("ab.cd", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.FixedDistanceAsciiChar, regex.Inspection.SearchPlan.Kind);
    }

    [Fact]
    public void SupportAnalyzerClassifiesInvariantIgnoreCaseDotPatternAsAsciiSimplePattern()
    {
        var regex = new Utf8Regex("ab.cd", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiteral, regex.Inspection.SearchPlan.Kind);
    }

    [Theory]
    [InlineData("ab[0-9]d")]
    [InlineData("a[^x]d")]
    [InlineData("^abc")]
    [InlineData("abc$")]
    [InlineData("^ab.cd$")]
    [InlineData("(ab.cd)")]
    public void SupportAnalyzerClassifiesAsciiCharacterClassesAsAsciiSimplePattern(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
    }

    [Theory]
    [InlineData(@"\d\d")]
    [InlineData(@"a\wb")]
    [InlineData(@"\d{2}")]
    [InlineData(@"[\d][A-Z]")]
    [InlineData(@"[\w-][\s]")]
    [InlineData(@"ab.cd|xy\d\d")]
    [InlineData(@"\d{2,4}")]
    public void SupportAnalyzerLowersUnicodeSensitiveCharacterClassesAsAsciiIntersections(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.False(regex.Inspection.SimplePatternPlan.IsUtf8ByteSafe);
    }

    [Theory]
    [InlineData(@"a\sb")]
    public void SupportAnalyzerLowersUnicodeWhitespaceClassesAsUnsafeAsciiIntersections(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.False(regex.Inspection.SimplePatternPlan.IsUtf8ByteSafe);
    }

    [Fact]
    public void SupportAnalyzerPreservesPredefinedClassInsideOuterNegation()
    {
        var regex = new Utf8Regex(@"[^\S]", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.False(regex.Inspection.SimplePatternPlan.IsUtf8ByteSafe);
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
            var actual = new Utf8Regex(pattern, options);
            var utf8 = System.Text.Encoding.UTF8.GetBytes(input);

            Assert.Equal(NativeExecutionKind.AsciiSimplePattern, actual.Inspection.ExecutionKind);
            Assert.False(actual.Inspection.SimplePatternPlan.IsUtf8ByteSafe);
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
        var actual = new Utf8Regex(pattern, options);
        var utf8 = System.Text.Encoding.UTF8.GetBytes(input);
        var actualMatches = new List<string>();
        var actualSplits = new List<string>();

        foreach (var match in actual.EnumerateMatches(utf8))
        {
            actualMatches.Add(System.Text.Encoding.UTF8.GetString(
                utf8.AsSpan(match.IndexInBytes, match.LengthInBytes)));
        }

        foreach (var split in actual.EnumerateSplits(utf8))
        {
            actualSplits.Add(split.GetValueString());
        }

        Span<byte> destination = stackalloc byte[utf8.Length];
        var replaceStatus = actual.TryReplace(utf8, "#", destination, out var bytesWritten);
        var expectedReplacement = expected.Replace(input, "#");

        Assert.False(actual.Inspection.SimplePatternPlan.IsUtf8ByteSafe);
        Assert.Equal(expected.Count(input), actual.Count(utf8));
        Assert.Equal(expected.Matches(input).Select(static match => match.Value), actualMatches);
        Assert.Equal(expectedReplacement, actual.ReplaceToString(utf8, "#"));
        Assert.Equal(expectedReplacement, System.Text.Encoding.UTF8.GetString(actual.Replace(utf8, "#")));
        Assert.Equal(expectedReplacement, System.Text.Encoding.UTF8.GetString(actual.Replace(utf8, "#"u8)));
        Assert.Equal(System.Buffers.OperationStatus.Done, replaceStatus);
        Assert.Equal(expectedReplacement, System.Text.Encoding.UTF8.GetString(destination[..bytesWritten]));
        Assert.Equal(expected.Split(input), actualSplits);
    }

    [Fact]
    public void SupportAnalyzerBuildsRunPlanForRepeatedAsciiCharClass()
    {
        var analysis = Utf8FrontEnd.Compile(@"[A-Za-z]{8,13}", RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, analysis.ExecutionKind);
        Assert.True(analysis.SimplePatternPlan.RunPlan.HasValue);
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
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

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
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

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
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(
            Regex.Count(input, pattern, options),
            regex.Count(System.Text.Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void StructuralLinearDeterministicRawScanMatchesDenseSimplePatternOffsets()
    {
        var analysis = Utf8FrontEnd.Compile("ab[0-9]d", RegexOptions.CultureInvariant);
        var input = System.Text.Encoding.UTF8.GetBytes("ab1d-ab2d-ab3d");
        var state = new Utf8AsciiDeterministicScanState(0, analysis.StructuralLinearProgram.DeterministicProgram.SearchLiteralOffset);
        var offsets = new List<int>();

        while (Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
            analysis.StructuralLinearProgram,
            input,
            ref state,
            budget: Utf8ExecutionDeadline.Infinite,
            out var match))
        {
            offsets.Add(match.Index);
            Assert.Equal(4, match.Length);
        }

        Assert.Equal([0, 5, 10], offsets);
    }

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
        if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            return;
        }

        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

        Assert.True(Utf8AsciiDeterministicFixedWidthCountExecutor.TryCount(
            analysis.StructuralLinearProgram,
            inputBytes,
            Utf8ExecutionDeadline.Infinite,
            out var actual));
        Assert.Equal(Regex.Count(input, pattern, RegexOptions.CultureInvariant), actual);
    }

    [Fact]
    public void VectorizedFixedWidthCountMatchesDotNetAcrossVectorBoundaries()
    {
        if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            return;
        }

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
                Assert.True(Utf8AsciiDeterministicFixedWidthCountExecutor.TryCount(
                    analysis.StructuralLinearProgram,
                    inputBytes,
                    Utf8ExecutionDeadline.Infinite,
                    out var actual));
                Assert.Equal(Regex.Count(input, patternCase.Pattern, RegexOptions.CultureInvariant), actual);
            }
        }
    }

    [Fact]
    public void VectorizedFixedWidthCountHandsLongBarrenSuffixToScalarSearch()
    {
        if (!System.Runtime.Intrinsics.X86.Avx2.IsSupported)
        {
            return;
        }

        const string pattern = "ab[0-9]d";
        var input = string.Concat(
            "ab1d-ab2d-ab3d-ab4d-",
            new string('x', 512),
            "-ab7d");
        var analysis = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant);
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);

        Assert.True(Utf8AsciiDeterministicFixedWidthCountExecutor.TryCount(
            analysis.StructuralLinearProgram,
            inputBytes,
            Utf8ExecutionDeadline.Infinite,
            out var actual));
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
            new Utf8Regex(pattern, options).Count(inputBytes));
        Assert.Equal(
            Regex.Count(input, pattern, options, TimeSpan.FromSeconds(10)),
            new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10)).Count(inputBytes));
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
            new Utf8Regex(pattern, options).Count(System.Text.Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CompiledFixedWidthCountPreservesDiagnosticEmitRoute()
    {
        const string Input = "ab1d-ab2d-ab3d";
        var regex = new Utf8Regex(
            "ab[0-9]d",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var session = Utf8SearchDiagnosticsSession.Start();

        try
        {
            Assert.Equal(
                Regex.Count(Input, "ab[0-9]d", RegexOptions.CultureInvariant),
                regex.Count(System.Text.Encoding.UTF8.GetBytes(Input)));
            Assert.Equal(Utf8ExecutionRoute.NativeStructuralLinearEmit, session.ExecutionRoute);
        }
        finally
        {
            session.Complete();
        }
    }

    [Theory]
    [InlineData("^ab[0-9]d", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d$", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    [InlineData("ab.d", RegexOptions.CultureInvariant)]
    [InlineData("ab[02468]d", RegexOptions.CultureInvariant)]
    [InlineData("a[0-9]", RegexOptions.CultureInvariant)]
    [InlineData("abcd[0-9]efgh", RegexOptions.CultureInvariant)]
    public void VectorizedFixedWidthCountRejectsUnqualifiedPlans(string pattern, RegexOptions options)
    {
        var analysis = Utf8FrontEnd.Compile(pattern, options);
        var input = System.Text.Encoding.UTF8.GetBytes(new string('x', 64) + "ab2d");

        Assert.False(Utf8AsciiDeterministicFixedWidthCountExecutor.TryCount(
            analysis.StructuralLinearProgram,
            input,
            Utf8ExecutionDeadline.Infinite,
            out _));
    }

    [Fact]
    public void VectorizedFixedWidthCountRejectsFiniteDeadline()
    {
        var analysis = Utf8FrontEnd.Compile("ab[0-9]d", RegexOptions.CultureInvariant);
        var input = System.Text.Encoding.UTF8.GetBytes(new string('x', 64) + "ab2d");

        Assert.False(Utf8AsciiDeterministicFixedWidthCountExecutor.TryCount(
            analysis.StructuralLinearProgram,
            input,
            Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(10)),
            out _));
    }

    [Theory]
    [InlineData("cat|dog")]
    [InlineData("cat|horse")]
    [InlineData("(?:cat|horse)")]
    public void SupportAnalyzerClassifiesAsciiLiteralAlternationsAsNativeLiteralFamilies(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

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
    public void AbsoluteStartAnchoredLinearOperationsMatchOracle(string pattern, string inputText)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);

            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));

            var expectedMatch = oracle.Match(inputText);
            var actualMatch = regex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
            }

            Assert.Equal(oracle.Match(inputText).Success, regex.MatchDetailed(input).Success);

            var expectedPositions = new List<string>();
            foreach (Match m in oracle.Matches(inputText))
            {
                expectedPositions.Add(m.Index + ":" + m.Length);
            }

            var actualPositions = new List<string>();
            foreach (var m in regex.EnumerateMatches(input))
            {
                actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
            }

            Assert.Equal(expectedPositions, actualPositions);

            foreach (var offset in new[] { 0, 1, 2 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                Assert.Equal(oracle.IsMatch(inputText, offset), regex.IsMatchFromUtf16Offset(input, offset));
                Assert.Equal(oracle.Matches(inputText, offset).Count, regex.CountFromUtf16Offset(input, offset));
                Assert.Equal(oracle.Match(inputText, offset).Success, regex.MatchFromUtf16Offset(input, offset).Success);
                Assert.Equal(oracle.Match(inputText, offset).Success, regex.MatchDetailedFromUtf16Offset(input, offset).Success);

                var expectedOffsetPositions = new List<string>();
                foreach (Match m in oracle.Matches(inputText, offset))
                {
                    expectedOffsetPositions.Add(m.Index + ":" + m.Length);
                }

                var actualOffsetPositions = new List<string>();
                foreach (var m in regex.EnumerateMatchesFromUtf16Offset(input, offset))
                {
                    actualOffsetPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
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
            var regex = new Utf8Regex(pattern, options);

            Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));

            var actualPositions = new List<string>();
            foreach (var m in regex.EnumerateMatches(input))
            {
                actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
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
            var regex = new Utf8Regex(pattern, options);

            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));

            var expectedMatch = oracle.Match(inputText);
            var actualMatch = regex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
            }

            Assert.Equal(oracle.Match(inputText).Success, regex.MatchDetailed(input).Success);

            var expectedPositions = new List<string>();
            foreach (Match m in oracle.Matches(inputText))
            {
                expectedPositions.Add(m.Index + ":" + m.Length);
            }

            var actualPositions = new List<string>();
            foreach (var m in regex.EnumerateMatches(input))
            {
                actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
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

    [Theory]
    [MemberData(nameof(LineEndAnchorScanCases))]
    public void LineEndAnchorScanOperationsMatchOracle(string pattern, string inputText)
    {
        var input = System.Text.Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);

            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));

            var expectedMatch = oracle.Match(inputText);
            var actualMatch = regex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
            }

            Assert.Equal(oracle.Match(inputText).Success, regex.MatchDetailed(input).Success);

            var expectedPositions = new List<string>();
            foreach (Match m in oracle.Matches(inputText))
            {
                expectedPositions.Add(m.Index + ":" + m.Length);
            }

            var actualPositions = new List<string>();
            foreach (var m in regex.EnumerateMatches(input))
            {
                actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
            }

            Assert.Equal(expectedPositions, actualPositions);

            foreach (var offset in new[] { 0, 1, 2 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                Assert.Equal(oracle.IsMatch(inputText, offset), regex.IsMatchFromUtf16Offset(input, offset));
                Assert.Equal(oracle.Matches(inputText, offset).Count, regex.CountFromUtf16Offset(input, offset));
                Assert.Equal(oracle.Match(inputText, offset).Success, regex.MatchFromUtf16Offset(input, offset).Success);
                Assert.Equal(oracle.Match(inputText, offset).Success, regex.MatchDetailedFromUtf16Offset(input, offset).Success);

                var expectedOffsetPositions = new List<string>();
                foreach (Match m in oracle.Matches(inputText, offset))
                {
                    expectedOffsetPositions.Add(m.Index + ":" + m.Length);
                }

                var actualOffsetPositions = new List<string>();
                foreach (var m in regex.EnumerateMatchesFromUtf16Offset(input, offset))
                {
                    actualOffsetPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
                }

                Assert.Equal(expectedOffsetPositions, actualOffsetPositions);
            }
        }
    }

    [Theory]
    [InlineData("^ab$", true)]
    [InlineData("\\Aab\\z", true)]
    [InlineData("(?m:^ab$)", false)]
    public void StartAnchorKindThreadsThroughSimplePlan(string pattern, bool expectedAbsolute)
    {
        // Absolute `^`/`\A` lower distinctly from line-anchored `(?m:^)`: only
        // the former may pin linear verification at offset 0.
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.True(regex.Inspection.SimplePatternPlan.IsStartAnchored);
        Assert.Equal(expectedAbsolute, regex.Inspection.SimplePatternPlan.IsAbsoluteStartAnchored);
    }

    [Theory]
    [InlineData("^ab$", true)]
    [InlineData("\\Aab\\z", true)]
    [InlineData("ab$", true)]
    [InlineData("(?m:^ab$)", false)]
    [InlineData("^(?m:ab$)", false)]
    [InlineData("(?m:ab$)", false)]
    public void EndAnchorKindThreadsThroughSimplePlan(string pattern, bool expectedAbsolute)
    {
        // Absolute end anchors (`$` outside multiline, `\Z`, `\z`) lower
        // distinctly from line anchors (`(?m:$)`): only the former pins the
        // match at the input end in linear verification.
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, regex.Inspection.ExecutionKind);
        Assert.True(regex.Inspection.SimplePatternPlan.IsEndAnchored);
        Assert.Equal(expectedAbsolute, regex.Inspection.SimplePatternPlan.IsAbsoluteEndAnchored);
    }

    public static IEnumerable<object?[]> GrouplessSimpleDetailedCases()
    {
        // Groupless simple patterns shape group 0 from the shared value path,
        // so detailed matches never construct the fallback engine where the value
        // path itself is owned. `a.c` keeps parity while its value path stays
        // BCL-backed; only ASCII-star-free rows below assert zero construction.
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
            var regex = new Utf8Regex(pattern, options);
            var expected = oracle.Match(inputText);

            var actual = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Index, actual.GetGroup(0).IndexInUtf16);
                Assert.Equal(expected.Length, actual.GetGroup(0).LengthInUtf16);
            }

            // Owned discovery stays construction-free on the ordinary
            // lane; the compiled lane keeps parity while its value path stays
            // BCL-backed, and `a.c` keeps parity on both lanes.
            if (expectOwned && (options & RegexOptions.Compiled) == 0)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            }

            foreach (var offset in new[] { 0, 1, 2 })
            {
                if (offset > inputText.Length)
                {
                    continue;
                }

                var expectedOffset = oracle.Match(inputText, offset);
                var actualOffset = regex.MatchDetailedFromUtf16Offset(input, offset);
                Assert.Equal(expectedOffset.Success, actualOffset.Success);
                if (expectedOffset.Success)
                {
                    Assert.Equal(expectedOffset.Index, actualOffset.GetGroup(0).IndexInUtf16);
                    Assert.Equal(expectedOffset.Length, actualOffset.GetGroup(0).LengthInUtf16);
                }
            }
        }
    }

    public static IEnumerable<object?[]> OwnedRouteZeroConstructionCases()
    {
        // Routing must never construct the fallback engine just to decide a route:
        // every predicate below answers from owned analysis state. Rows cover all
        // five operations on shapes whose values are served owned end to end.
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

        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(inputText), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);

        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(inputText).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);

        var matchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Match(inputText).Success, matchRegex.Match(input).Success);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);

        var detailedRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Match(inputText).Success, detailedRegex.MatchDetailed(input).Success);
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);

        var enumerateRegex = new Utf8Regex(pattern, options);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NonAsciiSplitCases()
    {
        string[] patterns = ["ab[0-9]d", "[0-9]{3}"];
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
    [MemberData(nameof(NonAsciiSplitCases))]
    public void EnumerateMatchesOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
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
    public void BoundedRepetitionSplitKeepsBclShaping(bool compiled)
    {
        // Declared R05 debt: bounded-repetition simple shapes (\d{2,4} here) are not
        // byte-safe, so discovery and split stay BCL-backed on non-ASCII input.
        // Parity holds; construction is pinned.
        const int count = int.MaxValue;
        const string pattern = "\\d{2,4}";
        const string text = "\u00e9\u00e9123\u00e9";
        var options = RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);

        var expectedMatches = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expectedMatches.Add((expectedMatch.Index, expectedMatch.Length));
        }

        var actualMatches = new System.Collections.Generic.List<(int Index, int Length)>();
        foreach (var valueMatch in regex.EnumerateMatches(input))
        {
            actualMatches.Add((valueMatch.IndexInUtf16, valueMatch.LengthInUtf16));
        }

        Assert.Equal(expectedMatches.Count, actualMatches.Count);

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

    public static System.Collections.Generic.IEnumerable<object?[]> NonAsciiReplaceCases()
    {
        string[] patterns = ["ab[0-9]d", "[0-9]{3}"];
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

    [Theory]
    [InlineData("^$")]
    [InlineData("^")]
    [InlineData("$")]
    [InlineData("(ab)+")]
    [InlineData("(?=ab)")]
    [InlineData("(?!ab)[a-z]")]
    public void SupportAnalyzerKeepsAnchorOnlyPatternsInFallback(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }
}
