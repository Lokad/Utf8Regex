using System.Reflection;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Search;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FoldedLiteralSetAdmissionTests
{
    private static byte Fold(byte value) => value is >= 65 and <= 90 ? (byte)(value + 32) : value;
    private static byte[][] Bytes(params string[] values) => values.Select(Encoding.UTF8.GetBytes).ToArray();
    private static byte[][] Family(int id) => id switch
    {
        0 => [], 1 => [[]], 2 => ["discarded"u8.ToArray(), []], 3 => Bytes("A"),
        4 => Bytes("ALPHA", "alpha", "Alpine"), 5 => Bytes("bAB", "AA", "Ba", "a"),
        6 => Bytes("abcdefgh", "UVWxyzAB", "RSTuvwXY"),
        7 => Enumerable.Range(0, 8).Select(i => Encoding.UTF8.GetBytes("ABCDEFGHIJ" + i)).ToArray(),
        8 => Enumerable.Range(0, 9).Select(i => Encoding.UTF8.GetBytes("ABCDEFGHIJ" + i)).ToArray(),
        9 => Enumerable.Range(0, 256).Select(i => new byte[] { (byte)i, 65, 97 }).ToArray(),
        10 => Bytes("😀TaIl", "😁TOE", "éAA"), 11 => Bytes("A", "a", "B", "b", "Z", "z"),
        12 => [new byte[] { 0, 255 }, new byte[] { 128, 65 }, new byte[] { 255, 97 }],
        13 => Bytes("aaa", "AAAA", "AA"), 14 => Bytes("AaaaBbbbCccc", "aAAAbbBBdDDd"),
        15 => Bytes("xx", "Xx", "xX", "XX"),
        16 => ["before"u8.ToArray(), [], "after"u8.ToArray()],
        _ => Enumerable.Range(0, id - 15).Select(i => Enumerable.Repeat((byte)(65 + i), id).ToArray()).ToArray()
    };
    public static IEnumerable<object[]> Families() => Enumerable.Range(0, 20).Select(i => new object[] { i });
    public static IEnumerable<object[]> Cutoffs() => from family in new[] { 2, 5, 6, 8 }
        from bytes in new[] { false, true } select new object[] { family, bytes };
    public static IEnumerable<object[]> Depths() => from family in new[] { 2, 5, 6, 9 }
        from depth in Enumerable.Range(0, 5) select new object[] { family, depth };

    private static ulong SearchLoan(PreparedByteSearch search) => Utf8ConstructionBudget.ArrayBytes<byte>(search.Count)
        + (search.Count > 3 ? 176ul : 0);
    private static PreparedMultiLiteralPackedNibbleSimdPrefilter Prefilter(PreparedAsciiIgnoreCaseLiteralSetSearch set)
        => (PreparedMultiLiteralPackedNibbleSimdPrefilter)typeof(PreparedAsciiIgnoreCaseLiteralSetSearch)
            .GetProperty("CorrelatedPrefilter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(set)!;
    private static ulong ExpectedLoan(byte[][] values, PreparedMultiLiteralPackedNibbleSimdPrefilter masks)
    {
        if (values.Length == 0) return 0;
        var groups = values.Any(v => v.Length == 0) ? new[] { new[] { Array.Empty<byte>() } }
            : values.GroupBy(v => Fold(v[0])).Select(g => g.ToArray()).ToArray();
        var searchCount = values.Any(v => v.Length == 0) ? 0 : groups.SelectMany(g => Fold(g[0][0]) is >= 97 and <= 122
            ? new[] { Fold(g[0][0]), (byte)(Fold(g[0][0]) - 32) } : new[] { Fold(g[0][0]) }).Distinct().Count();
        var result = Utf8ConstructionBudget.ObjectBytes<PreparedAsciiIgnoreCaseLiteralSetSearchDataState>() + 64
            + Utf8ConstructionBudget.ArrayBytes<AsciiIgnoreCaseLiteralBucket>(groups.Length)
            + Utf8ConstructionBudget.ArrayBytes<int>(256) + Utf8ConstructionBudget.ArrayBytes<byte>(searchCount)
            + (searchCount > 3 ? 176ul : 0);
        foreach (var group in groups)
        {
            result += Utf8ConstructionBudget.ArrayBytes<byte[]>(group.Length);
            foreach (var value in group) result += Utf8ConstructionBudget.ArrayBytes<byte>(value.Length);
        }
        if (masks.HasValue)
            result += Utf8ConstructionBudget.ArrayBytes<int>(masks.MaskLength)
                + 2 * Utf8ConstructionBudget.ArrayBytes<Vector256<byte>>(masks.MaskLength) + SearchLoan(masks.Search);
        return result;
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void GroupingNormalizationFirstByteMembershipAndAllRetainedLoansAgreeWithIndependentModels(int family)
    {
        var literals = Family(family); var originals = literals.Select(v => v.ToArray()).ToArray();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        var search = new PreparedAsciiIgnoreCaseLiteralSetSearch(literals); var masks = Prefilter(search);
        Assert.Equal(literals.Length == 0 ? int.MaxValue : literals.Min(v => v.Length), search.ShortestLength);
        Assert.Equal(112 + ExpectedLoan(literals, masks), owner.Budget.ReservedBytes);
        var groups = literals.Length == 0 ? Array.Empty<byte[][]>() : literals.Any(v => v.Length == 0)
            ? new[] { new[] { Array.Empty<byte>() } } : literals.GroupBy(v => Fold(v[0])).Select(g => g.ToArray()).ToArray();
        Assert.Equal(groups.Length, search.Buckets.Length);
        for (var i = 0; i < groups.Length; i++)
        {
            Assert.Equal(groups[i][0].Length == 0 ? 0 : Fold(groups[i][0][0]), search.Buckets[i].FoldedFirstByte);
            Assert.Equal(groups[i].Length, search.Buckets[i].Literals.Length);
            for (var j = 0; j < groups[i].Length; j++)
            {
                Assert.Equal(groups[i][j].Select(Fold).ToArray(), search.Buckets[i].Literals[j]);
                if (groups[i][j].Length > 0) Assert.NotSame(groups[i][j], search.Buckets[i].Literals[j]);
            }
        }
        for (var value = 0; value < 256; value++)
        {
            var expected = search.ShortestLength is > 0 and < int.MaxValue && literals.Any(v => Fold(v[0]) == Fold((byte)value));
            Assert.Equal(expected, search.FirstByteSearch.IndexOf(new[] { (byte)value }) == 0);
            if (search.ShortestLength != int.MaxValue)
                Assert.Equal(Array.FindIndex(search.Buckets, b => b.FoldedFirstByte == value), search.BucketIndexMap[value]);
        }
        for (var i = 0; i < originals.Length; i++) Assert.Equal(originals[i], literals[i]);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(17, parent.Value);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryByteAndWorkCutoffRestoresParentStorageRetainersAndDepthWithoutRefundingWork(int family, bool bytes)
    {
        var values = Family(family); ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = funded.Budget.AllocateObject(17, static value => new Box(value));
            _ = new PreparedAsciiIgnoreCaseLiteralSetSearch(values);
            peak = funded.Budget.PeakBytes - 112; work = funded.Budget.WorkSteps - 2;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (bytes ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? 112 + allowance : ulong.MaxValue,
                bytes ? ulong.MaxValue : 2 + allowance, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
            var checkpoint = owner.Budget.ObjectCheckpoint; PreparedAsciiIgnoreCaseLiteralSetSearch search = default;
            var error = Record.Exception(() => search = new(values));
            if (error is null) { successes++; Assert.Equal(112 + ExpectedLoan(values, Prefilter(search)), owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            Assert.InRange(owner.Budget.WorkSteps, 2ul, bytes ? ulong.MaxValue : 2 + allowance);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(17, parent.Value);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void NestedSearchMaskAndDescriptorFailuresPreserveFundedParents(int family, int depth)
    {
        var values = Family(family); ulong required;
        using (var complete = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { _ = new PreparedAsciiIgnoreCaseLiteralSetSearch(values); required = complete.Budget.PeakDepth; }
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Record.Exception(() => _ = new PreparedAsciiIgnoreCaseLiteralSetSearch(values));
        if ((ulong)depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        else Assert.Null(error);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        if (depth == 0) { Assert.Equal(112ul, owner.Budget.PeakBytes); Assert.Equal(2ul, owner.Budget.WorkSteps); }
    }

    [Fact]
    public void EmptyAndDefaultFamiliesBorrowAllStorageWithoutWorkOrDepth()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        foreach (var search in new[] { default(PreparedAsciiIgnoreCaseLiteralSetSearch), new PreparedAsciiIgnoreCaseLiteralSetSearch([]) })
        {
            Assert.Empty(search.Buckets); Assert.Empty(search.BucketIndexMap); Assert.Equal(int.MaxValue, search.ShortestLength);
            Assert.Equal(-1, search.IndexOf("a"u8)); Assert.Equal(-1, search.LastIndexOf("a"u8));
            Assert.False(search.TryFindFirstMatchWithLength("a"u8, out _, out _));
        }
        Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }
    [Fact]
    public void DescriptorCarrierCoversEveryRetainedInstanceField()
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var descriptor = typeof(PreparedAsciiIgnoreCaseLiteralSetSearchData).GetFields(flags).Select(f => f.FieldType.FullName).Order().ToArray();
        var carrier = typeof(PreparedAsciiIgnoreCaseLiteralSetSearchDataState).GetFields(flags).Select(f => f.FieldType.FullName).Order().ToArray();
        Assert.Equal(5, descriptor.Length); Assert.Equal(descriptor, carrier);
    }
    [Fact]
    public void LateEmptyAlternativeNeverNormalizesAbandonedLiteralPayloads()
    {
        var huge = Enumerable.Repeat((byte)'A', 1_000_000).ToArray(); ulong? held = null, work = null;
        foreach (var first in new[] { "A"u8.ToArray(), huge })
        {
            using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue, 1);
            var search = new PreparedAsciiIgnoreCaseLiteralSetSearch([first, []]);
            Assert.Equal(0, search.ShortestLength); Assert.Empty(Assert.Single(Assert.Single(search.Buckets).Literals));
            if (held.HasValue) { Assert.Equal(held.Value, owner.Budget.ReservedBytes); Assert.Equal(work!.Value, owner.Budget.WorkSteps); }
            held = owner.Budget.ReservedBytes; work = owner.Budget.WorkSteps;
        }
        Assert.All(huge, v => Assert.Equal((byte)'A', v));
    }

    private static byte[]? Match(byte[][] literals, byte[] input, int index)
        => literals.FirstOrDefault(v => v.Length <= input.Length - index && v.Select(Fold).SequenceEqual(input.Skip(index).Take(v.Length).Select(Fold)));
    [Fact]
    public void SeededForwardReverseFusedAndNonOverlappingCountAgreeWithIndependentByteOracle()
    {
        var random = new Random(903487); byte[] alphabet = [0, 65, 97, 66, 98, 90, 122, 128, 255];
        for (var sample = 0; sample < 300; sample++)
        {
            var literals = Enumerable.Range(0, random.Next(1, 12)).Select(_ => Enumerable.Range(0, random.Next(1, 13))
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()).ToArray();
            var input = Enumerable.Repeat((byte)'a', sample % 2 == 0 ? 300 : 3).Concat(literals.SelectMany(v => new byte[] { 124 }.Concat(v)))
                .Concat(alphabet).ToArray(); var search = new PreparedAsciiIgnoreCaseLiteralSetSearch(literals);
            var first = -1; var last = -1; var firstLength = 0;
            for (var index = 0; index < input.Length; index++)
            {
                var expected = Match(literals, input, index); var found = search.TryGetMatchedLiteralLength(input, index, out var length);
                Assert.Equal(expected is not null, found); Assert.Equal(expected?.Length ?? 0, length);
                if (expected is null) continue;
                if (first < 0) { first = index; firstLength = expected.Length; } last = index;
            }
            Assert.Equal(first, search.IndexOf(input)); Assert.Equal(last, search.LastIndexOf(input));
            Assert.Equal(first >= 0, search.TryFindFirstMatchWithLength(input, out var matchIndex, out var matched));
            Assert.Equal(first, matchIndex); Assert.Equal(firstLength, matched);
            if (search.TryCountWithSelectiveCorrelatedPrefilter(input, out var count))
            {
                var expected = 0;
                for (var start = 0; start < input.Length;)
                { var literal = Match(literals, input, start); if (literal is null) start++; else { expected++; start += literal.Length; } }
                Assert.Equal(expected, count);
            }
        }
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.RightToLeft | RegexOptions.Compiled)]
    public void PublicFoldedSupplementaryAlternativesKeepScalarByteRanges(RegexOptions options)
    {
        var regex = new Utf8Regex("(?i:😀Tail|😁Toe|éAA)", options); var input = "xx😀tAIL|😁tOE|éaa"u8;
        Assert.Equal(3, regex.Count(input)); var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
        var expected = new[] { (2, 8), (11, 7), (19, 4) }; if ((options & RegexOptions.RightToLeft) != 0) Array.Reverse(expected);
        Assert.Equal(expected, ranges); Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
    private sealed class Box(int value) { internal readonly int Value = value; }
}
