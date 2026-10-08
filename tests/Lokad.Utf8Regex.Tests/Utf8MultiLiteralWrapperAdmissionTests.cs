using System.Reflection;
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
        from bytes in new[] { false, true } select new object[] { row.Item1, row.Item2, bytes };
    public static IEnumerable<object[]> Depths() => from mode in Enumerable.Range(0, 4)
        from depth in Enumerable.Range(0, 6) select new object[] { mode, depth };

    private static PreparedMultiLiteralSearch Prepare(byte[][] literals, int mode, PreparedLiteralSetSearch exact,
        PreparedAsciiIgnoreCaseLiteralSetSearch folded) => mode switch
        { 0 => new(literals, false), 1 => new(exact), 2 => new(literals, true), _ => new(folded) };

    // Independent selector model, including canonical empty alternatives, prefix
    // precedence, and the small family's first-byte/unique-anchor conditions.
    private static PreparedMultiLiteralKind ExpectedKind(byte[][] values, bool ignoreCase)
    {
        if (ignoreCase) return PreparedMultiLiteralKind.AsciiIgnoreCase;
        if (values.Length == 0 || values.Any(v => v.Length == 0)) return PreparedMultiLiteralKind.ExactDirect;
        var shortest = values.Min(v => v.Length);
        if (values.Length is >= 3 and < 8 && shortest >= 4 && values.Any(v => v.Any(b => b >= 128)) &&
            Enumerable.Range(0, shortest).Any(offset => values.Select(v => v[offset]).Distinct().Count() == values.Length))
            return PreparedMultiLiteralKind.ExactPacked;
        if (values.Length <= 3 && shortest >= 4 && values.Sum(v => (long)v.Length) >= 24)
            return PreparedMultiLiteralKind.ExactEarliest;
        var groups = values.GroupBy(v => v[0]).Select(g => g.ToArray()).ToArray();
        var prefix = groups.Length == 1 && shortest >= 2 && values.Select(v => v[1]).Distinct().Count() == 1;
        if (values.Length >= 5 && !prefix) return PreparedMultiLiteralKind.ExactAutomaton;
        if (values.Length >= 8) return PreparedMultiLiteralKind.ExactTrie;
        if (values.Length is >= 3 and < 5 && shortest >= 4 && groups.Length > 1 && groups.Any(g => g.Length > 1) &&
            Enumerable.Range(1, shortest - 1).Any(offset => values.Select(v => v[offset]).Distinct().Count() == values.Length))
            return PreparedMultiLiteralKind.ExactTrie;
        return PreparedMultiLiteralKind.ExactDirect;
    }

    private static int Stride<T>() => Unsafe.SizeOf<T>();
    private static ulong ArrayLoan(Array array)
    {
        var stride = (int)typeof(Utf8MultiLiteralWrapperAdmissionTests).GetMethod(nameof(Stride), BindingFlags.Static | BindingFlags.NonPublic)!
            .MakeGenericMethod(array.GetType().GetElementType()!).Invoke(null, null)!;
        return array.Length == 0 ? 0 : 32 + (((ulong)stride * (ulong)array.Length + 15) & ~15ul);
    }
    private static ulong Walk(object? value, HashSet<object> excluded, HashSet<object> seen)
    {
        if (value is null) return 0;
        var type = value.GetType();
        if (!type.IsValueType && (excluded.Contains(value) || !seen.Add(value))) return 0;
        if (value is Array array)
        {
            var loan = ArrayLoan(array);
            if (array.GetType().GetElementType() is { } itemType && (itemType.IsPrimitive || itemType.IsEnum)) return loan;
            foreach (var item in array) loan += Walk(item, excluded, seen);
            return loan;
        }
        if (type.Assembly != typeof(Utf8Regex).Assembly) return 0;
        return type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Aggregate(0ul, (sum, field) => sum + Walk(field.GetValue(value), excluded, seen));
    }
    private static HashSet<object> Borrowed(byte[][] literals, object? search = null)
    {
        var set = new HashSet<object>(ReferenceEqualityComparer.Instance);
        _ = Walk(literals, new(ReferenceEqualityComparer.Instance), set);
        _ = Walk(search, new(ReferenceEqualityComparer.Instance), set);
        return set;
    }
    private static ulong ObjectLoans(Utf8ConstructionBudget.ObjectAllocation? latest, Utf8ConstructionBudget.ObjectAllocation? checkpoint)
    {
        ulong bytes = 0;
        for (var node = latest; !ReferenceEquals(node, checkpoint); node = node!.Previous)
        { Assert.NotNull(node); bytes += node.Bytes; }
        return bytes;
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void SelectionAndReachablePayloadLoansAgreeWithoutAcquiringDiscardedExactState(int family, int mode)
    {
        var literals = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
        var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
        var loan = owner.Budget.ReservedBytes; var checkpoint = owner.Budget.ObjectCheckpoint;
        var borrowed = Borrowed(literals, mode == 1 ? exact : mode == 3 ? folded : null);
        var search = Prepare(literals, mode, exact, folded);
        Assert.Equal(ExpectedKind(literals, mode >= 2), search.Kind);
        var expected = mode == 1 ? exact.SearchData.Buckets.SelectMany(b => b.Literals).ToArray()
            : mode == 3 ? folded.Buckets.SelectMany(b => b.Literals).ToArray() : literals;
        Assert.Equal(expected.Length, search.Literals.Length);
        for (var i = 0; i < expected.Length; i++) Assert.Same(expected[i], search.Literals[i]);
        if (mode is 0 or 2) Assert.Same(literals, search.Literals);
        Assert.Equal(loan + Walk(search, borrowed, new(ReferenceEqualityComparer.Instance))
            + ObjectLoans(owner.Budget.ObjectCheckpoint, checkpoint), owner.Budget.ReservedBytes);
        if (search.Kind != PreparedMultiLiteralKind.ExactDirect) Assert.Null(search.ExactSearch.SearchData.Buckets);
        if (search.Kind is not (PreparedMultiLiteralKind.ExactTrie or PreparedMultiLiteralKind.ExactAutomaton)) Assert.Null(search.TrieSearch.RootMap);
        if (search.Kind is not (PreparedMultiLiteralKind.ExactEarliest or PreparedMultiLiteralKind.ExactAutomaton)) Assert.Null(search.EarliestSearch.Literals);
        if (search.Kind == PreparedMultiLiteralKind.ExactEarliest) Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        if (mode == 1 && search.Kind == PreparedMultiLiteralKind.ExactDirect) Assert.Same(exact.SearchData.Buckets, search.ExactSearch.SearchData.Buckets);
        if (mode == 3) Assert.Same(folded.Buckets, search.IgnoreCaseSearch.Buckets);
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryByteAndWorkCutoffPreservesFundedParentsAndNeverRefundsWork(int family, int mode, bool bytes)
    {
        var literals = Family(family); ulong parentLoan, parentWork, peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
            var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
            _ = funded.Budget.AllocateObject(17, static value => new Box(value));
            parentLoan = funded.Budget.ReservedBytes; parentWork = funded.Budget.WorkSteps;
            _ = Prepare(literals, mode, exact, folded); peak = funded.Budget.PeakBytes - parentLoan; work = funded.Budget.WorkSteps - parentWork;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (bytes ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? parentLoan + allowance : ulong.MaxValue,
                bytes ? ulong.MaxValue : parentWork + allowance, ulong.MaxValue);
            var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
            var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => _ = Prepare(literals, mode, exact, folded));
            if (error is null) successes++;
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
    public void WrapperAndSelectedChildrenDenyDepthBeforeAcquisition(int mode, int depth)
    {
        var literals = Family(8);
        var exact = mode == 1 ? new PreparedLiteralSetSearch(literals) : default;
        var folded = mode == 3 ? new PreparedAsciiIgnoreCaseLiteralSetSearch(literals) : default;
        ulong required;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { _ = Prepare(literals, mode, exact, folded); required = funded.Budget.PeakDepth; }
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
        var error = Record.Exception(() => _ = Prepare(literals, mode, exact, folded));
        if ((ulong)depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        else Assert.Null(error);
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        if (depth == 0) Assert.Equal(112ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyWrappersAndEarliestSearchBorrowAllStateWithoutAnAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        Assert.Equal(PreparedMultiLiteralKind.ExactDirect, new PreparedMultiLiteralSearch([], false).Kind);
        Assert.Equal(PreparedMultiLiteralKind.ExactDirect, new PreparedMultiLiteralSearch(new PreparedLiteralSetSearch(Array.Empty<byte[]>())).Kind);
        Assert.Equal(PreparedMultiLiteralKind.AsciiIgnoreCase, new PreparedMultiLiteralSearch([], true).Kind);
        Assert.Equal(PreparedMultiLiteralKind.AsciiIgnoreCase, new PreparedMultiLiteralSearch(new PreparedAsciiIgnoreCaseLiteralSetSearch([])).Kind);
        var earliest = new PreparedMultiLiteralEarliestExactSearch([]);
        Assert.Equal(int.MaxValue, earliest.ShortestLength); Assert.Equal(0, earliest.LongestLength);
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void EarliestPreparationBorrowsLargeLiteralsAndMetersEachLengthVisit()
    {
        var literals = Bytes(new string('a', 100_000), new string('b', 100_001), new string('c', 100_002));
        for (var allowance = 0ul; allowance <= 3; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, 0, allowance, 1);
            var error = Record.Exception(() => _ = new PreparedMultiLiteralEarliestExactSearch(literals));
            if (allowance < 3) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            else Assert.Null(error);
            Assert.Equal(allowance, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
        }
        // Three literals are packed-eligible, so selection must first prove
        // that their bytes are ASCII. Length preparation itself remains O(k).
        var selectionWork = (ulong)literals.Sum(v => (long)v.Length) + 12;
        using var wrapperOwner = new Utf8ConstructionScope(0, 0, selectionWork, 2);
        var search = new PreparedMultiLiteralSearch(literals, false);
        Assert.Equal(PreparedMultiLiteralKind.ExactEarliest, search.Kind); Assert.Same(literals, search.EarliestSearch.Literals);
        Assert.Equal(100_000, search.EarliestSearch.ShortestLength); Assert.Equal(100_002, search.EarliestSearch.LongestLength);
        Assert.Equal(0ul, wrapperOwner.Budget.PeakBytes); Assert.Equal(selectionWork, wrapperOwner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(2, false)] [InlineData(2, true)] [InlineData(3, false)] [InlineData(3, true)]
    [InlineData(5, false)] [InlineData(5, true)] [InlineData(7, false)] [InlineData(7, true)]
    [InlineData(8, false)] [InlineData(8, true)] [InlineData(9, false)] [InlineData(9, true)]
    public void PublicLiteralFamiliesPreserveScalarGapByteRangesWithoutBclExecution(int family, bool compiled)
    {
        var literals = Family(family);
        var regex = new Utf8Regex(string.Join("|", literals.Select(v => Regex.Escape(Encoding.UTF8.GetString(v)))),
            RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
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
        Assert.Equal(ranges, actual); Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
    private sealed class Box(int value) { internal readonly int Value = value; }
}
