using System.Diagnostics.CodeAnalysis;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

internal readonly record struct Pcre2CompileRequest(
    object PatternSource,
    Pcre2CompileOptions Options,
    Utf8Pcre2CompileSettings Settings,
    Utf8Pcre2ExecutionLimits DefaultLimits,
    TimeSpan MatchTimeout)
{


    internal Utf8ConstructionPattern ConstructionPattern => PatternSource is byte[] bytes
        ? new Utf8ConstructionPattern(bytes) : new Utf8ConstructionPattern((string)PatternSource);

    // Explicit diagnostic/interoperability projection; native compilation and
    // successful execution never request it, and do not retain the result.
    internal string Pattern => PatternSource is string text ? text : Encoding.UTF8.GetString((byte[])PatternSource);

    // Derived during compilation, including explicit newline atoms and leading
    // class filtering. Explicit caller starts at LF remain admissible.
    internal bool SkipsCrLfContinuation { get; init; }

    internal bool UsesCrLfNewlines => Settings.Newline is Pcre2NewlineConvention.Crlf or
        Pcre2NewlineConvention.Any or Pcre2NewlineConvention.AnyCrlf;

    // Core preparation uses the same fixed numeric policy. Execution still
    // borrows the PCRE2 invocation and retains its route-specific units/errors.
    internal Utf8ResourcePolicy CoreResourcePolicy => new()
    {
        MaxWorkSteps = DefaultLimits.MatchLimit == 0 ? null : DefaultLimits.MatchLimit,
        MaxDepth = DefaultLimits.DepthLimit == 0 ? null : DefaultLimits.DepthLimit,
        MaxWorkingBytes = DefaultLimits.HeapLimitInBytes == 0 ? null : DefaultLimits.HeapLimitInBytes,
        MaxResultBytes = DefaultLimits.ResultLimitInBytes == 0 ? null : DefaultLimits.ResultLimitInBytes
    };
}

internal static class Pcre2Compiler
{
    internal static Pcre2CompiledProgram Compile(Pcre2CompileRequest request, bool sharedBranchTier)
    {
        Pcre2CompileValidator.ValidatePattern(request.ConstructionPattern, request.Options, request.Settings);
        if (Pcre2LiteralCompiler.Compile(request) is { } literal)
        {
            var literalProgram = Pcre2CompiledProgramOverlay.CreateLiteral(request, literal);
            Pcre2ProgramInvariant.Validate(literalProgram);
            return literalProgram;
        }

        if (Pcre2BacktrackingCompiler.Compile(request) is Pcre2CompiledBacktrackingOutcome backtracking)
        {
            Utf8Regex? core = null;
            if (!backtracking.RequiresProfileOptions &&
                Pcre2CompiledProgramOverlay.CanPrepareCore(request, backtracking.Root))
                _ = TryCreateUtf8Regex(request, sharedBranchTier, out core);
            var foundation = Pcre2CompiledProgramOverlay.CreateBacktracking(
                request, backtracking.Root, backtracking.Program, core);
            Pcre2ProgramInvariant.Validate(foundation);
            return foundation;
        }

        throw new NotSupportedException("The pattern is not supported by the managed PCRE2 grammar.");
    }

    private static bool TryCreateUtf8Regex(Pcre2CompileRequest request, bool sharedBranchTier, out Utf8Regex? regex)
    {
        try
        {
            regex = Utf8Regex.PrepareSharedBranchSearch(request.ConstructionPattern, ToRegexOptions(request.Options), new Utf8RegexPolicy
            {
                MatchTimeout = request.MatchTimeout,
                ResourcePolicy = request.CoreResourcePolicy
            }, sharedBranchTier);
            return true;
        }
        catch (ArgumentException)
        {
            regex = null;
            return false;
        }
        catch (NotSupportedException)
        {
            regex = null;
            return false;
        }
    }

    private static RegexOptions ToRegexOptions(Pcre2CompileOptions options)
    {
        var regexOptions = RegexOptions.None;
        if ((options & Pcre2CompileOptions.Caseless) != 0)
        {
            regexOptions |= RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        }

        if ((options & Pcre2CompileOptions.Multiline) != 0)
        {
            regexOptions |= RegexOptions.Multiline;
        }

        if ((options & Pcre2CompileOptions.DotAll) != 0)
        {
            regexOptions |= RegexOptions.Singleline;
        }

        if ((options & (Pcre2CompileOptions.Extended | Pcre2CompileOptions.ExtendedMore)) != 0)
        {
            regexOptions |= RegexOptions.IgnorePatternWhitespace;
        }

        return regexOptions;
    }
}

internal interface IPcre2DirectProgram
{
    Pcre2DirectProgramKind Kind { get; }
}

