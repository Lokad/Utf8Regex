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
        24 => Bytes("Tom", "Sawyer", "Huckleberry", "Finn"),
        25 => Bytes("abcdefghijklmnop", "qrstuvwxy", "ABCDEFGHIJK", "LMNOPQRSTUVWX"),
        26 => Bytes("abcdefghijklmnopq", "xyz"),
        _ => Enumerable.Range(0, 2 + id % 4).Select(i => Enumerable.Range(0, 3 + id % 7)
            .Select(j => (byte)(33 + (i * 7 + j + id) % 90)).ToArray()).ToArray()
    };
    public static IEnumerable<object[]> Families() => Enumerable.Range(0, 27).Select(family => new object[] { family });
    public static IEnumerable<object[]> Cutoffs() => from family in new[] { 1, 2 } select new object[] { family };
    private static object? Field(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value);

    // Eligibility and unique pairs are semantic; a frequency heuristic is not.
    private static (int Primary, int Secondary)[] Pairs(byte[][] values)
    {
        if (values.Length is < 2 or > 4 || values.Any(v => v.Length is < 3 or > 8 || v.Any(b => b >= 128))) return [];
        var length = values.Min(v => v.Length);
        return (from first in Enumerable.Range(0, length) from second in Enumerable.Range(0, length)
            where first != second && values.Select(v => v[first]).Distinct().Count() > 1 && values.Select(v => v[second]).Distinct().Count() > 1
                && values.Select(v => (v[first], v[second])).Distinct().Count() == values.Length
            select (first, second)).ToArray();
    }
    [Theory]
    [MemberData(nameof(Families))]
    public void UniquePairsUseOnlyFundedFinalPayloads(int family)
    {
        var values = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        var pairs = Pairs(values); var accepted = PreparedShortAsciiLiteralFamilyCounter.TryCreate(values, out var counter);
        Assert.Equal(pairs.Length > 0, accepted);
        var primary = accepted ? Assert.IsType<int>(Field(counter, "_primaryOffset")) : -1;
        var count = accepted ? values.Select(v => v[primary]).Distinct().Count() : 0;
        Assert.Equal(128 + (accepted ? Utf8ConstructionBudget.ArrayBytes<byte>(count) + (count > 5 ? Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>()) : 0) : 0), owner.Budget.ReservedBytes);
        if (accepted)
        {
            Assert.Contains((primary, Assert.IsType<int>(Field(counter, "_secondaryOffset"))), pairs);
            var search = Assert.IsType<PreparedByteSearch>(Field(counter, "_primarySearch"));
            Assert.Equal(values.Select(v => v[primary]).Distinct().ToArray(), search.Values.ToArray());
            for (var i = 0; i < values.Length; i++) Assert.Same(values[i], Field(counter, "_literal" + i));
        }
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void ByteCutoffsPreserveFundedParents(int family)
    {
        var values = Family(family); ulong peak, loan;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue))
        {
            _ = funded.Budget.AllocateObject(17, static value => new Box(value)); Assert.True(PreparedShortAsciiLiteralFamilyCounter.TryCreate(values, out _));
            peak = funded.Budget.PeakBytes - 128; loan = funded.Budget.ReservedBytes;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, 128 + allowance, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => Assert.True(PreparedShortAsciiLiteralFamilyCounter.TryCreate(values, out _)));
            if (error is null) { successes++; Assert.Equal(loan, owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RootsAndSelectedByteSearchDenyDepthBeforeAcquisition(int depth)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
        // Four distinct primary values borrow the checked BCL search; only the
        // counter and independent byte-array preparation frames remain.
        var error = Record.Exception(() => Assert.True(PreparedShortAsciiLiteralFamilyCounter.TryCreate(Family(14), out _)));
        if (depth < 2)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        else Assert.Null(error);
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        if (depth == 0) Assert.Equal(128ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyAndOutOfCountFamiliesBorrowAllStateWithoutAnAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0);
        Assert.False(PreparedShortAsciiLiteralFamilyCounter.TryCreate([], out _));
        Assert.False(PreparedShortAsciiLiteralFamilyCounter.TryCreate(Bytes("abc"), out _));
Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void SeededPairSelectionAndCountingAgreeWithIndependentOrderedMatching()
    {
        var random = new Random(923848);
        for (var sample = 0; sample < 250; sample++)
        {
            var values = Enumerable.Range(0, random.Next(2, 5)).Select(_ => Enumerable.Range(0, random.Next(3, 18))
                .Select(_ => (byte)(random.Next(97, 102) ^ (random.Next(2) == 0 ? 0 : 32))).ToArray()).ToArray();
            var pairs = Pairs(values); Assert.Equal(pairs.Length > 0, PreparedShortAsciiLiteralFamilyCounter.TryCreate(values, out var counter));
            if (pairs.Length == 0) continue;
            Assert.Contains((Assert.IsType<int>(Field(counter, "_primaryOffset")), Assert.IsType<int>(Field(counter, "_secondaryOffset"))), pairs);
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
        var values = Family(family); var regex = new Utf8Regex(string.Join("|", values.Select(v => Regex.Escape(Encoding.UTF8.GetString(v)))), options:             RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
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
