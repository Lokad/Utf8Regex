using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MultiLiteralMapAdmissionTests
{
    public static IEnumerable<object[]> PackedFamilies() => Enumerable.Range(0, 12).Select(i => new object[] { i });
    public static IEnumerable<object[]> SegmentFamilies() => Enumerable.Range(0, 10).Select(i => new object[] { i });
    public static IEnumerable<object[]> RequestedOffsets() =>
        from family in Enumerable.Range(0, 12) from offset in new[] { -1, 0, 1, 2, 3, 4, 6, 260 } select new object[] { family, offset };
    public static IEnumerable<object[]> Cutoffs() =>
        from kind in Enumerable.Range(0, 3) from bytes in new[] { false, true } select new object[] { kind, bytes };
    public static IEnumerable<object[]> Depths() =>
        from kind in Enumerable.Range(0, 3) from depth in Enumerable.Range(0, 4) select new object[] { kind, depth };
    private static byte[][] Bytes(params string[] values) => values.Select(Encoding.UTF8.GetBytes).ToArray();
    private static byte[][] PackedFamily(int family) => family switch
    {
        0 => [], 1 => [[]], 2 => Bytes("a", "b", "c"), 3 => Bytes("ab", "cd", "ef"), 4 => Bytes("abc", "def", "ghi"),
        5 => Bytes("xa", "xb", "xc"), 6 => Bytes("aabx", "aacx", "aadx"), 7 => Bytes("abc", "abc"),
        8 => Enumerable.Range(0, 256).Select(i => new[] { (byte)i }).ToArray(),
        9 => Enumerable.Range(0, 257).Select(i => new[] { (byte)i }).ToArray(),
        10 => Bytes("é😀", "é中", "😀é", "😀中", "中é", "中😀"),
        _ => Bytes("éalpha", "éfizz", "étail"),
    };
    private static byte[][] SegmentFamily(int family) => family switch
    {
        0 => [], 1 => [[]], 2 => Bytes("éx", "éy", "étail"), 3 => Bytes("中abc", "中tail", "文xy", "字tail"),
        4 => Bytes("中中文", "中中甲", "中中乙", "文文甲"), 5 => Bytes("éx", "ascii"), 6 => Bytes("😀x"),
        7 => Enumerable.Range(0, 100).Select(i => Encoding.UTF8.GetBytes("中文" + i)).ToArray(),
        8 => Enumerable.Range(0, 64).Select(i => Encoding.UTF8.GetBytes(new Rune(0x4e00 + i).ToString() + "tail")).ToArray(),
        _ => Bytes("é"),
    };
    private static int Discriminator(byte[][] literals)
    {
        if (literals.Length == 0) return -1;
        return Enumerable.Range(0, literals.Min(v => v.Length))
            .Where(offset => literals.Select(v => v[offset]).Distinct().Count() == literals.Length).DefaultIfEmpty(-1).First();
    }
    private static ulong SearchLoan(PreparedByteSearch search) => Utf8ConstructionBudget.ArrayBytes<byte>(search.Count) + (search.Count > 3 ? 176ul : 0);
    private static ulong PackedLoan(PreparedMultiLiteralPackedSearch p) => Utf8ConstructionBudget.ArrayBytes<byte[]>(256)
        + Utf8ConstructionBudget.ArrayBytes<int>(256) + (p.HasSecondByteAnchor ? Utf8ConstructionBudget.ArrayBytes<byte>(256) : 0)
        + (p.HasThirdByteAnchor ? Utf8ConstructionBudget.ArrayBytes<byte>(256) : 0) + SearchLoan(p.DiscriminatorSearch);
    private static ulong SegmentLoan(PreparedMultiLiteralLeadingUtf8SegmentPrefilter p) => !p.HasValue ? 0
        : 128 + Utf8ConstructionBudget.ArrayBytes<byte[]>(p.Segments.Length)
            + p.Segments.Aggregate(0ul, (loan, segment) => loan + Utf8ConstructionBudget.ArrayBytes<byte>(segment.Length));

    private static void CheckPacked(PreparedMultiLiteralPackedSearch p, byte[][] values, int offset)
    {
        Assert.Same(values, p.Literals); Assert.Equal(offset, p.DiscriminatorOffset);
        var shortest = values.Min(v => v.Length); Assert.Equal(shortest, p.ShortestLength);
        Assert.Equal(offset + 1 < shortest, p.HasSecondByteAnchor); Assert.Equal(offset + 2 < shortest, p.HasThirdByteAnchor);
        Assert.Equal(p.HasSecondByteAnchor ? 256 : 0, p.SecondByteByDiscriminator.Length);
        Assert.Equal(p.HasThirdByteAnchor ? 256 : 0, p.ThirdByteByDiscriminator.Length);
        Assert.Equal(values.Select(v => v[offset]).ToArray(), p.DiscriminatorSearch.Values.ToArray());
        for (var b = 0; b < 256; b++)
        {
            var id = Array.FindIndex(values, v => v[offset] == b);
            Assert.Equal(id, p.LiteralIdByDiscriminator[b]);
            if (id < 0) { Assert.Null(p.LiteralByDiscriminator[b]); continue; }
            Assert.Same(values[id], p.LiteralByDiscriminator[b]);
            if (p.HasSecondByteAnchor) Assert.Equal(values[id][offset + 1], p.SecondByteByDiscriminator[b]);
            if (p.HasThirdByteAnchor) Assert.Equal(values[id][offset + 2], p.ThirdByteByDiscriminator[b]);
        }
    }

    [Theory]
    [MemberData(nameof(PackedFamilies))]
    public void PackedMapsAndLoansFollowIndependentUniquenessAndAnchorModels(int family)
    {
        var values = PackedFamily(family); var expected = Discriminator(values);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var accepted = PreparedMultiLiteralPackedSearch.TryCreate(values, out var p);
        Assert.Equal(expected >= 0, accepted); Assert.Equal(48 + (accepted ? PackedLoan(p) : 0), owner.Budget.ReservedBytes);
        if (accepted) CheckPacked(p, values, expected);
        else { Assert.Null(p.Literals); Assert.Null(p.LiteralByDiscriminator); Assert.Null(p.LiteralIdByDiscriminator); }
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0xa5, parent[0]);
    }

    [Theory]
    [MemberData(nameof(RequestedOffsets))]
    public void ExplicitOffsetsAdmitOnlyUniqueInRangeDiscriminators(int family, int offset)
    {
        var values = PackedFamily(family); var shortest = values.Length == 0 ? 0 : values.Min(v => v.Length);
        var expected = offset >= 0 && offset < shortest && values.Select(v => v[offset]).Distinct().Count() == values.Length;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var accepted = PreparedMultiLiteralPackedSearch.TryCreateAtDiscriminatorOffset(values, offset, out var p);
        Assert.Equal(expected, accepted); Assert.Equal(accepted ? PackedLoan(p) : 0, owner.Budget.ReservedBytes);
        if (accepted) CheckPacked(p, values, offset);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
    }

    private static int SegmentLength(byte[] value)
    {
        if (value.Length <= 2 || Rune.DecodeFromUtf8(value, out _, out var width) != OperationStatus.Done || width is not (2 or 3)) return 0;
        return width == 3 && Rune.DecodeFromUtf8(value.AsSpan(width), out _, out var second) == OperationStatus.Done && second == 3 ? 6 : width;
    }
    private static IGrouping<string, byte[]>[] Groups(byte[][] values) => values.Length == 0 || values.Any(v => SegmentLength(v) == 0) ? []
        : values.GroupBy(v => Convert.ToHexString(v.AsSpan(0, SegmentLength(v)))).ToArray();

    [Theory]
    [MemberData(nameof(SegmentFamilies))]
    public void SegmentProjectionPreservesFirstBucketAndLiteralOrderWithExactLoans(int family)
    {
        var values = SegmentFamily(family); var expected = Groups(values);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var p = new PreparedMultiLiteralLeadingUtf8SegmentPrefilter(values);
        Assert.Equal(values.Length == 0 ? int.MaxValue : values.Min(v => v.Length), p.ShortestLength);
        Assert.Equal(expected.Length, p.Segments.Length); Assert.Equal(48 + SegmentLoan(p), owner.Budget.ReservedBytes);
        for (var group = 0; group < expected.Length; group++)
        {
            Assert.Equal(expected[group].Key, Convert.ToHexString(p.Segments[group]));
        }
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0xa5, parent[0]);
    }

    private static ulong Prepare(int kind)
    {
        if (kind == 2) return SegmentLoan(new PreparedMultiLiteralLeadingUtf8SegmentPrefilter(SegmentFamily(2)));
        var values = PackedFamily(6);
        var accepted = kind == 0 ? PreparedMultiLiteralPackedSearch.TryCreate(values, out var p)
            : PreparedMultiLiteralPackedSearch.TryCreateAtDiscriminatorOffset(values, 2, out p);
        Assert.True(accepted); return PackedLoan(p);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryByteAndWorkCutoffRestoresParentObjectsAndRetainsSpentWork(int kind, bool bytes)
    {
        ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { _ = Prepare(kind); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (bytes ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? allowance + 112 : ulong.MaxValue,
                bytes ? ulong.MaxValue : allowance + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var loan = 0ul; var error = Record.Exception(() => loan = Prepare(kind));
            if (error is null) { successes++; Assert.Equal(112 + loan, owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, bytes ? ulong.MaxValue : allowance + 2);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void RootDepthAndNestedDiscriminatorSearchAreAdmittedBeforeScratch(int kind, int depth)
    {
        var required = kind == 2 ? 1 : 2;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Record.Exception(() => Prepare(kind));
        if (depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        else { Assert.Null(error); Assert.Equal((ulong)required, owner.Budget.PeakDepth); }
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        if (depth == 0) Assert.Equal(112ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyFamiliesBorrowAllStateWithoutAnyAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        Assert.False(PreparedMultiLiteralPackedSearch.TryCreate([], out _));
        Assert.False(PreparedMultiLiteralPackedSearch.TryCreateAtDiscriminatorOffset([], 0, out _));
        Assert.Empty(new PreparedMultiLiteralLeadingUtf8SegmentPrefilter([]).Segments);
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void UnsupportedLateSegmentDoesNotAcquireOrRetainAnAbandonedGroup()
    {
        using var owner = new Utf8ConstructionScope(0, 112, ulong.MaxValue, 1);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var p = new PreparedMultiLiteralLeadingUtf8SegmentPrefilter(SegmentFamily(5));
        Assert.False(p.HasValue); Assert.Empty(p.Segments); Assert.Equal(112ul, owner.Budget.PeakBytes);
        Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
    }

    private static (int Index, int Length, int Id) First(byte[][] values, ReadOnlySpan<byte> subject)
    {
        for (var index = 0; index < subject.Length; index++)
            for (var id = 0; id < values.Length; id++)
                if (subject[index..].StartsWith(values[id])) return (index, values[id].Length, id);
        return (-1, 0, -1);
    }

    [Fact]
    public void SeededPackedDiscoveryAndProgressionAgreeWithIndependentOrderedMatching()
    {
        var random = new Random(98217); byte[] alphabet = [0, 97, 98, 128, 255];
        for (var sample = 0; sample < 300; sample++)
        {
            var prefix = new byte[random.Next(0, 11)];
            var values = Enumerable.Range(0, random.Next(1, 21)).Select(id => prefix.Concat(new[] { (byte)(id + 1) })
                .Concat(Enumerable.Range(0, random.Next(2, 10)).Select(_ => alphabet[random.Next(alphabet.Length)])).ToArray()).ToArray();
            Assert.True(PreparedMultiLiteralPackedSearch.TryCreate(values, out var p));
            var subject = values.SelectMany(v => alphabet.Concat(v)).Concat(alphabet).ToArray();
            var expected = First(values, subject); var first = p.TryFindFirstMatch(subject, out var index, out var length, out var id);
            Assert.Equal(expected.Index >= 0, first); Assert.Equal(expected, (index, length, id));
            var last = -1; for (var start = 0; start < subject.Length; start++) if (First(values, subject.AsSpan(start)).Index == 0) last = start;
            Assert.Equal(last, p.LastIndexOf(subject));
            var from = 0; var state = default(PreparedMultiLiteralScanState);
            while (from <= subject.Length)
            {
                expected = First(values, subject.AsSpan(from)); var ok = p.TryFindNextNonOverlappingMatch(subject, ref state, out index, out length, out id);
                Assert.Equal(expected.Index >= 0, ok); if (!ok) break;
                Assert.Equal((from + expected.Index, expected.Length, expected.Id), (index, length, id));
                from = index + length; Assert.Equal(from, state.NextStart);
            }
        }
    }

    private static long MeasureSingleGroup(byte[][] values)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var before = GC.GetAllocatedBytesForCurrentThread(); var p = new PreparedMultiLiteralLeadingUtf8SegmentPrefilter(values);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Single(p.Segments); Assert.Equal("中文"u8.ToArray(), p.Segments[0]);
        Assert.Equal(SegmentLoan(p), owner.Budget.ReservedBytes); return allocated;
    }

    [Theory]
    [InlineData("ab|cx", 0)] [InlineData("xxcx", -1)]
    public async Task ReverseDiscoveryExcludesRejectedDiscriminatorsAndTerminates(string subject, int expected)
    {
        var p = PreparedMultiLiteralPackedSearch.Create(Bytes("ab", "cd"));
        var result = await Task.Run(() => p.LastIndexOf(Encoding.UTF8.GetBytes(subject))).WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SharedSegmentMaterializationKeepsConstantStorageWithoutDeadLiteralReferences()
    {
        var small = Enumerable.Range(0, 128).Select(i => Encoding.UTF8.GetBytes("中文" + i)).ToArray();
        var large = Enumerable.Range(0, 1024).Select(i => Encoding.UTF8.GetBytes("中文" + i)).ToArray();
        _ = MeasureSingleGroup(small); _ = MeasureSingleGroup(large);
        var smallBytes = MeasureSingleGroup(small); var largeBytes = MeasureSingleGroup(large);
        Assert.Equal(smallBytes, largeBytes);
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void PublicSupplementaryFamilyKeepsScalarRangesWithoutFallback(RegexOptions options)
    {
        var regex = new Utf8Regex("(?:é😀|é中|😀é|😀中|中é|中😀)", options); var subject = "xxé😀|é中|😀é|😀中|中é|中😀xx"u8;
        Assert.Equal(6, regex.Count(subject)); var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(subject)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(new[] { (2, 6), (9, 5), (15, 6), (22, 7), (30, 5), (36, 7) }, ranges);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
    private sealed class Box(int value) { public readonly int Value = value; }
}