internal sealed class Pcre2LiteralDirectProgram : IPcre2DirectProgram
{
    internal Pcre2LiteralDirectProgram(Pcre2LiteralProgram program)
    {
        Program = program;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2Literal;

    internal Pcre2LiteralProgram Program { get; }
}

internal sealed class Pcre2BacktrackingDirectProgram : IPcre2DirectProgram
{
    internal Pcre2BacktrackingDirectProgram(Pcre2BacktrackingProgram program)
    {
        Program = program;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2Backtracking;

    internal Pcre2BacktrackingProgram Program { get; }
}

// R02a: routes slice-shaped patterns through the shared
// primary-library slice runner. The shell keeps the replaced
// backtracking program for non-None match options, which the
// shared runner does not model.
internal sealed class Pcre2SliceDirectProgram : IPcre2DirectProgram
{
    internal Pcre2SliceDirectProgram(
        Utf8SliceProgram sliceProgram,
        Pcre2BacktrackingProgram fallback,
        Pcre2LiteralProgram? booleanLiteral,
        int disjointClassDelimiter)
    {
        SliceProgram = sliceProgram;
        Fallback = fallback;
        BooleanLiteral = booleanLiteral;
        DisjointClassDelimiter = disjointClassDelimiter;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2Slice;

    internal Utf8SliceProgram SliceProgram { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
    internal Pcre2LiteralProgram? BooleanLiteral { get; }
    internal int DisjointClassDelimiter { get; }
}

internal class Pcre2LiteralFamilyDirectProgram : IPcre2DirectProgram
{
    internal Pcre2LiteralFamilyDirectProgram(Utf8Regex regex, Pcre2BacktrackingProgram fallback)
    {
        Regex = regex;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2LiteralFamily;

    internal Utf8Regex Regex { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal sealed class Pcre2FiniteLiteralLanguageDirectProgram : Pcre2LiteralFamilyDirectProgram
{
    internal Pcre2FiniteLiteralLanguageDirectProgram(
        Pcre2FiniteLiteralLanguageCompilation compilation,
        Pcre2BacktrackingProgram fallback)
        : base(compilation.Regex, fallback)
    {
        BoundaryProjection = compilation.BoundaryProjection;
        BooleanSearch = compilation.BooleanSearch;
    }

    internal Pcre2FiniteLiteralBoundaryProjection? BoundaryProjection { get; }

    internal Pcre2FiniteLiteralBooleanSearch BooleanSearch { get; }
}

internal sealed record Pcre2FiniteLiteralLanguageCompilation(
    Utf8Regex Regex,
    Pcre2FiniteLiteralBooleanSearch BooleanSearch,
    Pcre2FiniteLiteralBoundaryProjection? BoundaryProjection);

internal sealed class Pcre2FiniteLiteralBooleanSearch
{
    private readonly byte[][] _alternatives;
    private readonly byte[] _leadingBytes;

    internal Pcre2FiniteLiteralBooleanSearch(byte[][] alternatives)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var minimal = new Utf8ConstructionBuffer<byte[]>(budget);
        foreach (var alternative in alternatives)
        {
            var covered = false;
            foreach (var candidate in alternatives)
            {
                budget.ChargeWork((ulong)Math.Min(candidate.Length, alternative.Length) + 1);
                if (!ReferenceEquals(candidate, alternative) && candidate.Length <= alternative.Length &&
                    alternative.AsSpan().StartsWith(candidate))
                { covered = true; break; }
            }
            if (!covered)
            {
                minimal.Add(alternative);
            }
        }

        _alternatives = minimal.Take();
        Span<byte> leading = stackalloc byte[64];
        var count = 0;
        foreach (var alternative in _alternatives)
        {
            budget.ChargeWork(1);
            if (!leading[..count].Contains(alternative[0])) leading[count++] = alternative[0];
        }
        _leadingBytes = budget.AllocateArray<byte>(count, false);
        leading[..count].CopyTo(_leadingBytes);
        construction.Complete();
    }

    internal bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_alternatives.Length == 1)
        {
            return input[startOffsetInBytes..].IndexOf(_alternatives[0]) >= 0;
        }

        // Boolean existence does not require the earliest alternative. Scan
        // candidate leading bytes directly and verify only at those positions.
        var remaining = input[startOffsetInBytes..];
        while (!remaining.IsEmpty)
        {
            var candidate = _leadingBytes.Length switch
            {
                1 => remaining.IndexOf(_leadingBytes[0]),
                2 => remaining.IndexOfAny(_leadingBytes[0], _leadingBytes[1]),
                3 => remaining.IndexOfAny(_leadingBytes[0], _leadingBytes[1], _leadingBytes[2]),
                _ => remaining.IndexOfAny(_leadingBytes),
            };
            if (candidate < 0)
            {
                return false;
            }

            var candidateInput = remaining[candidate..];
            foreach (var alternative in _alternatives)
            {
                if (candidateInput[0] == alternative[0] && candidateInput.StartsWith(alternative))
                {
                    return true;
                }
            }

            remaining = candidateInput[1..];
        }

        return false;
    }
}

internal sealed class Pcre2FiniteLiteralBoundaryProjection
{
    private readonly Pcre2FiniteLiteralBoundaryAlternative[] _alternatives;

    internal Pcre2FiniteLiteralBoundaryProjection(Pcre2FiniteLiteralBoundaryAlternative[] alternatives)
    {
        _alternatives = alternatives;
    }

    internal int ProjectStartOffset(ReadOnlySpan<byte> input, int consumedStart, int consumedEnd)
    {
        var consumedValue = input[consumedStart..consumedEnd];
        foreach (var alternative in _alternatives)
        {
            if (consumedValue.SequenceEqual(alternative.Value))
            {
                return consumedStart + alternative.ReportedStartOffsetInBytes;
            }
        }

        throw new InvalidOperationException("A finite literal match must belong to its compiled language.");
    }
}

internal readonly record struct Pcre2FiniteLiteralBoundaryAlternative(
    byte[] Value,
    int ReportedStartOffsetInBytes);

internal enum Pcre2DirectProgramKind : byte
{
    Pcre2Literal = 3,
    Pcre2Backtracking = 5,
    Pcre2LiteralFamily = 6,
    Pcre2SameStartLiteral = 12,
    Pcre2Slice = 19,
}

internal readonly record struct Pcre2OperationPrograms(
    IPcre2DirectProgram IsMatch,
    IPcre2DirectProgram Count,
    IPcre2DirectProgram Enumerate,
    IPcre2DirectProgram Match,
    IPcre2DirectProgram Replace);

internal enum Pcre2CandidateSearchKind : byte
{
    None = 0,
    BranchLeadingLiterals = 1,
    LeadingRunThenLiteral = 2,
    LeadingAsciiSet = 3,
    LeadingAsciiSetWithWindow = 4,
    BoundedLiteralWindow = 5,
    LeadingAsciiWordBoundaryRun = 6,
    LeadingAsciiWordRun = 7,
}

internal readonly record struct Pcre2CandidateWindowConstraint(
    byte[] Literal,
    int MinimumOffset,
    int MaximumOffset)
{
    internal bool HasValue => Literal is { Length: > 0 };
}

internal readonly struct Pcre2CandidateSearchProgram
{
    private Pcre2CandidateSearchProgram(
        Pcre2CandidateSearchKind kind,
        PreparedSearcher searcher,
        Pcre2CharacterToken[] leadingRunTokens,
        Pcre2CandidateWindowConstraint windowConstraint,
        int minimumWordRunLength,
        int maximumBranchPrefixLength = 0,
        bool requiresLeadingWordBoundary = false)
    {
        Kind = kind;
        RequiresLeadingWordBoundary = requiresLeadingWordBoundary;
        Searcher = searcher;
        LeadingRunTokens = leadingRunTokens;
        WindowConstraint = windowConstraint;
        MinimumWordRunLength = minimumWordRunLength;
        MaximumBranchPrefixLength = maximumBranchPrefixLength;
    }

    internal Pcre2CandidateSearchKind Kind { get; }
    internal bool RequiresLeadingWordBoundary { get; }

    // Keep searches on the immutable payload instead of returning it by value.
    internal readonly PreparedSearcher Searcher;

    internal Pcre2CharacterToken[] LeadingRunTokens { get; }

    internal Pcre2CandidateWindowConstraint WindowConstraint { get; }

    internal int MinimumWordRunLength { get; }
    internal int MaximumBranchPrefixLength { get; }

    internal bool HasValue => Kind != Pcre2CandidateSearchKind.None;

    internal static Pcre2CandidateSearchProgram FromBranchLeadingLiterals(PreparedSearcher searcher, int maximumPrefixLength) =>
        new(
            Pcre2CandidateSearchKind.BranchLeadingLiterals,
            searcher,
            [],
            default,
            0,
            maximumPrefixLength);

    internal static Pcre2CandidateSearchProgram FromLeadingRunThenLiteral(
        Pcre2CharacterToken[] leadingRunTokens,
        byte[] literal) =>
        new(
            Pcre2CandidateSearchKind.LeadingRunThenLiteral,
            new PreparedSearcher(new PreparedSubstringSearch(literal, ignoreCase: false), ignoreCase: false),
            leadingRunTokens,
            default,
            0);

    internal static Pcre2CandidateSearchProgram FromLeadingAsciiSet(
        byte[] values,
        Pcre2CandidateWindowConstraint windowConstraint) =>
        new(
            windowConstraint.HasValue
                ? Pcre2CandidateSearchKind.LeadingAsciiSetWithWindow
                : Pcre2CandidateSearchKind.LeadingAsciiSet,
            new PreparedSearcher(PreparedByteSearch.Create(values)),
            [],
            windowConstraint,
            0);

    internal static Pcre2CandidateSearchProgram FromBoundedLiteralWindow(
        Pcre2CandidateWindowConstraint windowConstraint, bool requiresLeadingWordBoundary) =>
        new(
            Pcre2CandidateSearchKind.BoundedLiteralWindow,
            new PreparedSearcher(
                new PreparedSubstringSearch(windowConstraint.Literal, ignoreCase: false),
                ignoreCase: false),
            [],
            windowConstraint,
            0,
            requiresLeadingWordBoundary: requiresLeadingWordBoundary);

    internal static Pcre2CandidateSearchProgram FromLeadingAsciiWordRun(int minimumWordRunLength, bool requiresBoundary) =>
        new(
            requiresBoundary ? Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun : Pcre2CandidateSearchKind.LeadingAsciiWordRun,
            default,
            [],
            default,
            minimumWordRunLength);
}

internal sealed class Pcre2CandidateSearchPlan
{
    private readonly Pcre2CandidateSearchProgram _program;

    internal Pcre2CandidateSearchPlan(Pcre2CandidateSearchProgram program)
    {
        _program = program;
    }

    internal ref readonly Pcre2CandidateSearchProgram Program => ref _program;
}

internal sealed class Pcre2CompiledProgram
{
    internal Pcre2CompiledProgram(
        Pcre2CompileRequest request,
        Pcre2OperationPrograms operations,
        Pcre2CandidateSearchProgram candidateSearch,
        Pcre2PartialProbeProgram partialProbe,
        IPcre2BacktrackingNode? probeRoot,
        Pcre2NameEntry[] nameEntries,
        int captureGroupCount)
    {
        Request = request;
        Operations = operations;
        CandidateSearchPlan = new Pcre2CandidateSearchPlan(candidateSearch);
        PartialProbe = partialProbe;
        ProbeRoot = probeRoot;
        // All producers transfer their internal immutable table. Public views
        // expose entries by copying, so the VM and compiled root can share it.
        NameEntries = nameEntries;
        CaptureGroupCount = captureGroupCount;
        var budget = Utf8ConstructionScope.CurrentBudget;
        NameEntriesUtf8 = budget.AllocateArray<byte[]>(nameEntries.Length, false);
        for (var index = 0; index < nameEntries.Length; index++)
        {
            budget.ChargeWork(1);
            NameEntriesUtf8[index] = budget.EncodeUtf8(nameEntries[index].Name.AsSpan());
        }
    }

    internal Pcre2CompileRequest Request { get; }

    internal Pcre2OperationPrograms Operations { get; }

    internal ref readonly Pcre2CandidateSearchProgram CandidateSearch => ref CandidateSearchPlan.Program;

    internal Pcre2CandidateSearchPlan CandidateSearchPlan { get; }

    internal Pcre2PartialProbeProgram PartialProbe { get; }

    // Only curated partial probing can consult the original backtracking tree.
    // Ordinary execution owns lowered programs, including its option fallback.
    internal IPcre2BacktrackingNode? ProbeRoot { get; }

    // Grammar metadata survives lowering even when captures are absent or
    // Match selects a specialized operation instead of the backtracking VM.

    internal Pcre2NameEntry[] NameEntries { get; }

    internal int CaptureGroupCount { get; }

    internal byte[][] NameEntriesUtf8 { get; }
}

internal static class Pcre2CompiledProgramOverlay
{
    internal static Pcre2CompiledProgram CreateLiteral(
        Pcre2CompileRequest request,
        Pcre2LiteralProgram literalProgram)
    {
        request = request with
        {
            SkipsCrLfContinuation = request.UsesCrLfNewlines && !literalProgram.HasExplicitCrOrLf,

        };
        var direct = new Pcre2LiteralDirectProgram(literalProgram);
        return new Pcre2CompiledProgram(
            request,
            new Pcre2OperationPrograms(direct, direct, direct, direct, direct),
            default,
            Pcre2PartialProbeProgram.None,
            null,
            [],
            0);
    }

    internal static Pcre2CompiledProgram CreateBacktracking(
        Pcre2CompileRequest request,
        IPcre2BacktrackingNode root,
        Pcre2BacktrackingProgram backtrackingProgram,
        Utf8Regex? preparedCore)
    {
        request = backtrackingProgram.Request;
        var direct = new Pcre2BacktrackingDirectProgram(backtrackingProgram);
        var operations = new Pcre2OperationPrograms(direct, direct, direct, direct, direct);
        if (Pcre2SameStartLiteralAnalyzer.TryCompile(
                     root,
                     request,
                     backtrackingProgram) is { } sameStartLiteral)
        {
            operations = operations with
            {
                Count = sameStartLiteral,
                Enumerate = sameStartLiteral,
            };
        }
        else if (preparedCore is { } primary &&
            primary.Inspection.CompiledEngineKind == Utf8CompiledEngineKind.LiteralFamily &&
            Pcre2LiteralFamilyAnalyzer.CanReuseCoreExecution(
                root,
                request,
                primary.ByteOffsetExecution.SearchPortfolioKind))
        {
            var literalFamily = new Pcre2LiteralFamilyDirectProgram(primary, backtrackingProgram);
            operations = operations with
            {
                IsMatch = literalFamily,
                Count = literalFamily,
                Enumerate = literalFamily,
            };
        }
        else if (Pcre2FiniteLiteralLanguageAnalyzer.TryCompile(
                     root,
                     request) is { } finiteLiteralCompilation)
        {
            var literalFamily = new Pcre2FiniteLiteralLanguageDirectProgram(
                finiteLiteralCompilation,
                backtrackingProgram);
            // Boolean and capture-independent global operations need only the
            // expanded language; capture-dependent operations retain the VM.
            operations = operations with
            {
                IsMatch = literalFamily,
                Count = literalFamily,
                Enumerate = literalFamily,
            };
        }

        var candidateSearch = Pcre2CandidateSearchAnalyzer.Compile(root, request);
        var partialProbe = Pcre2PartialProbeCompiler.Compile(root, request);
        operations = WithSlice(request, root, backtrackingProgram, preparedCore, operations, candidateSearch.Kind);
        if (operations.Count is Pcre2BacktrackingDirectProgram &&
            Pcre2LiteralCompiler.CompileCaselessLiteral(root, request) is { } caselessLiteral)
        {
            var literal = new Pcre2LiteralDirectProgram(caselessLiteral);
            operations = operations with { Count = literal, Enumerate = literal };
        }
        return new Pcre2CompiledProgram(
            request,
            operations,
            candidateSearch,
            partialProbe,
            partialProbe.Kind == Pcre2PartialProbeProgramKind.None ? null : root,
            backtrackingProgram.NameEntries,
            backtrackingProgram.CaptureSlotCount - 1);
    }

    // Shared slice eligibility preserves PCRE2 option, class, capture and
    // lookbehind semantics. All five operations may own the same slice payload;
    // nondefault match options retain their owned PCRE2 fallback.
    internal static bool CanPrepareCore(Pcre2CompileRequest request, IPcre2BacktrackingNode root)
        => ((request.Options & SliceIncompatibleOptions) == 0 &&
            request.Settings.Newline is Pcre2NewlineConvention.Default or Pcre2NewlineConvention.Lf) ||
            Pcre2LiteralFamilyAnalyzer.HasCompatibleSyntax(root, request);

    private static Pcre2OperationPrograms WithSlice(Pcre2CompileRequest request,
        IPcre2BacktrackingNode root, Pcre2BacktrackingProgram fallback,
        Utf8Regex? preparedCore, Pcre2OperationPrograms operations, Pcre2CandidateSearchKind candidateKind)
    {
        if (preparedCore is null) return operations;
        var sliceProgram = preparedCore.Inspection.AvailableOwnedSliceProgram;
        // A complete terminal scalar Count ignores capture markers. Other native
        // Core shapes retain the existing fallback-only shared-slice selection.
        var countOnly = preparedCore.Inspection.ExecutionKind != NativeExecutionKind.FallbackRegex;
        if (countOnly && (operations.Count is not Pcre2BacktrackingDirectProgram ||
            !Utf8SliceRunner.CanCountTerminalScalar(sliceProgram))) return operations;
        // PCRE2's multiline caret rejects EOF after a final newline. Core's
        // caret accepts it; nullable programs therefore keep profile execution.
        Utf8ConstructionScope.ChargeWork((ulong)sliceProgram.Instructions.Length);
        if (sliceProgram.CanMatchEmpty && sliceProgram.Instructions.Any(static instruction =>
            instruction.Kind == Utf8SliceInstructionKind.Anchor &&
            instruction.A == (int)Utf8SliceAnchorKind.Beginning && instruction.B != 0))
            return operations;
        if ((request.Options & SliceIncompatibleOptions) != 0 ||
            (request.Settings.Newline is not Pcre2NewlineConvention.Default &&
             request.Settings.Newline is not Pcre2NewlineConvention.Lf) ||
            sliceProgram.RequiresUnicodeWordBoundary ||
            sliceProgram.RequiresUnicodeShorthand ||
            sliceProgram.RequiresUnicodeCategory ||
            sliceProgram.IgnoreCase ||
            sliceProgram.HasNonFixedLookbehind ||
            // PCRE2 executes fixed lookbehind forward. Preserve its owned
            // profile program for dependencies whose .NET backward order differs.
            sliceProgram.HasOrderSensitiveLookbehind ||
            // PCRE2 POSIX classes differ from .NET's literal bracket members.
            // Collating/equivalence spellings also retain their profile programs.
            sliceProgram.UsesLiteralClassBracketSyntax ||
            sliceProgram.HasMixedNamedGroups ||
            // .NET aliases duplicate names; PCRE2 keeps distinct physical slots.
            sliceProgram.HasSharedCaptureDeclarations ||
            // Explicit .NET numbers do not establish PCRE2 positional identities.
            sliceProgram.HasExplicitCaptureNumbers ||
            sliceProgram.HasBalancing ||
            sliceProgram.HasConditionalTest)
        {
            return operations;
        }

        // Keep previously excluded lazy bounded windows on profile backtracking.
        // Retiring the Count shortcut does not qualify shared-slice tail priority.
        if (IsLazyGapWindow(root))
        {
            return operations;
        }

        // Keep the complete admitted language fact, without retaining another
        // Core regex/root. Count can reuse its checked primitive under this
        // profile's original invocation; other operations keep the slice.
        var booleanLiteral = !countOnly && operations.IsMatch is Pcre2BacktrackingDirectProgram
            ? Pcre2LiteralCompiler.CompileImpliedLookahead(root) : null;
        var delimiter = -1;
        if (!countOnly && candidateKind is
            (Pcre2CandidateSearchKind.LeadingRunThenLiteral or Pcre2CandidateSearchKind.LeadingAsciiSet))
        {
            Utf8ConstructionScope.ChargeWork(1);
            delimiter = Pcre2SliceDriver.GetDisjointClassDelimiter(sliceProgram);
        }
        var shared = new Pcre2SliceDirectProgram(sliceProgram, fallback, booleanLiteral, delimiter);
        if (countOnly) return operations with { Count = shared };
        return operations with
        {
            IsMatch = operations.IsMatch is Pcre2BacktrackingDirectProgram ? shared : operations.IsMatch,
            Count = operations.Count is Pcre2BacktrackingDirectProgram ? shared : operations.Count,
            Enumerate = operations.Enumerate is Pcre2BacktrackingDirectProgram ? shared : operations.Enumerate,
            Match = operations.Match is Pcre2BacktrackingDirectProgram ? shared : operations.Match,
            Replace = operations.Replace is Pcre2BacktrackingDirectProgram ? shared : operations.Replace,
        };
    }

    private const Pcre2CompileOptions SliceIncompatibleOptions =
        Pcre2CompileOptions.Caseless |
        // Core's dollar permits a final LF and has no PCRE2 end-only option.
        Pcre2CompileOptions.DollarEndOnly |
        Pcre2CompileOptions.Extended |
        Pcre2CompileOptions.ExtendedMore |
        Pcre2CompileOptions.Anchored |
        Pcre2CompileOptions.EndAnchored |
        Pcre2CompileOptions.Ungreedy |
        Pcre2CompileOptions.NoAutoCapture |
        Pcre2CompileOptions.FirstLine;
    private static bool IsGreedyLiteralWindowBranch(IPcre2BacktrackingNode node)
    {
        if (node is not Pcre2SequenceBacktrackingNode { Children.Length: >= 3 } sequence)
        {
            return false;
        }

        var repeatIndex = -1;
        for (var i = 0; i < sequence.Children.Length; i++)
        {
            if (sequence.Children[i] is Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.None,
                        Literal: var literal,
                    },
                } &&
                literal.IsAscii &&
                literal.Value != '\n')
            {
                continue;
            }

            if (repeatIndex < 0 &&
                i > 0 &&
                i < sequence.Children.Length - 1 &&
                sequence.Children[i] is Pcre2RepeatBacktrackingNode
                {
                    Minimum: var minimum,
                    Maximum: var maximum,
                    Preference: Pcre2RepeatPreference.Greedy,
                    Body: Pcre2TokenBacktrackingNode
                    {
                        Token:
                        {
                            Kind: Pcre2CharacterTokenKind.Any,
                            Options: Pcre2CharacterOptions.None,
                        },
                    },
                } &&
                minimum >= 0 &&
                maximum >= minimum &&
                maximum != int.MaxValue)
            {
                repeatIndex = i;
                continue;
            }

            return false;
        }

        return repeatIndex > 0 && repeatIndex < sequence.Children.Length - 1;
    }

