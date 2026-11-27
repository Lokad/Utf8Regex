using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class InvariantIgnoreCaseLiteralSemanticTests
{
    private const string Subject = "KILO cat";

    [Theory]
    [InlineData("Sherlock Holmes|John Watson|Irene Adler", "é😀 sheRLock hOlmes λ John Watson 😀 IRENE ADLER")]
    [InlineData("a|aaa", "éAAA😀a")]
    [InlineData("aa|a", "éAAA😀a")]
    [InlineData("(cat)|(horse)", "😀é CAT λ Horse")]
    [InlineData(@"\b(?:cat|dog)\b", "éCAT😀 catDOG dog")]
    [InlineData("kilo|cat|dog", "é😀 KILO CAT dog")]
    [InlineData("kilo", "中文KILO")]
    [InlineData("kilo|cat|dog", "中文KILO CAT dog")]
    [InlineData("sherlock|moriarty", "éſherlock SHERLOCK 😀 moriarty")]
    [InlineData("cat||dog", "é😀 CAT dog")]
    public void FoldedFamiliesKeepAllPublicOperationsOnMixedSubjects(string pattern, string text)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var options = mode | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
            var regex = new Utf8Regex(pattern, options: options);
            var oracle = new Regex(pattern, options);
            Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, [text]);
        }
    }

    [Theory]
    [InlineData("kilo", false)]
    [InlineData("kilo", true)]
    [InlineData("KILO", false)]
    [InlineData("\\x6Bilo", false)]
    [InlineData("kilo|cat|dog", false)]
    [InlineData("kilo|cat|dog", true)]
    [InlineData("\\x6Bilo|cat|dog", false)]
    public void LiteralRoutesHonorKelvinSignCaseEquivalence(string pattern, bool compiled)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(Subject);

        Assert.Equal(expected.IsMatch(Subject), actual.IsMatch(input));
        Assert.Equal(expected.Count(Subject), actual.Count(input));

        var expectedMatch = expected.Match(Subject);
        var actualMatch = actual.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(Subject.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(Subject.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
        Assert.Equal(Encoding.UTF8.GetByteCount(Subject.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(Subject.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));

        var expectedRanges = expected.Matches(Subject)
            .Select(match => (Encoding.UTF8.GetByteCount(Subject.AsSpan(0, match.Index)), Encoding.UTF8.GetByteCount(match.Value)))
            .ToArray();
        var actualRanges = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(input))
        {
            actualRanges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        }

        Assert.Equal(expectedRanges, actualRanges);
    }

    [Theory]
    [InlineData("key", "😀Key KEY")]
    [InlineData("abck", "AbCK ABCK")]
    [InlineData("kk", "KKK")]
    [InlineData("key", "Kx KEY")]
    [InlineData("key", "Kx")]
    public void LateKelvinResumeRetainsScalarEdgesAndEarlierOverlaps(string pattern, string suffix)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled;
        var text = new string('x', 128) + suffix;
        var input = Encoding.UTF8.GetBytes(text);
        using var regex = new Utf8Regex(pattern, options);
        var expected = Regex.Match(text, pattern, options);
        var actual = regex.Match(input);
        Assert.Equal(expected.Success, regex.IsMatch(input));
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success) Utf8MatchSequence.AssertRange(actual, text, expected.Index, expected.Length);
    }

    [Fact]
    public void FiniteFoldedSearchChargesBlocksAndKeepsTheEarliestMatch()
    {
        using var regex = new Utf8Regex("key", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            new() { ResourcePolicy = new() { MaxDepth = 4096 } });
        using var source = new CancellationTokenSource();
        var prefix = "kex" + new string('x', 16 * 1024 - 4);
        var input = Encoding.UTF8.GetBytes(prefix + "KEY key");

        Assert.True(regex.IsMatch(input, source.Token));
        Assert.Equal(new Utf8CaptureRange(prefix.Length, prefix.Length + 5), regex.Match(input, cancellationToken: source.Token));
        Assert.Equal(2, regex.Count(input, source.Token));

        var distant = Encoding.UTF8.GetBytes(new string('x', 256 * 1024) + "KEY");
    }

    [Theory]
    [InlineData("kk", "kKKkK", 2044)]
    [InlineData("abck", "abcK € abCK ABcK", 2045)]
    [InlineData("abcdk", "ABCDK", 2042)]
    [InlineData("key", "€ KEY € KeX", 2046)]
    public void FiniteFoldedCountKeepsCompletedMatchesAndUnresolvedOverlaps(string pattern, string suffix, int padding)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        using var regex = new Utf8Regex(pattern, options,
            new() { ResourcePolicy = new() { MaxDepth = 4096 } });
        using var source = new CancellationTokenSource();
        var text = new string('x', padding) + suffix;
        Assert.Equal(Regex.Count(text, pattern, options), regex.Count(Encoding.UTF8.GetBytes(text), source.Token));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FoldedStarterScanUsesTheOriginalInterruptionAuthority(bool expire, bool count)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var source = new CancellationTokenSource();
        using var regex = new Utf8Regex("key", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            new() { MatchTimeout = expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout });
        var input = Encoding.UTF8.GetBytes(new string('x', 256 * 1024) + "KEY");
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            if (expire) Assert.Throws<RegexMatchTimeoutException>(Run);
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }

        void Run()
        {
            if (count) _ = regex.Count(input, source.Token);
            else _ = regex.IsMatch(input, source.Token);
        }
    }

    [Theory]
    [InlineData("kilo", false)]
    [InlineData("kilo", true)]
    [InlineData("kilo|cat|dog", false)]
    [InlineData("kilo|cat|dog", true)]
    public void LiteralOutputHonorsKelvinSignCaseEquivalence(string pattern, bool compiled)
    {
        const string replacement = "X";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var expectedRegex = new Regex(pattern, options);
        var expected = expectedRegex.Replace(Subject, replacement);
        var actual = new Utf8Regex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(Subject);

        Assert.Equal(expected, Encoding.UTF8.GetString(actual.Replace(input, "X"u8)));
        Assert.Equal(expected, Encoding.UTF8.GetString(actual.Replace(input, "X"u8)));
        Assert.Equal(expected, Encoding.UTF8.GetString(actual.Replace(input, "X"u8)));

        Span<byte> destination = stackalloc byte[32];
        var status = actual.TryReplace(input, "X"u8, destination, out var bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));

        status = actual.TryReplace(input, "X"u8, destination, out bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));

        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(input))
        {
            actualSplits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        }

        Assert.Equal(expectedRegex.Split(Subject), actualSplits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongLiteralCountPreservesMatchesOnNonAsciiInputWithoutKelvinSign(bool compiled)
    {
        const string pattern = "Sherlock Holmes";
        const string subject = "é SHERLOCK HOLMES Sherlock Holmes 夏 sherlock holmesx";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options: options);

        Assert.Equal(expected.Count(subject), actual.Count(Encoding.UTF8.GetBytes(subject)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongLiteralFamilyCountPreservesMixedUtf8AndNonOverlappingSemantics(bool compiled)
    {
        const string pattern =
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty";
        const string subject =
            "é SHERLOCK HOLMES sherlock holmesx John Watson 夏 irene adler Professor Moriarty";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options: options);

        Assert.Equal(expected.Count(subject), actual.Count(Encoding.UTF8.GetBytes(subject)));
    }

    [Theory]
    [InlineData("alpha|needle|omega|zeta", false)]
    [InlineData("alpha|needle|omega|zeta", true)]
    [InlineData(@"\b(?:alpha|needle|omega|zeta)\b", false)]
    [InlineData(@"\b(?:alpha|needle|omega|zeta)\b", true)]
    public void CorrelatedLiteralFamiliesPreserveGlobalAndOutputSemantics(
        string pattern,
        bool compiled)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var falsePrefix = string.Concat(Enumerable.Repeat("aqqq nqqq oqqq zqqq ", 32));
        var subject = $"{falsePrefix}nEeDlE aqqq OMEGA";
        var input = Encoding.UTF8.GetBytes(subject);
        var expected = new Regex(pattern, options, TimeSpan.FromSeconds(1));
        var actual = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(1)
        });

        Assert.Equal(expected.IsMatch(subject), actual.IsMatch(input));
        Assert.Equal(expected.Count(subject), actual.Count(input));

        var expectedMatch = expected.Match(subject);
        var actualMatch = actual.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(subject.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(subject.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));

        var expectedRanges = expected.Matches(subject)
            .Select(match => (System.Text.Encoding.UTF8.GetByteCount(subject.AsSpan(0, match.Index)), System.Text.Encoding.UTF8.GetByteCount(subject.AsSpan(match.Index, match.Length))))
            .ToArray();
        var actualRanges = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(input))
        {
            actualRanges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        }

        Assert.Equal(expectedRanges, actualRanges);
        Assert.Equal(expected.Replace(subject, "X"), Encoding.UTF8.GetString(actual.Replace(input, "X"u8)));
        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(input))
        {
            actualSplits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        }

        Assert.Equal(expected.Split(subject), actualSplits);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void MixedUtf8LiteralFamiliesPreserveUtf16Coordinates(bool compiled, bool finiteTimeout)
    {
        const string pattern = "alpha|bravo|charlie";
        const string subject = "é 😀 ALPHA bravo 夏 CHARLIE";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var timeout = finiteTimeout ? TimeSpan.FromSeconds(1) : Regex.InfiniteMatchTimeout;
        var expected = new Regex(pattern, options, timeout);
        var actual = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
        {
            MatchTimeout = timeout
        });
        var input = Encoding.UTF8.GetBytes(subject);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, actual.Inspection.ExecutionKind);
        Assert.Equal(expected.IsMatch(subject), actual.IsMatch(input));
        Assert.Equal(expected.Count(subject), actual.Count(input));

        var expectedFirst = expected.Match(subject);
        var actualFirst = actual.Match(input);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(subject.AsSpan(0, expectedFirst.Index)), actualFirst.StartOffsetInBytes);
        Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(subject.AsSpan(expectedFirst.Index, expectedFirst.Length)), (actualFirst.EndOffsetInBytes - actualFirst.StartOffsetInBytes));
        Assert.Equal(
            Encoding.UTF8.GetByteCount(subject.AsSpan(0, expectedFirst.Index)),
            actualFirst.StartOffsetInBytes);

        var expectedRanges = expected.Matches(subject)
            .Select(match => (
                ByteIndex: Encoding.UTF8.GetByteCount(subject.AsSpan(0, match.Index)),
                ByteLength: Encoding.UTF8.GetByteCount(subject.AsSpan(match.Index, match.Length))))
            .ToArray();
        var actualRanges = new List<(int ByteIndex, int ByteLength)>();
        foreach (var match in actual.EnumerateMatches(input))
        {
            actualRanges.Add((
                match.StartOffsetInBytes,
                (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        }

        Assert.Equal(expectedRanges, actualRanges);
        Assert.Equal(expected.Replace(subject, "X"), Encoding.UTF8.GetString(actual.Replace(input, "X"u8)));

        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(input))
        {
            actualSplits.Add(Encoding.UTF8.GetString(split.GetValueBytes()));
        }

        Assert.Equal(expected.Split(subject), actualSplits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MixedUtf8LiteralFamilyForcedNativeSplitPreservesUtf16Coordinates(bool compiled)
    {
        const string pattern = "alpha|bravo|charlie";
        const string subject = "é 😀 ALPHA bravo 夏 CHARLIE";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var expected = new Regex(pattern, options);
        var actual = new Utf8Regex(pattern, options: options);
        var analysis = Utf8FrontEnd.Compile(pattern, options);
        var input = Encoding.UTF8.GetBytes(subject);
        var splits = Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
            actual.Inspection.PreparedRegex,
            input, int.MaxValue,
            Utf8ExecutionDeadline.Infinite);

        var expectedSplits = new List<(int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in expected.EnumerateSplits(subject))
        {
            var (index, length) = split.GetOffsetAndLength(subject.Length);
            expectedSplits.Add((
                Encoding.UTF8.GetByteCount(subject.AsSpan(0, index)),
                Encoding.UTF8.GetByteCount(subject.AsSpan(index, length)),
                subject.Substring(index, length)));
        }
        var actualSplits = new List<(int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in splits)
        {
            actualSplits.Add((
                split.IndexInBytes,
                split.LengthInBytes,
                Encoding.UTF8.GetString(split.GetValueBytes())));
        }

        Assert.Equal(expectedSplits, actualSplits);
    }

    public static IEnumerable<object?[]> NonAsciiEnumerationCases()
    {
        string[] patterns = ["abc", "kil", "IB", "(aI)"];
        string[] texts = ["xxAB12cdxx", "ééABCé", "xKILy", "\u0130\u0131-test-K", "x\U0001F600aB", "nomatchhere", ""];
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
    [InlineData(false)]
    [InlineData(true)]
    public void EnumerationUsesOwnedShapingForKelvin(bool compiled)
    {
        const string pattern = "kil";
        const string text = "x\u212Ailo";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        var expected = new List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length))));
        }

        var actual = new List<(int Index, int Length)>();
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

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [MemberData(nameof(NonAsciiEnumerationCases))]
    public void EnumerationOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        var expected = new List<(int Index, int Length)>();
        foreach (Match expectedMatch in oracle.Matches(text))
        {
            expected.Add((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expectedMatch.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expectedMatch.Index, expectedMatch.Length))));
        }

        var actual = new List<(int Index, int Length)>();
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

        Assert.Equal(oracle.Matches(text).Count, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static IEnumerable<object?[]> NonAsciiSplitCases()
    {
        string[] patterns = ["abc", "kil", "IB", "(aI)"];
        string[] texts = ["xxAB12cdxx", "ééABCé", "xKILy", "\u0130\u0131-test-K", "x\U0001F600aB", "nomatchhere", ""];
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
    [MemberData(nameof(NonAsciiSplitCases))]
    public void SplitOwnsNonAsciiInput(string pattern, RegexOptions options, string text)
    {
        const int count = int.MaxValue;
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], global::System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), global::System.Text.Encoding.UTF8.GetByteCount(text[start..end])));
        }

        var actual = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }

        Assert.Equal(expected, actual);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SplitUsesOwnedShapingForKelvin(bool compiled)
    {
        // Invariant Kelvin folding and split projection use the owned unit cursor.
        const int count = int.MaxValue;
        const string pattern = "kil";
        const string text = "x\u212Ailo";
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var regex = new Utf8Regex(pattern, options: options);

        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(text, count))
        {
            var start = range.Start.Value;
            var end = range.End.Value;
            expected.Add((text[start..end], global::System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, start)), global::System.Text.Encoding.UTF8.GetByteCount(text[start..end])));
        }

        var actual = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }

        Assert.Equal(expected, actual);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static IEnumerable<object?[]> NonAsciiTemplateCases()
    {
        string[] patterns = ["abc", "kil", "IB"];
        string[] templates = ["x", "<$1>", "$&", "$`", "$\u0027", "$_", "$+", "$$"];
        string[] texts = ["ééABCé", "xKILy", "x\U0001F600aB", "nomatchhere"];
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
    [MemberData(nameof(NonAsciiTemplateCases))]
    public void TemplateOwnsNonAsciiInput(string pattern, RegexOptions options, string template, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(bytesRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, bytesRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));

        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(stringRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, stringRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static IEnumerable<object?[]> GroupedFoldedLiteralTemplateCases()
    {
        string[] texts = ["xxAB12cdxx", "xKILy", "ééABCé", "x\U0001F600aB", "nomatchhere"];
        string[] templates = ["x", "$&", "<$1>", "$+"];
        foreach (var template in templates)
        {
            foreach (var text in texts)
            {
                yield return new object?[] { RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, template, text };
                yield return new object?[] { RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, template, text };
            }
        }
    }

    [Theory]
    [MemberData(nameof(GroupedFoldedLiteralTemplateCases))]
    public void GroupedFoldedLiteralTemplateOwnsNonAscii(RegexOptions options, string template, string text)
    {
        const string pattern = "(aI)";
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var expected = oracle.Replace(text, template);

        var bytesRegex = new Utf8Regex(pattern, options: options);
        var bytesActual = Encoding.UTF8.GetString(bytesRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, bytesActual);
        Assert.True(bytesRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(bytesRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, bytesRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));

        var stringRegex = new Utf8Regex(pattern, options: options);
        var stringActual = Encoding.UTF8.GetString(stringRegex.Replace(input, Encoding.UTF8.GetBytes(template)));
        Assert.Equal(expected, stringActual);
        Assert.True(stringRegex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(stringRegex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, stringRegex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    private static void ConsumeMatches(Utf8Regex regex, byte[] input)
    {
    }
}
