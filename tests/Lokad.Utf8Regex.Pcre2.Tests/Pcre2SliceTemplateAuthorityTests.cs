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
        var regex = new Utf8Pcre2Regex("(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
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
    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null,
        string pattern = "(é)(😀)")
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
            },
            MatchTimeout = timeout ?? Timeout.InfiniteTimeSpan
        });
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
    [InlineData("(é)(😀)", "xé😀xé😀", "$2$1", Pcre2SubstitutionOptions.None, 0, "x😀éx😀é")]
    public void OwnedByteTemplatesKeepTheirSelectedScalarOutcomes(string pattern, string input,
        string template, Pcre2SubstitutionOptions options, int start, string expected)
    {
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var regex = Create(new() { MatchLimit = 1000000, HeapLimitInBytes = 1000000 }, pattern: pattern);
            var bytes = Encoding.UTF8.GetBytes(input);
            var result = regex.Replace(bytes, template, start, options, Pcre2MatchOptions.None, token);
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            Assert.Equal(expectedBytes, result);
            if (bytes.Length != 0 && input == expected) Assert.NotSame(bytes, result);
            bytes.AsSpan().Fill((byte)'q');
            Assert.Equal(expectedBytes, result);
            Assert.False(Pcre2ReplacementPreparationControl.IsActive);
        }
    }

    public static IEnumerable<object[]> Cutoffs()
    {
        foreach (var working in new[] { false, true })
        foreach (var hit in new[] { false, true })
        foreach (var liveToken in new[] { false, true })
            yield return [working, hit, liveToken];
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void PublicWorkAndHeapCutoffsIncludeFinalByteOutput(bool working, bool hit, bool liveToken)
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
                return Encoding.UTF8.GetString(regex.Replace(input, template, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, token));
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
}
