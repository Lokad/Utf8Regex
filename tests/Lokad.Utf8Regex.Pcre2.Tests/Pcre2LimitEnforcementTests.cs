using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LimitEnforcementTests
{
    public static IEnumerable<object?[]> CountRows()
    {
        yield return new object?[] { "foo", Pcre2CompileOptions.None, "foo foo foo", 3 };
        yield return new object?[] { "cat|horse", Pcre2CompileOptions.None, "cat horse cat", 3 };
        yield return new object?[] { "[ab]+", Pcre2CompileOptions.None, "ab ab ab", 3 };
        yield return new object?[] { "foo.+?bar", Pcre2CompileOptions.None, "fooXbar fooYbar", 2 };
        yield return new object?[] { "^ERROR", Pcre2CompileOptions.Multiline, "ERROR a\nERROR b\nERROR c", 3 };
        yield return new object?[] { "(?#comment)ab", Pcre2CompileOptions.None, "xxab xxab xxab", 3 };
        yield return new object?[] { @"\u0061b", Pcre2CompileOptions.None, "xxab xxab xxab", 3 };
        yield return new object?[] { "^ERROR: .+$", Pcre2CompileOptions.Multiline, "ERROR: a\nERROR: b", 2 };
        yield return new object?[] { "Holmes.{0,25}Watson|Watson.{0,25}Holmes", Pcre2CompileOptions.None, "Holmes+Watson" + new string((char)120, 30) + "Holmes+Watson", 2 };
        yield return new object?[] { "(?:ab|a)+z", Pcre2CompileOptions.None, "abababz abababz", 2 };
        yield return new object?[] { "(?<=abc)(|def)", Pcre2CompileOptions.None, "abcdefabcdef", 4 };
        yield return new object?[] { "(a)b+", Pcre2CompileOptions.None, "abb abb", 2 };
    }

    [Theory]
    [MemberData(nameof(CountRows))]
    public void TinyMatchLimitBreachesEnumerateOnEveryProgramKind(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        Assert.True(expected > 1);
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);

        Pcre2MatchException? caught = null;
        try
        {
            var enumerator = regex.EnumerateMatches(Encoding.UTF8.GetBytes(inputText));
            while (enumerator.MoveNext())
            {
            }
        }
        catch (Pcre2MatchException exception)
        {
            caught = exception;
        }

        Assert.NotNull(caught);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, caught.ErrorKind);
    }

    [Theory]
    [MemberData(nameof(CountRows))]
    public void TinyMatchLimitBreachesMatchManyOnEveryProgramKind(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        Assert.True(expected > 1);
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        var destination = new Utf8Pcre2MatchData[8];
        Assert.Throws<Pcre2MatchException>(() => regex.MatchMany(Encoding.UTF8.GetBytes(inputText), destination, out _));
    }

    [Fact]
    public void CountRowsSpanEveryProgramKind()
    {
        var kinds = new System.Collections.Generic.HashSet<string>();
        foreach (var row in CountRows())
        {
            var regex = new Utf8Pcre2Regex((string)row[0]!, (Pcre2CompileOptions)row[1]!, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
            foreach (var part in regex.DebugDescribeExecutionPlan().Split(", "))
            {
                if (part.StartsWith("Count=", System.StringComparison.Ordinal))
                {
                    kinds.Add(part.Substring("Count=".Length));
                }
            }
        }
        Assert.Equal(new[] { "Pcre2AsciiLiteralAlternationRepeat", "Pcre2AsciiOrderedLiteralWindow", "Pcre2Character", "Pcre2Literal", "Pcre2LiteralFamily", "Pcre2LiteralPrefixRepeat", "Pcre2MultilinePrefix", "Pcre2SameStartLiteral", "Pcre2SingleTokenRepeat", "Pcre2Slice", "Utf8Regex" }, kinds.OrderBy(static k => k, System.StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(CountRows))]
    public void UnlimitedCountMatchesExpected(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(inputText)));
    }

    [Theory]
    [MemberData(nameof(CountRows))]
    public void GenerousMatchLimitPreservesExactCounts(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1000 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(inputText)));
    }

    [Theory]
    [MemberData(nameof(CountRows))]
    public void TinyMatchLimitBreachesOnEveryProgramKind(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        Assert.True(expected > 1);
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(Encoding.UTF8.GetBytes(inputText)));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }

    public static IEnumerable<object?[]> DepthRows()
    {
        yield return new object?[] { "(a+)(b+)", Pcre2CompileOptions.None, "aaabbb aaabbb", 2 };
        yield return new object?[] { "((a+))b", Pcre2CompileOptions.None, "aaab aaab", 2 };
    }

    [Theory]
    [MemberData(nameof(DepthRows))]
    public void UnlimitedDepthMatchesExpected(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(inputText)));
    }

    [Theory]
    [MemberData(nameof(DepthRows))]
    public void TinyDepthLimitBreachesOnNestedGroups(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        Assert.True(expected > 1);
        var limits = new Utf8Pcre2ExecutionLimits { DepthLimit = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(Encoding.UTF8.GetBytes(inputText)));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Theory]
    [MemberData(nameof(DepthRows))]
    public void GenerousDepthLimitPreservesExactCounts(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        var limits = new Utf8Pcre2ExecutionLimits { DepthLimit = 1000000 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(inputText)));
    }

    [Theory]
    [MemberData(nameof(DepthRows))]
    public void TinyHeapLimitBreachesOnBacktrackingWork(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        Assert.True(expected > 1);
        var limits = new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(Encoding.UTF8.GetBytes(inputText)));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }
    public static IEnumerable<object?[]> ShallowDepthRows()
    {
        yield return new object?[] { "^ERROR: .+$", Pcre2CompileOptions.Multiline, "ERROR: a\nERROR: b", 2 };
        yield return new object?[] { "(?<=abc)(|def)", Pcre2CompileOptions.None, "abcdefabcdef", 4 };
        yield return new object?[] { "cat|horse", Pcre2CompileOptions.None, "cat horse cat", 3 };
    }

    [Theory]
    [MemberData(nameof(ShallowDepthRows))]
    public void TinyDepthLimitSucceedsOnShallowShapes(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        // Depth tracks peak backtracking-frame depth, not match count: these
        // shapes never push more than one frame, so a limit of one admits
        // their full counts.
        var limits = new Utf8Pcre2ExecutionLimits { DepthLimit = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(inputText)));
    }

    public static IEnumerable<object?[]> MeteredHeapRows()
    {
        yield return new object?[] { "[ab]+", Pcre2CompileOptions.None, "ab ab ab", 3 };
        yield return new object?[] { "cat|horse", Pcre2CompileOptions.None, "cat horse cat", 3 };
        yield return new object?[] { "^ERROR: .+$", Pcre2CompileOptions.Multiline, "ERROR: a\nERROR: b", 2 };
        yield return new object?[] { "(?:ab|a)+z", Pcre2CompileOptions.None, "abababz abababz", 2 };
        yield return new object?[] { "(?<=abc)(|def)", Pcre2CompileOptions.None, "abcdefabcdef", 4 };
        yield return new object?[] { "(a)b+", Pcre2CompileOptions.None, "abb abb", 2 };
    }

    [Theory]
    [MemberData(nameof(MeteredHeapRows))]
    public void TinyHeapLimitBreachesOnMeteredWorkspace(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        Assert.True(expected > 1);
        var limits = new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count(Encoding.UTF8.GetBytes(inputText)));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }

    public static IEnumerable<object?[]> WorkspaceFreeHeapRows()
    {
        yield return new object?[] { "foo", Pcre2CompileOptions.None, "foo foo foo", 3 };
        yield return new object?[] { "^ERROR", Pcre2CompileOptions.Multiline, "ERROR a\nERROR b\nERROR c", 3 };
        yield return new object?[] { "(?#comment)ab", Pcre2CompileOptions.None, "xxab xxab xxab", 3 };
    }

    [Theory]
    [MemberData(nameof(WorkspaceFreeHeapRows))]
    public void TinyHeapLimitSucceedsOnWorkspaceFreePaths(string pattern, Pcre2CompileOptions options, string inputText, int expected)
    {
        // Direct paths (literal scan, character scan, Utf8-kind delegation)
        // rent no metered working bytes, so a one-byte heap budget admits
        // their full counts.
        var limits = new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 };
        var regex = new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(expected, regex.Count(Encoding.UTF8.GetBytes(inputText)));
    }
}
