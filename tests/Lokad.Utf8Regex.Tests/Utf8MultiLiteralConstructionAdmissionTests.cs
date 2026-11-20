using System.Runtime.Intrinsics.X86;
using System.Text;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MultiLiteralConstructionAdmissionTests
{
    public enum Builder
    {
        Trie,
        Automaton,
        PackedAtStart,
        PackedAfterPrefix,
        LeadingSegment,
        ExactMask,
        AsciiFoldedMask,
        CyrillicFoldedMask,
    }

    public static IEnumerable<object[]> Cutoffs() =>
        from builder in Enum.GetValues<Builder>()
        from byteLimit in new[] { false, true }
        select new object[] { builder, byteLimit };

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void ByteAndWorkDenialsRestoreParentAndKeepAdmittedWork(Builder builder, bool byteLimit)
    {
        var texts = builder switch
        {
            Builder.PackedAtStart => new[] { "abc", "def", "ghi" },
            Builder.PackedAfterPrefix => new[] { "aabx", "aacx", "aadx" },
            Builder.LeadingSegment => new[] { "éx", "éy", "étail" },
            Builder.ExactMask or Builder.AsciiFoldedMask => new[] { "aaazaaaa", "aaaaxaaa", "aaaqaaaa" },
            Builder.CyrillicFoldedMask => new[] { "привет", "пример", "проверка" },
            _ => new[] { "ab", "ac", "ad", "bb" },
        };
        var literals = texts.Select(Encoding.UTF8.GetBytes).ToArray();
        ulong heldBytes, heldWork, retainedBytes, lateCutoff, successCutoff;
        using (var measured = new Utf8ConstructionScope(1, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = measured.Budget.AllocateArray<int>(1, instructions: true);
            heldBytes = measured.Budget.ReservedBytes;
            heldWork = measured.Budget.WorkSteps;
            BuildAndCheck(builder, literals);
            // The published arrays still need their loans even when construction
            // scratch can grow less under a tighter byte cap.
            retainedBytes = measured.Budget.ReservedBytes - heldBytes;
            var required = byteLimit ? retainedBytes : measured.Budget.WorkSteps - heldWork;
            lateCutoff = required == 0 ? 0 : required - 1;
            successCutoff = byteLimit ? measured.Budget.PeakBytes - heldBytes : measured.Budget.WorkSteps - heldWork;
        }
        foreach (var allowance in new[] { 0ul, lateCutoff, successCutoff }.Distinct())
        {
            var byteCap = byteLimit ? heldBytes + allowance : ulong.MaxValue;
            var workCap = byteLimit ? ulong.MaxValue : heldWork + allowance;
            using var owner = new Utf8ConstructionScope(1, byteCap, workCap, ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<int>(1, instructions: true);
            parent[0] = 17;
            var workBeforeLastCharge = heldWork;
            owner.Budget.CheckpointHook = work => workBeforeLastCharge = work;

            if (allowance == successCutoff)
            {
                BuildAndCheck(builder, literals);
                Assert.Equal(heldBytes + retainedBytes, owner.Budget.ReservedBytes);
            }
            else
            {
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                    BuildAndCheck(builder, literals));
                Assert.Equal(byteLimit ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    failure.ResourceKind);
                Assert.Equal(byteLimit ? byteCap : workCap, failure.Limit);
                Assert.True(failure.Requested > failure.Limit);
                Assert.Equal(heldBytes, owner.Budget.ReservedBytes);
                if (!byteLimit) Assert.Equal(workBeforeLastCharge, owner.Budget.WorkSteps);
                if (allowance != 0)
                {
                    Assert.True(owner.Budget.WorkSteps > heldWork);
                    if (retainedBytes != 0) Assert.True(owner.Budget.PeakBytes > heldBytes);
                }
            }
            Assert.Equal(17, parent[0]);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(1ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, workBeforeLastCharge, workCap);
            Assert.InRange(owner.Budget.PeakBytes, heldBytes, byteCap);
        }
    }

    private static void BuildAndCheck(Builder builder, byte[][] literals)
    {
        switch (builder)
        {
            case Builder.Trie:
                var trie = PreparedMultiLiteralTrieSearch.Create(literals);
                Assert.Equal((2, 4), (trie.IndexOf("xxacbb"u8), trie.LastIndexOf("xxacbb"u8)));
                break;
            case Builder.Automaton:
                var search = PreparedMultiLiteralAutomatonSearch.Create(literals);
                Assert.Equal((2, 4), (search.IndexOf("xxacbb"u8), search.LastIndexOf("xxacbb"u8)));
                break;
            case Builder.PackedAtStart:
            case Builder.PackedAfterPrefix:
                Assert.True(PreparedMultiLiteralPackedSearch.TryCreate(literals, out var packed));
                Assert.Same(literals, packed.Literals);
                break;
            case Builder.LeadingSegment:
                Assert.Equal("é"u8.ToArray(),
                    Assert.Single(new PreparedMultiLiteralLeadingUtf8SegmentPrefilter(literals).Segments));
                break;
            default:
                var mask = builder switch
                {
                    Builder.ExactMask => new PreparedMultiLiteralPackedNibbleSimdPrefilter(literals),
                    Builder.AsciiFoldedMask => PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(literals),
                    Builder.CyrillicFoldedMask => PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateInvariantCyrillicIgnoreCase(literals),
                    _ => throw new ArgumentOutOfRangeException(nameof(builder)),
                };
                Assert.Equal(Ssse3.IsSupported && Sse2.IsSupported, mask.HasValue);
                break;
        }
    }
}
