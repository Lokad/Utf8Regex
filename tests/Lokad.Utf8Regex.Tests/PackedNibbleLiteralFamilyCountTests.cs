using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class PackedNibbleLiteralFamilyCountTests
{
    private const string Pattern = "夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CandidateBlocksRespectBorrowedOffsetsAndEveryTail(bool folded)
    {
        if (!(Sse2.IsSupported && Ssse3.IsSupported)) return;
        var literals = (folded ? "abcdefghjlmn|qrstuvwxyzab|cdefghjklmno|pqrstuvwxyza" : Pattern)
            .Split('|').Select(Encoding.UTF8.GetBytes).ToArray();
        var prefilter = folded
            ? PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(literals)
            : new PreparedMultiLiteralPackedNibbleSimdPrefilter(literals);
        foreach (var borrowedOffset in new[] { 0, 1, 17, 31 })
        for (var prefix = 0; prefix < 64; prefix++)
        for (var tail = 0; tail < 32; tail++)
        {
            var literal = folded ? Encoding.UTF8.GetBytes("ABCDEFGHJLMN") : literals[1];
            var length = prefix + literal.Length + tail;
            var storage = Enumerable.Repeat((byte)'x', borrowedOffset + length + 32).ToArray();
            literal.CopyTo(storage.AsSpan(borrowedOffset + prefix));
            var subject = storage.AsSpan(borrowedOffset, length);
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            var previous = -1;
            var found = false;
            while (prefilter.TryFindNextCandidate(subject, ref state, out var candidate))
            {
                Assert.InRange(candidate, previous + 1, length - prefilter.ShortestLength);
                previous = candidate;
                found |= candidate == prefix;
            }
            Assert.True(found);
        }
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void ChineseLiteralFamilyUsesPackedNibbleCountStrategy(RegexOptions options)
    {
        const string input = "夏洛克·福尔摩斯和约翰华生拜访阿德勒。雷斯垂德追踪莫里亚蒂教授。";
        var regex = new Utf8Regex(Pattern, options);
        var oracle = new Regex(Pattern, options, Regex.InfiniteMatchTimeout);
        var bytes = Encoding.UTF8.GetBytes(input);

        Assert.Equal(Sse2.IsSupported && Ssse3.IsSupported, regex.Inspection.DebugHasPackedNibbleLiteralFamilyCountStrategy);
        Assert.Equal(oracle.Count(input), regex.Count(bytes));
        if (Sse2.IsSupported && Ssse3.IsSupported)
        {
            Assert.Equal("literal_family_packed_nibble_simd_count", regex.CollectCountDiagnostics(bytes).ExecutionRoute);
        }
    }

    [Fact]
    public void CountCoversEveryVectorLaneAndTheScalarTail()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
        var oracle = new Regex(
            Pattern,
            RegexOptions.CultureInvariant | RegexOptions.Compiled,
            Regex.InfiniteMatchTimeout);
        var literal = "莫里亚蒂教授"u8;

        for (var offset = 0; offset < 96; offset++)
        {
            var input = Enumerable.Repeat((byte)'.', 160).ToArray();
            literal.CopyTo(input.AsSpan(offset));

            Assert.Equal(oracle.Count(Encoding.UTF8.GetString(input)), regex.Count(input));
        }
    }

    [Theory]
    [InlineData("猫猫猫猫|猫猫猫", "猫猫猫猫猫猫")]
    [InlineData("猫猫猫|猫猫猫猫", "猫猫猫猫猫猫")]
    [InlineData("猫猫猫|狗狗狗", "猫猫猫猫猫猫狗狗狗")]
    public void CountPreservesLeftmostBranchOrderAndNonOverlap(string pattern, string input)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void CountMatchesDotNetOnDeterministicValidSubjects()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);
        var oracle = new Regex(Pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var random = new Random(0x43A11);
        string[] fragments =
        [
            "甲", "乙", "丙", "。", " ", "x", "夏", "洛", "克",
            "夏洛克·福尔摩斯", "约翰华生", "阿德勒", "雷斯垂德", "莫里亚蒂教授",
        ];

        for (var subjectIndex = 0; subjectIndex < 256; subjectIndex++)
        {
            var builder = new StringBuilder();
            var fragmentCount = random.Next(1, 80);
            for (var fragmentIndex = 0; fragmentIndex < fragmentCount; fragmentIndex++)
            {
                builder.Append(fragments[random.Next(fragments.Length)]);
            }

            var input = builder.ToString();
            Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
        }
    }

    [Fact]
    public void CountDoesNotBypassMalformedUtf8Rejection()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.CultureInvariant);
        byte[] malformed = [.. "夏洛克·福尔摩斯"u8, 0xFF, .. "阿德勒"u8];

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }

    [Fact]
    public void FiniteTimeoutRetainsTheBudgetedCountRoute()
    {
        var regex = new Utf8Regex(
            Pattern,
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));

        Assert.False(regex.Inspection.DebugHasPackedNibbleLiteralFamilyCountStrategy);
        Assert.Equal(2, regex.Count("夏洛克·福尔摩斯和阿德勒"u8));
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void FiniteAndLiveTokenCallsKeepTheirScalarAuthority(RegexOptions options)
    {
        var regex = new Utf8Regex(Pattern, options, TimeSpan.FromSeconds(5), new()
        {
            MaxWorkSteps = 100_000, MaxDepth = 1024,
            MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576,
        });
        using var cancellation = new CancellationTokenSource();
        Assert.Equal(2, regex.Count("夏洛克·福尔摩斯和阿德勒"u8, cancellation.Token));
        cancellation.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
            regex.Count("夏洛克·福尔摩斯和阿德勒"u8, cancellation.Token));
        Assert.Equal(cancellation.Token, error.CancellationToken);
        Assert.Throws<ArgumentException>(() => regex.Count(new byte[] { 0xFF }));
    }

    [Theory]
    [InlineData("alpha|beta", RegexOptions.CultureInvariant)]
    [InlineData("夏洛克|Sherlock", RegexOptions.CultureInvariant)]
    [InlineData("夏洛|约翰", RegexOptions.CultureInvariant)]
    [InlineData(@"\b(?:夏洛克|约翰华生)\b", RegexOptions.CultureInvariant)]
    [InlineData(Pattern, RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    public void SelectorRejectsUnprovenFamilyShapes(string pattern, RegexOptions options)
    {
        var regex = new Utf8Regex(pattern, options);

        Assert.False(regex.Inspection.DebugHasPackedNibbleLiteralFamilyCountStrategy);
    }
}
