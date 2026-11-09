using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Auto-possessification soundness (native pcre2_auto_possess.c analog): a
// A disjoint literal or required word boundary can make a greedy single-token
// repeat's shorter alternatives impossible. Keep zero-minimum boundary runs,
// overlapping classes and nullable suffixes. Counters prove engagement; public
// ASCII results use BCL only on overlapping semantics.
public sealed class Pcre2AutoPossessifyTests
{
    private static void AssertAutoPossessCount(string pattern, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(expected, program.Program.AutoPossessiveRepeatCount);
    }

    private static void AssertAutoPossessCount(string pattern, Pcre2CompileOptions options, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(expected, program.Program.AutoPossessiveRepeatCount);
    }

    private static void AssertMatchesBcl(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.CultureInvariant, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        var expected = oracle.Match(inputText);
        var actual = regex.Match(input);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expected.Value, actual.GetValueString());
        }

        Assert.Equal(oracle.Count(inputText), regex.Count(input));
    }

    [Theory]
    [InlineData(@"\d+b", "123b")]
    [InlineData(@"\d+b", "123c")]
    [InlineData(@"\d+bc", "12bc")]
    [InlineData(@"\d+bc", "12bd")]
    public void EngagedShapesMatchBcl(string pattern, string inputText)
    {
        AssertAutoPossessCount(pattern, 1);
        AssertMatchesBcl(pattern, inputText);
    }

    [Theory]
    [InlineData("[ab]+b", "abb")]
    [InlineData("[ab]+b", "aba")]
    [InlineData("a+b?", "aab")]
    [InlineData("a+b?", "aa")]
    [InlineData("a+", "aaa")]
    [InlineData("a+", "b")]
    public void DeclinedShapesMatchBcl(string pattern, string inputText)
    {
        AssertMatchesBcl(pattern, inputText);
    }

    [Theory]
    [InlineData("[ab]+b", Pcre2CompileOptions.Caseless)]
    public void OverlappingOrNullableShapesDecline(string pattern, Pcre2CompileOptions options)
    {
        // R03 class slice serves the default-options shape on the shared
        // runner; caseless still routes the backtracking program, where the
        // overlap decline is proven.
        AssertAutoPossessCount(pattern, options, 0);
    }

    [Fact]
    public void DateWordBoundaryCommitsOnlyUncontrolledPlans()
    {
        const string pattern = @"\b\d{1,2}/\d{1,2}/\d{2,4}\b";
        // Count consumes the same lowered VM program even when Boolean calls
        // have an independently proved finite-language plan.
        var uncontrolled = new Utf8Pcre2Regex(pattern);
        Assert.Equal(3, Assert.IsType<Pcre2BacktrackingDirectProgram>(
            uncontrolled.DebugCompiledProgram.Operations.Count).Program.AutoPossessiveRepeatCount);
        var controlled = Utf8Pcre2Regex.Create(pattern, default, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 100_000, MaxDepth = 512 }, Timeout.InfiniteTimeSpan);
        var program = Assert.IsType<Pcre2BacktrackingDirectProgram>(controlled.DebugCompiledProgram.Operations.Count);
        Assert.Equal(0, program.Program.AutoPossessiveRepeatCount);
    }

    [Theory]
    [InlineData(@"\d+\b", "123 ")]
    [InlineData(@"\d{2,4}\b", "12345 ")]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "11/18/2019 11/19/201A")]
    [InlineData(@"([A-Za-z0-9_]+)\b", "abc_12!")]
    [InlineData(@"\w+\b", "abc_12x")]
    [InlineData(@"a+(?:\bX|\bY)", "aaaY")]
    [InlineData(@"\d*\b1", "123x")]
    [InlineData(@"\d+\B3", "123")]
    [InlineData(@"[^a]+\bA", "12 A")]
    [InlineData(@"[\w ]+\bA", "12 A")]
    [InlineData(@"\d+(?:\b|3)", "123x")]
    public void BoundaryCommitmentPreservesPublicAsciiResults(string pattern, string inputText)
        => AssertMatchesBcl(pattern, inputText);

    [Theory]
    [InlineData(Pcre2CompileOptions.Ucp, "١٢ ", "١٢")]
    [InlineData(Pcre2CompileOptions.Ucp, "𝟘𝟙 ", "𝟘𝟙")]
    [InlineData(Pcre2CompileOptions.None, "١٢ ", "")]
    public void BoundaryWordProofUsesTheSameUnicodeMode(Pcre2CompileOptions options, string input, string expected)
    {
        var regex = new Utf8Pcre2Regex(@"\b\d+\b", options);
        var match = regex.Match(Encoding.UTF8.GetBytes(input));
        Assert.Equal(expected.Length != 0, match.Success);
        if (match.Success) Assert.Equal(expected, match.GetValueString());
    }
}
