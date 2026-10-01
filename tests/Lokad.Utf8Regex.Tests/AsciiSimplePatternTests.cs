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

    public static System.Collections.Generic.IEnumerable<object?[]> NonAsciiEvaluatorCases()
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
    [MemberData(nameof(NonAsciiEvaluatorCases))]
    public void EvaluatorOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
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
        var regex = new Utf8Regex(pattern, options);

        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);

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
    public void ScalarDiscoveryOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: fixed-token non-byte-safe simple patterns verify scalar-aware on
        // astral-free non-ASCII input: ASCII search literals stay sound candidates
        // through UTF-8 self-synchronization while dots and ASCII-closed classes
        // consume whole scalars, so IsMatch and Count never construct the fallback
        // engine. Enumeration and shaping keep their existing BCL routes.
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

    [Theory]
    [MemberData(nameof(FoldedScalarDiscoveryOwnedCases))]
    public void ScalarDiscoveryOwnsFoldedNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: IgnoreCase scalar verification folds through the owned invariant
        // table (ASCII bytes fold inline, other BMP scalars meet the folded ASCII
        // mask through their fold pair, Kelvin included; shorthands and dots are
        // fold-neutral), so folded IsMatch and Count never construct the fallback
        // engine on astral-free non-ASCII input. Astral inputs keep BCL routing.
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
        // R05: light Match projects the scalar-verified first hit through the
        // boundary map, so group-0 coordinates never construct the fallback engine.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.Match(input);

        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Length, actual.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: groupless detailed matches shape group 0 from the owned MatchCore
        // hit, so the anchored verification route is bypassed entirely.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);

        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        if (expected.Success)
        {
            Assert.Equal(expected.Groups[0].Index, actual.GetGroup(0).IndexInUtf16);
            Assert.Equal(expected.Groups[0].Length, actual.GetGroup(0).LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: enumeration projects every scalar-verified hit through the boundary
        // map, so coordinates never construct the fallback engine. Grouped shapes
        // enumerate values only; their shaping keeps existing routes.
        const int count = int.MaxValue;
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

        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }

        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: folded enumeration projects every scalar-verified hit through the
        // boundary map exactly like the case-sensitive battery above, so folded
        // coordinates and splits never construct the fallback engine either.
        const int count = int.MaxValue;
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

        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }

        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void ScalarGrouplessTemplateOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: groupless templates expand from the owned enumeration above through
        // the shared groupless worker, with no group machinery involved.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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

    [Theory]
    [MemberData(nameof(FoldedScalarTemplateOwnedCases))]
    public void FoldedScalarTemplateOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: folded groupless templates expand from the owned enumeration above
        // through the shared groupless worker; expansion copies matched text and
        // is fold-neutral.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> ScalarGrouplessEvaluatorOwnedCases()
    {
        string[] patterns = ["a.c", "a[^x]d", ".[0-9]"];
        string[] texts = ["éabcé", "xyz"];
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
    [MemberData(nameof(ScalarGrouplessEvaluatorOwnedCases))]
    public void ScalarGrouplessEvaluatorOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: groupless evaluator callbacks observe the same owned enumeration
        // through the shared groupless arm on both cores.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedScalarEvaluatorOwnedCases()
    {
        string[] patterns = ["(?i)a.c", "(?i)a[^x]d"];
        string[] texts = ["éABCé", "xyz"];
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
    [MemberData(nameof(FoldedScalarEvaluatorOwnedCases))]
    public void FoldedScalarEvaluatorOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: folded groupless evaluator callbacks observe the same owned
        // enumeration through the shared groupless arm on both cores.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void DigitDiscoveryMatchDetailedOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: decimal-digit shorthands verify through the owned Nd table on
        // astral-free non-ASCII input, so IsMatch, Count, light Match, and
        // groupless Detailed never construct the fallback engine.
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
        Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups[0].Index, actualDetailed.GetGroup(0).IndexInUtf16);
            Assert.Equal(expectedDetailed.Groups[0].Length, actualDetailed.GetGroup(0).LengthInUtf16);
        }

        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(DigitDiscoveryMatchDetailedCases))]
    public void DigitEnumerationSplitOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: the scalar cursor projects every Nd-verified hit, driving
        // enumeration and splits owned with exact coordinates.
        const int count = int.MaxValue;
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

        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }

        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void DigitTemplateStringCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: groupless Nd templates expand owned on the string core through the
        // shared groupless worker over the scalar enumeration.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(DigitTemplateCases))]
    public void DigitTemplateByteCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // Control: the byte core rides the same groupless worker; these rows pin
        // the shared behavior while the string and destination cores graduate.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = Encoding.UTF8.GetString(regex.Replace(input, template));

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(DigitTemplateCases))]
    public void DigitTemplateDestinationCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: the destination core serves the same groupless Nd templates owned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var destination = new byte[4096];
        var status = regex.TryReplace(input, template, destination, out var bytesWritten);

        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, bytesWritten)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> DigitEvaluatorCases()
    {
        (string pattern, string text)[] rows =
        [
            ("\\d{2,4}", "é12é"),
            ("\\d{2,4}", "xyz"),
            ("a\\db", "éa1bé"),
            ("a\\db", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(DigitEvaluatorCases))]
    public void DigitEvaluatorOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: groupless evaluator callbacks observe the scalar enumeration on
        // both cores without fallback construction.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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

    [Theory]
    [MemberData(nameof(WordSpaceDiscoveryMatchDetailedCases))]
    public void WordSpaceDiscoveryMatchDetailedOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: whitespace and word shorthands verify through the owned tables on
        // astral-free non-ASCII input, so IsMatch, Count, light Match, and
        // groupless Detailed never construct the fallback engine.
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
        Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups[0].Index, actualDetailed.GetGroup(0).IndexInUtf16);
            Assert.Equal(expectedDetailed.Groups[0].Length, actualDetailed.GetGroup(0).LengthInUtf16);
        }

        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(WordSpaceDiscoveryMatchDetailedCases))]
    public void WordSpaceEnumerationSplitOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: the scalar cursor projects every table-verified hit, driving
        // enumeration and splits owned with exact coordinates.
        const int count = int.MaxValue;
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

        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }

        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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

    [Theory]
    [MemberData(nameof(WordSpaceTemplateCases))]
    public void WordSpaceTemplateStringCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: groupless shorthand templates expand owned on the string core.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(WordSpaceTemplateCases))]
    public void WordSpaceTemplateByteCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // Control: the byte core rides the same groupless worker.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = Encoding.UTF8.GetString(regex.Replace(input, template));

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(WordSpaceTemplateCases))]
    public void WordSpaceTemplateDestinationCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: the destination core serves the same groupless templates owned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var destination = new byte[4096];
        var status = regex.TryReplace(input, template, destination, out var bytesWritten);

        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, bytesWritten)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> WordSpaceEvaluatorCases()
    {
        (string pattern, string text)[] rows =
        [
            ("a\\sb", "xa bx"),
            ("a\\sb", "xyz"),
            ("a\\wb", "xaébx"),
            ("a\\wb", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(WordSpaceEvaluatorCases))]
    public void WordSpaceEvaluatorOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: groupless evaluator callbacks observe the scalar enumeration on
        // both cores without fallback construction.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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

    [Theory]
    [MemberData(nameof(NegatedShorthandDiscoveryMatchDetailedCases))]
    public void NegatedShorthandDiscoveryMatchDetailedOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: negated shorthands complement exactly through their table on
        // astral-free non-ASCII input, so IsMatch, Count, light Match, and
        // groupless Detailed never construct the fallback engine.
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
        Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups[0].Index, actualDetailed.GetGroup(0).IndexInUtf16);
            Assert.Equal(expectedDetailed.Groups[0].Length, actualDetailed.GetGroup(0).LengthInUtf16);
        }

        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(NegatedShorthandDiscoveryMatchDetailedCases))]
    public void NegatedShorthandEnumerationSplitOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: the scalar cursor projects every complemented hit, driving
        // enumeration and splits owned with exact coordinates.
        const int count = int.MaxValue;
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

        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }

        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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

    [Theory]
    [MemberData(nameof(NegatedShorthandTemplateCases))]
    public void NegatedShorthandTemplateStringCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: groupless complemented templates expand owned on the string core.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(NegatedShorthandTemplateCases))]
    public void NegatedShorthandTemplateByteCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // Control: the byte core rides the same groupless worker.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = Encoding.UTF8.GetString(regex.Replace(input, template));

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(NegatedShorthandTemplateCases))]
    public void NegatedShorthandTemplateDestinationCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: the destination core serves the same complemented templates owned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var destination = new byte[4096];
        var status = regex.TryReplace(input, template, destination, out var bytesWritten);

        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, bytesWritten)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> NegatedShorthandEvaluatorCases()
    {
        (string pattern, string text)[] rows =
        [
            ("[^\\d]", "éabé"),
            ("[^\\d]", "١٢٣"),
            ("\\D", "éabé"),
            ("\\D", "١٢٣"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(NegatedShorthandEvaluatorCases))]
    public void NegatedShorthandEvaluatorOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: groupless evaluator callbacks observe the complemented enumeration
        // on both cores without fallback construction.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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

    [Theory]
    [MemberData(nameof(DoublyNegatedShorthandOwnedCases))]
    public void DoublyNegatedShorthandOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: pure doubly-negated shorthands lower as their positive category, so
        // IsMatch and Count never construct the fallback engine on astral-free
        // non-ASCII input; the existing digit/word/whitespace arms serve them.
        // Mixed payloads keep BCL routing; astral discovery is unit-owned.
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

    [Theory]
    [MemberData(nameof(SinglelineDotDiscoveryMatchDetailedCases))]
    public void SinglelineDotDiscoveryMatchDetailedOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: singleline dots match any scalar including line feeds on astral-free
        // non-ASCII input, so IsMatch, Count, light Match, and groupless Detailed
        // never construct the fallback engine.
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
        Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups[0].Index, actualDetailed.GetGroup(0).IndexInUtf16);
            Assert.Equal(expectedDetailed.Groups[0].Length, actualDetailed.GetGroup(0).LengthInUtf16);
        }

        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(SinglelineDotDiscoveryMatchDetailedCases))]
    public void SinglelineDotEnumerationSplitOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: the scalar cursor projects every dot-verified hit, driving
        // enumeration and splits owned with exact coordinates.
        const int count = int.MaxValue;
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

        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expectedSplits.Add((text[start..end], start, end - start));
        }

        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, count))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expectedSplits, actualSplits);
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    [MemberData(nameof(SinglelineDotTemplateCases))]
    public void SinglelineDotTemplateStringCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: groupless singleline templates expand owned on the string core.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = regex.ReplaceToString(input, template);

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(SinglelineDotTemplateCases))]
    public void SinglelineDotTemplateByteCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // Control: the byte core rides the same groupless worker.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var actual = Encoding.UTF8.GetString(regex.Replace(input, template));

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    [Theory]
    [MemberData(nameof(SinglelineDotTemplateCases))]
    public void SinglelineDotTemplateDestinationCoreOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        // R05: the destination core serves the same templates owned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expected = oracle.Replace(text, template);
        var destination = new byte[4096];
        var status = regex.TryReplace(input, template, destination, out var bytesWritten);

        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, bytesWritten)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SinglelineDotEvaluatorCases()
    {
        (string pattern, string text)[] rows =
        [
            ("(?s:a.c)", "éaécé"),
            ("(?s:a.c)", "xyz"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(SinglelineDotEvaluatorCases))]
    public void SinglelineDotEvaluatorOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        // R05: groupless evaluator callbacks observe the dot enumeration on both
        // cores without fallback construction.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");

        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);

        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
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
        // R05: scalar-verifiable shapes expand literal templates through the scalar cursor
        // on astral-free non-ASCII input, so byte, string, and destination cores never
        // construct the fallback engine. Astral inputs keep BCL routing.
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
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: negated Unicode categories behave exactly as their positive-category counterparts,
        // so discovery, projection, enumeration,
        // splits, templates, and evaluators never construct the fallback engine here.
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var splitRegex = new Utf8Regex(pattern, options);
        var expectedSplits = new System.Collections.Generic.List<string>();
        foreach (var range in oracle.EnumerateSplits(text, int.MaxValue))
        {
            expectedSplits.Add(range.Start.Value + ":" + range.End.Value + ":" + text[range]);
        }
        var actualSplits = new System.Collections.Generic.List<string>();
        foreach (var s in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add(s.IndexInUtf16 + ":" + (s.IndexInUtf16 + s.LengthInUtf16) + ":" + s.GetValueString());
        }
        Assert.Equal(expectedSplits, actualSplits);
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var templateRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "<$1>"), templateRegex.ReplaceToString(input, "<$1>"));
        Assert.False(templateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(templateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var evaluatorRegex = new Utf8Regex(pattern, options);
        var evaluatorActual = evaluatorRegex.ReplaceToString(input, 0, (in Utf8MatchContext context, ref int state) => "<" + context.GetGroup(0).GetValueString() + ">");
        Assert.Equal(oracle.Replace(text, static match => "<" + match.Value + ">"), evaluatorActual);
        Assert.False(evaluatorRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(evaluatorRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitDiscoveryOwnedCases()
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
    [MemberData(nameof(AstralUnitDiscoveryOwnedCases))]
    public void AstralUnitDiscoveryOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: astral inputs verify unit-aware over decoded units (dots and negations
        // consume halves exactly like BCL), so IsMatch and Count never construct the
        // fallback engine. Match projection, IgnoreCase plans, and anchored plans keep
        // their existing routes.
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
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitDiscoveryDeclineCases()
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
    [MemberData(nameof(AstralUnitDiscoveryDeclineCases))]
    public void AstralUnitDiscoveryDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: IgnoreCase plans and anchored plans keep BCL routing on
        // astral input; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitMatchOwnedCases()
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
    [MemberData(nameof(AstralUnitMatchOwnedCases))]
    public void AstralUnitMatchOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: astral first-matches project owned from unit coordinates through the
        // boundary map (half-involving hits shape without byte ranges, exactly like BCL),
        // and groupless detailed matches shape from the owned value. Match-form projection beyond light
        // matches stays on existing routes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
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
    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitEnumerationOwnedCases()
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
    [MemberData(nameof(AstralUnitEnumerationOwnedCases))]
    public void AstralUnitEnumerationOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: astral enumeration steps every unit through the shared unit verifier and
        // projects through the boundary map, so full sequences match BCL with zero fallback
        // construction on both lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatches(input))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitSplitOwnedCases()
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
    [MemberData(nameof(AstralUnitSplitOwnedCases))]
    public void AstralUnitSplitOwnsInput(string pattern, RegexOptions options, string text, int count)
    {
        // R05: astral splits shape unit-space through the unit match cursor, so full
        // split sequences match BCL with zero fallback construction on both lanes.
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
    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitLiteralTemplateOwnedCases()
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
    [MemberData(nameof(AstralUnitLiteralTemplateOwnedCases))]
    public void AstralUnitLiteralTemplateOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        // R05: astral literal templates splice unit-aware over decoded units, so byte,
        // string, and destination cores never construct the fallback engine here.
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
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitLiteralTemplateDeclineCases()
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
    [MemberData(nameof(AstralUnitLiteralTemplateDeclineCases))]
    public void AstralUnitLiteralTemplateDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: IgnoreCase plans and anchored plans keep BCL routing for
        // astral literal templates; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);
        var bytesRegex = new Utf8Regex(pattern, options);
        Assert.Equal(expected, Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template))));
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitGroupTemplateOwnedCases()
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
    [MemberData(nameof(AstralUnitGroupTemplateOwnedCases))]
    public void AstralUnitGroupTemplateOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: astral group templates shape single-wrap and groupless groups from owned
        // unit discovery, so byte, string, destination, and detailed paths never construct
        // the fallback engine here.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, "<$1>");
        var bytesRegex = new Utf8Regex(pattern, options);
        // Byte-level oracle: lone surrogates cannot exist in well-formed UTF-8 bytes,
        // so both sides encode (BCL substitutes identically through the same encoder).
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes("<$1>"));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, "<$1>");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        // Group templates can expand output past the input length on multi-hit rows.
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, "<$1>", destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups[1].Value, actualDetailed.GetGroup(1).GetValueString());
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitBranchOwnedCases()
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
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralUnitBranchOwnedCases))]
    public void AstralUnitBranchOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        // R05: nested divergent simple patterns with branch-invariant unit spans shape
        // every group from static offsets onto the owned unit-space hit, so light Match,
        // Detailed, enumeration, and all three replacement cores never construct the
        // fallback engine here. Byte-level lanes compare encoded bytes because lone
        // halves substitute identically through the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitBranchDeclineCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("((a|.)c)", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "a\U0001F600c", "<$1>"),
            ("((x)|(.))", RegexOptions.CultureInvariant, "x", "<$2>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AstralUnitBranchDeclineCases))]
    public void AstralUnitBranchDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: ambiguous branch attribution (several branches verify
        // the hit) keeps BCL routing, while right-to-left and IgnoreCase unit shapes
        // keep their existing BCL routes; parity holds and construction is pinned
        // per lane. Single-present-branch shapes such as (a|(.)c) graduate to the
        // owned battery above: exactly one branch verifies their hits.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, template), stringRegex.ReplaceToString(input, template));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitBranchEvaluatorDeclineCases()
    {
        (string pattern, string text)[] rows =
        [
            ("((a|.)c)", "a\U0001F600c"),
            ("((a|.)c)", "xabc"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(AstralUnitBranchEvaluatorDeclineCases))]
    public void AstralUnitBranchEvaluatorDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: evaluator callbacks keep BCL shaping for nested
        // divergent shapes (half-involving hits would throw under the byte-splicing
        // evaluator contract instead of shaping like BCL); parity holds and
        // construction is pinned per lane. FromOffset detailed matches graduate to
        // the owned battery below.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var evaluatorRegex = new Utf8Regex(pattern, options);
        var evaluatorActual = evaluatorRegex.ReplaceToString(input, 0, (in Utf8MatchContext context, ref int state) => "<" + context.GetGroup(1).GetValueString() + ">");
        Assert.Equal(oracle.Replace(text, static match => "<" + match.Groups[1].Value + ">"), evaluatorActual);
        Assert.True(evaluatorRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitFromOffsetOwnedCases()
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
    [MemberData(nameof(AstralUnitFromOffsetOwnedCases))]
    public void AstralUnitFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        // R05: unit-served discovery and shaping run unit-aware from the start unit
        // on astral input, so IsMatch, Count, Match, and Detailed from a UTF-16 offset
        // never construct the fallback engine here. Counts compare against BCL
        // start-at semantics, and Detailed checks every group including halves.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.IsMatchFromUtf16Offset(input, offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, countRegex.CountFromUtf16Offset(input, offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, offset);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitFromOffsetDeclineCases()
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
    [MemberData(nameof(AstralUnitFromOffsetDeclineCases))]
    public void AstralUnitFromOffsetDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, int offset)
    {
        // Declared R05 debt: right-to-left and IgnoreCase unit shapes keep their
        // existing BCL routes (right-to-left search finds different winners), and
        // astral-free inputs stay on existing routing since unit serving needs
        // astral leads; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.IsMatchFromUtf16Offset(input, offset));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, countRegex.CountFromUtf16Offset(input, offset));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, offset);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups[1].Value, actualDetailed.GetGroup(1).GetValueString());
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
    }


    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitFromOffsetEnumOwnedCases()
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
    [MemberData(nameof(AstralUnitFromOffsetEnumOwnedCases))]
    public void AstralUnitFromOffsetEnumOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        // R05: unit-served shapes enumerate unit-aware from the start unit on
        // astral input, bypassing the byte-offset routing whose base-offset
        // projection throws on half-involving hits; full start-at sequences match
        // BCL with zero fallback construction on both lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatchesFromUtf16Offset(input, offset))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AstralUnitFromOffsetEnumDeclineCases()
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
    [MemberData(nameof(AstralUnitFromOffsetEnumDeclineCases))]
    public void AstralUnitFromOffsetEnumDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, int offset)
    {
        // Declared R05 debt: right-to-left and IgnoreCase unit shapes keep their
        // existing BCL routes from nonzero starts; parity holds and construction
        // is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatchesFromUtf16Offset(input, offset))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: whole-pattern greedy or lazy unbounded loops over unit-closed atoms
        // and word-boundary runs (positive edges, plus negated edges positioned
        // away from scalar halves) run scalarwise exactly like BCL units, so discovery,
        // Alternation-of-loops joins this routing (iterations project without the fallback engine).
        // Unbounded divergent loops project ordered first-success spans here (greedy and lazy, including the longest-divergent text).
        // Atomic divergent bodies join this routing (per-iteration commit steps exactly like BCL units, hits and misses).
        // Nested-plus loops join this routing (inner groups project last-iteration wins without the fallback engine).
        // shaping, splits, and
        // all three replacement cores never construct the fallback engine here.
        // Byte-level lanes compare encoded bytes because lone halves substitute
        // identically through the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void AstralWholeLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: concat tails after divergent atoms, tailless lazy loops,
        // nested captures, positive shorthands, and inner captures keep BCL
        // routing on astral input; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, template), stringRegex.ReplaceToString(input, template));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
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
    public void TaillessLazyAstralEnumerateDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: tailless lazy loops over astral-consuming atoms
        // stay BCL-routed on astral input because the oracle matches lone halves
        // ([0:1,1:1,2:1,3:1] on a 4-unit input), which scalar stepping cannot
        // reproduce; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in regex.EnumerateMatches(input))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
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
    public void TaillessLazyAstralDeclineKeepsBclRouting(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: tailless lazy loops over astral-consuming atoms stay
        // BCL-routed on every lane on astral input: the oracle matches lone halves
        // (probed count 4 on `a<astral>b`: `a`, high half, low half, `b`), which scalar
        // stepping cannot reproduce, so unlike headed lazy loops no verifier-owned
        // discovery applies here; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
            public static System.Collections.Generic.IEnumerable<object?[]> WordBoundaryLoopDeclineCases()
            {
                (string pattern, string text, bool verifierDiscovery)[] rows =
                [
                    ("a.+\\B", "xa\U0001F600b", true),
                    ("\\B.+\\B", "xa\U0001F600b", false),
                ];
                foreach (var row in rows)
                {
                    yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.verifierDiscovery };
                    yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.verifierDiscovery };
                }
            }

            [Theory]
            [MemberData(nameof(WordBoundaryLoopDeclineCases))]
            public void WordBoundaryLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, bool verifierDiscovery)
            {
                // Declared R05 debt: negated word edges meet mid-scalar (both halves
                // are non-word, so BCL holds them where scalar stepping cannot follow);
                // headed shapes keep verifier-owned discovery on the ordinary lane
                // (the compiled lane diverts every discovery op to BCL) while bare
                // shapes go BCL everywhere;
                // shaping and materialization keep BCL routing in both cases;
                // parity holds and construction is pinned per lane.
                var compiled = (options & RegexOptions.Compiled) != 0;
                var input = Encoding.UTF8.GetBytes(text);
                var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
                var isMatchRegex = new Utf8Regex(pattern, options);
                Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
                if (verifierDiscovery)
                {
                    Assert.Equal(compiled, isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
                    if (!compiled)
                    {
                        Assert.True(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
                    }
                }
                else
                {
                    Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
                }
                var countRegex = new Utf8Regex(pattern, options);
                Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
                if (verifierDiscovery)
                {
                    Assert.Equal(compiled, countRegex.Inspection.DebugFallbackRegexIsCreated);
                    if (!compiled)
                    {
                        Assert.True(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
                    }
                }
                else
                {
                    Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
                }
                var matchRegex = new Utf8Regex(pattern, options);
                var expectedMatch = oracle.Match(text);
                var actualMatch = matchRegex.Match(input);
                Assert.Equal(expectedMatch.Success, actualMatch.Success);
                if (expectedMatch.Success)
                {
                    Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                    Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
                }
                if (verifierDiscovery)
                {
                    Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
                    if (!compiled)
                    {
                        Assert.True(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
                    }
                }
                else
                {
                    Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
                }
                var detailedRegex = new Utf8Regex(pattern, options);
                var expectedDetailed = oracle.Match(text);
                var actualDetailed = detailedRegex.MatchDetailed(input);
                Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
                Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
                Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
                Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
                var expected = oracle.Replace(text, "Q");
                var stringRegex = new Utf8Regex(pattern, options);
                Assert.Equal(expected, stringRegex.ReplaceToString(input, "Q"));
                Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: folded whole-pattern greedy unbounded loops over unit-closed
        // atoms run scalarwise exactly like BCL units (folding only refines
        // which BMP scalars meet ASCII atoms; halves still miss positives
        // and meet negations), so discovery, shaping, splits, and all three
        // replacement cores never construct the fallback engine here.
        // Byte-level lanes compare encoded bytes because lone halves substitute
        // identically through the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
            ("(?i)a\\p{L}{2,}b", RegexOptions.CultureInvariant, "xa\U00010400b"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(FoldedWholeLoopDeclineCases))]
    public void FoldedWholeLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: divergent branches, tailless lazy loops, inner captures,
        // positive shorthands and categories, capturing alternations, empty-capable bounds,
        // and right-to-left folded loops keep BCL routing on astral input;
        // parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: whole-pattern greedy unbounded loops with ASCII head/tail runs
        // Headed lazy-plus category loops join this routing (min-one has no units-vs-scalars tension, hits and misses).
        // keep scalarwise stepping exactly like BCL units on astral input
        // (heads and tails, including lookbehind and lookahead heads and tails, only
        // ever meet ASCII bytes in both semantics while
        // the loop keeps its scalar-boundary start/stop argument), so
        // discovery, shaping, splits, and all three replacement cores never
        // construct the fallback engine here. Byte-level lanes compare
        // encoded bytes because lone halves substitute identically through
        // the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: headed lazy-plus loops complete the shorthand atom table on ASCII
        // input: every atom (positive, negated, and property shorthands plus the
        // negated set) serves all nine lanes fully owned on both lanes, hits and
        // misses alike. The astral variants of the covered atoms ride the
        // TailedWholeLoopOwnedCases battery; astral \W keeps its word-group
        // verifier contract and \s shaping diverts (see the space decline below).
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> TailedLazySpaceShapingDeclineCases()
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
    [MemberData(nameof(TailedLazySpaceShapingDeclineCases))]
    public void TailedLazySpaceShapingKeepsBclRouting(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: the whitespace lazy-plus serves discovery owned
        // through the ordered-literal-window lane but keeps BCL routing for
        // shaping and materialization on both lanes. Parity holds and
        // construction is pinned per lane.
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
            ("\\D{2,}b", RegexOptions.CultureInvariant, "x\U0001F600b"),
            ("[^x]{2,}?b", RegexOptions.CultureInvariant, "x\U0001F600b"),
            ("\\D{2,}?b", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            (".{2,}?b", RegexOptions.CultureInvariant, "x\U0001F600b"),
            ("(?i)A[^x]{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            ("(?i)A\\D{2,}?B", RegexOptions.CultureInvariant, "xa\U0001F600b"),
            ("(?i)A\\D{2,}B", RegexOptions.CultureInvariant, "xa\U0001F600b"),
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
    public void TailedWholeLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: divergent loop bodies with tails, empty-capable
        // and lazy tailed loops (including lazy ranges with no head to anchor verifier
        // search), positive-shorthand, negated-shorthand
        // min-bound, inner-capture, and lookaround-anchored tailed loops (astral-meeting
        // bodies or bounds keep scalar stepping from following BCL), anchored or directed
        // (dot-headed, dot-tailed) runs, nested
        // tails inside alternation branches, and right-to-left tailed loops
        // keep BCL routing on astral input; parity holds and construction is
        // pinned per lane. Dot-tailed ASCII-closed runs keep verifier-owned or
        // owned discovery with BCL shaping; see DotTailedLoopDeclineCases.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
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
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.False(isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(0, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        Assert.False(matchRegex.Match(input).Success);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.False(isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(0, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        Assert.False(matchRegex.Match(input).Success);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void DotTailedLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: a dot tail over an ASCII-closed loop keeps BCL
        // routing for shaping and materialization on astral input (the tail
        // atom can meet either half of a scalar in general, so the shape gate
        // stays pattern-based), while discovery stays verifier-owned on the
        // ordinary lane (anchored verifier, no plain fallback) and fully owned
        // for IsMatch and Count on the compiled lane (compiled Match still
        // diverts); parity holds and construction is pinned per lane.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.Equal(!compiled, isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.Equal(!compiled, countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, "Q");
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes("Q"));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, "Q");
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, "Q", destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: whole-pattern greedy or lazy unbounded loops with line, absolute,
        // or contiguous anchors keep scalarwise stepping exactly like BCL units
        // on astral
        // input (anchors only ever meet ASCII-determined positions in both
        // semantics while the loop keeps its scalar-boundary argument), so
        // discovery, shaping, splits, and all three replacement cores never
        // construct the fallback engine here. Byte-level lanes compare
        // encoded bytes because lone halves substitute identically through
        // the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredLoopDeclineCases()
    {
        (string pattern, RegexOptions options, string text)[] rows =
        [
            ("^a[^x]*b$", RegexOptions.CultureInvariant, "a\U0001F600b"),
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
    public void AnchoredLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: word-boundary anchors, empty-capable anchored
        // loops, and right-to-left anchored loops keep BCL routing on
        // astral input; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredLoopAsciiOwnedCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("^a[^x]*b$", RegexOptions.CultureInvariant, "axaayb", "Q"),
            ("^a[^x]*b$", RegexOptions.CultureInvariant, "ab", "Q"),
            ("^a[^x]+b$", RegexOptions.CultureInvariant, "axaayb", "Q"),
            ("(?<=x)a+", RegexOptions.CultureInvariant, "xaay", "Q"),
            ("a+(?=y)", RegexOptions.CultureInvariant, "xaay", "Q"),
            ("\\Ba+\\B", RegexOptions.CultureInvariant, "xaay", "Q"),
            ("\\ba+\\b", RegexOptions.CultureInvariant, "xaay", "Q"),
            ("^a[^x]+b", RegexOptions.CultureInvariant | RegexOptions.RightToLeft, "axaayb", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(AnchoredLoopAsciiOwnedCases))]
    public void AnchoredLoopAsciiOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        // R05: anchored loops serve fully owned on ASCII input across all nine
        // lanes of both lanes: doubly-anchored empty-capable loops (including
        // the empty iteration), lookbehind- and lookahead-anchored loops,
        // non-word-boundary loops, word-boundary loops, and right-to-left
        // anchored loops. Misses and hits alike construct no fallback engine;
        // the astral fence is input-gated (see AnchoredLoopAstralDeclineCases).
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> AnchoredLoopAstralDeclineCases()
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
    [MemberData(nameof(AnchoredLoopAstralDeclineCases))]
    public void AnchoredLoopAstralDeclineKeepsBclRouting(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: doubly-anchored loops keep BCL routing on astral
        // input on every lane of both lanes (the existing AnchoredLoopDecline
        // pin covers IsMatch plus string replacement; this battery extends the
        // same row to all nine lanes). The fence is input-gated like the
        // directed runs. Parity holds and construction is pinned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: whole-pattern, tailed, and anchored loops serve every FromOffset
        // lane owned from boundary and half starts on astral input (the byte
        // runner mirrors BCL unit semantics at half positions exactly like it
        // does from scalar starts); full start-at sequences match BCL with
        // zero fallback construction on both lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.IsMatchFromUtf16Offset(input, offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, countRegex.CountFromUtf16Offset(input, offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, offset);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var enumerateRegex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatchesFromUtf16Offset(input, offset))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
            ("[^x]*", RegexOptions.CultureInvariant, "ax\U0001F600b", 0),
            ("[^x]*", RegexOptions.CultureInvariant, "ax\U0001F600b", 2),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.offset };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.offset };
        }
    }

    [Theory]
    [MemberData(nameof(WholeLoopFromOffsetDeclineCases))]
    public void WholeLoopFromOffsetDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, int offset)
    {
        // Declared R05 debt: tailless lazy, inner-capture, right-to-left, and astral-flagged
        // empty loops keep BCL routing on astral input from boundary and half
        // starts alike;
        // parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.IsMatchFromUtf16Offset(input, offset));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, countRegex.CountFromUtf16Offset(input, offset));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: empty-capable loops serve every FromOffset lane owned from
        // boundary and half starts on astral input (empty matches advance one
        // unit exactly like BCL, halves included); full start-at sequences
        // match BCL with zero fallback construction on both lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.IsMatchFromUtf16Offset(input, offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, countRegex.CountFromUtf16Offset(input, offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, offset);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var enumerateRegex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatchesFromUtf16Offset(input, offset))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> WholeLoopEvaluatorOwnedCases()
    {
        (string pattern, string text)[] rows =
        [
            ("[^x]+", "x\U0001F600y"),
            ("a[^x]+b", "xa\U0001F600bxb"),
            (".+b", "\U0001F600b"),
            ("^a[^x]+b$", "a\U0001F600b"),
            ("(?i)[^x]+B", "x\U0001F600B"),
            ("(a[^x]+b)", "xa\U0001F600b"),
            ("[\\p{L}_]+", "a_b\U0001F600c"),
            ("(?>[^x]+)", "a\U0001F600b"),
            ("a.+b", "xa\U0001F600b"),
            ("a.+?b", "xa\U0001F600b"),
            ("\\ba[^x]+b\\b", "x a\U0001F600b!"),
            ("\\Ga[^x]+b", "a\U0001F600b"),
            ("(a|ab)+", "xaab\U0001F600ab"),
            ("(a+?)", "xaay\U0001F600"),
            ("([0-9]{2,})", "x12\U0001F600"),
            ("(?>a+?)", "xaay\U0001F600"),
            ("\\ba+\\b", "xaay\U0001F600"),
            ("((a|b)c)+", "xacbc\U0001F600"),
            ("\\d+", "x12\U0001F600"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text };
        }
    }

    [Theory]
    [MemberData(nameof(WholeLoopEvaluatorOwnedCases))]
    public void WholeLoopEvaluatorOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: whole-pattern, tailed, and anchored loops project every
        // callback argument from the deterministic owned re-query at each
        // enumerated hit start on astral input, so both evaluator cores serve
        // owned with zero fallback construction on both lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");
        var byteRegex = new Utf8Regex(pattern, options);
        var byteReplaced = byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        });
        Assert.Equal(expected, Encoding.UTF8.GetString(byteReplaced));
        Assert.False(byteRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(byteRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: empty-capable slice loops tile astral input unit-wise exactly
        // like BCL (empty matches at half gaps included, enumerated identically
        // on every discovery lane), so the owned splicer reproduces BCL
        // replacement byte-for-byte and all four replacement cores plus splits
        // never construct the fallback engine here. Byte-level lanes compare
        // encoded bytes because lone halves substitute identically through
        // the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, template);
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var utf8Regex = new Utf8Regex(pattern, options);
        var utf8Actual = utf8Regex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, utf8Actual);
        Assert.False(utf8Regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(utf8Regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableReplaceDeclineCases()
    {
        (string pattern, RegexOptions options, string text, string template)[] rows =
        [
            ("[^x]*", RegexOptions.CultureInvariant, "a\U0001F600b", "Q"),
            ("(.*)", RegexOptions.CultureInvariant, "\U0001F600", "<$1>"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, row.options, row.text, row.template };
            yield return new object?[] { row.pattern, row.options | RegexOptions.Compiled, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(EmptyCapableReplaceDeclineCases))]
    public void EmptyCapableReplaceDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: negated-dotted empty loops keep the astral flag, so all
        // four replacement cores keep BCL routing on astral input; parity
        // holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(expected, stringRegex.ReplaceToString(input, template));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var bytesRegex = new Utf8Regex(pattern, options);
        Assert.Equal(expectedBytes, bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: anchored and bounded empty-capable loops ride the same unit-wise
        // tiling as the bare loops (anchors pin ASCII-determined positions,
        // bounds only cap iterations), so the three bytes replacement cores
        // and splits serve owned with zero fallback construction on both
        // lanes; the string lane stays BCL-backed and is pinned separately.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var programRegex = new Utf8Regex(pattern, options);
        Assert.True(programRegex.Inspection.DebugUsesOwnedSliceProgram);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, template);
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var utf8Regex = new Utf8Regex(pattern, options);
        var utf8Actual = utf8Regex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, utf8Actual);
        Assert.False(utf8Regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(utf8Regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: empty-capable slice loops over ASCII-closed atoms tile astral input
        // unit-wise exactly like BCL on discovery lanes (empty matches at half gaps
        // included), so IsMatch, Count, light Match, detailed Match, and enumeration
        // never construct the fallback engine here. Replacement and evaluator lanes
        // keep their own batteries; the string-template lane diverts per the
        // lone-halves well-formedness rule below.
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        _ = template;
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
    public void EmptyCapableStringDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: the string replacement lane cannot reproduce lone
        // halves under well-formedness (BCL keeps them, owned substitutes
        // U+FFFD), so it keeps BCL routing for empty-capable programs on
        // astral input; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, template), stringRegex.ReplaceToString(input, template));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> EmptyCapableEvaluatorAlignedCases()
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
    [MemberData(nameof(EmptyCapableEvaluatorAlignedCases))]
    public void EmptyCapableEvaluatorRequiresAlignedMatches(string pattern, RegexOptions options, string text)
    {
        // Declared evaluator contract: the bytes core splices raw input bytes
        // around each match, so half-gap empty matches (no scalar boundaries)
        // fail closed instead of invoking callbacks there or splicing split
        // bytes around them. The string core serves the same shapes owned
        // below; aligned evaluator coverage lives in the whole-loop battery.
        var input = Encoding.UTF8.GetBytes(text);
        var byteRegex = new Utf8Regex(pattern, options);
        Assert.Throws<System.InvalidOperationException>(() => byteRegex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        }));
    }

    [Theory]
    [MemberData(nameof(EmptyCapableEvaluatorAlignedCases))]
    public void EmptyCapableStringEvaluatorOwnsInput(string pattern, RegexOptions options, string text)
    {
        // R05: the string evaluator core shapes every callback argument from
        // decoded substrings (lone halves preserved exactly like BCL) and
        // splices in string space, so empty-capable loops serve owned with
        // zero fallback construction on both lanes even on astral input.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, static match => "[" + match.Value + "]");
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]");
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: bounded repetitions with nonzero minima step scalarwise exactly
        // like BCL units on astral input (bounds only cap iterations of the
        // same unit-closed atoms, shorthand tables included: halves miss every
        // positive table on both sides), so discovery, shaping, splits, and
        // all four replacement cores never construct the fallback engine here.
        // Byte-level lanes compare encoded bytes because lone halves substitute
        // identically through the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        // Bounded repeats with nonzero minima route to dedicated owned families
        // (literal-family kernels here), not the slice, so there is no slice
        // program to assert; the lanes below pin owned routing with parity.
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, template);
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var utf8Regex = new Utf8Regex(pattern, options);
        var utf8Actual = utf8Regex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, utf8Actual);
        Assert.False(utf8Regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(utf8Regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: bounded repetitions with zero minima tile astral input unit-wise
        // exactly like BCL (empty matches at half gaps included, enumerated
        // identically on every discovery lane), so discovery, splits, and the
        // three bytes replacement cores serve owned with zero fallback
        // construction on both lanes; the string lane stays BCL-backed and is
        // pinned with the other empty-capable string rows.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, template);
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var utf8Regex = new Utf8Regex(pattern, options);
        var utf8Actual = utf8Regex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, utf8Actual);
        Assert.False(utf8Regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(utf8Regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: whole-pattern greedy or lazy unbounded loops over positive
        // categories (and negations whose positive side misses halves) run
        // scalarwise exactly like BCL units on astral input (the runner fails
        // astral leads closed while BCL halves miss every positive table, and
        // consumes whole scalars where BCL halves match the negation, while
        // BMP matching already agrees exactly), so discovery, shaping, splits,
        // Headed loops whose tables consume whole astral scalars mid-span join this routing (probed parity-clean on every lane).
        // and all three replacement cores never construct the fallback engine
        // here. Byte-level lanes compare encoded bytes because lone halves
        // substitute identically through the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void PositiveCategoryLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: surrogate-covering categories in either polarity
        // (BCL halves meet positive Cs tables and miss negations while the runner
        // would consume whole scalars), mixed category sets, inner captures,
        // right-to-left, and tailless lazy category loops keep BCL routing on
        // astral input on every lane; parity holds and construction is pinned
        // per lane. Half-missing negations serve owned through the loop battery below.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expectedBytes = Encoding.UTF8.GetBytes(oracle.Replace(text, "Q"));
        var bytesRegex = new Utf8Regex(pattern, options);
        Assert.Equal(expectedBytes, bytesRegex.Replace(input, Encoding.UTF8.GetBytes("Q")));
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, "Q", destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: positive-category and positive-hybrid loops compose with the
        // already-owned head/tail runs (literals, anchors) through the same
        // indifference rule, so discovery, shaping, splits, and all three
        // replacement cores never construct the fallback engine here.
        // Byte-level lanes compare encoded bytes because lone halves substitute
        // identically through the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: whole-pattern greedy or lazy unbounded loops over uniform
        // category sets run scalarwise exactly like BCL units on astral input
        // (the runner consumes exactly where halves match, either parity, and
        // misses where they miss), so discovery, shaping, splits, and all
        // three replacement cores never construct the fallback engine here.
        // Byte-level lanes compare encoded bytes because lone halves
        // substitute identically through the encoder on both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void PositiveCategorySetDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: split-half members (tables covering only lead or
        // only trail surrogates, e.g. surrogate blocks), double-negated
        // surrogate sets (the parser XOR-collapses them to surrogate-covering
        // positives), inner captures, right-to-left, and tailless lazy
        // category-set loops keep BCL routing on astral input; parity holds
        // and construction is pinned per lane. Uniform sets serve owned
        // through the loop battery below.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: backreference, atomic, and conditional shapes serve shaping owned
        // on ASCII input across both lanes: Detailed projects every group with
        // oracle coordinates and values, splits enumerate owned gaps, and all
        // three replacement cores shape from owned spans, covering both
        // conditional branches. Their R03 slice batteries pin discovery; this
        // battery pins the shaping lanes these exact rows lacked.
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: atomic groups are transparent to the unit/stepping argument
        // (atomicity only prunes backtracking, which whole-pattern greedy
        // loops never need on either side), so atomic-wrapped loops serve
        // every lane owned exactly like their bare shapes with zero fallback
        // construction on both lanes. Byte-level lanes compare encoded bytes
        // because lone halves substitute identically through the encoder on
        // both sides.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void AtomicLoopDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: divergent atomic bodies, atomic alternations,
        // tailless lazy atomic loops, and right-to-left atomic loops keep BCL routing
        // on astral input; parity holds and construction is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: dot and negated-newline gaps with ASCII literal heads
        // and tails route to the general slice (never the separator-window
        // family, whose gap model only fits whitespace runs), so discovery,
        // shaping, splits, and all replacement cores serve owned with zero
        // fallback construction on both lanes, newlines and all. Lazy gaps
        // join through first stops: minimum one is fenced and the tail pins
        // every stop at scalar boundaries, so lazy discovery and shaping
        // need no anchored verifier here.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: directed dot runs (leading-dot heads, trailing-dot tails, and
        // single dots) serve fully owned on ASCII input across all nine lanes
        // of both lanes: fixed directed shapes ride the AsciiSimplePattern
        // interpreter while looped directed shapes ride the general slice.
        // The astral fence is input-gated (see DirectedAstralDeclineCases), not
        // body-gated: ASCII-closed bodies divert as soon as any astral scalar
        // shares the input.
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void DirectedAstralDeclineKeepsBclRouting(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: directed dot runs keep BCL routing on astral input
        // on every lane of both lanes. The fence is input-gated: ASCII-closed
        // bodies divert as soon as any astral scalar shares the input, even when
        // no candidate touches it. Parity holds and construction is pinned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: leftover ASCII tracts from declined families serve fully owned on
        // all nine lanes of both lanes: nested tails in branches, folded headed
        // hits, word-group min-bound hits, word-boundary loops, and digit
        // min-bound hits. Their astral/folded-decline twins keep the recorded
        // pins; only the ASCII side graduates here.
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> SpaceMinBoundShapingDeclineCases()
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

    [Theory]
    [MemberData(nameof(SpaceMinBoundShapingDeclineCases))]
    public void SpaceMinBoundShapingKeepsBclRouting(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: the whitespace min-bound serves discovery owned
        // through the ordered-literal-window lane (like its lazy-plus twin in
        // TailedLazySpaceShapingDeclineCases) but keeps BCL routing for shaping
        // and materialization on both lanes. Parity holds and construction is
        // pinned per lane.
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> PositiveCategoryRangeAstralCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\p{L}{2,}b", "xa\U00010400b", "Q"),
            ("a\\p{Nd}{2,}b", "xa\U0001D7CE\U0001D7CEb", "Q"),
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
    public void PositiveCategoryRangeAstralKeepsVerifierDiscovery(string pattern, RegexOptions options, string text, string template)
    {
        // R05: positive-category ranges keep verifier-owned ordinary discovery
        // with BCL shaping on astral input, exactly like the unit-divergent
        // headed ranges: the anchored verifier is constructed on IsMatch and
        // Count of both lanes while the plain fallback shapes Detailed,
        // enumeration, splits, and all three replacement cores (compiled Match
        // diverts with it). The oracle misses single-astral gaps here because
        // positive categories never meet astral scalars; parity holds on every
        // lane.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.True(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.True(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: positive-category ranges serve fully owned on ASCII input across
        // all nine lanes of both lanes, hits and misses alike. The astral
        // single-letter miss joins this battery: positive categories never meet
        // astral scalars, so no candidate survives to verification and no
        // fallback is constructed on any lane.
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void AstralLiteralLoopDeclineKeepsBclRouting(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: astral-literal loops keep BCL routing on every lane
        // of both lanes. The R02a slice admits ASCII-only patterns by gate, so no
        // slice program is built for astral literals and every operation constructs
        // the fallback engine; extending the slice language past ASCII is engine
        // work, not a routing fix. Parity holds and construction is pinned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.False(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> MinBoundHeadedAstralCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a[^x]{2,}b", "xa\U0001F600b", "Q"),
            ("a.{2,}b", "xa\U0001F600b", "Q"),
            ("(a[^x]{2,}b)", "xa\U0001F600b", "<$1>"),
            ("(a[^x]{2,}b)", "xa\U0001F600yb", "<$1>"),
            ("a[^x]{2,}?b", "xa\U0001F600b", "Q"),
            ("a.{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\D{2,}b", "xa\U0001F600b", "Q"),
            ("(a\\D{2,}b)", "xa\U0001F600b", "<$1>"),
            ("a\\D{2,}?b", "xa\U0001F600b", "Q"),
            ("a.{2,5}?b", "xa\U0001F600b", "Q"),
            ("a[^x]{2,5}?b", "xa\U0001F600b", "Q"),
            ("a\\D{2,5}?b", "xa\U0001F600b", "Q"),
            ("a.*?b", "xa\U0001F600b", "Q"),
            ("a[^x]*?b", "xa\U0001F600b", "Q"),
            ("a\\D*?b", "xa\U0001F600b", "Q"),
            ("a.*b", "xa\U0001F600b", "Q"),
            ("a[^x]*b", "xa\U0001F600b", "Q"),
            ("a\\D*b", "xa\U0001F600b", "Q"),
            ("a\\D{2,}b", "xa\U0001D7CE\U0001D7CEb", "Q"),
            ("a\\S{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\S{2,}b", "xa\U0001F600b", "Q"),
            ("a\\P{Nd}{2,}?b", "xa\U0001F600b", "Q"),
            ("a\\P{Nd}{2,}b", "xa\U0001F600b", "Q"),
            ("a\\P{Lu}{2,}?b", "xa\U0001F600b", "Q"),
            ("a[\\s\\S]{2,}b", "xa\U0001F600b", "Q"),
            ("a[^\n]{2,}b", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(MinBoundHeadedAstralCases))]
    public void MinBoundHeadedAstralKeepsVerifierDiscovery(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: minimum bounds count UTF-16 units in BCL but
        // scalars in the slice runner, so a minimum of two or more over a
        // unit-divergent atom can force a pair split the runner cannot
        // reproduce (probed: `a[^x]{2,}b` misses `xa<astral>b` owned with no
        // fallback). Headed shapes keep verifier-owned discovery on astral
        // input (the ordinary lane builds the anchored verifier; compiled
        // Match diverts) while shaping and materialization keep BCL routing;
        // Min-zero lazy stars over the same atoms join this routing (probed parity-clean with a stable repeat: ordinary discovery keeps the anchored verifier, shaping keeps BCL routing).
        // Greedy min-zero stars probe identically on the same lanes with a stable repeat.
        // parity holds and construction is pinned per lane.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.Equal(!compiled, isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.Equal(!compiled, countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void HeadedGroupCategoryRangeAstralKeepsVerifierDiscovery(RegexOptions options)
    {
        // All-negated group lists (word-group and inverted property groups)
        // keep verifier-owned discovery on astral input like the other
        // negated-category ranges, but construction differs: every lane
        // builds the anchored verifier on IsMatch where single codes (and
        // the positive word group, probed) do not on the compiled lane.
        // The anchored Lazy is only ever evaluated inside verifier TryVerify
        // paths, which then execute it, so a created anchored regex witnesses
        // BCL verification on that operation; the plain Lazy is not such a
        // witness (construction paths pre-touch it). Replacement diverts to BCL
        // shaping with parity. Parity holds per lane.
        var compiled = (options & RegexOptions.Compiled) != 0;
        foreach (var pattern in new string[] { "a\\W{2,}?b", "a\\W{2,}b", "a\\P{L}{2,}?b", "a\\P{L}{2,}b" })
        {
            const string text = "xa\U0001F600b";
            var input = Encoding.UTF8.GetBytes(text);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var isMatchRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
            Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
            Assert.True(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
            var countRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
            Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
            Assert.True(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
            var matchRegex = new Utf8Regex(pattern, options);
            var expectedMatch = oracle.Match(text);
            var actualMatch = matchRegex.Match(input);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
            }
            Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
            var stringRegex = new Utf8Regex(pattern, options);
            Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
            Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
            var bytesRegex = new Utf8Regex(pattern, options);
            Assert.Equal(Encoding.UTF8.GetBytes(oracle.Replace(text, "Q")), bytesRegex.Replace(input, Encoding.UTF8.GetBytes("Q")));
            Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        }
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedGroupCategoryVerifierUseCases()
    {
        (string pattern, string text, bool expectUse)[] rows =
        [
            ("a\\W{2,}?b", "xa\U0001F600b", true),
            ("a\\W{2,}b", "xa\U0001F600b", true),
            ("a\\P{L}{2,}?b", "xa\U0001F600b", true),
            ("a\\P{L}{2,}b", "xa\U0001F600b", true),
            ("a\\W{2,}?b", "xa\U0001F600c", false),
            ("a\\P{L}{2,}b", "xa\U0001F600c", false),
            ("a\\P{L}{2,}?b", "xa12b", false),
            ("a\\W{2,}?b", "xa12b", false),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.None, row.text, row.expectUse };
            yield return new object?[] { row.pattern, RegexOptions.Compiled, row.text, row.expectUse };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedGroupCategoryVerifierUseCases))]
    public void HeadedGroupCategoryVerifierUseMatchesConstruction(string pattern, RegexOptions options, string text, bool expectUse)
    {
        // R05: the word-group anchored verifier is used exactly when it is built.
        // On astral-meeting hits one candidate reaches TryVerify, which executes the
        // anchored regex (one invocation alongside construction, both lanes); on
        // misses no candidate survives search, so the Lazy is never evaluated and no
        // anchored regex is constructed; ASCII hits serve fully owned with neither
        // candidates nor invocations. The verifier mode stays AnchoredRegex on every
        // row: the plan admits the anchored shape, but construction and execution
        // both follow candidate demand. This closes the use-vs-construction question
        // for the word-group contract with invocation evidence, not just flags.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchProbe = new Utf8Regex(pattern, options);
        var isMatchDiagnostics = isMatchProbe.CollectIsMatchDiagnostics(input);
        Assert.Equal(oracle.IsMatch(text), isMatchDiagnostics.Result);
        Assert.Equal("AnchoredRegex", isMatchDiagnostics.FallbackVerifierMode);
        Assert.Equal(expectUse, isMatchDiagnostics.VerifierInvocations > 0);
        Assert.Equal(expectUse, isMatchDiagnostics.SearchCandidates > 0);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.Equal(expectUse, isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countProbe = new Utf8Regex(pattern, options);
        var countDiagnostics = countProbe.CollectCountDiagnostics(input);
        Assert.Equal(oracle.Matches(text).Count, countDiagnostics.Result);
        Assert.Equal(expectUse, countDiagnostics.VerifierInvocations > 0);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.Equal(expectUse, countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedWordGroupMinBoundDivertCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("(?i)a\\W{2,}b", "xa\U0001F600b", "Q"),
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
    public void FoldedWordGroupMinBoundDivertsToBcl(string pattern, RegexOptions options, string text, string template)
    {
        // R05: folded unbounded word-group loops with minima other than one divert
        // to BCL-backed engines on astral input with oracle parity on every lane.
        // The byte-safe linear verifier counts scalars while BCL counts astral
        // units, so these shapes false-missed discovery owned (probed miss against
        // oracle [1:4] with no fallback constructed); the linear compile fence for
        // projected unbounded minima other than one closes the hole. Bounded
        // ranges, min-one loops, lazy loops, unfolded word groups, and sibling
        // shorthands keep their owned or verifier-owned routes (probe-mapped).
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.Equal(1, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> BoundedExactDivergentCases()
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
    [MemberData(nameof(BoundedExactDivergentCases))]
    public void BoundedExactDivergentKeepsBclDetailed(string pattern, RegexOptions options, string text, string template)
    {
        // R05: exact-count divergent loops serve literal-route discovery,
        // enumeration, splits, and all three replacement cores with oracle
        // parity and zero fallback construction on both lanes, while Detailed
        // shaping keeps BCL routing on both lanes. Ranged counts ({m,n} with
        // n>m) are excluded: the literal route returns longest-match spans where
        // the oracle stops at first success (probed: (a|ab){2,3} light [1:4] vs
        // oracle [1:2] with internal Match/Detailed disagreement), which stays
        // diagnosed engine debt, not a pinned contract.
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Index, actualDetailed.IndexInUtf16);
            Assert.Equal(expectedDetailed.Length, actualDetailed.LengthInUtf16);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        // R05: ranged divergent counts serve ordered first-success spans from
        // IgnoreCase variants join through the same gate (folded first-success spans, including a folding-mismatch text).
        // the owned slice with zero fallback construction on both lanes (the
        // longest-match automaton route is declined at analysis). Exact counts
        // keep their literal routes, see BoundedExactDivergentCases.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
    public void HeadedCategoryRangeAstralKeepsVerifierDiscovery(string pattern, RegexOptions options, string text, string template)
    {
        // R05: headed lazy ranges over unit-divergent category tables keep
        // verifier-owned discovery with BCL shaping on both lanes. The
        // anchored verifier is constructed on IsMatch and Count of both
        // lanes (stable across isolated, reversed-order, and cartesian runs;
        // the earlier isolation-vs-cartesian variance does not reproduce).
        // Parity holds on every lane.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.True(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.True(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
    public void HeadedUnboundedLazyRangeAstralKeepsVerifierDiscovery(string pattern, RegexOptions options, string text, string template)
    {
        // R05: headed unbounded lazy ranges over unit-divergent category tables keep
        // verifier-owned ordinary discovery with BCL shaping on astral input. The
        // anchored verifier is constructed on IsMatch, Count, and Match of the
        // ordinary lane while the plain fallback shapes Detailed, enumeration,
        // splits, and all three replacement cores. Compiled discovery serves owned
        // with no verifier for the set and single-negation atoms ([^x], \D, \S) in
        // isolation but keeps the anchored verifier for the word-group (\W) and
        // property (\p{L}) atoms; compiled anchored flags stay unpinned per the
        // headed-lazy-range precedent (process-context sensitivity) with the
        // isolation values recorded here. Compiled Match and shaping construct the
        // plain fallback. The \p{L} row misses (the gap needs two letters and only
        // one astral letter precedes the tail) yet routes exactly like the hits.
        // Parity holds on every lane.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
    public void HeadedUnboundedLazyRangeAstralMissOwnsDiscoveryKeepsFallbackShaping(string pattern, RegexOptions options, string text, string template)
    {
        // R05: headed unbounded lazy ranges miss astral input with zero fallback on
        // discovery: the unbounded lazy gap exhausts without meeting the tail, so no
        // candidate survives slice discovery to verification on either lane. The
        // shaping lanes still construct the plain fallback eagerly. Parity holds on
        // every lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var oracleHit = oracle.IsMatch(text);
        Assert.False(oracleHit);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.False(isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(0, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        Assert.False(matchRegex.Match(input).Success);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        Assert.False(detailedRegex.MatchDetailed(input).Success);
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        var splitRegex = new Utf8Regex(pattern, options);
        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }
        Assert.Equal(new System.Collections.Generic.List<(string Value, int Index, int Length)> { (text, 0, text.Length) }, actualSplits);
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: headed unbounded greedy ranges over unit-divergent category tables
        // serve fully owned on ASCII input across all nine lanes of both lanes.
        // Units and scalars coincide here, so the min-two-plus fence never
        // triggers: no verifier is built and no fallback is constructed, hits
        // and misses alike. The astral miss keeps owned discovery with fallback
        // shaping (see the miss battery below).
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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

    [Theory]
    [MemberData(nameof(HeadedUnboundedGreedyRangeAstralMissCases))]
    public void HeadedUnboundedGreedyRangeAstralMissOwnsDiscoveryKeepsFallbackShaping(string pattern, RegexOptions options, string text, string template)
    {
        // R05: headed unbounded greedy ranges miss astral input with zero fallback
        // on discovery: the unbounded greedy gap exhausts without meeting the tail,
        // so no candidate survives slice discovery to verification on either lane.
        // The shaping lanes still construct the plain fallback eagerly. Parity holds
        // on every lane. Mirrors the unbounded-lazy miss battery exactly.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var oracleHit = oracle.IsMatch(text);
        Assert.False(oracleHit);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.False(isMatchRegex.IsMatch(input));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(0, countRegex.Count(input));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        Assert.False(matchRegex.Match(input).Success);
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        Assert.False(detailedRegex.MatchDetailed(input).Success);
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        var splitRegex = new Utf8Regex(pattern, options);
        var actualSplits = new System.Collections.Generic.List<(string Value, int Index, int Length)>();
        foreach (var split in splitRegex.EnumerateSplits(input, int.MaxValue))
        {
            actualSplits.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }
        Assert.Equal(new System.Collections.Generic.List<(string Value, int Index, int Length)> { (text, 0, text.Length) }, actualSplits);
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
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
        // R05: minimum bounds of two or more over ASCII-closed atoms count UTF-16
        // units and scalars identically, so headed, tailed, bare, and lazy variants
        // step exactly like BCL units on astral input; discovery, shaping, splits,
        // and all three replacement cores never construct the fallback engine here.
        // Unit-divergent atoms keep the verifier-owned or BCL pins in
        // MinBoundHeadedAstralCases and MinBoundBareAstralDeclineCases.
        // Word-table ranges join this routing (ASCII hit probed fully owned on every lane).
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var sliceRegex = new Utf8Regex(pattern, options);
        Assert.True(sliceRegex.Inspection.DebugUsesOwnedSliceProgram);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> HeadedTaillessMinBoundDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("a\\D{2,}", "xa\U0001F600", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(HeadedTaillessMinBoundDeclineCases))]
    public void HeadedTaillessMinBoundDeclineKeepsBclShaping(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt: a headed but tailless minimum bound over a unit-divergent
        // atom keeps verifier-owned discovery and light-match shaping on the ordinary
        // lane (anchored verifier, no plain fallback) while the compiled lane diverts
        // every lane to BCL: without a tail the compiled kernel has no trailing anchor
        // to close the bound against, so only headed-plus-tail shapes keep owned compiled
        // discovery (see MinBoundHeadedAstralCases). Detailed shaping, enumeration,
        // splits, and all three replacement cores keep BCL routing on both lanes; parity
        // holds and construction is pinned per lane.
        var compiled = (options & RegexOptions.Compiled) != 0;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.Equal(compiled, isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.Equal(compiled, countRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.Equal(compiled, matchRegex.Inspection.DebugFallbackRegexIsCreated);
        if (!compiled)
        {
            Assert.True(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> MinBoundBareAstralDeclineCases()
    {
        (string pattern, string text, string template)[] rows =
        [
            ("([^x]{2,})", "\U0001F600yy", "Q"),
            ("(?i)([^x]{2,})", "\U0001F600yy", "Q"),
            ("\\p{Nd}{2,}", "x\U000104A0y", "Q"),
            ("(.{2,}?)", "a\U0001F600b", "Q"),
            ("([^x]{2,}?)", "a\U0001F600b", "Q"),
            ("(\\D{2,}?)", "a\U0001F600b", "<$1>"),
            ("\\p{L}{2,}", "xa\U0001F600b", "Q"),
        ];
        foreach (var row in rows)
        {
            yield return new object?[] { row.pattern, RegexOptions.CultureInvariant, row.text, row.template };
            yield return new object?[] { row.pattern, RegexOptions.Compiled | RegexOptions.CultureInvariant, row.text, row.template };
        }
    }

    [Theory]
    [MemberData(nameof(MinBoundBareAstralDeclineCases))]
    public void MinBoundBareAstralDeclineKeepsBclRouting(string pattern, RegexOptions options, string text, string template)
    {
        // Declared R05 debt, bare counterpart of the headed battery above:
        // without a head or tail for verifier search, every lane keeps BCL
        // routing on astral input; parity holds and construction is pinned.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Matches(text).Count, countRegex.Count(input));
        Assert.True(countRegex.Inspection.DebugFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text);
        var actualMatch = matchRegex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.True(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text);
        var actualDetailed = detailedRegex.MatchDetailed(input);
        Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
        if (expectedDetailed.Success)
        {
            Assert.Equal(expectedDetailed.Groups.Count, actualDetailed.GroupCount);
        }
        Assert.True(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.True(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.True(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 16 + 128];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.True(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedUnitOwnedCases()
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
    [MemberData(nameof(FoldedUnitOwnedCases))]
    public void FoldedUnitOwnsInput(string pattern, RegexOptions options, string text, string template)
    {
        // R05: the unit walk folds ASCII literals and mask classes through the
        // invariant fold table exactly like the scalar walk, so IgnoreCase
        // discovery, shaping, splits, replacement, and FromOffset lanes serve
        // owned on astral input with zero fallback construction on both lanes.
        // Byte-level lanes compare encoded bytes because lone halves substitute
        // identically through the encoder on both sides.
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        Assert.False(splitRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(splitRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var expected = oracle.Replace(text, template);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var bytesRegex = new Utf8Regex(pattern, options);
        var bytesActual = bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template));
        Assert.Equal(expectedBytes, bytesActual);
        Assert.False(bytesRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(bytesRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        var stringActual = stringRegex.ReplaceToString(input, template);
        Assert.Equal(expected, stringActual);
        Assert.False(stringRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(stringRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var destinationRegex = new Utf8Regex(pattern, options);
        Span<byte> destination = stackalloc byte[input.Length * 4 + 16];
        var status = destinationRegex.TryReplace(input, template, destination, out var bytesWritten);
        Assert.Equal(System.Buffers.OperationStatus.Done, status);
        Assert.Equal(expectedBytes, destination[..bytesWritten].ToArray());
        Assert.False(destinationRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(destinationRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var offsetRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, 0), offsetRegex.IsMatchFromUtf16Offset(input, 0));
        var expectedOffsetCount = 0;
        foreach (Match m in oracle.Matches(text, 0))
        {
            expectedOffsetCount++;
        }
        Assert.Equal(expectedOffsetCount, offsetRegex.CountFromUtf16Offset(input, 0));
        var expectedOffsetMatch = oracle.Match(text, 0);
        var actualOffsetMatch = offsetRegex.MatchFromUtf16Offset(input, 0);
        Assert.Equal(expectedOffsetMatch.Success, actualOffsetMatch.Success);
        if (expectedOffsetMatch.Success)
        {
            Assert.Equal(expectedOffsetMatch.Index, actualOffsetMatch.IndexInUtf16);
            Assert.Equal(expectedOffsetMatch.Length, actualOffsetMatch.LengthInUtf16);
        }
        Assert.False(offsetRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(offsetRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var offsetDetailedRegex = new Utf8Regex(pattern, options);
        var expectedOffsetDetailed = oracle.Match(text, 0);
        var actualOffsetDetailed = offsetDetailedRegex.MatchDetailedFromUtf16Offset(input, 0);
        Assert.Equal(expectedOffsetDetailed.Success, actualOffsetDetailed.Success);
        if (expectedOffsetDetailed.Success)
        {
            Assert.Equal(expectedOffsetDetailed.Groups.Count, actualOffsetDetailed.GroupCount);
            for (var number = 0; number < expectedOffsetDetailed.Groups.Count; number++)
            {
                var expectedOffsetGroup = expectedOffsetDetailed.Groups[number];
                var actualOffsetGroup = actualOffsetDetailed.GetGroup(number);
                Assert.Equal(expectedOffsetGroup.Success, actualOffsetGroup.Success);
                if (expectedOffsetGroup.Success)
                {
                    Assert.Equal(expectedOffsetGroup.Index, actualOffsetGroup.IndexInUtf16);
                    Assert.Equal(expectedOffsetGroup.Length, actualOffsetGroup.LengthInUtf16);
                    Assert.Equal(expectedOffsetGroup.Value, actualOffsetGroup.GetValueString());
                }
            }
        }
        Assert.False(offsetDetailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(offsetDetailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var offsetEnumerateRegex = new Utf8Regex(pattern, options);
        var expectedOffsetPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, 0))
        {
            expectedOffsetPositions.Add(m.Index + ":" + m.Length);
        }
        var actualOffsetPositions = new System.Collections.Generic.List<string>();
        foreach (var m in offsetEnumerateRegex.EnumerateMatchesFromUtf16Offset(input, 0))
        {
            actualOffsetPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedOffsetPositions, actualOffsetPositions);
        Assert.False(offsetEnumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(offsetEnumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedUnitFromOffsetOwnedCases()
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
    [MemberData(nameof(FoldedUnitFromOffsetOwnedCases))]
    public void FoldedUnitFromOffsetOwnsInput(string pattern, RegexOptions options, string text, int offset)
    {
        // R05: folded unit shapes serve every FromOffset lane owned from the
        // start unit on astral input; full start-at sequences match BCL with
        // zero fallback construction on both lanes.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text, offset), isMatchRegex.IsMatchFromUtf16Offset(input, offset));
        Assert.False(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(isMatchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var countRegex = new Utf8Regex(pattern, options);
        var expectedCount = 0;
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedCount++;
        }
        Assert.Equal(expectedCount, countRegex.CountFromUtf16Offset(input, offset));
        Assert.False(countRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(countRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var matchRegex = new Utf8Regex(pattern, options);
        var expectedMatch = oracle.Match(text, offset);
        var actualMatch = matchRegex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        if (expectedMatch.Success)
        {
            Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
            Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        }
        Assert.False(matchRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(matchRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var detailedRegex = new Utf8Regex(pattern, options);
        var expectedDetailed = oracle.Match(text, offset);
        var actualDetailed = detailedRegex.MatchDetailedFromUtf16Offset(input, offset);
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
        Assert.False(detailedRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(detailedRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        var enumerateRegex = new Utf8Regex(pattern, options);
        var expectedPositions = new System.Collections.Generic.List<string>();
        foreach (Match m in oracle.Matches(text, offset))
        {
            expectedPositions.Add(m.Index + ":" + m.Length);
        }
        var actualPositions = new System.Collections.Generic.List<string>();
        foreach (var m in enumerateRegex.EnumerateMatchesFromUtf16Offset(input, offset))
        {
            actualPositions.Add(m.IndexInUtf16 + ":" + m.LengthInUtf16);
        }
        Assert.Equal(expectedPositions, actualPositions);
        Assert.False(enumerateRegex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(enumerateRegex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    public static System.Collections.Generic.IEnumerable<object?[]> FoldedUnitDeclineCases()
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
    [MemberData(nameof(FoldedUnitDeclineCases))]
    public void FoldedUnitDeclineKeepsBclShaping(string pattern, RegexOptions options, string text)
    {
        // Declared R05 debt: Kelvin inputs keep the fenced BCL routes,
        // right-to-left search finds different winners, and unbounded
        // IgnoreCase loops stay slice-routed; parity holds and construction
        // is pinned per lane.
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var isMatchRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.IsMatch(text), isMatchRegex.IsMatch(input));
        Assert.True(isMatchRegex.Inspection.DebugFallbackRegexIsCreated);
        var stringRegex = new Utf8Regex(pattern, options);
        Assert.Equal(oracle.Replace(text, "Q"), stringRegex.ReplaceToString(input, "Q"));
        Assert.True(stringRegex.Inspection.DebugFallbackRegexIsCreated);
    }
}
