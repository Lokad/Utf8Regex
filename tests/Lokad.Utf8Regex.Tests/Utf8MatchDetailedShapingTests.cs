using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// Detailed views project complete scalars with byte and derived UTF-16 ranges.
/// The finite comparator preserves this fixture's predicates and scalar identity;
/// grouped patterns and bounded shaping allocation remain covered below.
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
    public void DetailedViewsProjectScalarRanges(string pattern, RegexOptions options, string text)
    {
        var subject = new Utf8ScalarCaptureFixture.Subject(text);
        var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, options));
        var utf8 = new Utf8Regex(pattern, options);
        var expected = oracle.Match(subject.Projected);
        var actual = utf8.MatchDetailed(subject.Input);
        Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, actual, subject);
        if (expected.Success)
        {
            Assert.Equal(1, actual.GroupCount);
            Assert.True(actual.TryGetGroup(0, out var tryGroup));
            Assert.Equal(subject.Value(expected.Index, expected.Length), tryGroup.GetValueString());
            Assert.True(actual.TryGetGroup("0", out var namedGroup));
            Assert.Equal(subject.Value(expected.Index, expected.Length), namedGroup.GetValueString());
            Assert.False(actual.TryGetGroup(1, out _));
            Assert.False(actual.TryGetGroup("missing", out _));
        }
        Assert.True(utf8.Inspection.PreparedRegex.ScalarProgram is not null);
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
    public void GroupedDetailedUsesOwnedShaping()
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

    [Theory]
    [InlineData("needle")]
    [InlineData("(needle)")]
    [InlineData("needle|other")]
    [InlineData("(?i:needle)")]
    [InlineData("(?i:k)")]
    public void LiteralDetailedViewsKeepCapturesAndScalarCoordinatesOnSlices(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled,
                     RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        foreach (var text in new[] { "xxneedle other NEEDLE", "é😀needle Kk", "xxxxx" })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var subject = new Utf8ScalarCaptureFixture.Subject(text);
            var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
            var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, options));
            var regex = new Utf8Regex(pattern, options);
            var actual = regex.MatchDetailed(storage.AsSpan(7, subject.Input.Length));
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, oracle.Match(subject.Projected), actual, subject);
        }
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("(needle)")]
    [InlineData("needle|other")]
    public void LiteralDetailedViewsRejectMalformedTailsAfterHitsAndMisses(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled,
                     RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        foreach (var prefix in new[] { "needle", "xxxxx" })
        {
            var regex = new Utf8Regex(pattern, mode | RegexOptions.CultureInvariant);
            byte[] input = [.. Encoding.UTF8.GetBytes(prefix), 0xff];
            Assert.Throws<ArgumentException>(() => regex.MatchDetailed(input));
        }
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

}
