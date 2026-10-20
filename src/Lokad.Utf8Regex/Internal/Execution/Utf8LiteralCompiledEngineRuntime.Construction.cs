using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8LiteralCompiledEngineRuntime
{
    // One field per acquired runtime field. Borrowed referenced payloads keep
    // their parent's admission; the wrapper has its own conservative charge.
    private readonly record struct ConstructionState(
        Utf8CompiledEngine CompiledEngine, Utf8PreparedRegex RegexPlan, bool UsesRightToLeft,
        Utf8SearchOperationPlan CountProgram,
        Utf8SearchOperationPlan FirstMatchProgram, Utf8EmittedLiteralFamilyCounter? EmittedCounter,
        byte[][]? SmallFamily, PreparedByteSearch FirstBytes,
        PreparedMultiLiteralCandidatePrefilter LeadingPrefilter,
        PreparedMultiLiteralAutomatonSearch LeadingAutomaton, PreparedSmallAsciiLiteralFamilySearch? Primitive,
        PreparedShortAsciiLiteralFamilyCounter ShortCounter, PreparedBmpThreeByteLiteralFamilySearch BmpSearch,
        CompositeTrailingLiteralSearch? CompositeSearch);

    internal static Utf8LiteralCompiledEngineRuntime Create(Utf8CompiledEngine compiledEngine,
        Utf8PreparedRegex regexPlan, bool usesRightToLeft)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var count = regexPlan.SearchPlan.CountOperation;
            var firstMatch = regexPlan.SearchPlan.FirstMatchOperation;
            // Emitted buffers and final managed copies retain their independent
            // construction admission; opaque CLR/JIT metadata stays excluded.
            var emitted = compiledEngine.Backend == Utf8CompiledExecutionBackend.EmittedInstruction &&
                Utf8EmittedLiteralFamilyCounter.TryCreate(regexPlan.SearchPlan, count, firstMatch, out var counter)
                ? counter : null;
            _ = TryCreateSmallAsciiLiteralFamily(regexPlan, usesRightToLeft, out var small, out var firstBytes);
            _ = TryCreateLeadingUtf8SegmentLiteralFamily(regexPlan, usesRightToLeft, out var prefilter, out var automaton);
            _ = TryCreateBmpThreeByteLiteralFamilySearch(regexPlan, usesRightToLeft, out var bmp);
            var primitive = TryCreateSmallAsciiLiteralFamilyPrimitive(regexPlan, usesRightToLeft, out var prepared)
                ? (PreparedSmallAsciiLiteralFamilySearch?)prepared : null;
            _ = TryCreateShortAsciiLiteralFamilyCounter(regexPlan, usesRightToLeft, out var shortCounter);
            var composite = CreateCompositeTrailingLiteralSearch(regexPlan, usesRightToLeft);
            var result = budget.AllocateObject(new ConstructionState(compiledEngine, regexPlan, usesRightToLeft,
                count, firstMatch, emitted, small, firstBytes, prefilter, automaton,
                primitive, shortCounter, bmp, composite), static state => new Utf8LiteralCompiledEngineRuntime(state));
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCreateSmallAsciiLiteralFamily(Utf8PreparedRegex regexPlan, bool usesRightToLeft,
        out byte[][]? literals, out PreparedByteSearch firstBytes)
    {
        literals = null; firstBytes = default;
        if (usesRightToLeft || regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            regexPlan.SearchPlan.HasBoundaryRequirements || regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is not { Length: >= 2 and <= 8 } candidates) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            Span<byte> values = stackalloc byte[8];
            Span<int> order = stackalloc int[8];
            var alreadyOrdered = true;
            for (var index = 0; index < candidates.Length; index++)
            {
                budget.ChargeWork(1);
                var literal = candidates[index];
                if (literal.Length == 0) return false;
                budget.ChargeWork((ulong)literal.Length);
                if (!Utf8InputAnalyzer.IsAscii(literal)) return false;
                budget.ChargeWork((ulong)index);
                if (values[..index].Contains(literal[0])) return false;
                values[index] = literal[0]; order[index] = index;
                if (index > 0 && values[index - 1] > values[index]) alreadyOrdered = false;
            }
            // At most eight entries: paired insertion sorting needs no comparer
            // or candidate vector. Distinct first bytes make its order unique.
            for (var index = 1; index < candidates.Length; index++)
            {
                budget.ChargeWork(1);
                var value = values[index]; var ordinal = order[index]; var position = index;
                while (position > 0)
                {
                    budget.ChargeWork(1);
                    if (values[position - 1] < value) break;
                    budget.ChargeWork(2);
                    values[position] = values[position - 1]; order[position] = order[position - 1]; position--;
                }
                budget.ChargeWork(2); values[position] = value; order[position] = ordinal;
            }
            var sorted = candidates;
            if (!alreadyOrdered)
            {
                sorted = budget.AllocateArray<byte[]>(candidates.Length, false);
                budget.ChargeWork((ulong)candidates.Length);
                for (var index = 0; index < sorted.Length; index++) sorted[index] = candidates[order[index]];
            }
            firstBytes = PreparedByteSearch.Create(values[..candidates.Length]);
            literals = sorted;
            scope.Complete(); return true;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCreateLeadingUtf8SegmentLiteralFamily(Utf8PreparedRegex regexPlan, bool usesRightToLeft,
        out PreparedMultiLiteralCandidatePrefilter prefilter, out PreparedMultiLiteralAutomatonSearch automaton)
    {
        prefilter = default; automaton = default;
        if (usesRightToLeft || regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            regexPlan.SearchPlan.HasBoundaryRequirements || regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 1 } literals) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            foreach (var literal in literals)
            {
                budget.ChargeWork(3); // Length and at most two leading-byte probes.
                if (!TryGetLeadingUtf8SegmentLength(literal, out _)) return false;
            }
            var prepared = PreparedMultiLiteralCandidatePrefilter.CreateLeadingUtf8Segment(literals);
            if (!prepared.HasValue) return false;
            var machine = PreparedMultiLiteralAutomatonSearch.Create(literals);
            prefilter = prepared; automaton = machine;
            scope.Complete(); return true;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCreateBmpThreeByteLiteralFamilySearch(Utf8PreparedRegex regexPlan, bool usesRightToLeft,
        out PreparedBmpThreeByteLiteralFamilySearch search)
    {
        search = default;
        if (usesRightToLeft || regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            regexPlan.SearchPlan.HasBoundaryRequirements || regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals) return false;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            foreach (var literal in literals)
            {
                budget.ChargeWork(1 + (ulong)literal.Length / 3);
                if (!CanUseFusedValidatedBmpThreeByteLiteralCount(literal)) return false;
            }
            if (!PreparedBmpThreeByteLiteralFamilySearch.TryCreate(literals, out var prepared)) return false;
            search = prepared; scope.Complete(); return true;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCreateSmallAsciiLiteralFamilyPrimitive(Utf8PreparedRegex regexPlan, bool usesRightToLeft,
        out PreparedSmallAsciiLiteralFamilySearch primitive)
    {
        primitive = default;
        return !usesRightToLeft && regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            !regexPlan.SearchPlan.HasBoundaryRequirements && !regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is { Length: >= 3 and <= 6 } literals &&
            PreparedMultiLiteralConstruction.GetShortestLength(literals, Utf8ConstructionScope.CurrentBudget) >= 5 &&
            PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out primitive);
    }

    private static bool TryCreateShortAsciiLiteralFamilyCounter(Utf8PreparedRegex regexPlan, bool usesRightToLeft,
        out PreparedShortAsciiLiteralFamilyCounter counter)
    {
        counter = default;
        return !usesRightToLeft && regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            !regexPlan.SearchPlan.HasBoundaryRequirements && !regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is { Length: >= 2 and <= 4 } literals &&
            PreparedShortAsciiLiteralFamilyCounter.TryCreate(literals, out counter);
    }

    private static CompositeTrailingLiteralSearch? CreateCompositeTrailingLiteralSearch(Utf8PreparedRegex regexPlan,
        bool usesRightToLeft)
    {
        if (usesRightToLeft || regexPlan.SearchPlan.HasBoundaryRequirements || regexPlan.ExecutionKind is not
            (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal) ||
            regexPlan.LiteralUtf8 is not { Length: > 0 } leading ||
            regexPlan.SearchPlan.TrailingLiteralUtf8 is not { Length: > 0 } trailing) return null;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var bytes = budget.AllocateArray<byte>(checked(leading.Length + trailing.Length), false);
            budget.ChargeWork((ulong)bytes.Length);
            leading.CopyTo(bytes, 0); trailing.CopyTo(bytes, leading.Length);
            budget.ChargeWork((ulong)bytes.Length + (ulong)leading.Length);
            var state = new CompositeConstructionState(new PreparedSubstringSearch(bytes, false),
                leading.Length, GetLiteralUtf16Length(leading));
            var result = budget.AllocateObject(state, static value => new CompositeTrailingLiteralSearch(
                value.Search, value.MatchLengthInBytes, value.MatchLengthInUtf16));
            scope.Complete(); return result;
        }
        finally { budget.LeaveDepth(); }
    }

    private readonly record struct CompositeConstructionState(PreparedSubstringSearch Search,
        int MatchLengthInBytes, int MatchLengthInUtf16);
}
