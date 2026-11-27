using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReDirectShapingTests
{
    // CPython 3.13.2 stdlib goldens: supplementary scalars, captures outside
    // group zero, optional/unmatched captures, references and empty retry.
    public static IEnumerable<object[]> Cases()
    {
        yield return ["(?P<x>😀)|(é)", "😀é", """
            {"shape":"GroupTuple","scalars":[],"tuples":[["😀",""],["","é"]],"split":["","😀",null,"",null,"é",""],"matches":[["😀",0,4,0,2],["é",4,6,2,3]]}
            """];
        yield return ["(?<=(😀))(é)", "😀é", """
            {"shape":"GroupTuple","scalars":[],"tuples":[["😀","é"]],"split":["😀","😀","é",""],"matches":[["é",4,6,2,3]]}
            """];
        yield return ["((?:😀|é)+)", "😀é", """
            {"shape":"SingleGroup","scalars":["😀é"],"tuples":[],"split":["","😀é",""],"matches":[["😀é",0,6,0,3]]}
            """];
        yield return ["(?P<x>.)(?P=x)", "😀😀éé", """
            {"shape":"SingleGroup","scalars":["😀","é"],"tuples":[],"split":["","😀","","é",""],"matches":[["😀😀",0,8,0,4],["éé",8,12,4,6]]}
            """];
        yield return ["(😀)?(é)?", "😀é", """
            {"shape":"GroupTuple","scalars":[],"tuples":[["😀","é"],["",""]],"split":["","😀","é","",null,null,""],"matches":[["😀é",0,6,0,3],["",6,6,3,3]]}
            """];
        yield return [".*?", "😀é", """
            {"shape":"FullMatch","scalars":["","😀","","é",""],"tuples":[],"split":["","","","","",""],"matches":[["",0,0,0,0],["😀",0,4,0,2],["",4,4,2,2],["é",4,6,2,3],["",6,6,3,3]]}
            """];
    }

    private static string?[] Strings(JsonElement value) => value.EnumerateArray().Select(x => x.GetString()).ToArray();

    [Theory]
    [MemberData(nameof(Cases))]
    public void CursorOutputsKeepPythonCaptureValuesAndRanges(string pattern, string text, string golden)
    {
        using var document = JsonDocument.Parse(golden);
        var expected = document.RootElement;
        var scalarValues = Strings(expected.GetProperty("scalars"));
        var tupleValues = expected.GetProperty("tuples").EnumerateArray().Select(Strings).ToArray();
        var ranges = expected.GetProperty("matches").EnumerateArray().ToArray();
        using var source = new CancellationTokenSource();
        foreach (var bytesPattern in new[] { false, true })
            foreach (var finite in new[] { false, true })
                foreach (var token in new[] { CancellationToken.None, source.Token })
                {
                    var policy = finite ? new Utf8ResourcePolicy { MaxResultBytes = 1000000 } : new Utf8ResourcePolicy();
                    var regex = bytesPattern ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
                    {
                        MatchTimeout = Regex.InfiniteMatchTimeout,
                        ResourcePolicy = policy
                    }) : new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
                    {
                        MatchTimeout = Regex.InfiniteMatchTimeout,
                        ResourcePolicy = policy
                    });
                    var input = Encoding.UTF8.GetBytes(text);
                    var wholeMatches4 = new List<Utf8PythonRangeMatch>();
                    using (var rangeCursor4 = regex.CreateMatchCursor(input, 0, token))
                    {
                        while (rangeCursor4.MoveNext(out var rangeMatch4))
                        {
                            wholeMatches4.Add(rangeMatch4);
                        }
                    }
                    var matches = wholeMatches4.ToArray();
                    Assert.Equal(ranges.Length, matches.Length);
                    for (var i = 0; i < matches.Length; i++)
                    {
                        Assert.True(matches[i].Success);
                        Assert.Equal(ranges[i][0].GetString(), System.Text.Encoding.UTF8.GetString(matches[i].GetValueMemory().Span));
                        Assert.Equal(ranges[i][1].GetInt32(), matches[i].Value.StartOffsetInBytes);
                        Assert.Equal(ranges[i][2].GetInt32(), matches[i].Value.EndOffsetInBytes);
                    }
                    if (regex.CaptureGroupCount > 1)
                    {
                        Assert.Equal(tupleValues.Length, matches.Length);
                        for (var index = 0; index < matches.Length; index++)
                            for (var number = 1; number <= regex.CaptureGroupCount; number++)
                                Assert.Equal(tupleValues[index][number - 1], Encoding.UTF8.GetString(matches[index].GetGroupMemory(number).Span));
                    }
                    else
                        Assert.Equal(scalarValues, matches.Select(match => Encoding.UTF8.GetString(match.GetGroupMemory(regex.CaptureGroupCount).Span)));
                    Assert.Equal(ranges.Select(x => (x[1].GetInt32(), x[2].GetInt32())),
                        matches.Select(x => (x.Value.StartOffsetInBytes, x.Value.EndOffsetInBytes)));
                }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void RangeSplitsKeepPythonCapturePriorityAndLookaroundExtents(string pattern, string text, string golden)
    {
        using var document = JsonDocument.Parse(golden);
        var expected = Strings(document.RootElement.GetProperty("split"));
        using var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        var ranges = regex.SplitRanges(input);
        Assert.Equal(expected, PythonReSplitAssert.Decode(input, ranges));
        for (var index = 0; index < ranges.Length; index++)
            Assert.Equal(index % (regex.CaptureGroupCount + 1), ranges[index].Number);
    }

    [Fact]
    public void IndexedValueSnapshotsHaveBoundedWorkWithoutPrefixRescans()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀é", 256)));
        var cursor = new PythonReScalarMatchCursor(input, program, 0,
            Regex.InfiniteMatchTimeout,
            null, "(.)", new(new(0, 0, 1000000), Regex.InfiniteMatchTimeout, CancellationToken.None));
        try
        {
            var count = 0;
            while (cursor.MoveNext())
            {

                var traffic = cursor.WorkingInvocation.Budget.ResultBytes;
                var storage = cursor.ReservedBytes;
                var steps = cursor.WorkingInvocation.Budget.DiagnosticCheckpoints;
                var value = cursor.SnapshotValueData();
                Assert.Equal(traffic, cursor.WorkingInvocation.Budget.ResultBytes);
                Assert.Equal(storage, cursor.ReservedBytes);
                var pair = count / 2;
                var second = count % 2 != 0;
                Assert.Equal(6 * pair + (second ? 4 : 0), value.StartOffsetInBytes);
                Assert.Equal(6 * pair + (second ? 6 : 4), value.EndOffsetInBytes);
                count++;
            }
            Assert.Equal(512, count);
        }
        finally
        {
            cursor.Dispose();
        }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }
}
