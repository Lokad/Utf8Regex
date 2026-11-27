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
    private static ulong FamilyBytes(byte[][] literals, out ulong peak)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        _ = new PreparedMultiLiteralSearch(literals, false);
        peak = owner.Budget.PeakBytes;
        return owner.Budget.ReservedBytes;
    }
    private static Utf8SearchFacts Fallback(byte[][] literals, byte[]? primary = null) =>
        Utf8SearchFacts.Create(Utf8SearchKind.None, new()
        {
            RequiredPrefilterLiteralUtf8 = primary ?? [],
            RequiredPrefilterAlternateLiteralsUtf8 = literals,
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
            Assert.Equal(payload, owner.Budget.ReservedBytes);
            Assert.Equal(payloadPeak, owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            var input = "x😀barabaé中"u8.ToArray();
            var expectedFirst = literals.Select(literal => input.AsSpan().IndexOf(literal)).Where(index => index >= 0).DefaultIfEmpty(-1).Min();
            Assert.Equal(expectedFirst, plan.MultiLiteralSearch.IndexOf(input));
            Assert.Equal(expectedFirst, plan.AlternateLiteralSearch.Value.IndexOf(input));
        }
    }

    [Fact]
    public void RequiredFamilyAcquiresOnlyItsPreparedSearcher()
    {
        foreach (var literals in new[] { Bytes("foo", "bar"), Bytes("alpha", "bravo", "charlie", "delta", "echo"),
            Bytes("😀tail", "😁tail", "😂tail"), Bytes("pre0", "pre1", "pre2", "pre3", "pre4", "pre5", "pre6", "pre7") })
        {
            var payload = FamilyBytes(literals, out var payloadPeak);
            var facts = Fallback(literals);
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var plan = Utf8SearchPlan.Prepare(facts);
            ref readonly var searcher = ref plan.RequiredPrefilterSearcher;
            Assert.Equal(PreparedSearcherKind.MultiLiteral, searcher.Kind);
            Assert.Equal(literals.Length, searcher.ExactMultiLiterals!.Length);
            Assert.Equal(0, searcher.FindFirst(literals[0]));
            Assert.False(plan.HasPreparedSearcher);
            Assert.Equal(payload, owner.Budget.ReservedBytes);
            Assert.Equal(payloadPeak, owner.Budget.PeakBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void WholePreparationCutoffsRollbackParentsAndBorrowedFactsAcrossRequiredSearchShapes()
    {
        var family = Bytes("foo", "bar");
        foreach (var facts in new[]
        {
            Utf8SearchFacts.Create(Utf8SearchKind.ExactAsciiLiterals, new() { AlternateLiteralsUtf8 = family }),
            Fallback(family), Fallback(family, "needle"u8.ToArray()),
        })
        {
            ulong peak, work, depth;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                _ = Utf8SearchPlan.Prepare(facts);
                peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; depth = funded.Budget.PeakDepth;
            }
            Assert.InRange(peak, 0ul, 100000ul); Assert.InRange(work, 1ul, 100000ul); Assert.InRange(depth, 1ul, 16ul);
            foreach (var bytes in new[] { true, false })
            {
                var limit = bytes ? peak : work;
                var belowLimit = limit == 0 ? 0 : limit - 1;
                foreach (var cutoff in new[] { 0ul, Math.Min(1ul, limit), limit / 2, belowLimit, limit }.Distinct())
                {
                    using var owner = new Utf8ConstructionScope(0, bytes ? 128 + cutoff : ulong.MaxValue,
                        bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new Parent(value));
                    var checkpoint = owner.Budget.ReservedBytes;
                    var error = Record.Exception(() => _ = Utf8SearchPlan.Prepare(facts));
                    if (cutoff == limit) Assert.Null(error);
                    else
                    {
                        var failure = Assert.IsType<Utf8ResourceLimitExceededException>(error);
                        Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
                        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                    }
                    Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
                    Assert.True(bytes ? owner.Budget.PeakBytes <= 128 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
                }
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
    public void PublicWindowAndFamilyOperationsKeepScalarByteRangesInEveryDirection()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(@"(?:é|😀)[\s\S]{0,2}中", options: options | RegexOptions.CultureInvariant);
            var input = "xéa😀中z😀中"u8.ToArray();
            Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(options.HasFlag(RegexOptions.RightToLeft) ? (12, 7) : (1, 10), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
            var expected = options.HasFlag(RegexOptions.RightToLeft) ? new[] { (12, 7), (1, 10) } : new[] { (1, 10), (12, 7) };
            var cursor = regex.EnumerateMatches(input); var ranges = new List<(int, int)>();
            while (cursor.MoveNext()) ranges.Add((cursor.Current.StartOffsetInBytes, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes)));
            Assert.Equal(expected, ranges);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, cancelled.Token));
            var direct = new Utf8Regex("é|😀|中", options: options | RegexOptions.CultureInvariant);
            Assert.Equal(5, direct.Count(input));
            Assert.Equal(2, new Utf8Regex("", options: options).Count("😀"u8));
        }
    }
}
