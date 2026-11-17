using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReResultExtentTests
{
    private static ulong ArrayBytes<T>(int count) => count == 0 ? 0 : 32UL + (((ulong)Unsafe.SizeOf<T>() * (ulong)count + 15) & ~15UL);

    [Fact]
    public void TemporaryGroupRootsReleaseAfterEachLastRead()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes(new string('é', 1025));
        var cursor = new PythonReScalarMatchState(input, program, 0,
            Regex.InfiniteMatchTimeout,
            "(.)", new(new(0, 0, 1UL << 20), Regex.InfiniteMatchTimeout, CancellationToken.None), null);
        try
        {
            Assert.True(cursor.MoveNext(input));
            var count = 0;
            do
            {

                var positioned = cursor.ReservedBytes;
                var marker = cursor.ResultReservation;
                var groups = cursor.SnapshotRanges(input, []);
                Assert.Equal(positioned + ArrayBytes<Utf8PythonCaptureRange>(2), cursor.ReservedBytes);
                Assert.Equal(2 * count, groups.Groups[1].StartOffsetInBytes);
                var resultTraffic = cursor.WorkingInvocation.Budget.ResultBytes;
                cursor.ReleaseResultsSince(marker);
                Assert.Equal(positioned, cursor.ReservedBytes);
                Assert.Equal(marker, cursor.ResultReservation);
                Assert.Equal(resultTraffic, cursor.WorkingInvocation.Budget.ResultBytes);
                count++;
            } while (cursor.MoveNext(input));
            Assert.Equal(1025, count);
            Assert.True(cursor.ReservedBytes < 65536); // Inline discovery retains bounded scratch until disposal.
        }
        finally { cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
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
            if (expire) Assert.Throws<RegexMatchTimeoutException>(() => PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)));
            else Assert.Throws<PublicationObservedException>(() => PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)));
            Assert.True(entered);
        }
        finally { diagnostics.Complete(); }
    }

    private sealed class PublicationObservedException : Exception { }
}
