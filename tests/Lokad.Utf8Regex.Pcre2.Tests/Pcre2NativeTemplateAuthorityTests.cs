using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2NativeTemplateAuthorityTests
{
    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits, TimeSpan? timeout = null)
    {
        var regex = new Utf8Pcre2Regex("(*MARK:tag)(é)(😀)", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
            },
            MatchTimeout = timeout ?? Timeout.InfiniteTimeSpan
        });
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        return regex;
    }

    [Fact]
    public void UnchangedOutputIsDeniedBeforeMajorAcquisition()
    {
        var input = new byte[1 << 20]; input.AsSpan().Fill((byte)'z');
        var regex = Create(new() { HeapLimitInBytes = 4096 });
        Assert.False(regex.IsMatch(input, 0, Pcre2MatchOptions.Anchored));
        string Invoke() => Encoding.UTF8.GetString(regex.Replace(input, "$2$1"u8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored));
        try { _ = Invoke(); } catch (Pcre2MatchException) { }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var failure = Assert.Throws<Pcre2MatchException>(() => Invoke());
        var acquired = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
        Assert.InRange(acquired, 0, 16384);
        Assert.False(Pcre2ReplacementPreparationControl.IsActive);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void PublicWorkAndHeapCutoffsCoverFinalOutput(bool heap, bool hit)
    {
        var input = Encoding.UTF8.GetBytes(hit ? "é😀é😀" : new string('z', 8192));
        var literal = new string('x', 4097);
        var template = literal + "$2$1";
        var expected = hit ? literal + "😀é" + literal + "😀é" : Encoding.UTF8.GetString(input);
        string Invoke(ulong cap)
        {
            var regex = Create(new() { HeapLimitInBytes = heap ? cap : 0 });
            return Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(template)));
        }
        var kind = Pcre2ErrorKind.HeapLimit;
        ulong first = 1, last = 1UL << 20;
        Assert.Equal(expected, Invoke(last));
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Invoke(cap)); last = cap; }
            catch (Pcre2MatchException error) { Assert.Equal(kind, error.ErrorKind); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(kind, Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind);
        Assert.Equal(expected, Invoke(first));
    }

    [Fact]
    public void DiscoveryAndWriterShareCountersOverlapAndRetainedCaptures()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var regex = Create(new() { HeapLimitInBytes = 1000000 });
        var compiled = regex.DebugCompiledProgram;
        var native = (Pcre2BacktrackingDirectProgram)compiled.Operations.Replace;
        var input = "é😀é😀"u8.ToArray();
        var invocation = Pcre2SliceDriver.CreateInvocation(compiled.Request, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(7));
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(native.Program, compiled.CandidateSearchPlan,
            input, new(0), Pcre2MatchOptions.None, compiled.Request,
            new Pcre2ResourceBudget(compiled.Request.DefaultLimits, invocation, false));
        Assert.True(cursor.MoveNext());
        var retained = cursor.ProjectCurrentCaptures();
        var beforeWriting = invocation.Budget.DiagnosticCheckpoints;
        var discoveryPeak = invocation.Budget.PeakWorkingBytes;
        var preparation = new Pcre2ReplacementPreparation(null, Pcre2PreparedReplacementParser.Parse("$2$1"u8, Pcre2SubstitutionOptions.None));
        cursor.WorkingInvocation.ShareStorageForCursor();
        var writer = new Pcre2ReplacementTemplateWriter(input, [], cursor.WorkingInvocation);
        try
        {
            Assert.Equal(7 + Utf8ConstructionBudget.ArrayBytes<byte>(input.Length), invocation.Ledger.ReservedBytes);
            writer.AppendTemplate(preparation, Pcre2SubstitutionOptions.None, retained, 0, 6, cursor.Current.Mark);
            Assert.True(invocation.Budget.DiagnosticCheckpoints > beforeWriting);
            Assert.True(cursor.MoveNext());
            Assert.True(invocation.Budget.PeakWorkingBytes > discoveryPeak);
            var current = cursor.ProjectCurrentCaptures();
            Assert.NotSame(retained, current);
            writer.AppendTemplate(preparation, Pcre2SubstitutionOptions.None, current, 6, 6, cursor.Current.Mark);
            Assert.False(cursor.MoveNext());
            var result = Encoding.UTF8.GetString(writer.Complete());
            Assert.Equal("😀é😀é", result);
        }
        finally { writer.Release(); writer.Release(); }
        Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        Assert.Equal((2, 6), (retained[2].StartOffsetInBytes, retained[2].EndOffsetInBytes));
        invocation.Ledger.Release(7);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void OriginalDeadlineAndTokenSurviveDiscoveryAndPublication(bool hit, bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = Create(new() { HeapLimitInBytes = 1000000 }, TimeSpan.FromMilliseconds(20));
        var compiled = regex.DebugCompiledProgram;
        var native = (Pcre2BacktrackingDirectProgram)compiled.Operations.Replace;
        var input = Encoding.UTF8.GetBytes(hit ? "é😀" : "zzz");
        var invocation = Pcre2SliceDriver.CreateInvocation(compiled.Request, source.Token);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(native.Program, compiled.CandidateSearchPlan,
            input, new(0), Pcre2MatchOptions.None, compiled.Request,
            new Pcre2ResourceBudget(compiled.Request.DefaultLimits, invocation, false));
        Assert.Equal(hit, cursor.MoveNext());
        ticks = 15;
        var writer = hit ? new Pcre2ReplacementTemplateWriter(input, [], cursor.WorkingInvocation) : default;
        if (hit) writer.AppendGap(0, input.Length);
        if (cancel) source.Cancel(); else ticks = 25;
        Exception? failure = null;
        try
        {
            if (hit) _ = writer.Complete();
            else _ = Pcre2ReplacementTemplateWriter.CopyUnchanged(input, ref cursor.WorkingInvocation);
        }
        catch (Exception error) { failure = error; }
        finally { writer.Release(); }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("(*MARK:tag)(é)(😀)", "é😀xé😀β", "$2$1", Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, 7, "é😀x😀éβ")]
    [InlineData("(*MARK:tag)(é)(😀)", "é😀xé😀β", "$2$1", Pcre2SubstitutionOptions.SubstituteReplacementOnly, Pcre2MatchOptions.None, 0, "😀é😀é")]
    [InlineData("(*MARK:tag)(é)(😀)", "é😀", "${*MARK}:$2$1", Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, 0, "tag:😀é")]
    [InlineData("(*MARK:tag)(é)(😀)", "zzz", "${", Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, 0, "zzz")]
    [InlineData("(é)(😀)", "é😀xé😀β", "$2$1", Pcre2SubstitutionOptions.None, Pcre2MatchOptions.Anchored, 0, "😀éxé😀β")]
    [InlineData("(?<=abc)(|def)", "abcdefabc😀", "<$1>", Pcre2SubstitutionOptions.None, Pcre2MatchOptions.NotBol, 0, "abc<><def>abc<>😀")]
    public void NativeAndExplicitOptionFallbacksPreserveScalarTemplateResults(string pattern, string subject,
        string template, Pcre2SubstitutionOptions options, Pcre2MatchOptions matchOptions, int start, string expected)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1000000
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        if (matchOptions == Pcre2MatchOptions.None) Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        else Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        var input = Encoding.UTF8.GetBytes(subject);
        var bytes = regex.Replace(input, Encoding.UTF8.GetBytes(template), start, options, matchOptions, source.Token);
        var text = System.Text.Encoding.UTF8.GetString(regex.Replace(input, Encoding.UTF8.GetBytes(template), start, options, matchOptions, source.Token));
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes)); Assert.Equal(expected, text);
        input.AsSpan().Fill((byte)'z');
        Assert.Equal(expected, Encoding.UTF8.GetString(bytes)); Assert.Equal(expected, text);
        Assert.False(Pcre2ReplacementPreparationControl.IsActive);
    }
}
