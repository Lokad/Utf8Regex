using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Python numbers groups strictly by opening paren while .NET numbers
// unnamed groups first, so numeric backreferences, conditionals, replacement
// references, and several result builders disagreed with CPython whenever a named
// group preceded an unnamed one. Every surface below now indexes Python slots;
// managed numbers appear only at the engine boundary. Expectations are CPython
// 3.13.2 ground truth (see ground-c07*.py).
public sealed class PythonGroupNumberingRegressionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ManyMixedGroupsKeepPythonOrderAndLastCompletion(bool trailingGroup, bool finite)
    {
        Utf8PythonRangeMatch cursorRange = default;
        var pieces = Enumerable.Range(0, 70).Select(index => index % 2 == 0 ? "é" : "😀").ToArray();
        var body = string.Concat(pieces);
        var pattern = "(?P<outer>" + string.Concat(Enumerable.Range(0, pieces.Length)
            .Select(index => index % 2 == 0 ? $"(?P<n{index}>.)" : "(.)")) + ")" + (trailingGroup ? "(.)" : "");
        var text = body + (trailingGroup ? "z" : "");
        var input = Utf8(text);
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = finite ? new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1000,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1 << 20
            }

    : default
        });
        var values = new[] { text, body }.Concat(pieces).Concat(trailingGroup ? new[] { "z" } : []).ToArray();
        void AssertGroups(Utf8PythonRangeMatch match)
        {
            Assert.True(match.Success);
            Assert.Equal(trailingGroup ? 72 : 1, match.LastIndex);
            Assert.Equal(trailingGroup ? null : "outer", match.LastGroup);
            Assert.Equal(values, PythonReCaptureAssert.Values(match));
            Assert.Equal(Enumerable.Range(0, values.Length), Enumerable.Range(0, match.Groups.Length));
        }
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        AssertGroups(snapshot1);
        Utf8PythonRangeMatch snapshot2 = default;
        using (var captureCursor2 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch))
        {
            captureCursor2.MoveNext(out snapshot2);
        }
        AssertGroups(snapshot2);
        Assert.Equal(values.Skip(1), Assert.Single(PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)).TupleValues));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        Assert.False(cursor.MoveNext(out cursorRange));
        cursor.Dispose();
        Utf8PythonRangeMatch snapshot3 = default;
        using (var captureCursor3 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor3.MoveNext(out snapshot3);
        }
        PythonReCaptureAssert.Equal(snapshot3, retained);
    }
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Theory]
    [InlineData("(?P<x>a)(b)\\1", "aba", true, 0, 3)]
    [InlineData("(?P<x>a)(b)\\2", "abb", true, 0, 3)]
    [InlineData("(?P<x>a)(b)\\2", "aba", false, 0, 0)]
    [InlineData("(a)(?P<x>b)\\1", "aba", true, 0, 3)]
    [InlineData("(a)(?P<x>b)\\2", "abb", true, 0, 3)]
    [InlineData("(?P<x>a)(?P<y>b)\\1", "aba", true, 0, 3)]
    [InlineData("(?P<x>a)(?P<y>b)\\2", "abb", true, 0, 3)]
    [InlineData("(a)(b)(c)\\3", "abcc", true, 0, 4)]
    public void NumericBackreferencesFollowPythonSlots(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8PythonRegex(pattern);
        AssertSearch(regex, input, expectedSuccess, expectedStart, expectedEnd);
        Assert.Equal(expectedSuccess, regex.Match(Utf8(input)).Success);
        Assert.Equal(expectedSuccess, regex.Match(Utf8(input), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData("(?P<x>a)(b)(?(2)y|n)", "aby", true, 0, 3)]
    [InlineData("(?P<x>a)(b)(?(2)y|n)", "abn", false, 0, 0)]
    [InlineData("(?P<x>a)(b)(?(x)y|n)", "aby", true, 0, 3)]
    [InlineData("(?P<x>a)?(?(1)y|n)", "ay", true, 0, 2)]
    [InlineData("(a)(b)(?(1)y|n)", "aby", true, 0, 3)]
    public void NumericAndNamedConditionalsFollowPythonSlots(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        AssertSearch(new Utf8PythonRegex(pattern), input, expectedSuccess, expectedStart, expectedEnd);
    }

    [Fact]
    public void DetailedGroupsFollowPythonSlotOrder()
    {
        Utf8PythonRangeMatch detailed = default;
        using (var captureCursor4 = new Utf8PythonRegex("(?P<x>a)(b)").CreateMatchCursor(Utf8("ab"), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor4.MoveNext(out detailed);
        }
        Assert.True(detailed.Success);
        Assert.Equal(["ab", "a", "b"], PythonReCaptureAssert.Values(detailed).ToArray());
        Assert.Equal([0, 1, 2], Enumerable.Range(0, detailed.Groups.Length).ToArray());
        var named = detailed.Groups[1];
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((named.Success ? detailed.GetGroupMemory(named.Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void NamedLookupUsesPythonSlots()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)(b)");
        Assert.Equal(["0", "x", "2"], regex.GetGroupNames());
        Utf8PythonRangeMatch context = default;
        using (var captureCursor5 = regex.CreateMatchCursor(Utf8("ab"), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor5.MoveNext(out context);
        }
        Assert.True(context.Success);
        Assert.True(context.TryGetGroup(1, out var first));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((first.Success ? context.GetGroupMemory(first.Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.True(context.TryGetGroup(2, out var second));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((second.Success ? context.GetGroupMemory(second.Number).Span : ReadOnlySpan<byte>.Empty)));
        Assert.True(context.TryGetFirstSetGroup("x", out var named));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((named.Success ? context.GetGroupMemory(named.Number).Span : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData("(?P<x>a)(b)", "\\2\\1", "ab", "ba")]
    [InlineData("(?P<x>a)(b)", "\\g<1>\\g<2>", "ab", "ab")]
    [InlineData("(?P<x>a)(b)", "\\g<x>!", "ab", "a!")]
    [InlineData("(?P<x>a)(b)(?(2)y|n)", "\\2\\1", "aby", "ba")]
    [InlineData("(a)(?P<x>b)", "\\2\\1", "ab", "ba")]
    public void ReplacementReferencesFollowPythonSlots(string pattern, string replacement, string input, string expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.Equal(expected, PythonReSubstitutionAssert.Decode(regex.Subn(Utf8(input), replacement, 0)).ResultText);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Subn(Utf8(input), replacement, 0).ResultBytes));
    }

    [Fact]
    public void SplitCapturesFollowPythonSlotOrder()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)(-)");
        var detailed = regex.SplitRanges(Utf8("xa-b"), 0, 0, default);
        Assert.Equal(["x", "a", "-", "b"], PythonReSplitAssert.Decode(Utf8("xa-b"), detailed));
        Assert.Equal([0, 1, 2, 0], detailed.Select(static item => item.Number).ToArray());
    }

    [Fact]
    public void FindAllTuplesFollowPythonSlotOrder()
    {
        var tuples = PythonReFindAllAssert.Decode(new Utf8PythonRegex("(?P<x>a)(b)").FindAllToUtf8(Utf8("abab"))).TupleValues;
        Assert.Equal(2, tuples.Length);
        Assert.Equal(["a", "b"], tuples[0]);
        Assert.Equal(["a", "b"], tuples[1]);
        var wholeMatches1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = new Utf8PythonRegex("(?P<x>a)(b)").CreateMatchCursor(Utf8("abab"), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
            {
                wholeMatches1.Add(rangeMatch1);
            }
        }
        var matches = wholeMatches1.ToArray();
        Assert.Equal(2, matches.Length);
    }

    [Fact]
    public void FindIterDetailedFollowsPythonSlotOrder()
    {
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = new Utf8PythonRegex("(?P<x>a)(b)").CreateMatchCursor(Utf8("xaby"), 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var found = collectedRanges1.ToArray();
        var single = Assert.Single(found);
        Assert.Equal(["ab", "a", "b"], single.Groups.ToArray().Select(group => System.Text.Encoding.UTF8.GetString(single.GetGroupMemory(group.Number).Span)).ToArray());
    }

    [Fact]
    public void RangeCapturesFollowPythonSlotOrder()
    {
        using var regex = new Utf8PythonRegex("(?P<x>a)(b)");
        using var cursor = regex.CreateMatchCursor(Utf8("ab"));
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal("ab", Encoding.UTF8.GetString(match.GetValueMemory().Span));
        Assert.Equal("a", Encoding.UTF8.GetString(match.GetGroupMemory(1).Span));
        Assert.Equal("b", Encoding.UTF8.GetString(match.GetGroupMemory(2).Span));

    }

    private static void AssertSearch(Utf8PythonRegex regex, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var match = regex.Match(Encoding.UTF8.GetBytes(input), mode: Utf8PythonMatchMode.Search);
        Assert.Equal(expectedSuccess, match.Success);
        if (expectedSuccess)
        {
            Assert.Equal(input.Substring(expectedStart, expectedEnd - expectedStart), System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
        }
    }
}