    private static bool IsLazyGapWindow(IPcre2BacktrackingNode node)
    {
        if (node is not Pcre2AlternationBacktrackingNode { Alternatives.Length: 2 } alternation)
        {
            return false;
        }

        var sawLazy = false;
        foreach (var branch in alternation.Alternatives)
        {
            if (IsLazyLiteralWindowBranch(branch))
            {
                sawLazy = true;
                continue;
            }

            if (!IsGreedyLiteralWindowBranch(branch))
            {
                return false;
            }
        }

        return sawLazy;
    }

    private static bool IsLazyLiteralWindowBranch(IPcre2BacktrackingNode node)
    {
        if (node is not Pcre2SequenceBacktrackingNode { Children.Length: >= 3 } sequence)
        {
            return false;
        }

        var repeatIndex = -1;
        for (var i = 0; i < sequence.Children.Length; i++)
        {
            if (sequence.Children[i] is Pcre2TokenBacktrackingNode
                {
                    Token:
                    {
                        Kind: Pcre2CharacterTokenKind.Literal,
                        Options: Pcre2CharacterOptions.None,
                        Literal: var literal,
                    },
                } &&
                literal.IsAscii &&
                literal.Value != '\n')
            {
                continue;
            }

            if (repeatIndex < 0 &&
                i > 0 &&
                i < sequence.Children.Length - 1 &&
                sequence.Children[i] is Pcre2RepeatBacktrackingNode
                {
                    Minimum: var minimum,
                    Maximum: var maximum,
                    Preference: Pcre2RepeatPreference.Lazy,
                    Body: Pcre2TokenBacktrackingNode
                    {
                        Token:
                        {
                            Kind: Pcre2CharacterTokenKind.Any,
                            Options: Pcre2CharacterOptions.None,
                        },
                    },
                } &&
                minimum >= 0 &&
                maximum != int.MaxValue &&
                maximum > minimum)
            {
                repeatIndex = i;
                continue;
            }

            return false;
        }

        return repeatIndex > 0 && repeatIndex < sequence.Children.Length - 1;
    }
}

internal static class Pcre2ProgramInvariant
{
    internal static void Validate(Pcre2CompiledProgram program)
    {
        ValidateDirectProgram(program.Operations.IsMatch, program);
        ValidateDirectProgram(program.Operations.Count, program);
        ValidateDirectProgram(program.Operations.Enumerate, program);
        ValidateDirectProgram(program.Operations.Match, program);
        ValidateDirectProgram(program.Operations.Replace, program);
    }

    private static bool IsOwnedIsMatchFallback(
        Pcre2CompiledProgram program,
        Pcre2BacktrackingProgram fallback)
    {
        return (program.Operations.IsMatch is Pcre2BacktrackingDirectProgram backtracking &&
            ReferenceEquals(fallback, backtracking.Program)) ||
            (program.Operations.IsMatch is Pcre2SliceDirectProgram slice &&
            ReferenceEquals(fallback, slice.Fallback));
    }

    private static bool IsOwnedMatchFallback(
        Pcre2CompiledProgram program,
        Pcre2BacktrackingProgram fallback)
    {
        return (program.Operations.Match is Pcre2BacktrackingDirectProgram backtracking &&
            ReferenceEquals(fallback, backtracking.Program)) ||
            (program.Operations.Match is Pcre2SliceDirectProgram slice &&
            ReferenceEquals(fallback, slice.Fallback));
    }

    private static void ValidateDirectProgram(IPcre2DirectProgram directProgram, Pcre2CompiledProgram program)
    {
        if (directProgram is Pcre2SliceDirectProgram sliceProgram)
        {
            var shared = program.Operations.Match is Pcre2SliceDirectProgram canonical &&
                ReferenceEquals(canonical.SliceProgram, sliceProgram.SliceProgram) &&
                ReferenceEquals(canonical.Fallback, sliceProgram.Fallback);
            var countOnly = ReferenceEquals(directProgram, program.Operations.Count) &&
                IsOwnedMatchFallback(program, sliceProgram.Fallback) &&
                Utf8SliceRunner.CanCountTerminalScalar(sliceProgram.SliceProgram);
            if (!shared && !countOnly)
                throw new InvalidOperationException("Slice operations must share their payload, or own a proved Count payload and canonical PCRE2 fallback.");
        }

        if (directProgram is Pcre2SameStartLiteralDirectProgram sameStartLiteralProgram &&
            !IsOwnedIsMatchFallback(program, sameStartLiteralProgram.Fallback))
        {
            throw new InvalidOperationException("A same-start literal backend must be owned by its compiled PCRE2 program.");
        }
    }
}

internal static class Pcre2LiteralFamilyAnalyzer
{
    internal static bool CanReuseCoreExecution(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Utf8SearchPortfolioKind portfolioKind)
    {
        return HasCompatibleSyntax(root, request) && IsExactLiteralFamilyPortfolio(portfolioKind);
    }

    internal static bool HasCompatibleSyntax(IPcre2BacktrackingNode root, Pcre2CompileRequest request)
        => request.Options == Pcre2CompileOptions.None &&
            root is Pcre2AlternationBacktrackingNode { Alternatives.Length: >= 2 } alternation &&
            alternation.Alternatives.All(IsNonEmptyExactLiteral);

    private static bool IsNonEmptyExactLiteral(IPcre2BacktrackingNode node)
    {
        if (node is Pcre2TokenBacktrackingNode token)
        {
            return IsExactLiteral(token.Token);
        }

        return node is Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence &&
            sequence.Children.All(static child =>
                child is Pcre2TokenBacktrackingNode token && IsExactLiteral(token.Token));
    }

    private static bool IsExactLiteral(Pcre2CharacterToken token) =>
        token.Kind == Pcre2CharacterTokenKind.Literal &&
        token.Options == Pcre2CharacterOptions.None;

    private static bool IsExactLiteralFamilyPortfolio(Utf8SearchPortfolioKind portfolioKind) =>
        portfolioKind is Utf8SearchPortfolioKind.ExactDirectFamily or
            Utf8SearchPortfolioKind.ExactTrieFamily or
            Utf8SearchPortfolioKind.ExactAutomatonFamily or
            Utf8SearchPortfolioKind.ExactPackedFamily or
            Utf8SearchPortfolioKind.ExactEarliestFamily;
}

internal static class Pcre2CandidateSearchAnalyzer
{
    internal static Pcre2CandidateSearchProgram Compile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request)
    {
        var containsCodeUnit = false;
        if (request.Options != Pcre2CompileOptions.None || ContainsUnsafeCandidateSemantics(root, ref containsCodeUnit))
        {
            return default;
        }

        if (TryCompileBranchLeadingLiterals(root, out var branchPlan))
        {
            return branchPlan;
        }

        // A literal at the start of every branch is necessary even if a
        // later code unit splits a scalar. Scalar-relative windows still
        // require a pattern without byte atoms.
        if (containsCodeUnit) return default;

        if (TryCompileLeadingRunThenLiteral(root, out var runPlan))
        {
            return runPlan;
        }

        if (TryCompileLeadingAsciiWordRun(root, out var wordRunPlan))
        {
            return wordRunPlan;
        }

        if (TryCompileLeadingAsciiSet(root, request, out var asciiSetPlan))
        {
            return asciiSetPlan;
        }

        if (!TryGetInitialBoundedLiteralConstraint(root, out var windowConstraint)) return default;
        // A mandatory root boundary can screen each scalar-aligned window start.
        // Nondefault request options, including UCP, were excluded above.
        var requiresLeadingWordBoundary = root is Pcre2SequenceBacktrackingNode
        {
            Children: [Pcre2TokenBacktrackingNode
                { Token: { Kind: Pcre2CharacterTokenKind.WordBoundary, Options: Pcre2CharacterOptions.None } }, ..],
        };
        return Pcre2CandidateSearchProgram.FromBoundedLiteralWindow(windowConstraint, requiresLeadingWordBoundary);
    }

    private static bool TryCompileBranchLeadingLiterals(
        IPcre2BacktrackingNode root,
        out Pcre2CandidateSearchProgram plan)
    {
        plan = default;
        var budget = Utf8ConstructionScope.CurrentBudget;
        var branches = (root as Pcre2AlternationBacktrackingNode)?.Alternatives;
        if (branches is null || branches.Length == 1)
        {
            if (!TryGetBranchLeadingLiteral(branches is null ? root : branches[0], budget, out var literal)) return false;
            var searcher = new PreparedSearcher(new PreparedSubstringSearch(literal, ignoreCase: false), ignoreCase: false);
            plan = Pcre2CandidateSearchProgram.FromBranchLeadingLiterals(searcher, literal.Length);
            return true;
        }

        using var attempt = Utf8ConstructionScope.Enter();
        var literals = budget.AllocateArray<byte[]>(branches.Length, false);
        var maximumPrefixLength = 0;
        for (var i = 0; i < branches.Length; i++)
        {
            if (!TryGetBranchLeadingLiteral(branches[i], budget, out literals[i])) return false;
            maximumPrefixLength = Math.Max(maximumPrefixLength, literals[i].Length);
        }

        plan = Pcre2CandidateSearchProgram.FromBranchLeadingLiterals(
            new PreparedSearcher(new PreparedMultiLiteralSearch(literals, ignoreCase: false)), maximumPrefixLength);
        attempt.Complete();
        return true;
    }

    private static bool TryCompileLeadingRunThenLiteral(
        IPcre2BacktrackingNode root,
        out Pcre2CandidateSearchProgram plan)
    {
        plan = default;
        if (root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 2 } sequence)
        {
            return false;
        }

