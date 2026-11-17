using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed partial class PythonReScalarGlobalTests
{
    [Theory]
    [MemberData(nameof(GlobalCases))]
    public void ScalarGlobalsMatchCpython(string pattern, string subject, int start, int limit,
        string replacement, string expectedJson)
        => AssertFrozenGlobalControls(pattern, subject, start, limit, replacement, expectedJson, PythonReCompileOptions.None);

    internal static void AssertFrozenGlobalControls(string pattern, string subject,
        int start, int limit, string replacement, string expectedJson, PythonReCompileOptions options)
    {
        using var expected = JsonDocument.Parse(expectedJson);
        var e = expected.RootElement;
        var input = Encoding.UTF8.GetBytes(subject);
        foreach (var live in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            var token = live ? source.Token : CancellationToken.None;
            var regex = new Utf8PythonRegex(pattern, options);
            var detailed = regex.FindIterDetailed(input, start, token);
            var values = e.GetProperty("matches").Deserialize<string[]>() ?? [];
            Assert.Equal(values, detailed.Select(static m => m.Value.ValueText));
            var wholeMatches13 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor13 = regex.CreateMatchCursor(input, start, token))
            {
                while (rangeCursor13.MoveNext(out var rangeMatch13))
                {
                    wholeMatches13.Add(rangeMatch13);
                }
            }
            Assert.Equal(values, wholeMatches13.ToArray().Select(static m => System.Text.Encoding.UTF8.GetString(m.GetValueMemory().Span)));
            Assert.Equal(values.Length, regex.Count(input, start, token));
            var ranges = e.GetProperty("byte_ranges");
            var expectedGroups = e.GetProperty("groups");
            for (var i = 0; i < detailed.Length; i++)
            {
                for (var g = 0; g < detailed[i].Groups.Length; g++)
                {
                    var group = detailed[i].Groups[g];
                    var startByte = ranges[i][g][0].GetInt32();
                    var endByte = ranges[i][g][1].GetInt32();
                    Assert.Equal(startByte >= 0, group.Success);
                    Assert.Equal(expectedGroups[i][g].GetString(), group.ValueText);
                    if (startByte < 0) continue;
                    Assert.Equal(startByte, group.StartOffsetInBytes);
                    Assert.Equal(endByte, group.EndOffsetInBytes);
                    Assert.True(startByte == input.Length || (input[startByte] & 0xc0) != 0x80);
                    Assert.True(endByte == input.Length || (input[endByte] & 0xc0) != 0x80);
                }
            }
            var utf8 = regex.FindAllToUtf8(input, start, token);
            var strings = PythonReFindAllAssert.Decode(utf8);
            var findall = e.GetProperty("findall");
            if (strings.Shape == Utf8PythonFindAllShape.GroupTuple)
            {
                var tuples = findall.Deserialize<string[][]>() ?? [];
                Assert.Equal(tuples.Length, strings.TupleValues.Length);
                for (var i = 0; i < tuples.Length; i++)
                {
                    Assert.Equal(tuples[i], strings.TupleValues[i]);
                }
            }
            else
            {
                var scalars = findall.Deserialize<string[]>() ?? [];
                Assert.Equal(scalars, strings.ScalarValues);
            }
            var split = e.GetProperty("split").Deserialize<string?[]>() ?? [];
            Assert.Equal(split, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, limit, start, token)));
            var resultText = e.GetProperty("replacement").GetString();
            var resultCount = e.GetProperty("replacement_count").GetInt32();
            Assert.Equal(resultText, Encoding.UTF8.GetString(regex.Replace(input, replacement, limit, start, token)));
            var bytes = regex.Subn(input, replacement, limit, start, token);
            Assert.Equal(resultText, Encoding.UTF8.GetString(bytes.ResultBytes));
            Assert.Equal(resultCount, bytes.ReplacementCount);

        }
    }

    [Theory]
    [InlineData(0)]  [InlineData(3)] [InlineData(4)]
    [InlineData(6)] [InlineData(8)] [InlineData(9)]
    public void CancellationInterruptsOwnedGlobalDiscovery(int operation)
    {
        var regex = new Utf8PythonRegex("(?P<x>.)+", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 256)));
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            entered = true;
            source.Cancel();
            if (steps >= 128) throw new InvalidOperationException("Global scalar operation dropped its token.");
        });
        try
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
            {
                switch (operation)
                {
                    case 0: regex.FindIterDetailed(input, 0, source.Token); break;
                    case 3: regex.FindAllToUtf8(input, 0, source.Token); break;
                    case 4: regex.SplitRanges(input, 0, 0, source.Token); break;
                    case 6: regex.Replace(input, "x", 0, 0, source.Token); break;
                    case 8: regex.Subn(input, "x", 0, 0, source.Token); break;
                    default: PythonReSubstitutionAssert.Decode(regex.Subn(input, "x", 0, 0, source.Token)); break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        finally { session.Complete(); }
        Assert.Single(regex.FindIterDetailed("\U0001f600\U0001f600"u8));
    }
}
