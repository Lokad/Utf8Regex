using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8StructuralLinearProgramKind : byte
{
    None = 0,
    AsciiStructuralFamily = 2,
}

internal readonly struct Utf8StructuralLinearProgram
{
    private Utf8StructuralLinearProgram(
        Utf8StructuralLinearProgramKind kind,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan,
        Utf8SearchPlan searchPlan, Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralVerifierPlan structuralVerifierPlan)
    {
        Kind = kind;
        StructuralIdentifierFamilyPlan = structuralIdentifierFamilyPlan;
        SearchPlan = searchPlan;
        StructuralSearchPlan = structuralSearchPlan;
        StructuralVerifierPlan = structuralVerifierPlan;
    }

    public Utf8StructuralLinearProgramKind Kind { get; }

    public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan { get; }

    public Utf8SearchPlan SearchPlan { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralVerifierPlan StructuralVerifierPlan { get; }

    public bool HasValue => Kind != Utf8StructuralLinearProgramKind.None;

    private static Utf8StructuralLinearProgram ForStructuralFamily(
        AsciiStructuralIdentifierFamilyPlan familyPlan, Utf8SearchPlan searchPlan, Utf8StructuralSearchPlan structuralSearchPlan, Utf8StructuralVerifierPlan verifierPlan) =>
        new(Utf8StructuralLinearProgramKind.AsciiStructuralFamily, familyPlan, searchPlan, structuralSearchPlan, verifierPlan);

    public static Utf8StructuralLinearProgram Create(
        NativeExecutionKind executionKind,
        AsciiStructuralIdentifierFamilyPlan structuralIdentifierFamilyPlan,
        Utf8StructuralVerifierPlan structuralVerifier, Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var selected = executionKind switch
        {
            NativeExecutionKind.AsciiStructuralIdentifierFamily when structuralVerifier.Kind == Utf8StructuralVerifierKind.AsciiStructuralProgram
                => ForStructuralFamily(structuralIdentifierFamilyPlan, searchPlan, structuralSearchPlan, structuralVerifier),
            _ => default,
        };
        scope.Complete();
        return selected;
    }
}

internal abstract class Utf8StructuralLinearRuntime
{
    private readonly Utf8PreparedRegex _regexPlan;

    protected Utf8StructuralLinearRuntime(Utf8PreparedRegex regexPlan)
    {
        _regexPlan = regexPlan;
    }

    protected Utf8PreparedRegex PreparedRegex => _regexPlan;

    protected ref readonly Utf8StructuralLinearProgram Program => ref _regexPlan.StructuralLinearProgram;

    /// <summary>
    /// Evaluates whether the validated complete subject contains a match. All
    /// scanning and verification charges the supplied deadline; false is final.
    /// </summary>
    public abstract bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Counts nonoverlapping matches in the validated complete subject using
    /// scalar global progression and charging the supplied deadline.
    /// </summary>
    public abstract int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Returns the first capture-zero match in the validated complete subject,
    /// or a default range, while charging the deadline.
    /// </summary>
    public abstract Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget);

    /// <summary>
    /// Finds the first match whose byte start is at or after
    /// <paramref name="startIndex"/> in a validated subject. The start must be
    /// an admitted boundary. A false result definitively exhausts the suffix,
    /// output values are then unspecified, and all work charges the deadline.
    /// </summary>
    public abstract bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength);

    public static Utf8StructuralLinearRuntime Create(Utf8PreparedRegex regexPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        // The prepared regex already owns the program. Borrow its storage rather
        // than copying the large program and all its alternative plan payloads.
        // Selected subtypes add no fields to this one-reference object state.
        var result = scope.Budget.AllocateObject<Utf8PreparedRegex, Utf8StructuralLinearRuntime>(regexPlan,
            static value => value.StructuralLinearProgram.Kind switch
        {
            Utf8StructuralLinearProgramKind.AsciiStructuralFamily => new Utf8AsciiStructuralFamilyLinearRuntime(value),
            _ => new Utf8NoStructuralLinearRuntime(value),
        });
        scope.Complete();
        return result;
    }

}

internal sealed class Utf8NoStructuralLinearRuntime : Utf8StructuralLinearRuntime
{
    public Utf8NoStructuralLinearRuntime(Utf8PreparedRegex regexPlan)
        : base(regexPlan)
    {
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return false;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return 0;
    }

    public override Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return default(Utf8CaptureRange);
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        return false;
    }
}

