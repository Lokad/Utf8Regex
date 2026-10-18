using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReDetailedPublicationContractTests
{
    [Theory]
    [InlineData("(?P<outer>(?P<face>😀)(é)?)(x)?", "xx😀é yy😀", 2, 1, "outer")]
    [InlineData("(?<=(?P<before>😀))(?P<body>é)", "😀é", 1, 2, "body")]
    [InlineData("(?P<empty>)(?P<next>😀)?", "😀", 2, 2, "next")]
    public void DetailedSnapshotsKeepScalarValuesCoordinatesAndOwnership(
        string pattern, string subject, int expectedCount, int firstLastIndex, string firstLastGroup)
    {
        var input = Encoding.UTF8.GetBytes(subject);
        var unlimited = new Utf8PythonRegex(pattern).FindIterDetailed(input);
        var finiteRegex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 });
        var finite = finiteRegex.FindIterDetailed(input);
        Assert.Equal(expectedCount, finite.Length);
        Assert.Equal(firstLastIndex, finite[0].LastIndex);
        Assert.Equal(firstLastGroup, finite[0].LastGroup);
        for (var match = 0; match < finite.Length; match++)
        {
            Assert.Equal(unlimited[match].Groups, finite[match].Groups);
            foreach (var group in finite[match].Groups)
            {
                if (!group.Success) { Assert.Equal(string.Empty, group.ValueText); continue; }
                Assert.Equal(group.ValueText, Encoding.UTF8.GetString(input.AsSpan(
                    group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes)));
                Assert.Equal(subject[..group.StartOffsetInUtf16], Encoding.UTF8.GetString(input.AsSpan(0, group.StartOffsetInBytes)));
                Assert.Equal(group.ValueText, subject[group.StartOffsetInUtf16..group.EndOffsetInUtf16]);
            }
        }
        using var cursor = finiteRegex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        if (expectedCount > 1)
        {
            Assert.True(cursor.MoveNext());
            Assert.NotSame(retained.Groups, cursor.Current.Groups);
            Assert.NotSame(retained.NameEntries, cursor.Current.NameEntries);
        }
        else Assert.False(cursor.MoveNext());
        cursor.Dispose();
        Assert.Equal(finite[0].Groups, retained.Groups);
        retained.Groups[0] = default;
        retained.NameEntries[0] = default;
        Assert.True(finiteRegex.FindIterDetailed(input)[0].Success);
    }

    [Fact]
    public void EveryDetailedWorkCutoffPreservesDenialAndCallerStorage()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(😀)(é?)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        var input = "😀é"u8.ToArray();
        var measured = new PythonReScalarMatchCursor(input, program, 0,
            Regex.InfiniteMatchTimeout, "(😀)(é?)", default, new(0, 0, 0, 1048576));
        ulong work, extent;
        try
        {
            Assert.True(measured.MoveNext());
            measured.EnsureMap();
            var beforeWork = measured.WorkingInvocation.Budget.Steps;
            var beforeBytes = measured.ReservedBytes;
            _ = measured.SnapshotDetailed([]);
            work = measured.WorkingInvocation.Budget.Steps;
            extent = measured.WorkingInvocation.Budget.ResultBytes;
            var detailedWork = work - beforeWork;
            var detailedWorking = measured.ReservedBytes - beforeBytes;
            var standalone = new PythonReScalarMatchCursor(input, program, 0,
                Regex.InfiniteMatchTimeout, "(😀)(é?)", default, new(0, 0, 0, 1048576));
            try
            {
                Assert.True(standalone.MoveNext()); standalone.EnsureMap();
                var initialWork = standalone.WorkingInvocation.Budget.Steps;
                var initialBytes = standalone.ReservedBytes;
                standalone.Checkpoint();
                _ = standalone.AllocateResultArray<Utf8PythonGroupMatchData>(3);
                foreach (var range in new[] { (0, input.Length), (0, 4), (4, input.Length) })
                {
                    standalone.Checkpoint();
                    _ = standalone.GetText(range.Item1, range.Item2);
                }
                _ = standalone.SnapshotNameEntries([]);
                standalone.Checkpoint();
                Assert.Equal(detailedWork, standalone.WorkingInvocation.Budget.Steps - initialWork);
                Assert.Equal(extent, standalone.WorkingInvocation.Budget.ResultBytes);
                Assert.Equal(detailedWorking, standalone.ReservedBytes - initialBytes);
            }
            finally { standalone.Dispose(); }
        }
        finally { measured.Dispose(); }
        for (ulong cap = 1; cap <= work; cap++)
        {
            PythonReScalarMatchCursor cursor = default;
            var constructed = false;
            var reserved = false;
            try
            {
                cursor = new(input, program, 0, Regex.InfiniteMatchTimeout,
                    "(😀)(é?)", default, new(cap, 0, 0, 1048576));
                constructed = true;
                Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
                reserved = true;
                Assert.True(cursor.MoveNext());
                var snapshot = cursor.SnapshotDetailed([]);
                Assert.Equal(work, cap);
                Assert.Equal(extent, cursor.WorkingInvocation.Budget.ResultBytes);
                Assert.Equal("😀é", snapshot.Value.ValueText);
            }
            catch (Utf8ResourceLimitExceededException error)
            {
                Assert.True(cap < work);
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
            }
            finally
            {
                if (constructed) cursor.Dispose();
                if (reserved) Assert.Equal(13UL, cursor.ReservedBytes);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DetailedDecodePollsOriginalAuthorityAndReleasesOwnedScratch(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.+)");
        // One decode alone must cross the diagnostic hook's 64-step interval;
        // sharing equal capture text must not make the injection depend on a
        // second redundant decode or on the VM's preceding step count.
        var input = Encoding.UTF8.GetBytes(new string('é', 150000));
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var cursor = new PythonReScalarMatchCursor(input, core.Inspection.AvailableOwnedSliceProgram,
            0, timeout, "(.+)", token.Token, new(0, 0, 64UL << 20, 1048576));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        try
        {
            Assert.True(cursor.MoveNext()); cursor.EnsureMap();
            cursor.WorkingInvocation.ShareStorageForCursor();
            var observer = cursor.WorkingInvocation;
            var initialBytes = cursor.ReservedBytes;
            var entered = false;
            cursor.WorkingInvocation.CheckpointHook = _ =>
            {
                if (observer.Ledger.ReservedBytes > initialBytes + 1024)
                { entered = true; if (expire) ticks = 50; else token.Cancel(); }
            };
            Exception? failure = null;
            try { _ = cursor.SnapshotDetailed([]); }
            catch (Exception error) { failure = error; }
            Assert.True(entered);
            if (expire)
            {
                var error = Assert.IsType<RegexMatchTimeoutException>(failure);
                Assert.Equal("(.+)", error.Pattern); Assert.Equal(timeout, error.MatchTimeout);
            }
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(13UL, cursor.ReservedBytes);
        Assert.True(new Utf8PythonRegex("(.+)").SearchDetailedData(input).Success);
    }
}