        var budget = Utf8ConstructionScope.CurrentBudget;
        var previous = default(Pcre2CharacterToken);
        var childIndex = 0;
        // Discovery retreats to the first scalar in each run. A finite
        // maximum can make that start fail while a later start succeeds.
        while (childIndex < sequence.Children.Length &&
               sequence.Children[childIndex] is Pcre2RepeatBacktrackingNode
               {
                   Minimum: >= 1,
                   Maximum: int.MaxValue,
                   Body: Pcre2TokenBacktrackingNode { Token: var runToken },
               } &&
               IsRetreatableRunToken(runToken))
        {
            budget.ChargeWork(1);
            if (childIndex != 0 && !AreKnownDisjoint(previous, runToken))
            {
                return false;
            }

            previous = runToken;
            childIndex++;
        }

        if (childIndex == 0 || !TryEncodeLeadingLiteral(sequence.Children.AsSpan(childIndex), 1, budget, out var literal))
        {
            return false;
        }

        var runTokens = budget.AllocateArray<Pcre2CharacterToken>(childIndex, false);
        budget.ChargeWork((ulong)childIndex);
        for (var i = 0; i < runTokens.Length; i++)
        {
            if ((i & 255) == 0) budget.ChargeWork(0);
            runTokens[i] = ((Pcre2TokenBacktrackingNode)((Pcre2RepeatBacktrackingNode)sequence.Children[i]).Body).Token;
        }
        plan = Pcre2CandidateSearchProgram.FromLeadingRunThenLiteral(runTokens, literal);
        return true;
    }

    private static bool TryCompileLeadingAsciiSet(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        out Pcre2CandidateSearchProgram plan)
    {
        var values = new HashSet<byte>();
        if (Pcre2BacktrackingAnalysis.GetMinimumByteLength(root) == 0 ||
            !TryCollectLeadingAsciiBytes(root, request, values) || values.Count is < 1 or > 64)
        {
            plan = default;
            return false;
        }

        _ = TryGetInitialBoundedLiteralConstraint(root, out var windowConstraint);
        plan = Pcre2CandidateSearchProgram.FromLeadingAsciiSet([.. values.Order()], windowConstraint);
        return true;
    }

    private static bool TryCompileLeadingAsciiWordRun(
        IPcre2BacktrackingNode root,
        out Pcre2CandidateSearchProgram plan)
    {
        var requiresBoundary = false;
        if (root is Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence)
        {
            root = sequence.Children[0];
            requiresBoundary = sequence.Children.Length >= 2 && root is Pcre2TokenBacktrackingNode
            {
                Token: { Kind: Pcre2CharacterTokenKind.WordBoundary, Options: Pcre2CharacterOptions.None },
            };
            if (requiresBoundary) root = sequence.Children[1];
        }

        if (root is not Pcre2RepeatBacktrackingNode
            {
                Minimum: >= 2,
                Body: Pcre2TokenBacktrackingNode { Token: var runToken },
            } repeat ||
            runToken.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            runToken.Options != Pcre2CharacterOptions.None ||
            runToken.CharacterClass.Negated ||
            runToken.CharacterClass.Terms is not
            [
                {
                    Kind: Pcre2CharacterClassTermKind.Word,
                    Negated: false,
                },
            ])
        {
            plan = default;
            return false;
        }

        plan = Pcre2CandidateSearchProgram.FromLeadingAsciiWordRun(repeat.Minimum, requiresBoundary);
        return true;
    }

    private static bool TryGetInitialBoundedLiteralConstraint(
        IPcre2BacktrackingNode root,
        out Pcre2CandidateWindowConstraint constraint)
    {
        if (root is Pcre2SequenceBacktrackingNode directSequence &&
            TryGetBoundedLiteralConstraint(directSequence, out constraint))
        {
            return true;
        }

        var startNode = root is Pcre2SequenceBacktrackingNode { Children.Length: > 0 } rootSequence
            ? rootSequence.Children[0]
            : root;
        if (startNode is Pcre2RepeatBacktrackingNode { Minimum: > 0 } repeat)
        {
            startNode = repeat.Body;
        }

        if (startNode is Pcre2SequenceBacktrackingNode sequence)
        {
            return TryGetBoundedLiteralConstraint(sequence, out constraint);
        }

        constraint = default;
        return false;
    }

    private static bool TryGetBoundedLiteralConstraint(
        Pcre2SequenceBacktrackingNode sequence,
        out Pcre2CandidateWindowConstraint constraint)
    {
        if (sequence.Children.Length == 0)
        {
            constraint = default;
            return false;
        }

        var minimumOffset = 0;
        var maximumOffset = 0;
        var budget = Utf8ConstructionScope.CurrentBudget;
        for (var childIndex = 0; childIndex < sequence.Children.Length; childIndex++)
        {
            budget.ChargeWork(1);
            var child = sequence.Children[childIndex];
            if (minimumOffset > 0 &&
                TryEncodeLeadingLiteral(sequence.Children.AsSpan(childIndex), 1, budget, out var literal))
            {
                constraint = new Pcre2CandidateWindowConstraint(literal, minimumOffset, maximumOffset);
                return true;
            }

            if (!IsAsciiOnlyRegularNode(child))
            {
                break;
            }

            minimumOffset = SaturatingAddCandidateOffset(
                minimumOffset,
                Pcre2BacktrackingAnalysis.GetMinimumByteLength(child));
            maximumOffset = SaturatingAddCandidateOffset(
                maximumOffset,
                Pcre2BacktrackingAnalysis.GetMaximumScalarLength(child));
            if (maximumOffset > 64)
            {
                break;
            }
        }

        constraint = default;
        return false;
    }

    private static bool IsAsciiOnlyRegularNode(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2TokenBacktrackingNode { Token: var token } =>
            token.Options == Pcre2CharacterOptions.None &&
            (token.Kind == Pcre2CharacterTokenKind.Literal && token.Literal.IsAscii ||
             token.Kind == Pcre2CharacterTokenKind.CharacterClass && IsAsciiOnlyClass(token.CharacterClass) ||
             // Boundaries consume no bytes; the original VM still verifies them.
             token.Kind is Pcre2CharacterTokenKind.WordBoundary or Pcre2CharacterTokenKind.NonWordBoundary),
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.All(IsAsciiOnlyRegularNode),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.All(IsAsciiOnlyRegularNode),
        Pcre2RepeatBacktrackingNode repeat => IsAsciiOnlyRegularNode(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => IsAsciiOnlyRegularNode(capture.Body),
        Pcre2AtomicBacktrackingNode atomic => IsAsciiOnlyRegularNode(atomic.Body),
        _ => false,
    };

    private static bool IsAsciiOnlyClass(Pcre2CharacterClass characterClass) =>
        !characterClass.Negated &&
        characterClass.Terms.All(static term => !term.Negated && term.Kind switch
        {
            Pcre2CharacterClassTermKind.Range => term.Range.High < 128,
            Pcre2CharacterClassTermKind.Digit or
                Pcre2CharacterClassTermKind.Space or
                Pcre2CharacterClassTermKind.Word => true,
            _ => false,
        });

    private static int SaturatingAddCandidateOffset(int left, int right) =>
        left > int.MaxValue - right ? int.MaxValue : left + right;

    private static bool TryCollectLeadingAsciiBytes(
        IPcre2BacktrackingNode node,
        Pcre2CompileRequest request,
        HashSet<byte> values)
    {
        switch (node)
        {
            case Pcre2TokenBacktrackingNode { Token: var token }:
                return TryCollectTokenAsciiBytes(token, request, values);

            case Pcre2SequenceBacktrackingNode { Children.Length: > 0 } sequence:
                foreach (var child in sequence.Children)
                {
                    if (!TryCollectLeadingAsciiBytes(child, request, values))
                    {
                        return false;
                    }

                    if (Pcre2BacktrackingAnalysis.GetMinimumByteLength(child) > 0)
                    {
                        return true;
                    }
                }

                return false;

            case Pcre2AlternationBacktrackingNode alternation:
                return alternation.Alternatives.All(branch =>
                    Pcre2BacktrackingAnalysis.GetMinimumByteLength(branch) > 0 &&
                    TryCollectLeadingAsciiBytes(branch, request, values));

            case Pcre2RepeatBacktrackingNode repeat:
                return TryCollectLeadingAsciiBytes(repeat.Body, request, values);

            case Pcre2CaptureBacktrackingNode capture:
                return TryCollectLeadingAsciiBytes(capture.Body, request, values);

            case Pcre2AtomicBacktrackingNode atomic:
                return TryCollectLeadingAsciiBytes(atomic.Body, request, values);

            default:
                return false;
        }
    }

    private static bool TryCollectTokenAsciiBytes(
        Pcre2CharacterToken token,
        Pcre2CompileRequest request,
        HashSet<byte> values)
    {
        if (token.Options != Pcre2CharacterOptions.None)
        {
            return false;
        }

        if (token.Kind == Pcre2CharacterTokenKind.Literal && token.Literal.IsAscii)
        {
            values.Add((byte)token.Literal.Value);
            return true;
        }

        if (token.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            (request.Options & Pcre2CompileOptions.Ucp) != 0 ||
            !IsAsciiOnlyClass(token.CharacterClass))
        {
            return false;
        }

        for (var value = 0; value < 128; value++)
        {
            if (token.CharacterClass.AsciiSet.Contains((byte)value))
            {
                values.Add((byte)value);
            }
        }

        return values.Count != 0;
    }

    private static bool IsRetreatableRunToken(Pcre2CharacterToken token) =>
        token.Options == Pcre2CharacterOptions.None &&
        token.Kind is Pcre2CharacterTokenKind.Literal or Pcre2CharacterTokenKind.CharacterClass;

    private static bool AreKnownDisjoint(Pcre2CharacterToken left, Pcre2CharacterToken right)
    {
        if (left.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            right.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            left.CharacterClass.Negated ||
            right.CharacterClass.Negated ||
            left.CharacterClass.Terms is not [{ Kind: var leftKind, Negated: false }] ||
            right.CharacterClass.Terms is not [{ Kind: var rightKind, Negated: false }])
        {
            return false;
        }

        return IsWordAndSpace(leftKind, rightKind) || IsWordAndSpace(rightKind, leftKind);
    }

    private static bool IsWordAndSpace(
        Pcre2CharacterClassTermKind word,
        Pcre2CharacterClassTermKind space) =>
        word == Pcre2CharacterClassTermKind.Word &&
        space is Pcre2CharacterClassTermKind.Space or
            Pcre2CharacterClassTermKind.HorizontalSpace or
            Pcre2CharacterClassTermKind.VerticalSpace;

    private static bool TryGetBranchLeadingLiteral(IPcre2BacktrackingNode branch, Utf8ConstructionBudget budget, out byte[] literal)
    {
        ReadOnlySpan<IPcre2BacktrackingNode> children = branch is Pcre2SequenceBacktrackingNode sequence
            ? sequence.Children
            : [branch];
        var index = 0;
        while (index < children.Length && IsSkippableZeroWidthPrefix(children[index]))
        {
            budget.ChargeWork(1);
            index++;
        }
        return TryEncodeLeadingLiteral(children[index..], 2, budget, out literal);
    }

    private static bool TryEncodeLeadingLiteral(ReadOnlySpan<IPcre2BacktrackingNode> nodes, int minimumBytes,
        Utf8ConstructionBudget budget, out byte[] literal)
    {
        var count = 0;
        var length = 0;
        while (count < nodes.Length &&
               nodes[count] is Pcre2TokenBacktrackingNode { Token: var token } &&
               token.Kind == Pcre2CharacterTokenKind.Literal &&
               token.Options == Pcre2CharacterOptions.None)
        {
            budget.ChargeWork(1);
            length = checked(length + token.Literal.Utf8SequenceLength);
            count++;
        }

        literal = length >= minimumBytes ? Pcre2LiteralCompiler.EncodeLiteralTokens(nodes[..count], length, budget) : [];
        return length >= minimumBytes;
    }

    private static bool IsSkippableZeroWidthPrefix(IPcre2BacktrackingNode node) =>
        node is Pcre2TokenBacktrackingNode
        {
            Token.Kind: Pcre2CharacterTokenKind.WordBoundary or Pcre2CharacterTokenKind.NonWordBoundary,
            Token.Options: Pcre2CharacterOptions.None,
        };

    private static bool ContainsUnsafeCandidateSemantics(IPcre2BacktrackingNode node, ref bool containsCodeUnit)
    {
        Utf8ConstructionScope.ChargeWork(1);
        switch (node)
        {
            // Later backreferences leave necessary leading bytes intact.
            case Pcre2SubroutineCallBacktrackingNode or Pcre2ConditionalBacktrackingNode or
                Pcre2ControlVerbBacktrackingNode or Pcre2MatchBoundaryResetBacktrackingNode:
                return true;
            case Pcre2TokenBacktrackingNode token:
                containsCodeUnit |= token.Token.Kind == Pcre2CharacterTokenKind.CodeUnit;
                return false;
            case Pcre2SequenceBacktrackingNode sequence:
                return ContainsUnsafeCandidateSemantics(sequence.Children, ref containsCodeUnit);
            case Pcre2AlternationBacktrackingNode alternation:
                return ContainsUnsafeCandidateSemantics(alternation.Alternatives, ref containsCodeUnit);
            case Pcre2RepeatBacktrackingNode repeat:
                return ContainsUnsafeCandidateSemantics(repeat.Body, ref containsCodeUnit);
            case Pcre2CaptureBacktrackingNode capture:
                return ContainsUnsafeCandidateSemantics(capture.Body, ref containsCodeUnit);
            case Pcre2AssertionBacktrackingNode assertion:
                return assertion.AssertionKind is Pcre2AssertionKind.PositiveLookbehind or Pcre2AssertionKind.NegativeLookbehind ||
                    ContainsUnsafeCandidateSemantics(assertion.Body, ref containsCodeUnit);
            case Pcre2AtomicBacktrackingNode atomic:
                return ContainsUnsafeCandidateSemantics(atomic.Body, ref containsCodeUnit);
            default:
                return false;
        }
    }

    private static bool ContainsUnsafeCandidateSemantics(IPcre2BacktrackingNode[] branches, ref bool containsCodeUnit)
    {
        foreach (var branch in branches)
            if (ContainsUnsafeCandidateSemantics(branch, ref containsCodeUnit)) return true;
        return false;
    }
}

internal static partial class Pcre2Runner
{