internal sealed class Utf8AsciiStructuralFamilyLinearRuntime : Utf8StructuralLinearRuntime
{
    public Utf8AsciiStructuralFamilyLinearRuntime(Utf8PreparedRegex regexPlan)
        : base(regexPlan)
    {
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        return UsesRightToLeft(PreparedRegex)
            ? Utf8PreparedScalarExecutor.IsMatch(PreparedRegex, input, validation, budget)
            : FindNext(input, verifierRuntime, 0, budget, out _) >= 0;
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralLinearAutomaton);
        return UsesRightToLeft(PreparedRegex)
            ? Utf8PreparedScalarExecutor.Count(PreparedRegex, input, validation, budget)
            : CountForward(input, verifierRuntime, budget);
    }

    public override Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if (UsesRightToLeft(PreparedRegex))
        {
            return Utf8PreparedScalarExecutor.Match(PreparedRegex, input, validation, budget);
        }

        var index = FindNext(input, verifierRuntime, 0, budget, out var matchedLength);
        return index < 0
            ? default(Utf8CaptureRange)
            : new Utf8CaptureRange(index, (index) + (matchedLength));
    }

    public override bool TryFindNext(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        if (UsesRightToLeft(PreparedRegex))
        {
            matchIndex = -1;
            matchedLength = 0;
            return false;
        }

        matchIndex = FindNext(input, verifierRuntime, startIndex, budget, out matchedLength);
        return matchIndex >= 0;
    }

    private int CountForward(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime, Utf8ExecutionDeadline budget)
    {
        if ((!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null) &&
            Program.StructuralVerifierPlan.AsciiProgram.LinearProgram.HasValue && Program.StructuralSearchPlan.HasValue)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var breach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryCount(Program.StructuralSearchPlan,
                Program.StructuralVerifierPlan.AsciiProgram, input, ref invocation, out var boundedCount);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            return boundedCount;
        }
        if (CanUseStatefulSearch())
        {
            var statefulCount = 0;
            var state = CreateScanState(0);
            while (TryFindNextStateful(input, ref state, budget, out _, out _))
            {
                statefulCount++;
            }

            return statefulCount;
        }

        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, verifierRuntime, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                break;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    internal bool CanUseStatefulSearch()
    {
        return Program.SearchPlan.AlternateLiteralSearchData.HasValue &&
            Program.StructuralSearchPlan.HasValue &&
            Program.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start;
    }

    internal Utf8AsciiStructuralFamilyScanState CreateScanState(int startIndex)
    {
        return new Utf8AsciiStructuralFamilyScanState(
            new Utf8StructuralSearchState(new PreparedSearchScanState(startIndex, default), default),
            startIndex);
    }

    internal bool TryFindNextStateful(
        ReadOnlySpan<byte> input, ref Utf8AsciiStructuralFamilyScanState state, Utf8ExecutionDeadline budget, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null) &&
            Program.StructuralVerifierPlan.AsciiProgram.LinearProgram.HasValue && Program.StructuralSearchPlan.HasValue)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var pending = state.SearchState;
            var breach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryFindNext(Program.StructuralSearchPlan,
                Program.StructuralVerifierPlan.AsciiProgram, input, state.MinStartIndex, ref pending, ref invocation,
                out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            state = new Utf8AsciiStructuralFamilyScanState(pending, found ? match.Index + Math.Max(1, match.Length) : state.MinStartIndex);
            matchIndex = found ? match.Index : -1;
            matchedLength = found ? match.Length : 0;
            return found;
        }
        var searchData = Program.SearchPlan.AlternateLiteralSearchData;
        if (!CanUseStatefulSearch() || !searchData.HasValue)
        {
            return false;
        }

        var searchState = state.SearchState;
        while (Program.StructuralSearchPlan.TryFindNextCandidate(input, ref searchState, out var candidate))
        {
            if (candidate.StartIndex < state.MinStartIndex)
            {
                continue;
            }

            budget.Step();
            var prefixLength = candidate.MatchLength;
            if (prefixLength <= 0 &&
                !AsciiSearch.TryGetMatchedLiteralLength(input, candidate.StartIndex, searchData.Value, out prefixLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, candidate.StartIndex, prefixLength, Program.StructuralIdentifierFamilyPlan, out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            matchIndex = candidate.StartIndex;
            state = new Utf8AsciiStructuralFamilyScanState(searchState, candidate.StartIndex + Math.Max(matchedLength, 1));
            return true;
        }

        state = new Utf8AsciiStructuralFamilyScanState(searchState, state.MinStartIndex);
        return false;
    }

    private int FindNext(ReadOnlySpan<byte> input, Utf8VerifierRuntime verifierRuntime, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        if ((!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null) &&
            Program.StructuralVerifierPlan.AsciiProgram.LinearProgram.HasValue && Program.StructuralSearchPlan.HasValue)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var state = new Utf8StructuralSearchState(new PreparedSearchScanState(startIndex, default), default);
            var breach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryFindNext(Program.StructuralSearchPlan,
                Program.StructuralVerifierPlan.AsciiProgram, input, startIndex, ref state, ref invocation, out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            matchedLength = found ? match.Length : 0;
            return found ? match.Index : -1;
        }
        var searchData = Program.SearchPlan.AlternateLiteralSearchData;
        if (!searchData.HasValue)
        {
            return -1;
        }

        if (Program.StructuralSearchPlan.HasValue && Program.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start)
        {
            var state = new Utf8StructuralSearchState(new PreparedSearchScanState(startIndex, default), default);
            while (Program.StructuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate))
            {
                if (candidate.StartIndex < startIndex)
                {
                    continue;
                }

                budget.Step();
                var prefixLength = candidate.MatchLength;
                if (prefixLength <= 0 &&
                    !AsciiSearch.TryGetMatchedLiteralLength(input, candidate.StartIndex, searchData.Value, out prefixLength))
                {
                    continue;
                }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, candidate.StartIndex, prefixLength, Program.StructuralIdentifierFamilyPlan, out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return candidate.StartIndex;
            }
            }

            return -1;
        }

        for (var candidate = Utf8SearchExecutor.FindNext(Program.SearchPlan, input, startIndex);
            candidate >= 0;
            candidate = Utf8SearchExecutor.FindNext(Program.SearchPlan, input, candidate + 1))
        {
            budget.Step();
            if (!AsciiSearch.TryGetMatchedLiteralLength(input, candidate, searchData.Value, out var prefixLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (verifierRuntime.StructuralVerifierRuntime.TryMatch(input, candidate, prefixLength, budget, out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return candidate;
            }
        }

        return -1;
    }

    private static bool UsesRightToLeft(Utf8PreparedRegex regexPlan)
    {
        return (regexPlan.SemanticRegex.Options & RegexOptions.RightToLeft) != 0;
    }
}

internal readonly record struct Utf8AsciiStructuralFamilyScanState(Utf8StructuralSearchState SearchState, int MinStartIndex);
