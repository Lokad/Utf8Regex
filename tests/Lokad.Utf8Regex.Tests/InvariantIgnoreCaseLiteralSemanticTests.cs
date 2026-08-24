using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class InvariantIgnoreCaseLiteralSemanticTests
{
    private const string Subject = "KILO cat";

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
        var actual = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(Subject);

        Assert.Equal(expected.IsMatch(Subject), actual.IsMatch(input));
        Assert.Equal(expected.Count(Subject), actual.Count(input));

        var expectedMatch = expected.Match(Subject);
        var actualMatch = actual.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
        Assert.Equal(Encoding.UTF8.GetByteCount(Subject.AsSpan(0, expectedMatch.Index)), actualMatch.IndexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(Subject.AsSpan(expectedMatch.Index, expectedMatch.Length)), actualMatch.LengthInBytes);

        var expectedRanges = expected.Matches(Subject)
            .Select(static match => (match.Index, match.Length))
            .ToArray();
        var actualRanges = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(input))
        {
            actualRanges.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal(expectedRanges, actualRanges);
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
        var actual = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(Subject);

        Assert.Equal(expected, Encoding.UTF8.GetString(actual.Replace(input, replacement)));
        Assert.Equal(expected, Encoding.UTF8.GetString(actual.Replace(input, "X"u8)));
        Assert.Equal(expected, actual.ReplaceToString(input, replacement));

        Span<byte> destination = stackalloc byte[32];
        var status = actual.TryReplace(input, replacement, destination, out var bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));

        status = actual.TryReplace(input, "X"u8, destination, out bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected, Encoding.UTF8.GetString(destination[..bytesWritten]));

        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(input))
        {
            actualSplits.Add(split.GetValueString());
        }

        Assert.Equal(expectedRegex.Split(Subject), actualSplits);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void KelvinFallbackStillRejectsMalformedUtf8(bool compiled)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        if (compiled)
        {
            options |= RegexOptions.Compiled;
        }

        var regex = new Utf8Regex("kilo|cat|dog", options);
        var malformed = new byte[] { 0xE2, 0x84, 0xAA, 0xFF };

        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        Assert.Throws<ArgumentException>(() => ConsumeMatches(regex, malformed));
        Assert.Throws<ArgumentException>(() => regex.Replace(malformed, "X"));
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
        var actual = new Utf8Regex(pattern, options);

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
        var actual = new Utf8Regex(pattern, options);

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
        var actual = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(1));

        Assert.Equal(expected.IsMatch(subject), actual.IsMatch(input));
        Assert.Equal(expected.Count(subject), actual.Count(input));

        var expectedMatch = expected.Match(subject);
        var actualMatch = actual.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);

        var expectedRanges = expected.Matches(subject)
            .Select(static match => (match.Index, match.Length))
            .ToArray();
        var actualRanges = new List<(int Index, int Length)>();
        foreach (var match in actual.EnumerateMatches(input))
        {
            actualRanges.Add((match.IndexInUtf16, match.LengthInUtf16));
        }

        Assert.Equal(expectedRanges, actualRanges);
        Assert.Equal(expected.Replace(subject, "X"), actual.ReplaceToString(input, "X"));
        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(input))
        {
            actualSplits.Add(split.GetValueString());
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
        var actual = new Utf8Regex(pattern, options, timeout);
        var input = Encoding.UTF8.GetBytes(subject);

        Assert.Equal(NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals, actual.Inspection.ExecutionKind);
        Assert.Equal(expected.IsMatch(subject), actual.IsMatch(input));
        Assert.Equal(expected.Count(subject), actual.Count(input));

        var expectedFirst = expected.Match(subject);
        var actualFirst = actual.Match(input);
        Assert.Equal(expectedFirst.Index, actualFirst.IndexInUtf16);
        Assert.Equal(expectedFirst.Length, actualFirst.LengthInUtf16);
        Assert.Equal(
            Encoding.UTF8.GetByteCount(subject.AsSpan(0, expectedFirst.Index)),
            actualFirst.IndexInBytes);

        var expectedRanges = expected.Matches(subject)
            .Select(match => (
                Utf16Index: match.Index,
                Utf16Length: match.Length,
                ByteIndex: Encoding.UTF8.GetByteCount(subject.AsSpan(0, match.Index)),
                ByteLength: Encoding.UTF8.GetByteCount(subject.AsSpan(match.Index, match.Length))))
            .ToArray();
        var actualRanges = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength)>();
        foreach (var match in actual.EnumerateMatches(input))
        {
            actualRanges.Add((
                match.IndexInUtf16,
                match.LengthInUtf16,
                match.IndexInBytes,
                match.LengthInBytes));
        }

        Assert.Equal(expectedRanges, actualRanges);
        Assert.Equal(expected.Replace(subject, "X"), actual.ReplaceToString(input, "X"));

        var actualSplits = new List<string>();
        foreach (var split in actual.EnumerateSplits(input))
        {
            actualSplits.Add(split.GetValueString());
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
        var actual = new Utf8Regex(pattern, options);
        var analysis = Utf8FrontEnd.Compile(pattern, options);
        var verifierRuntime = Utf8VerifierRuntime.Create(
            analysis,
            pattern,
            options,
            Regex.InfiniteMatchTimeout);
        var input = Encoding.UTF8.GetBytes(subject);
        var validation = Utf8Validation.Validate(input);
        var splits = Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
            actual.Inspection.PreparedRegex,
            verifierRuntime,
            input,
            validation,
            validation.Utf16Length,
            int.MaxValue,
            Utf8ExecutionDeadline.Infinite);

        var expectedSplits = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in expected.EnumerateSplits(subject))
        {
            var (index, length) = split.GetOffsetAndLength(subject.Length);
            expectedSplits.Add((
                index,
                length,
                Encoding.UTF8.GetByteCount(subject.AsSpan(0, index)),
                Encoding.UTF8.GetByteCount(subject.AsSpan(index, length)),
                subject.Substring(index, length)));
        }
        var actualSplits = new List<(int Utf16Index, int Utf16Length, int ByteIndex, int ByteLength, string Value)>();
        foreach (var split in splits)
        {
            actualSplits.Add((
                split.IndexInUtf16,
                split.LengthInUtf16,
                split.IndexInBytes,
                split.LengthInBytes,
                split.GetValueString()));
        }

        Assert.Equal(expectedSplits, actualSplits);
    }

    private static void ConsumeMatches(Utf8Regex regex, byte[] input)
    {
        foreach (var _ in regex.EnumerateMatches(input))
        {
        }
    }
}
