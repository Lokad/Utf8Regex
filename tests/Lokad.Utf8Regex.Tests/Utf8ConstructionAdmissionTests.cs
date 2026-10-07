using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionAdmissionTests
{
    [Theory]
    [InlineData("(?<x>a?)*b", false)] [InlineData("(?<x>a?)*b", true)]
    [InlineData("(?<x>😀){2,4}", false)] [InlineData("(?<x>😀){2,4}", true)]
    public void EveryConstructorCapacityCutoffIsAdmittedOrFailsOwnedAndRecovers(string pattern, bool compiled)
    {
        var options = RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        ulong peak;
        ulong retained;
        var input = pattern.Contains("😀", StringComparison.Ordinal) ? "😀😀😀"u8.ToArray() : "aaab"u8.ToArray();
        int expectedCount;
        using (var measured = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity))
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            peak = measured.Budget.Peak;
            retained = measured.Budget.Reserved;
            expectedCount = regex.Count(input);
        }
        Assert.InRange(peak, 2ul, 1024ul);
        for (ulong limit = 1; limit < peak; limit++)
        {
            using var scope = new Utf8ConstructionScope(limit);
            Utf8Regex? regex = null;
            var failure = Record.Exception(() => regex = new Utf8Regex(pattern, options));
            if (failure is null)
            {
                Assert.NotNull(regex);
                Assert.Equal(expectedCount, regex.Count(input));
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
                Assert.True(scope.Budget.Peak <= limit);
                Assert.Equal(retained, scope.Budget.Reserved);
                continue;
            }
            var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
            Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, breach.ResourceKind);
            Assert.Equal(limit, breach.Limit);
            Assert.True(breach.Requested > limit);
            Assert.True(scope.Budget.Peak <= limit);
            Assert.Equal(0ul, scope.Budget.Reserved);
            Assert.NotNull(Utf8SliceParser.TryParse("", RegexOptions.None));
            Assert.Equal(1ul, scope.Budget.Reserved);
        }
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void HugeConsumingFiniteBoundsFailBeforeExpansion(bool compiled)
    {
        var options = RegexOptions.NonBacktracking | (compiled ? RegexOptions.Compiled : 0);
        using var scope = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex("(?<x>a){1000000000}", options));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionInstructionCapacity, failure.ResourceKind);
        Assert.Equal(Utf8ConstructionScope.DefaultInstructionCapacity, failure.Limit);
        Assert.True(failure.Requested >= 1_000_000_000ul);
        Assert.True(scope.Budget.Peak < 32ul); // Numeric preflight, not one million emitted copies.
        Assert.Equal(0ul, scope.Budget.Reserved);
        Assert.Equal(2, new Utf8Regex("(?<x>){1000000000}", options).Count("😀"u8));
    }

    [Theory]
    [InlineData("(?=a)(?=b)(?=c)", false)]
    [InlineData("(?=(?=a)a)(?<=b)c", false)]
    [InlineData("(?:(?<x>a?)*)*b", true)]
    public void AllAssertionAndLiftedArraysRetainTheirAdmission(string pattern, bool nonBacktracking)
    {
        var options = RegexOptions.CultureInvariant | (nonBacktracking ? RegexOptions.NonBacktracking : 0);
        using var scope = new Utf8ConstructionScope(4096);
        var program = Utf8SliceParser.TryParse(pattern, options);
        Assert.NotNull(program);
        var pending = new Stack<Utf8SliceProgram>();
        pending.Push(program);
        ulong retained = 0;
        while (pending.TryPop(out var graph))
        {
            retained += (ulong)graph.Instructions.Length;
            foreach (var assertion in graph.Assertions) pending.Push(assertion.Body);
        }
        Assert.Equal(retained, scope.Budget.Reserved);
        Assert.True(scope.Budget.Peak > retained); // Old/new temporary overlap is admitted.
    }

    [Fact]
    public void NestedConstructionCannotResetTheParentCapacity()
    {
        ulong peak;
        using (var measured = new Utf8ConstructionScope(Utf8ConstructionScope.DefaultInstructionCapacity))
        {
            Assert.NotNull(Utf8SliceParser.TryParse("(?<x>a?)*b", RegexOptions.NonBacktracking));
            peak = measured.Budget.Peak;
            Assert.True(2 * measured.Budget.Reserved > peak);
        }
        using var parent = new Utf8ConstructionScope(peak);
        Assert.NotNull(Utf8SliceParser.TryParse("(?<x>a?)*b", RegexOptions.NonBacktracking));
        var held = parent.Budget.Reserved;
        Assert.True(held > 0);
        using (var child = Utf8ConstructionScope.Enter())
        {
            Assert.Same(parent.Budget, child.Budget);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                Utf8SliceParser.TryParse("(?<x>a?)*b", RegexOptions.NonBacktracking));
            Assert.Equal(peak, failure.Limit);
            Assert.Equal(held, parent.Budget.Reserved);
        }
        Assert.Equal(held, parent.Budget.Reserved);
    }

    [Fact]
    public void CapacityArithmeticCannotWrapOrCorruptTheOwner()
    {
        var budget = new Utf8ConstructionInstructionBudget(ulong.MaxValue);
        budget.Reserve(ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => budget.Reserve(1));
        Assert.Equal(ulong.MaxValue, failure.Requested);
        Assert.Equal(ulong.MaxValue, budget.Reserved);
        budget.Release(ulong.MaxValue);
        Assert.Equal(0ul, budget.Reserved);
    }

    [Fact]
    public void ConcurrentConstructionOwnersRemainIndependent()
    {
        Parallel.For(0, 32, index =>
        {
            Assert.False(Utf8ConstructionScope.IsActive);
            using (var scope = new Utf8ConstructionScope(index % 2 == 0 ? 2ul : 128ul))
            {
                if (index % 2 == 0)
                {
                    Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex("(?<x>a?)*b", RegexOptions.NonBacktracking));
                    Assert.Equal(0ul, scope.Budget.Reserved);
                }
                else
                {
                    Assert.True(new Utf8Regex("(?<x>a?)*b", RegexOptions.NonBacktracking).IsMatch("aaab"u8));
                    Assert.True(scope.Budget.Reserved > 0);
                }
            }
            Assert.False(Utf8ConstructionScope.IsActive);
        });
    }
}
