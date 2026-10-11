using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureCompletionTests
{
    private static IEnumerable<JsonElement> Rows()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Corpus", "capture-completion.json")));
        return document.RootElement.GetProperty("cases").EnumerateArray().Select(row => row.Clone()).ToArray();
    }

    public static IEnumerable<object[]> SingleCases()
    {
        foreach (var row in Rows())
            for (var kind = 0; kind < 3; kind++)
                yield return [row.GetProperty("id").GetInt32(), kind, row.GetRawText()];
    }

    public static IEnumerable<object[]> GlobalCases() => Rows().Select(row =>
        new object[] { row.GetProperty("id").GetInt32(), row.GetRawText() });

    private static int? LastIndex(JsonElement expected)
        => expected.GetProperty("lastIndex").ValueKind == JsonValueKind.Null ? null : expected.GetProperty("lastIndex").GetInt32();

    private static void AssertOwned(JsonElement expected, Utf8PythonDetailedMatchData actual)
    {
        Assert.Equal(expected.GetProperty("success").GetBoolean(), actual.Success);
        Assert.Equal(LastIndex(expected), actual.LastIndex);
        Assert.Equal(expected.GetProperty("lastGroup").GetString(), actual.LastGroup);
        var groups = expected.GetProperty("groups");
        Assert.Equal(groups.GetArrayLength(), actual.CaptureSlotCount);
        for (var number = 0; number < groups.GetArrayLength(); number++)
        {
            var group = groups[number]; var value = actual.Groups[number];
            Assert.Equal(number, value.Number);
            Assert.Equal(group.GetProperty("success").GetBoolean(), value.Success);
            Assert.Equal(group.GetProperty("text").GetString(), value.ValueText);
            Assert.Equal(group.GetProperty("startByte").GetInt32(), value.StartOffsetInBytes);
            Assert.Equal(group.GetProperty("endByte").GetInt32(), value.EndOffsetInBytes);
            Assert.Equal(group.GetProperty("startUtf16").GetInt32(), value.StartOffsetInUtf16);
            Assert.Equal(group.GetProperty("endUtf16").GetInt32(), value.EndOffsetInUtf16);
        }
    }

    private static void AssertContext(JsonElement expected, Utf8PythonMatchContext actual)
    {
        Assert.Equal(expected.GetProperty("success").GetBoolean(), actual.Success);
        Assert.Equal(LastIndex(expected), actual.LastIndex);
        Assert.Equal(expected.GetProperty("lastGroup").GetString(), actual.LastGroup);
        var groups = expected.GetProperty("groups");
        Assert.Equal(groups.GetArrayLength(), actual.CaptureSlotCount);
        for (var number = 0; number < groups.GetArrayLength(); number++)
        {
            var group = groups[number]; var value = actual.GetGroup(number);
            Assert.Equal(group.GetProperty("success").GetBoolean(), value.Success);
            Assert.Equal(group.GetProperty("text").GetString(), value.GetValueString());
            Assert.Equal(group.GetProperty("startByte").GetInt32(), value.Value.StartOffsetInBytes);
            Assert.Equal(group.GetProperty("endByte").GetInt32(), value.Value.EndOffsetInBytes);
            Assert.Equal(group.GetProperty("startUtf16").GetInt32(), value.Value.StartOffsetInUtf16);
            Assert.Equal(group.GetProperty("endUtf16").GetInt32(), value.Value.EndOffsetInUtf16);
        }
    }

    [Theory]
    [MemberData(nameof(SingleCases))]
    public void CompleteOneShotCapturesFollowCpython(int id, int kind, string json)
    {
        using var document = JsonDocument.Parse(json); var row = document.RootElement;
        var pattern = row.GetProperty("pattern").GetString()!;
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var expected = row.GetProperty("single")[kind];
        using var source = new CancellationTokenSource();
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxResultBytes = 1000000 };
            var regex = bytes ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
                : new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            var owned = kind switch { 0 => regex.SearchDetailedData(input, 0, source.Token),
                1 => regex.MatchDetailedData(input, 0, source.Token), _ => regex.FullMatchDetailedData(input, 0, source.Token) };
            AssertOwned(expected, owned);
            var context = kind switch { 0 => regex.SearchDetailed(input, 0, source.Token),
                1 => regex.MatchDetailed(input, 0, source.Token), _ => regex.FullMatchDetailed(input, 0, source.Token) };
            AssertContext(expected, context);
        }
    }

    [Theory]
    [MemberData(nameof(GlobalCases))]
    public void GlobalCursorAliasesAndCallbacksPublishCompleteRetainableCaptures(int id, string json)
    {
        using var document = JsonDocument.Parse(json); var row = document.RootElement;
        var regex = new Utf8PythonRegex(row.GetProperty("pattern").GetString()!);
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var expected = row.GetProperty("global");
        var array = regex.FindIterDetailed(input);
        Assert.Equal(expected.GetArrayLength(), array.Length);
        var retained = new List<Utf8PythonDetailedMatchData>();
        using (var cursor = regex.CreateMatchCursor(input))
        {
            var alias = cursor;
            for (var index = 0; index < array.Length; index++)
            {
                Assert.True((index % 2 == 0 ? cursor : alias).MoveNext());
                retained.Add(alias.Current);
            }
            Assert.False(alias.MoveNext());
        }
        var callbacks = new List<Utf8PythonDetailedMatchData>();
        var replaced = regex.Subn(input, callbacks, static (list, match) => { list.Add(match); return "#"; });
        Assert.Equal(array.Length, replaced.ReplacementCount);
        Assert.Equal(array.Length, callbacks.Count);
        for (var index = 0; index < array.Length; index++)
        {
            AssertOwned(expected[index], array[index]);
            AssertOwned(expected[index], retained[index]);
            AssertOwned(expected[index], callbacks[index]);
        }
    }

    [Theory]
    [InlineData("(?P<outer>(😀))", "😀")]
    [InlineData("(?<=(?P<last>.){2})", "😀é")]
    [InlineData("(?:(a)b|(?P<ok>a))", "a")]
    [InlineData("(?P<empty>)(?P<maybe>a)?", "a")]
    public void CompletionPublicationHasEnforceableWorkAndResultCutoffs(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var expected = new Utf8PythonRegex(pattern).SearchDetailedData(input);
        foreach (var work in new[] { false, true })
        {
            Utf8PythonRegex Create(ulong cap) => new(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout,
                work ? new() { MaxWorkSteps = cap } : new() { MaxResultBytes = cap });
            ulong first = 1, last = 1000000;
            while (first < last)
            {
                var cap = first + (last - first) / 2;
                try { var actual = Create(cap).SearchDetailedData(input); Assert.Equal(expected.LastIndex, actual.LastIndex); last = cap; }
                catch (Utf8ResourceLimitExceededException error)
                { Assert.Equal(work ? Utf8ResourceLimitKind.ExecutionWorkSteps : Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind); first = cap + 1; }
            }
            var result = Create(first).SearchDetailedData(input);
            Assert.Equal(expected.LastIndex, result.LastIndex); Assert.Equal(expected.LastGroup, result.LastGroup);
            Assert.Equal(expected.Groups, result.Groups);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(first - 1).SearchDetailedData(input));
            Assert.Equal(first - 1, failure.Limit);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void MetadataPublicationRetainsOriginalTokenAndDeadline(bool expire)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("(?<=(?P<outer>(😀)))é", PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20));
        using var cursor = regex.CreateMatchCursor("😀é"u8.ToArray(), 0, source.Token);
        var invocation = cursor.WorkingInvocation;
        ticks = 15; Assert.True(cursor.MoveNext()); var retained = cursor.Current;
        Assert.Equal(1, retained.LastIndex); Assert.Equal("outer", retained.LastGroup);
        if (expire)
        { ticks = 25; Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext()); }
        else
        { source.Cancel(); var error = Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()); Assert.Equal(source.Token, error.CancellationToken); }
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        Assert.Equal("😀", retained.Groups[1].ValueText); Assert.Equal("outer", retained.LastGroup);
    }

    [Fact]
    public void DefaultDetailedResultsHaveNoCompletion()
    {
        Assert.Null(default(Utf8PythonDetailedMatchData).LastIndex);
        Assert.Null(default(Utf8PythonDetailedMatchData).LastGroup);
        Assert.Null(default(Utf8PythonMatchContext).LastIndex);
        Assert.Null(default(Utf8PythonMatchContext).LastGroup);
    }
}
