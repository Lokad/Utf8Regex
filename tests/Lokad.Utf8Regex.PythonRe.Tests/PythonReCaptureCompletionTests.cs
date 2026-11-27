using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureCompletionTests
{
    [Theory]
    [InlineData("(?P<outer>a(?P<inner>b))", "ab", 1, "ab")]
    [InlineData("(?P<one>a)(?P<two>b)?", "a", 1, "a")]
    [InlineData("(?P<one>a)(?P<two>)", "a", 2, "")]
    [InlineData("(?P<one>a)+(?P<two>b)", "aab", 2, "b")]
    [InlineData("(?P<one>a)(?P<two>b)|(?P<three>c)", "c", 3, "c")]
    [InlineData("(?P<a>a)(?P<b>b)(?P<c>c|cd)e", "abcde", 3, "cd")]
    [InlineData("(?P<a>a)(?P<b>b)(?:(?P<c>c)d|ce)", "abce", 2, "b")]
    public void MultipleCaptureCompletionSurvivesReducedRollbackStorage(
        string pattern, string text, int last, string capture)
    {
        {
            var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkingBytes = 4096,
                    MaxResultBytes = 4096
                }
            });
            var input = Encoding.UTF8.GetBytes(text);
            Utf8PythonRangeMatch result = default;
            using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out result);
            }
            Assert.True(result.Success);
            Assert.Equal(last, result.LastIndex);
            Assert.Equal(capture, System.Text.Encoding.UTF8.GetString((result.Groups[last].Success ? result.GetGroupMemory(result.Groups[last].Number).Span : ReadOnlySpan<byte>.Empty)));

            var expected = PythonReCaptureAssert.Values(result).Skip(1).ToArray();
            ReadOnlyMemory<byte> snapshots1Input = input;
            var snapshots1 = new List<Utf8PythonRangeMatch>();
            using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
            {
                while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                    snapshots1.Add(snapshots1Match);
            }
            Assert.Equal(expected, Assert.Single(snapshots1.Select(captureSnapshot1 => Enumerable.Range(1, captureSnapshot1.Groups.Length - 1).Select(captureNumber1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureNumber1).Span)).ToArray()).ToArray()));
            ReadOnlyMemory<byte> snapshots2Input = input;
            var snapshots2 = new List<Utf8PythonRangeMatch>();
            using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
            {
                while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                    snapshots2.Add(snapshots2Match);
            }
            Assert.Equal(expected, Assert.Single(snapshots2.Select(captureSnapshot2 => Enumerable.Range(1, captureSnapshot2.Groups.Length - 1).Select(captureNumber2 => captureSnapshot2.GetGroupMemory(captureNumber2).ToArray()).ToArray()).ToArray()).Select(value => Encoding.UTF8.GetString(value)));
            Utf8PythonRangeMatch snapshot2 = default;
            using (var captureCursor2 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor2.MoveNext(out snapshot2);
            }
            Assert.Equal(last, snapshot2.LastIndex);
            Assert.Equal(last, result.LastIndex);
            Assert.Equal(capture, System.Text.Encoding.UTF8.GetString((result.Groups[last].Success ? result.GetGroupMemory(result.Groups[last].Number).Span : ReadOnlySpan<byte>.Empty)));
        }
    }
    [Theory]
    [InlineData("(?P<v>a)", "a", "a")]
    [InlineData("(?P<v>)", "", "")]
    public void SingleCaptureCompletionRemainsValidWithFiniteLimits(string pattern, string text, string capture)
    {
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkingBytes = 1024,
                MaxResultBytes = 4096
            }
        });
        Utf8PythonRangeMatch result = default;
        using (var captureCursor3 = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor3.MoveNext(out result);
        }
        Assert.True(result.Success);
        Assert.Equal(1, result.LastIndex);
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString((result.Groups[1].Success ? result.GetGroupMemory(result.Groups[1].Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    private static IEnumerable<JsonElement> Rows()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,
            "Corpus", "capture-completion.json")));
        return document.RootElement.GetProperty("cases").EnumerateArray().Select(row => row.Clone()).ToArray();
    }

    public static IEnumerable<object[]> SingleCases()
    {
        foreach (var row in Rows())
        {
            for (var kind = 0; kind < 3; kind++)
            {
                yield return [kind, row.GetRawText()];
            }
        }
    }

    public static IEnumerable<object[]> GlobalCases() => Rows().Select(row =>
        new object[] { row.GetRawText() });

    private static int? LastIndex(JsonElement expected)
        => expected.GetProperty("lastIndex").ValueKind == JsonValueKind.Null ? null : expected.GetProperty("lastIndex").GetInt32();

    private static void AssertRange(JsonElement expected, Utf8PythonRangeMatch actual, Utf8PythonRegex regex)
    {
        Assert.Equal(expected.GetProperty("success").GetBoolean(), actual.Success);
        Assert.Equal(LastIndex(expected), actual.LastIndex);
        var name = expected.GetProperty("lastGroup").GetString();
        if (name is not null)
        {
            Assert.True(regex.TryGetGroupNumber(Encoding.UTF8.GetBytes(name), out var number));
            Assert.Equal(actual.LastIndex, number);
        }
        else if (actual.LastIndex is int unnamed)
        {
            Span<byte> label = stackalloc byte[11];
            Assert.Equal(System.Buffers.OperationStatus.Done, regex.TryCopyGroupNameUtf8(unnamed, label, out var written));
            Assert.Equal(unnamed.ToString(System.Globalization.CultureInfo.InvariantCulture), Encoding.UTF8.GetString(label[..written]));
        }
        var groups = expected.GetProperty("groups");
        Assert.Equal(groups.GetArrayLength(), actual.Groups.Length);
        for (var number = 0; number < groups.GetArrayLength(); number++)
        {
            var group = groups[number];
            var value = actual.Groups[number];
            Assert.Equal(group.GetProperty("success").GetBoolean(), value.Success);
            Assert.Equal(group.GetProperty("text").GetString(), System.Text.Encoding.UTF8.GetString(actual.GetGroupMemory(number).Span));
            Assert.Equal(group.GetProperty("startByte").GetInt32(), value.StartOffsetInBytes);
            Assert.Equal(group.GetProperty("endByte").GetInt32(), value.EndOffsetInBytes);
        }
    }
    [Theory]
    [MemberData(nameof(SingleCases))]
    public void CompleteOneShotCapturesFollowCpython(int kind, string json)
    {
        using var document = JsonDocument.Parse(json);
        var row = document.RootElement;
        var pattern = row.GetProperty("pattern").GetString()!;
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var expected = row.GetProperty("single")[kind];
        using var source = new CancellationTokenSource();
        foreach (var bytes in new[] { false, true })
        {
            var policy = new Utf8ResourcePolicy { MaxResultBytes = 1000000 };
            var regex = bytes ? new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern), options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            }) : new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy
            });
            Utf8PythonRangeMatch context = default;
            using (var captureCursor4 = kind switch
            {
                0 => regex.CreateMatchCursor(input, 0, source.Token, mode: Utf8PythonMatchMode.Search),
                1 => regex.CreateMatchCursor(input, 0, source.Token, mode: Utf8PythonMatchMode.Match),
                _ => regex.CreateMatchCursor(input, 0, source.Token, mode: Utf8PythonMatchMode.FullMatch)
            })
            {
                captureCursor4.MoveNext(out context);
            }
            AssertRange(expected, context, regex);
        }
    }
    [Theory]
    [MemberData(nameof(GlobalCases))]
    public void GlobalCursorAliasesPublishCompleteRetainableCaptures(string json)
    {
        Utf8PythonRangeMatch cursorRange = default;
        using var document = JsonDocument.Parse(json);
        var row = document.RootElement;
        var regex = new Utf8PythonRegex(row.GetProperty("pattern").GetString()!);
        var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
        var expected = row.GetProperty("global");
        Assert.Equal(expected.GetArrayLength(), regex.Count(input));
        var retained = new List<Utf8PythonRangeMatch>();
        using (var cursor = regex.CreateMatchCursor(input))
        {
            var alias = cursor;
            for (var index = 0; index < expected.GetArrayLength(); index++)
            {
                Assert.True((index % 2 == 0 ? cursor : alias).MoveNext(out cursorRange));
                retained.Add(cursorRange);
            }
            Assert.False(alias.MoveNext(out cursorRange));
        }
        for (var index = 0; index < expected.GetArrayLength(); index++)
        {
            AssertRange(expected[index], retained[index], regex);
        }
    }
    [Theory]
    [InlineData("(?P<outer>(😀))", "😀")]
    [InlineData("(?<=(?P<last>.){2})", "😀é")]
    [InlineData("(?:(a)b|(?P<ok>a))", "a")]
    [InlineData("(?P<empty>)(?P<maybe>a)?", "a")]
    [InlineData("(?:(?(2)(a)|()))+", "aa")]
    [InlineData("(?:(?(3)(?(2)(😀)|())|())){2,4}", "😀😀")]
    public void CompletionPublicationHasEnforceableWorkAndResultCutoffs(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        Utf8PythonRangeMatch expected = default;
        using (var captureCursor5 = new Utf8PythonRegex(pattern).CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor5.MoveNext(out expected);
        }
        foreach (var work in new[] { false})
        {
            Utf8PythonRegex Create(ulong cap) => new(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = work ? new()
                {
                    MaxDepth = 4096
                }

    : new()
    {
        MaxResultBytes = cap
    }
            });
            ulong first = 1, last = 1000000;
            while (first < last)
            {
                var cap = first + (last - first) / 2;
                try
                {
                    Utf8PythonRangeMatch actual = default;
                    using (var captureCursor6 = Create(cap).CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
                    {
                        captureCursor6.MoveNext(out actual);
                    }
                    Assert.Equal(expected.LastIndex, actual.LastIndex);
                    last = cap;
                }
                catch (Utf8ResourceLimitExceededException error)
                {
                    Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
                    first = cap + 1;
                }
            }
            Utf8PythonRangeMatch result = default;
            using (var captureCursor7 = Create(first).CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
            {
                captureCursor7.MoveNext(out result);
            }
            Assert.Equal(expected.LastIndex, result.LastIndex);
            PythonReCaptureAssert.Equal(expected, result);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                Utf8PythonRangeMatch snapshot8 = default;
                using (var captureCursor8 = Create(first - 1).CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
                {
                    captureCursor8.MoveNext(out snapshot8);
                }

                _ = snapshot8;
            });
            Assert.Equal(first - 1, failure.Limit);
        }
    }
    [Theory]
    [InlineData(false, "(?<=(?P<outer>(😀)))é", "😀é")]
    [InlineData(true, "(?<=(?P<outer>(😀)))é", "😀é")]
    [InlineData(false, "(?:(?(2)(😀)|()))+", "😀😀")]
    [InlineData(true, "(?:(?(2)(😀)|()))+", "😀😀")]
    public void MetadataPublicationRetainsOriginalTokenAndDeadline(bool expire, string pattern, string text)
    {
        Utf8PythonRangeMatch cursorRange = default;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var account = new PythonReSharedWorkingAllowanceTests.Allowance(1 << 20);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(text), 0, source.Token, account);
        ticks = 15;
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        Assert.Equal(1, retained.LastIndex);
        if (expire)
        {
            ticks = 25;
            Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
        }
        else
        {
            source.Cancel();
            var error = Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _));
            Assert.Equal(source.Token, error.CancellationToken);
        }
        Assert.Equal(0UL, account.Used);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }
    [Fact]
    public void DefaultDetailedResultsHaveNoCompletion()
    {
        Assert.Null(default(Utf8PythonRangeMatch).LastIndex);
    }
}
