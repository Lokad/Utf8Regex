using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// Capture cursors retain complete scalars in primary byte ranges.
/// The finite comparator preserves this fixture's predicates and scalar identity;
/// Grouped patterns, borrowed slices and metadata are exercised here;
/// allocation bounds belong to Utf8ScalarDiscoveryTests.
/// </summary>
public sealed class Utf8CaptureCursorShapingTests
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
    public void CaptureCursorsKeepScalarRanges(string pattern, RegexOptions options, string text)
    {
        var subject = new Utf8ScalarCaptureFixture.Subject(text);
        var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, options));
        var utf8 = new Utf8Regex(pattern, options);
        var expected = oracle.Match(subject.Projected);
        var actual = Utf8MatchSequence.First(utf8.CreateMatchCursor(subject.Input));
        Utf8MatchSequence.AssertCaptures(expected, actual, subject.Input, index => subject.Bytes[index]);
        if (expected.Success)
        {
            Assert.Equal(1, actual.GroupNumbers.Length);
            Assert.True(actual.TryGetGroup(0, out var tryGroup));
            Assert.Equal(actual.Value, tryGroup);
            Assert.True(actual.TryGetGroup("0", out var namedGroup));
            Assert.Equal(tryGroup, namedGroup);
            Assert.False(actual.TryGetGroup(1, out _));
            Assert.False(actual.TryGetGroup("missing", out _));
        }
        Assert.True(utf8.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("abc", "xxabcxx", 0)]
    [InlineData("abc", "xxabcxx", 5)]
    [InlineData("y", "x\U0001F600y", 1)]
    public void ByteStartsMatchRuntimeShaping(string pattern, string text, int offset)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var utf8 = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var expected = new Regex(pattern, RegexOptions.CultureInvariant).Match(text, offset);
        var actual = Utf8MatchSequence.First(utf8.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, offset))));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);

    }

    [Fact]
    public void GroupedCapturesKeepIndependentByteRanges()
    {
        const string text = "xxabxx";
        var input = Encoding.UTF8.GetBytes(text);
        var utf8 = new Utf8Regex("(a)(b)", RegexOptions.CultureInvariant);
        var expected = new Regex("(a)(b)", RegexOptions.CultureInvariant).Match(text);
        var actual = Utf8MatchSequence.First(utf8.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(expected, actual, input, text);
        Assert.Equal(3, actual.GroupNumbers.Length);
        Assert.Equal("a"u8.ToArray(), actual.GetGroupMemory(1).ToArray());
        Assert.Equal("b"u8.ToArray(), actual.GetGroupMemory(2).ToArray());
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("(needle)")]
    [InlineData("needle|other")]
    [InlineData("(?i:needle)")]
    [InlineData("(?i:k)")]
    public void LiteralCursorsKeepCapturesAndByteCoordinatesOnSlices(string pattern)
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
            var actual = Utf8MatchSequence.First(regex.CreateMatchCursor(storage.AsMemory(7, subject.Input.Length)));
            Utf8MatchSequence.AssertCaptures(oracle.Match(subject.Projected), actual,
                subject.Input, index => subject.Bytes[index]);
        }
    }

    [Theory]
    [InlineData("needle")]
    [InlineData("(needle)")]
    [InlineData("needle|other")]
    public void ValidatingCursorsRejectMalformedTailsAfterHitsAndMisses(string pattern)
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled,
                     RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        foreach (var prefix in new[] { "needle", "xxxxx" })
        {
            var regex = new Utf8Regex(pattern, mode | RegexOptions.CultureInvariant);
            byte[] input = [.. Encoding.UTF8.GetBytes(prefix), 0xff];
            Assert.Throws<ArgumentException>(() => Utf8MatchSequence.First(regex.CreateMatchCursor(input)));
        }
    }

}
