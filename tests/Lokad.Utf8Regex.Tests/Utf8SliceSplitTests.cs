using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// Split segments project the owned scalar match cursor. Captured groups do
// not appear as segments. Zero count is unlimited, one returns the whole
// subject, and negative counts fail before any matching begins. Comparators
// below use subjects and patterns whose code-unit behavior overlaps scalars.
public sealed class Utf8SliceSplitTests
{
    // Bare shapes keep native precedence; quantified shapes route owned.
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
        yield return new object?[] { "\\t+", "a\t\tb\tc", int.MaxValue };
        yield return new object?[] { "\\n+", "a\nb", int.MaxValue };
        yield return new object?[] { "\\cA+", "a\u0001b", int.MaxValue };
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
            expected.Add((value, global::System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), global::System.Text.Encoding.UTF8.GetByteCount(value)));
        }

        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options: options);
            var actual = new List<(string Value, int Index, int Length)>();
            foreach (var split in regex.EnumerateSplits(input, count))
            {
                actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
            }

            Assert.Equal(expected, actual);
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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
            expected.Add((value, global::System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), global::System.Text.Encoding.UTF8.GetByteCount(value)));
        }

        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var actual = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }

        Assert.Equal(expected, actual);
        if (regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var segments = new List<(string Value, int Index, int Length)>();
        foreach (var split in regex.EnumerateSplits(input, 1))
        {
            segments.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
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
                expected.Add((inputText.Substring(offset, length),global::System.Text.Encoding.UTF8.GetByteCount(inputText.AsSpan(0, offset)), global::System.Text.Encoding.UTF8.GetByteCount(inputText.Substring(offset, length))));
            }
        }
        catch (Exception ex)
        {
            oracleError = ex;
        }

        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
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
            actual.Add((Encoding.UTF8.GetString(split.GetValueBytes()), split.IndexInBytes, split.LengthInBytes));
        }

        Assert.Equal(expected, actual);
    }
}
