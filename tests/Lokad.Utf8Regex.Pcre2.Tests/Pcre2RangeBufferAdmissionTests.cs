using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2RangeBufferAdmissionTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_budget")]
    private static extern ref Pcre2ResourceBudget FamilyBudget(ref Pcre2LiteralFamilyGlobalMatchCursor cursor);

    [Fact]
    public void LiteralFamilyRangesBorrowTheLiveCursorLedger()
    {
        var compiled = new Utf8Pcre2Regex(@"(?>a\Kbz|ab)").DebugCompiledProgram;
        var program = Assert.IsAssignableFrom<Pcre2LiteralFamilyDirectProgram>(compiled.Operations.Enumerate);
        var cursor = new Pcre2LiteralFamilyGlobalMatchCursor(program,
            Utf8ValidatedInput.Create("abz ab"u8), new Utf8BytePosition(0),
            new Pcre2ResourceBudget(compiled.Request.DefaultLimits, compiled.Request.MatchTimeout, false));
        ref var budget = ref FamilyBudget(ref cursor);
        budget.ReserveWorking(13);
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            for (var i = 0; i < 17; i++) ledger.Add(new(i, 1), cursor.WorkingBufferControl);
            Assert.Equal(13UL + 32 + (ulong)Unsafe.SizeOf<Utf8ReplacementRange>() * 32, budget.ReservedWorkingBytes);
            Assert.Equal(17, ledger.Count);
        }
        finally { ledger.Dispose(cursor.WorkingBufferControl); }
        Assert.Equal(13UL, budget.ReservedWorkingBytes);
        budget.ReleaseWorking(13);
    }

    [Theory]
    // Warm native queries borrow their span: only range capacity and its old/new
    // growth overlap remain, with no former 48-byte decoded-template loan.
    [InlineData(159UL, false)] [InlineData(160UL, true)]
    [InlineData(447UL, false)] [InlineData(448UL, true)]
    public void LiteralDriverAdmitsCompleteRangeArraysAndGrowthOverlap(ulong heap, bool allow)
    {
        var count = heap < 300 ? 1 : 17;
        var regex = new Utf8Pcre2Regex("a", Pcre2CompileOptions.None, default,
            new() { HeapLimitInBytes = heap }, Regex.InfiniteMatchTimeout);
        var prepare = typeof(Utf8Pcre2Regex).GetMethod("GetTemplatePreparation",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .CreateDelegate<Func<Utf8ConstructionPattern, Pcre2SubstitutionOptions, Pcre2ReplacementPreparation>>(regex);
        _ = prepare(new("b"u8), default);
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
        static ulong Bytes(int capacity) => capacity == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<Utf8ReplacementRange>() * (ulong)capacity + 15) & ~15UL);
        var next = count == 0 ? 16 : 2 * count;
        var maximum = 13 + Bytes(count) + Bytes(next) - 1;
        var budget = new Pcre2ResourceBudget(new() { HeapLimitInBytes = maximum }, Regex.InfiniteMatchTimeout, false);
        budget.ReserveWorking(13);
        var control = new Pcre2CaptureProjectionBudget(ref budget);
        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            for (var i = 0; i < count; i++) ledger.Add(new(i, 1), control);
            Pcre2MatchException? failure = null;
            try { ledger.Add(new(count, 1), control); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.NotNull(failure);
            Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
            Assert.Equal(count, ledger.Count);
            Assert.Equal(13 + Bytes(count), budget.ReservedWorkingBytes);
            for (var i = 0; i < count; i++) Assert.Equal(new(i, 1), ledger[i]);
        }
        finally { ledger.Dispose(control); }
        Assert.Equal(13UL, budget.ReservedWorkingBytes);
        budget.ReleaseWorking(13);
    }
}
