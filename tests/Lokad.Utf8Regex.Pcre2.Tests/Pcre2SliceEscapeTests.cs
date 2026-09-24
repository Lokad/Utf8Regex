using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 numeric escapes for the PCRE2 profile: hex and NUL-led octal route the
// native single-token programs with identical behavior; \u escapes (which
// native PCRE2 rejects) delegate to the core library, which now serves
// ASCII results on the owned slice instead of the BCL fallback. Bare C0
// controls (\t \n \e) and control letters (\cA) also stay native: PCRE2
// accepts them as single tokens with identical behavior.
public sealed class Pcre2SliceEscapeTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Fact]
    public void HexEscapeKeepsNativeRouting()
    {
        var regex = new Utf8Pcre2Regex("\\x41+", Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.DoesNotContain("Utf8Regex", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(Utf8("AAA")));
    }

    [Fact]
    public void UnicodeEscapeDelegatesToOwnedCore()
    {
        var regex = new Utf8Pcre2Regex("\\u0041+", Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(Utf8("AAA")));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void ControlEscapesKeepNativeRouting()
    {
        foreach (var pattern in new[] { "\\t+", "\\n+", "\\e+", "\\cA+" })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
            Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
            Assert.DoesNotContain("Utf8Regex", regex.DebugDescribeExecutionPlan());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\x41+", "AAB", true };
        yield return new object?[] { "\\u0041+", "AAB", true };
        yield return new object?[] { "\\01+", "\u0001\u0001", true };
        yield return new object?[] { "\\x41+", "BBB", false };
        yield return new object?[] { "\\t+", "\t", true };
        yield return new object?[] { "\\t+", "\n", false };
        yield return new object?[] { "\\n+", "\n", true };
        yield return new object?[] { "\\n+", "\t", false };
        yield return new object?[] { "\\e+", "\u001B", true };
        yield return new object?[] { "\\e+", "e", false };
        yield return new object?[] { "\\cA+", "\u0001", true };
        yield return new object?[] { "\\cA+", "\t", false };
        yield return new object?[] { "\\cA+", "A", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void EscapeIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }
}