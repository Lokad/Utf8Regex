using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SliceTemplateAuthorityTests
{
    [Theory]
    [InlineData("(é)(😀)", "xé😀xé😀", "$2$1", "x😀éx😀é")]
    [InlineData("(é)(😀)", "nothing", "$2$1", "nothing")]
    [InlineData("(é)(😀)", "", "$2$1", "")]
    [InlineData("(?<=abc)(|def)", "abcdefabc😀", "<$1>", "abc<><def>abc<>😀")]
    public void PublicSliceTemplatesKeepScalarValuesAndOwnedRoots(string pattern, string text, string replacement, string expected)
    {
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, Regex.InfiniteMatchTimeout);
            Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
            var input = Encoding.UTF8.GetBytes(text);
            var result = regex.Replace(input, replacement, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token);
            Assert.Equal(expected, Encoding.UTF8.GetString(result));
            if (input.Length != 0 && text == expected) Assert.NotSame(input, result);
            input.AsSpan().Fill((byte)'x');
            Assert.Equal(expected, Encoding.UTF8.GetString(result));
            Assert.Equal(expected, regex.ReplaceToString(Encoding.UTF8.GetBytes(text), replacement));
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicSliceWorkCutoffsIncludeWritingAndTheUnchangedCopy(bool hit)
    {
        var input = Encoding.UTF8.GetBytes(hit ? "é😀é😀" : "zzz");
        var expected = Encoding.UTF8.GetBytes(hit ? "😀é😀é" : "zzz");
        byte[] Run(uint cap) => new Utf8Pcre2Regex("(é)(😀)", Pcre2CompileOptions.None, default,
            new() { MatchLimit = cap }, Regex.InfiniteMatchTimeout).Replace(input, "$2$1");
        uint first = 1, last = 100000;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Run(cap)); last = cap; }
            catch (Pcre2MatchException error) { Assert.Equal(Pcre2ErrorKind.MatchLimit, error.ErrorKind); first = cap + 1; }
        }
        Assert.True(first > 1);
        Assert.Equal(expected, Run(first));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => Run(first - 1)).ErrorKind);
        Assert.Equal(expected, Run(first));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnchangedRootHasExactAdmissionAndLeavesParentLoans(bool allow)
    {
        var input = new byte[1024]; input.AsSpan().Fill(42);
        var extent = Utf8ConstructionBudget.ArrayBytes<byte>(input.Length);
        var invocation = new Utf8SliceInvocation(new(0, 0, extent + 7 - (allow ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, new Utf8InvocationBufferControl(ref invocation).TryReserveWorking(7));
        if (allow)
        {
            var result = Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref invocation);
            Assert.NotSame(input, result); Assert.Equal(input, result);
            input.AsSpan().Clear(); Assert.All(result, static value => Assert.Equal((byte)42, value));
            Assert.Equal(extent + 7, invocation.Budget.PeakWorkingBytes);
        }
        else Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(
            () => Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref invocation)).ErrorKind);
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void LargeUnchangedRootIsDeniedBeforeAcquisition()
    {
        var input = new byte[1000000];
        var warm = new Utf8SliceInvocation(new(0, 0, 4096), Regex.InfiniteMatchTimeout, CancellationToken.None);
        try { _ = Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref warm); } catch (Pcre2MatchException) { }
        var invocation = new Utf8SliceInvocation(new(0, 0, 4096), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var before = GC.GetAllocatedBytesForCurrentThread();
        Pcre2MatchException? failure = null;
        try { _ = Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref invocation); }
        catch (Pcre2MatchException error) { failure = error; }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.InRange(allocated, 0, 16384);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void BorrowedWriterSharesHeapOverlapAndReleasesOnlyItsOwnLoans()
    {
        var extent = Utf8ConstructionBudget.ArrayBytes<byte>(2);
        var invocation = new Utf8SliceInvocation(new(0, 0, 2 * extent + 7), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, new Utf8InvocationBufferControl(ref invocation).TryReserveWorking(7));
        var writer = new Pcre2ReplacementTemplateWriter("ab"u8, [], invocation);
        byte[] result;
        try
        {
            Assert.Equal(extent + 7, invocation.Ledger.ReservedBytes);
            writer.AppendGap(0, 2); result = writer.Complete();
            Assert.Equal(2 * extent + 7, invocation.Budget.PeakWorkingBytes);
        }
        finally { writer.Release(); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("ab"u8.ToArray(), result);
        invocation.Ledger.Release(7);
    }

    [Fact]
    public void BorrowedWorkCannotResetAtWriterCreationOrInsideCopying()
    {
        var spent = new Utf8SliceInvocation(new(6, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        for (var i = 0; i < 6; i++) Assert.Equal(Utf8ExecutionLimitKind.None, spent.TryChargeInputStep());
        Pcre2MatchException? failure = null;
        try { var denied = new Pcre2ReplacementTemplateWriter("ab"u8, [], spent); denied.Release(); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(7UL, spent.Budget.Steps); Assert.Equal(0UL, spent.Ledger.ReservedBytes);
        var partial = new Utf8SliceInvocation(new(3, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var writer = new Pcre2ReplacementTemplateWriter(new byte[8192], [], partial);
        failure = null;
        try { writer.AppendGap(0, 8192); }
        catch (Pcre2MatchException error) { failure = error; }
        finally { writer.Release(); }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(4UL, partial.Budget.Steps); Assert.Equal(0UL, partial.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DiscoveryDeadlineSurvivesWriterCreationAndEof(bool match)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var regex = new Utf8Pcre2Regex("(é)(😀)", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
        var direct = Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        var input = Encoding.UTF8.GetBytes(match ? "é😀" : "zzz");
        var cursor = new Pcre2SliceGlobalMatchCursor(direct.SliceProgram, Utf8ValidatedInput.Create(input),
            new Utf8BytePosition(0), regex.DebugCompiledProgram.Request, CancellationToken.None);
        Assert.Equal(match, cursor.MoveNext());
        ticks = 15;
        if (match)
        {
            var writer = new Pcre2ReplacementTemplateWriter(input, [], cursor.WorkingInvocation);
            ticks = 25;
            Pcre2MatchException? failure = null;
            try { writer.AppendGap(0, input.Length); }
            catch (Pcre2MatchException error) { failure = error; }
            finally { writer.Release(); }
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
        else
        {
            ticks = 25;
            Pcre2MatchException? failure = null;
            try { _ = Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref cursor.WorkingInvocation); }
            catch (Pcre2MatchException error) { failure = error; }
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        }
        Assert.Equal(0UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void OriginalCancellationPrecedesCloneHeapDenial()
    {
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(0, 0, 1), Regex.InfiniteMatchTimeout, source.Token);
        source.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() => Pcre2ReplacementTemplateWriter.CopyUnchanged(new byte[10000], ref invocation));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }
}
