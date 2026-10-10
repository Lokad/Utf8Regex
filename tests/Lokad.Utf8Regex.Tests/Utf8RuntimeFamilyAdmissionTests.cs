using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RuntimeFamilyAdmissionTests
{
    private static byte[][] Bytes(params string[] values) => values.Select(Encoding.UTF8.GetBytes).ToArray();
    private static byte[][] Family(int id) => id switch
    {
        0 => [], 1 => Bytes("abc", "def"), 2 => Bytes("a0x", "a1y", "b0x", "b1y"),
        3 => Bytes("aaa", "aaa", "bbb"), 4 => Bytes("matches", "words", "shape"), 5 => Bytes("ab", "bc"),
        6 => Bytes("😀", "中"), 7 => Bytes("中文词", "中文人", "日本人"),
        8 => Bytes("中文", "日本", "韩国"), 9 => Bytes("中", "日", "韩"),
        10 => [[], "abc"u8.ToArray()],
        _ => Enumerable.Range(0, 2 + id % 4).Select(i => Enumerable.Range(0, 3 + id % 7)
            .Select(j => (byte)(33 + (i * 7 + j + id) % 90)).ToArray()).ToArray()
    };
    public static IEnumerable<object[]> Families() => from family in Enumerable.Range(0, 24)
        from kind in new[] { 0, 1 } select new object[] { family, kind };
    public static IEnumerable<object[]> Cutoffs() => from family in new[] { 1, 2 }
        from kind in new[] { 0, 1 } from bytes in new[] { false, true } select new object[] { family, kind, bytes };
    public static IEnumerable<object[]> Depths() => from kind in new[] { 0, 1 }
        from depth in Enumerable.Range(0, 5) select new object[] { kind, depth };
    private static object? Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);

    private static (int Primary, int Secondary)? Pair(byte[][] values)
    {
        if (values.Length is < 2 or > 4 || values.Any(v => v.Length is < 3 or > 8 || v.Any(b => b >= 128))) return null;
        var length = values.Min(v => v.Length);
        int Score(int offset) { var bytes = values.Select(v => v[offset]).Distinct().ToArray(); return bytes.Sum(b => PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(b)) * 8 + bytes.Length; }
        var choices = (from first in Enumerable.Range(0, length) from second in Enumerable.Range(0, length)
            where first != second && values.Select(v => v[first]).Distinct().Count() > 1 && values.Select(v => v[second]).Distinct().Count() > 1
                && values.Select(v => (v[first], v[second])).Distinct().Count() == values.Length
            orderby Score(first) * 4 + Score(second), first, second select (first, second)).ToArray();
        return choices.Length == 0 ? null : choices[0];
    }
    private static byte[][] Scalars(byte[][] values, int offset) => values.Select(v => v.Skip(offset).Take(3).ToArray())
        .GroupBy(Convert.ToHexString).Select(g => g.First()).ToArray();
    private static ulong ScalarLoan(byte[][] values) => Utf8ConstructionBudget.ArrayBytes<byte[]>(values.Length) + (ulong)values.Length * 48;

    [Theory]
    [MemberData(nameof(Families))]
    public void OrderedScalarGuardsAndBestPairsUseOnlyFundedFinalPayloads(int family, int kind)
    {
        var values = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        if (kind == 0)
        {
            var pair = Pair(values); var accepted = PreparedShortAsciiLiteralFamilyCounter.TryCreate(values, out var counter);
            Assert.Equal(pair.HasValue, accepted);
            var count = accepted ? values.Select(v => v[pair!.Value.Primary]).Distinct().Count() : 0;
            Assert.Equal(112 + (accepted ? Utf8ConstructionBudget.ArrayBytes<byte>(count) + (count > 3 ? 176ul : 0) : 0), owner.Budget.ReservedBytes);
            if (accepted)
            {
                Assert.Equal(pair!.Value.Primary, Assert.IsType<int>(Field(counter, "_primaryOffset")));
                Assert.Equal(pair.Value.Secondary, Assert.IsType<int>(Field(counter, "_secondaryOffset")));
                var search = Assert.IsType<PreparedByteSearch>(Field(counter, "_primarySearch"));
                Assert.Equal(values.Select(v => v[pair.Value.Primary]).Distinct().ToArray(), search.Values.ToArray());
                for (var i = 0; i < values.Length; i++) Assert.Same(values[i], Field(counter, "_literal" + i));
            }
        }
        else
        {
            var accepted = values.Length > 0 && values.All(v => v.Length >= 3);
            Assert.Equal(accepted, PreparedBmpThreeByteLiteralFamilySearch.TryCreate(values, out var search));
            ulong loan = 0;
            if (accepted)
            {
                Assert.Same(values, search.Literals); var shortest = values.Min(v => v.Length);
                Assert.Equal(shortest, search.ShortestLength);
                var first = Scalars(values, 0); var second = shortest >= 6 ? Scalars(values, 3) : []; var third = shortest >= 9 ? Scalars(values, 6) : [];
                Assert.Equal(first, search.FirstScalars); Assert.Equal(second, search.SecondScalars); Assert.Equal(third, search.ThirdScalars);
                loan = ScalarLoan(first) + ScalarLoan(second) + ScalarLoan(third) + 128ul * (ulong)(1 + (shortest >= 6 ? 1 : 0) + (shortest >= 9 ? 1 : 0));
            }
            Assert.Equal(112 + loan, owner.Budget.ReservedBytes);
        }
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
    }

    private static bool Prepare(byte[][] values, int kind) => kind == 0
        ? PreparedShortAsciiLiteralFamilyCounter.TryCreate(values, out _) : PreparedBmpThreeByteLiteralFamilySearch.TryCreate(values, out _);
    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryStorageAndWorkCutoffRefundsChildrenPreservesParentsAndKeepsWorkSpent(int family, int kind, bool bytes)
    {
        var values = Family(family); ulong peak, work, loan;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = funded.Budget.AllocateObject(17, static value => new Box(value)); Assert.True(Prepare(values, kind));
            peak = funded.Budget.PeakBytes - 112; work = funded.Budget.WorkSteps - 2; loan = funded.Budget.ReservedBytes;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (bytes ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? 112 + allowance : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + allowance, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => Assert.True(Prepare(values, kind)));
            if (error is null) { successes++; Assert.Equal(loan, owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            Assert.InRange(owner.Budget.WorkSteps, 2ul, bytes ? ulong.MaxValue : 2 + allowance);
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void RootsAndSelectedByteSearchDenyDepthBeforeAcquisition(int kind, int depth)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        // Four distinct primary values exercise the owned set's extra frame.
        // Family 2 now needs only a two-byte search after membership deduplication.
        var error = Record.Exception(() => Assert.True(Prepare(Family(14), kind)));
        if (depth < (kind == 0 ? 3 : 1))
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        else Assert.Null(error);
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        if (depth == 0) Assert.Equal(112ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyAndOutOfCountFamiliesBorrowAllStateWithoutAnAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        Assert.False(PreparedShortAsciiLiteralFamilyCounter.TryCreate([], out _));
        Assert.False(PreparedShortAsciiLiteralFamilyCounter.TryCreate(Bytes("abc"), out _));
        Assert.False(PreparedBmpThreeByteLiteralFamilySearch.TryCreate([], out _));
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void IdenticalBmpScalarGuardsHaveConstantStorageAsFamilyGrows()
    {
        ulong[] loans = [0, 0]; var counts = new[] { 8, 1024 };
        for (var i = 0; i < counts.Length; i++)
        {
            var values = Enumerable.Range(0, counts[i]).Select(_ => Encoding.UTF8.GetBytes("中文词")).ToArray();
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 1);
            Assert.True(PreparedBmpThreeByteLiteralFamilySearch.TryCreate(values, out var search));
            Assert.Single(search.FirstScalars); Assert.Single(search.SecondScalars); Assert.Single(search.ThirdScalars);
            loans[i] = owner.Budget.ReservedBytes;
        }
        Assert.Equal(672ul, loans[0]); Assert.Equal(loans[0], loans[1]);
    }

    [Fact]
    public void SeededPairSelectionAndCountingAgreeWithIndependentOrderedMatching()
    {
        var random = new Random(923848);
        for (var sample = 0; sample < 250; sample++)
        {
            var values = Enumerable.Range(0, random.Next(2, 5)).Select(_ => Enumerable.Range(0, random.Next(3, 9))
                .Select(_ => (byte)random.Next(97, 102)).ToArray()).ToArray();
            var pair = Pair(values); Assert.Equal(pair.HasValue, PreparedShortAsciiLiteralFamilyCounter.TryCreate(values, out var counter));
            if (!pair.HasValue) continue;
            Assert.Equal(pair.Value.Primary, Assert.IsType<int>(Field(counter, "_primaryOffset"))); Assert.Equal(pair.Value.Secondary, Assert.IsType<int>(Field(counter, "_secondaryOffset")));
            var input = values.SelectMany(v => new byte[] { 124 }.Concat(v)).Concat("|miss"u8.ToArray()).ToArray();
            var expected = 0;
            for (var start = 0; start < input.Length; start++)
            { var literal = values.FirstOrDefault(v => input.AsSpan(start).StartsWith(v)); if (literal is null) continue; expected++; start += literal.Length - 1; }
            Assert.Equal(expected, counter.Count(input));
        }
    }

    [Theory]
    [InlineData(2, false)] [InlineData(2, true)] [InlineData(4, false)] [InlineData(4, true)]
    [InlineData(7, false)] [InlineData(7, true)] [InlineData(8, false)] [InlineData(8, true)]
    public void PublicFamiliesKeepWholeScalarByteRangesAndOwnedExecution(int family, bool compiled)
    {
        var values = Family(family); var regex = new Utf8Regex(string.Join("|", values.Select(v => Regex.Escape(Encoding.UTF8.GetString(v)))),
            RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = values.SelectMany(v => "😀|"u8.ToArray().Concat(v)).Concat("é!"u8.ToArray()).ToArray();
        Assert.Equal(values.Length, regex.Count(input)); Assert.True(regex.IsMatch(input));
        var ranges = new List<(int, int)>(); var last = 0;
        for (var start = 0; start < input.Length; start++)
        { var literal = values.FirstOrDefault(v => input.AsSpan(start).StartsWith(v)); if (literal is null) continue; ranges.Add((last, start - last)); last = start + literal.Length; start = last - 1; }
        ranges.Add((last, input.Length - last)); var actual = new List<(int, int)>();
        foreach (var gap in regex.EnumerateSplits(input)) actual.Add((gap.IndexInBytes, gap.LengthInBytes));
        Assert.Equal(ranges, actual); Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
    private sealed class Box(int value) { internal readonly int Value = value; }
}
