using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionBufferAdmissionTests
{
    [Theory]
    [InlineData("(?<x>a?)*b", "aaab", true, false)]
    [InlineData("(?<x>a?)*b", "aaab", true, true)]
    [InlineData("(?=😀)(?<x>.)\\k<x>", "😀😀", false, false)]
    [InlineData("(?=😀)(?<x>.)\\k<x>", "😀😀", false, true)]
    public void ByteCutoffsFailOwnedAndRestoreBothCounters(string pattern, string text, bool nonBacktracking, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (nonBacktracking ? RegexOptions.NonBacktracking : 0)
            | (compiled ? RegexOptions.Compiled : 0);
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        ulong peak;
        int count;
        using (var measured = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity))
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            count = regex.Count(input);
            peak = measured.Budget.PeakBytes;
            Assert.True(measured.Budget.ReservedBytes > 0);
        }
        // Keep the former variable-storage bound alongside the one fixed
        // constructor envelope; every total-byte transition is still checked.
        Assert.InRange(peak - Utf8Regex.ProgramFixedMetadataBytes, 48ul, 32768ul);
        // Every charged extent is a multiple of 16; test both sides of each
        // transition, including the exact peak and the byte immediately below.
        for (ulong limit = 0; limit <= peak; limit += 16)
        {
            foreach (var cutoff in new[] { limit, limit == 0 ? 0 : limit - 1 })
            {
                using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, cutoff);
                Utf8Regex? regex = null;
                var failure = Record.Exception(() => regex = new Utf8Regex(pattern, options));
                Assert.True(scope.Budget.PeakBytes <= cutoff);
                if (failure is null)
                {
                    Assert.NotNull(regex);
                    Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
                    Assert.Equal(count, regex.Count(input));
                    Assert.True(scope.Budget.ReservedBytes > 0);
                }
                else
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                    Assert.Equal(cutoff, breach.Limit);
                    Assert.True(breach.Requested > cutoff);
                    Assert.Equal(0ul, scope.Budget.Reserved);
                    Assert.Equal(0ul, scope.Budget.ReservedBytes);
                }
            }
        }
        Assert.False(Utf8ConstructionScope.IsActive);
        Assert.Equal(count, new Utf8Regex(pattern, options).Count(input));
    }

    [Theory]
    [InlineData("(?=a)(?=(?<=b)c)d", false)]
    [InlineData("(?:(?<x>a?)*)*b", true)]
    [InlineData("(?<x>[abc]\\p{L})\\k<x>", false)]
    [InlineData("(?<x>[a-z-[aeiou]])", false)]
    [InlineData("(?<x>[😀-🙏]|[é-\uFFFF])", false)]
    public void RetainedMajorArraysFitTheLoweringAdmission(string pattern, bool nonBacktracking)
    {
        // Lowering borrows the catalog from the already closed parser owner.
        // Catalog acquisition has its own byte/work/depth cutoff controls.
        var options = nonBacktracking ? RegexOptions.NonBacktracking : RegexOptions.None;
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity);
        var program = Utf8OwnedTreeSliceLowerer.TryLower(tree, options);
        Assert.NotNull(program);
        var retained = Utf8ScalarIrAdmissionTests.CountOwnedGraphArrays(program);

        Assert.True(scope.Budget.ReservedBytes >= retained + 128ul);
        Assert.True(scope.Budget.PeakBytes > retained);
    }

    [Fact]
    public void NestedConstructionCannotResetByteCapacityAndFailedChildRecovers()
    {
        const string pattern = "(?<x>a?)*b";
        ulong peak, recoveryPeak;
        using (var measured = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity))
        {
            Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.NonBacktracking));
            peak = measured.Budget.PeakBytes;
        }
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Assert.NotNull(Utf8SliceParser.TryParse("", RegexOptions.None));
            recoveryPeak = measured.Budget.PeakBytes;
        }
        // Fresh reference-keyed graphs and capacity-clamped growth need not
        // repeat an unlimited run's peak. Fund the first graph with headroom,
        // then retain an explicit live blocker to test aggregate child denial.
        var cap = 2 * Math.Max(peak, recoveryPeak);
        using var parent = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity, cap);
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, RegexOptions.NonBacktracking));
        var headroom = recoveryPeak + 512;
        Assert.True(headroom < parent.Budget.ReservedBytes);
        var payload = checked((int)((cap - parent.Budget.ReservedBytes - headroom - 32) & ~15ul));
        var blocker = parent.Budget.AllocateArray<byte>(payload, false);
        var held = parent.Budget.ReservedBytes;
        var heldSlots = parent.Budget.Reserved;
        using (var child = Utf8ConstructionScope.Enter())
        {
            Assert.Same(parent.Budget, child.Budget);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                Utf8SliceParser.TryParse(pattern, RegexOptions.NonBacktracking));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(cap, failure.Limit);
            Assert.Equal(held, parent.Budget.ReservedBytes);
            Assert.Equal(heldSlots, parent.Budget.Reserved);
        }
        Assert.Equal(held, parent.Budget.ReservedBytes);
        Assert.NotNull(Utf8SliceParser.TryParse("", RegexOptions.None));
        GC.KeepAlive(blocker);
        Assert.True(parent.Budget.PeakBytes <= cap);
    }

    [Theory]
    [InlineData(1)] [InlineData(17)] [InlineData(128)]
    public void ArrayChargeCoversMeasuredManagedArrayAllocation(int count)
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, ulong.MaxValue);
        // Warm generic instantiations before measuring only the acquired array.
        var warm = budget.AllocateArray<Utf8SliceInstruction>(count, false);
        budget.ReleaseArray(warm, false);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var array = budget.AllocateArray<Utf8SliceInstruction>(count, false);
        var allocated = (ulong)(GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(allocated <= budget.ReservedBytes);
        Assert.True(allocated > 0);
        budget.ReleaseArray(array, false);
        Assert.Equal(0ul, budget.ReservedBytes);
    }

    [Fact]
    public void DeniedArrayAcquisitionCannotLeaveAnInstructionLoan()
    {
        var budget = new Utf8ConstructionBudget(ulong.MaxValue, 1);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            budget.AllocateArray<Utf8SliceInstruction>(int.MaxValue, true));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0ul, budget.Reserved);
        Assert.Equal(0ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.Peak);
        Assert.Equal(0ul, budget.PeakBytes);
    }
}
