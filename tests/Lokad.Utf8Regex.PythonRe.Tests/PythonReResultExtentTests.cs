using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReResultExtentTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);

    [Fact]
    public void DetailedGraphIncludesAllOwnedChildrenAndReleasesTemporaryGroups()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var cursor = new PythonReScalarMatchCursor("😀é"u8, program, [0, 1], 0,
            Regex.InfiniteMatchTimeout, "(.)", CancellationToken.None, new(0, 0, 1UL << 20));
        var names = Enumerable.Repeat(new PythonReNameEntry { Name = "value", Number = 1 }, 257).ToArray();
        Utf8PythonDetailedMatchData result;
        try
        {
            Assert.True(cursor.MoveNext());
            cursor.EnsureMap();
            var baseline = cursor.ReservedBytes;
            result = cursor.SnapshotDetailed(names);
            var graph = ArrayBytes<Utf8PythonGroupMatchData>(2) + ArrayBytes<PythonReNameEntry>(257) + 2 * 48UL;
            Assert.Equal(graph, cursor.ResultReservation);
            Assert.Equal(baseline + graph, cursor.ReservedBytes);
            Assert.Equal(baseline + graph + ArrayBytes<PythonReGroupData>(2), cursor.WorkingInvocation.Budget.PeakWorkingBytes);
            Assert.NotSame(names, result.NameEntries);
            Assert.Same(names[0].Name, result.NameEntries[0].Name);
            Assert.Equal("😀", result.Groups[1].ValueText);
            Assert.True(cursor.MoveNext());
            Assert.Equal("😀", result.Groups[1].ValueText);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
        Assert.Equal("😀", result.Groups[1].ValueText);
    }

    [Fact]
    public void TemporaryGroupRootsReleaseAfterEachLastRead()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes(new string('é', 1025));
        var cursor = new PythonReScalarMatchCursor(input, program, [0, 1], 0,
            Regex.InfiniteMatchTimeout, "(.)", CancellationToken.None, new(0, 0, 1UL << 20));
        try
        {
            cursor.EnsureMap();
            var baseline = cursor.ReservedBytes;
            var count = 0;
            while (cursor.MoveNext())
            {
                var groups = cursor.SnapshotGroups();
                Assert.Equal(baseline + ArrayBytes<PythonReGroupData>(2), cursor.ReservedBytes);
                Assert.Equal(2 * count, groups[1].StartOffsetInBytes);
                cursor.ReleaseResultArray(groups);
                Assert.Equal(baseline, cursor.ReservedBytes);
                Assert.Equal(0UL, cursor.ResultReservation);
                count++;
            }
            Assert.Equal(1025, count);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DecodedChildFailureUsesTheOriginalTokenOrProfileDeadline(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.+)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes(new string('é', 5000));
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var cursor = new PythonReScalarMatchCursor(input, program, [0, 1], 0,
            timeout, "(.+)", token.Token, new(0, 0, 1UL << 20));
        try
        {
            Assert.True(cursor.MoveNext()); cursor.EnsureMap();
            var baseline = cursor.ReservedBytes;
            ref var invocation = ref cursor.WorkingInvocation;
            while ((invocation.Budget.Steps & 63) != 60) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
            var entered = false;
            invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else token.Cancel(); };
            string? result = null;
            Exception? failure = null;
            try { result = cursor.GetText(0, input.Length); }
            catch (Exception error) { failure = error; }
            Assert.True(entered); Assert.Null(result);
            if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(baseline, cursor.ReservedBytes);
            Assert.Equal(0UL, cursor.ResultReservation);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CanonicalLiteralPublicationContinuesTheCountInvocation(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8PythonRegex("é", PythonReCompileOptions.None, timeout);
        var field = typeof(Utf8PythonRegex).GetField("_utf8Regex", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.NotNull(field);
        var core = Assert.IsType<global::Lokad.Utf8Regex.Utf8Regex>(field.GetValue(regex));
        var input = Encoding.UTF8.GetBytes(new string('é', 20000));
        var invocation = new Utf8SliceInvocation(new(0, 0, 1UL << 24), timeout, CancellationToken.None);
        invocation.CheckpointHook = _ => { };
        var count = core.CountForResult(input, 0, ref invocation);
        Assert.Equal(20000, count);
        var countSteps = invocation.Budget.Steps;
        var publicationHook = ((countSteps / 64) + 1) * 64;
        var entered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps >= publicationHook) { entered = true; if (expire) ticks = 50; else throw new PublicationObservedException(); }
        });
        try
        {
            if (expire) Assert.Throws<RegexMatchTimeoutException>(() => regex.FindAllToStrings(input));
            else Assert.Throws<PublicationObservedException>(() => regex.FindAllToStrings(input));
            Assert.True(entered);
        }
        finally { diagnostics.Complete(); }
    }

    private sealed class PublicationObservedException : Exception { }
}
