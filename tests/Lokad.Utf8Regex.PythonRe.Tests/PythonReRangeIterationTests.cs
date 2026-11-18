using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReRangeIterationTests
{
    public static IEnumerable<object?[]> CaptureCases()
    {
        var none = PythonReCompileOptions.None;
        var ignoreCase = PythonReCompileOptions.IgnoreCase;
        yield return new object?[] { "cat|horse", none, "cat horse fish", 0, new[] { "cat", "horse" }, new string?[][] { new string?[] { "cat" }, new string?[] { "horse" } } };
        yield return new object?[] { "cat|horse", none, "no match", 0, new string[0], new string?[0][] };
        yield return new object?[] { "cat|horse", none, "", 0, new string[0], new string?[0][] };
        yield return new object?[] { "(cat)", none, "cat horse cat", 0, new[] { "cat", "cat" }, new string?[][] { new string?[] { "cat", "cat" }, new string?[] { "cat", "cat" } } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 0, new[] { "cat", "horse" }, new string?[][] { new string?[] { "cat", "cat", null }, new string?[] { "horse", null, "horse" } } };
        yield return new object?[] { "(cat)|(horse)", none, "horsecat", 0, new string?[] { "horse", "cat" }, new string?[][] { new string?[] { "horse", null, "horse" }, new string?[] { "cat", "cat", null } } };
        yield return new object?[] { "(cat)|(horse)", none, "cat horse", 4, new string?[] { "horse" }, new string?[][] { new string?[] { "horse", null, "horse" } } };
        yield return new object?[] { "(?P<w>cat)|(?P<v>horse)", none, "cat horse", 0, new[] { "cat", "horse" }, new string?[][] { new string?[] { "cat", "cat", null }, new string?[] { "horse", null, "horse" } } };
        yield return new object?[] { "(a)|(ab)", none, "xab", 0, new string?[] { "a" }, new string?[][] { new string?[][] { new string?[] { "a", "a", null } }[0] } };
        yield return new object?[] { "(ab)|(a)", none, "xab", 0, new string?[] { "ab" }, new string?[][] { new string?[] { "ab", "ab", null } } };
        yield return new object?[] { "(cat)|horse", none, "horsecat", 0, new string?[] { "horse", "cat" }, new string?[][] { new string?[] { "horse", null }, new string?[] { "cat", "cat" } } };
        yield return new object?[] { "((cat)|(horse))", none, "cat horse", 0, new string?[] { "cat", "horse" }, new string?[][] { new string?[] { "cat", "cat", "cat", null }, new string?[] { "horse", "horse", null, "horse" } } };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", none, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3", 0, new string?[] { "caf\u00E9", "ni\u00F1o" }, new string?[][] { new string?[] { "caf\u00E9", "caf\u00E9", null }, new string?[] { "ni\u00F1o", null, "ni\u00F1o" } } };
        yield return new object?[] { "(CAT)|(DOG)", ignoreCase, "cat dog", 0, new string?[] { "cat", "dog" }, new string?[][] { new string?[] { "cat", "cat", null }, new string?[] { "dog", null, "dog" } } };
        yield return new object?[] { "(a)", none, "baac", 2, new string?[] { "a" }, new string?[][] { new string?[] { "a", "a" } } };
    }

    [Theory]
    [MemberData(nameof(CaptureCases))]
    public void RangeIterationMatchesCaptureFixtures(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values, string?[][] groups)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, startOffsetInBytes, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var matches = collectedRanges1.ToArray();
        Assert.Equal(values, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        Assert.Equal(values.Length, groups.Length);
        for (var i = 0; i < matches.Length; i++)
        {
            var matchGroups = matches[i].Groups.ToArray();
            Assert.Equal(groups[i].Length, matchGroups.Length);
            for (var g = 0; g < matchGroups.Length; g++)
            {
                if (groups[i][g] is null)
                {
                    Assert.False(matchGroups[g].Success);
                    Assert.Equal(string.Empty, Encoding.UTF8.GetString(matches[i].GetGroupMemory(g).Span));
                    Assert.Equal(0, matchGroups[g].StartOffsetInBytes);
                    Assert.Equal(0, matchGroups[g].EndOffsetInBytes);
                }
                else
                {
                    Assert.True(matchGroups[g].Success);
                    Assert.Equal(groups[i][g], Encoding.UTF8.GetString(matches[i].GetGroupMemory(g).Span));
                    Assert.Equal(matches[i].Value.StartOffsetInBytes, matchGroups[g].StartOffsetInBytes);
                    Assert.Equal(matches[i].Value.EndOffsetInBytes, matchGroups[g].EndOffsetInBytes);
                }
            }
        }

    }

    public static IEnumerable<object?[]> ValueCases()
    {
        var none = PythonReCompileOptions.None;
        yield return new object?[] { "(a)+", none, "aaabaaa", 0, new[] { "aaa", "aaa" } };
        yield return new object?[] { "(b)|(:+)", none, ":a:b::c", 0, new string?[] { ":", ":", "b", "::" } };
        yield return new object?[] { "a*", none, "ba", 0, new string?[] { "", "a", "" } };
    }

    [Theory]
    [MemberData(nameof(ValueCases))]
    public void RangeIterationMatchesValueFixtures(string pattern, PythonReCompileOptions options, string inputText, int startOffsetInBytes, string[] values)
    {
        var regex = new Utf8PythonRegex(pattern, options: options);
        var input = Encoding.UTF8.GetBytes(inputText);
        var collectedRanges2 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor2 = regex.CreateMatchCursor(input, startOffsetInBytes, default))
        {
            while (rangeCursor2.MoveNext(out var rangeMatch2))
                collectedRanges2.Add(rangeMatch2);
        }
        var matches = collectedRanges2.ToArray();
        Assert.Equal(values, matches.Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
    }
}
