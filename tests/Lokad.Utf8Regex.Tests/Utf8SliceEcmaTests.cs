using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceEcmaTests
{
    private static readonly string[] Patterns =
    [
        "ab", "a+b", "(a)(b)", @"\w+", @"\W+", @"\s+", @"\S+", @"\d+", @"\D+",
        @"\b\w+\b", @"\B", @"[\w]+", @"[\W]+", @"[\s\S]+", @"[\D\d]+",
        @"[\w\p{L}]+", @"[a-z-[\w]]+", @"[\w-a]+", @"(?i:\w+)", @"(?i:[\w]+)", @"(?i:[\W]+)",
        @"(?<x>a)?\k<x>b", @"(a)?\1b", @"(?<x>a\k<x>)+", @"\k<0>", @"(a)\18+",
        @"\1(a)", @"\k<x>(?<x>a)", @"(a)\12(b)", @"(?<12>a)\123+", @"\8+", @"\9{0}",
        @"\18+", @"\400+", @"\777+", @"[\400-\777]+", @"[\8\9]+", @"\q+", @"[\q]+",
        @"(a)\18{0}", @"\18{0}(a)", @"\7778{0}", @"\12345678901234567890+",
        @"(?<2147483647>a)\21474836478", @"(a)(?<12>b)\123",
        @"(a)\123(?<12>b)", @"(a)\1(?<12>b)\123", @"(a\1)+",
        @"(a)\123(?<11>b)", @"(a)\123(?<20>b)", @"(a)\123(?<1000>b)",
        @"(?<214748364>a)\2147483648", @"(?<214748364>a)\214748364123",
        @"(?<2147483647>a)\2147483647000",
        @"(?(?=a)(a)|(b))\1\2", @"(?<x>a)(?<x>b)\1", @"\1(?<x>a)",
        @"\p{L}+", @"(?<=(?<x>a)\k<x>)b", @"(?(?=a)(?<x>a)|b)", @"(?<a>x)(?<-a>y)",
        @"(?x: a + b )", @"(?n:(?<x>a))\k<x>", @"(?s:.)+", @"^a+$",
    ];
    private static readonly string[] Texts =
    [
        "", "ab", "aab", "abb", "aaab", "b", "a2b", "aa888", "aa22", "xy", "a3 aa3 aa333",
        "a\nA\nab", "8 88 ?7 ?777 0 000", "\u0001a\u0001b\0a\na", "q qqq 8 99",
        "abb3 aa23b aa3b ababb3 aa8", "ab bb aa aaa", "aa123 aa000 aab aa2b aa8b",
        "K kK ſ s İ ı A_9", "é a٢ b\u00a0c\u2003d\v", "\u200ca\u200d", "\U0001f600ab\U0001f600",
    ];

    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var ignoreCase in new[] { false, true })
        foreach (var multiline in new[] { false, true })
            yield return [pattern, RegexOptions.ECMAScript | RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None) |
                (multiline ? RegexOptions.Multiline : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void EcmaGrammarKeepsAllOwnedOperations(string pattern, RegexOptions options)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        Assert.NotNull(Utf8OwnedTreeSliceLowerer.TryLower(tree, options));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, Texts, options);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EcmaSetsAndBoundariesCoverEveryBmpScalar(bool compiled, bool ignoreCase)
    {
        var text = new string(Enumerable.Range(0, 65536).Where(value => value < 0xd800 || value > 0xdfff)
            .Select(value => (char)value).ToArray()) + "\U0001f600";
        var input = Encoding.UTF8.GetBytes(text);
        var options = RegexOptions.ECMAScript | RegexOptions.CultureInvariant |
            (ignoreCase ? RegexOptions.IgnoreCase : RegexOptions.None);
        foreach (var pattern in new[] { @"\d", @"\D", @"\s", @"\S", @"\w", @"\W",
            @"[\d]", @"[\D]", @"[\s]", @"[\S]", @"[\w]", @"[\W]", @"\b", @"\B" })
        {
            // Membership uses the stable interpreted oracle; Compiled changes
            // the owned selection without changing the profile's sets.
            var oracle = new Regex(pattern, options);
            var regex = new Utf8Regex(pattern, options | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var expected = oracle.EnumerateMatches(text);
            var actual = regex.EnumerateMatches(input);
            var count = 0;
            while (expected.MoveNext())
            {
                Assert.True(actual.MoveNext());
                Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
                Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
                count++;
            }
            Assert.False(actual.MoveNext());
            Assert.Equal(count, regex.Count(input));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
    }

    [Theory]
    [InlineData(@"\k")]
    [InlineData(@"\k<x>")]
    [InlineData(@"\c!")]
    [InlineData(@"[\c!]")]
    [InlineData(@"[z-a]")]
    [InlineData(@"(?<0>a)")]
    [InlineData(@"(?<2147483648>a)")]
    [InlineData(@"(?(2)a|b)")]
    public void InvalidEcmaPatternsKeepExactConstructionFailures(string pattern)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.ECMAScript | RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var expected = Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
            var actual = Assert.Throws<RegexParseException>(() => new Utf8Regex(pattern, options));
            Assert.Equal(expected.Error, actual.Error);
            Assert.Equal(expected.Offset, actual.Offset);
            Assert.Equal(expected.Message, actual.Message);
            Assert.Null(Utf8SliceParser.TryParse(pattern, options));
        }
    }

    [Theory]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.Singleline)]
    [InlineData(RegexOptions.ExplicitCapture)]
    [InlineData(RegexOptions.IgnorePatternWhitespace)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void InvalidEcmaOptionCombinationsKeepConstructionFailures(RegexOptions invalid)
    {
        var options = RegexOptions.ECMAScript | invalid;
        var expected = Assert.Throws<ArgumentOutOfRangeException>(() => new Regex("a", options));
        var actual = Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8Regex("a", options));
        Assert.Equal(expected.ParamName, actual.ParamName);
        Assert.Equal(expected.Message, actual.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EcmaReferencesRetainBudgetCancellationAndRecovery(bool compiled)
    {
        const string pattern = @"\A(?<x>(a|aa)+b)?\1c\b";
        var options = RegexOptions.ECMAScript | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var input = Encoding.ASCII.GetBytes(new string('a', 9) + "c");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.False(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref baseline).IsMatch);
        Assert.True(baseline.Budget.Steps > 128);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            var outcome = Utf8SliceRunner.IsMatch(program, input, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.False(outcome.IsMatch);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var storage = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.TrailReserveBytes - 1),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.IsMatch(program, input, starts, ends, ref storage).Breach);
        Assert.Equal(0UL, storage.Ledger.ReservedBytes);
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        using var source = new CancellationTokenSource();
        var canceled = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        canceled.CheckpointHook = steps => { if (steps >= 64) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.IsMatch(program, input, starts, ends, ref canceled));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, canceled.Ledger.ReservedBytes);
        var regex = new Utf8Regex(pattern, options);
        Assert.False(regex.IsMatch(input));
        Assert.True(regex.IsMatch("aaabac"u8));
        Assert.True(regex.IsMatch("c"u8)); // The absent reference matches empty.
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
