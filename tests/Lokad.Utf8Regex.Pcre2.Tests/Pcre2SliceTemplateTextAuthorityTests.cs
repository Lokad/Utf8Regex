using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SliceTemplateTextAuthorityTests
{
    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null,
        string pattern = "(é)(😀)")
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, limits,
            timeout ?? Timeout.InfiniteTimeSpan);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        return regex;
    }

    [Theory]
    [InlineData("(é)(😀)", "é😀xé😀β", "$2$1", Pcre2SubstitutionOptions.None, 0, "😀éx😀éβ")]
    [InlineData("(é)(😀)", "nothing😀", @"\o{Z}", Pcre2SubstitutionOptions.Extended, 0, "nothing😀")]
    [InlineData("(é)(😀)", "", "${", Pcre2SubstitutionOptions.None, 0, "")]
    [InlineData("(é)(😀)", "é😀xé😀β", "$2$1", Pcre2SubstitutionOptions.None, 7, "é😀x😀éβ")]
    [InlineData("(é)(😀)", "é😀xé😀β", "$2$1", Pcre2SubstitutionOptions.SubstituteReplacementOnly, 0, "😀é😀é")]
    [InlineData("(é)(😀)", "é😀xé😀β", "$2$1", Pcre2SubstitutionOptions.SubstituteLiteral, 0, "$2$1x$2$1β")]
    [InlineData("(é)(😀)", "é😀", @"${1:+\U𐐨:other}$2", Pcre2SubstitutionOptions.Extended, 0, "𐐀😀")]
    [InlineData("(?<=abc)(|def)", "abcdefabc😀", "<$1>", Pcre2SubstitutionOptions.None, 0, "abc<><def>abc<>😀")]
    public void OwnedByteAndTextTemplatesKeepTheirSelectedScalarOutcomes(string pattern, string input,
        string template, Pcre2SubstitutionOptions options, int start, string expected)
    {
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var regex = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, pattern: pattern);
            var bytes = Encoding.UTF8.GetBytes(input);
            Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(bytes, template, start, options, Pcre2MatchOptions.None, token)));
            var result = regex.ReplaceToString(bytes, template, start, options, Pcre2MatchOptions.None, token);
            Assert.Equal(expected, result);
            bytes.AsSpan().Fill((byte)'q');
            Assert.Equal(expected, result);
            Assert.False(Pcre2ReplacementPreparationControl.IsActive);
        }
    }

    public static IEnumerable<object[]> Cutoffs()
    {
        foreach (var working in new[] { false, true })
        foreach (var text in new[] { false, true })
        foreach (var hit in new[] { false, true })
        foreach (var liveToken in new[] { false, true })
            yield return [working, text, hit, liveToken];
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void PublicWorkAndHeapCutoffsIncludeTheRequestedFinalRepresentation(bool working, bool text, bool hit, bool liveToken)
    {
        using var source = new CancellationTokenSource();
        var token = liveToken ? source.Token : CancellationToken.None;
        var input = Encoding.UTF8.GetBytes(hit ? "é😀é😀" : new string('q', 8192));
        var literal = new string('a', 4097);
        var template = literal + "$2";
        var expected = hit ? literal + "😀" + literal + "😀" : Encoding.UTF8.GetString(input);
        string Invoke(ulong cap)
        {
            var regex = Create(new() { MatchLimit = working ? 0 : (uint)cap, HeapLimitInBytes = working ? cap : 0 });
            try
            {
                return text ? regex.ReplaceToString(input, template, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token)
                    : Encoding.UTF8.GetString(regex.Replace(input, template, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
            }
            finally { Assert.False(Pcre2ReplacementPreparationControl.IsActive); }
        }
        var kind = working ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.MatchLimit;
        Assert.Equal(expected, Invoke(working ? ulong.MaxValue : uint.MaxValue));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(1)).ErrorKind);
        ulong first = 1, last = 1UL << 20;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Invoke(cap)); last = cap; }
            catch (Pcre2MatchException failure) { Assert.Equal(kind, failure.ErrorKind); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind);
        Assert.Equal(expected, Invoke(first));
    }

    public static IEnumerable<object[]> StringOverlap()
    {
        foreach (var text in new[] { "a", "é😀", new string('a', 4097) + "😀é" })
        foreach (var allow in new[] { false, true }) yield return [text, allow];
    }

    [Theory]
    [MemberData(nameof(StringOverlap))]
    public void FinalStringAdmitsItsExactOverlapWithStagingAndPreservesParentLoans(string text, bool allow)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var staging = Utf8ConstructionBudget.ArrayBytes<byte>(input.Length);
        var extent = Utf8ConstructionBudget.StringBytes(text.Length);
        var invocation = new Utf8SliceInvocation(new(0, 0, 7 + staging + extent - (allow ? 0UL : 1UL)),
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(7));
        var writer = new Pcre2ReplacementTemplateWriter(input, [], invocation);
        try
        {
            writer.AppendGap(0, input.Length);
            if (allow)
            {
                var result = writer.CompleteToString();
                Assert.Equal(text, result);
                Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
                input.AsSpan().Fill((byte)'q');
                Assert.Equal(text, result);
            }
            else
            {
                Pcre2MatchException? failure = null;
                try { writer.CompleteToString(); }
                catch (Pcre2MatchException error) { failure = error; }
                Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
                Assert.Equal(7 + staging, invocation.Ledger.ReservedBytes);
                Assert.Equal(input.Length, writer.Length);
                failure = null;
                try { writer.Complete(); }
                catch (Pcre2MatchException error) { failure = error; }
                Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            }
            Assert.Equal(7 + staging + extent, invocation.Budget.PeakWorkingBytes);
        }
        finally { writer.Release(); writer.Release(); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NoMatchStringAdmitsOnlyItsReturnedRootAndKeepsTheParent(bool allow)
    {
        var text = "😀" + new string('a', 4097) + "é";
        var input = Encoding.UTF8.GetBytes(text);
        var extent = Utf8ConstructionBudget.StringBytes(text.Length);
        var invocation = new Utf8SliceInvocation(new(0, 0, extent + 7 - (allow ? 0UL : 1UL)),
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(7));
        if (allow)
        {
            var result = Pcre2ReplacementTemplateWriter.DecodeUnchanged(input, ref invocation);
            Assert.Equal(text, result);
            input.AsSpan().Fill((byte)'q');
            Assert.Equal(text, result);
        }
        else Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(
            () => Pcre2ReplacementTemplateWriter.DecodeUnchanged(input, ref invocation)).ErrorKind);
        Assert.Equal(extent + 7, invocation.Budget.PeakWorkingBytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalProjectionCannotResetSpentDiscoveryWork(bool noMatch)
    {
        var invocation = new Utf8SliceInvocation(new(100, 0, 1000000), Timeout.InfiniteTimeSpan, CancellationToken.None);
        if (noMatch)
        {
            while (invocation.Budget.Steps < 100) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(
                () => Pcre2ReplacementTemplateWriter.DecodeUnchanged("😀"u8, ref invocation)).ErrorKind);
        }
        else
        {
            var writer = new Pcre2ReplacementTemplateWriter("😀"u8, [], invocation);
            try
            {
                writer.AppendGap(0, 4);
                while (invocation.Budget.Steps < 100) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
                Pcre2MatchException? failure = null;
                try { writer.CompleteToString(); }
                catch (Pcre2MatchException error) { failure = error; }
                Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            }
            finally { writer.Release(); }
        }
        Assert.Equal(101UL, invocation.Budget.Steps);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void DiscoveryDeadlineAndOriginalTokenSurviveFinalProjection(bool hit, bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = Create(new() { HeapLimitInBytes = 1000000 }, TimeSpan.FromMilliseconds(20));
        var direct = (Pcre2SliceDirectProgram)regex.DebugCompiledProgram.Operations.Replace;
        var input = Encoding.UTF8.GetBytes(hit ? "é😀" : "zzz");
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, Utf8ValidatedInput.Create(input),
            new Utf8BytePosition(0), regex.DebugCompiledProgram.Request, source.Token);
        Assert.Equal(hit, cursor.MoveNext());
        ticks = 15;
        var writer = hit ? new Pcre2ReplacementTemplateWriter(input, [], cursor.WorkingInvocation) : default;
        if (hit) writer.AppendGap(0, input.Length);
        if (cancel) source.Cancel(); else ticks = 25;
        Exception? failure = null;
        try
        {
            if (hit) writer.CompleteToString();
            else Pcre2ReplacementTemplateWriter.DecodeUnchanged(input, ref cursor.WorkingInvocation);
        }
        catch (Exception error) { failure = error; }
        finally { writer.Release(); }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(0UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void SharedStringDecoderKeepsCumulativeResultTrafficAfterWorkingRelease()
    {
        var input = "😀é"u8.ToArray();
        var extent = Utf8ConstructionBudget.StringBytes(3);
        var invocation = new Utf8SliceInvocation(new(0, 0, extent + 7, extent * 2),
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(7));
        Assert.Equal("😀é", Utf8SliceBufferWork.DecodeUtf8(input, ref invocation));
        Assert.Equal("😀é", Utf8SliceBufferWork.DecodeUtf8(input, ref invocation));
        Assert.Equal(extent * 2, invocation.Budget.ResultBytes);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => Utf8SliceBufferWork.DecodeUtf8(input, ref invocation));
        Assert.Equal(extent * 3, invocation.Budget.ResultBytes);
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.Budget.LimitBreach);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void LargeStringIsDeniedBeforeItsPayloadAllocation()
    {
        var input = new byte[1000000]; input.AsSpan().Fill((byte)'a');
        var staging = Utf8ConstructionBudget.ArrayBytes<byte>(input.Length);
        var extent = Utf8ConstructionBudget.StringBytes(input.Length);
        void Deny(bool measure)
        {
            var invocation = new Utf8SliceInvocation(new(0, 0, staging + extent - 1), Timeout.InfiniteTimeSpan, CancellationToken.None);
            var writer = new Pcre2ReplacementTemplateWriter(input, [], invocation);
            writer.AppendGap(0, input.Length);
            var before = GC.GetAllocatedBytesForCurrentThread();
            Pcre2MatchException? failure = null;
            try { writer.CompleteToString(); }
            catch (Pcre2MatchException error) { failure = error; }
            finally { writer.Release(); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            if (measure) Assert.InRange(allocated, 0, 65536);
            Assert.Equal(staging + extent, invocation.Budget.PeakWorkingBytes);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Deny(false); Deny(true);
    }

    [Fact]
    public void FinalStringDoesNotAcquireAnotherEncodedByteArray()
    {
        var input = new byte[1000000]; input.AsSpan().Fill((byte)'a');
        string Complete(bool measure)
        {
            var writer = new Pcre2ReplacementTemplateWriter(input, [],
                new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 0),
                    Timeout.InfiniteTimeSpan, CancellationToken.None));
            writer.AppendGap(0, input.Length);
            var before = GC.GetAllocatedBytesForCurrentThread();
            string result;
            try { result = writer.CompleteToString(); }
            finally { writer.Release(); }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(0UL, writer.ReservedWorkingBytes);
            if (measure) Assert.InRange(allocated, 2L * input.Length, (long)Utf8ConstructionBudget.StringBytes(input.Length) + 65536);
            return result;
        }
        Complete(false);
        var actual = Complete(true);
        Assert.Equal(input.Length, actual.Length);
        Assert.Equal('a', actual[0]); Assert.Equal('a', actual[^1]);
    }

    [Fact]
    public void FinalStringRejectsInvalidByteFragmentsWithoutDiscardingTheStagingLoan()
    {
        var input = new byte[] { 0xf0 };
        var invocation = new Utf8SliceInvocation(new(0, 0, 1000), Timeout.InfiniteTimeSpan, CancellationToken.None);
        var writer = new Pcre2ReplacementTemplateWriter(input, [], invocation);
        try
        {
            writer.AppendGap(0, 1);
            ArgumentException? failure = null;
            try { writer.CompleteToString(); }
            catch (ArgumentException error) { failure = error; }
            Assert.NotNull(failure);
            Assert.Equal(Utf8ExecutionLimitKind.None, invocation.Budget.LimitBreach);
            Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(1), invocation.Ledger.ReservedBytes);
            Assert.Equal(input, writer.Complete());
        }
        finally { writer.Release(); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void StrictSubjectOffsetsAndCanceledTokensKeepTheirEntryOrder(bool text)
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var regex = Create(new() { HeapLimitInBytes = 1 });
        string Invoke(byte[] input, int start, CancellationToken token)
            => text ? regex.ReplaceToString(input, "$2", start, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token)
                : Encoding.UTF8.GetString(regex.Replace(input, "$2", start, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => Invoke([0xff], 0, source.Token)).CancellationToken);
        Assert.Throws<ArgumentException>(() => Invoke([0xff], 0, CancellationToken.None));
        Assert.Throws<ArgumentOutOfRangeException>(() => Invoke("é😀"u8.ToArray(), 3, CancellationToken.None));
        Assert.False(Pcre2ReplacementPreparationControl.IsActive);
    }
}
