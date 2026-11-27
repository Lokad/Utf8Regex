using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MultiLiteralTrieAdmissionTests
{
    public static IEnumerable<object[]> NonemptyFamilies() => Enumerable.Range(1, 8).Select(value => new object[] { value });

    private static byte[][] Literals(int family) => family switch
    {
        0 => [], 1 => [[]], 2 => ["ab"u8.ToArray(), "ac"u8.ToArray(), "ad"u8.ToArray(), "bb"u8.ToArray()],
        3 => ["aa"u8.ToArray(), "a"u8.ToArray(), "aa"u8.ToArray()],
        4 => new[] { "é", "é😀", "😀", "中", "😀😀" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        5 => Enumerable.Range(0, 17).Select(value => Encoding.UTF8.GetBytes("shared-prefix-" + value)).ToArray(),
        6 => Enumerable.Range(0, 256).Select(value => new[] { (byte)value }).ToArray(),
        7 => [new byte[] { 0, 128, 255 }, new byte[] { 0, 128 }, new byte[] { 255, 0 }, new byte[] { 128 }],
        _ => [Encoding.UTF8.GetBytes(new string('a', 257)), "ab"u8.ToArray(), "ac"u8.ToArray()],
    };

    [Theory]
    [MemberData(nameof(NonemptyFamilies))]
    public void PublishedTrieKeepsMajorStorageAdmitted(int family)
    {
        var literals = Literals(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 0xa5;
        var held = owner.Budget.ReservedBytes;
        var search = PreparedMultiLiteralTrieSearch.Create(literals);

        Assert.NotEmpty(search.RootMap);
        var rootMapBytes = (ulong)search.RootMap.Length * sizeof(int);
        Assert.InRange(owner.Budget.ReservedBytes, held + rootMapBytes, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0xa5, parent[0]);
        var rootCount = literals.Where(value => value.Length > 0).Select(value => value[0]).Distinct().Count();
        Assert.Equal(literals.Min(value => value.Length), search.ShortestLength);
        Assert.Equal(rootCount, search.RootSearch.Count);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void RootAndNestedSearchDepthDenialsReleaseAllChildLoans(int depth)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 0xa5;
        var held = owner.Budget.ReservedBytes;
        var error = Record.Exception(() => PreparedMultiLiteralTrieSearch.Create(Literals(6)));
        if (depth < 3)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(held, owner.Budget.ReservedBytes);
        }
        else Assert.Null(error);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0xa5, parent[0]);
        Assert.True(owner.Budget.PeakDepth <= (ulong)depth);
    }

    [Fact]
    public void EmptyTrieBorrowsAllStateWithoutAnOwnerOrAnyAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0);
        var search = PreparedMultiLiteralTrieSearch.Create([]);
        Assert.Empty(search.RootMap); Assert.Equal(0, search.RootSearch.Count);
        Assert.Equal(-1, search.IndexOf("😀"u8)); Assert.Equal(-1, search.LastIndexOf("😀"u8));
        Assert.False(search.TryGetMatchedLiteralInfo("😀"u8, 0, out _, out _));
Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);

    }

    private static (bool Found, int Length, int Id) At(byte[][] literals, ReadOnlySpan<byte> subject, int index)
    {
        for (var order = 0; order < literals.Length; order++)
        {
            var literal = literals[order];
            if ((uint)index < (uint)subject.Length && literal.Length <= subject.Length - index
                && subject.Slice(index, literal.Length).SequenceEqual(literal)) return (true, literal.Length, order);
        }
        return (false, 0, -1);
    }

    private static void AssertSearch(byte[][] literals, byte[] subject)
    {
        var search = PreparedMultiLiteralTrieSearch.Create(literals);
        var first = -1; var last = -1;
        for (var index = -1; index <= subject.Length + 1; index++)
        {
            var expected = At(literals, subject, index);
            var found = search.TryGetMatchedLiteralInfo(subject, index, out var length, out var id);
            Assert.Equal(expected, (found, length, id));
            if (found) { if (first < 0) first = index; last = index; }
        }
        Assert.Equal(first, search.IndexOf(subject)); Assert.Equal(last, search.LastIndexOf(subject));
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void OrderedMatchingAgreesWithIndependentByteOracleAcrossWidthsAndPrefixes(int family)
    {
        var literals = Literals(family);
        byte[][] subjects = [[], "ababa é😀😀中 shared-prefix-16"u8.ToArray(), Enumerable.Range(0, 256).Select(value => (byte)value).ToArray(),
            literals.SelectMany(value => value.Concat(new byte[] { 17 })).ToArray()];
        foreach (var subject in subjects) AssertSearch(literals, subject);
    }

    [Fact]
    public void SeededFamiliesPreservePrefixAndDuplicateLiteralPriority()
    {
        var random = new Random(73019); byte[] alphabet = [0, 97, 98, 128, 255];
        for (var sample = 0; sample < 300; sample++)
        {
            var literals = Enumerable.Range(0, random.Next(1, 18)).Select(_ =>
                Enumerable.Range(0, random.Next(1, 9)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()).ToArray();
            var subject = Enumerable.Range(0, 48).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();
            AssertSearch(literals, subject);
        }
    }

    [Fact]
    public void FrozenTrieDoesNotRetainBorrowedLiteralArraysAndSearchAllocatesNothing()
    {
        var literals = Literals(2);
        var search = PreparedMultiLiteralTrieSearch.Create(literals);
        foreach (var literal in literals) Array.Fill(literal, (byte)'z');
        Array.Fill(literals, Array.Empty<byte>());
        // Warm every route below before measuring steady-state allocation.
        for (var warm = 0; warm < 1000; warm++)
        {
            _ = search.IndexOf("xxacbb"u8);
            _ = search.LastIndexOf("xxacbb"u8);
            _ = search.TryGetMatchedLiteralInfo("xxacbb"u8, 2, out _, out _);
        }
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0;
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            sum += search.IndexOf("xxacbb"u8) + search.LastIndexOf("xxacbb"u8);
            _ = search.TryGetMatchedLiteralInfo("xxacbb"u8, 2, out var length, out var id); sum += length + id;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(9000, sum); Assert.Equal(0, allocated);
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void PublicSupplementaryLiteralFamilyReportsScalarByteRanges(RegexOptions options)
    {
        var regex = new Utf8Regex("(?:é😀|é中|😀é|😀中|中é|中😀)", options: options);
        var subject = "xxé😀|é中|😀é|😀中|中é|中😀xx"u8;
        Assert.True(regex.IsMatch(subject)); Assert.Equal(6, regex.Count(subject));
        var ranges = new List<(int, int)>(); foreach (var match in regex.EnumerateMatches(subject)) ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(new[] { (2, 6), (9, 5), (15, 6), (22, 7), (30, 5), (36, 7) }, ranges);
    }
}