    internal static bool TryIsMatch(
        Pcre2CompiledProgram compiledProgram,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Pcre2ResourceBudget budget,
        out bool result)
    {
        var program = compiledProgram.Operations.IsMatch;
        // Keep ordinary matching out of the larger specialized dispatch frame.
        // Profile instructions use their own checkpoint cadence.
        if (program is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            budget.WorkingInvocation.CheckpointHook = null;
            result = Pcre2BacktrackingRunner.Match(
                backtrackingProgram.Program,
                compiledProgram.CandidateSearch,
                input,
                start,
                matchOptions,
                ref budget, out _).Success;
            return true;
        }

        if (program is Pcre2LiteralDirectProgram literalProgram)
        {
            budget.WorkingInvocation.CheckpointHook = null;
            result = Pcre2LiteralRunner.Match(
                literalProgram.Program,
                input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        return TryIsMatchSpecialized(compiledProgram, input, start, matchOptions,
            ref budget, out result);
    }

    private static bool TryIsMatchSpecialized(
        Pcre2CompiledProgram compiledProgram,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Pcre2ResourceBudget budget,
        out bool result)
    {
        var program = compiledProgram.Operations.IsMatch;
        ref var invocation = ref budget.WorkingInvocation;

        if (program is Pcre2FiniteLiteralLanguageDirectProgram finiteLiteralProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null))
        {
            result = finiteLiteralProgram.BooleanSearch.IsMatch(input, start.Value);
            return true;
        }

        if (program is Pcre2LiteralFamilyDirectProgram literalFamilyProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                if (literalFamilyProgram.Regex.TryFindNativeLiteralForResult(input, start.Value,
                        ref invocation, out result, out _, out _)) return true;
            }

            invocation.CheckpointHook = null;
            result = Pcre2BacktrackingRunner.Match(
                literalFamilyProgram.Fallback,
                compiledProgram.CandidateSearch,
                input,
                start,
                matchOptions,
                ref budget, out _).Success;
            return true;
        }

        if (program is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                invocation.CheckpointHook = null;
                result = Pcre2BacktrackingRunner.Match(
                    sliceProgram.Fallback,
                    compiledProgram.CandidateSearch,
                    input,
                    start,
                    matchOptions,
                    ref budget, out _).Success;
                return true;
            }

            if (sliceProgram.BooleanLiteral is { } exact)
            {
                Pcre2SliceDriver.ThrowIfSliceBreached(invocation.TryReserveWorking(0));
                var breach = Utf8SearchExecutor.TryFindLiteralCandidate(exact.Search, input,
                    start.Value, input.Length - exact.LiteralUtf8.Length, false, ref invocation, out var candidate);
                Pcre2SliceDriver.ThrowIfSliceBreached(breach);
                result = candidate >= 0;
                return true;
            }

            if (Pcre2SliceDriver.RejectMissingCandidate(sliceProgram.SliceProgram,
                compiledProgram.CandidateSearch, input, start.Value, ref invocation, out var firstAsciiStart))
            {
                result = false;
                return true;
            }

            var leadingAsciiByte = (compiledProgram.Request.Options & Pcre2CompileOptions.Anchored) != 0
                ? null : sliceProgram.Fallback.LeadingAsciiByte;
            result = Pcre2SliceDriver.FindFirst(sliceProgram.SliceProgram, input, start.Value,
                sliceProgram.Fallback.MinimumByteLength, ref invocation, leadingAsciiByte,
                sliceProgram.DisjointClassDelimiter, firstAsciiStart).Success;
            return true;
        }

        result = false;
        return false;
    }

    internal static bool TryMatch(
        Pcre2CompiledProgram compiledProgram,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Pcre2ResourceBudget budget,
        out Pcre2GroupData result, out string? mark)
    {
        mark = null;
        var program = compiledProgram.Operations.Match;
        ref var invocation = ref budget.WorkingInvocation;
        Pcre2CharacterMatch directMatch;
        if (program is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                invocation.CheckpointHook = null;
                directMatch = Pcre2BacktrackingRunner.Match(
                    sliceProgram.Fallback,
                    compiledProgram.CandidateSearch,
                    input,
                    start,
                    matchOptions,
                    ref budget, out mark);
            }
            else
            {
                var firstAsciiStart = -1;
                // Bulk leading-byte discovery avoids trying every scalar in a
                // long prefix. Short whole matches use the direct attempt.
                if (input.Length - start.Value >= 64 &&
                    compiledProgram.CandidateSearch.Kind == Pcre2CandidateSearchKind.LeadingAsciiSet &&
                    Pcre2SliceDriver.RejectMissingCandidate(sliceProgram.SliceProgram, compiledProgram.CandidateSearch,
                        input, start.Value, ref invocation, out firstAsciiStart))
                {
                    result = default;
                    return true;
                }
                directMatch = Pcre2SliceDriver.FindFirst(sliceProgram.SliceProgram, input, start.Value,
                    sliceProgram.Fallback.MinimumByteLength, ref invocation, delimiter: sliceProgram.DisjointClassDelimiter,
                    firstAsciiStart: firstAsciiStart);
                invocation.CheckpointHook = null;
            }
        }

        else if (program is Pcre2LiteralDirectProgram literalProgram)
        {
            invocation.CheckpointHook = null;
            var literalMatch = Pcre2LiteralRunner.Match(
                literalProgram.Program,
                input,
                start,
                matchOptions,
                ref budget);
            if (!literalMatch.Success) { result = default; return true; }
            // Discovery and required coordinate projection share the original
            // native authority; a short successful kernel is not publication.
            result = Pcre2MatchPublication.Publish(literalMatch.StartOffsetInBytes,
                literalMatch.EndOffsetInBytes, input, ref budget);
            budget.CheckDeadline();
            return true;
        }
        else if (program is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            invocation.CheckpointHook = null;
            directMatch = Pcre2BacktrackingRunner.Match(
                backtrackingProgram.Program,
                compiledProgram.CandidateSearch,
                input,
                start,
                matchOptions,
                ref budget, out mark);
        }
        else
        {
            result = default;
            return false;
        }

