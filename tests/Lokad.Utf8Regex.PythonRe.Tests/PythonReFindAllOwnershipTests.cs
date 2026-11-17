using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReFindAllOwnershipTests
{
    [Fact]
    public void NestedTupleValuesKeepIndependentSlotsAndSurviveProgramDisposal()
    {
        var regex = new Utf8PythonRegex("((é😀))");
        var tuples = regex.FindAllToUtf8(Encoding.UTF8.GetBytes("é😀 é😀")).TupleValues;
        regex.Dispose();
        Assert.Equal(2, tuples.Length);
        Assert.Equal(new[] { "é😀", "é😀" }, tuples[0].Select(Encoding.UTF8.GetString));
        Assert.Equal(new[] { "é😀", "é😀" }, tuples[1].Select(Encoding.UTF8.GetString));
        tuples[0][0][0] = (byte)'x';
        Assert.Equal("é😀", Encoding.UTF8.GetString(tuples[0][1]));
        Assert.Equal(new[] { "é😀", "é😀" }, tuples[1].Select(Encoding.UTF8.GetString));
    }

    [Fact]
    public void EqualCaptureRangesFitAUsefulFiniteResultBudget()
    {
        using var regex = new Utf8PythonRegex("(([a-z]+))", resourcePolicy: new()
        {
            MaxResultBytes = 12 << 10,
            MaxWorkingBytes = 1 << 20,
            MaxWorkSteps = 1000000
        });
        var text = new string('a', 4096);
        var tuples = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(Encoding.UTF8.GetBytes(text))).TupleValues;
        Assert.Single(tuples);
        Assert.Equal(new[] { text, text }, tuples[0]);
    }

    public static IEnumerable<object?[]> OwnedScalarCases()
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
    [MemberData(nameof(OwnedScalarCases))]
    public void CursorWholeMatchGoldensMatchManaged(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values)
    {
        var regex = new Utf8PythonRegex(pattern, options);
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
        var utf8 = regex.FindAllToUtf8(input, startOffsetInBytes);
        Assert.Equal(values, utf8.ScalarValues.Select(Encoding.UTF8.GetString).ToArray());
    }

    public static IEnumerable<object?[]> OwnedSingleGroupCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, new[] { "cat", "cat" }, new[] { "cat", "cat" } };
        yield return new object?[] { "(cat)|horse", none, "horsecat", 0, new[] { "horse", "cat" }, new string?[] { "", "cat" } };
    }

    [Theory]
    [MemberData(nameof(OwnedSingleGroupCases))]
    public void SingleGroupCursorKeepsWholeMatchGoldens(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values, string?[] groupValues)
    {
        var regex = new Utf8PythonRegex(pattern, options);
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
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes));
        Assert.Equal(groupValues, strings.ScalarValues);
    }

    public static IEnumerable<object?[]> OwnedTupleCases()
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
    [MemberData(nameof(OwnedTupleCases))]
    public void OwnedTupleFindAllMatchesManaged(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[][] tuples)
    {
        var regex = new Utf8PythonRegex(pattern, options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var strings = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input, startOffsetInBytes));
        Assert.Equal(tuples.Length, strings.TupleValues.Length);
        for (var i = 0; i < tuples.Length; i++)
        {
            Assert.Equal(tuples[i], strings.TupleValues[i]);
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
        var regex = new Utf8PythonRegex(pattern, options);
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
    public void GenericBulkBytesDoNotAcquirePerMatchVmWorkspaces(
        string pattern, string unit, int repetitions, int expectedCount)
    {
        var text = string.Concat(Enumerable.Repeat(unit, repetitions));
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(pattern);
        for (var warmup = 0; warmup < 4; warmup++) PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));

        var before = GC.GetAllocatedBytesForCurrentThread();
        var result = PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(expectedCount, result.Count);
        // Required byte roots and host decoding fit comfortably. A fresh VM workspace for
        // each result previously allocated 3.7-5.4 MB on these public workloads.
        Assert.InRange(allocated, 0, 1_048_576);
        var oracle = new System.Text.RegularExpressions.Regex(pattern);
        var expected = oracle.Matches(text).Select(static match => match.Value).ToArray();
        Assert.Equal(expected, result.ScalarValues);
        Assert.Equal(expected, regex.FindAllToUtf8(input).ScalarValues.Select(Encoding.UTF8.GetString));
    }



}
