using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class InvariantCyrillicLiteralCountTests
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    [Theory]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void SherlockLiteralUsesNativeCountStrategy(RegexOptions options)
    {
        var regex = new Utf8Regex("Шерлок Холмс", options);
        var oracle = new Regex("Шерлок Холмс", options, Regex.InfiniteMatchTimeout);
        const string input = "шерлок холмс; ШЕРЛОК ХОЛМС; Шерлок ХоЛмС; Шерлок Уолмс";

        Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.False(regex.Inspection.DebugInvariantCyrillicCountUsesCorrelatedPrefilter);
        Assert.True(regex.Inspection.DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void SherlockLiteralAlternationUsesNativeCountStrategy(RegexOptions options)
    {
        const string pattern = "Шерлок Холмс|Джон Уотсон|Ирен Адлер|инспектор Лестрейд|профессор Мориарти";
        const string input = "шерлок холмс; ДЖОН УОТСОН; ирен адлер; ИнСпЕкТоР ЛеСтРеЙд; ПРОФЕССОР МОРИАРТИ; Шерлок Уолмс";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.Equal(
            Sse2.IsSupported && Ssse3.IsSupported,
            regex.Inspection.DebugInvariantCyrillicCountUsesCorrelatedPrefilter);
        Assert.False(regex.Inspection.DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("А|АА", "АА")]
    [InlineData("АА|А", "АА")]
    [InlineData("АБ|Б", "АББ")]
    public void LiteralAlternationPreservesLeftmostBranchOrderAndNonOverlap(string pattern, string input)
    {
        var regex = new Utf8Regex(pattern, Options);
        var oracle = new Regex(pattern, Options, Regex.InfiniteMatchTimeout);

        Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void BasicCyrillicPairsMatchTheInvariantRegexOracle()
    {
        var patterns = Enumerable.Range('\u0410', 0x40)
            .Select(static value => (char)value)
            .Append('\u0401')
            .Append('\u0451')
            .Distinct();

        foreach (var pattern in patterns)
        {
            var regex = new Utf8Regex(pattern.ToString(), Options);
            var oracle = new Regex(pattern.ToString(), Options, Regex.InfiniteMatchTimeout);
            Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);

            for (var candidateValue = 0x0400; candidateValue <= 0x052F; candidateValue++)
            {
                var candidate = ((char)candidateValue).ToString();
                Assert.Equal(
                    oracle.Count(candidate),
                    regex.Count(Encoding.UTF8.GetBytes(candidate)));
            }
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("Ш+")]
    [InlineData("Україна")]
    [InlineData("Шерлок|")]
    [InlineData("Шерлок|abc")]
    public void StrategyRejectsPatternsOutsideItsExactSemanticSubset(string pattern)
    {
        var regex = new Utf8Regex(pattern, Options);

        Assert.False(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
    }

    [Fact]
    public void StrategyRejectsFiniteTimeouts()
    {
        var regex = new Utf8Regex("Шерлок Холмс", Options, TimeSpan.FromSeconds(1));

        Assert.False(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
    }

    [Fact]
    public void SingleLiteralFiniteAndLiveTokenCallsKeepTheirInvocationAuthority()
    {
        var input = "ШЕРЛОК ХОЛМС; шерлок холмс"u8.ToArray();
        var regex = new Utf8Regex("Шерлок Холмс", Options, TimeSpan.FromSeconds(5), new()
        {
            MaxWorkSteps = 100_000, MaxDepth = 1024,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576,
        });
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(2, regex.Count(input, cancellation.Token));
        var denied = new Utf8Regex("Шерлок Холмс", Options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1 });
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
        cancellation.Cancel();
        var canceled = Assert.Throws<OperationCanceledException>(() => denied.Count(input, cancellation.Token));
        Assert.Equal(cancellation.Token, canceled.CancellationToken);
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { 0xFF }));
    }

    [Theory]
    [InlineData(false, "Шерлок Холмс")]
    [InlineData(true, "Шерлок Холмс")]
    [InlineData(false, "Шерлок Холмс|Джон Уотсон")]
    [InlineData(true, "Шерлок Холмс|Джон Уотсон")]
    public void PreparedStrategyKeepsFiniteAndLiveTokenAuthority(bool compiled, string pattern)
    {
        var options = Options | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var input = Encoding.UTF8.GetBytes(new string('x', 4096) + "ШЕРЛОК ХОЛМС; шерлок холмс");
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);

        var finite = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout, new()
        {
            MaxWorkSteps = 50_000_000, MaxDepth = 1024,
            MaxWorkingBytes = 64 * 1_048_576, MaxResultBytes = 128 * 1_048_576,
        });
        Assert.True(finite.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.Equal(2, finite.Count(input));
        var denied = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1 });
        Assert.True(denied.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);

        using var cancellation = new CancellationTokenSource();
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ => cancellation.Cancel());
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token));
            Assert.Equal(cancellation.Token, error.CancellationToken);
        }
        finally
        {
            diagnostics.Complete();
        }

        Assert.Equal(2, regex.Count(input));
    }

    [Fact]
    public void StrategyStillRejectsMalformedUtf8()
    {
        var regex = new Utf8Regex("Шерлок Холмс", Options);
        var malformed = new byte[] { 0xD0, 0x20 };

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Fact]
    public void FusedSingleLiteralCountCoversEveryVectorLaneAndTail()
    {
        var ordinary = new Utf8Regex("Шерлок Холмс", Options);
        var compiled = new Utf8Regex("Шерлок Холмс", Options | RegexOptions.Compiled);
        var oracle = new Regex("Шерлок Холмс", Options, Regex.InfiniteMatchTimeout);
        var literal = "Шерлок Холмс"u8;

        foreach (var borrowedOffset in new[] { 0, 1, 17, 31 })
        for (var offset = 0; offset < 96; offset++)
        for (var tail = 0; tail < 32; tail++)
        {
            // Invalid bytes outside the borrowed subject must never enter its
            // validation proof, including at every complete-vector/tail edge.
            var storage = Enumerable.Repeat((byte)0xFF, borrowedOffset + offset + literal.Length + tail + 32).ToArray();
            var input = storage.AsSpan(borrowedOffset, offset + literal.Length + tail);
            input.Fill((byte)'.');
            literal.CopyTo(input[offset..]);
            var text = Encoding.UTF8.GetString(input);
            var expected = oracle.Count(text);

            Assert.Equal(expected, ordinary.Count(input));
            Assert.Equal(expected, compiled.Count(input));
        }

        if (Vector256.IsHardwareAccelerated)
        {
            Assert.Equal(
                "invariant_cyrillic_fused_validated_count",
                compiled.CollectCountDiagnostics(Encoding.UTF8.GetBytes("... Шерлок Холмс ... ШЕРЛОК ХОЛМС ...")).ExecutionRoute);
        }
    }

    [Theory]
    [InlineData("12Ш", "12ш")]
    [InlineData(" Ш", " ш")]
    [InlineData("1Ш 2Х", "1ш 2х")]
    public void AsciiPrefixBeforeFirstCyrillicScalarCanBelongToTheMatch(string pattern, string value)
    {
        var ordinary = new Utf8Regex(pattern, Options);
        var compiled = new Utf8Regex(pattern, Options | RegexOptions.Compiled);
        var oracle = new Regex(pattern, Options);
        for (var padding = 0; padding < 64; padding++)
        {
            var text = new string('.', padding) + value + new string('.', 32);
            var bytes = Encoding.UTF8.GetBytes(text);
            var storage = Enumerable.Repeat((byte)0xFF, bytes.Length + 48).ToArray();
            bytes.CopyTo(storage, 17);
            var input = storage.AsSpan(17, bytes.Length);
            Assert.Equal(oracle.Count(text), ordinary.Count(input));
            Assert.Equal(oracle.Count(text), compiled.Count(input));
        }
    }

    [Fact]
    public void FusedSingleLiteralCountResumesAcrossMixedWidthScalars()
    {
        const string input = "ШЕРЛОК ХОЛМС € 夏 😀 шерлок холмс λ Шерлок Уолмс";
        var ordinary = new Utf8Regex("Шерлок Холмс", Options);
        var compiled = new Utf8Regex("Шерлок Холмс", Options | RegexOptions.Compiled);
        var oracle = new Regex("Шерлок Холмс", Options, Regex.InfiniteMatchTimeout);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(oracle.Count(input), ordinary.Count(bytes));
        Assert.Equal(oracle.Count(input), compiled.Count(bytes));
    }

    [Theory]
    [InlineData("€")]
    [InlineData("夏")]
    [InlineData("😀")]
    public void WiderScalarResumePreservesEveryBlockBoundaryAndBorrowedOffset(string scalar)
    {
        var ordinary = new Utf8Regex("Шерлок Холмс", Options);
        var compiled = new Utf8Regex("Шерлок Холмс", Options | RegexOptions.Compiled);
        foreach (var borrowedOffset in new[] { 1, 17, 31 })
        for (var padding = 0; padding < 64; padding++)
        {
            var text = new string('.', padding) + scalar + "ШЕРЛОК ХОЛМС" +
                new string('.', 33) + scalar + "шерлок холмс" + scalar;
            var bytes = Encoding.UTF8.GetBytes(text);
            var storage = Enumerable.Repeat((byte)0xFF, borrowedOffset + bytes.Length + 1).ToArray();
            bytes.CopyTo(storage, borrowedOffset);
            var input = storage.AsSpan(borrowedOffset, bytes.Length);
            Assert.Equal(2, ordinary.Count(input));
            Assert.Equal(2, compiled.Count(input));
            // The same byte outside the borrow is ignored; inside it is rejected,
            // including after both vector restarts have already found matches.
            Assert.Throws<ArgumentException>(() => ordinary.Count(storage.AsSpan(borrowedOffset, bytes.Length + 1)));
            Assert.Throws<ArgumentException>(() => compiled.Count(storage.AsSpan(borrowedOffset, bytes.Length + 1)));
        }
    }

    [Fact]
    public void FusedSingleLiteralCountMatchesDotNetOnDeterministicValidSubjects()
    {
        var ordinary = new Utf8Regex("Шерлок Холмс", Options);
        var compiled = new Utf8Regex("Шерлок Холмс", Options | RegexOptions.Compiled);
        var oracle = new Regex("Шерлок Холмс", Options, Regex.InfiniteMatchTimeout);
        var random = new Random(0x5E210C);
        string[] fragments =
        [
            "Шерлок Холмс", "шерлок холмс", "ШЕРЛОК ХОЛМС", "Шерлок Уолмс",
            "русский", " ", ".", "€", "夏", "😀", "λ",
        ];

        for (var subjectIndex = 0; subjectIndex < 256; subjectIndex++)
        {
            var builder = new StringBuilder();
            var fragmentCount = random.Next(8, 80);
            for (var fragmentIndex = 0; fragmentIndex < fragmentCount; fragmentIndex++)
            {
                builder.Append(fragments[random.Next(fragments.Length)]);
            }

            var input = builder.ToString();
            var bytes = Encoding.UTF8.GetBytes(input);
            var expected = oracle.Count(input);
            Assert.Equal(expected, ordinary.Count(bytes));
            Assert.Equal(expected, compiled.Count(bytes));
        }
    }

    [Fact]
    public void FusedSingleLiteralCountRejectsMalformedUtf8AtEveryVectorLane()
    {
        var regex = new Utf8Regex("Шерлок Холмс", Options | RegexOptions.Compiled);
        for (var offset = 0; offset < 96; offset++)
        {
            var input = Enumerable.Repeat((byte)'.', 160).ToArray();
            input[offset] = 0xD0;
            input[offset + 1] = (byte)' ';

            Assert.Throws<ArgumentException>(() => regex.Count(input));
        }
    }
}
