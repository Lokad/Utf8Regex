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
        Assert.Equal(32, fields.Length);
        Assert.InRange(Unsafe.SizeOf<Utf8SearchPlan>(), 1, 4096);
        var retired = new[] { "FallbackSearch", "NativeCandidateSource", "FallbackCandidateSource", "ConfirmationPlan",
            "CountOperation", "FirstMatchOperation", "EnumerationOperation" };
        Assert.DoesNotContain(fields, f => retired.Any(name => f.Name == $"<{name}>k__BackingField"));
        Assert.Single(fields.Where(f => f.Name == "_fallbackCandidatePlans"));
        Assert.True(typeof(Utf8SearchStrategySelector).GetMethod("Create")!.GetParameters()[0].ParameterType.IsByRef);
    }

    [Fact]
    public void PrefilterAndFallbackViewsBorrowAllSourcesWithZeroCapacity()
    {
        var primary = new PreparedSearcher(new PreparedSubstringSearch("a"u8.ToArray(), false), false);
        var secondary = new PreparedSearcher(new PreparedSubstringSearch("z"u8.ToArray(), false), false);
        var structural = Utf8StructuralSearchPlan.CreateStartPlan(primary);
        var windows = new[] { structural };
        var candidates = new[] { structural, structural };
        using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
        foreach (var p in new[] { false, true })
        foreach (var s in new[] { false, true })
        foreach (var w in new[] { false, true })
        foreach (var c in new[] { false, true })
        {
            var filter = new Utf8PrefilterPlan(p ? primary : default, s ? secondary : default, w ? windows : null);
            var fallback = new Utf8FallbackSearchPlan(filter, c ? candidates : null);
            Assert.Equal(p || s || w, filter.HasValue); Assert.Equal(filter.HasValue, fallback.HasRequiredPrefilter);
            Assert.Equal(p, filter.PrimarySource.HasValue); Assert.Equal(s, filter.SecondarySource.HasValue);
            Assert.Equal(w, filter.WindowSource.HasValue); Assert.Equal(c, fallback.CandidateSource.HasValue);
            Assert.Same(w ? windows : null, fallback.RequiredWindowPrefilterPlans);
            Assert.Same(c ? candidates : null, fallback.CandidateSource.StructuralSearchPlans);
            Assert.Equal(c ? Utf8SearchSemantics.CandidateScan with { RequiresConfirmation = true } : default,
                fallback.CandidateSource.Semantics);
        }
        Assert.Equal(0ul, zero.Budget.WorkSteps); Assert.Equal(0ul, zero.Budget.ReservedBytes);
        Assert.Equal(0ul, zero.Budget.Depth);
    }

    [Fact]
    public void DerivedDispositionsKeepOperationChoicesAndCanonicalReferencesWithoutRuntimeAdmission()
    {
        var patterns = new[] { "", "a", "é|😀|中", @"\b(?:Task|ValueTask|IAsyncEnumerable)\b", "a.*b",
            "Sherlock Holmes|John Watson|Irene Adler|Inspector Lestrade|Professor Moriarty", "[0-9]{2}x",
            "ab(?=cd)", @"\w+!", "^😀.*é$", "(?i)abababababcz" };
        foreach (var pattern in patterns)
        {
            var plan = Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant).SearchPlan;
            using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
            foreach (var request in new[] { Utf8SearchSemantics.CountMatches, Utf8SearchSemantics.FirstMatch,
                Utf8SearchSemantics.EnumerateMatches, new(Utf8SearchOverlapPolicy.Overlapping, Utf8SearchExitPolicy.First, true, true) })
            {
                var projection = request.RequiresProjection ? plan.ProjectionPlan : default;
                var operation = Utf8SearchStrategySelector.Create(plan, request, plan.ConfirmationPlan, projection);
                var native = plan.NativeCandidateSource; var fallback = plan.FallbackCandidateSource;
                var confirm = native.HasValue ? plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement : fallback.HasValue;
                var expected = !native.HasValue && !fallback.HasValue ? Utf8SearchOperationKind.None :
                    !native.HasValue ? plan.HasRequiredPrefilter ? Utf8SearchOperationKind.PrefilterThenConfirm : Utf8SearchOperationKind.SearchThenConfirm :
                    plan.HasRequiredPrefilter ? confirm ? Utf8SearchOperationKind.PrefilterThenConfirm : Utf8SearchOperationKind.PrefilterThenSearch :
                    confirm ? Utf8SearchOperationKind.SearchThenConfirm :
                    plan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton ? Utf8SearchOperationKind.HybridSearch : Utf8SearchOperationKind.DirectSearch;
                Assert.Equal(expected, operation.Kind);
                if (operation.HasValue)
                {
                    Assert.Equal(request with { RequiresConfirmation = confirm }, operation.Semantics);
                    var selected = native.HasValue ? native : fallback;
                    Assert.Equal(selected.Kind, operation.CandidateSource.Kind);
                    Assert.Same(selected.StructuralSearchPlans, operation.CandidateSource.StructuralSearchPlans);
                    var preferred = plan.PrefilterPlan.PrimarySource.HasValue ? plan.PrefilterPlan.PrimarySource :
                        plan.PrefilterPlan.SecondarySource.HasValue ? plan.PrefilterPlan.SecondarySource : plan.PrefilterPlan.WindowSource;
                    Assert.Equal(preferred.Kind, operation.PrefilterSource.Kind);
                    Assert.Same(preferred.StructuralSearchPlans, operation.PrefilterSource.StructuralSearchPlans);
                }
            }
            Assert.Equal(plan.CountOperation.Kind, Utf8SearchStrategySelector.Create(plan, Utf8SearchSemantics.CountMatches, plan.ConfirmationPlan, default).Kind);
            Assert.Equal(plan.FirstMatchOperation.Kind, Utf8SearchStrategySelector.Create(plan, Utf8SearchSemantics.FirstMatch, plan.ConfirmationPlan, default).Kind);
            Assert.Equal(plan.EnumerationOperation.Kind, Utf8SearchStrategySelector.Create(plan, Utf8SearchSemantics.EnumerateMatches, plan.ConfirmationPlan, plan.ProjectionPlan).Kind);
            Assert.Same(plan.FallbackCandidatePlans, plan.FallbackSearch.CandidatePlans);
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
            Assert.False(plan.FallbackSearch.HasCandidates); Assert.False(plan.NativeCandidateSource.HasValue);
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