        if (!directMatch.Success)
        {
            result = default;
            return true;
        }
        result = Pcre2MatchPublication.Publish(directMatch.StartOffsetInBytes,
            directMatch.EndOffsetInBytes, input, ref budget);
        return true;
    }

    internal static bool TryMatchDetailed(
        Pcre2CompiledProgram compiledProgram,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Utf8SliceInvocation invocation,
        out Pcre2GroupData[] result,
        out string? mark)
    {
        if (compiledProgram.Operations.Match is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                var fallbackMatch = ExecuteBacktrackingDetailed(
                    sliceProgram.Fallback,
                    compiledProgram.CandidateSearch,
                    input,
                    start,
                    matchOptions,
                    compiledProgram.Request, ref invocation);
                result = fallbackMatch.Success ?
                    fallbackMatch.CaptureResult.ProjectedGroups : [];
                mark = fallbackMatch.Mark;
                return true;
            }

            return Pcre2SliceDriver.TryMatchDetailed(
                sliceProgram,
                compiledProgram.CandidateSearch,
                input,
                start,
                ref invocation,
                out result,
                out mark);
        }

        if (compiledProgram.Operations.Match is not Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            var budget = new Pcre2ResourceBudget(compiledProgram.Request.DefaultLimits,
                invocation, false);
            if (Pcre2GlobalOperationDriver.TryCreateCursor(compiledProgram, input, start, matchOptions,
                out var cursor, budget))
            {
                mark = null;
                if (!cursor.MoveNext()) { result = []; return true; }
                var control = cursor.WorkingBufferControl;
                var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
                control.TryChargeResultBytes(bytes);
                control.ReserveWorking(bytes);
                try { result = [cursor.Current]; return true; }
                finally { control.ReleaseWorking(bytes); }
            }
            result = [];
            mark = null;
            return false;
        }

        var match = ExecuteBacktrackingDetailed(
            backtrackingProgram.Program,
            compiledProgram.CandidateSearch,
            input,
            start,
            matchOptions,
            compiledProgram.Request, ref invocation);
        if (!match.Success)
        {
            result = [];
            mark = match.Mark;
            return true;
        }

        result = match.CaptureResult.ProjectedGroups;
        mark = match.Mark;
        return true;
    }

    private static Pcre2BacktrackingMatch ExecuteBacktrackingDetailed(
        Pcre2BacktrackingProgram program,
        in Pcre2CandidateSearchProgram candidateSearch,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request, scoped ref Utf8SliceInvocation invocation)
    {
        try
        {
            var budget = new Pcre2ResourceBudget(request.DefaultLimits,
                invocation, collectDiagnostics: false);
            var result = Pcre2BacktrackingRunner.MatchDetailed(program, candidateSearch, input, start, matchOptions, ref budget);
            result.CaptureResult.DetachProjectedWorking(ref budget);
            return result;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

}

internal static class Pcre2GlobalOperationDriver
{
    internal static bool TryCount(
        Pcre2CompiledProgram compiledProgram,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Utf8SliceInvocation invocation,
        out int result)
    {
        var program = compiledProgram.Operations.Count;

        if (matchOptions == Pcre2MatchOptions.None && program is Pcre2BacktrackingDirectProgram tokenProgram &&
            Pcre2SliceDriver.TryCountSingleToken(tokenProgram.Program, input[start.Value..], ref invocation, out result))
            return true;

        if (program is Pcre2LiteralFamilyDirectProgram literalFamilyProgram &&
            matchOptions == Pcre2MatchOptions.None)
        {
            if (invocation.IsUnmetered && invocation.CheckpointHook is null)
            {
                // Expanded literals preserve consumption and ordered choice.
                // A start reset changes the reported range only.
                var regex = literalFamilyProgram.Regex;
                var remaining = input[start.Value..];
                var prepared = regex.Inspection.PreparedRegex;
                if (prepared.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal &&
                    prepared.SearchPlan.LiteralSearch is { Length: > 0 } literal)
                {
                    // Keep the prepared searcher's selective anchors on a
                    // single literal and advance over its full consumed range.
                    result = 0;
                    while (remaining.Length >= literal.Length)
                    {
                        var found = literal.IndexOf(remaining);
                        if (found < 0) break;
                        result++;
                        remaining = remaining[(found + literal.Length)..];
                    }
                }
                else result = regex.Count(remaining);
                return true;
            }

            // The finite-language compiler already owns escaped, ordered literals.
            // Reuse their bounded native Count under the original authority; neither
            // this value operation nor its scalar fallback needs projected captures.
            var inspection = literalFamilyProgram.Regex.Inspection;
            var scalarProgram = inspection.AvailableOwnedSliceProgram;
            if (!scalarProgram.CanMatchEmpty && !scalarProgram.RightToLeft)
            {
                var searchPlan = inspection.SearchPlan;
                if (literalFamilyProgram is Pcre2FiniteLiteralLanguageDirectProgram { BoundaryProjection: null } &&
                    (inspection.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal ||
                     inspection.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
                     searchPlan.AlternateLiteralsUtf8 is { Length: 2 or 3 }) &&
                    !searchPlan.HasBoundaryRequirements && !searchPlan.HasTrailingLiteralRequirement)
                {
                    var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(searchPlan,
                        input[start.Value..], false, ref invocation, out result);
                    Pcre2SliceDriver.ThrowIfSliceBreached(breach);
                    return true;
                }
                result = Pcre2SliceDriver.CountNonEmpty(scalarProgram, searchPlan, input, start, ref invocation);
                return true;
            }
        }

        if (program is Pcre2SliceDirectProgram sliceProgram && matchOptions == Pcre2MatchOptions.None)
        {
            var scalar = sliceProgram.SliceProgram;
            if (Pcre2SliceDriver.RejectMissingCandidate(scalar,
                compiledProgram.CandidateSearch, input, start.Value, ref invocation, out var firstAsciiStart))
            {
                result = 0;
                return true;
            }

            // An admitted ASCII start rules out the preceding bytes. Keep the
            // original subject and start when the program can observe the origin.
            if (firstAsciiStart > start.Value && !scalar.HasContiguousAnchor)
                start = new Utf8BytePosition(firstAsciiStart);

            result = Pcre2SliceDriver.Count(
                sliceProgram,
                compiledProgram.CandidateSearch,
                input,
                start,
                compiledProgram.Request,
                ref invocation);
            return true;
        }

        var budget = new Pcre2ResourceBudget(compiledProgram.Request.DefaultLimits, invocation, false);
        Pcre2GlobalMatchCursor cursor;
        var reuseBacktrackingScratch = false;
        if (program is not Pcre2SliceDirectProgram &&
            TryCreateCursor(compiledProgram, input, start, matchOptions, out cursor, budget))
        {
            reuseBacktrackingScratch = compiledProgram.Operations.Enumerate is Pcre2BacktrackingDirectProgram;
        }
        else
        {
            // Flagged slice Count uses the selected slice's fallback. The
            // enumeration overlay also supplies one when no other global
            // vehicle can count the program under the active limits.
            var fallback = (program as Pcre2SliceDirectProgram ??
                compiledProgram.Operations.Enumerate as Pcre2SliceDirectProgram)?.Fallback;
            if (fallback is null)
            {
                result = 0;
                return false;
            }

            cursor = Pcre2GlobalMatchCursor.CreateBacktracking(
                fallback,
                compiledProgram.CandidateSearchPlan,
                input,
                start,
                matchOptions,
                compiledProgram.Request,
                budget);
        }

        try
        {
            result = 0;
            if (reuseBacktrackingScratch)
            {
                // This lexical cursor owns scratch across attempts.
                var frames = Pcre2BacktrackingRunner.CreateFrames(ref budget, 0);
                var repeatMutations = new Pcre2InvocationStateStack<Pcre2RepeatMutation>(int.MaxValue, budget.CollectsDiagnostics);
                var workspace = new Pcre2BacktrackingWorkspace();
                try
                {
                    while (cursor.MoveNext(ref frames, ref repeatMutations, ref workspace)) result = checked(result + 1);
                }
                finally { cursor.ReleaseBacktrackingStorage(ref frames, ref repeatMutations, ref workspace); }
            }
            else
            {
                while (cursor.MoveNext()) result = checked(result + 1);
            }
            return true;
        }
        finally
        {
            // Return ownership after cursor cleanup, including on denial.
            // The profile cursor used its own checkpoint cadence.
            var hook = invocation.CheckpointHook;
            invocation = cursor.WorkingBufferControl.WorkingInvocation;
            invocation.CheckpointHook = hook;
        }
    }

    internal static bool HasUnmeteredExecution(Pcre2CompileRequest request) =>
        request.MatchTimeout == Timeout.InfiniteTimeSpan &&
        request.DefaultLimits.MatchLimit == 0 &&
        request.DefaultLimits.DepthLimit == 0 &&
        request.DefaultLimits.HeapLimitInBytes == 0 &&
        request.DefaultLimits.ResultLimitInBytes == 0;

    internal static bool TryCreateCursor(
        Pcre2CompiledProgram compiledProgram,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        out Pcre2GlobalMatchCursor cursor,
        scoped in Pcre2ResourceBudget budget)
    {

        if (compiledProgram.Operations.Enumerate is Pcre2SameStartLiteralDirectProgram sameStartLiteralProgram)
        {
            cursor = matchOptions == Pcre2MatchOptions.None && budget.IsUnmetered
                ? Pcre2GlobalMatchCursor.CreateSameStartLiteral(
                    sameStartLiteralProgram,
                    input,
                    start,
                    compiledProgram.Request, budget)
                : Pcre2GlobalMatchCursor.CreateBacktracking(
                    sameStartLiteralProgram.Fallback,
                    compiledProgram.CandidateSearchPlan,
                    input,
                    start,
                    matchOptions,
                    compiledProgram.Request,
                    budget);
            return true;
        }

        if (compiledProgram.Operations.Enumerate is Pcre2LiteralFamilyDirectProgram literalFamilyProgram)
        {
            cursor = matchOptions == Pcre2MatchOptions.None && budget.IsUnmetered
                ? Pcre2GlobalMatchCursor.CreateLiteralFamily(literalFamilyProgram, input, start, budget)
                : Pcre2GlobalMatchCursor.CreateBacktracking(
                    literalFamilyProgram.Fallback,
                    compiledProgram.CandidateSearchPlan,
                    input,
                    start,
                    matchOptions,
                    compiledProgram.Request,
                    budget);
            return true;
        }

        if (compiledProgram.Operations.Enumerate is Pcre2LiteralDirectProgram literalProgram)
        {
            cursor = Pcre2GlobalMatchCursor.CreateLiteral(
                literalProgram.Program, input, start, matchOptions, compiledProgram.Request, budget);
            return true;
        }

        if (compiledProgram.Operations.Enumerate is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            cursor = Pcre2GlobalMatchCursor.CreateBacktracking(
                backtrackingProgram.Program,
                compiledProgram.CandidateSearchPlan,
                input,
                start,
                matchOptions,
                compiledProgram.Request,
                budget);
            return true;
        }

        cursor = default;
        return false;
    }
}

internal ref struct Pcre2GlobalMatchCursor
{
    [UnscopedRef]
    internal Pcre2ResultControl WorkingBufferControl => _kind == Pcre2GlobalCursorKind.Direct
        ? _direct.WorkingBufferControl : _literalFamily.WorkingBufferControl;

    private readonly Pcre2GlobalCursorKind _kind;
    private Pcre2LiteralFamilyGlobalMatchCursor _literalFamily;
    private Pcre2DirectGlobalMatchCursor _direct;

    private Pcre2GlobalMatchCursor(scoped in Pcre2DirectGlobalMatchCursor direct)
    {
        _kind = Pcre2GlobalCursorKind.Direct;
        _literalFamily = default;
        _direct = direct;
    }

    private Pcre2GlobalMatchCursor(Pcre2LiteralFamilyGlobalMatchCursor literalFamily)
    {
        _kind = Pcre2GlobalCursorKind.LiteralFamily;
        _literalFamily = literalFamily;
        _direct = default;
    }

    internal Pcre2GroupData Current => _kind switch
    {
        Pcre2GlobalCursorKind.LiteralFamily => _literalFamily.Current,
        Pcre2GlobalCursorKind.Direct => _direct.Current,
        _ => default,
    };

    internal Pcre2ExecutionDiagnostics Diagnostics => _kind == Pcre2GlobalCursorKind.Direct
        ? _direct.Diagnostics
        : default;

    internal static Pcre2GlobalMatchCursor CreateLiteral(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        scoped in Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateLiteral(program, input, start, matchOptions, request, budget));

    internal static Pcre2GlobalMatchCursor CreateLiteralFamily(
        Pcre2LiteralFamilyDirectProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        scoped in Pcre2ResourceBudget budget) =>
        new(new Pcre2LiteralFamilyGlobalMatchCursor(program, input, start, budget));

    internal static Pcre2GlobalMatchCursor CreateSameStartLiteral(
        Pcre2SameStartLiteralDirectProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        scoped in Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateSameStartLiteral(program, input, start, request, budget));

    internal static Pcre2GlobalMatchCursor CreateBacktracking(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        bool collectDiagnostics) =>
        CreateBacktracking(program, candidateSearch, input, start, matchOptions, request,
            new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout, collectDiagnostics));

    internal static Pcre2GlobalMatchCursor CreateBacktracking(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        scoped in Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateBacktracking(
            program,
            candidateSearch,
            input,
            start,
            matchOptions,
            request,
            budget));

    internal bool MoveNext()
    {
        if (_kind == Pcre2GlobalCursorKind.None) return false;
        var control = WorkingBufferControl;
        Pcre2SliceDriver.CheckInvocation(ref control.WorkingInvocation);
        var moved = _kind == Pcre2GlobalCursorKind.LiteralFamily
            ? _literalFamily.MoveNext() : _direct.MoveNext();
        Pcre2SliceDriver.CheckInvocation(ref control.WorkingInvocation);
        return moved;
    }

    internal bool MoveNext(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace)
    {
        if (_kind != Pcre2GlobalCursorKind.Direct) return MoveNext();
        var control = WorkingBufferControl;
        Pcre2SliceDriver.CheckInvocation(ref control.WorkingInvocation);
        var moved = _direct.MoveNext(ref frames, ref repeatMutations, ref workspace);
        Pcre2SliceDriver.CheckInvocation(ref control.WorkingInvocation);
        return moved;
    }

    internal void ReleaseBacktrackingStorage(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace)
        => _direct.ReleaseBacktrackingStorage(ref frames, ref repeatMutations, ref workspace);

    private enum Pcre2GlobalCursorKind : byte
    {
        None = 0,
        Direct = 1,
        LiteralFamily = 2,
    }
}

internal readonly record struct Pcre2DirectGlobalMatch(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int ConsumedStartOffsetInBytes,
    int ConsumedEndOffsetInBytes,
    bool MatchBoundaryWasReset);

