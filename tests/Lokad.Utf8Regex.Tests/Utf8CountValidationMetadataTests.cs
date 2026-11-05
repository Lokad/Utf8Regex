using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8CountValidationMetadataTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2045)] [InlineData(2046)]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4095)]
    public void BorrowedAsciiProofKeepsBlockWorkAndFullCoordinateValidation(int prefix)
    {
        foreach (var suffix in new[] { "", "é", "中", "😀", "é😀中" })
        {
            var text = new string('a', prefix) + suffix;
            var bytes = Encoding.UTF8.GetBytes(text);
            var backing = new byte[bytes.Length + 13];
            bytes.CopyTo(backing.AsSpan(5));
            var input = backing.AsSpan(5, bytes.Length);
            var full = new Utf8SliceInvocation(new(1_000_000, 0, 0), Timeout.InfiniteTimeSpan, default);
            var flags = new Utf8SliceInvocation(new(1_000_000, 0, 0), Timeout.InfiniteTimeSpan, default);
            var metadata = Utf8Validation.Validate(input, ref full);
            Assert.Equal(suffix.Length == 0, Utf8Validation.ValidateIsAscii(input, ref flags));
            Assert.Equal(text.Length, metadata.Utf16Length);
            Assert.Equal(bytes.Length, metadata.ByteLength);
            Assert.Equal(suffix.Length == 0, metadata.IsAscii);
            Assert.Equal(suffix.Contains("😀", StringComparison.Ordinal), metadata.ContainsSupplementaryScalars);
            Assert.Equal(full.Budget.Steps, flags.Budget.Steps);
            Assert.Equal(0UL, flags.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("")] [InlineData("ascii")] [InlineData("é")] [InlineData("😀")]
    public void UnmeteredValidationAlsoReturnsAnExactAsciiProof(string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(default, Timeout.InfiniteTimeSpan, default);
        Assert.Equal(System.Text.Ascii.IsValid(input), Utf8Validation.ValidateIsAscii(input, ref invocation));
    }

    [Theory]
    [InlineData(2046)] [InlineData(2047)] [InlineData(2048)] [InlineData(4095)]
    public void MalformedBlocksRetainTheOriginalAbsoluteError(int prefix)
    {
        foreach (var tail in new byte[][] { [0xc2], [0xed, 0xa0, 0x80], [0xf0, 0x9f], [0x80, 0x80, 0x80, 0x80] })
        {
            var input = Encoding.UTF8.GetBytes(new string('a', prefix)).Concat(tail).ToArray();
            var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.ThrowIfInvalidOnly(input));
            var actual = Assert.Throws<ArgumentException>(() =>
            {
                var invocation = new Utf8SliceInvocation(new(1_000_000, 0, 0), Timeout.InfiniteTimeSpan, default);
                _ = Utf8Validation.ValidateIsAscii(input, ref invocation);
            });
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    [Theory]
    [InlineData("abc[0-9]{2}", "abc12 abc34")]
    [InlineData("abc[0-9]{2}", "é😀abc12中abc34")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "class Alpha record Beta")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+", "é😀 class Alpha 中record Wrong record Good")]
    public void FinitePublicCountUsesTheSameScalarSemanticsAndValidatesTheWholeTail(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, mode, Timeout.InfiniteTimeSpan,
                new() { MaxWorkSteps = 1_000_000, MaxDepth = 10000,
                    MaxWorkingBytes = 1 << 24, MaxResultBytes = 1 << 24 });
            using var live = new CancellationTokenSource();
            Assert.Equal(new Regex(pattern, mode).Count(text), regex.Count(input, live.Token));
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 255 }).ToArray()));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicBlockInterruptionKeepsTheOriginalTokenAndDeadline(bool expire)
    {
        var input = Encoding.ASCII.GetBytes(new string('x', 262144) + "abc12");
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var regex = new Utf8Regex("abc[0-9]{2}", RegexOptions.None, TimeSpan.FromMilliseconds(20),
            new() { MaxWorkSteps = 1_000_000, MaxWorkingBytes = 1 << 20 });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= 2) return;
            entered = true;
            if (expire) ticks = 50;
            else cancellation.Cancel();
        });
        try
        {
            if (expire) Assert.Equal("abc[0-9]{2}", Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input)).Pattern);
            else Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(entered);
        ticks = 0;
        Assert.Equal(1, regex.Count("abc12"u8));
        var denied = new Utf8Regex("abc[0-9]{2}", RegexOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 1, MaxWorkingBytes = 1 << 20 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Count(input)).ResourceKind);
    }
}
