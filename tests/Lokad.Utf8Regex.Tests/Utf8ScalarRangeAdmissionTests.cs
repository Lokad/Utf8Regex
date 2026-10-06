using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarRangeAdmissionTests
{
    [Theory]
    [InlineData("[\U00010400-\uffff]")]
    [InlineData("[\U0001f64f-\U0001f600]")]
    [InlineData(@"[\uD801\uDC00-\uffff]")]
    public void DescendingSupplementaryBoundsRejectScalarOrder(string pattern)
    {
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var failure = Assert.Throws<ArgumentException>(() => new Utf8Regex(pattern, options));
            Assert.Equal("pattern", failure.ParamName);
            Assert.Contains("Unicode scalar order", failure.Message);
        }
    }

    [Fact]
    public void ClassWireBmpQueriesRemainConsistentForMixedScalarRanges()
    {
        const string pattern = "[A-Z\U00010400-\U00010402-[B\U00010401]]";
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var root = tree.Root;
        while (root.ChildCount == 1) root = root.Child(0);
        Assert.Equal(Utf8TreeNodeKind.Set, root.Kind);
        Assert.NotNull(root.Str);
        Assert.True(Utf8OwnedCharClass.CharInClass('A', root.Str));
        Assert.False(Utf8OwnedCharClass.CharInClass('B', root.Str));
        Assert.False(Utf8OwnedCharClass.CharInClass('a', root.Str));
        var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(2, regex.Count(Encoding.UTF8.GetBytes("\U00010400\U00010401\U00010402")));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WorkLimitStopsSupplementaryInsensitiveReferenceAfterCaptureAndRecovers(bool rightToLeft)
    {
        var options = RegexOptions.CultureInvariant | (rightToLeft ? RegexOptions.RightToLeft : RegexOptions.None);
        var pattern = rightToLeft ? @"(?i)^\1([\s\S]{80})$" : @"(?i)^([\s\S]{80})\1$";
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U00010400", 80)) +
            string.Concat(Enumerable.Repeat("\U00010428", 80)));
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        var start = rightToLeft ? input.Length : 0;
        Assert.True(Utf8SliceRunner.TryMatchAt(program, input, start, starts, ends, true, ref baseline).IsMatch);
        Assert.Equal(320, ends[0] - starts[0]);
        // Leave forty scalar comparisons plus the end anchor/accept unperformed.
        var limit = baseline.Budget.Steps - 42;
        var denied = new Utf8SliceInvocation(new Utf8ExecutionLimits(limit, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var stopped = Utf8SliceRunner.TryMatchAt(program, input, start, starts, ends, true, ref denied);
        Assert.False(stopped.IsMatch);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, stopped.Breach);
        Assert.Equal(limit + 1, denied.Budget.Steps);
        Assert.All(starts, value => Assert.Equal(-1, value));
        Assert.All(ends, value => Assert.Equal(-1, value));
        Assert.Null(stopped.CaptureStarts);
        Assert.Null(stopped.CaptureLengths);
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        var recovered = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8SliceRunner.TryMatchAt(program, input, start, starts, ends, true, ref recovered).IsMatch);
        Assert.Equal(320, ends[0] - starts[0]);
        Assert.Equal(0UL, recovered.Ledger.ReservedBytes);
    }
}
