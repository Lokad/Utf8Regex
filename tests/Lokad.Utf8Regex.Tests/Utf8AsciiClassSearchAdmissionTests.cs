using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AsciiClassSearchAdmissionTests
{
    private static AsciiCharClass Class(int count, bool negated = false)
        => AsciiCharClass.FromBytes(Enumerable.Range(0, count).Select(value => (byte)value).ToArray(), negated);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    [InlineData(16)] [InlineData(63)] [InlineData(64)] [InlineData(128)]
    public void PositiveAndComplementedMasksCopyExactAscendingAsciiMembership(int count)
    {
        foreach (var negated in new[] { false, true })
        {
            var charClass = Class(count, negated);
            var expected = Enumerable.Range(0, 128).Where(value => negated ? value >= count : value < count)
                .Select(value => (byte)value).ToArray();
            Span<byte> destination = stackalloc byte[129];
            destination.Fill(0xff);
            Assert.Equal(expected.Length, charClass.CopyMatchBytes(destination));
            Assert.Equal(expected, destination[..expected.Length].ToArray());
            Assert.Equal(0xff, destination[expected.Length]);
            Assert.Equal(expected, charClass.GetMatchBytes());
            Assert.Equal(negated ? Array.Empty<byte>() : expected, charClass.GetPositiveMatchBytes());
            foreach (var value in Enumerable.Range(0, 256))
                Assert.Equal(value < 128 && (negated ? value >= count : value < count), charClass.Contains((byte)value));
        }
    }

    [Fact]
    public void SparseMasksCrossBothWordsWithoutTouchingDestinationOnShortBuffer()
    {
        byte[] values = [0, 3, 31, 63, 64, 79, 126, 127];
        var charClass = AsciiCharClass.FromBytes(values);
        var shortBuffer = Enumerable.Repeat((byte)0xee, values.Length - 1).ToArray();
        Assert.Throws<ArgumentException>(() => charClass.CopyMatchBytes(shortBuffer));
        Assert.All(shortBuffer, value => Assert.Equal(0xee, value));
        var output = new byte[128];
        Assert.Equal(values.Length, charClass.CopyMatchBytes(output));
        Assert.Equal(values, output[..values.Length]);
    }

    private static void Produce(int route, AsciiCharClass charClass, byte[] caller)
    {
        switch (route)
        {
            case 0: Assert.Equal(4, charClass.GetMatchBytes().Length); break;
            case 1: Assert.Equal(4, charClass.GetPositiveMatchBytes().Length); break;
            case 2: Assert.Equal(124, Class(4, true).GetMatchBytes().Length); break;
            case 3: Assert.Equal(caller, PreparedByteSearch.Create(caller).Values.ToArray()); break;
            case 4: Assert.Equal(4, Utf8PreparedByteSearchFactory.CreateFromPositiveClass(charClass).Count); break;
            case 5: Assert.Equal(4, new AsciiSimplePatternRunPlan(charClass, 2, 4).Search.Count); break;
            case 6:
                var headTail = new AsciiSimplePatternAnchoredHeadTailRunPlan(charClass, charClass, 2);
                Assert.True(headTail.IsMatch(new byte[] { 0, 1, 2 }));
                Assert.False(headTail.IsMatch(new byte[] { 0, 1, 128 })); break;
            case 7:
                var positive = Utf8FixedDistanceSet.FromBytes(7, caller, false);
                Assert.Equal(7, positive.Distance); Assert.True(positive.Contains(2)); Assert.False(positive.Contains(5)); break;
            default:
                var complement = Utf8FixedDistanceSet.FromBytes(8, caller, true);
                Assert.False(complement.Contains(2)); Assert.True(complement.Contains(5)); Assert.False(complement.Contains(128)); break;
        }
    }

    public static IEnumerable<object[]> Routes() => Enumerable.Range(0, 9).Select(route => new object[] { route });
    public static IEnumerable<object[]> Denials()
        => from route in Enumerable.Range(0, 9) from denial in Enumerable.Range(0, 3) select new object[] { route, denial };

    [Theory]
    [MemberData(nameof(Denials))]
    public void RootByteWorkAndDepthDenialRestoreParentBeforeArrayAcquisition(int route, int denial)
    {
        var charClass = Class(4); byte[] caller = [0, 1, 2, 3];
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 48ul : ulong.MaxValue,
            denial == 1 ? 1ul : ulong.MaxValue, denial == 2 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Produce(route, charClass, caller));
        Assert.Equal(denial switch { 0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionWorkSteps, _ => Utf8ResourceLimitKind.ConstructionDepth }, error.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes); Assert.Equal(48ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Depth); Assert.Null(owner.Budget.ObjectCheckpoint); Assert.Equal(0xa5, parent[0]);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryWorkCutoffRecoversParentAndSuccessRetainsItsOwnedArrayAndSet(int route)
    {
        var charClass = Class(4); byte[] caller = [0, 1, 2, 3]; ulong work, retained;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Produce(route, charClass, caller); work = measure.Budget.WorkSteps; retained = measure.Budget.ReservedBytes; }
        Assert.Equal(route switch { 0 or 1 => 8ul, 2 => 248ul, 3 => 270ul, 4 or 5 => 266ul, _ => 258ul }, work);
        var setBytes = route >= 3 ? Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>()
            + Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>() : 0;
        Assert.Equal(setBytes + (route >= 6 ? 0ul : Utf8ConstructionBudget.ArrayBytes<byte>(route == 2 ? 124 : 4)), retained);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 1, ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
            var error = Record.Exception(() => Produce(route, charClass, caller));
            if (cutoff < work)
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
            }
            else { Assert.Null(error); Assert.Equal(48 + retained, owner.Budget.ReservedBytes); }
            Assert.Equal(0ul, owner.Budget.Depth); Assert.InRange(owner.Budget.WorkSteps, 1ul, cutoff + 1);
            Assert.Equal(route >= 3 && cutoff == work, owner.Budget.ObjectCheckpoint is not null); Assert.Equal(0xa5, parent[0]);
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void EveryByteCutoffIncludesPeakScratchAndRestoresParentOnFailure(int route)
    {
        var charClass = Class(4); byte[] caller = [0, 1, 2, 3]; ulong peak;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Produce(route, charClass, caller); peak = measure.Budget.PeakBytes; }
        var setBytes = route >= 3 ? Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>()
            + Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>() : 0;
        Assert.Equal(setBytes + (route >= 6 ? 0ul : Utf8ConstructionBudget.ArrayBytes<byte>(route == 2 ? 124 : 4)), peak);
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, 48 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateArray<byte>(1, false); parent[0] = 0xa5;
            var error = Record.Exception(() => Produce(route, charClass, caller));
            if (allowance < peak)
            {
                var limit = Assert.IsType<Utf8ResourceLimitExceededException>(error);
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, limit.ResourceKind); Assert.True(limit.Requested > 48 + allowance);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
            }
            else Assert.Null(error);
            Assert.InRange(owner.Budget.PeakBytes, 48ul, 48 + allowance); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(route >= 3 && allowance == peak, owner.Budget.ObjectCheckpoint is not null); Assert.Equal(0xa5, parent[0]);
        }
    }

    [Fact]
    public void SharedEmptyClassBytesRequireNoAcquisitionOrDepth()
    {
        var empty = Class(0); var full = Class(128, true);
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        Assert.Empty(empty.GetMatchBytes()); Assert.Empty(full.GetMatchBytes()); Assert.Empty(Class(4, true).GetPositiveMatchBytes());
        Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes); Assert.Equal(0ul, owner.Budget.PeakDepth);
    }

    [Fact]
    public void PreparedSearchIsolatesArbitraryCallerBytesAndKeepsFullByteDomain()
    {
        byte[] values = [0, 64, 128, 255];
        var search = PreparedByteSearch.Create(values); Array.Fill(values, (byte)17);
        Assert.Equal(new byte[] { 0, 64, 128, 255 }, search.Values.ToArray());
        Assert.Equal(1, search.IndexOf(new byte[] { 17, 128, 18, 255 }));
        Assert.Equal(3, search.LastIndexOf(new byte[] { 17, 128, 18, 255 }));
        Assert.Equal(-1, search.IndexOf(new byte[] { 17, 18 }));
    }

    private delegate bool ClassRunMatch(ReadOnlySpan<byte> input, int index, int length, AsciiCharClass charClass);
    private delegate bool UniformMatch(ReadOnlySpan<byte> input, AsciiSimplePatternToken[] branch, out int length);

    [Fact]
    public void RuntimeClassValidationUsesCallerScratchWithoutConstructionOrHeapAllocation()
    {
        var charClass = AsciiCharClass.FromBytes("ACDEGHIJKLM"u8);
        var match = typeof(Utf8AsciiAnchoredValidatorExecutor).GetMethod("MatchesCharClassRun", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<ClassRunMatch>();
        byte[] hit = "ACDEGHIJKLMACDEGHIJKLM"u8.ToArray(); byte[] miss = "ACDEGHIJKLMx"u8.ToArray();
        Assert.True(match(hit, 0, hit.Length, charClass)); Assert.False(match(miss, 0, miss.Length, charClass));
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var before = GC.GetAllocatedBytesForCurrentThread(); var count = 0;
        for (var iteration = 0; iteration < 2000; iteration++)
            if (match(hit, 0, hit.Length, charClass)) count++;
        var allocation = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(2000, count); Assert.Equal(0, allocation); Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes);
    }

    [Fact]
    public void CompiledUniformBranchUsesCallerScratchWithoutConstructionOrHeapAllocation()
    {
        var charClass = AsciiCharClass.FromBytes("ACDEGHIJKLM"u8);
        var branch = Enumerable.Repeat(new AsciiSimplePatternToken(charClass), 12).ToArray();
        var match = typeof(Utf8SimplePatternCompiledWholeMatcher).GetMethod("TryMatchUniformPositiveCharClassBranch", BindingFlags.Static | BindingFlags.NonPublic)!
            .CreateDelegate<UniformMatch>();
        byte[] hit = "ACDEGHIJKLMAC"u8.ToArray(); byte[] miss = "ACDEGHIJKLMxC"u8.ToArray();
        Assert.True(match(hit, branch, out var length)); Assert.Equal(12, length); Assert.False(match(miss, branch, out _));
        using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
        var before = GC.GetAllocatedBytesForCurrentThread(); var count = 0;
        for (var iteration = 0; iteration < 2000; iteration++)
            if (match(hit, branch, out _)) count++;
        var allocation = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(2000, count); Assert.Equal(0, allocation); Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarPublicOperationsPreserveAsciiClassesAcrossSupplementarySubjectsWithoutBcl(bool compiled)
    {
        var regex = new Utf8Regex("[ABCD]{2,4}", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var runtime = Assert.IsType<Utf8VerifierRuntime>(typeof(Utf8Regex).GetProperty("_verifierRuntime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
        Utf8OwnedVerifierAssertions.RetainsPreparedOwner(runtime);
        var subject = Encoding.UTF8.GetBytes("😀ABCD中ABC😀A");
        Assert.True(regex.IsMatch(subject)); Assert.Equal(2, regex.Count(subject));
        var match = regex.Match(subject); Assert.Equal(4, match.IndexInBytes); Assert.Equal(4, match.LengthInBytes);
        var detail = regex.MatchDetailed(subject); Assert.Equal(4, detail.IndexInBytes); Assert.Equal(4, detail.LengthInBytes);
        var count = 0; foreach (var item in regex.EnumerateMatches(subject)) { Assert.True(item.LengthInBytes >= 2); count++; }
        Assert.Equal(2, count); Assert.Equal("😀X中X😀A", regex.ReplaceToString(subject, "X"));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null); Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
