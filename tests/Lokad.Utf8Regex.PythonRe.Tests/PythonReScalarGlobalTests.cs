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
            Assert.Equal(values, regex.FindAll(input, start, token).Select(static m => m.ValueText));
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
                    Assert.Equal(Encoding.UTF8.GetCharCount(input.AsSpan(0, startByte)), group.StartOffsetInUtf16);
                    Assert.Equal(Encoding.UTF8.GetCharCount(input.AsSpan(0, endByte)), group.EndOffsetInUtf16);
                }
            }
            var strings = regex.FindAllToStrings(input, start, token);
            var utf8 = regex.FindAllToUtf8(input, start, token);
            var findall = e.GetProperty("findall");
            if (strings.Shape == Utf8PythonFindAllShape.GroupTuple)
            {
                var tuples = findall.Deserialize<string[][]>() ?? [];
                Assert.Equal(tuples.Length, strings.TupleValues.Length);
                for (var i = 0; i < tuples.Length; i++)
                {
                    Assert.Equal(tuples[i], strings.TupleValues[i]);
                    Assert.Equal(tuples[i], utf8.TupleValues[i].Select(Encoding.UTF8.GetString));
                }
            }
            else
            {
                var scalars = findall.Deserialize<string[]>() ?? [];
                Assert.Equal(scalars, strings.ScalarValues);
                Assert.Equal(scalars, utf8.ScalarValues.Select(Encoding.UTF8.GetString));
            }
            var split = e.GetProperty("split").Deserialize<string?[]>() ?? [];
            Assert.Equal(split, regex.SplitToStrings(input, limit, start, token));
            Assert.Equal(split, regex.SplitDetailed(input, limit, start, token).Select(static item => item.ValueText));
            var resultText = e.GetProperty("replacement").GetString();
            var resultCount = e.GetProperty("replacement_count").GetInt32();
            Assert.Equal(resultText, regex.ReplaceToString(input, replacement, limit, start, token));
            Assert.Equal(resultText, Encoding.UTF8.GetString(regex.Replace(input, replacement, limit, start, token)));
            var subn = regex.SubnToString(input, replacement, limit, start, token);
            Assert.Equal(resultText, subn.ResultText);
            Assert.Equal(resultCount, subn.ReplacementCount);
            var bytes = regex.Subn(input, replacement, limit, start, token);
            Assert.Equal(resultText, Encoding.UTF8.GetString(bytes.ResultBytes));
            Assert.Equal(resultCount, bytes.ReplacementCount);
            Utf8PythonMatchEvaluator<int> evaluator = static (_, match) => "<" + string.Join("|", match.Groups.Select(static group => group.ValueText)) + ">";
            Utf8PythonUtf8MatchEvaluator<int> utf8Evaluator = (_, match) => Encoding.UTF8.GetBytes(evaluator(0, match));
            Assert.Equal(resultText, regex.ReplaceToString(input, 0, evaluator, limit, start, token));
            Assert.Equal(resultText, Encoding.UTF8.GetString(regex.Replace(input, 0, evaluator, limit, start, token)));
            Assert.Equal(resultText, Encoding.UTF8.GetString(regex.Replace(input, 0, utf8Evaluator, limit, start, token)));
            Assert.Equal(resultText, regex.SubnToString(input, 0, evaluator, limit, start, token).ResultText);
            Assert.Equal(resultText, Encoding.UTF8.GetString(regex.Subn(input, 0, evaluator, limit, start, token).ResultBytes));
            Assert.Equal(resultText, Encoding.UTF8.GetString(regex.Subn(input, 0, utf8Evaluator, limit, start, token).ResultBytes));
            Assert.True(regex.DebugUsesOwnedSlice);
            Assert.True(regex.DebugUsesOwnedSlice);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)]
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
                    case 1: regex.FindAll(input, 0, source.Token); break;
                    case 2: regex.FindAllToStrings(input, 0, source.Token); break;
                    case 3: regex.FindAllToUtf8(input, 0, source.Token); break;
                    case 4: regex.SplitToStrings(input, 0, 0, source.Token); break;
                    case 5: regex.SplitDetailed(input, 0, 0, source.Token); break;
                    case 6: regex.Replace(input, "x", 0, 0, source.Token); break;
                    case 7: regex.ReplaceToString(input, "x", 0, 0, source.Token); break;
                    case 8: regex.Subn(input, "x", 0, 0, source.Token); break;
                    default: regex.SubnToString(input, "x", 0, 0, source.Token); break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, failure.CancellationToken);
        }
        finally { session.Complete(); }
        Assert.Single(regex.FindIterDetailed("\U0001f600\U0001f600"u8));
        Assert.True(regex.DebugUsesOwnedSlice);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GlobalDeadlinePersistsAcrossEvaluatorCalls(bool utf8)
    {
        var regex = new Utf8PythonRegex("(.)", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(500));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 8)));
        var calls = 0;
        Utf8PythonMatchEvaluator<int> evaluator = (_, match) => { calls++; Thread.Sleep(300); return match.Value.ValueText; };
        var failure = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            if (utf8) regex.Subn(input, 0, (int state, Utf8PythonDetailedMatchData match) => Encoding.UTF8.GetBytes(evaluator(state, match)), 0, 0);
            else regex.SubnToString(input, 0, evaluator, 0, 0);
        });
        Assert.InRange(calls, 1, 2);
        Assert.Equal(regex.MatchTimeout, failure.MatchTimeout);
        Assert.True(regex.DebugUsesOwnedSlice);
    }
}
