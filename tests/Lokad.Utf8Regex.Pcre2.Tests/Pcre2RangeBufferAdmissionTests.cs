using System.Buffers;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2RangeBufferAdmissionTests
{
    [Theory]
    [InlineData(223UL, false)] [InlineData(224UL, true)]
    [InlineData(639UL, false)] [InlineData(640UL, true)]
    public void LiteralDriverAdmitsCompleteRangeArraysAndGrowthOverlap(ulong heap, bool allow)
    {
        var count = heap < 300 ? 1 : 17;
        var regex = new Utf8Pcre2Regex("a", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = heap }, Regex.InfiniteMatchTimeout);
        var input = Enumerable.Repeat((byte)'a', count).ToArray();
        var destination = Enumerable.Repeat((byte)0xcc, count).ToArray();
        if (!allow)
        {
            var failure = Assert.Throws<Pcre2MatchException>(() => regex.TryReplace(input, "b"u8, destination, out _));
            Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
            Assert.All(destination, static value => Assert.Equal(0xcc, value));
        }
        else
        {
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "b"u8, destination, out var written));
            Assert.Equal(count, written);
            Assert.All(destination, static value => Assert.Equal((byte)'b', value));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(16)] [InlineData(2048)]
    public void BorrowedVmAuthorityKeepsParentLoanAndOldRangesOnDenial(int count)
    {
        static ulong Bytes(int capacity) => capacity == 0 ? 0 : 32UL + ((12UL * (ulong)capacity + 15) & ~15UL);
        var next = count == 0 ? 16 : 2 * count;
        var maximum = 13 + Bytes(count) + Bytes(next) - 1;
        var budget = new Pcre2ResourceBudget(new() { HeapLimitInBytes = maximum }, Regex.InfiniteMatchTimeout, false);
        budget.ReserveWorking(13);
        var control = new Pcre2CaptureProjectionBudget(ref budget);
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            for (var i = 0; i < count; i++) ledger.Add(new(i, 1, i), control);
            Pcre2MatchException? failure = null;
            try { ledger.Add(new(count, 1, count), control); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.NotNull(failure);
            Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
            Assert.Equal(count, ledger.Count);
            Assert.Equal(13 + Bytes(count), budget.ReservedWorkingBytes);
            for (var i = 0; i < count; i++) Assert.Equal(new(i, 1, i), ledger[i]);
        }
        finally { ledger.Dispose(control); }
        Assert.Equal(13UL, budget.ReservedWorkingBytes);
        budget.ReleaseWorking(13);
    }
}
