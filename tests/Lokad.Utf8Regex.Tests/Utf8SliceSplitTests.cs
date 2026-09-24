using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 split on the slice: segments project the owned match cursor, whose
// empty-match advancement already follows the BCL search order, so the
// shared segment/tail/count shaping serves slice shapes with BCL parity.
// Captured groups never appear as segments (matching EnumerateSplits, not
// classic Split); non-positive counts keep the BCL tail with identical
// behavior, including its failure modes.
public sealed class Utf8SliceSplitTests
{
    // Bare shapes keep native precedence; quantified shapes route owned.
    public static IEnumerable<object?[]> SliceSplitShapes()
    {
        yield return new object?[] { "a+" };
        yield return new object?[] { "x*|y" };
        yield return new object?[] { "\\d+" };
        yield return new object?[] { "(?:cat|horse)+" };
        yield return new object?[] { "(a)(?:b)(c)+" };
    }

    [Theory]
    [MemberData(nameof(SliceSplitShapes))]
    public void SplitShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> SplitCases()
    {
        yield return new object?[] { ",", "a,b,,c", int.MaxValue };
        yield return new object?[] { ",", "a,b,c,d", 3 };
        yield return new object?[] { ",", "a,b,c,d", 0 };
        yield return new object?[] { ",", "abc", int.MaxValue };
        yield return new object?[] { ",", string.Empty, int.MaxValue };
        yield return new object?[] { "a+", "aaabaa", int.MaxValue };
        yield return new object?[] { "a+", "aaabaa", 2 };
        yield return new object?[] { "x*|y", "axbyc", int.MaxValue };
        yield return new object?[] { "x*|y", "abc", int.MaxValue };
        yield return new object?[] { "(a)", "baac", int.MaxValue };
        yield return new object?[] { "(?:cat|horse)", "cat horse cathorse", int.MaxValue };
        yield return new object?[] { "(?:cat|horse)", "cat horse cathorse", 2 };
        yield return new object?[] { "(cat)|(horse)", "cat horse fish horse", int.MaxValue };
        yield return new object?[] { "\\d+", "a1b22c", int.MaxValue };
        yield return new object?[] { "(a)(?:b)(c)", "xabcxabc", int.MaxValue };
        yield return new object?[] { "a+", "axyza", int.MaxValue };
        yield return new object?[] { "\\p{L}*", "123", int.MaxValue };
        yield return new object?[] { "[\\p{L}a]*", "1a", int.MaxValue };
        yield return new object?[] { "\\P{L}*", "ab", int.MaxValue };
        yield return new object?[] { "x*|\\p{L}", "ax", int.MaxValue };
        yield return new object?[] { "(?:\\p{L}|)*", "ab", int.MaxValue };
    }

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void OwnedSplitMatchesOracle(string pattern, string inputText, int count)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(inputText, count))
        {
            var offset = range.Start.GetOffset(inputText.Length);
            var value = inputText[range];
            expected.Add((value, offset, value.Length));
        }

        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            var actual = new List<(string Value, int Index, int Length)>();
            foreach (var split in regex.EnumerateSplits(input, count))
            {
                actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
            }

            Assert.Equal(expected, actual);
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
            }
        }
    }

    public static IEnumerable<object?[]> AstralSplitCases()
    {
        var smile = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { ",", "a" + smile + ",b", int.MaxValue };
        yield return new object?[] { "a+", smile + "aa" + smile, int.MaxValue };
    }

    [Theory]
    [MemberData(nameof(AstralSplitCases))]
    public void OwnedAstralSplitMatchesOracle(string pattern, string inputText, int count)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = new List<(string Value, int Index, int Length)>();
        foreach (var range in oracle.EnumerateSplits(inputText, count))
        {
            var offset = range.Start.GetOffset(inputText.Length);
            var value = inputText[range];
            expected.Add((value, offset, value.Length));
        }

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        if (regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    // BCL EnumerateSplits treats count 0 as unlimited and negative counts throw
    // at the call. Count 1 is a standing library-wide gap: every route (twin, native,
    // slice) returns the whole input while the BCL oracle throws inside its first
    // MoveNext, so the pin documents our uniform contract instead of oracle parity.
    [Theory]
    [InlineData(",", "a,b")]
    [InlineData("a+", "aaabaa")]
    public void CountOneReturnsWholeInput(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var segments = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, 1))
        {
            segments.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal([(inputText, 0, inputText.Length)], segments);
    }

    [Theory]
    [InlineData(",", "a,b", -1)]
    public void DegenerateCountMatchesOracleBehavior(string pattern, string inputText, int count)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        List<(string Value, int Index, int Length)>? expected = null;
        Exception? oracleError = null;
        try
        {
            expected = new List<(string Value, int Index, int Length)>();
            foreach (var range in oracle.EnumerateSplits(inputText, count))
            {
                var (offset, length) = range.GetOffsetAndLength(inputText.Length);
                expected.Add((inputText.Substring(offset, length), offset, length));
            }
        }
        catch (Exception ex)
        {
            oracleError = ex;
        }

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        if (oracleError is not null)
        {
            var actualError = Assert.Throws(oracleError.GetType(), () =>
            {
                foreach (var split in regex.EnumerateSplits(input, count))
                {
                }
            });
            Assert.Equal(oracleError.GetType(), actualError.GetType());
            return;
        }

        var actual = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((split.GetValueString(), split.IndexInUtf16, split.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
    }
}