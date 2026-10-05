using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceClassAdmissionTests
{
    private static readonly string[] Texts =
    ["", "a", "b", "abcab", "aaabbb", "a1 A_9", "é Θθ ١", "\U0001f600a\U0001f600", "ab\na", "K kK ſs"];

    private static string Chain(int depth, string outer, string inner)
    {
        var pattern = new StringBuilder().Append('[');
        for (var level = 0; level < depth; level++) pattern.Append(outer).Append("-[");
        return pattern.Append(inner).Append(']', depth + 1).ToString();
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var depth in new[] { 0, 64, 65, 66, 128, 257 })
        {
            var ascii = Chain(depth, "a-z", "ae");
            var unicode = Chain(depth, @"^\p{Lu}\d", @"\w\p{L}");
            var halves = Chain(depth, @"\D", @"\uD800-\uDBFF");
            var patterns = new[]
            {
                @"(?<x>" + ascii + @"+)\k<x>?",
                @"(?<12>" + unicode + @"{1,2})(?(12)\k<12>|b)",
                @"(?=(?<x>" + ascii + @"+))\k<x>",
                new string('(', 40) + unicode + new string(')', 40),
                "(?:" + halves + "|b)+",
                "(?i:" + ascii + "+?)",
            };
            foreach (var pattern in patterns)
            foreach (var compiled in new[] { false, true })
            foreach (var rtl in new[] { false, true })
            foreach (var ignoreCase in new[] { false, true })
                yield return [pattern, RegexOptions.CultureInvariant |
                    (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                    (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
                    (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)];
        }
        foreach (var depth in new[] { 65, 128 })
        foreach (var compiled in new[] { false, true })
        foreach (var ignoreCase in new[] { false, true })
            yield return [@"(?<x>" + Chain(depth, @"\w", "k") + @"+)\k<x>?",
                RegexOptions.ECMAScript | RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void DeepClassChainsKeepCompleteOwnedOperations(string pattern, RegexOptions options)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        Assert.NotNull(Utf8OwnedTreeSliceLowerer.TryLower(tree, options));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, Texts, options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LongLexicalPrefixesKeepCompleteOwnedOperations(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var padding = new string('x', 65537);
        foreach (var pattern in new[] { "(?#" + padding + @")(?<x>a+)\k<x>",
            "(?x:" + new string(' ', 65537) + @"(?<x>a+)\k<x>)" })
            Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, Texts, options);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ClassChainBeyondBothFormerBoundariesKeepsOwnedOperations(bool compiled)
    {
        var pattern = @"\A(?<x>" + Chain(16385, "a", "b") + ")";
        Assert.True(pattern.Length > 65536);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        // S0={b}, S(n+1)={a}\S(n): every odd n>=1 is exactly {a}.
        // .NET 10 accepts this deep pattern in the isolated main-thread probe
        // but its recursive scanner can overflow the test worker's stack.
        // Retain the full owned-depth witness and use the equivalent shallow
        // pattern for complete metadata, capture and operation parity.
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, ["", "a", "b", "aa", "\U0001f600a"],
            @"\A(?<x>a)", options);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LongLiteralKeepsOwnedCoordinatesAndResultOperations(bool compiled, bool rtl)
    {
        var pattern = new string('a', 65537);
        var text = "\U0001f600" + pattern + "|" + pattern;
        var input = Encoding.UTF8.GetBytes(text);
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options);
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, options));
        Assert.Equal(oracle.GetGroupNumbers(), regex.GetGroupNumbers());
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));
        var expected = oracle.EnumerateMatches(text);
        var actual = regex.EnumerateMatches(input);
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
            Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
        }
        Assert.False(actual.MoveNext());
        foreach (var offset in new[] { 0, 1, 2, text.Length })
        {
            var match = oracle.Match(text, offset);
            var value = regex.MatchFromUtf16Offset(input, offset);
            Assert.Equal(match.Success, value.Success);
            Assert.Equal(match.Index, value.IndexInUtf16);
            Assert.Equal(match.Length, value.LengthInUtf16);
            var detailed = regex.MatchDetailedFromUtf16Offset(input, offset);
            Assert.Equal(match.Success, detailed.Success);
            Assert.Equal(match.Index, detailed.IndexInUtf16);
            Assert.Equal(match.Length, detailed.LengthInUtf16);
        }
        var splits = regex.EnumerateSplits(input);
        var expectedSplits = oracle.EnumerateSplits(text);
        while (expectedSplits.MoveNext())
        {
            Assert.True(splits.MoveNext());
            Assert.Equal(expectedSplits.Current.Start.Value, splits.Current.IndexInUtf16);
            Assert.Equal(expectedSplits.Current.End.Value - expectedSplits.Current.Start.Value, splits.Current.LengthInUtf16);
            Assert.Equal(text[expectedSplits.Current], splits.Current.GetValueString());
        }
        Assert.False(splits.MoveNext());
        Assert.Equal(oracle.Replace(text, "<$&>"), regex.ReplaceToString(input, "<$&>"));
        Assert.Equal(Encoding.UTF8.GetBytes(oracle.Replace(text, "!")), regex.Replace(input, "!"u8));
        Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(new string('a', 65536))));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeepClassStepsRespectLimitsCancellationAndRecovery(bool compiled)
    {
        var pattern = @"\A(?<x>" + Chain(4097, "a", "b") + ")";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var input = "a"u8.ToArray();
        var map = Utf8BoundaryMap.Create(input);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref baseline).IsMatch);
        Assert.True(baseline.Budget.Steps > 4097);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
                Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref invocation).Breach);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var storage = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.TrailReserveBytes - 1),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref storage).Breach);
        Assert.Equal(0UL, storage.Ledger.ReservedBytes);
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = steps => { if (steps >= 128) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.IsMatch(program, input, map, starts, ends, ref canceled));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal("a", regex.MatchDetailed(input).GetGroup("x").GetValueString());
        Assert.False(regex.IsMatch("b"u8));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeepClassClosersKeepExactConstructionFailures(bool compiled)
    {
        var valid = Chain(128, "a", "b");
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        foreach (var pattern in new[] { valid[..^1], valid[..^1] + "c]", valid[..^1] + "-[c]]" })
        {
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(Utf8SliceParser.TryParse(pattern, options));
        }
    }
}
