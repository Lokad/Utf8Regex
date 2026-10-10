using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ExactLiteralSetAdmissionTests
{
    public static IEnumerable<object[]> Families() => Enumerable.Range(0, 17).Select(i => new object[] { i });
    public static IEnumerable<object[]> Modes() => from family in Enumerable.Range(0, 17) from borrowed in new[] { false, true } select new object[] { family, borrowed };
    public static IEnumerable<object[]> Cutoffs() => from kind in Enumerable.Range(0, 3) from bytes in new[] { false, true } select new object[] { kind, bytes };
    public static IEnumerable<object[]> Depths() => from kind in Enumerable.Range(0, 3) from depth in Enumerable.Range(0, 5) select new object[] { kind, depth };
    private static byte[][] Bytes(params string[] values) => values.Select(Encoding.UTF8.GetBytes).ToArray();
    private static byte[][] Family(int id) => id switch
    {
        0 => [], 1 => [[]], 2 => Bytes("ax", "by", "", "cz"), 3 => Bytes("a", "b", "c"),
        4 => Bytes("alpha"), 5 => Bytes("abc", "abd", "abef"), 6 => Bytes("a", "ab", "ac"),
        7 => Bytes("ab0", "ac1", "bd2", "be3"), 8 => Bytes("aa0", "aa1", "ba2", "ba3"),
        9 => Bytes("aX0", "aX1", "bX0", "bX1", "cX0", "cX1", "dX0", "dX1"),
        10 => Enumerable.Range(0, 12).Select(i => Encoding.UTF8.GetBytes("a" + (char)('b' + i) + "tail")).ToArray(),
        11 => Bytes("😀tail", "😁tail", "😀toe"), 12 => Bytes("abc", "abc"),
        13 => Enumerable.Range(0, 256).Select(i => new[] { (byte)i, (byte)127 }).ToArray(),
        14 => Enumerable.Range(0, 257).Select(i => new[] { (byte)97, (byte)i, (byte)127 }).ToArray(),
        15 => Bytes("aX0", "aX1", "bX0", "bX1", "cX0", "cX1", "dX0", "dX1", "a"),
        _ => Bytes("中abc", "文xy", "中tail", "字tail"),
    };
    private static byte[][][] Groups(byte[][] literals) => literals.Length == 0 ? [] : literals.Any(v => v.Length == 0) ? new[] { new byte[][] { [] } }
        : literals.GroupBy(v => v[0]).Select(g => g.ToArray()).ToArray();
    private static byte[] Prefix(byte[][] group)
    {
        var length = Enumerable.Range(0, group.Min(v => v.Length)).TakeWhile(i => group.All(v => v[i] == group[0][i])).Count();
        return length > 1 ? group[0].Take(length).ToArray() : [];
    }
    private static byte[]? Seconds(byte[][] group)
    {
        if (group.Length <= 1 || group.Any(v => v.Length < 2)) return null;
        var values = group.Select(v => v[1]).Distinct().ToArray(); return values.Length > 1 ? values : null;
    }
    private static int UniqueOffset(byte[][][] groups, int shortest)
    {
        var literals = groups.SelectMany(g => g).ToArray();
        if (literals.Length <= 1 || literals.Length > 256 || shortest < 2) return -1;
        return Enumerable.Range(1, shortest - 1).Where(i => literals.Select(v => v[i]).Distinct().Count() == literals.Length).DefaultIfEmpty(-1).First();
    }
    private static PreparedLiteralSetStrategy Strategy(byte[][] literals)
    {
        var groups = Groups(literals); if (groups.Length == 0) return PreparedLiteralSetStrategy.Empty;
        if (groups.Length == 1 && Prefix(groups[0]).Length > 1) return PreparedLiteralSetStrategy.SingleBucketPrefix;
        if (groups.Length == 1 && Seconds(groups[0]) is not null) return PreparedLiteralSetStrategy.Bucketed;
        if (groups.All(g => g.Length == 1)) return PreparedLiteralSetStrategy.SingleLiteralBuckets;
        var shortest = groups.SelectMany(g => g).Min(v => v.Length);
        if (UniqueOffset(groups, shortest) >= 0) return PreparedLiteralSetStrategy.UniqueAnchorByte;
        return shortest > 0 && groups.Sum(g => g.Length) >= 8 ? PreparedLiteralSetStrategy.MultiLiteralTrie : PreparedLiteralSetStrategy.Bucketed;
    }
    private static ulong SearchLoan(PreparedByteSearch search) => Utf8ConstructionBudget.ArrayBytes<byte>(search.Count) + (search.Count > 3 ? 176ul : 0);
    private static ulong DataLoan(AsciiExactLiteralSearchData data) => SearchLoan(data.FirstByteSearch)
        + Utf8ConstructionBudget.ArrayBytes<int>(data.BucketIndexMap.Length) + Utf8ConstructionBudget.ArrayBytes<AsciiExactLiteralBucket>(data.Buckets.Length)
        + data.Buckets.Aggregate(0ul, (sum, b) => sum + Utf8ConstructionBudget.ArrayBytes<byte[]>(b.Literals.Length)
            + Utf8ConstructionBudget.ArrayBytes<byte>(b.CommonPrefix.Length) + Utf8ConstructionBudget.ArrayBytes<byte>(b.SecondByteCandidates?.Length ?? 0)
            + (b.PrefixDiscriminator.HasValue ? Utf8ConstructionBudget.ArrayBytes<byte[]>(256) : 0));
    private static object? Property(PreparedLiteralSetSearch search, string name) => typeof(PreparedLiteralSetSearch).GetProperty(name, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(search);
    private static ulong ExtraLoan(PreparedLiteralSetSearch search)
    {
        if (search.Strategy == PreparedLiteralSetStrategy.UniqueAnchorByte)
        { var anchor = Assert.IsType<PreparedLiteralSetUniqueAnchor>(Property(search, "UniqueAnchor")); return SearchLoan(anchor.AnchorSearch) + Utf8ConstructionBudget.ArrayBytes<byte[]>(256); }
        if (search.Strategy != PreparedLiteralSetStrategy.MultiLiteralTrie) return 0;
        var trie = Assert.IsType<PreparedMultiLiteralTrieSearch>(Property(search, "MultiLiteral"));
        var nodes = Assert.IsType<PreparedMultiLiteralTrieNode[]>(typeof(PreparedMultiLiteralTrieSearch).GetField("_nodes", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(trie));
        return 256 + SearchLoan(trie.RootSearch) + Utf8ConstructionBudget.ArrayBytes<int>(256) + Utf8ConstructionBudget.ArrayBytes<PreparedMultiLiteralTrieNode>(nodes.Length)
            + nodes.Aggregate(0ul, (sum, n) => sum + Utf8ConstructionBudget.ArrayBytes<byte>(n.Keys.Length) + Utf8ConstructionBudget.ArrayBytes<int>(n.Children.Length));
    }

    [Theory]
    [MemberData(nameof(Families))]
    public void ExactBucketsFollowIndependentOrderPrefixAndMembershipModelsWithExactLoans(int family)
    {
        var literals = Family(family); var groups = Groups(literals);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        var data = AsciiSearch.CreateExactLiteralSearchData(literals);
        Assert.Equal(literals.Length == 0 ? int.MaxValue : literals.Min(v => v.Length), data.ShortestLength);
        Assert.Equal(groups.Length, data.Buckets.Length);
        Assert.Equal(literals.Length == 0 ? Array.Empty<byte>() : data.ShortestLength == 0 ? Array.Empty<byte>() : groups.Select(g => g[0][0]).ToArray(), data.FirstByteSearch.Values.ToArray());
        Assert.Equal(112 + DataLoan(data), owner.Budget.ReservedBytes);
        for (var group = 0; group < groups.Length; group++)
        {
            var bucket = data.Buckets[group]; var expected = groups[group]; var prefix = Prefix(expected);
            Assert.Equal(expected[0].Length == 0 ? 0 : expected[0][0], bucket.FirstByte);
            Assert.Equal(expected.Length, bucket.Literals.Length);
            for (var i = 0; i < expected.Length; i++) Assert.Same(expected[i], bucket.Literals[i]);
            Assert.Equal(prefix, bucket.CommonPrefix); Assert.Equal(Seconds(expected), bucket.SecondByteCandidates);
            var discriminator = expected.Length > 1 && prefix.Length > 0 && prefix.Length < expected.Min(v => v.Length)
                && expected.Select(v => v[prefix.Length]).Distinct().Count() == expected.Length;
            Assert.Equal(discriminator, bucket.PrefixDiscriminator.HasValue);
            if (discriminator)
                foreach (var literal in expected) Assert.Same(literal, bucket.PrefixDiscriminator.LiteralsByByte[literal[prefix.Length]]);
        }
        for (var value = 0; value < data.BucketIndexMap.Length; value++)
            Assert.Equal(Array.FindIndex(data.Buckets, b => b.FirstByte == value), data.BucketIndexMap[value]);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(17, parent.Value);
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void OnlySelectedStrategiesAcquireAnchorsOrTriesAndBorrowedDataIsNotChargedAgain(int family, bool borrowed)
    {
        var values = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        var data = borrowed ? AsciiSearch.CreateExactLiteralSearchData(values) : default;
        var search = borrowed ? new PreparedLiteralSetSearch(data) : new PreparedLiteralSetSearch(values);
        Assert.Equal(Strategy(values), search.Strategy);
        Assert.Equal(112 + DataLoan(search.SearchData) + ExtraLoan(search), owner.Budget.ReservedBytes);
        if (search.Strategy != PreparedLiteralSetStrategy.UniqueAnchorByte) Assert.Null(Property(search, "UniqueAnchor"));
        if (search.Strategy != PreparedLiteralSetStrategy.MultiLiteralTrie)
        { var trie = Assert.IsType<PreparedMultiLiteralTrieSearch>(Property(search, "MultiLiteral")); Assert.Null(trie.RootMap); }
        if (borrowed) Assert.Same(data.Buckets, search.SearchData.Buckets);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
    }

    private static byte[][] CutoffFamily(int kind) => Family(kind == 0 ? 5 : kind == 1 ? 7 : 9);
    private static ulong Prepare(int kind, AsciiExactLiteralSearchData borrowed) => kind == 0
        ? DataLoan(AsciiSearch.CreateExactLiteralSearchData(CutoffFamily(kind)))
        : ExtraLoan(new PreparedLiteralSetSearch(borrowed));
    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryByteAndWorkCutoffRefundsChildrenAndPreservesFundedParentAndSpentWork(int kind, bool bytes)
    {
        ulong peak, work, parentLoan = 112, parentWork = 2;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var data = kind == 0 ? default : AsciiSearch.CreateExactLiteralSearchData(CutoffFamily(kind));
            parentLoan += funded.Budget.ReservedBytes; parentWork += funded.Budget.WorkSteps;
            _ = funded.Budget.AllocateObject(17, static value => new Box(value));
            _ = Prepare(kind, data); peak = funded.Budget.PeakBytes - parentLoan; work = funded.Budget.WorkSteps - parentWork;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (bytes ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? parentLoan + allowance : ulong.MaxValue,
                bytes ? ulong.MaxValue : parentWork + allowance, ulong.MaxValue);
            var data = kind == 0 ? default : AsciiSearch.CreateExactLiteralSearchData(CutoffFamily(kind));
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var loan = 0ul; var error = Record.Exception(() => loan = Prepare(kind, data));
            if (error is null) { successes++; Assert.Equal(parentLoan + loan, owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(parentLoan, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            }
            Assert.InRange(owner.Budget.WorkSteps, parentWork, bytes ? ulong.MaxValue : parentWork + allowance);
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void PreparationOwnersAndNestedSearchDenyDepthBeforeAcquisition(int kind, int depth)
    {
        var values = kind == 0 ? Family(13) : kind == 1 ? Family(7) : Family(9);
        var required = kind == 2 ? 4 : 3;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Record.Exception(() => { if (kind == 0) _ = AsciiSearch.CreateExactLiteralSearchData(values); else _ = new PreparedLiteralSetSearch(values); });
        if (depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        else Assert.Null(error);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        if (depth == 0) Assert.Equal(112ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyFamiliesBorrowEveryExtentWithoutAnAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var data = AsciiSearch.CreateExactLiteralSearchData([]); Assert.Empty(data.Buckets); Assert.Empty(data.BucketIndexMap);
        Assert.Equal(PreparedLiteralSetStrategy.Empty, new PreparedLiteralSetSearch(data).Strategy);
        Assert.Equal(PreparedLiteralSetStrategy.Empty, new PreparedLiteralSetSearch(Array.Empty<byte[]>()).Strategy);
        Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    private static long MeasurePrefixOnly(AsciiExactLiteralSearchData data)
    {
        using var owner = new Utf8ConstructionScope(0, 0, ulong.MaxValue, 1);
        var before = GC.GetAllocatedBytesForCurrentThread(); var search = new PreparedLiteralSetSearch(data);
        var bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(PreparedLiteralSetStrategy.SingleBucketPrefix, search.Strategy); Assert.Equal(0ul, owner.Budget.ReservedBytes);
        return bytes;
    }
    [Fact]
    public void SharedPrefixSelectionDoesNotBuildUnusedAnchorsOrTriesAsFamilyGrows()
    {
        var small = AsciiSearch.CreateExactLiteralSearchData(Enumerable.Range(0, 8).Select(i => Encoding.UTF8.GetBytes("prefix" + i)).ToArray());
        var large = AsciiSearch.CreateExactLiteralSearchData(Enumerable.Range(0, 1024).Select(i => Encoding.UTF8.GetBytes("prefix" + i)).ToArray());
        _ = MeasurePrefixOnly(small); _ = MeasurePrefixOnly(large);
        Assert.Equal(MeasurePrefixOnly(small), MeasurePrefixOnly(large));
    }

    [Fact]
    public void SeededDiscoveryAgreesWithIndependentOrderedByteMatching()
    {
        var random = new Random(234982); byte[] alphabet = [0, 97, 98, 120, 128, 255];
        for (var sample = 0; sample < 300; sample++)
        {
            var values = Enumerable.Range(0, random.Next(1, 21)).Select(_ => Enumerable.Range(0, random.Next(1, 9))
                .Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()).ToArray();
            var subject = values.SelectMany(v => alphabet.Concat(v)).Concat(alphabet).ToArray();
            var data = AsciiSearch.CreateExactLiteralSearchData(values);
            foreach (var search in new[] { new PreparedLiteralSetSearch(values), new PreparedLiteralSetSearch(data) })
            {
                var first = -1; var last = -1; var firstLength = 0;
                for (var start = 0; start < subject.Length; start++)
                {
                    var literal = values.FirstOrDefault(v => subject.AsSpan(start).StartsWith(v));
                    var found = search.TryGetMatchedLiteralLength(subject, start, out var length);
                    Assert.Equal(literal is not null, found); Assert.Equal(literal?.Length ?? 0, length);
                    if (literal is null) continue;
                    if (first < 0) { first = start; firstLength = literal.Length; }
                    last = start;
                }
                Assert.Equal(first, search.IndexOf(subject)); Assert.Equal(last, search.LastIndexOf(subject));
                Assert.Equal(first >= 0, search.TryFindFirstMatchWithLength(subject, out var index, out var matched));
                Assert.Equal(first, index); Assert.Equal(firstLength, matched);
            }
        }
    }

    [Theory]
    [InlineData("alpha", 0)] [InlineData("xxalpha", 2)] [InlineData("alpha|alpXX", 0)] [InlineData("alpXX", -1)]
    public void ReversePrefixWindowIncludesCompleteFinalCandidateAndExcludesRejectedStarts(string subject, int expected)
    {
        var search = new PreparedLiteralSetSearch(Bytes("alpha", "alpine"));
        Assert.Equal(expected, search.LastIndexOf(Encoding.UTF8.GetBytes(subject)));
    }
    [Fact]
    public void ReverseSupplementaryPrefixWindowFindsTheCompleteLastScalarLiteral()
    {
        var search = new PreparedLiteralSetSearch(Bytes("😀tail", "😁tail", "😀toe"));
        Assert.Equal(20, search.LastIndexOf("xx😀tail|😁tail|😀toe"u8));
        Assert.Equal(0, search.LastIndexOf("😀tail"u8));
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.RightToLeft)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft)]
    public void PublicSupplementaryPrefixFamilyKeepsCompleteScalarRanges(RegexOptions options)
    {
        var regex = new Utf8Regex("(?:😀tail|😁tail|😀toe)", options); var subject = "xx😀tail|😁tail|😀toe"u8;
        Assert.Equal(3, regex.Count(subject)); var ranges = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(subject)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
        var expected = new[] { (2, 8), (11, 8), (20, 7) }; if ((options & RegexOptions.RightToLeft) != 0) Array.Reverse(expected);
        Assert.Equal(expected, ranges); Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
    private sealed class Box(int value) { internal readonly int Value = value; }
}
