using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R03 detailed shaping: groupless matches project every Match, Group, and Capture
/// view field from owned spans (BCL still discovers), with full view parity to the
/// BCL-backed oracle and bounded shaping allocation. Other grouped patterns keep BCL
/// shaping; grouped single literals are owned in GroupedLiteralDetailedTests.
/// </summary>
public sealed class Utf8MatchDetailedShapingTests
{
    public static IEnumerable<object?[]> ShapingCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { "^abc", RegexOptions.CultureInvariant, "abcabc" };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, "11ab22" };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, "xayb" };
        yield return new object?[] { "Шерлок", RegexOptions.None, "аШерлокб" };
        yield return new object?[] { "y", RegexOptions.CultureInvariant, "x\U0001F600y" };
        yield return new object?[] { "ABC", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxabCxx" };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, "bba" };
        yield return new object?[] { "zzz", RegexOptions.CultureInvariant, "abc" };
        yield return new object?[] { "abc", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, "xxabcxx" };
        yield return new object?[] { ".", RegexOptions.CultureInvariant, "\U0001F600" };
    }

    [Theory]
    [MemberData(nameof(ShapingCases))]
    public void DetailedViewsMatchRuntimeShaping(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var utf8 = new Utf8Regex(pattern, options);
        var expected = new Regex(pattern, options).Match(text);
        var actual = utf8.MatchDetailed(input);

        Assert.Equal(expected.Success, actual.Success);
        if (!expected.Success)
        {
            Assert.Equal(expected.Groups.Count, actual.GroupCount);
            Assert.Equal(string.Empty, actual.GetValueString());
            return;
        }

        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        Assert.Equal(1, actual.GroupCount);

        var start = expected.Index;
        var end = start + expected.Length;
        var aligned = IsScalarBoundary(text, start) && IsScalarBoundary(text, end);
        Assert.Equal(aligned, actual.IsByteAligned);
        if (aligned)
        {
            Assert.Equal(Encoding.UTF8.GetByteCount(text.Substring(0, start)), actual.IndexInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.Substring(start, expected.Length)), actual.LengthInBytes);
        }
        else
        {
            var threw = false;
            try
            {
                _ = actual.IndexInBytes;
            }
            catch (InvalidOperationException)
            {
                threw = true;
            }

            Assert.True(threw);
        }


        var group = actual.GetGroup(0);
        Assert.True(group.Success);
        Assert.Equal(expected.Index, group.IndexInUtf16);
        Assert.Equal(expected.Length, group.LengthInUtf16);
        Assert.Equal(expected.Value, group.GetValueString());
        Assert.Equal(1, group.CaptureCount);
        Assert.Equal(expected.Value, group.GetCapture(0).GetValueString());
        Assert.True(actual.TryGetGroup(0, out var tryGroup));
        Assert.Equal(expected.Value, tryGroup.GetValueString());
        Assert.True(actual.TryGetGroup("0", out var namedGroup));
        Assert.Equal(expected.Value, namedGroup.GetValueString());
        Assert.False(actual.TryGetGroup(1, out _));
        Assert.False(actual.TryGetGroup("missing", out _));
    }

    [Theory]
    [InlineData("abc", "xxabcxx", 0)]
    [InlineData("abc", "xxabcxx", 5)]
    [InlineData("y", "x\U0001F600y", 1)]
    public void DetailedFromOffsetMatchesRuntimeShaping(string pattern, string text, int offset)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Match(text, offset);
        var actual = utf8.MatchDetailedFromUtf16Offset(input, offset);

        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Value, actual.GetValueString());
            Assert.Equal(1, actual.GroupCount);
        }
    }

    [Fact]
    public void GroupedDetailedKeepsBclShaping()
    {
        const string text = "xxabxx";
        var input = Encoding.UTF8.GetBytes(text);
        var utf8 = new Utf8Regex("(a)(b)", RegexOptions.CultureInvariant);
        var expected = new Regex("(a)(b)", RegexOptions.CultureInvariant).Match(text);
        var actual = utf8.MatchDetailed(input);

        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(3, actual.GroupCount);
        Assert.Equal("a", actual.GetGroup(1).GetValueString());
        Assert.Equal("b", actual.GetGroup(2).GetValueString());
    }

    [Fact]
    public void GrouplessDetailedShapingStaysBounded()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(new string('x', 32) + "abc" + new string('y', 29));
        for (var i = 0; i < 10; i++)
        {
            Touch(regex.MatchDetailed(input));
        }

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            Touch(regex.MatchDetailed(input));
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.InRange(allocated, 0, 65536); // ~456 B/call measured (Debug, 2026-09-22); bound allows ~1.4x headroom.
    }

    private static void Touch(Utf8MatchContext match)
    {
        _ = match.Success;
        _ = match.IndexInUtf16;
        _ = match.GetValueString();
        var group = match.GetGroup(0);
        _ = group.GetValueString();
        _ = group.GetCapture(0).GetValueString();
    }

    private static bool IsScalarBoundary(string text, int utf16Offset)
    {
        if ((uint)utf16Offset > (uint)text.Length)
        {
            return false;
        }

        if (utf16Offset == 0 || utf16Offset == text.Length)
        {
            return true;
        }

        return !(char.IsHighSurrogate(text[utf16Offset - 1]) && char.IsLowSurrogate(text[utf16Offset]));
    }
}
