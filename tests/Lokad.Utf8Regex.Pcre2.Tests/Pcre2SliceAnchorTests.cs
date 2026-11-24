using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Shared scalar execution serves compatible anchors. PCRE2-specific newline,
// nullable multiline-caret and end-only-dollar behavior stays profile-owned.
public sealed class Pcre2SliceAnchorTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void ImpossibleFinalScalarSkipsSliceWorkingAdmission()
    {
        // The two tails both start at A. The hit requires a real retry.
        using var regex = new Utf8Pcre2Regex(@"\A(?:[a-z]+\.)+(?:[A-Z]{2,3}|[A-Z][0-9])\]?$", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy { MaxWorkingBytes = 1 }
        });
        Assert.Contains("IsMatch=Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.False(regex.IsMatch("abc.A#\n"u8));
        Assert.Equal(0, regex.Count("abc.A#"u8));
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.IsMatch("abc.A1\n"u8)).ErrorKind);
    }

    [Fact]
    public void ImpossibleFinalStartNeedsOnlyEnumerationAdmission()
    {
        using var regex = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline, policy: new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 1 }
        });
        using var source = new CancellationTokenSource();
        var cursor = regex.EnumerateMatches("😀\nERROR: "u8, 5, cancellationToken: source.Token);

        Assert.False(cursor.MoveNext());
    }

    [Fact]
    public void MultilineAnchorsUseCrlfInsteadOfLoneLf()
    {
        using var regex = new Utf8Pcre2Regex("^a|b$", Pcre2CompileOptions.Multiline,
            compileSettings: new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Crlf });
        var cursor = regex.EnumerateMatches("é\nax\r\nab\r\n"u8);

        Assert.True(cursor.MoveNext());
        Assert.Equal((7, 8), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.True(cursor.MoveNext());
        Assert.Equal((8, 9), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.False(cursor.MoveNext());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "^a.*b$", "a1b", true };
        yield return new object?[] { "^a.*b$", "xa1b", false };
        yield return new object?[] { "^(a.c)+$", "abcayc", true };
        yield return new object?[] { "^a|b$", "b", true };
        yield return new object?[] { "^a|b$", "xax", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, bool expected)
    {
        using var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
    }

    [Fact]
    public void OwnedIsMatchServesAstralAnchorScalar()
    {
        // The .NET oracle misses (unit-split dot); PCRE2-native scalar dots hit.
        const string inputText = "a\U0001F600c";
        Assert.False(
            new System.Text.RegularExpressions.Regex(
                "^a.c$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                System.Text.RegularExpressions.Regex.InfiniteMatchTimeout).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex("^a.c$", options: Pcre2CompileOptions.None);
        Assert.True(regex.IsMatch(Utf8(inputText), 0));
        var found = regex.Match(Utf8(inputText));
        Assert.True(found.Success);
        Assert.Equal(0, found.StartOffsetInBytes);
        Assert.Equal(6, found.EndOffsetInBytes);
    }

    [Theory]
    [InlineData("^a|b$", "aXb", 0, 2)]
    [InlineData("^(a.c)+$", "abcayc", 0, 1)]
    [InlineData("^a|b$", "a\nb", 0, 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
    }

    [Theory]
    [InlineData("^a|b$", "x\na", 0, true, 2, 3)]
    [InlineData("^a.*b$", "a1b", 0, true, 0, 3)]
    public void OwnedFirstMatchProjectsScalarBytes(
        string pattern, string inputText, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Multiline);
        var found = regex.Match(Utf8(inputText), startOffsetInBytes: startOffsetInBytes);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughAnchors()
    {
        var regex = new Utf8Pcre2Regex("^a|b$", options: Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aXb"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-1", "2-3"], ranges);
    }

    [Fact]
    public void PrimaryMatchWritesAnchorGroup()
    {
        var regex = new Utf8Pcre2Regex("^(a.c)+$", options: Pcre2CompileOptions.None);
        ReadOnlySpan<byte> detailed1Input = Utf8("abc");
        var detailed1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var detailed = regex.Match(detailed1Input, captures: detailed1Captures);
        Assert.True(detailed.Success);
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(detailed.GetValueBytes()));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString((detailed1Captures[1].Success ? detailed1Input.Slice(detailed1Captures[1].StartOffsetInBytes, detailed1Captures[1].EndOffsetInBytes - detailed1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void OwnedReplaceExpandsAnchorGroup()
    {
        var regex = new Utf8Pcre2Regex("^(a.c)+$", options: Pcre2CompileOptions.None);
        Assert.Equal("<abc>", System.Text.Encoding.UTF8.GetString(regex.Replace(Utf8("abc"), "<$1>"u8)));
    }
}
