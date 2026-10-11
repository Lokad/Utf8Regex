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
    public void ScalarOutputsKeepPythonShapesRangesAndOwnedValues(string pattern, string text, string golden)
    {
        using var document = JsonDocument.Parse(golden);
        var expected = document.RootElement;
        var shape = Enum.Parse<Utf8PythonFindAllShape>(expected.GetProperty("shape").GetString()!);
        var scalarValues = Strings(expected.GetProperty("scalars"));
        var tupleValues = expected.GetProperty("tuples").EnumerateArray().Select(Strings).ToArray();
        var splitValues = Strings(expected.GetProperty("split"));
        var ranges = expected.GetProperty("matches").EnumerateArray().ToArray();
        var captures = shape == Utf8PythonFindAllShape.FullMatch ? 0 : shape == Utf8PythonFindAllShape.SingleGroup ? 1 : tupleValues[0].Length;
        using var source = new CancellationTokenSource();
        foreach (var bytesPattern in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxResultBytes = 1000000 } : new Utf8ResourcePolicy();
            var regex = bytesPattern ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
                : new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            var input = Encoding.UTF8.GetBytes(text);
            var matches = regex.FindAll(input, 0, token);
            Assert.Equal(ranges.Length, matches.Length);
            for (var i = 0; i < matches.Length; i++)
            {
                Assert.True(matches[i].Success);
                Assert.Equal(ranges[i][0].GetString(), matches[i].ValueText);
                Assert.Equal(ranges[i][1].GetInt32(), matches[i].StartOffsetInBytes);
                Assert.Equal(ranges[i][2].GetInt32(), matches[i].EndOffsetInBytes);
                Assert.Equal(ranges[i][3].GetInt32(), matches[i].StartOffsetInUtf16);
                Assert.Equal(ranges[i][4].GetInt32(), matches[i].EndOffsetInUtf16);
            }
            var strings = regex.FindAllToStrings(input, 0, token);
            var bytes = regex.FindAllToUtf8(input, 0, token);
            var split = regex.SplitToStrings(input, 0, 0, token);
            var detailed = regex.SplitDetailed(input, 0, 0, token);
            Assert.Equal(shape, strings.Shape); Assert.Equal(shape, bytes.Shape);
            Assert.Equal(splitValues, split);
            Assert.Equal(splitValues, detailed.Select(x => x.ValueText));
            for (var i = 0; i < detailed.Length; i++)
            {
                var group = i % (captures + 1);
                Assert.Equal(group > 0, detailed[i].IsCapture);
                Assert.Equal(group, detailed[i].CaptureGroupNumber);
            }
            input.AsSpan().Fill((byte)'x');
            Assert.Equal(scalarValues, strings.ScalarValues);
            Assert.Equal(tupleValues, strings.TupleValues);
            Assert.Equal(scalarValues, bytes.ScalarValues.Select(Encoding.UTF8.GetString));
            Assert.Equal(tupleValues, bytes.TupleValues.Select(t => t.Select(Encoding.UTF8.GetString).ToArray()).ToArray());
            Assert.Equal(ranges.Select(x => x[0].GetString()), matches.Select(x => x.ValueText));
            Assert.Equal(splitValues, split);
        }
    }

    [Fact]
    public void MappedValueSnapshotsHaveBoundedWorkWithoutPrefixRescans()
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("😀é", 256)));
        var cursor = new PythonReScalarMatchCursor(input, program, [0, 1], 0,
            Regex.InfiniteMatchTimeout, "(.)", CancellationToken.None, new(1000000, 0, 0, 1000000));
        try
        {
            var count = 0;
            while (cursor.MoveNext())
            {
                cursor.EnsureMap();
                var traffic = cursor.WorkingInvocation.Budget.ResultBytes;
                var storage = cursor.ReservedBytes;
                var steps = cursor.WorkingInvocation.Budget.Steps;
                var value = cursor.SnapshotValueData();
                Assert.True(cursor.HasCoordinateMap);
                Assert.Equal(traffic, cursor.WorkingInvocation.Budget.ResultBytes);
                Assert.Equal(storage, cursor.ReservedBytes);
                Assert.InRange(cursor.WorkingInvocation.Budget.Steps - steps, 1UL, 8UL);
                var pair = count / 2;
                var second = count % 2 != 0;
                Assert.Equal(6 * pair + (second ? 4 : 0), value.StartOffsetInBytes);
                Assert.Equal(6 * pair + (second ? 6 : 4), value.EndOffsetInBytes);
                Assert.Equal(3 * pair + (second ? 2 : 0), value.StartOffsetInUtf16);
                Assert.Equal(3 * pair + (second ? 3 : 2), value.EndOffsetInUtf16);
                count++;
            }
            Assert.Equal(512, count);
        }
        finally { cursor.Dispose(); }
        Assert.Equal(0UL, cursor.ReservedBytes);
    }
}
