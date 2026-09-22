using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

// R07 adaptation of pinned .NET runtime regression cases (see PLAN.md):
// nested class subtraction (50f4fecf875), atomic-loop bodies (5d62ba2e928),
// conditional resume state (4bd0bf40d82/dc803e0af2d), lazy-loop unwinding
// (04c21ab8734), and multiline candidate positions (f25f0e24d03).
// ValueListBuilder growth (2ec033b0cec) is covered by Utf8BudgetedGrowthTests
// and symbolic StripEffects memoization (ee4d4df31f6) has no analog (no
// symbolic engine); both recorded in PLAN.md, not tested here.
// All shapes below ride BCL fallback today; these rows pin oracle agreement
// so a future owned route must preserve the exact semantics.
public sealed class Utf8RegexRuntimeRegressionDiffTests
{
    [Theory]
    [InlineData(@"[a-z-[aeiou]]+", "bcdfg", RegexOptions.CultureInvariant)]
    [InlineData(@"[a-z-[aeiou]]+", "aeiou", RegexOptions.CultureInvariant)]
    [InlineData(@"[a-z-[aeiou]]+", "hello world", RegexOptions.CultureInvariant)]
    [InlineData(@"[0-9-[13579]]+", "2468", RegexOptions.CultureInvariant)]
    [InlineData(@"[0-9-[13579]]+", "13579", RegexOptions.CultureInvariant)]
    [InlineData(@"[[a-z]-[aeiou]]+", "bcdfg", RegexOptions.CultureInvariant)]
    [InlineData(@"[[a-z]-[aeiou]]+", "aeiou", RegexOptions.CultureInvariant)]
    [InlineData(@"[a-z-[d-w-[m-p]]]+", "abcxyz", RegexOptions.CultureInvariant)]
    [InlineData(@"[a-z-[d-w-[m-p]]]+", "mnop", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForNestedClassSubtraction(string pattern, string input, RegexOptions options)
    {
        // Adapted from dotnet/runtime 50f4fecf875 (iterative subtraction traversal):
        // nested subtraction must match iteratively with identical membership.
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData(@"(?>a+)b", "aaab", RegexOptions.CultureInvariant)]
    [InlineData(@"(?>a+)b", "aaa", RegexOptions.CultureInvariant)]
    [InlineData(@"(?>a|ab)c", "abc", RegexOptions.CultureInvariant)]
    [InlineData(@"(?>a|ab)c", "ac", RegexOptions.CultureInvariant)]
    [InlineData(@"(?>a*)b", "b", RegexOptions.CultureInvariant)]
    [InlineData(@"(?>a*)b", "aaab", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForAtomicLoopBodies(string pattern, string input, RegexOptions options)
    {
        // Adapted from dotnet/runtime 5d62ba2e928: iterations that need not be
        // returned do not imply internal alternatives can be discarded.
        // Atomic bodies must not give backtracking back into the group.
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData(@"(a)?(?(1)b|c)", "ab", RegexOptions.CultureInvariant)]
    [InlineData(@"(a)?(?(1)b|c)", "c", RegexOptions.CultureInvariant)]
    [InlineData(@"(a)?(?(1)b|c)", "a", RegexOptions.CultureInvariant)]
    [InlineData(@"(a)?(?(1)b|c)", "b", RegexOptions.CultureInvariant)]
    [InlineData(@"((a)?(?(2)b|c))+", "abcab", RegexOptions.CultureInvariant)]
    [InlineData(@"((a)?(?(2)b|c))+", "cc", RegexOptions.CultureInvariant)]
    [InlineData(@"(a)(?(1)b)", "ab", RegexOptions.CultureInvariant)]
    [InlineData(@"(a)(?(1)b)", "a", RegexOptions.CultureInvariant)]
    [InlineData(@"(?(?=a)a|b)", "a", RegexOptions.CultureInvariant)]
    [InlineData(@"(?(?=a)a|b)", "b", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForConditionalResume(string pattern, string input, RegexOptions options)
    {
        // Adapted from dotnet/runtime 4bd0bf40d82 + dc803e0af2d: conditional
        // resume state and capture rollback must survive loop iterations,
        // including the missing-no-branch path.
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData(@"(([0-9]){1,3}?x)?", "12x", RegexOptions.CultureInvariant)]
    [InlineData(@"(([0-9]){1,3}?x)?", "1x", RegexOptions.CultureInvariant)]
    [InlineData(@"(([0-9]){1,3}?x)?", "123x", RegexOptions.CultureInvariant)]
    [InlineData(@"(([0-9]){1,3}?x)?", "", RegexOptions.CultureInvariant)]
    [InlineData(@"(a{1,3}?b)?c", "aabc", RegexOptions.CultureInvariant)]
    [InlineData(@"(a{1,3}?b)?c", "c", RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForLazyLoopUnwind(string pattern, string input, RegexOptions options)
    {
        // Adapted from dotnet/runtime 04c21ab8734: abandoning a lazy loop at
        // maximum iterations must unwind every iteration frame with correct
        // capture rollback.
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Fact]
    public void LazyLoopAbandonmentPinsDotNet10OracleDisagreement()
    {
        // dotnet/runtime 04c21ab8734 fixes lazy-loop abandonment at maximum
        // iterations in the compiler/generator only. On .NET 10 the oracle
        // disagrees with itself for "(([0-9]){1,3}?x)?" on "123": interpreted
        // matches empty (length 0) while compiled keeps one iteration of
        // progress (length 1, outer group unmatched while the inner capture keeps its last iteration). Both Utf8 lanes ride
        // BCL fallback and mirror their own oracle lane faithfully, so only
        // per-lane fidelity is asserted here; the shared parity helper would
        // demand cross-lane agreement no faithful implementation can give.
        const string pattern = @"(([0-9]){1,3}?x)?";
        const string input = "123";
        const RegexOptions options = RegexOptions.CultureInvariant;
        var context = RegexParityContext.Create(pattern, input, options);
        var bytes = Encoding.UTF8.GetBytes(input);

        var expected = context.DotNet.Match(input);
        var expectedCompiled = context.DotNetCompiled.Match(input);
        Assert.True(expected.Success);
        Assert.Equal(0, expected.Length);
        Assert.True(expectedCompiled.Success);
        Assert.Equal(1, expectedCompiled.Length);
        Assert.False(expectedCompiled.Groups[1].Success);
        Assert.True(expectedCompiled.Groups[2].Success);

        var actual = context.Utf8.MatchDetailed(bytes);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Groups.Count, actual.GroupCount);

        var actualCompiled = context.Utf8Compiled.MatchDetailed(bytes);
        Assert.Equal(expectedCompiled.Success, actualCompiled.Success);
        Assert.Equal(expectedCompiled.Index, actualCompiled.IndexInUtf16);
        Assert.Equal(expectedCompiled.Length, actualCompiled.LengthInUtf16);
        Assert.Equal(expectedCompiled.Groups.Count, actualCompiled.GroupCount);
    }

    [Theory]
    [InlineData(@"^b", "a\nb\nc", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData(@"^b", "abc", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData(@"^abc", "xx\nabc", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData(@"abc$", "abc\nxx", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData(@"^", "a\nb", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    public void MatchDetailedMatchesRuntimeForMultilineCandidates(string pattern, string input, RegexOptions options)
    {
        // Adapted from dotnet/runtime f25f0e24d03: candidate discovery and
        // verification must agree on the advanced beginning-of-line position.
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [InlineData(@"^b", "a\nb\nc", 2, RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData(@"^abc", "xx\nabc", 3, RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    public void MatchDetailedFromOffsetMatchesRuntimeForMultilineCandidates(string pattern, string input, int offset, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertDetailedMatchGroupParityFromOffset(offset);
    }
}
