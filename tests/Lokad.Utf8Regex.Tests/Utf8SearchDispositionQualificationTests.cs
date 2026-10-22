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
        Assert.Equal(29, fields.Length);
        Assert.InRange(Unsafe.SizeOf<Utf8SearchPlan>(), 1, 4096);
        var retired = new[] { "ConfirmationPlan",
            "CountOperation", "FirstMatchOperation", "EnumerationOperation" };
        Assert.DoesNotContain(fields, f => retired.Any(name => f.Name == $"<{name}>k__BackingField"));
        Assert.Single(fields.Where(f => f.Name == "_fallbackCandidatePlans"));
        Assert.True(typeof(Utf8SearchStrategySelector).GetMethod("Create")!.GetParameters()[0].ParameterType.IsByRef);
    }

    [Fact]
    public void PrefilterViewsBorrowSourcesAndExecuteWithoutConstructionAdmission()
    {
        var primary = new PreparedSearcher(new PreparedSubstringSearch("a"u8.ToArray(), false), false);
        var secondary = new PreparedSearcher(new PreparedSubstringSearch("z"u8.ToArray(), false), false);
        var structural = Utf8StructuralSearchPlan.CreateStartPlan(primary);
        var windows = new[] { structural };
        using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
        foreach (var p in new[] { false, true })
        foreach (var s in new[] { false, true })
        foreach (var w in new[] { false, true })
        {
            var filter = new Utf8PrefilterPlan(p ? primary : default, s ? secondary : default, w ? windows : null);
            Assert.Equal(p || s || w, filter.HasValue);
            Assert.Equal(p, filter.PrimarySource.HasValue); Assert.Equal(s, filter.SecondarySource.HasValue);
            Assert.Equal(w, filter.WindowSource.HasValue);
            Assert.Same(w ? windows : null, filter.WindowSource.StructuralSearchPlans);
            Assert.False(Utf8PrefilterExecutor.Rejects(filter, "az"u8));
            Assert.Equal(filter.HasValue, Utf8PrefilterExecutor.Rejects(filter, "qq"u8));
        }
        Assert.Equal(0ul, zero.Budget.WorkSteps); Assert.Equal(0ul, zero.Budget.ReservedBytes);
        Assert.Equal(0ul, zero.Budget.Depth);
    }

    [Fact]
    public void DerivedDispositionsKeepExecutionChoicesWithoutRuntimeAdmission()
    {
        var patterns = new[] { "", "a", "é|😀|中", @"\b(?:Task|ValueTask|IAsyncEnumerable)\b", "a.*b",
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", "[0-9]{2}x",
            "ab(?=cd)", @"\w+!", "^😀.*é$", "(?i)abababababcz" };
        foreach (var pattern in patterns)
        {
            var plan = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant).SearchPlan;
            using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
            foreach (var projection in new[] { default(Utf8ProjectionPlan), plan.ProjectionPlan })
            {
                var operation = Utf8SearchStrategySelector.Create(plan, plan.ConfirmationPlan, projection);
                var native = plan.HasPreparedSearcher || plan.HasStructuralCandidates;
                var fallback = plan.HasFallbackCandidates;
                var confirm = native ? plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement : fallback;
                var expected = !native && !fallback ? Utf8SearchOperationKind.None :
                    !native ? plan.HasRequiredPrefilter ? Utf8SearchOperationKind.PrefilterThenConfirm : Utf8SearchOperationKind.SearchThenConfirm :
                    plan.HasRequiredPrefilter ? confirm ? Utf8SearchOperationKind.PrefilterThenConfirm : Utf8SearchOperationKind.PrefilterThenSearch :
                    confirm ? Utf8SearchOperationKind.SearchThenConfirm :
                    plan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton ? Utf8SearchOperationKind.HybridSearch : Utf8SearchOperationKind.DirectSearch;
                Assert.Equal(expected, operation.Kind);
                if (operation.HasValue)
                {
                    Assert.Equal(projection, operation.Projection);
                    Assert.Equal(plan.ConfirmationPlan, operation.Confirmation);

                }
            }
            Assert.Equal(plan.CountOperation.Kind, Utf8SearchStrategySelector.Create(plan, plan.ConfirmationPlan, default).Kind);
            Assert.Equal(plan.FirstMatchOperation.Kind, Utf8SearchStrategySelector.Create(plan, plan.ConfirmationPlan, default).Kind);
            Assert.Equal(plan.EnumerationOperation.Kind, Utf8SearchStrategySelector.Create(plan, plan.ConfirmationPlan, plan.ProjectionPlan).Kind);
            Assert.Equal(0ul, zero.Budget.WorkSteps); Assert.Equal(0ul, zero.Budget.Depth); Assert.Equal(0ul, zero.Budget.ReservedBytes);
        }
    }

    [Fact]
    public void DefaultAndPreparedEmptyPlansKeepTheirDistinctProjectionMetadata()
    {
        var prepared = Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(Utf8SearchKind.None));
        using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
        var empty = default(Utf8SearchPlan);
        Assert.Equal(Utf8ProjectionKind.None, empty.ProjectionPlan.Kind);
        Assert.Equal(Utf8ProjectionKind.Utf16BoundaryMap, prepared.ProjectionPlan.Kind);
        foreach (var plan in new[] { empty, prepared })
        {
            Assert.False(plan.CountOperation.HasValue); Assert.False(plan.FirstMatchOperation.HasValue);
            Assert.False(plan.EnumerationOperation.HasValue); Assert.False(plan.ConfirmationPlan.HasValue);
            Assert.False(plan.HasFallbackCandidates); Assert.False(plan.HasPreparedSearcher); Assert.False(plan.HasStructuralCandidates);
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
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
            var input = "aé😀中z"u8.ToArray(); Assert.Equal(3, regex.Count(input));
            var match = regex.MatchDetailed(input);
            Assert.Equal(options.HasFlag(RegexOptions.RightToLeft) ? (7, 3) : (1, 2), (match.IndexInBytes, match.LengthInBytes));
            var enumerator = regex.EnumerateMatches(input); var count = 0;
            while (enumerator.MoveNext()) { count++; Assert.True(enumerator.Current.LengthInBytes > 0); }
            Assert.Equal(3, count);
            Assert.Equal(2, new Utf8Regex("", options).Count("😀"u8));
            Assert.Throws<ArgumentException>(() => regex.IsMatch(input.Concat(new byte[] { 0xff }).ToArray()));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token));
        }
    }
}
