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
        from mode in Enumerable.Range(0, 3) from family in Enumerable.Range(0, 8) select new object[] { mode, family };
    public static IEnumerable<object[]> Depths() =>
        from mode in Enumerable.Range(0, 3) from depth in Enumerable.Range(0, 4) select new object[] { mode, depth };

    private static byte[][] Family(int family) => family switch
    {
        0 => [], 1 => [[]], 2 => ["a"u8.ToArray(), "b"u8.ToArray(), "c"u8.ToArray()],
        3 => new[] { "aaazaaaa", "aaaaxaaa", "aaaqaaaa" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        4 => new[] { "中中文甲", "中中文乙", "中文国甲" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        5 => new[] { "привет", "пример", "проверка" }.Select(Encoding.UTF8.GetBytes).ToArray(),
        6 => Enumerable.Range(0, 64).Select(i => new[] { (byte)i }).ToArray(),
        _ => Enumerable.Range(0, 65).Select(i => new[] { (byte)i }).ToArray(),
    };

    private static PreparedMultiLiteralPackedNibbleSimdPrefilter Prepare(int mode, byte[][] values) => mode switch
    {
        0 => new PreparedMultiLiteralPackedNibbleSimdPrefilter(values),
        1 => PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(values),
        2 => PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateInvariantCyrillicIgnoreCase(values),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
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

    private static void CheckMasks(int mode, PreparedMultiLiteralPackedNibbleSimdPrefilter p, byte[][] literals)
    {
        var shortest = literals.Length == 0 ? int.MaxValue : literals.Min(v => v.Length);
        Assert.Equal(shortest, p.ShortestLength);
        Assert.Equal(Simd && literals.Length is > 0 and <= 8 && shortest > 0, p.HasValue);
        if (!p.HasValue) { Assert.Empty(p.MaskOffsets); return; }
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
    }

    [Theory]
    [MemberData(nameof(ModesAndFamilies))]
    public void PublishedMasksKeepTheirVectorsAdmittedAndMatchTheByteOracle(int mode, int family)
    {
        var literals = Family(family);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 0xa5;
        var held = owner.Budget.ReservedBytes;
        var value = Prepare(mode, literals);
        CheckMasks(mode, value, literals);
        if (value.HasValue)
        {
            var vectorBytes = (ulong)(value.LowMaskVectors.Length + value.HighMaskVectors.Length) * (ulong)Vector256<byte>.Count;
            Assert.InRange(owner.Budget.ReservedBytes, held + vectorBytes, owner.Budget.PeakBytes);
        }
        else Assert.Equal(held, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0xa5, parent[0]);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void RootAndNestedSearchDepthAreAdmittedBeforeAcquisition(int mode, int depth)
    {
        var required = !Simd ? 1 : mode == 1 ? 3 : 2;
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Record.Exception(() => Prepare(mode, Family(2)));
        if (depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        else { Assert.Null(error); Assert.Equal((ulong)required, owner.Budget.PeakDepth); }
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        if (depth == 0) Assert.Equal(checkpoint, owner.Budget.PeakBytes);
    }

    [Fact]
    public void EmptyFamiliesBorrowEveryExtentWithoutAnOwnerLoanOrWork()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        for (var mode = 0; mode < 3; mode++) Assert.False(Prepare(mode, []).HasValue);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void LongSelectorsKeepFixedStorageAndPreserveOffsetTieOrdering()
    {
        ulong shortLoan;
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var literals = new[] { new string('a', 128) + 'q', new string('a', 128) + 'z' }.Select(Encoding.UTF8.GetBytes).ToArray();
            _ = PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(literals);
            shortLoan = owner.Budget.PeakBytes;
        }
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            var literals = new[] { new string('a', 4096) + 'q', new string('a', 4096) + 'z' }.Select(Encoding.UTF8.GetBytes).ToArray();
            var value = PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(literals);
            Assert.Equal(shortLoan, owner.Budget.PeakBytes);
            if (Simd) Assert.Equal(new[] { 4096, 0, 1 }, value.MaskOffsets);
        }
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)] [InlineData(RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void FoldedSupplementaryLiteralFamilyReportsScalarByteRanges(RegexOptions options)
    {
        var regex = new Utf8Regex("(?i:abcdefgh😀|xyzuvw😀|qwerty😀)", options: options);
        var subject = "xxABCDEFGH😀|xyzuvw😀|QWERTY😀yy"u8;
        Assert.True(regex.IsMatch(subject)); Assert.Equal(3, regex.Count(subject));
        var ranges = new List<(int, int)>(); foreach (var match in regex.EnumerateMatches(subject)) ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
        Assert.Equal(new[] { (2, 12), (15, 10), (26, 10) }, ranges);
    }

    private sealed class Box(int value) { public readonly int Value = value; }
}