internal ref struct Pcre2DirectGlobalMatchCursor
{
    [UnscopedRef]
    internal Pcre2ResultControl WorkingBufferControl => new(ref _budget);

    private readonly Pcre2DirectGlobalCursorKind _kind;
    private readonly Pcre2LiteralProgram? _literalProgram;
    private readonly Pcre2SameStartLiteralDirectProgram? _sameStartLiteralProgram;
    private readonly Pcre2BacktrackingProgram? _backtrackingProgram;
    private readonly Pcre2CandidateSearchPlan? _candidateSearch;
    private ReadOnlySpan<byte> _input;
    private readonly Pcre2CompileRequest _request;
    private readonly Pcre2MatchOptions _matchOptions;
    private Pcre2ResourceBudget _budget;
    private Utf8BytePosition _restartPosition;
    private Utf8BytePosition _firstMatchingPosition;
    private Pcre2GlobalRetryState _retryState;

    private Pcre2DirectGlobalMatchCursor(
        Pcre2DirectGlobalCursorKind kind,
        Pcre2LiteralProgram? literalProgram,
        Pcre2SameStartLiteralDirectProgram? sameStartLiteralProgram,
        Pcre2BacktrackingProgram? backtrackingProgram,
        Pcre2CandidateSearchPlan? candidateSearch,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        scoped in Pcre2ResourceBudget budget)
    {
        _kind = kind;
        _literalProgram = literalProgram;
        _sameStartLiteralProgram = sameStartLiteralProgram;
        _backtrackingProgram = backtrackingProgram;
        _candidateSearch = candidateSearch;
        _input = input;
        _request = request;
        _matchOptions = matchOptions;
        _budget = budget;
        _restartPosition = start;
        _firstMatchingPosition = start;
        _retryState = Pcre2GlobalRetryState.Search;
        Current = default;
    }

    internal Pcre2GroupData Current { get; private set; }

    internal readonly Pcre2ExecutionDiagnostics Diagnostics => _budget.Diagnostics;

    internal static Pcre2DirectGlobalMatchCursor CreateLiteral(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        scoped in Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.Literal,
            program,
            sameStartLiteralProgram: null,
            backtrackingProgram: null,
            candidateSearch: default,
            input,
            start,
            matchOptions,
            request,
            budget);

    internal static Pcre2DirectGlobalMatchCursor CreateSameStartLiteral(
        Pcre2SameStartLiteralDirectProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        scoped in Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.SameStartLiteral,
            literalProgram: null,
            program,
            backtrackingProgram: null,
            candidateSearch: default,
            input,
            start,
            Pcre2MatchOptions.None,
            request,
            budget);

    internal static Pcre2DirectGlobalMatchCursor CreateBacktracking(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        scoped in Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.Backtracking,
            literalProgram: null,
            sameStartLiteralProgram: null,
            program,
            candidateSearch,
            input,
            start,
            matchOptions,
            request,
            budget);

    internal void ReleaseBacktrackingStorage(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace)
    {
        workspace.Dispose(ref _budget);
        repeatMutations.Dispose(ref _budget);
        frames.Dispose(ref _budget);
    }

    internal bool MoveNext()
    {
        var frames = Pcre2BacktrackingRunner.CreateFrames(ref _budget, 0);
        var repeatMutations = new Pcre2InvocationStateStack<Pcre2RepeatMutation>(int.MaxValue, _budget.CollectsDiagnostics);
        var workspace = new Pcre2BacktrackingWorkspace();
        try { return MoveNext(ref frames, ref repeatMutations, ref workspace); }
        finally { ReleaseBacktrackingStorage(ref frames, ref repeatMutations, ref workspace); }
    }

    internal bool MoveNext(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames,
        scoped ref Pcre2InvocationStateStack<Pcre2RepeatMutation> repeatMutations,
        scoped ref Pcre2BacktrackingWorkspace workspace)
    {
        while (_retryState != Pcre2GlobalRetryState.Finished)
        {
            var retryingEmptyAtSamePosition = _retryState == Pcre2GlobalRetryState.EmptyAtSamePosition;
            var options = retryingEmptyAtSamePosition
                ? _matchOptions | Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart
                : _matchOptions;

            Pcre2DirectGlobalMatch match;
            try
            {
                switch (_kind)
                {
                    case Pcre2DirectGlobalCursorKind.Literal:
                        var literalProgram = _literalProgram ??
                            throw new InvalidOperationException("The PCRE2 literal cursor has no execution program.");
                        var literalMatch = Pcre2LiteralRunner.Match(
                            literalProgram,
                            _input,
                            _restartPosition,
                            options,
                            ref _budget);
                        match = new Pcre2DirectGlobalMatch(
                            literalMatch.Success,
                            literalMatch.StartOffsetInBytes,
                            literalMatch.EndOffsetInBytes,
                            literalMatch.StartOffsetInBytes,
                            literalMatch.ConsumedEndOffsetInBytes,
                            MatchBoundaryWasReset: false);
                        break;
                    case Pcre2DirectGlobalCursorKind.SameStartLiteral:
                        var sameStartLiteralProgram = _sameStartLiteralProgram ??
                            throw new InvalidOperationException("The PCRE2 same-start literal cursor has no execution program.");
                        match = Pcre2SameStartLiteralRunner.Match(
                            sameStartLiteralProgram,
                            _input,
                            _restartPosition,
                            _firstMatchingPosition,
                            options);
                        break;
                    case Pcre2DirectGlobalCursorKind.Backtracking:
                        var backtrackingProgram = _backtrackingProgram ??
                            throw new InvalidOperationException("The PCRE2 backtracking cursor has no execution program.");
                        var candidateSearch = _candidateSearch ??
                            throw new InvalidOperationException("The PCRE2 backtracking cursor has no candidate-search plan.");
                        var backtrackingMatch = Pcre2BacktrackingRunner.Match(
                            backtrackingProgram,
                            candidateSearch.Program,
                            _input,
                            _restartPosition,
                            _firstMatchingPosition,
                            options,
                            ref _budget, ref frames, ref repeatMutations, ref workspace);
                        match = new Pcre2DirectGlobalMatch(
                            backtrackingMatch.Success,
                            backtrackingMatch.StartOffsetInBytes,
                            backtrackingMatch.EndOffsetInBytes,
                            backtrackingMatch.ConsumedStartOffsetInBytes,
                            backtrackingMatch.ConsumedEndOffsetInBytes,
                            backtrackingMatch.MatchBoundaryWasReset);
                        break;
                    default:
                        Current = default;
                        return false;
                }
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
            }

            if (match.Success)
            {
                var isBacktracking = _kind == Pcre2DirectGlobalCursorKind.Backtracking;
                if (isBacktracking &&
                    _backtrackingProgram is { SuppressesUnresetEmptyMatches: true } &&
                    !match.MatchBoundaryWasReset &&
                    match.ConsumedStartOffsetInBytes == match.ConsumedEndOffsetInBytes)
                {
                    if (!Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(
                            _input,
                            _request, _matchOptions, new Utf8BytePosition(match.ConsumedEndOffsetInBytes),
                            out _restartPosition))
                    {
                        _retryState = Pcre2GlobalRetryState.Finished;
                        Current = default;
                        return false;
                    }

                    _retryState = Pcre2GlobalRetryState.Search;
                    continue;
                }

                if (isBacktracking && match.StartOffsetInBytes > match.EndOffsetInBytes)
                {
                    throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
                }

                if (isBacktracking)
                {
                    _budget.RecordResultProjection();
                }

                Current = Pcre2MatchPublication.Publish(
                    match.StartOffsetInBytes,
                    match.EndOffsetInBytes,
                    _input,
                    ref _budget);
                _restartPosition = new Utf8BytePosition(match.ConsumedEndOffsetInBytes);
                if (isBacktracking)
                {
                    _firstMatchingPosition = _restartPosition;
                }

                var isEmptyForRetry = isBacktracking
                    ? match.ConsumedStartOffsetInBytes == match.ConsumedEndOffsetInBytes
                    : match.StartOffsetInBytes == match.EndOffsetInBytes;
                _retryState = isEmptyForRetry
                    ? Pcre2GlobalRetryState.EmptyAtSamePosition
                    : Pcre2GlobalRetryState.Search;
                return true;
            }

            if (!retryingEmptyAtSamePosition ||
                !Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(
                    _input,
                    _request, _matchOptions, _restartPosition,
                    out _restartPosition))
            {
                _retryState = Pcre2GlobalRetryState.Finished;
                Current = default;
                return false;
            }

            _retryState = Pcre2GlobalRetryState.Search;
        }

        Current = default;
        return false;
    }

    private enum Pcre2DirectGlobalCursorKind : byte
    {
        None = 0,
        Literal = 1,
        Backtracking = 4,
        SameStartLiteral = 6,
    }
}

internal ref struct Pcre2LiteralFamilyGlobalMatchCursor
{
    private Utf8PreparedValueMatchEnumerator _enumerator;
    private ReadOnlySpan<byte> _input;
    private readonly Pcre2FiniteLiteralBoundaryProjection? _boundaryProjection;
    private Pcre2ResourceBudget _budget;

    [UnscopedRef]
    internal Pcre2ResultControl WorkingBufferControl => new(ref _budget);

    internal Pcre2LiteralFamilyGlobalMatchCursor(
        Pcre2LiteralFamilyDirectProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        scoped in Pcre2ResourceBudget budget)
    {
        _enumerator = program.Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start);
        _input = input;
        _boundaryProjection = (program as Pcre2FiniteLiteralLanguageDirectProgram)?.BoundaryProjection;
        // The selector admits this route only for unmetered matching. Shaping
        // still owns an aggregate working ledger on this same operation cursor.
        _budget = budget;
        Current = default;
    }

    internal Pcre2GroupData Current { get; private set; }

    internal bool MoveNext()
    {
        if (!_enumerator.MoveNext())
        {
            Current = default;
            return false;
        }

        var consumedStart = _enumerator.StartOffsetInBytes;
        var consumedEnd = _enumerator.EndOffsetInBytes;
        var reportedStart = _boundaryProjection?.ProjectStartOffset(
            _input,
            consumedStart,
            consumedEnd) ?? consumedStart;
        Current = Pcre2MatchPublication.Publish(
            reportedStart,
            consumedEnd,
            _input,
            ref _budget);
        return true;
    }
}

internal ref struct Pcre2BacktrackingDetailedGlobalMatchCursor
{
    private readonly Pcre2BacktrackingProgram _program;
    private readonly Pcre2CandidateSearchPlan _candidateSearch;
    private ReadOnlySpan<byte> _input;
    private readonly Pcre2CompileRequest _request;
    private readonly Pcre2MatchOptions _matchOptions;
    private Pcre2ResourceBudget _budget;
    private Pcre2GlobalProgress _progress;

    internal Pcre2BacktrackingDetailedGlobalMatchCursor(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
        : this(program, candidateSearch, input, start, matchOptions, request,
            new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout, collectDiagnostics: false))
    {
    }

    internal Pcre2BacktrackingDetailedGlobalMatchCursor(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget)
    {
        _program = program;
        _candidateSearch = candidateSearch;
        _input = input;
        _request = request;
        _matchOptions = matchOptions;
        _budget = budget;
        _progress = new(start);
        Current = default;
    }

    internal Pcre2BacktrackingMatch Current { get; private set; }

    [UnscopedRef]
    internal ref Utf8SliceInvocation WorkingInvocation => ref _budget.WorkingInvocation;

    internal Pcre2GroupData[] ProjectCurrentCaptures() => Current.CaptureResult.ProjectedGroups;

    internal bool MoveNext()
    {
        while (!_progress.Finished)
        {
            _progress.GetAttempt(_matchOptions, out var restart, out var firstMatchingPosition, out var options);

            Pcre2BacktrackingMatch match;
            try
            {
                match = Pcre2BacktrackingRunner.MatchDetailed(
                    _program,
                    _candidateSearch.Program,
                    _input,
                    restart,
                    firstMatchingPosition,
                    options,
                    ref _budget);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
            }

            match.CaptureResult.DetachProjectedWorking(ref _budget);

            if (match.Success)
            {
                _progress.Accept(match.StartOffsetInBytes, match.EndOffsetInBytes,
                    match.ConsumedStartOffsetInBytes, match.ConsumedEndOffsetInBytes);
                Current = match;
                return true;
            }

            if (!_progress.AfterMiss(_input, _request, _matchOptions))
            {
                Current = default;
                return false;
            }
        }

        Current = default;
        return false;
    }
}

internal static class Pcre2MatchPublication
{
    internal static Pcre2GroupData Publish(int start, int end,
        ReadOnlySpan<byte> input, ref Pcre2ResourceBudget budget)
        => Publish(start, end, input, new Pcre2ResultControl(ref budget));

    internal static Pcre2GroupData Publish(int start, int end,
        ReadOnlySpan<byte> input, scoped Pcre2ResultControl control)
    {
        control.ChargeWorkspaceStep();
        return new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = start,
            EndOffsetInBytes = end,
            Utf8SliceIsWellFormed = Pcre2CaptureRanges.IsWellFormed(input, start, end, scalarRanges: false)
        };
    }
}

