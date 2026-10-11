using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RuntimePreferenceQualificationTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8PreparedRegex BasePlan() => Utf8FrontEnd.Compile("abcd", RegexOptions.CultureInvariant);
    private static Utf8SearchPlan Search(byte[] literal, int distance = 0, int? exact = null,
        Utf8SearchKind kind = Utf8SearchKind.FixedDistanceAsciiLiteral, bool window = false, bool requiredWindow = false) =>
        Utf8SearchPlan.Prepare(Utf8SearchFacts.Create(kind, new Utf8SearchFactData {
            LiteralUtf8 = literal, Distance = distance, ExactRequiredLength = exact,
            OrderedWindowLeadingLiteralsUtf8 = window ? ["a"u8.ToArray()] : [],
            OrderedWindowTrailingLiteralUtf8 = window ? "z"u8.ToArray() : [],
            OrderedWindowMaxGap = window ? 8 : null,
            RequiredWindowPrefilters = requiredWindow ? [Utf8WindowSearchFacts.WithinLines(
                Utf8PreparedSearcherInfo.LiteralFamily(["a"u8.ToArray()]),
                Utf8PreparedSearcherInfo.LiteralFamily(["z"u8.ToArray()]), 1)] : []
        }));
    private static Utf8PreparedRegex WithSearch(Utf8PreparedRegex p, Utf8SearchPlan search,
        Utf8DeterministicByteGuard[]? guards, string? reason = "unsupported_loop",
        NativeExecutionKind kind = NativeExecutionKind.FallbackRegex) =>
        Utf8PreparedRegex.Create(p.SemanticRegex, p.Features, p.ExecutionPattern, kind, p.ExecutionBackend,
            p.CompiledPatternCategory, p.ExecutionTree, p.ExecutionProgram, p.ScalarProgram,
            Utf8OperationMatchCursorPlans.Create(p.ExecutionProgram, search, p.StructuralLinearProgram,
                p.SimplePatternPlan, p.StructuralSearchPlan, p.StructuralVerifier.AsciiProgram),
            p.StructuralSearchPlan, p.DeterministicAnchor, new(guards, null, 0, null, 0, false),
            p.ScalarVerifier, p.StructuralVerifier, p.StructuralIdentifierFamilyPlan, p.StructuralTokenWindowPlan,
            p.StructuralRepeatedSegmentPlan, p.StructuralQuotedRelationPlan, p.OrderedLiteralWindowPlan,
            p.LiteralUtf8, reason, default);
    private static MethodInfo Method(string name)
    {
        var method = typeof(Utf8CompiledRuntimeFactory).GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.NotNull(method); return method;
    }
    private static bool Invoke(MethodInfo method, object[] arguments)
    {
        try { return Assert.IsType<bool>(method.Invoke(null, arguments)); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static bool Policy(Utf8CompiledEngineKind kind, Utf8PreparedRegex plan, Utf8ConstructionBudget budget) =>
        Invoke(Method("ShouldUseDirectScalarRuntime"), [kind, plan, budget]);
    private static bool Outside(Utf8PreparedRegex plan, Utf8ConstructionBudget budget) =>
        Invoke(Method("HasVariableWidthFixedDistanceCandidateOutsidePrefix"), [plan, budget]);
    private static Utf8CompiledEngineRuntime Build(Utf8CompiledEngineKind kind, Utf8PreparedRegex plan, Utf8VerifierRuntime verifier) =>
        Utf8CompiledRuntimeFactory.Create(new(kind), plan, verifier, RegexOptions.None);

    [Fact]
    public void PreferenceHasNoRetainedStateAndRetiredOptionHelperAndBclNameAreAbsent()
    {
        Assert.Empty(typeof(Utf8CompiledRuntimeFactory).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance));
        var methods = typeof(Utf8CompiledRuntimeFactory).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
        Assert.DoesNotContain(methods, m => m.Name is "NormalizeDirectRouteOptions" or "ShouldPreferFallbackRegexRuntime");
        Assert.Equal(3, Method("ShouldUseDirectScalarRuntime").GetParameters().Length);
    }

    [Fact]
    public void RuntimeKindsAndExistingReasonGatesSelectOnlyTheQualifiedCandidates()
    {
        var p = BasePlan(); var search = Search("ab"u8.ToArray());
        foreach (var kind in Enum.GetValues<Utf8CompiledEngineKind>())
        foreach (var reason in new string?[] { null, "unsupported_loop", "unsupported_lookaround" })
        foreach (var native in new[] { NativeExecutionKind.FallbackRegex, NativeExecutionKind.ExactAsciiLiteral })
        {
            var plan = WithSearch(p, search, [], reason, native);
            using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
            var expected = kind is Utf8CompiledEngineKind.ByteSafeLinear or Utf8CompiledEngineKind.CompiledFallback &&
                reason == "unsupported_loop" && native == NativeExecutionKind.FallbackRegex;
            Assert.Equal(expected, Policy(kind, plan, owner.Budget));
            Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.PeakBytes);

        }
    }

    [Fact]
    public void WeakPrefilterLengthsAndBothWindowFormsUseConstantBorrowedQueries()
    {
        var p = BasePlan();
        foreach (var width in Enumerable.Range(0, 5))
        foreach (var shape in Enumerable.Range(0, 3))
        {
            var literal = Encoding.ASCII.GetBytes("abcd"[..width]);
            var search = Search(literal, kind: Utf8SearchKind.ExactAsciiLiteral, window: shape == 1, requiredWindow: shape == 2);
            var plan = WithSearch(p, search, []);
            if (shape == 1) Assert.True(search.HasWindowSearch);
            if (shape == 2) Assert.NotEmpty(search.RequiredWindowPrefilterPlans ?? []);
            using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
            foreach (var kind in new[] { Utf8CompiledEngineKind.ByteSafeLinear, Utf8CompiledEngineKind.CompiledFallback })
                Assert.Equal(width is >= 1 and <= 3 && shape == 0, Policy(kind, plan, owner.Budget));
            Assert.Equal(0ul, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.ReservedBytes);
            Assert.Same(literal.Length == 0 ? null : literal, plan.SearchPlan.LiteralUtf8);
        }
        // The weak-prefilter branch returns before visiting a large guard vector.
        var many = Enumerable.Range(0, 1000).Select(i => new Utf8DeterministicByteGuard(i, (byte)'a')).ToArray();
        var weak = WithSearch(p, Search("ab"u8.ToArray()), many);
        using var zero = new Utf8ConstructionScope(0, 0, 0, 0);
        Assert.True(Policy(Utf8CompiledEngineKind.CompiledFallback, weak, zero.Budget));
        Assert.Equal(0ul, zero.Budget.WorkSteps);
    }

    [Fact]
    public void FixedDistanceCoverageCannotCertifyOffsetsWrappedPastIntMaxValue()
    {
        var p = BasePlan(); var literal = "abcd"u8.ToArray();
        foreach (var distance in new[] { 0, 17, int.MaxValue - 2, int.MaxValue })
        {
            // Deliberately malformed wrapped guards must not certify the positive
            // byte extent. This is an internal admission sentinel, not public input.
            var guards = Enumerable.Range(0, literal.Length).Select(i =>
                new Utf8DeterministicByteGuard(unchecked(distance + i), literal[i])).ToArray();
            var plan = WithSearch(p, Search(literal, distance), guards);
            var certificates = guards.Where(g => g.Literal.HasValue).Select(g => ((long)g.Offset, g.Literal)).ToHashSet();
            var expected = Enumerable.Range(0, literal.Length).Any(i => !certificates.Contains(((long)distance + i, (byte?)literal[i])));
            using var owner = new Utf8ConstructionScope(0, 0, ulong.MaxValue, 0);
            Assert.Equal(expected, Outside(plan, owner.Budget));
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
        }
    }

    [Fact]
    public void CoverageBorrowsNulAndMaskGuardsAndEveryWorkCutoffRestoresParentState()
    {
        var p = BasePlan(); byte[] literal = [0, (byte)'a', (byte)'b', (byte)'c'];
        foreach (var shape in Enumerable.Range(0, 4))
        {
            Utf8DeterministicByteGuard[] guards = shape switch {
                0 => [new(3, (byte)'c'), new(2, (byte)'b'), new(1, (byte)'a'), new(0, (byte)0)],
                1 => [new(0, AsciiCharClass.FromBytes([0])), new(1, (byte)'a'), new(2, (byte)'b'), new(3, (byte)'c')],
                2 => [new(0, (byte)0), new(1, (byte)'a'), new(1, (byte)'a'), new(3, (byte)'c')],
                _ => []
            };
            var original = guards.ToArray(); var plan = WithSearch(p, Search(literal), guards);
            ulong work;
            using (var funded = new Utf8ConstructionScope(0, 0, ulong.MaxValue, 0))
            { Assert.Equal(shape != 0, Outside(plan, funded.Budget)); work = funded.Budget.WorkSteps; }
            for (var allowance = 0ul; allowance <= work; allowance++)
            {
                using var owner = new Utf8ConstructionScope(0, 128, 2 + allowance, 0);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                var checkpoint = owner.Budget.ReservedBytes;
                var error = Record.Exception(() => Assert.Equal(shape != 0, Outside(plan, owner.Budget)));
                if (allowance < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                else Assert.Null(error);
                Assert.Equal(2 + allowance, owner.Budget.WorkSteps); Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes); Assert.Equal(17, parent.Value);
                Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                Assert.Equal(original, guards); Assert.Equal(new byte[] { 0, (byte)'a', (byte)'b', (byte)'c' }, literal);
            }
        }
    }

    [Fact]
    public void FullFactoryPreferenceAndSelectedRootShareEveryAdmissionCutoff()
    {
        var p = BasePlan(); var literal = "abcd"u8.ToArray();
        Utf8DeterministicByteGuard[] covered = [new(0, (byte)'a'), new(1, (byte)'b'), new(2, (byte)'c'), new(3, (byte)'d')];
        foreach (var scenario in Enumerable.Range(0, 4))
        foreach (var kind in new[] { Utf8CompiledEngineKind.ByteSafeLinear, Utf8CompiledEngineKind.CompiledFallback })
        {
            var plan = WithSearch(p, Search(scenario == 0 ? "ab"u8.ToArray() : literal),
                scenario == 2 ? covered : [new(0, (byte)'a')], scenario == 3 ? null : "unsupported_loop");
            var verifier = Utf8VerifierRuntime.Create(plan);
            var direct = scenario == 0 || scenario == 1 && kind == Utf8CompiledEngineKind.CompiledFallback;
            var expectedType = direct ? typeof(Utf8FallbackRegexCompiledEngineRuntime) :
                kind == Utf8CompiledEngineKind.CompiledFallback ? typeof(Utf8CompiledFallbackCompiledEngineRuntime) : typeof(Utf8ByteSafeLinearCompiledEngineRuntime);
            ulong bytes, work, depth, loan;
            using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                _ = funded.Budget.AllocateObject(17, static value => new ParentValue(value));
                var runtime = Build(kind, plan, verifier); Assert.Equal(expectedType, runtime.GetType());
                var flags = BindingFlags.NonPublic | BindingFlags.Instance;
                var planField = runtime.GetType().GetField("_regexPlan", flags); Assert.NotNull(planField);
                var verifierField = runtime.GetType().GetField("_verifierRuntime", flags); Assert.NotNull(verifierField);
                Assert.Same(plan, planField.GetValue(runtime)); Assert.Same(verifier, verifierField.GetValue(runtime));
                bytes = funded.Budget.PeakBytes - 128; work = funded.Budget.WorkSteps - 2;
                depth = funded.Budget.PeakDepth; loan = funded.Budget.ReservedBytes;
            }
            foreach (var resource in new[] { "bytes", "work", "depth" })
            {
                var maximum = resource == "bytes" ? bytes : resource == "work" ? work : depth;
                var successes = 0;
                for (var allowance = 0ul; allowance <= maximum; allowance++)
                {
                    using var owner = new Utf8ConstructionScope(0, resource == "bytes" ? 128 + allowance : ulong.MaxValue,
                        resource == "work" ? 2 + allowance : ulong.MaxValue, resource == "depth" ? allowance : ulong.MaxValue);
                    var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                    var checkpoint = owner.Budget.ReservedBytes;
                    var error = Record.Exception(() => Assert.Equal(expectedType, Build(kind, plan, verifier).GetType()));
                    if (error is null) { successes++; Assert.Equal(loan, owner.Budget.ReservedBytes); }
                    else {
                        var expected = resource == "bytes" ? Utf8ResourceLimitKind.ConstructionBytes :
                            resource == "work" ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionDepth;
                        Assert.Equal(expected, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                        Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                    }
                    Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
                    Assert.True(resource != "bytes" || owner.Budget.PeakBytes <= 128 + allowance);
                    Assert.True(resource != "work" || owner.Budget.WorkSteps <= 2 + allowance);
                    Assert.Equal(literal, "abcd"u8.ToArray()); Assert.Equal((byte)'a', covered[0].Literal);
                }
                Assert.Equal(1, successes);
            }
        }
    }

    [Fact]
    public void LateSelectedChildFailureKeepsConsumedScanWorkAndSameOwnerCanQueryAgain()
    {
        var p = BasePlan(); var plan = WithSearch(p, Search("abcd"u8.ToArray()), [new(0, (byte)'a')]);
        var verifier = Utf8VerifierRuntime.Create(plan); ulong scan;
        using (var funded = new Utf8ConstructionScope(0, 0, ulong.MaxValue, 0))
        { Assert.True(Policy(Utf8CompiledEngineKind.CompiledFallback, plan, funded.Budget)); scan = funded.Budget.WorkSteps; }
        foreach (var resource in new[] { "bytes", "depth" })
        {
            using var owner = new Utf8ConstructionScope(0, resource == "bytes" ? 128ul : ulong.MaxValue, ulong.MaxValue,
                resource == "depth" ? 1ul : ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Build(Utf8CompiledEngineKind.CompiledFallback, plan, verifier));
            Assert.Equal(resource == "bytes" ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(2 + scan, owner.Budget.WorkSteps); Assert.Equal(0ul, owner.Budget.Depth);
            Assert.True(Policy(Utf8CompiledEngineKind.CompiledFallback, plan, owner.Budget));
            Assert.Equal(2 + 2 * scan, owner.Budget.WorkSteps); Assert.Equal(17, parent.Value);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
    }

    [Fact]
    public void PublicWeakPrefilterRoutesKeepScalarCapturesStrictInputAndCancellation()
    {
        const string email = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        const string uri = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        foreach (var item in new[] { (email, "😀 𐐀@é.org tail", 5, 11), (uri, "😀 https://é😀/x?q=𐐀#尾 rest", 5, 27) })
        foreach (var lane in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("(?<value>" + item.Item1 + ")", RegexOptions.CultureInvariant | lane);
            var input = Encoding.UTF8.GetBytes(item.Item2);
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.Equal((item.Item3, item.Item4), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal((item.Item3, item.Item4), (detailed.IndexInBytes, detailed.LengthInBytes));
            var capture = detailed.GetGroup("value");
            Assert.Equal((item.Item3, item.Item4), (capture.IndexInBytes, capture.LengthInBytes));
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Equal(cancelled.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token)).CancellationToken);
            Assert.Equal(1, regex.Count(input));
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
        }
        foreach (var pattern in new[] { email, uri })
        foreach (var text in new[] { "", "admin@example.org", "https://example.org/a?q=x#z", "miss / @", "a@b.co a@c.co" })
        {
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            var owned = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var input = Encoding.ASCII.GetBytes(text);
            Assert.Equal(oracle.Count(text), owned.Count(input)); Assert.Equal(oracle.IsMatch(text), owned.IsMatch(input));
        }
    }
}
