using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Search;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MultiLiteralWrapperAdmissionTests
{
    private static byte[][] Bytes(params string[] values) => values.Select(Encoding.UTF8.GetBytes).ToArray();
    private static byte[][] Family(int id) => id switch
    {
        0 => [], 1 => Bytes("ab", "bc"), 2 => Bytes("abcdefgh", "ijklmnoq", "qrstuvwx"),
        3 => Bytes("a1xx", "a2xx", "b3xx", "b4xx"), 4 => Bytes("a1xx", "a1xy", "b1xx", "b1xy"),
        5 => Enumerable.Range(0, 5).Select(i => Encoding.UTF8.GetBytes("a" + i + "xx")).ToArray(),
        6 => Enumerable.Range(0, 5).Select(i => Encoding.UTF8.GetBytes("pre" + i)).ToArray(),
        7 => Enumerable.Range(0, 8).Select(i => Encoding.UTF8.GetBytes("pre" + i)).ToArray(),
        8 => Bytes("😀tail", "😁tail", "😂tail"),
        9 => Bytes("😀tail", "😀toe", "😁tail", "😁toe", "😂tail", "😂toe"),
        10 => Enumerable.Range(0, 8).Select(i => Encoding.UTF8.GetBytes("a0xx" + i % 4)).ToArray(),
        11 => [[], "a"u8.ToArray(), "ab"u8.ToArray(), "x"u8.ToArray(), "y"u8.ToArray(), "z"u8.ToArray()],
        12 => Bytes("a", "ab", "b", "c", "d"),
        _ => Enumerable.Range(0, 1 + id % 11).Select(i => Enumerable.Range(0, 1 + id % 8)
            .Select(j => (byte)(33 + (i * 7 + j + id) % 90)).ToArray()).ToArray()
    };
    public static IEnumerable<object[]> Modes() => from family in Enumerable.Range(0, 24)
        from mode in Enumerable.Range(0, 4) select new object[] { family, mode };
    public static IEnumerable<object[]> Cutoffs() => from row in new[] { (1, 0), (2, 0), (8, 0), (1, 1), (1, 3) }
        select new object[] { row.Item1, row.Item2 };
    public static IEnumerable<object[]> Depths() => from mode in Enumerable.Range(0, 4)
        from depth in Enumerable.Range(0, 6) select new object[] { mode, depth };

    private static PreparedMultiLiteralSearch Prepare(byte[][] literals, int mode, PreparedLiteralSetSearch exact,
        PreparedAsciiIgnoreCaseLiteralSetSearch folded) => mode switch
        { 0 => new(literals, false), 1 => new(exact), 2 => new(literals, true), _ => new(folded) };

    [Theory]
    [MemberData(nameof(Modes))]
    public void WrapperKeepsBorrowedLiteralsAndOnlyPublishesSelectedState(int family, int mode)
    {
        var literals = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
        var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
        var held = owner.Budget.ReservedBytes;
        var search = Prepare(literals, mode, exact, folded);
        Assert.Equal(mode >= 2, search.Kind == PreparedMultiLiteralKind.AsciiIgnoreCase);
        var expected = mode == 1 ? exact.SearchData.Buckets.SelectMany(b => b.Literals).ToArray()
            : mode == 3 ? folded.Buckets.SelectMany(b => b.Literals).ToArray() : literals;
        Assert.Equal(expected.Length, search.Literals.Length);
        for (var i = 0; i < expected.Length; i++) Assert.Same(expected[i], search.Literals[i]);
        if (mode is 0 or 2) Assert.Same(literals, search.Literals);
        var flattenedPayload = mode is 1 or 3 ? (ulong)search.Literals.Length * (ulong)Unsafe.SizeOf<byte[]>() : 0;
        Assert.InRange(owner.Budget.ReservedBytes, held + flattenedPayload, owner.Budget.PeakBytes);
        if (search.Kind != PreparedMultiLiteralKind.ExactDirect) Assert.Null(search.ExactSearch.SearchData.Buckets);
        if (search.Kind is not (PreparedMultiLiteralKind.ExactTrie or PreparedMultiLiteralKind.ExactAutomaton)) Assert.Null(search.TrieSearch.RootMap);
        if (search.Kind is not (PreparedMultiLiteralKind.ExactEarliest or PreparedMultiLiteralKind.ExactAutomaton)) Assert.Null(search.EarliestSearch.Literals);
        if (search.Kind == PreparedMultiLiteralKind.ExactEarliest)
            Assert.Equal(held + Utf8ConstructionBudget.ArrayBytes<byte[]>(mode == 1 ? search.Literals.Length : 0),
                owner.Budget.ReservedBytes);
        if (mode == 1 && search.Kind == PreparedMultiLiteralKind.ExactDirect) Assert.Same(exact.SearchData.Buckets, search.ExactSearch.SearchData.Buckets);
        if (mode == 3) Assert.Same(folded.Buckets, search.IgnoreCaseSearch.Buckets);
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void WrapperByteDenialsPreserveFundedParents(int family, int mode)
    {
        var literals = Family(family);
        ulong heldBytes, required;
        using (var measured = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue))
        {
            var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
            var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
            _ = measured.Budget.AllocateObject(17, static value => new Box(value));
            heldBytes = measured.Budget.ReservedBytes;
            _ = Prepare(literals, mode, exact, folded);
            required = measured.Budget.PeakBytes - heldBytes;
        }

        foreach (var allowance in new[] { 0ul, required == 0 ? 0ul : required - 1, required }.Distinct())
        {
            var byteCap = heldBytes + allowance;
            using var owner = new Utf8ConstructionScope(0, byteCap, ulong.MaxValue);
            var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
            var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));

            if (allowance == required)
            {
                var search = Prepare(literals, mode, exact, folded);
                Assert.NotEmpty(search.Literals);
            }
            else
            {
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                    Prepare(literals, mode, exact, folded));
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,
                    failure.ResourceKind);
                Assert.Equal(byteCap, failure.Limit);
                Assert.True(failure.Requested > failure.Limit);
                Assert.Equal(heldBytes, owner.Budget.ReservedBytes);
            }
            Assert.Equal(17, parent.Value);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.PeakBytes, heldBytes, byteCap);
        }
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void WrapperAndSelectedChildrenDenyDepthBeforeAcquisition(int mode, int depth)
    {
        var literals = Family(8);
        var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
        var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
        ulong required;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue))
        { _ = Prepare(literals, mode, exact, folded); required = funded.Budget.PeakDepth; }
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Record.Exception(() => _ = Prepare(literals, mode, exact, folded));
        if ((ulong)depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        else Assert.Null(error);
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        if (depth == 0) Assert.Equal(128ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyWrappersAndEarliestSearchBorrowAllStateWithoutAnAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0);
        Assert.Equal(PreparedMultiLiteralKind.ExactDirect, new PreparedMultiLiteralSearch([], false).Kind);
        Assert.Equal(PreparedMultiLiteralKind.ExactDirect, new PreparedMultiLiteralSearch(new PreparedLiteralSetSearch(Array.Empty<byte[]>())).Kind);
        Assert.Equal(PreparedMultiLiteralKind.AsciiIgnoreCase, new PreparedMultiLiteralSearch([], true).Kind);
        Assert.Equal(PreparedMultiLiteralKind.AsciiIgnoreCase, new PreparedMultiLiteralSearch(new PreparedAsciiIgnoreCaseLiteralSetSearch([])).Kind);
        var earliest = new PreparedMultiLiteralEarliestExactSearch([]);
        Assert.Equal(int.MaxValue, earliest.ShortestLength); Assert.Equal(0, earliest.LongestLength);
Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void EarliestPreparationBorrowsLargeLiteralsAndMetersEachLengthVisit()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var literals = Bytes(new string('a', 100_000), new string('b', 100_001), new string('c', 100_002));
        // Three literals are packed-eligible, so selection must first prove
        // that their bytes are ASCII. Length preparation itself remains O(k).
        var selectionWork = (ulong)literals.Sum(v => (long)v.Length) + 12;
        using var wrapperOwner = new Utf8ConstructionScope(0, 0, 2);
        var search = new PreparedMultiLiteralSearch(literals, false);
        Assert.Equal(PreparedMultiLiteralKind.ExactEarliest, search.Kind); Assert.Same(literals, search.EarliestSearch.Literals);
        Assert.Equal(100_000, search.EarliestSearch.ShortestLength); Assert.Equal(100_002, search.EarliestSearch.LongestLength);
        Assert.Equal(0ul, wrapperOwner.Budget.PeakBytes); Assert.Equal(selectionWork, wrapperOwner.Budget.DiagnosticCheckpoints);
    }

    [Theory]
    [InlineData(2, false)] [InlineData(2, true)] [InlineData(3, false)] [InlineData(3, true)]
    [InlineData(5, false)] [InlineData(5, true)] [InlineData(7, false)] [InlineData(7, true)]
    [InlineData(8, false)] [InlineData(8, true)] [InlineData(9, false)] [InlineData(9, true)]
    public void PublicLiteralFamiliesPreserveScalarGapByteRangesWithoutBclExecution(int family, bool compiled)
    {
        var literals = Family(family);
        var regex = new Utf8Regex(string.Join("|", literals.Select(v => Regex.Escape(Encoding.UTF8.GetString(v)))), options:             RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = literals.SelectMany(v => "中|"u8.ToArray().Concat(v)).Concat("😀!"u8.ToArray()).ToArray();
        var ranges = new List<(int, int)>(); var last = 0; var count = 0;
        for (var start = 0; start < input.Length; start++)
        {
            var literal = literals.FirstOrDefault(v => input.AsSpan(start).StartsWith(v));
            if (literal is null) continue;
            ranges.Add((last, start - last)); count++; last = start + literal.Length; start = last - 1;
        }
        ranges.Add((last, input.Length - last));
        Assert.True(regex.IsMatch(input)); Assert.Equal(count, regex.Count(input));
        var actual = new List<(int, int)>(); foreach (var gap in regex.EnumerateSplits(input)) actual.Add((gap.IndexInBytes, gap.LengthInBytes));
        Assert.Equal(ranges, actual); Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
    private sealed class Box(int value) { internal readonly int Value = value; }
}
