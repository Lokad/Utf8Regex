using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReDetailedPublicationContractTests
{
    [Theory]
    [InlineData("(?P<outer>(?P<face>😀)(é)?)(x)?", "xx😀é yy😀", 2, 1)]
    [InlineData("(?<=(?P<before>😀))(?P<body>é)", "😀é", 1, 2)]
    [InlineData("(?P<empty>)(?P<next>😀)?", "😀", 2, 2)]
    [InlineData("(?P<outer>(?P<inner>😀))", "é😀 é😀", 2, 1)]
    [InlineData("(?P<outer>(?P<inner>))", "é😀", 3, 1)]
    [InlineData("(?P<key>[a-z]+)-(?P<value>[0-9]+)", "é abc-123 x-45", 2, 2)]
    public void DetailedSnapshotsKeepScalarValuesCoordinatesAndOwnership(
        string pattern, string subject, int expectedCount, int firstLastIndex)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = Encoding.UTF8.GetBytes(subject);
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = new Utf8PythonRegex(pattern).CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var unlimited = collectedRanges1.ToArray();
        var finiteRegex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 8192,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            }
        });
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = finiteRegex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        var finite = collectedRanges2.ToArray();
        Assert.Equal(expectedCount, finite.Length);
        Assert.Equal(expectedCount, unlimited.Length);
        Assert.Equal(firstLastIndex, finite[0].LastIndex);
        for (var match = 0; match < finite.Length; match++)
        {
            Assert.Equal(unlimited[match].LastIndex, finite[match].LastIndex);
            Assert.Equal(unlimited[match].Groups.ToArray(), finite[match].Groups.ToArray());
            foreach (var group in finite[match].Groups)
            {
                if (!group.Success) { Assert.Equal(string.Empty, System.Text.Encoding.UTF8.GetString(finite[match].GetGroupMemory(group.Number).Span)); continue; }
                Assert.Equal(System.Text.Encoding.UTF8.GetString(finite[match].GetGroupMemory(group.Number).Span), Encoding.UTF8.GetString(input.AsSpan(
                    group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes)));
            }
        }
        using var cursor = finiteRegex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        if (expectedCount > 1)
        {
            Assert.True(cursor.MoveNext(out cursorRange));
        }
        else Assert.False(cursor.MoveNext(out cursorRange));
        cursor.Dispose();
        PythonReCaptureAssert.Equal(finite[0], retained);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RetainedCursorSnapshotsKeepNumberingAndIndependentSlots(bool named)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var input = "é😀"u8.ToArray();
        var retained = new List<Utf8PythonRangeMatch>();
        using (var regex = new Utf8PythonRegex(named ? "(?P<outer>(?P<inner>.))" : "((.))", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 4096,
                MaxResultBytes = 4096
            }
        }))
        {
            using var cursor = regex.CreateMatchCursor(input);
            while (cursor.MoveNext(out cursorRange)) retained.Add(cursorRange);
        }

        Assert.Equal(2, retained.Count);
        foreach (var (match, ordinal) in retained.Select((value, index) => (value, index)))
        {
            Assert.Equal(1, match.LastIndex);
            Assert.Equal(3, match.Groups.Length);
            for (var group = 0; group < 3; group++)
            {
                var value = match.Groups[group];
                Assert.Equal(group, value.Number); Assert.True(value.Success); Assert.Equal(ordinal == 0 ? "é" : "😀", System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(group).Span));
            }
        }
        Assert.Equal("é", System.Text.Encoding.UTF8.GetString(retained[0].GetGroupMemory(1).Span)); Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained[1].GetValueMemory().Span));
        if (named)
        {
            Assert.True(retained[1].TryGetGroup(1, out var outer)); Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((outer.Success ? retained[1].GetGroupMemory(outer.Number) : default).Span));
        }
    }

    [Fact]
    public void EveryDetailedWorkCutoffPreservesDenialAndCallerStorage()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(😀)(é?)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        var input = "😀é"u8.ToArray();
        PythonReScalarMatchState.Initialize(out var measured, input, program, 0,
            Regex.InfiniteMatchTimeout,
            "(😀)(é?)", new(new(0, 0, 1048576), Regex.InfiniteMatchTimeout, default), null);
        ulong work, extent;
        try
        {
            Assert.True(measured.MoveNext(input));
            _ = measured.SnapshotRanges(input);
            work = measured.WorkingInvocation.Budget.DiagnosticCheckpoints;
            extent = measured.WorkingInvocation.Budget.ResultBytes;
        }
        finally { measured.Dispose(); }
    }
}
