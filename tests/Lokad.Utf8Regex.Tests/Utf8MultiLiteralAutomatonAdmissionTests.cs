using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MultiLiteralAutomatonAdmissionTests
{
    public static IEnumerable<object[]> Families() => Enumerable.Range(0, 10).Select(value => new object[] { value });
    private static byte[][] Literals(int family) => family switch
    {
        0 => [], 1 => [[]], 2 => ["a"u8.ToArray()], 3 => ["ab"u8.ToArray(), "ac"u8.ToArray(), "bb"u8.ToArray()],
        4 => new[] { "she", "he", "his", "hers" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        5 => new[] { "é", "é😀", "😀", "中", "😀😀" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        6 => Enumerable.Range(0, 256).Select(value => new[] { (byte)value }).ToArray(),
        7 => ["aa"u8.ToArray(), "a"u8.ToArray(), "aa"u8.ToArray()],
        8 => [new byte[] { 0, 128, 255 }, new byte[] { 0, 128 }, new byte[] { 255, 0 }, new byte[] { 128 }],
        _ => [Encoding.UTF8.GetBytes(new string('a', 257)), "ab"u8.ToArray(), "ac"u8.ToArray()],
    };

    private static List<byte[]> Prefixes(byte[][] literals)
    {
        var keys = new HashSet<string> { "" }; List<byte[]> prefixes = [[]];
        foreach (var literal in literals)
        for (var length = 1; length <= literal.Length; length++)
            if (keys.Add(Convert.ToHexString(literal.AsSpan(0, length)))) prefixes.Add(literal[..length]);
        return prefixes;
    }

    private static ulong RetainedBytes(int states) =>
        2 * (Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<(Utf8ConstructionBudget, bool, object[], int)>()))
        + Utf8ConstructionBudget.ArrayBytes<int>(states * 256) + 3 * Utf8ConstructionBudget.ArrayBytes<int>(states);

    [Theory]
    [MemberData(nameof(Families))]
    public void StateCountsAndFinalLoansMatchIndependentPrefixModel(int family)
    {
        var literals = Literals(family); var count = literals.Length == 0 ? 0 : Prefixes(literals).Count;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var search = PreparedMultiLiteralAutomatonSearch.Create(literals);
        Assert.Equal(48 + (count == 0 ? 0 : RetainedBytes(count)), owner.Budget.ReservedBytes);
        Assert.Equal(count, search.StateCount); Assert.Equal(count * 256, search.Transitions.Length);
        Assert.Equal(count, search.BestOutputOrders.Length); Assert.Equal(count, search.ShortestOutputLengths.Length);
        Assert.Equal(count == 0 ? 0ul : 1ul, owner.Budget.PeakDepth);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0xa5, parent[0]);
        Assert.Equal(literals.Length == 0 ? int.MaxValue : literals.Min(value => value.Length), search.ShortestLength);
        Assert.Equal(literals.Length == 0 ? 0 : literals.Max(value => value.Length), search.LongestLength);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryByteAndWorkCutoffRestoresParentObjectsAndPreservesSpentWork(bool byteLimit)
    {
        var literals = Literals(2); ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = PreparedMultiLiteralAutomatonSearch.Create(literals); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (byteLimit ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, byteLimit ? 128 + allowance : ulong.MaxValue,
                byteLimit ? ulong.MaxValue : allowance + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => {
                var search = PreparedMultiLiteralAutomatonSearch.Create(literals);
                Assert.Equal(2, search.IndexOf("xxaa"u8)); Assert.Equal(3, search.LastIndexOf("xxaa"u8));
            });
            if (error is null)
            {
                successes++; Assert.Equal(128 + RetainedBytes(2), owner.Budget.ReservedBytes);
                Assert.True(owner.Budget.ReservedBytes > checkpoint);
                if (!byteLimit) Assert.Equal(work, allowance);
            }
            else
            {
                var failure = Assert.IsType<Utf8ResourceLimitExceededException>(error);
                Assert.Equal(byteLimit ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, byteLimit ? ulong.MaxValue : 2 + allowance);
            if (byteLimit) Assert.InRange(owner.Budget.PeakBytes, 128ul, 128 + allowance);
        }
        Assert.True(successes > 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalMatrixCopyDenialReleasesScratchAndPreservesTheParent(bool byteLimit)
    {
        byte[][] literals = ["ab"u8.ToArray()]; ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = PreparedMultiLiteralAutomatonSearch.Create(literals); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps;
            Assert.True(peak > funded.Budget.ReservedBytes + Utf8ConstructionBudget.ArrayBytes<int>(768));
        }
        using var owner = new Utf8ConstructionScope(0, byteLimit ? peak + 111 : ulong.MaxValue,
            byteLimit ? ulong.MaxValue : work + 1, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => PreparedMultiLiteralAutomatonSearch.Create(literals));
        Assert.Equal(byteLimit ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value); Assert.True(owner.Budget.WorkSteps > 2);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void RootDepthIsAdmittedBeforeScratchAcquisitionAndDoesNotGrowWithPrefixLength(int depth)
    {
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var error = Record.Exception(() => PreparedMultiLiteralAutomatonSearch.Create(Literals(9)));
        if (depth == 0)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(48ul, owner.Budget.PeakBytes); Assert.Equal(48ul, owner.Budget.ReservedBytes);
        }
        else { Assert.Null(error); Assert.Equal(1ul, owner.Budget.PeakDepth); }
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0xa5, parent[0]);
    }

    [Fact]
    public void EmptyAutomatonBorrowsAllStateWithoutAnyAllowance()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var search = PreparedMultiLiteralAutomatonSearch.Create([]);
        Assert.Empty(search.Transitions); Assert.Empty(search.BestOutputOrders); Assert.Empty(search.BestOutputLengths); Assert.Empty(search.ShortestOutputLengths);
        Assert.Equal(0, search.StateCount); Assert.Equal(0, search.RootFanout); Assert.Equal(0, search.OutputStateCount);
        Assert.Equal(-1, search.IndexOf("😀"u8)); Assert.Equal(-1, search.LastIndexOf("😀"u8));
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)]
    public void EveryTransitionAndFailureOutputAgreesWithIndependentSuffixModel(int family)
    {
        var literals = Literals(family); var prefixes = Prefixes(literals); var search = PreparedMultiLiteralAutomatonSearch.Create(literals);
        var ids = prefixes.Select((value, id) => (Key: Convert.ToHexString(value), Id: id)).ToDictionary(value => value.Key, value => value.Id);
        for (var state = 0; state < prefixes.Count; state++)
        {
            var prefix = prefixes[state]; var candidate = new byte[prefix.Length + 1]; prefix.CopyTo(candidate, 0);
            for (var key = 0; key < 256; key++)
            {
                candidate[^1] = (byte)key; var expected = 0;
                for (var start = 0; start <= candidate.Length; start++)
                    if (ids.TryGetValue(Convert.ToHexString(candidate.AsSpan(start)), out expected)) break;
                Assert.Equal(expected, search.Transitions[state * 256 + key]);
            }
            var outputs = literals.Select((value, id) => (Value: value, Id: id)).Where(value => prefix.AsSpan().EndsWith(value.Value)).ToArray();
            var best = outputs.OrderByDescending(value => value.Value.Length).ThenBy(value => value.Id).FirstOrDefault((Value: Array.Empty<byte>(), Id: -1));
            Assert.Equal(best.Id, search.BestOutputOrders[state]); Assert.Equal(best.Value.Length, search.BestOutputLengths[state]);
            Assert.Equal(outputs.Length == 0 ? int.MaxValue : outputs.Min(value => value.Value.Length), search.ShortestOutputLengths[state]);
        }
    }

    private static (int Index, int Length, int Id) First(byte[][] literals, ReadOnlySpan<byte> subject)
    {
        for (var index = 0; index < subject.Length; index++)
        for (var id = 0; id < literals.Length; id++)
            if (literals[id].Length <= subject.Length - index && subject.Slice(index, literals[id].Length).SequenceEqual(literals[id]))
                return (index, literals[id].Length, id);
        return (-1, 0, -1);
    }

    [Fact]
    public void SeededOrderedMatchingAndProgressionAgreeWithTheNaiveByteOracle()
    {
        var random = new Random(73109); byte[] alphabet = [0, 97, 98, 128, 255];
        for (var sample = 0; sample < 300; sample++)
        {
            var literals = Enumerable.Range(0, random.Next(1, 18)).Select(_ =>
                Enumerable.Range(0, random.Next(1, 9)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray()).ToArray();
            var subject = Enumerable.Range(0, 48).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray();
            var search = PreparedMultiLiteralAutomatonSearch.Create(literals);
            var state = default(PreparedMultiLiteralScanState); var from = 0;
            while (from <= subject.Length)
            {
                var expected = First(literals, subject.AsSpan(from));
                var found = search.TryFindNextNonOverlappingMatch(subject, ref state, out var index, out var length, out var id);
                Assert.Equal(expected.Index >= 0, found);
                if (!found) break;
                Assert.Equal((from + expected.Index, expected.Length, expected.Id), (index, length, id));
                from = index + length; Assert.Equal(from, state.NextStart);
            }
            var first = First(literals, subject); var success = search.TryFindFirstMatch(subject, out var firstIndex, out var firstLength, out var firstId);
            Assert.Equal(first.Index >= 0, success); Assert.Equal(first, (firstIndex, firstLength, firstId));
            var last = -1; for (var index = 0; index < subject.Length; index++) if (First(literals, subject.AsSpan(index)).Index == 0) last = index;
            Assert.Equal(last, search.LastIndexOf(subject));
        }
    }

    [Fact]
    public void FrozenAutomatonCopiesCallerValuesAndMatchingAllocatesNothing()
    {
        var literals = Literals(4); var search = PreparedMultiLiteralAutomatonSearch.Create(literals);
        foreach (var literal in literals) Array.Fill(literal, (byte)'z'); Array.Fill(literals, Array.Empty<byte>());
        for (var warm = 0; warm < 1000; warm++) _ = search.IndexOf("ushers"u8);
        var before = GC.GetAllocatedBytesForCurrentThread(); var sum = 0;
        for (var iteration = 0; iteration < 1000; iteration++)
        {
            sum += search.IndexOf("ushers"u8) + search.LastIndexOf("ushers"u8);
            _ = search.TryFindFirstMatch("ushers"u8, out _, out var length, out var id); sum += length + id;
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(6000, sum); Assert.Equal(0, allocated);
    }

    [Fact]
    public void SupersededMutableNodeAndTransitionFlagsAreAbsent()
        => Assert.DoesNotContain(typeof(Utf8Regex).Assembly.GetTypes(), type => type.Name == "MutablePreparedMultiLiteralAutomatonNode");

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void PublicSupplementaryLiteralFamilyKeepsScalarRangesWithoutFallback(RegexOptions options)
    {
        var regex = new Utf8Regex("(?:é😀|é中|😀é|😀中|中é|中😀)", options); var subject = "xxé😀|é中|😀é|😀中|中é|中😀xx"u8;
        Assert.True(regex.IsMatch(subject)); Assert.Equal(6, regex.Count(subject));
        var ranges = new List<(int, int)>(); foreach (var match in regex.EnumerateMatches(subject)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(new[] { (2, 6), (9, 5), (15, 6), (22, 7), (30, 5), (36, 7) }, ranges);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    private sealed class Box(int value) { public readonly int Value = value; }
}
