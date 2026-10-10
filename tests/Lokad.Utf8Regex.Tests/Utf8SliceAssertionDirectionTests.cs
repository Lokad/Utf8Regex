using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceAssertionDirectionTests
{
    public static IEnumerable<object[]> Cases()
    {
        (string Pattern, string Witness)[] rows = [
            (@"(?<=(a)\1)b", "aab"), (@"(?<=\1(a))b", "aab"),
            (@"(?<=ab|(a)\1)cd", "aacd"), (@"(?<=ab|\1(a))cd", "aacd"),
            (@"(?<=(a)(?(1)b|c))d", "acd"), (@"(?<=(?(1)b|c)(a))d", "bad"),
            (@"(?<=(?<12>a)\12)b", "aab"), (@"(?<=\12(?<12>a))b", "aab"),
            (@"(?<=(a+)(b+))c", "aaabbc"), (@"(?<=([ab]+)([bc]+))$", "abc"),
            (@"(?<=([ab]+?)([bc]+?))$", "abc"), (@"(?<=(a|aa)+)b", "aaab"),
            (@"(?<=((a)+))b", "aaab"), (@"(?<=(?<x>a)+(?<x>b)+)c", "aabbc"),
            (@"(?<=\k<x>(?<x>a+))b", "aaaab"),
            (@"(?<=(?=a)b)ab", "ab"), (@"(?<=(?<=a)b)ab", "aab"),
            (@"(?=(a)\1)aa", "aa"), (@"(?=(?<=a)b)ab", "ab"),
            (@"(?<=(?=bc)b)c", "abc"), (@"(?<=a(?<=a)b)c", "abc"),
            (@"(?<=a\b)b", "ab"), (@"(?<=a$)\n", "a\n"),
            (@"(?<=(?:(a)|ab))c", "abc"), (@"(?<=(?>a|aa))b", "aab"),
            (@"(?<=(a))(?<o>x)+(?<-o>y)+", "axy"),
            (@"(?<!a(?<q>z))(?<o>x)+(?<-o>y)+", "xy"),
            (@"(?<o>a)+(?<=b(?<-o>))c", "abc"),
            (@"(?<o>a)+b(?<=(?<-o>b))c", "abc"),
            (@"(?<o>a)+b(?<!(?<-o>z))c(?(o)d|e)", "abcd"),
            (@"(?<o>a)+b(?<=(?<-o>b))c(?(o)d|e)", "abce"),
            (@"(?<o>a)+(?=b(?<-o>))b(?(o)c|d)", "abd"),
            (@"(?<o>a)+(?!(?<-o>b))c(?(o)d|e)", "acd"),
            (@"(?<o>a)(?:(?=b(?<-o>))bz|b)(?(o)c|d)", "abc"),
            (@"(?<x>a(?=(?<x>b))b)\k<x>", "abab"),
            (@"(?<x>a(?<=(?<x>a))b)\k<x>", "abab"),
            (@"(?<=.(.))x", "\U0001f600x"),
            (@"(?<=\p{Cs}(\p{Cs}))x", "\U0001f600x"),
            (@"(?<=(?=\p{L})\p{L})x", "\u03b1x"),
            (@"(?<=\G(a*))b", "aaab"),
            (@"(?<o>a)+b(?<=(?<gap-o>b))c", "abc"),
            (@"(?<o>a)+b(?<!(?<gap-o>z))c", "abc"),
            (@"(?<o>a)(?=(?<gap-o>b))b", "ab"),
            (@"(?<=(?(?=b)b|a))c", "abc"),
            (@"(a)(?<=\1)b", "ab"), (@"(a)(?<=\1\1)b", "aab"),
            (@"(?<=(a|b))\1", "xaa"), (@"(?=(a)\1)", "aa"),
            (@"(?<o>a)bb(?<gap-o>c)", "abbc"),
            (@"(?=(?<o>a))(?<gap-o>a)", "a"),
            (@"(?<o>)(?<gap-o>a)", "a"),
            (@"(?<o>a)(?<-o>(?(o)b|c))", "ab"),
            (@"(?<o>a)(?<-o>\k<o>)", "aa"),
            (@"(?<o>a)(?<-o>(?<-o>b))", "ab"),
            (@"(?<o>a)(?<gap-o>(?<o>b))", "ab"),
            (@"(?<o>a)+b(?<=(?<-o>(?(o)b|z)))c", "abc"),
            (@"(?<-o>(?<o>a))", "a"), (@"(?<gap-o>(?<o>a))", "a"),
            (@"(?<-o>(?(o)a|(?<o>b)))", "b"),
        ];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var fold in new[] { false, true })
            yield return [row.Pattern, row.Witness, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
                (fold ? RegexOptions.IgnoreCase : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AssertionsPreserveDirectionAndCompleteCaptureState(string pattern, string witness, RegexOptions options)
    {
        string[] texts = ["", witness, "q" + witness + "z", witness + witness,
            "aaabbccdxxxyyy", "abcababaaabcdabce", "\u03b1a\U0001f600bc", "\U0001f600a\U0001f600x"];
        // Give the comparator explicit complete-scalar atoms where UTF-16 dot
        // would split the supplementary witness. Cs contains no valid scalar.
        const string scalar = @"(?:[\uD800-\uDBFF][\uDC00-\uDFFF]|[^\uD800-\uDFFF\n])";
        var oraclePattern = pattern switch
        {
            @"(?<=.(.))x" => "(?<=" + scalar + "(" + scalar + "))x",
            @"(?<=\p{Cs}(\p{Cs}))x" => @"(?<=(?!)((?!)))x",
            _ => pattern,
        };
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, texts, oraclePattern, options);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NestedAssertionJournalHonorsLimitsAndRecovers(bool compiled, bool interval)
    {
        var pattern = interval ? @"\A(?<o>a)+b(?<=(?<gap-o>b))(?=c(?(o)d|e))ce\z"
            : @"\A(?<o>a)+b(?<=(?<-o>b))(?=c(?(o)d|e))ce\z";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var input = "abce"u8.ToArray();
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref baseline).IsMatch);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps,
                Utf8SliceRunner.IsMatch(program, input, starts, ends, ref invocation).Breach);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        for (uint depth = 1; depth < baseline.Budget.PeakDepth; depth++)
        {
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, depth, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.Equal(Utf8ExecutionLimitKind.Depth,
                Utf8SliceRunner.IsMatch(program, input, starts, ends, ref invocation).Breach);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, (ulong)Utf8SliceRunner.BalanceReserveBytes - 1),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes,
            Utf8SliceRunner.IsMatch(program, input, starts, ends, ref denied).Breach);
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(1, regex.Count(input));
        Assert.False(regex.MatchDetailed(input).GetGroup("o").Success);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancellationInsideBackwardCaptureLoopReleasesAndRecovers(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        const string pattern = @"(?<=(?<o>a)+)b";
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var input = Encoding.ASCII.GetBytes(new string('a', 1024) + "b");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        invocation.CheckpointHook = steps => { if (steps >= 64) source.Cancel(); };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            Utf8SliceRunner.TryMatchAt(program, input, 1024, starts, ends, true, ref invocation));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        var regex = new Utf8Regex(pattern, options);
        var match = regex.MatchDetailedFromUtf16Offset(input, 1024);
        Assert.Equal(1024, match.GetGroup("o").CaptureCount);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void NestedAssertionEntriesCountTowardDepthWithoutBacktrackForks()
    {
        var program = Utf8SliceParser.TryParse(@"(?<=a(?<=a(?<=a)))b", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var starts = Array.Empty<int>();
        var ends = Array.Empty<int>();
        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 2, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.Depth,
            Utf8SliceRunner.TryMatchAt(program, "ab"u8, 1, starts, ends, true, ref denied).Breach);
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        var admitted = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 3, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryMatchAt(program, "ab"u8, 1, starts, ends, true, ref admitted).IsMatch);
        Assert.Equal(3U, admitted.Budget.PeakDepth);
        Assert.Equal(0UL, admitted.Ledger.ReservedBytes);
    }
}
