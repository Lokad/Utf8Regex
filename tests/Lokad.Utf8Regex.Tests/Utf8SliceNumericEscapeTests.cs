using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceNumericEscapeTests
{
    public static IEnumerable<object[]> Cases()
    {
        (string Pattern, string Witness)[] rows = [
            (@"a\12b", "a\nb"), (@"(a)\10", "a\b"), (@"\12", "\n"),
            (@"\123", "S"), (@"\1234", "S4"), (@"\1000", "@0"),
            (@"\18", "\u00018"), (@"\19+", "\u0001999"),
            (@"\1998", "\u0001998"), (@"\178", "\u000f8"),
            (@"\178{2}", "\u000f88"), (@"\1789{2}", "\u000f899"),
            (@"\178{0}", "\u000f"), (@"\1789{0}", "\u000f8"),
            (@"\12345{0}", "S4"), (@"\178+?", "\u000f888"),
            (@"\178*", "\u000f"), (@"\178?", "\u000f8"),
            (@"(?:\178)+", "\u000f8\u000f8"), (@"(?:\178){0}", ""),
            (@"(\178)+", "\u000f8\u000f8"), (@"((?:\178){2})", "\u000f8\u000f8"),
            (@"\777", "\u00ff"), (@"\7777", "\u00ff7"), (@"\400", "\0"),
            (@"\777{2}", "\u00ff\u00ff"), (@"\7777{0}", "\u00ff"),
            (@"\101", "A"), (@"\101+", "AAA"), (@"\1012{2}", "A22"),
            (@"(?i:\1012{0})", "a"), (@"\08", "\08"), (@"\0008", "\08"),
            (@"\078", "\a8"), (@"\080", "\080"),
            (@"[\178]", "\u000f8"), (@"[\777]", "\u00ff"),
            (@"[\400]", "\0"), (@"[\1x]", "\u0001x"), (@"[\10-a]", "a\b"),
            (@"(?<12>a)\12", "aa"), (@"\12(?<12>a)", "aa"),
            (@"(?<12>a)\123", "aS"), (@"(?<123>a)\123", "aa"),
            (@"(?<18>a)\18", "aa"), (@"(?<99>a)\99", "aa"),
            (@"(?<777>a)\777", "aa"),
            (@"(?<2147483647>a)\2147483647", "aa"),
            (@"\2147483647(?<2147483647>a)", "aa"),
            (@"(?<12>a)+\12+", "aaaa"), (@"(?<12>a)\12{0}b", "ab"),
            (@"(?<12>a)\12{2}", "aaa"), (@"\12(?<12>a){0}", "\n"),
            (@"(?<012>a)\12(?<12>b)", "aab"),
            (@"(?<x>a)(b)(c)(d)(e)(f)(g)(h)(i)(j)(k)(l)\12", "abcdefghijkla"),
            (@"(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\10", "abcdefghijj"),
            (@"\10(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)", "jabcdefghij"),
            (@"(?<12>a)\12(?(12)b|c)", "aab"),
            (@"(?<12>a)\12(?<-12>b)", "aab"),
            (@"(?>(?<12>a)\12)b", "aab"),
            (@"\k<0>", ""), (@"\k<00>", ""), (@"\k'000'", ""),
            (@"\k<0>*", "a"), (@"\k<00>{0}", "a"),
            (@"(?:\k<0>|a)+", "aaa"), (@"(?!\k<0>)a", "a"),
            (@"(?<-0>a)", "a"), (@"(?<x-00>a)|b", "b"),
            (@"(?<x>a)(?<-0>b)?", "ab"), (@"(?<-0>a){0}b", "b"),
            (@"(?<x-0>a){0}(?(x)b|c)", "c"),
            (@"\1{0}", ""), (@"\8", ""), (@"\89", ""),
            (@"\2147483648", ""), (@"\999999999999999", ""),
            (@"\2147483648{0}", ""), (@"(?<-0>\1)", ""),
            (@"\2147483647", "\u008c7483647"), (@"\2147483647{0}", "\u008c748364"),
            (@"\178{2x}", "\u000f8{2x}"), (@"\178{1,2}?", "\u000f88"),
            (@"[a-z]+", "aaa"), (@"[A-Za-z]", "a"), (@"[\x08-a]+", "a\b"),
            (@"\178{2147483648}", ""), (@"\178{2,1}", ""),
            (@"{2}", ""), (@"a{2147483648}", ""),
            (@"\p{L}}", "a}"),
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
    public void NumericBindingPreservesEveryOwnedPublicOperation(string pattern, string witness, RegexOptions options)
    {
        string[] texts = ["", witness, "x" + witness + "y", witness + witness,
            "aaab", "\n\b\u0001\u000f", "\u00ff\u0178\u01ff\u0100\0", "\U0001f600a\U0001f600"];
        Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options, texts, options);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ExplicitCaptureAndWhitespaceUseTheFinalNamedCatalog(bool compiled, bool rtl)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture |
            RegexOptions.IgnorePatternWhitespace |
            (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(
            @"(a) (?<12>b) \12 # final group identity", options, ["", "abb", "bba", "a\U0001f600bb"], options);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(
            @"\1789 {0}", options, ["", "\u000f8", "\u000f89"], options);
    }

    [Theory]
    [InlineData(@"\777", 255)]
    [InlineData(@"\400", 0)]
    [InlineData(@"\7777{0}", 255)]
    public void FrontEndAndVmShareTheWrappedOctalValue(string pattern, int value)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        Assert.Contains(program.Instructions, instruction =>
            instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.A == value);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(((char)value).ToString())));
    }
    [Theory]
    [InlineData(@"(\178)+")]
    [InlineData(@"(?:\178)+")]
    [InlineData(@"((?:\178){2})")]
    [InlineData(@"(?:\178|a)+")]
    public void FrontEndReplayKeepsEnclosingOperationsStructurallyValid(string pattern)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var pending = new Stack<Utf8TreeNode>();
        pending.Push(tree.Root);
        while (pending.Count != 0)
        {
            var node = pending.Pop();
            if (node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop)
                Assert.Equal(1, node.ChildCount);
            for (var child = 0; child < node.ChildCount; child++)
            {
                Assert.Same(node, node.Child(child).Parent);
                pending.Push(node.Child(child));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NumericReferenceJournalUsesSharedLimitsAndRecovers(bool compiled)
    {
        const string pattern = @"\A(?<12>c(?<12>a)+b)\12\z";
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var text = "c" + new string('a', 16) + "b";
        var input = Encoding.ASCII.GetBytes(text + text);
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.IsMatch(program, input, starts, ends, ref baseline).IsMatch);
        Assert.True(baseline.Budget.Steps > 64);
        Assert.True(baseline.Budget.PeakDepth > 1);
        Assert.Equal(0UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(work, 0, 0),
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            var outcome = Utf8SliceRunner.IsMatch(program, input, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, outcome.Breach);
            Assert.False(outcome.IsMatch);
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
        for (uint depth = 1; depth < baseline.Budget.PeakDepth; depth++)
        {
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, depth, 0),
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.Equal(Utf8ExecutionLimitKind.Depth,
                Utf8SliceRunner.IsMatch(program, input, starts, ends, ref invocation).Breach);
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
        var recovered = regex.MatchDetailed(input);
        Assert.Equal(text, recovered.GetGroup(12).GetValueString());
        Assert.Equal(17, recovered.GetGroup(12).CaptureCount);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void NativeAsciiUnitCountAndSplitKeepCancellationAcrossMatches(bool compiled, bool split)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("a", options);
        var input = Encoding.ASCII.GetBytes(new string('a', 2000));
        using var source = new CancellationTokenSource();
        ulong checkpoint = 0;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            checkpoint = steps;
            if (steps >= 64) source.Cancel();
        });
        try
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
            {
                if (split)
                {
                    var pieces = regex.EnumerateSplits(input, 0, source.Token);
                    while (pieces.MoveNext()) { }
                }
                else
                    _ = regex.CountFromUtf16Offset(input, 0, source.Token);
            });
            Assert.Equal(source.Token, failure.CancellationToken);
            Assert.Equal(64UL, checkpoint);
        }
        finally { session.Complete(); }
        Assert.Equal(2000, regex.CountFromUtf16Offset(input, 0));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

}
