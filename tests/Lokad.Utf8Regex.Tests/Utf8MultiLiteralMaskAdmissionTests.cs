using System.Buffers;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MultiLiteralMaskAdmissionTests
{
    private static bool Simd => Ssse3.IsSupported && Sse2.IsSupported;
    public static IEnumerable<object[]> ModesAndFamilies() =>
        from mode in Enumerable.Range(0, 6) from family in Enumerable.Range(0, 8) select new object[] { mode, family };
    public static IEnumerable<object[]> Cutoffs() =>
        from mode in Enumerable.Range(0, 6) from bytes in new[] { false, true } select new object[] { mode, bytes };
    public static IEnumerable<object[]> Depths() =>
        from mode in Enumerable.Range(0, 6) from depth in Enumerable.Range(0, 4) select new object[] { mode, depth };

    private static byte[][] Family(int family) => family switch
    {
        0 => [], 1 => [[]], 2 => ["a"u8.ToArray(), "b"u8.ToArray(), "c"u8.ToArray()],
        3 => new[] { "aaazaaaa", "aaaaxaaa", "aaaqaaaa" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        4 => new[] { "中中文甲", "中中文乙", "中文国甲" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        5 => new[] { "привет", "пример", "проверка" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        6 => Enumerable.Range(0, 64).Select(i => new[] { (byte)i }).ToArray(),
        _ => Enumerable.Range(0, 65).Select(i => new[] { (byte)i }).ToArray(),
    };

    private static object Prepare(int mode, byte[][] values) => mode switch
    {
        0 => new PreparedMultiLiteralPackedNibbleSimdPrefilter(values),
        1 => PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(values),
        2 => PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateInvariantCyrillicIgnoreCase(values),
        3 => new PreparedMultiLiteralNibbleMaskPrefilter(values),
        4 => new PreparedMultiLiteralOffsetMaskPrefilter(values),
        _ => new PreparedMultiLiteralRareBytePrefilter(values),
    };

    private static ulong SearchLoan(PreparedByteSearch search) => Utf8ConstructionBudget.ArrayBytes<byte>(search.Count) + (search.Count > 3 ? Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>()) : 0);
    private static ulong Loan(object value) => value switch
    {
        PreparedMultiLiteralPackedNibbleSimdPrefilter p => Utf8ConstructionBudget.ArrayBytes<int>(p.MaskLength)
            + 2 * Utf8ConstructionBudget.ArrayBytes<Vector256<byte>>(p.MaskLength) + SearchLoan(p.Search),
        PreparedMultiLiteralNibbleMaskPrefilter p => 2 * Utf8ConstructionBudget.ArrayBytes<ulong[]>(p.MaskLength)
            + (ulong)(2 * p.MaskLength) * Utf8ConstructionBudget.ArrayBytes<ulong>(16) + SearchLoan(p.Search),
        PreparedMultiLiteralOffsetMaskPrefilter p => Utf8ConstructionBudget.ArrayBytes<int>(p.Offsets.Length)
            + Utf8ConstructionBudget.ArrayBytes<byte[]>(p.Offsets.Length) + (ulong)p.Offsets.Length * Utf8ConstructionBudget.ArrayBytes<byte>(256) + SearchLoan(p.Search),
        PreparedMultiLiteralRareBytePrefilter p => Utf8ConstructionBudget.ArrayBytes<int>(p.OffsetByValue.Length) + SearchLoan(p.Search),
        _ => throw new InvalidOperationException(),
    };

    private static byte[] Alternatives(byte[] literal, int offset, int mode)
    {
        var value = literal[offset];
        if (mode == 1 && value is >= (byte)'a' and <= (byte)'z') return [value, (byte)(value - 32)];
        if (mode != 2) return [value];
        var start = (value & 0xc0) == 0x80 ? offset - 1 : offset;
        if (start < 0 || Rune.DecodeFromUtf8(literal.AsSpan(start), out var rune, out var width) != OperationStatus.Done || width != 2) return [value];
        var scalar = rune.Value;
        var other = scalar is >= 0x410 and <= 0x42f ? scalar + 32
            : scalar is >= 0x430 and <= 0x44f ? scalar - 32 : scalar == 0x401 ? 0x451 : scalar == 0x451 ? 0x401 : -1;
        if (other < 0) return [value];
        var bytes = Encoding.UTF8.GetBytes(new Rune(other).ToString());
        return [value, bytes[offset - start]];
    }

    private static int[] BestOffsets(byte[][] literals) => Enumerable.Range(0, literals.Min(v => v.Length))
        .Select(offset => new { Offset = offset, Values = literals.Select(v => v[offset]).Distinct().ToArray() })
        .OrderBy(v => v.Values.Length).ThenBy(v => v.Values.Sum(b => PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(b)))
        .ThenBy(v => v.Offset).Take(3).Select(v => v.Offset).ToArray();

    private static void CheckMasks(int mode, object value, byte[][] literals)
    {
        var shortest = literals.Length == 0 ? int.MaxValue : literals.Min(v => v.Length);
        switch (value)
        {
            case PreparedMultiLiteralPackedNibbleSimdPrefilter p:
                Assert.Equal(shortest, p.ShortestLength);
                Assert.Equal(Simd && literals.Length is > 0 and <= 8 && shortest > 0, p.HasValue);
                if (!p.HasValue) { Assert.Empty(p.MaskOffsets); break; }
                for (var index = 0; index < p.MaskLength; index++)
                    for (var nibble = 0; nibble < 16; nibble++)
                    {
                        byte low = 0, high = 0;
                        for (var id = 0; id < literals.Length; id++)
                        {
                            var values = Alternatives(literals[id], p.MaskOffsets[index], mode);
                            if (values.Any(b => (b & 15) == nibble)) low |= (byte)(1 << id);
                            if (values.Any(b => (b >> 4) == nibble)) high |= (byte)(1 << id);
                        }
                        Assert.Equal(low, p.LowMaskVectors[index].GetElement(nibble));
                        Assert.Equal(low, p.LowMaskVectors[index].GetElement(nibble + 16));
                        Assert.Equal(high, p.HighMaskVectors[index].GetElement(nibble));
                        Assert.Equal(high, p.HighMaskVectors[index].GetElement(nibble + 16));
                    }
                var expected = literals.SelectMany(v => Alternatives(v, 0, mode)).Distinct().Order().ToArray();
                Assert.Equal(expected, p.Search.Values.ToArray().Order().ToArray());
                break;
            case PreparedMultiLiteralNibbleMaskPrefilter p:
                Assert.Equal(shortest, p.ShortestLength);
                Assert.Equal(literals.Length is > 0 and <= 64 && shortest > 0, p.HasValue);
                Assert.Equal(p.HasValue ? Math.Min(4, shortest) : 0, p.MaskLength);
                for (var offset = 0; offset < p.MaskLength; offset++)
                    for (var nibble = 0; nibble < 16; nibble++)
                    {
                        ulong low = 0, high = 0;
                        for (var id = 0; id < literals.Length; id++)
                        {
                            if ((literals[id][offset] & 15) == nibble) low |= 1ul << id;
                            if ((literals[id][offset] >> 4) == nibble) high |= 1ul << id;
                        }
                        Assert.Equal(low, p.LowMasks[offset][nibble]); Assert.Equal(high, p.HighMasks[offset][nibble]);
                    }
                break;
            case PreparedMultiLiteralOffsetMaskPrefilter p:
                Assert.Equal(shortest, p.ShortestLength);
                Assert.Equal(literals.Length > 0 && shortest > 0, p.HasValue);
                if (!p.HasValue) { Assert.Empty(p.Offsets); break; }
                Assert.Equal(BestOffsets(literals), p.Offsets);
                for (var index = 0; index < p.Offsets.Length; index++)
                    for (var b = 0; b < 256; b++) Assert.Equal(literals.Any(v => v[p.Offsets[index]] == b) ? 1 : 0, p.Masks[index][b]);
                break;
            case PreparedMultiLiteralRareBytePrefilter p:
                Assert.Equal(shortest, p.ShortestLength);
                if (literals.Length == 0) { Assert.Empty(p.OffsetByValue); break; }
                var selected = literals.SelectMany(v => v).Distinct().OrderBy(PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank).ThenBy(v => v).Take(3).ToArray();
                Assert.Equal(selected, p.Search.Values.ToArray());
                for (var b = 0; b < 256; b++)
                {
                    var max = selected.Contains((byte)b) ? literals.SelectMany(v => v.Select((item, index) => (item, index)))
                        .Where(v => v.item == b).Max(v => v.index) : -1;
                    Assert.Equal(max, p.OffsetByValue[b]);
                }
                break;
        }
    }

    [Theory]
    [MemberData(nameof(ModesAndFamilies))]
    public void RetainedLoansAndEveryMaskEntryAgreeWithIndependentModels(int mode, int family)
    {
        var literals = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var value = Prepare(mode, literals);
        Assert.Equal(48 + Loan(value), owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(0xa5, parent[0]);
        CheckMasks(mode, value, literals);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryByteAndWorkCutoffRestoresParentStorageAndKeepsSpentWork(int mode, bool bytes)
    {
        var literals = Family(3); ulong peak, work;
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { _ = Prepare(mode, literals); peak = owner.Budget.PeakBytes; work = owner.Budget.WorkSteps; }
        var successes = 0;
        for (var allowance = 0ul; allowance <= (bytes ? peak : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, bytes ? 128 + allowance : ulong.MaxValue,
                bytes ? ulong.MaxValue : allowance + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
            object? result = null; var error = Record.Exception(() => result = Prepare(mode, literals));
            if (error is null)
            { successes++; Assert.Equal(128 + Loan(result!), owner.Budget.ReservedBytes); Assert.Equal(bytes ? peak : work, allowance); }
            else
            {
                Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, bytes ? ulong.MaxValue : 2 + allowance);
        }
        Assert.Equal(1, successes);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void RootAndNestedSearchDepthAreAdmittedBeforeAcquisition(int mode, int depth)
    {
        var required = mode <= 2 && !Simd ? 1 : mode == 1 ? 3 : 2;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Record.Exception(() => Prepare(mode, Family(2)));
        if (depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        else { Assert.Null(error); Assert.Equal((ulong)required, owner.Budget.PeakDepth); }
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        if (depth == 0) Assert.Equal(128ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyFamiliesBorrowEveryExtentWithoutAnOwnerLoanOrWork()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        for (var mode = 0; mode < 6; mode++) Assert.Equal(0ul, Loan(Prepare(mode, [])));
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public void LongSelectorsKeepFixedStorageAndPreserveOffsetTieOrdering(int mode)
    {
        ulong shortLoan;
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var literals = new[] { new string('a', 128) + 'q', new string('a', 128) + 'z' }.Select(Encoding.UTF8.GetBytes).ToArray();
            var value = mode == 0 ? (object)new PreparedMultiLiteralOffsetMaskPrefilter(literals) : PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(literals);
            shortLoan = owner.Budget.PeakBytes;
            if (mode == 0) Assert.Equal(BestOffsets(literals), ((PreparedMultiLiteralOffsetMaskPrefilter)value).Offsets);
        }
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var literals = new[] { new string('a', 4096) + 'q', new string('a', 4096) + 'z' }.Select(Encoding.UTF8.GetBytes).ToArray();
            var value = mode == 0 ? (object)new PreparedMultiLiteralOffsetMaskPrefilter(literals) : PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(literals);
            Assert.Equal(shortLoan, owner.Budget.PeakBytes);
            if (mode == 0) Assert.Equal(BestOffsets(literals), ((PreparedMultiLiteralOffsetMaskPrefilter)value).Offsets);
            else if (Simd) Assert.Equal(new[] { 4096, 0, 1 }, ((PreparedMultiLiteralPackedNibbleSimdPrefilter)value).MaskOffsets);
        }
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void FoldedSupplementaryLiteralFamilyKeepsScalarRangesWithoutFallback(RegexOptions options)
    {
        var regex = new Utf8Regex("(?i:abcdefgh😀|xyzuvw😀|qwerty😀)", options);
        var subject = "xxABCDEFGH😀|xyzuvw😀|QWERTY😀yy"u8;
        Assert.True(regex.IsMatch(subject)); Assert.Equal(3, regex.Count(subject));
        var ranges = new List<(int, int)>(); foreach (var match in regex.EnumerateMatches(subject)) ranges.Add((match.IndexInBytes, match.LengthInBytes));
        Assert.Equal(new[] { (2, 12), (15, 10), (26, 10) }, ranges);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    private sealed class Box(int value) { public readonly int Value = value; }
}