internal static class Pcre2GlobalCursorMovement
{
    internal static bool TryAdvanceAfterEmpty(
        ReadOnlySpan<byte> input,
        in Pcre2CompileRequest request,
        Pcre2MatchOptions matchOptions,
        Utf8BytePosition position,
        out Utf8BytePosition next)
    {
        // An anchored search cannot bump to another starting position after
        // an empty retry fails or an unreset empty result is suppressed.
        if ((request.Options & Pcre2CompileOptions.Anchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0)
        {
            next = position;
            return false;
        }
        var settings = request.Settings;
        var bytes = input;
        if (!position.IsScalarBoundary(input))
        {
            next = new Utf8BytePosition(position.Value + 1);
            return next.Value <= bytes.Length;
        }

        if (RecognizesCrlf(settings.Newline) &&
            position.Value <= bytes.Length - 2 &&
            bytes[position.Value] == (byte)'\r' &&
            bytes[position.Value + 1] == (byte)'\n')
        {
            next = new Utf8BytePosition(position.Value + 2);
            return true;
        }

        return position.TryAdvanceScalar(input, out next);
    }

    private static bool RecognizesCrlf(Pcre2NewlineConvention newline)
        => newline is Pcre2NewlineConvention.Crlf or
            Pcre2NewlineConvention.Any or
            Pcre2NewlineConvention.AnyCrlf;
}

internal enum Pcre2GlobalRetryState : byte
{
    Search = 0,
    EmptyAtSamePosition = 1,
    Finished = 2,
}

internal sealed class Pcre2ExecutionDiagnosticsCollector
{
    internal void RecordBacktrackingInstruction(
        Pcre2BacktrackingInstructionKind instructionKind,
        Pcre2CharacterTokenKind tokenKind)
    {
        switch (instructionKind)
        {
            case Pcre2BacktrackingInstructionKind.Token:
                VmTokenSteps++;
                switch (tokenKind)
                {
                    case Pcre2CharacterTokenKind.Literal:
                        VmLiteralTokenSteps++;
                        break;
                    case Pcre2CharacterTokenKind.CharacterClass:
                        VmClassTokenSteps++;
                        break;
                    case Pcre2CharacterTokenKind.BeginningOfLine:
                    case Pcre2CharacterTokenKind.EndOfLine:
                    case Pcre2CharacterTokenKind.BeginningOfSubject:
                    case Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline:
                    case Pcre2CharacterTokenKind.EndOfSubject:
                    case Pcre2CharacterTokenKind.FirstMatchingPosition:
                    case Pcre2CharacterTokenKind.WordBoundary:
                    case Pcre2CharacterTokenKind.NonWordBoundary:
                        VmBoundaryAnchorTokenSteps++;
                        break;
                    default:
                        VmOtherTokenSteps++;
                        break;
                }
                break;
            case Pcre2BacktrackingInstructionKind.Split:
            case Pcre2BacktrackingInstructionKind.Jump:
                VmBranchSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.Repeat:
                VmRepeatSteps++;
                VmRepeatEnterSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.RepeatEnd:
                VmRepeatSteps++;
                VmRepeatEndSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.RepeatExit:
                VmRepeatSteps++;
                VmRepeatExitSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.PossessiveTokenRepeat:
                VmRepeatSteps++;
                VmRepeatEnterSteps++;
                VmPossessiveTokenScanSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.GreedyClassRepeat:
                VmRepeatSteps++;
                VmRepeatEnterSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.CaptureStart:
            case Pcre2BacktrackingInstructionKind.CaptureEnd:
            case Pcre2BacktrackingInstructionKind.CaptureToken:
            case Pcre2BacktrackingInstructionKind.Backreference:
            case Pcre2BacktrackingInstructionKind.BackreferenceSlotSet:
                VmCaptureSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.Assertion:
            case Pcre2BacktrackingInstructionKind.SubroutineCall:
            case Pcre2BacktrackingInstructionKind.SubroutineReturn:
                VmAssertionSubroutineSteps++;
                break;
            case Pcre2BacktrackingInstructionKind.Accept:
                VmAcceptSteps++;
                break;
            default:
                VmControlSteps++;
                break;
        }
    }

    internal ulong VmTokenSteps { get; set; }

    internal ulong VmLiteralTokenSteps { get; set; }

    internal ulong VmClassTokenSteps { get; set; }

    internal ulong VmBoundaryAnchorTokenSteps { get; set; }

    internal ulong VmOtherTokenSteps { get; set; }

    internal ulong VmBranchSteps { get; set; }

    internal ulong VmRepeatSteps { get; set; }

    internal ulong VmRepeatEnterSteps { get; set; }

    internal ulong VmPossessiveTokenScanSteps { get; set; }

    internal ulong VmPossessiveTokenScanCharacters { get; set; }

    internal ulong VmRepeatEndSteps { get; set; }

    internal ulong VmRepeatExitSteps { get; set; }

    internal ulong VmCaptureSteps { get; set; }

    internal ulong VmAssertionSubroutineSteps { get; set; }

    internal ulong VmControlSteps { get; set; }

    internal ulong VmAcceptSteps { get; set; }

    internal ulong ResultProjections { get; set; }

    internal ulong WorkspacePoolRents { get; set; }

    internal ulong WorkspaceFixedRents { get; set; }

    internal ulong WorkspaceInitialStackRents { get; set; }

    internal ulong WorkspaceFrameRents { get; set; }

    internal ulong WorkspaceRepeatMutationRents { get; set; }

    internal ulong WorkspaceCaptureMutationRents { get; set; }

    internal ulong WorkspaceControlRents { get; set; }

    internal ulong WorkspacePoolGrowths { get; set; }

    internal Pcre2ExecutionDiagnostics Diagnostics => new(
        CandidateAttempts: 0,
        BacktrackingSteps: 0,
        VmTokenSteps,
        VmLiteralTokenSteps,
        VmClassTokenSteps,
        VmBoundaryAnchorTokenSteps,
        VmOtherTokenSteps,
        VmBranchSteps,
        VmRepeatSteps,
        VmRepeatEnterSteps,
        VmPossessiveTokenScanSteps,
        VmPossessiveTokenScanCharacters,
        VmRepeatEndSteps,
        VmRepeatExitSteps,
        VmCaptureSteps,
        VmAssertionSubroutineSteps,
        VmControlSteps,
        VmAcceptSteps,
        ResultProjections,
        WorkspacePoolRents,
        WorkspaceFixedRents,
        WorkspaceInitialStackRents,
        WorkspaceFrameRents,
        WorkspaceRepeatMutationRents,
        WorkspaceCaptureMutationRents,
        WorkspaceControlRents,
        WorkspacePoolGrowths);
}

internal struct Pcre2ResourceBudget : IUtf8InvocationBufferControl
{
    // Controlled cursor copies share enforcement while their positions and
    // profile diagnostic counts stay local. Default unlimited runs stay inline.
    private Utf8SliceInvocation _invocation;
    private readonly Pcre2ExecutionDiagnosticsCollector? _diagnostics;

    internal Pcre2ResourceBudget(
        Utf8Pcre2ExecutionLimits limits,
        TimeSpan timeout,
        bool collectDiagnostics)
        : this(limits, new Utf8SliceInvocation(limits.ToSliceLimits(), timeout, CancellationToken.None), collectDiagnostics)
    {
    }

    internal Pcre2ResourceBudget(
        Utf8Pcre2ExecutionLimits limits,
        in Utf8SliceInvocation invocation,
        bool collectDiagnostics)
        : this(limits, invocation, collectDiagnostics ? new Pcre2ExecutionDiagnosticsCollector() : null)
    {
        // Profile-only callers use their own instruction checkpoint cadence.
        _invocation.CheckpointHook = null;
    }

    private Pcre2ResourceBudget(Utf8Pcre2ExecutionLimits limits,
        in Utf8SliceInvocation invocation, Pcre2ExecutionDiagnosticsCollector? diagnostics)
    {
        Limits = limits;
        _invocation = invocation;
        _diagnostics = diagnostics;
        CandidateSteps = 0;
        BacktrackingSteps = 0;
    }

    // One-shot dispatch can enter the shared VM using the same owner and hook.
    internal Pcre2ResourceBudget(Pcre2CompileRequest request,
        CancellationToken token, IUtf8MemoryAllowance? allowance = null)
    {
        Limits = request.DefaultLimits;
        Utf8SliceInvocation.InitializeExclusive(out _invocation, Limits.ToSliceLimits(), request.MatchTimeout, token, allowance);
    }

    internal Utf8Pcre2ExecutionLimits Limits { get; }

    // Required shaping borrows the execution owner, including its inline
    // unlimited path. Never copy this value into a new decoding invocation.
    [UnscopedRef]
    internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    internal ulong CandidateSteps { get; private set; }

    internal ulong BacktrackingSteps { get; private set; }


    internal ulong HeapBytes => _invocation.Budget.PeakWorkingBytes;

    internal bool CollectsDiagnostics => _diagnostics is not null;

    internal Pcre2ExecutionDiagnostics Diagnostics =>
        (_diagnostics?.Diagnostics ?? default) with
        {
            CandidateAttempts = CandidateSteps,
            BacktrackingSteps = BacktrackingSteps,
        };

    internal bool RequiresCandidateMetering => !_invocation.Budget.IsInfiniteDeadline || Limits.MatchLimit != 0;

    internal bool IsUnmetered => _invocation.Budget.IsUnmetered;

    // Short native work may never reach the periodic instruction poll. Check
    // the same absolute authority without changing candidate/work accounting.
    internal void CheckDeadline()
    {
        try { _invocation.Budget.CheckDeadline(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    internal void ChargeCandidate()
    {
        if (_diagnostics is not null) CandidateSteps++;
        Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.Budget.TryChargeStep());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ChargeBacktracking(in Pcre2BacktrackingInstruction instruction)
    {
        if (_diagnostics is { } diagnostics)
        {
            BacktrackingSteps++;
            diagnostics.RecordBacktrackingInstruction(instruction.Kind, instruction.Token.Kind);
        }
        Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.Budget.TryChargeStep());
    }

    internal void RecordPossessiveTokenScanCharacters(ulong characters)
    {
        if (_diagnostics is { } diagnostics)
        {
            diagnostics.VmPossessiveTokenScanCharacters += characters;
        }
    }

    internal void ChargeFrame(uint depth)
    {
        Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.Budget.TryNoteDepth(depth));
    }

    internal ulong ReservedWorkingBytes => _invocation.Ledger.ReservedBytes;

    internal bool CanRetainWorkspace => _invocation.HasUnlimitedWorkingMemory;

    internal void ReserveWorking(ulong bytes)
    {
        try { Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.TryReserveWorking(bytes)); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    internal void ReleaseWorking(ulong bytes) => _invocation.Ledger.Release(bytes);

    internal void ThrowWorkingLimit()
        => Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None
            ? _invocation.Budget.LimitBreach : Utf8ExecutionLimitKind.WorkingBytes);

    internal void ChargeWorkspaceStep()
    {
        if (_invocation.Budget.IsUnmetered) return;
        try
        {
            _invocation.Budget.CheckDeadline();
            Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.Budget.TryChargeStep());
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
    }

    bool IUtf8InvocationBufferControl.CanRetainWorkspace => CanRetainWorkspace;

    Utf8ExecutionLimitKind IUtf8InvocationBufferControl.TryReserveWorking(ulong bytes)
    {
        ReserveWorking(bytes);
        return Utf8ExecutionLimitKind.None;
    }

    void IUtf8InvocationBufferControl.ReleaseWorking(ulong bytes) => ReleaseWorking(bytes);

    Utf8ExecutionLimitKind IUtf8InvocationBufferControl.TryChargeWorkspaceStep()
    {
        ChargeWorkspaceStep();
        return Utf8ExecutionLimitKind.None;
    }

    Utf8ExecutionLimitKind IUtf8InvocationBufferControl.TryChargeResultBytes(ulong bytes)
    {
        Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.Budget.TryChargeResultBytes(bytes));
        return Utf8ExecutionLimitKind.None;
    }

    internal void RecordResultProjection()
    {
        if (_diagnostics is { } diagnostics)
        {
            diagnostics.ResultProjections++;
        }
    }

    internal void RecordWorkspacePoolTraffic(
        ulong fixedRents,
        ulong frameRents,
        ulong repeatMutationRents,
        ulong captureMutationRents,
        ulong controlRents,
        ulong stackGrowths)
    {
        if (_diagnostics is { } diagnostics)
        {
            var stackRents = frameRents + repeatMutationRents + captureMutationRents + controlRents;
            diagnostics.WorkspacePoolRents += stackRents + fixedRents;
            diagnostics.WorkspaceFixedRents += fixedRents;
            diagnostics.WorkspaceInitialStackRents += stackRents - stackGrowths;
            diagnostics.WorkspaceFrameRents += frameRents;
            diagnostics.WorkspaceRepeatMutationRents += repeatMutationRents;
            diagnostics.WorkspaceCaptureMutationRents += captureMutationRents;
            diagnostics.WorkspaceControlRents += controlRents;
            diagnostics.WorkspacePoolGrowths += stackGrowths;
        }
    }

}

internal readonly record struct Pcre2ExecutionDiagnostics(
    ulong CandidateAttempts,
    ulong BacktrackingSteps,
    ulong VmTokenSteps,
    ulong VmLiteralTokenSteps,
    ulong VmClassTokenSteps,
    ulong VmBoundaryAnchorTokenSteps,
    ulong VmOtherTokenSteps,
    ulong VmBranchSteps,
    ulong VmRepeatSteps,
    ulong VmRepeatEnterSteps,
    ulong VmPossessiveTokenScanSteps,
    ulong VmPossessiveTokenScanCharacters,
    ulong VmRepeatEndSteps,
    ulong VmRepeatExitSteps,
    ulong VmCaptureSteps,
    ulong VmAssertionSubroutineSteps,
    ulong VmControlSteps,
    ulong VmAcceptSteps,
    ulong ResultProjections,
    ulong WorkspacePoolRents,
    ulong WorkspaceFixedRents,
    ulong WorkspaceInitialStackRents,
    ulong WorkspaceFrameRents,
    ulong WorkspaceRepeatMutationRents,
    ulong WorkspaceCaptureMutationRents,
    ulong WorkspaceControlRents,
    ulong WorkspacePoolGrowths);
