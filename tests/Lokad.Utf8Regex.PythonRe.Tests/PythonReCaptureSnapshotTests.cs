using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReCaptureSnapshotTests
{
    [Fact]
    public void OverlappingCaptureSnapshotsSurviveCursorAndProgramDisposal()
    {
        var regex = new Utf8PythonRegex("((é😀))");
        var input = Encoding.UTF8.GetBytes("é😀 é😀");
        var matches = new List<Utf8PythonRangeMatch>();
        using (var cursor = regex.CreateMatchCursor(input))
        {
            while (cursor.MoveNext(out var match))
                matches.Add(match);
        }
        regex.Dispose();
        Assert.Equal(2, matches.Count);
        Assert.Equal(new[] { 0, 7 }, matches.Select(match => match.Value.StartOffsetInBytes));
        foreach (var match in matches)
        {
            Assert.Equal(match.Groups[0].StartOffsetInBytes, match.Groups[1].StartOffsetInBytes);
            Assert.Equal(match.Groups[0].EndOffsetInBytes, match.Groups[1].EndOffsetInBytes);
            Assert.Equal(match.Groups[1].StartOffsetInBytes, match.Groups[2].StartOffsetInBytes);
            Assert.Equal(match.Groups[1].EndOffsetInBytes, match.Groups[2].EndOffsetInBytes);
            Assert.Equal("é😀", Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
            Assert.Equal("é😀", Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));
        }
    }

    [Fact]
    public void EqualCaptureRangesFitAUsefulFiniteResultBudget()
    {
        using var regex = new Utf8PythonRegex("(([a-z]+))", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxResultBytes = 12 << 10,
                MaxWorkingBytes = 1 << 20
            }
        });
        var text = new string('a', 4096);
        ReadOnlyMemory<byte> snapshots2Input = Encoding.UTF8.GetBytes(text);
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        var tuples = snapshots2.Select(captureSnapshot2 => Enumerable.Range(1, captureSnapshot2.Groups.Length - 1).Select(captureNumber2 => System.Text.Encoding.UTF8.GetString(captureSnapshot2.GetGroupMemory(captureNumber2).Span)).ToArray()).ToArray();
        Assert.Single(tuples);
        Assert.Equal(new[] { text, text }, tuples[0]);
    }

    public static IEnumerable<object?[]> LiteralCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "cat|horse", none, "cat horse fish", 0, new[] { "cat", "horse" } };
        yield return new object?[] { "cat|horse", none, "cathorse", 0, new[] { "cat", "horse" } };
        yield return new object?[] { "cat|horse", none, "no match", 0, new string[0] };
        yield return new object?[] { "cat|horse", none, "", 0, new string[0] };
        yield return new object?[] { "(?:cat|horse)", none, "cat horse", 0, new[] { "cat", "horse" } };
        yield return new object?[] { "cat|horse", none, "cat horse fish", 4, new[] { "horse" } };
        yield return new object?[] { "needle|needle-long", none, "\u00E9 skip haystack", 8, new string[0] };
        yield return new object?[] { "(?:CAT|DOG)", ignoreCase, "cat dog", 0, new[] { "cat", "dog" } };
    }

    [Theory]
    [MemberData(nameof(LiteralCases))]
    public void CursorWholeMatchGoldensMatchManaged(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var wholeMatches6 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor6 = regex.CreateMatchCursor(input, startOffsetInBytes, default))
        {
            while (rangeCursor6.MoveNext(out var rangeMatch6))
            {
                wholeMatches6.Add(rangeMatch6);
            }
        }
        var matches = wholeMatches6.ToArray();
        Assert.Equal(values, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());

    }

    public static IEnumerable<object?[]> SingleGroupCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, new[] { "cat", "cat" }, new[] { "cat", "cat" } };
        yield return new object?[] { "(cat)|horse", none, "horsecat", 0, new[] { "horse", "cat" }, new string?[] { "", "cat" } };
    }

    [Theory]
    [MemberData(nameof(SingleGroupCases))]
    public void SingleGroupCursorKeepsWholeMatchGoldens(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values, string?[] groupValues)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var wholeMatches7 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor7 = regex.CreateMatchCursor(input, startOffsetInBytes, default))
        {
            while (rangeCursor7.MoveNext(out var rangeMatch7))
            {
                wholeMatches7.Add(rangeMatch7);
            }
        }
        var matches = wholeMatches7.ToArray();
        Assert.Equal(values, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(groupValues, matches.Select(match => Encoding.UTF8.GetString(match.GetGroupMemory(1).Span)));

    }

    public static IEnumerable<object?[]> CaptureCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "((é😀))", none, "xé😀 é😀", 1, new[] { new[] { "é😀", "é😀" }, new[] { "é😀", "é😀" } } };
        yield return new object?[] { "(([a-z]+)-[0-9]+)", none, "item-12 other-345", 0, new[] { new[] { "item-12", "item" }, new[] { "other-345", "other" } } };
        yield return new object?[] { "()([a-z]+)", none, "word next", 0, new[] { new[] { "", "word" }, new[] { "", "next" } } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, new[] { new[] { "cat", "" }, new[] { "", "horse" } } };
        yield return new object?[] { "(cat)|(horse)", none, "horsecat", 0, new[] { new[] { "", "horse" }, new[] { "cat", "" } } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 4, new[] { new[] { "", "horse" } } };
        yield return new object?[] { "(?P<w>cat)|(?P<v>horse)", none, "cat horse", 0, new[] { new[] { "cat", "" }, new[] { "", "horse" } } };
        yield return new object?[] { "(a)|(ab)", none, "xab", 0, new[] { new[] { "a", "" } } };
        yield return new object?[] { "(ab)|(a)", none, "xab", 0, new[] { new[] { "ab", "" } } };
        yield return new object?[] { "((cat)|(horse))", none, "cat horse", 0, new[] { new[] { "cat", "cat", "" }, new[] { "horse", "", "horse" } } };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 0, new[] { new[] { "caf\u00E9", "" }, new[] { "", "ni\u00F1o" } } };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "cat dog", 0, new[] { new[] { "cat", "" }, new[] { "", "dog" } } };
    }

    [Theory]
    [MemberData(nameof(CaptureCases))]
    public void CursorKeepsCaptureGoldens(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[][] tuples)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        ReadOnlyMemory<byte> snapshots5Input = input;
        var snapshots5 = new List<Utf8PythonRangeMatch>();
        using (var snapshots5Cursor = regex.CreateMatchCursor(snapshots5Input, startOffsetInBytes, default))
        {
            while (snapshots5Cursor.MoveNext(out var snapshots5Match))
                snapshots5.Add(snapshots5Match);
        }
        var strings = snapshots5;
        Assert.Equal(tuples.Length, strings.Count);
        for (var i = 0; i < tuples.Length; i++)
        {
            Assert.Equal(tuples[i], Enumerable.Range(1, strings[i].Groups.Length - 1)
                .Select(number => Encoding.UTF8.GetString(strings[i].GetGroupMemory(number).Span)));
        }

    }

    public static IEnumerable<object?[]> ManagedCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(b)|(:+)", none, ":a:b::c", 0, new[] { ":", ":", "b", "::" } };
        yield return new object?[] { "a*", none, "ba", 0, new[] { "", "a", "" } };
        yield return new object?[] { "\\b|\\w+", none, "\u00E9 skip y", 8, new[] { "", "y", "" } };
    }

    [Theory]
    [MemberData(nameof(ManagedCases))]
    public void GeneralCursorKeepsWholeMatchGoldens(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var wholeMatches8 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor8 = regex.CreateMatchCursor(input, startOffsetInBytes, default))
        {
            while (rangeCursor8.MoveNext(out var rangeMatch8))
            {
                wholeMatches8.Add(rangeMatch8);
            }
        }
        var matches = wholeMatches8.ToArray();
        Assert.Equal(values, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
    }
    [Theory]
    [InlineData("[a-z]+", "alpha beta gamma 123 ", 1024, 3072)]
    [InlineData("[a-z]+[0-9]+", "item12 other345 ", 512, 1024)]
    public void CursorDoesNotAcquirePerMatchVmWorkspaces(
        string pattern, string unit, int repetitions, int expectedCount)
    {
        var text = string.Concat(Enumerable.Repeat(unit, repetitions));
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        for (var warmup = 0; warmup < 4; warmup++)
        {
            ReadOnlyMemory<byte> snapshots6Input = input;
            var snapshots6 = new List<Utf8PythonRangeMatch>();
            using (var snapshots6Cursor = regex.CreateMatchCursor(snapshots6Input, 0, default))
            {
                while (snapshots6Cursor.MoveNext(out var snapshots6Match))
                    snapshots6.Add(snapshots6Match);
            }
            _ = snapshots6;
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        ReadOnlyMemory<byte> snapshots7Input = input;
        var snapshots7 = new List<Utf8PythonRangeMatch>();
        using (var snapshots7Cursor = regex.CreateMatchCursor(snapshots7Input, 0, default))
        {
            while (snapshots7Cursor.MoveNext(out var snapshots7Match))
                snapshots7.Add(snapshots7Match);
        }
        var result = snapshots7;
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(expectedCount, result.Count);
        // Retained snapshot metadata fits comfortably. A fresh VM workspace for
        // each result previously allocated 3.7-5.4 MB on these public workloads.
        Assert.InRange(allocated, 0, 1_048_576);
        var oracle = new System.Text.RegularExpressions.Regex(pattern);
        var expected = oracle.Matches(text).Select(static match => match.Value).ToArray();
        Assert.Equal(expected, result.Select(captureSnapshot8 => System.Text.Encoding.UTF8.GetString(captureSnapshot8.GetGroupMemory(captureSnapshot8.Groups.Length == 2 ? 1 : 0).Span)).ToArray());

    }

}
