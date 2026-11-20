using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SearchDispositionQualificationTests
{
    [Fact]
    public void CanonicalSearchPlanRetiresRepeatedViewsAndSelectorBorrowsThePlan()
    {
        var fields = typeof(Utf8SearchPlan).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.InRange(Unsafe.SizeOf<Utf8SearchPlan>(), 1, 4096);
        var retired = new[] { "ConfirmationPlan", "SearchOperation" };
        Assert.DoesNotContain(fields, f => retired.Any(name => f.Name == $"<{name}>k__BackingField"));
        Assert.True(typeof(Utf8SearchStrategySelector).GetMethod("Create")!.GetParameters()[0].ParameterType.IsByRef);
    }

    [Fact]
    public void DerivedDispositionsKeepStrategyAndConfirmationWithoutRuntimeAdmission()
    {
        var patterns = new[] { "", "a", "é|😀|中", @"\b(?:Task|ValueTask|IAsyncEnumerable)\b", "a.*b",
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", "[0-9]{2}x",
            "ab(?=cd)", @"\w+!", "^😀.*é$", "(?i)abababababcz" };
        foreach (var pattern in patterns)
        {
            var plan = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant).SearchPlan;
            using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
            {
                var operation = plan.SearchOperation;
                var native = plan.HasPreparedSearcher || plan.HasStructuralCandidates;
                var hasWindows = plan.RequiredWindowPrefilterPlans is { Length: > 0 };
                var confirm = plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement;
                var expected = !native && !hasWindows ? Utf8SearchOperationKind.None :
                    !native ? Utf8SearchOperationKind.PrefilterThenConfirm :
                    plan.HasRequiredPrefilter ? confirm ? Utf8SearchOperationKind.PrefilterThenConfirm : Utf8SearchOperationKind.PrefilterThenSearch :
                    confirm ? Utf8SearchOperationKind.SearchThenConfirm :
                    plan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton ? Utf8SearchOperationKind.HybridSearch : Utf8SearchOperationKind.DirectSearch;
                Assert.Equal(expected, operation.Kind);
                if (operation.HasValue)
                {
                    Assert.Equal(plan.ConfirmationPlan, operation.Confirmation);
                }
            }
            Assert.Equal(0ul, zero.Budget.WorkSteps); Assert.Equal(0ul, zero.Budget.Depth); Assert.Equal(0ul, zero.Budget.ReservedBytes);
        }
    }

    [Fact]
    public void DefaultAndPreparedEmptyPlansHaveNoSearchOperation()
    {
        var prepared = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None));
        using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
        var empty = default(Utf8SearchPlan);
        foreach (var plan in new[] { empty, prepared })
        {
            Assert.False(plan.SearchOperation.HasValue); Assert.False(plan.ConfirmationPlan.HasValue);
            Assert.False(plan.HasFallbackCandidates); Assert.False(plan.HasPreparedSearcher);
        }
        Assert.Equal(0ul, zero.Budget.WorkSteps);
    }

    [Fact]
    public void PublicOperationViewsKeepByteRangesScalarAdvancementAndStrictInput()
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.Compiled })
        foreach (var pattern in new[] { "é|😀|中", "(?:é|😀|中)", "[é😀中]" })
        {
            var regex = new Utf8Regex(pattern, options: options | RegexOptions.CultureInvariant);
            var input = "aé😀中z"u8.ToArray(); Assert.Equal(3, regex.Count(input));
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(options.HasFlag(RegexOptions.RightToLeft) ? (7, 3) : (1, 2), (match.Value.StartOffsetInBytes, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes)));
            var enumerator = regex.EnumerateMatches(input); var count = 0;
                while (enumerator.MoveNext()) { count++; Assert.True((enumerator.Current.EndOffsetInBytes - enumerator.Current.StartOffsetInBytes) > 0); }
                Assert.Equal(3, count);
            Assert.Equal(2, new Utf8Regex("", options: options).Count("😀"u8));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token));
        }
    }
}
