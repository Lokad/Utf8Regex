using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8MultiLiteralConstructionAdmissionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ByteAndWorkDenialsRestoreParentAndKeepAdmittedWork(bool automaton, bool byteLimit)
    {
        byte[][] literals = ["ab"u8.ToArray(), "ac"u8.ToArray(), "ad"u8.ToArray(), "bb"u8.ToArray()];
        ulong heldBytes, heldWork, lateCutoff, successCutoff;
        using (var measured = new Utf8ConstructionScope(1, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = measured.Budget.AllocateArray<int>(1, instructions: true);
            heldBytes = measured.Budget.ReservedBytes;
            heldWork = measured.Budget.WorkSteps;
            _ = BuildAndFind();
            // The published arrays still need their loans even when construction
            // scratch can grow less under a tighter byte cap.
            var required = byteLimit ? measured.Budget.ReservedBytes - heldBytes : measured.Budget.WorkSteps - heldWork;
            Assert.True(required > 1);
            lateCutoff = required - 1;
            successCutoff = byteLimit ? measured.Budget.PeakBytes - heldBytes : measured.Budget.WorkSteps - heldWork;
        }
        foreach (var allowance in new[] { 0ul, lateCutoff, successCutoff })
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
                Assert.Equal((2, 4), BuildAndFind());
                Assert.True(owner.Budget.ReservedBytes > heldBytes);
            }
            else
            {
                var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                    BuildAndFind());
                Assert.Equal(byteLimit ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps,
                    failure.ResourceKind);
                Assert.Equal(byteLimit ? byteCap : workCap, failure.Limit);
                Assert.True(failure.Requested > failure.Limit);
                Assert.Equal(heldBytes, owner.Budget.ReservedBytes);
                if (!byteLimit) Assert.Equal(workBeforeLastCharge, owner.Budget.WorkSteps);
                if (allowance == lateCutoff)
                {
                    Assert.True(owner.Budget.PeakBytes > heldBytes);
                    Assert.True(owner.Budget.WorkSteps > heldWork);
                }
            }
            Assert.Equal(17, parent[0]);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(1ul, owner.Budget.Reserved);
            Assert.InRange(owner.Budget.WorkSteps, workBeforeLastCharge, workCap);
            Assert.InRange(owner.Budget.PeakBytes, heldBytes, byteCap);
        }

        (int First, int Last) BuildAndFind()
        {
            ReadOnlySpan<byte> subject = "xxacbb"u8;
            if (automaton)
            {
                var search = PreparedMultiLiteralAutomatonSearch.Create(literals);
                return (search.IndexOf(subject), search.LastIndexOf(subject));
            }
            var trie = PreparedMultiLiteralTrieSearch.Create(literals);
            return (trie.IndexOf(subject), trie.LastIndexOf(subject));
        }
    }
}
