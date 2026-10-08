using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SharedByteSearchAdmissionTests
{
    private static ulong SetBytes => Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>()
        + Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>();

    public static IEnumerable<object[]> Shapes()
        => from length in new[] { 0, 1, 2, 3, 4, 5, 16, 128, 256 }
           from span in new[] { false, true } select new object[] { length, span };

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ArrayAndBorrowedSpanConstructorsRetainOneIndependentAdmittedCopy(int length, bool span)
    {
        var caller = Enumerable.Range(0, length).Select(value => (byte)(17 + value * 79)).ToArray();
        var expected = caller.ToArray();
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var search = span ? PreparedByteSearch.Create(caller.AsSpan()) : PreparedByteSearch.Create(caller);
        Array.Fill(caller, (byte)11);
        Assert.Equal(expected, search.Values.ToArray()); Assert.Equal(length, search.Count);
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(length) + (length > 3 ? SetBytes : 0), owner.Budget.ReservedBytes);
        Assert.Equal(length > 3 ? (ulong)(3 * length + 258) : (ulong)(2 * length), owner.Budget.WorkSteps);
        Assert.Equal(length == 0 ? 0ul : length > 3 ? 2ul : 1ul, owner.Budget.PeakDepth);
        Assert.Equal(length > 3, owner.Budget.ObjectCheckpoint is not null);
        Assert.Equal(0ul, owner.Budget.Depth);
        byte[] subject = [11, 17, 96, 175, 254, 128, 255];
        Assert.Equal(Array.FindIndex(subject, value => expected.Contains(value)), search.IndexOf(subject));
        Assert.Equal(Array.FindLastIndex(subject, value => expected.Contains(value)), search.LastIndexOf(subject));
    }

    public static IEnumerable<object[]> Cutoffs()
        => from length in new[] { 1, 3, 4, 5, 16, 256 }
           from span in new[] { false, true } select new object[] { length, span };

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void EveryByteAndWorkCutoffPreservesParentObjectAndDoesNotRefundWork(int length, bool span)
    {
        var input = Enumerable.Range(0, length).Select(value => (byte)value).ToArray();
        var bytes = Utf8ConstructionBudget.ArrayBytes<byte>(length) + (length > 3 ? SetBytes : 0);
        var work = length > 3 ? (ulong)(3 * length + 258) : (ulong)(2 * length);
        foreach (var byteLimit in new[] { false, true })
        for (var allowance = 0ul; allowance <= (byteLimit ? bytes : work); allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, byteLimit ? 112 + allowance : ulong.MaxValue,
                byteLimit ? ulong.MaxValue : allowance + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var error = Record.Exception(() => { _ = span ? PreparedByteSearch.Create(input.AsSpan()) : PreparedByteSearch.Create(input); });
            if (allowance < (byteLimit ? bytes : work))
            {
                var limit = Assert.IsType<Utf8ResourceLimitExceededException>(error);
                Assert.Equal(byteLimit ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, limit.ResourceKind);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                if (byteLimit) Assert.True(limit.Requested > 112 + allowance);
            }
            else
            {
                Assert.Null(error); Assert.Equal(112 + bytes, owner.Budget.ReservedBytes);
                Assert.Equal(length > 3, !ReferenceEquals(checkpoint, owner.Budget.ObjectCheckpoint));
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, byteLimit ? 2 + work : 2 + allowance);
            if (byteLimit) Assert.InRange(owner.Budget.PeakBytes, 112ul, 112 + allowance);
        }
    }

    public static IEnumerable<object[]> Denials()
        => from route in Enumerable.Range(0, 4) from denial in Enumerable.Range(0, 3) select new object[] { route, denial };

    [Theory]
    [MemberData(nameof(Denials))]
    public void SharedSearchAndSetRootsDenyBytesWorkAndDepthBeforeHeapAcquisition(int route, int denial)
    {
        byte[] input = [0, 64, 128, 255];
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 48ul : ulong.MaxValue,
            denial == 1 ? 1ul : ulong.MaxValue, denial == 2 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => {
            switch (route)
            {
                case 0: _ = PreparedByteSearch.Create(input); break;
                case 1: _ = PreparedByteSearch.Create(input.AsSpan()); break;
                case 2: _ = PreparedByteSet.CreateOwned(input); break;
                default: _ = PreparedByteSet.CreateOwned(1, 1, 1, 1ul << 63); break;
            }
        });
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionWorkSteps, _ => Utf8ResourceLimitKind.ConstructionDepth }, error.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Null(owner.Budget.ObjectCheckpoint); Assert.Equal(0xa5, parent[0]);
    }

    [Fact]
    public void SharedEmptyPayloadsNeedNoOwnerWorkOrDepth()
    {
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var array = PreparedByteSearch.Create(); var span = PreparedByteSearch.Create(ReadOnlySpan<byte>.Empty);
        Assert.Equal(0, array.Count); Assert.Equal(-1, array.IndexOf(new byte[] { 0, 128, 255 }));
        Assert.Equal(-1, span.LastIndexOf(new byte[] { 0, 128, 255 }));
        Assert.Same(PreparedByteSet.Empty, PreparedByteSet.CreateOwned(0, 0, 0, 0));
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void DuplicatesAndInputOrderRemainAvailableWithoutChangingMembership()
    {
        byte[] input = [255, 0, 255, 64, 128, 0];
        using var owner = Utf8ConstructionScope.Enter();
        var search = PreparedByteSearch.Create(input.AsSpan());
        Assert.Equal(input, search.Values.ToArray()); Assert.Equal(6, search.Count);
        Assert.Equal(1, search.IndexOf(new byte[] { 17, 255, 18, 128 }));
        Assert.Equal(3, search.LastIndexOf(new byte[] { 17, 255, 18, 128 }));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public void MultiLiteralSearchProducersUseTheSameByteOwnerAndRecoverAfterDenial(int route)
    {
        byte[][] literals = ["ABCD"u8.ToArray(), "EFGH"u8.ToArray(), "IJKL"u8.ToArray(), "MNOP"u8.ToArray()];
        PreparedByteSearch Produce()
        {
            return route switch
            {
                0 => PreparedMultiLiteralPackedSearch.Create(literals).DiscriminatorSearch,
                1 => new PreparedMultiLiteralPackedNibbleSimdPrefilter(literals).Search,
                2 => new PreparedMultiLiteralNibbleMaskPrefilter(literals).Search,
                3 => new PreparedMultiLiteralOffsetMaskPrefilter(literals).Search,
                4 => new PreparedMultiLiteralRareBytePrefilter(literals).Search,
                _ => PreparedMultiLiteralTrieSearch.Create(literals).RootSearch,
            };
        }
        using var owner = new Utf8ConstructionScope(0, 48, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.Throws<Utf8ResourceLimitExceededException>(() => Produce()).ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Null(owner.Budget.ObjectCheckpoint); Assert.Equal(0xa5, parent[0]);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    private sealed class Box(int value) { public readonly int Value = value; }

    [Fact]
    public void SharedConstructionOwnerHasOneFlavorNeutralIdentity()
    {
        Assert.Equal("Lokad.Utf8Regex.Internal", typeof(Utf8ConstructionScope).Namespace);
        Assert.Equal(typeof(Utf8ConstructionScope).Namespace, typeof(Utf8ConstructionBudget).Namespace);
        Assert.Empty(typeof(Utf8Regex).Assembly.GetTypes().Where(type =>
            type.Namespace == "Lokad.Utf8Regex.Internal.FrontEnd" && type.Name is "Utf8ConstructionScope" or "Utf8ConstructionBudget"));
    }
}
