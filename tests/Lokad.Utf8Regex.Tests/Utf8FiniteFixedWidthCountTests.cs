using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8FiniteFixedWidthCountTests
{
    private static readonly byte[] Dense = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(" xx ab12 ab34 ", 1000)));
    private static Utf8Regex Create(string pattern, ulong work = 1000000, ulong working = 1 << 20,
        RegexOptions options = RegexOptions.None) => new(pattern, options | RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, new() { MaxWorkSteps = work, MaxDepth = 1024,
                MaxWorkingBytes = working, MaxResultBytes = 1 });

    [Theory]
    [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void DenseOverlapPreservesBorrowedOffsetsAndEveryVectorTail(int width)
    {
        var pattern = "aa[A-Za-z]{" + (width - 2) + "}";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = Create(pattern, options: options);
            foreach (var length in Enumerable.Range(0, 100).Concat(new[] { 127, 128, 129, 255, 256, 257, 511 }))
            {
                var storage = Encoding.UTF8.GetBytes("pad" + new string('a', length) + "pad");
                for (var offset = 0; offset < 4; offset++)
                {
                    var bytes = new byte[length + offset];
                    storage.AsSpan(3, length).CopyTo(bytes.AsSpan(offset));
                    Assert.Equal(length / width, regex.Count(bytes.AsSpan(offset)));
                }
            }
        }
    }

    [Fact]
    public void EligibleCountNeedsNoVmDepthOrWorkingStorage()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 1,
            MaxWorkingBytes = 1, MaxResultBytes = 1 };
        foreach (var option in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("ab[0-9]{2}", option | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout, policy);
            // The vector kernel stays preferred; its checked branch fallback
            // also needs no VM storage on hardware without AVX2.
            Assert.Equal(2000, regex.Count(Dense));
        }
    }

    [Theory]
    [InlineData("ab[0-9]{2}$", "ab12 xx ab34")]
    [InlineData("^ab[0-9]{2}", "xx ab12 ab34")]
    [InlineData(@"ab[0-9]{2}\b", "ab12ab34")]
    [InlineData("(?i)ab[0-9]{2}", "AB12 ab34")]
    [InlineData("(?<=x)ab[0-9]{2}", "xab12 ab34")]
    [InlineData("ab[0-9]{2}", "😀ab12éab34")]
    public void IneligibleSemanticsKeepTheirExistingResults(string pattern, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(new Regex(pattern, RegexOptions.CultureInvariant).Count(text), Create(pattern).Count(bytes));
    }

    [Fact]
    public void SparseDeclineKeepsSpentWorkAndDoesNotPublishPartialCount()
    {
        var text = string.Concat(Enumerable.Repeat("ab12 ", 100)) + new string('x', 256) + "ab34";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = Create("ab[0-9]{2}");
        var invocation = new Utf8SliceInvocation(new(1000000, 1024, 1 << 20, 1), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref invocation);
        var validation = invocation.Budget.Steps;
        Assert.False(Utf8AsciiDeterministicFixedWidthCountExecutor.TryCountValidatedAscii(
            regex.Inspection.PreparedRegex.StructuralLinearProgram, input, ref invocation, out var count, out var breach));
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(0, count);
        if (Avx2.IsSupported) Assert.True(invocation.Budget.Steps > validation);
        Assert.Equal(101, regex.Count(input));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ValidationAndVectorChecksKeepOneCumulativeWorkAllowance()
    {
        var regex = Create("ab[0-9]{2}");
        var invocation = new Utf8SliceInvocation(new(1000000, 1024, 1 << 20, 1), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(Dense, ref invocation);
        var validation = invocation.Budget.Steps;
        var handled = Utf8AsciiDeterministicFixedWidthCountExecutor.TryCountValidatedAscii(
            regex.Inspection.PreparedRegex.StructuralLinearProgram, Dense, ref invocation, out var count, out var breach);
        Assert.Equal(Avx2.IsSupported, handled);
        if (!handled) { Assert.Equal(2000, regex.Count(Dense)); return; }
        Assert.Equal(Utf8ExecutionLimitKind.None, breach);
        Assert.Equal(2000, count);
        var matching = invocation.Budget.Steps - validation;
        Assert.True(validation > 0 && matching > 0);
        var cap = Math.Max(validation, matching);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create("ab[0-9]{2}", cap).Count(Dense));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Equal(cap + 1, failure.Requested);
        Assert.Equal(0UL, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(0UL, invocation.Budget.PeakDepth);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void VectorBlocksKeepOriginalPostValidationInterruption(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var tokens = new CancellationTokenSource();
        var regex = Create("ab[0-9]{2}");
        var invocation = new Utf8SliceInvocation(new(1000000, 1024, 1 << 20, 1), TimeSpan.FromMilliseconds(20), tokens.Token);
        _ = Utf8Validation.Validate(Dense, ref invocation);
        var validation = invocation.Budget.Steps;
        var checkpoints = 0;
        invocation.CheckpointHook = _ => { checkpoints++; if (cancel) tokens.Cancel(); else ticks = 25; };
        Exception? failure = null;
        try
        {
            var handled = Utf8AsciiDeterministicFixedWidthCountExecutor.TryCountValidatedAscii(
                regex.Inspection.PreparedRegex.StructuralLinearProgram, Dense, ref invocation, out _, out _);
            Assert.False(Avx2.IsSupported && handled);
        }
        catch (Exception error) { failure = error; }
        if (!Avx2.IsSupported) { Assert.Null(failure); return; }
        Assert.True(checkpoints > 0 && invocation.Budget.Steps > validation);
        if (cancel) Assert.Equal(tokens.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void NativeCountCannotSkipMalformedSuffix()
    {
        var malformed = new byte[Dense.Length + 1];
        Dense.CopyTo(malformed, 0);
        malformed[^1] = 255;
        Assert.Throws<ArgumentException>(() => Create("ab[0-9]{2}").Count(malformed));
    }
}
