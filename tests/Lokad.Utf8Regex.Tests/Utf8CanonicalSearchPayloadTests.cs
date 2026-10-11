using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CanonicalSearchPayloadTests
{
    private sealed class Parent(int value) { internal readonly int Value = value; }
    private static byte[][] Bytes(params string[] values) => values.Select(Encoding.UTF8.GetBytes).ToArray();
    private static readonly Utf8SearchAsciiSet Quoted = Utf8SearchAsciiSet.FromBytes("abcdefghijklmnopqrstuvwxyz"u8);
    private static PreparedMultiLiteralSearch Family(PreparedSearcher search) =>
        (PreparedMultiLiteralSearch)typeof(PreparedSearcher).GetProperty("MultiLiteralSearch", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(search)!;
    private static PreparedWindowSearch Window(Utf8StructuralSearchPlan plan) =>
        Assert.Single(plan.Stages.Where(stage => stage.Kind == Utf8StructuralSearchStageKind.FindWindow)).WindowSearch;
    private static ulong FamilyBytes(byte[][] literals, out ulong peak)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        _ = new PreparedMultiLiteralSearch(literals, false);
        peak = owner.Budget.PeakBytes;
        return owner.Budget.ReservedBytes;
    }
    private static Utf8SearchFacts Fallback(byte[][] literals, byte[]? primary = null, Utf8WindowSearchFacts[]? windows = null) =>
        Utf8SearchFacts.Create(Utf8SearchKind.None, new()
        {
            RequiredPrefilterLiteralUtf8 = primary ?? [],
            RequiredPrefilterAlternateLiteralsUtf8 = literals,
            SecondaryRequiredPrefilterQuotedAsciiSet = Quoted,
            SecondaryRequiredPrefilterQuotedAsciiLength = 3,
            RequiredWindowPrefilters = windows ?? [],
        });

    [Fact]
    public void DirectFamiliesShareOneExactPayloadAndAdmitOnlyItsReachableStorage()
    {
        foreach (var literals in new[] { Bytes("foo", "bar"), Bytes("a", "ab"), Bytes("é", "😀", "中"),
            Bytes("ab", "aba"), Bytes("pre0", "pre1", "pre2", "pre3", "pre4"), Bytes("x", "", "yz") })
        {
            var payload = FamilyBytes(literals, out var payloadPeak);
            var unicode = literals.Any(literal => literal.Any(b => b >= 128));
            var facts = Utf8SearchFacts.Create(unicode ? Utf8SearchKind.ExactUtf8Literals : Utf8SearchKind.ExactAsciiLiterals,
                new() { AlternateLiteralsUtf8 = literals });
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var plan = Utf8SearchPlan.Prepare(facts);
            Assert.Equal(PreparedMultiLiteralKind.ExactDirect, plan.MultiLiteralSearch.Kind);
            Assert.True(plan.AlternateLiteralSearch.HasValue);
            Assert.Same(plan.AlternateLiteralSearch.Value.SearchData.Buckets, plan.MultiLiteralSearch.ExactSearch.SearchData.Buckets);
            var expected = payload + Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchStage>(plan.StructuralSearchPlan.Stages.Length)
                + Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchPlan>(1)
                + (unicode ? Utf8ConstructionBudget.ArrayBytes<int>(literals.Length) : 0);
            Assert.Equal(expected, owner.Budget.ReservedBytes);
            Assert.Equal(Math.Max(payloadPeak + (unicode ? Utf8ConstructionBudget.ArrayBytes<int>(literals.Length) : 0), expected), owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            var input = "x😀barabaé中"u8.ToArray();
            var expectedFirst = literals.Select(literal => input.AsSpan().IndexOf(literal)).Where(index => index >= 0).DefaultIfEmpty(-1).Min();
            Assert.Equal(expectedFirst, plan.MultiLiteralSearch.IndexOf(input));
            Assert.Equal(expectedFirst, plan.AlternateLiteralSearch.Value.IndexOf(input));
        }
    }

    [Fact]
    public void FallbackWindowsBorrowPrimaryFamilyAndOneCanonicalCandidateVector()
    {
        foreach (var literals in new[] { Bytes("foo", "bar"), Bytes("alpha", "bravo", "charlie", "delta", "echo"),
            Bytes("😀tail", "😁tail", "😂tail"), Bytes("pre0", "pre1", "pre2", "pre3", "pre4", "pre5", "pre6", "pre7") })
        {
            var payload = FamilyBytes(literals, out var payloadPeak);
            var facts = Fallback(literals);
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var plan = Utf8SearchPlan.Prepare(facts);
            var windows = Assert.IsType<Utf8StructuralSearchPlan[]>(plan.PrefilterPlan.WindowPlans);
            Assert.Equal(2, windows.Length);
            Assert.Same(windows, plan.FallbackCandidatePlans);
            var primary = Family(plan.PrefilterPlan.PrimarySearcher);
            var first = Family(Window(windows[0]).LeadingSearcher);
            var second = Family(Window(windows[1]).TrailingSearcher);
            Assert.Same(primary.Literals, first.Literals);
            Assert.Same(primary.Literals, second.Literals);
            Assert.Same(primary.ExactSearch.SearchData.Buckets, first.ExactSearch.SearchData.Buckets);
            Assert.Equal(primary.Kind, first.Kind);
            Assert.Equal(PreparedSearcherKind.QuotedAsciiRun, Window(windows[0]).TrailingSearcher.Kind);
            var expected = payload + Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchPlan>(2)
                + windows.Aggregate(0ul, (sum, window) => sum + Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchStage>(window.Stages.Length));
            Assert.Equal(expected, owner.Budget.ReservedBytes);
            Assert.Equal(Math.Max(payloadPeak, expected), owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void DistinctPrimaryLiteralCannotReplaceTheFallbackFamilyAndInvalidWindowsDoNotAcquireChildren()
    {
        var literals = Bytes("foo", "bar");
        var invalid = Utf8WindowSearchFacts.WithinLines(Utf8PreparedSearcherInfo.LiteralFamily(literals),
            Utf8PreparedSearcherInfo.QuotedAsciiRun(default, 3), 5);
        foreach (var primaryLiteral in new byte[]?[] { null, "needle"u8.ToArray() })
        {
            var facts = Fallback(literals, primaryLiteral, [invalid]);
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var plan = Utf8SearchPlan.Prepare(facts);
            Assert.Equal(primaryLiteral is null ? PreparedSearcherKind.MultiLiteral : PreparedSearcherKind.ExactLiteral,
                plan.PrefilterPlan.PrimarySearcher.Kind);
            var windows = Assert.IsType<Utf8StructuralSearchPlan[]>(plan.FallbackCandidatePlans);
            var family = Family(Window(windows[0]).LeadingSearcher);
            Assert.Equal(1, family.IndexOf("xbar"u8));
            Assert.Equal(-1, family.IndexOf("needle"u8));
            Assert.Equal(primaryLiteral is null ? -1 : 0, plan.PrefilterPlan.PrimarySearcher.FindFirst("needle"u8));
            Assert.Same(windows, plan.PrefilterPlan.WindowPlans);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void WholePreparationCutoffsRollbackParentsAndBorrowedFactsAcrossWindowShapes()
    {
        var family = Bytes("foo", "bar");
        var valid = Utf8WindowSearchFacts.WithinLines(Utf8PreparedSearcherInfo.LiteralFamily(family),
            Utf8PreparedSearcherInfo.QuotedAsciiRun(Quoted, 3), 5);
        var invalid = Utf8WindowSearchFacts.WithinLines(Utf8PreparedSearcherInfo.LiteralFamily(family), default, 5);
        foreach (var facts in new[]
        {
            Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiterals, new() { AlternateLiteralsUtf8 = family }),
            Fallback(family), Fallback(family, "needle"u8.ToArray()),
            Fallback(family, windows: [invalid, valid, invalid, valid]),
            Utf8SearchFacts.Create(Utf8SearchKind.None, new()
            {
                OrderedWindowLeadingLiteralsUtf8 = Bytes("é", "😀"),
                OrderedWindowTrailingLiteralUtf8 = "中"u8.ToArray(), OrderedWindowMaxGap = 8,
            }),
        })
        {
            ulong peak, work, depth;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                _ = Utf8SearchPlan.Prepare(facts);
                peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; depth = funded.Budget.PeakDepth;
            }
            Assert.InRange(peak, 1ul, 100000ul); Assert.InRange(work, 1ul, 100000ul); Assert.InRange(depth, 1ul, 16ul);
            foreach (var bytes in new[] { true, false })
            foreach (var cutoff in new[] { 0ul, 1ul, (bytes ? peak : work) / 2, (bytes ? peak : work) - 1, bytes ? peak : work }.Distinct())
            {
                using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue,
                    bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new Parent(value));
                var checkpoint = owner.Budget.ReservedBytes;
                var error = Record.Exception(() => _ = Utf8SearchPlan.Prepare(facts));
                if (cutoff == (bytes ? peak : work)) Assert.Null(error);
                else
                {
                    var limit = Assert.IsType<Utf8ResourceLimitExceededException>(error);
                    Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, limit.ResourceKind);
                    Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                }
                Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
                Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
            }
            for (var cutoff = 0ul; cutoff <= depth; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, cutoff);
                var parent = owner.Budget.AllocateObject(17, static value => new Parent(value));
                var checkpoint = owner.Budget.ReservedBytes;
                var error = Record.Exception(() => _ = Utf8SearchPlan.Prepare(facts));
                if (cutoff == depth) Assert.Null(error);
                else
                {
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                    Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                }
                Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
            }
        }
    }

    [Fact]
    public void RejectedWindowEndpointsNeverAcquireTheOtherwiseLargeFamily()
    {
        var family = Enumerable.Range(0, 128).Select(i => Encoding.UTF8.GetBytes("prefix" + i)).ToArray();
        var invalid = new[]
        {
            Utf8WindowSearchFacts.WithinLines(Utf8PreparedSearcherInfo.LiteralFamily(family), default, 5),
            Utf8WindowSearchFacts.WithinLines(default, Utf8PreparedSearcherInfo.LiteralFamily(family), 5),
        };
        var facts = Utf8SearchFacts.Create(Utf8SearchKind.None, new() { RequiredWindowPrefilters = invalid });
        using var owner = new Utf8ConstructionScope(0, 0, 32, 4);
        var plan = Utf8SearchPlan.Prepare(facts);
        Assert.False(plan.HasFallbackCandidates); Assert.False(plan.HasRequiredPrefilter);
        Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void PublicWindowAndFamilyOperationsKeepScalarByteRangesInEveryDirection()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(@"(?:é|😀)[\s\S]{0,2}中", options | RegexOptions.CultureInvariant);
            var input = "xéa😀中z😀中"u8.ToArray();
            Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
            var match = regex.MatchDetailed(input);
            Assert.Equal(options.HasFlag(RegexOptions.RightToLeft) ? (12, 7) : (1, 10), (match.IndexInBytes, match.LengthInBytes));
            var expected = options.HasFlag(RegexOptions.RightToLeft) ? new[] { (12, 7), (1, 10) } : new[] { (1, 10), (12, 7) };
            var cursor = regex.EnumerateMatches(input); var ranges = new List<(int, int)>();
            while (cursor.MoveNext()) ranges.Add((cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
            Assert.Equal(expected, ranges);
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, cancelled.Token));
            var direct = new Utf8Regex("é|😀|中", options | RegexOptions.CultureInvariant);
            Assert.Equal(5, direct.Count(input));
            Assert.Equal(2, new Utf8Regex("", options).Count("😀"u8));
        }
    }
}
