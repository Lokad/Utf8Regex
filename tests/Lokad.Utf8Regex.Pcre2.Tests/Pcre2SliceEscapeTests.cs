using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// PCRE2 hex, octal and control escapes use profile-owned scalar programs.
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
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }
}