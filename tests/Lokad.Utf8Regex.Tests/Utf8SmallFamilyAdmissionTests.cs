using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Search;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SmallFamilyAdmissionTests
{
    private static byte[][] Bytes(params string[] values) => values.Select(Encoding.UTF8.GetBytes).ToArray();
    private static byte[][] Family(int id) => id switch
    {
        0 => [], 1 => Bytes("aa"), 2 => Bytes("aa", "bb"), 3 => Bytes("a", "bb", "cc"),
        4 => Bytes("éaa", "bbb", "ccc"), 5 => Bytes("aaa", "abb", "acc"),
        6 => Bytes("A0A0", "B0B0", "C0C0"), 7 => Bytes("aaa", "aaa", "bbb"),
        8 => Bytes("aa", "aaa", "bb", "bbb"), 9 => Bytes("a0x", "a1y", "b0x", "b1y"),
        10 => Bytes("alpha", "bravo", "charlie", "delta", "echo", "foxtrot"),
        11 => [[], "aa"u8.ToArray(), "bb"u8.ToArray()],
        12 => Enumerable.Range(0, 7).Select(i => new byte[] { (byte)(65 + i), 65 }).ToArray(),
        13 => Bytes("alpha", "alphabet", "bravo"),
        14 => Bytes("a" + new string('x', 64) + "A", "a" + new string('x', 64) + "B", "b" + new string('x', 64) + "C"),
        _ => Enumerable.Range(0, 3 + id % 4).Select(i => Enumerable.Range(0, 2 + id % 11).Select(j => (byte)(33 + (i * 7 + j + id) % 90)).ToArray()).ToArray()
    };
    private static bool Eligible(byte[][] values) => values.Length is >= 3 and <= 6 && values.All(v => v.Length >= 2 && v.All(b => b < 128))
        && values.Select(v => v[0]).Distinct().Count() >= 2;
    public static IEnumerable<object[]> Families() => Enumerable.Range(0, 26).Select(i => new object[] { i });
    public static IEnumerable<object[]> Cutoffs() => from family in new[] { 6, 7, 9, 10 }
        from bytes in new[] { false, true } select new object[] { family, bytes };
    public static IEnumerable<object[]> Depths() => from family in new[] { 4, 5, 6, 10 }
        from depth in Enumerable.Range(0, 5) select new object[] { family, depth };
    private static object? Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);
    private static object? Property(object value, string name) => value.GetType().GetProperty(name)!.GetValue(value);
    private static ulong SearchLoan(PreparedByteSearch search) => Utf8ConstructionBudget.ArrayBytes<byte>(search.Count) + (search.Count > 3 ? 176ul : 0);
    private static ulong DataLoan(AsciiExactLiteralSearchData data)
    {
        if (data.Buckets is null) return 0;
        var loan = SearchLoan(data.FirstByteSearch) + Utf8ConstructionBudget.ArrayBytes<AsciiExactLiteralBucket>(data.Buckets.Length)
            + Utf8ConstructionBudget.ArrayBytes<int>(256);
        foreach (var bucket in data.Buckets)
        {
            loan += Utf8ConstructionBudget.ArrayBytes<byte[]>(bucket.Literals.Length) + Utf8ConstructionBudget.ArrayBytes<byte>(bucket.CommonPrefix.Length);
            if (bucket.SecondByteCandidates is not null) loan += Utf8ConstructionBudget.ArrayBytes<byte>(bucket.SecondByteCandidates.Length);
            if (bucket.PrefixDiscriminator.HasValue) loan += Utf8ConstructionBudget.ArrayBytes<byte[]>(256);
        }
        return loan;
    }
    private static int[] Ranking(byte[][] values) => Enumerable.Range(0, values.Min(v => v.Length))
        .Select(offset => new { offset, bytes = values.Select(v => v[offset]).Distinct().ToArray() })
        .Where(v => v.bytes.Length > 1).Select(v => new { v.offset, score = v.bytes.Sum(b => PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(b)) * 8 + v.bytes.Length })
        .OrderBy(v => v.score).ThenByDescending(v => v.offset).Take(4).Select(v => v.offset).ToArray();
    private static int Secondary(byte[][] values, int primary) => Enumerable.Range(0, values.Min(v => v.Length))
        .FirstOrDefault(offset => offset != primary && values.Select(v => (v[primary], v[offset])).Distinct().Count() == values.Length, -1);
    private static ulong ExtraLoan(byte[][] values)
    {
        var offsets = Ranking(values); var anchorCount = values.Select(v => v[offsets[0]]).Distinct().Count();
        var loan = Utf8ConstructionBudget.ArrayBytes<byte>(anchorCount) + (anchorCount > 3 ? 176ul : 0)
            + (offsets.Length == 1 ? 0 : 32ul + ((16ul * (ulong)(offsets.Length - 1) + 15) & ~15ul));
        foreach (var offset in offsets.Skip(1)) loan += Utf8ConstructionBudget.ArrayBytes<byte>(values.Select(v => v[offset]).Distinct().Count());
        if (values.Length != 3 && Secondary(values, offsets[0]) >= 0) loan += 32 + ((16ul * (ulong)values.Length + 15) & ~15ul);
        return loan;
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void EligibilityRankingDispatchBorrowingAndSelectedStorageAgreeWithIndependentModels(int family)
    {
        var literals = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var accepted = PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var search);
        Assert.Equal(Eligible(literals), accepted);
        if (!accepted) { Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); }
        else
        {
            var data = (AsciiExactLiteralSearchData)Field(search, "_searchData")!; var offsets = Ranking(literals);
            Assert.All(data.Buckets, bucket => { Assert.Empty(bucket.CommonPrefix); Assert.Null(bucket.SecondByteCandidates); });
            Assert.Equal(offsets[0], Field(search, "_anchorOffset"));
            var anchor = (PreparedByteSearch)Field(search, "_anchorSearch")!;
            Assert.Equal(literals.Select(v => v[offsets[0]]).Distinct().ToArray(), anchor.Values.ToArray());
            var filters = (Array)Field(search, "_filters")!; Assert.Equal(offsets.Length - 1, filters.Length);
            for (var i = 0; i < filters.Length; i++)
            {
                var filter = filters.GetValue(i)!;
                Assert.Equal(offsets[i + 1], Property(filter, "Offset"));
                Assert.Equal(literals.Select(v => v[offsets[i + 1]]).Distinct().ToArray(), (byte[])Property(filter, "Values")!);
            }
            var secondary = Secondary(literals, offsets[0]); var triple = Field(search, "_tripleDispatch")!; var pair = Field(search, "_pairDispatch")!;
            Assert.Equal(literals.Length == 3 && secondary >= 0, Property(triple, "HasValue"));
            Assert.Equal(literals.Length != 3 && secondary >= 0, Property(pair, "HasValue"));
            if (literals.Length == 3 && secondary >= 0)
                for (var i = 0; i < 3; i++) Assert.Same(literals[i], Property(triple, "Literal" + i));
            if (literals.Length != 3 && secondary >= 0)
            {
                Assert.Equal(secondary, Property(pair, "SecondaryOffset")); var entries = (Array)Property(pair, "Entries")!;
                for (var i = 0; i < entries.Length; i++) Assert.Same(literals[i], Property(entries.GetValue(i)!, "Literal"));
            }
            foreach (var bucket in data.Buckets)
                foreach (var value in bucket.Literals) Assert.Contains(literals, v => ReferenceEquals(value, v));
            Assert.Equal(112 + DataLoan(data) + ExtraLoan(literals), owner.Budget.ReservedBytes);
        }
        Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryStorageAndWorkCutoffPreservesFundedParentsAndKeepsAdmittedWork(int family, bool bytes)
    {
        var values = Family(family); ulong peak, work;
        using (var full = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = full.Budget.AllocateObject(17, static value => new Box(value));
            Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(values, out _));
            peak = full.Budget.PeakBytes - 112; work = full.Budget.WorkSteps - 2;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (bytes ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? 112 + allowance : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + allowance, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            PreparedSmallAsciiLiteralFamilySearch search = default;
            var error = Record.Exception(() => Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(values, out search)));
            if (error is null)
            { successes++; Assert.Equal(112 + DataLoan((AsciiExactLiteralSearchData)Field(search, "_searchData")!) + ExtraLoan(values), owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                Assert.True(owner.Budget.WorkSteps >= 2 + (bytes || allowance > 0 ? 1ul : 0));
            }
            Assert.InRange(owner.Budget.WorkSteps, 2ul, bytes ? ulong.MaxValue : 2 + allowance);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(17, parent.Value);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void EligibilityAndNestedBytePreparationHaveBoundedDepthAndRefundFailure(int family, int depth)
    {
        var values = Family(family); var required = family is 4 or 5 ? 1 : family == 6 ? 3 : 4;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Record.Exception(() => PreparedSmallAsciiLiteralFamilySearch.TryCreate(values, out _));
        if (depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        else Assert.Null(error);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        if (depth == 0) { Assert.Equal(112ul, owner.Budget.PeakBytes); Assert.Equal(2ul, owner.Budget.WorkSteps); }
    }
    [Fact]
    public void OutOfDomainFamiliesNeedNoOwnerWorkStorageOrDepth()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        foreach (var values in new[] { null!, Family(0), Family(1), Family(2), Family(12) })
            Assert.False(PreparedSmallAsciiLiteralFamilySearch.TryCreate(values, out _));
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }
    [Fact]
    public void OneBucketDeclinesBeforeAcquiringUnusedExactMetadata()
    {
        using var owner = new Utf8ConstructionScope(0, 0, ulong.MaxValue, 1);
        Assert.False(PreparedSmallAsciiLiteralFamilySearch.TryCreate(Family(5), out _));
        Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.True(owner.Budget.WorkSteps > 0);
    }
    [Fact]
    public void SelectedMetadataStaysConstantAsDiscardedOffsetCandidatesGrow()
    {
        ulong? previous = null, previousTotal = null;
        foreach (var length in new[] { 8, 512 })
        {
            var values = Enumerable.Range(0, 3).Select(i => Enumerable.Repeat((byte)(65 + i), length).ToArray()).ToArray();
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(values, out var search));
            var metadata = owner.Budget.ReservedBytes - DataLoan((AsciiExactLiteralSearchData)Field(search, "_searchData")!);
            Assert.Equal(ExtraLoan(values), metadata); if (previous.HasValue) Assert.Equal(previous.Value, metadata); previous = metadata;
            if (previousTotal.HasValue) Assert.Equal(previousTotal.Value, owner.Budget.ReservedBytes);
            previousTotal = owner.Budget.ReservedBytes;
        }
    }
    [Fact]
    public void SeededFirstAndNonoverlappingDiscoveryAgreeWithIndependentOrderedMatching()
    {
        var random = new Random(749024); byte[] alphabet = [0, 65, 66, 67, 97, 98, 99, 120, 121];
        for (var sample = 0; sample < 300; sample++)
        {
            var values = Enumerable.Range(0, random.Next(3, 7)).Select(_ => Enumerable.Range(0, random.Next(2, 13))
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()).ToArray();
            if (!Eligible(values)) continue;
            Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(values, out var search));
            var input = Enumerable.Repeat((byte)'x', 40).Concat(values.SelectMany(v => new byte[] { 124 }.Concat(v))).Concat(alphabet).ToArray();
            var expected = new List<(int, int)>();
            for (var start = 0; start < input.Length;)
            {
                var value = values.FirstOrDefault(v => input.AsSpan(start).StartsWith(v));
                if (value is null) start++; else { expected.Add((start, value.Length)); start += value.Length; }
            }
            Assert.Equal(expected.Count, search.Count(input)); Assert.Equal(expected.Count, search.CountScalar(input));
            Assert.Equal(expected.Count > 0, search.TryFindFirst(input, out var index, out var length));
            Assert.Equal(expected.Count == 0 ? (-1, 0) : expected[0], (index, length));
            var actual = new List<(int, int)>(); var cursor = 0;
            while (search.TryFindNextNonOverlapping(input, ref cursor, out index, out length)) actual.Add((index, length));
            Assert.Equal(expected, actual);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DiscriminatorByteDoesNotProveTheSkippedCommonPrefix(bool appendValid)
    {
        var values = Family(14); var falseCandidate = values[0].ToArray(); falseCandidate[10] = (byte)'z';
        var input = appendValid ? falseCandidate.Concat(new byte[] { 124 }).Concat(values[2]).ToArray() : falseCandidate;
        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(values, out var search));
        Assert.Equal(appendValid ? 1 : 0, search.Count(input)); Assert.Equal(appendValid ? 1 : 0, search.CountScalar(input));
        Assert.Equal(appendValid, search.TryFindFirst(input, out var index, out var length));
        Assert.Equal(appendValid ? falseCandidate.Length + 1 : -1, index); Assert.Equal(appendValid ? values[2].Length : 0, length);
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex(string.Join("|", values.Select(Encoding.UTF8.GetString)),
                RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            Assert.True(regex.Inspection.DebugCanUseSmallAsciiLiteralFamilySplit(input));
            Assert.Equal(appendValid, regex.IsMatch(input)); Assert.Equal(appendValid ? 1 : 0, regex.Count(input));
            var gaps = new List<(int, int)>(); foreach (var gap in regex.EnumerateSplits(input)) gaps.Add((gap.IndexInBytes, gap.LengthInBytes));
            Assert.Equal(appendValid ? new[] { (0, falseCandidate.Length + 1), (input.Length, 0) } : new[] { (0, input.Length) }, gaps);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicAsciiFamilySplitRetainsSupplementaryGapByteRanges(bool compiled)
    {
        var regex = new Utf8Regex("alpha|bravo|charlie", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = "😀alpha|ébravo|中charlie!"u8; Assert.Equal(3, regex.Count(input));
        var ranges = new List<(int, int)>(); foreach (var split in regex.EnumerateSplits(input)) ranges.Add((split.IndexInBytes, split.LengthInBytes));
        Assert.Equal(new[] { (0, 4), (9, 3), (17, 4), (28, 1) }, ranges); Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
    private sealed class Box(int value) { internal readonly int Value = value; }
}
