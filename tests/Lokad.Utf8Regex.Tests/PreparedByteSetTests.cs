using System.Buffers;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class PreparedByteSetTests
{
    [Fact]
    public void EveryByteCanBeTheOnlyMemberOrTheOnlyExcludedMember()
    {
        byte[] all = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        for (var member = 0; member < 256; member++)
        {
            foreach (var complement in new[] { false, true })
            {
                var values = all.Where(value => (value == member) != complement).ToArray();
                var set = PreparedByteSet.CreateOwned(values);
                var oracle = SearchValues.Create(values);
                foreach (var value in all) Assert.Equal((value == member) != complement, set.Contains(value));
                for (var offset = 0; offset < 33; offset++)
                {
                    var input = all.AsSpan(offset);
                    Assert.Equal(input.IndexOfAny(oracle), set.IndexOf(input));
                    Assert.Equal(input.LastIndexOfAny(oracle), set.LastIndexOf(input));
                    Assert.Equal(input.IndexOfAnyExcept(oracle), set.IndexOfAnyExcept(input));
                    Assert.Equal(input.LastIndexOfAnyExcept(oracle), set.LastIndexOfAnyExcept(input));
                }
            }
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)] [InlineData(11)]
    public void ForwardReverseAndExceptAgreeAcrossVectorBoundariesAndUnalignedSlices(int profile)
    {
        var random = new Random(8433 + profile);
        byte[] values = profile switch
        {
            0 => [], 1 => Enumerable.Range(0, 256).Select(value => (byte)value).ToArray(),
            2 => [0, 64, 128, 255, 0, 255], 3 => "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_"u8.ToArray(),
            4 => Enumerable.Range(115, 35).Select(value => (byte)value).ToArray(),
            _ => Enumerable.Range(0, 256).Where(_ => random.Next(4) < profile % 3 + 1).Select(value => (byte)value).ToArray(),
        };
        var set = PreparedByteSet.CreateOwned(values);
        var oracle = SearchValues.Create(values);
        var members = values.Distinct().ToArray();
        var nonmembers = Enumerable.Range(0, 256).Select(value => (byte)value).Except(members).ToArray();
        int[] lengths = [0, 1, 2, 3, 7, 15, 16, 17, 31, 32, 33, 47, 63, 64, 65, 127, 128, 129, 257, 1025];
        foreach (var length in lengths)
        foreach (var offset in new[] { 0, 1, 7, 15, 16, 31, 32 })
        foreach (var subjectKind in new[] { 0, 1, 2 })
        {
            var storage = new byte[length + offset];
            random.NextBytes(storage);
            var input = storage.AsSpan(offset, length);
            var pool = subjectKind == 1 ? members : subjectKind == 2 ? nonmembers : [];
            if (pool.Length != 0)
                for (var i = 0; i < input.Length; i++) input[i] = pool[random.Next(pool.Length)];
            Assert.Equal(input.IndexOfAny(oracle), set.IndexOf(input));
            Assert.Equal(input.LastIndexOfAny(oracle), set.LastIndexOf(input));
            Assert.Equal(input.IndexOfAnyExcept(oracle), set.IndexOfAnyExcept(input));
            Assert.Equal(input.LastIndexOfAnyExcept(oracle), set.LastIndexOfAnyExcept(input));
        }
    }

    [Fact]
    public void MatrixSearchFindsEachLaneAndTheScalarTailInBothDirections()
    {
        byte[] members = [0, 15, 16, 31, 63, 64, 127, 128, 143, 191, 192, 254, 255];
        var set = PreparedByteSet.CreateOwned(members);
        foreach (var member in members)
        foreach (var length in new[] { 15, 16, 17, 31, 32, 33, 65, 129 })
        for (var position = 0; position < length; position++)
        {
            var input = Enumerable.Repeat((byte)3, length).ToArray(); input[position] = member;
            Assert.Equal(position, set.IndexOf(input)); Assert.Equal(position, set.LastIndexOf(input));
            Array.Fill(input, member); input[position] = 3;
            Assert.Equal(position, set.IndexOfAnyExcept(input)); Assert.Equal(position, set.LastIndexOfAnyExcept(input));
        }
    }

    [Fact]
    public void SetOwnershipIsIndependentOfCallerMutationAndAdmitsEveryInstanceField()
    {
        byte[] values = [0, 64, 128, 255];
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var set = PreparedByteSet.CreateOwned(values); Array.Fill(values, (byte)17);
        Assert.True(set.Contains(0)); Assert.True(set.Contains(255)); Assert.False(set.Contains(17));
        Assert.Equal(262ul, owner.Budget.WorkSteps);
        Assert.Equal(Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>()), owner.Budget.ReservedBytes);
        Assert.NotNull(set);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void SharedEmptySetHasNoAcquisitionAndAsciiComplementsExcludeNonAscii()
    {
        var full = AsciiCharClass.FromRange(Utf8InclusiveByteRange.Create(0, 127), true);
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        Assert.Same(PreparedByteSet.Empty, PreparedByteSet.CreateOwned(Array.Empty<byte>()));
        Assert.Same(PreparedByteSet.Empty, Utf8PreparedByteSearchFactory.CreateSet(full));
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void NestedObjectDenialPreservesParentLoanAndCheckpoint()
    {
        var setBytes = Math.Max(128ul, Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>());
        using var owner = new Utf8ConstructionScope(0, 128 + setBytes - 1, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.Throws<Utf8ResourceLimitExceededException>(() => PreparedByteSet.CreateOwned(new byte[] { 0, 128, 255 }));
        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(261ul, owner.Budget.WorkSteps);
    }

    [Fact]
    public void DirectAsciiMaskAndFixedDistanceSearchAgreeIncludingEmptyComplements()
    {
        byte[] input = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
        foreach (var count in new[] { 0, 1, 4, 16, 63, 64, 127, 128 })
        foreach (var negated in new[] { false, true })
        {
            var values = input[..count];
            var charClass = AsciiCharClass.FromBytes(values, negated);
            var direct = Utf8PreparedByteSearchFactory.CreateSet(charClass);
            var fixedSet = Lokad.Utf8Regex.Internal.Planning.Utf8FixedDistanceSet.FromBytes(7, values, negated);
            var expected = input.Where(value => value < 128 && ((value < count) != negated)).ToArray();
            var oracle = SearchValues.Create(expected);
            foreach (var value in input)
            {
                Assert.Equal(expected.Contains(value), direct.Contains(value));
                Assert.Equal(expected.Contains(value), fixedSet.Search.Contains(value));
            }
            for (var offset = 0; offset < input.Length; offset++)
            {
                var subject = input.AsSpan(offset);
                Assert.Equal(subject.IndexOfAny(oracle), direct.IndexOf(subject));
                Assert.Equal(subject.LastIndexOfAny(oracle), direct.LastIndexOf(subject));
                Assert.Equal(subject.IndexOfAnyExcept(oracle), direct.IndexOfAnyExcept(subject));
                Assert.Equal(subject.LastIndexOfAnyExcept(oracle), direct.LastIndexOfAnyExcept(subject));
                Assert.Equal(subject.IndexOfAny(oracle), fixedSet.Search.IndexOf(subject));
            }
        }
    }

    private sealed class Box(int value) { public readonly int Value = value; }
}
