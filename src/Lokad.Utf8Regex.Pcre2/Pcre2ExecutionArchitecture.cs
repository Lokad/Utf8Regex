using System.Diagnostics.CodeAnalysis;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

internal readonly record struct Pcre2CompileRequest(
    string Pattern,
    Pcre2CompileOptions Options,
    Utf8Pcre2CompileSettings Settings,
    Utf8Pcre2ExecutionLimits DefaultLimits,
    TimeSpan MatchTimeout);

internal delegate Pcre2CompiledProgram Pcre2FoundationProgramFactory(Pcre2CompileRequest request);

internal static class Pcre2Compiler
{
    internal static Pcre2CompiledProgram Compile(Pcre2CompileRequest request, Pcre2FoundationProgramFactory foundationProgramFactory)
    {
        Pcre2CompileValidator.ValidatePattern(request.Pattern, request.Options, request.Settings);
        if (Pcre2LiteralCompiler.Compile(request) is Pcre2CompiledLiteralOutcome literal)
        {
            var literalProgram = foundationProgramFactory(request);
            literalProgram = Pcre2CompiledProgramOverlay.WithLiteralOneShot(literalProgram, literal.SyntaxTree, literal.Program);
            Pcre2ProgramInvariant.Validate(literalProgram);
            return literalProgram;
        }

        if (Pcre2CharacterCompiler.Compile(request) is Pcre2CompiledCharacterOutcome character)
        {
            Pcre2CompiledProgram foundation;
            try
            {
                foundation = foundationProgramFactory(request);
            }
            catch (RegexParseException)
            {
                foundation = Pcre2CompiledProgramOverlay.CreateGenericFoundation(request);
            }

            foundation = Pcre2CompiledProgramOverlay.WithCharacterOneShot(foundation, character.SyntaxTree, character.Program);
            Pcre2ProgramInvariant.Validate(foundation);
            return foundation;
        }

        if (Pcre2BacktrackingCompiler.Compile(request) is Pcre2CompiledBacktrackingOutcome backtracking)
        {
            Pcre2CompiledProgram foundation;
            if (backtracking.RequiresProfileOptions)
            {
                // Inline x/xx, J and reset directives need PCRE2 option semantics. The owned
                // profile already compiled them; omit incompatible Core preparation.
                foundation = Pcre2CompiledProgramOverlay.CreateGenericFoundation(request);
            }
            else
            {
                try
                {
                    foundation = foundationProgramFactory(request);
                }
                catch (RegexParseException)
                {
                    foundation = Pcre2CompiledProgramOverlay.CreateGenericFoundation(request);
                }
            }

            foundation = Pcre2CompiledProgramOverlay.WithBacktracking(
                foundation,
                backtracking.SyntaxTree,
                backtracking.Program);
            foundation = Pcre2CompiledProgramOverlay.WithSlice(foundation);
            Pcre2ProgramInvariant.Validate(foundation);
            return foundation;
        }

        var program = foundationProgramFactory(request);
        Pcre2ProgramInvariant.Validate(program);
        return program;
    }
}

internal interface IPcre2Utf8ProgramSlot
{
    bool IsPresent { get; }
}

internal sealed class Pcre2EmptyUtf8ProgramSlot : IPcre2Utf8ProgramSlot
{
    internal static Pcre2EmptyUtf8ProgramSlot Instance { get; } = new();

    private Pcre2EmptyUtf8ProgramSlot()
    {
    }

    public bool IsPresent => false;
}

internal sealed class Pcre2Utf8ProgramSlot : IPcre2Utf8ProgramSlot
{
    internal Pcre2Utf8ProgramSlot(Utf8Regex regex)
    {
        Regex = regex;
    }

    public bool IsPresent => true;

    internal Utf8Regex Regex { get; }

}

internal interface IPcre2DirectProgram
{
    Pcre2DirectProgramKind Kind { get; }
}

internal sealed class Pcre2NoDirectProgram : IPcre2DirectProgram
{
    internal static Pcre2NoDirectProgram Instance { get; } = new();

    private Pcre2NoDirectProgram()
    {
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.None;
}

internal sealed class Pcre2Utf8DirectProgram : IPcre2DirectProgram
{
    internal Pcre2Utf8DirectProgram(Utf8Regex regex)
    {
        Regex = regex;
        // Preserve the delegated context's numeric/name aliases as immutable
        // compile-time metadata instead of rebuilding the table per match.
        using var owner = Utf8ConstructionScope.Enter();
        var names = ScalarProgram.GroupNames;
        ContextNameEntries = owner.Budget.AllocateArray<Pcre2NameEntry>(names.Length, false);
        for (var index = 0; index < names.Length; index++)
            ContextNameEntries[index] = new Pcre2NameEntry { Name = names[index], Number = index };
        owner.Complete();
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Utf8Regex;

    internal Utf8Regex Regex { get; }

    internal Utf8SliceProgram ScalarProgram => Regex.Inspection.AvailableOwnedSliceProgram;

    internal Pcre2NameEntry[] ContextNameEntries { get; }
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

internal sealed class Pcre2CharacterDirectProgram : IPcre2DirectProgram
{
    internal Pcre2CharacterDirectProgram(Pcre2CharacterProgram program)
    {
        Program = program;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2Character;

    internal Pcre2CharacterProgram Program { get; }
}

internal sealed class Pcre2SingleTokenRepeatDirectProgram : IPcre2DirectProgram
{
    internal Pcre2SingleTokenRepeatDirectProgram(Pcre2SingleTokenRepeatProgram program)
    {
        Program = program;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2SingleTokenRepeat;

    internal Pcre2SingleTokenRepeatProgram Program { get; }
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
        Pcre2BacktrackingProgram fallback)
    {
        SliceProgram = sliceProgram;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2Slice;

    internal Utf8SliceProgram SliceProgram { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
}

internal sealed class Pcre2AsciiRegularIsMatchDirectProgram : IPcre2DirectProgram
{
    internal Pcre2AsciiRegularIsMatchDirectProgram(
        Utf8Regex regex,
        bool supportsPreparedOffsets,
        Pcre2BacktrackingProgram fallback)
    {
        Regex = regex;
        SupportsPreparedOffsets = supportsPreparedOffsets;
        Fallback = fallback;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2AsciiRegularIsMatch;

    internal Utf8Regex Regex { get; }

    internal bool SupportsPreparedOffsets { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }
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
        var minimal = new List<byte[]>(alternatives.Length);
        foreach (var alternative in alternatives)
        {
            var covered = alternatives.Any(candidate =>
                !ReferenceEquals(candidate, alternative) &&
                candidate.Length <= alternative.Length &&
                alternative.AsSpan().StartsWith(candidate));
            if (!covered)
            {
                minimal.Add(alternative);
            }
        }

        _alternatives = minimal.ToArray();
        _leadingBytes = _alternatives.Select(static alternative => alternative[0]).Distinct().ToArray();
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
    None = 0,
    Utf8Regex = 1,
    Pcre2Literal = 3,
    Pcre2Character = 4,
    Pcre2Backtracking = 5,
    Pcre2LiteralFamily = 6,
    Pcre2SingleTokenRepeat = 7,
    Pcre2AsciiRegularIsMatch = 8,
    Pcre2MultilinePrefix = 9,
    Pcre2AsciiOrderedLiteralWindow = 10,
    Pcre2AsciiLiteralAlternationRepeat = 11,
    Pcre2SameStartLiteral = 12,
    Pcre2LiteralPrefixRepeat = 13,
    Pcre2PalindromeIsMatch = 14,
    Pcre2LeadingDotStarLiteralIsMatch = 15,
    Pcre2SeparatedRunsIsMatch = 16,
    Pcre2AsciiBoundedIsMatch = 17,
    Pcre2AsciiAnchoredDfaIsMatch = 18,
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
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Pcre2CandidateSearchProgram>();

    private Pcre2CandidateSearchProgram(
        Pcre2CandidateSearchKind kind,
        PreparedSearcher searcher,
        Pcre2CharacterToken[] leadingRunTokens,
        Pcre2CandidateWindowConstraint windowConstraint,
        int minimumWordRunLength)
    {
        Kind = kind;
        Searcher = searcher;
        LeadingRunTokens = leadingRunTokens;
        WindowConstraint = windowConstraint;
        MinimumWordRunLength = minimumWordRunLength;
    }

    internal Pcre2CandidateSearchKind Kind { get; }

    internal PreparedSearcher Searcher { get; }

    internal Pcre2CharacterToken[] LeadingRunTokens { get; }

    internal Pcre2CandidateWindowConstraint WindowConstraint { get; }

    internal int MinimumWordRunLength { get; }

    internal bool HasValue => Kind != Pcre2CandidateSearchKind.None;

    internal static Pcre2CandidateSearchProgram FromBranchLeadingLiterals(byte[][] literals) =>
        new(
            Pcre2CandidateSearchKind.BranchLeadingLiterals,
            literals.Length == 1
                ? new PreparedSearcher(new PreparedSubstringSearch(literals[0], ignoreCase: false), ignoreCase: false)
                : new PreparedSearcher(new PreparedMultiLiteralSearch(literals, ignoreCase: false)),
            [],
            default,
            0);

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
        Pcre2CandidateWindowConstraint windowConstraint) =>
        new(
            Pcre2CandidateSearchKind.BoundedLiteralWindow,
            new PreparedSearcher(
                new PreparedSubstringSearch(windowConstraint.Literal, ignoreCase: false),
                ignoreCase: false),
            [],
            windowConstraint,
            0);

    internal static Pcre2CandidateSearchProgram FromLeadingAsciiWordBoundaryRun(int minimumWordRunLength) =>
        new(
            Pcre2CandidateSearchKind.LeadingAsciiWordBoundaryRun,
            default,
            [],
            default,
            minimumWordRunLength);
}

internal sealed class Pcre2CandidateSearchPlan
{
    internal Pcre2CandidateSearchPlan(Pcre2CandidateSearchProgram program)
    {
        Program = program;
    }

    internal Pcre2CandidateSearchProgram Program { get; }
}

internal sealed class Pcre2CompiledProgram
{
    internal Pcre2CompiledProgram(
        Pcre2CompileRequest request,
        IPcre2Utf8ProgramSlot primaryUtf8,
        Pcre2OperationPrograms operations,
        Pcre2CandidateSearchProgram candidateSearch,
        Pcre2PartialProbeProgram partialProbe,
        IPcre2SyntaxTree syntaxTree,
        string[] groupNames,
        Pcre2NameEntry[] nameEntries)
    {
        Request = request;
        PrimaryUtf8 = primaryUtf8;
        Operations = operations;
        CandidateSearchPlan = new Pcre2CandidateSearchPlan(candidateSearch);
        PartialProbe = partialProbe;
        SyntaxTree = syntaxTree;
        GroupNames = [.. groupNames];
        NameEntries = [.. nameEntries];
    }

    internal Pcre2CompileRequest Request { get; }

    internal IPcre2Utf8ProgramSlot PrimaryUtf8 { get; }

    internal Pcre2OperationPrograms Operations { get; }

    internal Pcre2CandidateSearchProgram CandidateSearch => CandidateSearchPlan.Program;

    internal Pcre2CandidateSearchPlan CandidateSearchPlan { get; }

    internal Pcre2PartialProbeProgram PartialProbe { get; }

    internal IPcre2SyntaxTree SyntaxTree { get; }

    internal string[] GroupNames { get; }

    internal Pcre2NameEntry[] NameEntries { get; }
}

internal static class Pcre2CompiledProgramOverlay
{
    internal static Pcre2CompiledProgram CreateGenericFoundation(Pcre2CompileRequest request)
    {
        var none = Pcre2NoDirectProgram.Instance;
        return new Pcre2CompiledProgram(
            request,
            Pcre2EmptyUtf8ProgramSlot.Instance,
            new Pcre2OperationPrograms(none, none, none, none, none),
            default,
            Pcre2PartialProbeProgram.None,
            Pcre2LegacySyntaxTree.Instance,
            ["0"],
            []);
    }

    internal static Pcre2CompiledProgram WithLiteralOneShot(
        Pcre2CompiledProgram legacy,
        Pcre2LiteralSyntaxTree syntaxTree,
        Pcre2LiteralProgram literalProgram)
    {
        var direct = new Pcre2LiteralDirectProgram(literalProgram);
        var operations = legacy.Operations with
        {
            IsMatch = direct,
            Count = direct,
            Enumerate = direct,
            Match = direct,
            Replace = direct,
        };
        return new Pcre2CompiledProgram(
            legacy.Request,
            legacy.PrimaryUtf8,
            operations,
            default,
            Pcre2PartialProbeProgram.None,
            syntaxTree,
            legacy.GroupNames,
            legacy.NameEntries);
    }

    internal static Pcre2CompiledProgram WithCharacterOneShot(
        Pcre2CompiledProgram legacy,
        Pcre2CharacterSyntaxTree syntaxTree,
        Pcre2CharacterProgram characterProgram)
    {
        var direct = new Pcre2CharacterDirectProgram(characterProgram);
        var operations = legacy.Operations with
        {
            IsMatch = direct,
            Count = direct,
            Enumerate = direct,
            Match = direct,
            Replace = direct,
        };
        return new Pcre2CompiledProgram(
            legacy.Request,
            legacy.PrimaryUtf8,
            operations,
            default,
            Pcre2PartialProbeCompiler.Compile(characterProgram, legacy.Request),
            syntaxTree,
            legacy.GroupNames,
            legacy.NameEntries);
    }

    internal static Pcre2CompiledProgram WithBacktracking(
        Pcre2CompiledProgram legacy,
        Pcre2BacktrackingSyntaxTree syntaxTree,
        Pcre2BacktrackingProgram backtrackingProgram)
    {
        var direct = new Pcre2BacktrackingDirectProgram(backtrackingProgram);
        var operations = legacy.Operations with
        {
            IsMatch = direct,
            Count = direct,
            Enumerate = direct,
            Match = direct,
            Replace = direct,
        };
        if (Pcre2MultilinePrefixAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                backtrackingProgram) is { } multilinePrefix)
        {
            operations = operations with
            {
                IsMatch = multilinePrefix,
                Count = multilinePrefix,
                Enumerate = multilinePrefix,
            };
        }
        else if (Pcre2SameStartLiteralAnalyzer.TryCompile(
                     syntaxTree.Root,
                     legacy.Request,
                     backtrackingProgram) is { } sameStartLiteral)
        {
            operations = operations with
            {
                Count = sameStartLiteral,
                Enumerate = sameStartLiteral,
            };
        }
        else if (Pcre2LiteralPrefixRepeatAnalyzer.TryCompile(
                     syntaxTree.Root,
                     legacy.Request,
                     backtrackingProgram) is { } literalPrefixRepeat)
        {
            operations = operations with
            {
                IsMatch = literalPrefixRepeat,
                Count = literalPrefixRepeat,
                Enumerate = literalPrefixRepeat,
            };
        }
        else if (legacy.PrimaryUtf8 is Pcre2Utf8ProgramSlot orderedWindowPrimary &&
            Pcre2AsciiOrderedLiteralWindowAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                orderedWindowPrimary.Regex,
                backtrackingProgram) is { } orderedWindow)
        {
            operations = operations with
            {
                Count = orderedWindow,
            };
        }
        else if (legacy.PrimaryUtf8 is Pcre2Utf8ProgramSlot alternationRepeatPrimary &&
            Pcre2AsciiLiteralAlternationRepeatAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                alternationRepeatPrimary.Regex,
                backtrackingProgram) is { } alternationRepeat)
        {
            operations = operations with
            {
                Count = alternationRepeat,
            };
        }
        else if (legacy.PrimaryUtf8 is Pcre2Utf8ProgramSlot primary &&
            Pcre2LiteralFamilyAnalyzer.CanReuseCoreExecution(
                syntaxTree.Root,
                legacy.Request,
                primary.Regex.ByteOffsetExecution.SearchPortfolioKind))
        {
            var literalFamily = new Pcre2LiteralFamilyDirectProgram(primary.Regex, backtrackingProgram);
            operations = operations with
            {
                IsMatch = literalFamily,
                Count = literalFamily,
                Enumerate = literalFamily,
            };
        }
        else if (Pcre2FiniteLiteralLanguageAnalyzer.TryCompile(
                     syntaxTree.Root,
                     legacy.Request) is { } finiteLiteralCompilation)
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
        else if (Pcre2SingleTokenRepeatAnalyzer.TryCompile(
                     syntaxTree.Root,
                     backtrackingProgram) is { } singleTokenRepeatProgram)
        {
            var singleTokenRepeat = new Pcre2SingleTokenRepeatDirectProgram(singleTokenRepeatProgram);
            operations = new Pcre2OperationPrograms(
                singleTokenRepeat,
                singleTokenRepeat,
                singleTokenRepeat,
                singleTokenRepeat,
                singleTokenRepeat);
        }

        if (operations.IsMatch is Pcre2BacktrackingDirectProgram &&
            Pcre2PalindromeIsMatchAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                backtrackingProgram) is { } palindrome)
        {
            operations = operations with
            {
                IsMatch = palindrome,
            };
        }

        if (operations.IsMatch is Pcre2BacktrackingDirectProgram &&
            Pcre2LeadingDotStarLiteralIsMatchAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                backtrackingProgram) is { } leadingDotStarLiteral)
        {
            operations = operations with
            {
                IsMatch = leadingDotStarLiteral,
            };
        }

        if (operations.IsMatch is Pcre2BacktrackingDirectProgram &&
            Pcre2SeparatedRunsIsMatchAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                backtrackingProgram) is { } separatedRuns)
        {
            operations = operations with
            {
                IsMatch = separatedRuns,
            };
        }

        if (operations.IsMatch is Pcre2BacktrackingDirectProgram &&
            Pcre2AsciiBoundedIsMatchAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                backtrackingProgram) is { } asciiBounded)
        {
            operations = operations with
            {
                IsMatch = asciiBounded,
            };
        }

        if (operations.IsMatch is Pcre2BacktrackingDirectProgram &&
            legacy.PrimaryUtf8 is Pcre2Utf8ProgramSlot asciiRegularPrimary &&
            Pcre2AsciiRegularIsMatchAnalyzer.CanReuseCoreExecution(
                syntaxTree.Root,
                legacy.Request,
                asciiRegularPrimary.Regex))
        {
            var supportsPreparedOffsets =
                Pcre2BacktrackingAnalysis.RestrictsSearchToInitialCandidate(syntaxTree.Root);
            operations = operations with
            {
                IsMatch = new Pcre2AsciiRegularIsMatchDirectProgram(
                    asciiRegularPrimary.Regex,
                    supportsPreparedOffsets,
                    backtrackingProgram),
            };
        }

        if (operations.IsMatch is Pcre2BacktrackingDirectProgram &&
            Pcre2AsciiAnchoredDfaIsMatchAnalyzer.TryCompile(
                syntaxTree.Root,
                legacy.Request,
                backtrackingProgram) is { } asciiAnchoredDfa)
        {
            operations = operations with
            {
                IsMatch = asciiAnchoredDfa,
            };
        }

        var candidateSearch = Pcre2CandidateSearchAnalyzer.Compile(syntaxTree.Root, legacy.Request);
        return new Pcre2CompiledProgram(
            legacy.Request,
            legacy.PrimaryUtf8,
            operations,
            candidateSearch,
            Pcre2PartialProbeCompiler.Compile(syntaxTree, legacy.Request),
            syntaxTree,
            backtrackingProgram.GroupNames,
            backtrackingProgram.NameEntries);
    }

    // R02a slice routing for IsMatch, Count, and first-match Match.
    // Detailed results, enumeration, and replacement stay on the
    // replaced backtracking programs until their owned projections
    // land. Eligibility piggybacks the foundation core instance: its
    // owned slice program already encodes ASCII-only literals,
    // numbered captures, ordered alternation, supported repeats, and
    // case-sensitive options. The explicit option gate additionally
    // declines whole-pattern search-domain constraints (Anchored,
    // EndAnchored, FirstLine), greediness inversion (Ungreedy),
    // capture renumbering (NoAutoCapture), and unreviewed case
    // folding (Caseless), even though the core parser currently
    // declines the latter families on its own. Multiline,
    // DollarEndOnly, and Ucp are inert: no anchor, class, or
    // boundary construct can appear in a core slice program, so they
    // cannot change slice-shape semantics. DotAll is folded into the
    // core program newline rule at construction, so it needs no gate
    // here. Dots need a newline-convention rule:
    // only Default (line feed) and Lf exclude exactly newline like the
    // shared runner, so every other convention declines. Word boundaries
    // decline unconditionally below: the shared runner applies Unicode word
    // semantics while the native boundary stays ASCII-scoped (UCP parity is
    // future work). Shorthand classes decline for the same reason: the
    // shared runner applies Unicode digits, word chars, and separators
    // while the native shorthands stay ASCII-scoped. Variable-length
    // Conditional expression tests decline because native PCRE2 flows assertion
    // captures (even from negative tests) while the shared runner follows BCL
    // (failed negatives leak nothing; group tests commit once). Group-test
    // conditionals keep shared execution (captures flow identically).
    // lookbehind declines because native PCRE2 rejects it at compile time
    // while the shared runner would match (fixed-length lookbehind agrees
    // on case-sensitive ASCII shapes). Mixed named/unnamed shapes decline
    // because native PCRE2 numbers captures positionally while BCL numbers
    // unnamed groups first (all-named shapes agree). Case-insensitive programs
    // decline because native caseless folds UCP-wide (sigma matches final sigma)
    // while the shared runner applies BCL-invariant per-scalar folding. Category
    // programs decline because named blocks stay natively unsupported (the
    // construction vocabulary is explicit None/NotSupported) while general
    // categories keep their native single-token programs. Extended and ExtendedMore
    // decline through the core parser, which rejects
    // IgnorePatternWhitespace.
    internal static Pcre2CompiledProgram WithSlice(
        Pcre2CompiledProgram program)
    {
        if (program.PrimaryUtf8 is not Pcre2Utf8ProgramSlot primary ||
            primary.Regex.Inspection.OwnedSliceProgram is not { } sliceProgram ||
            (program.Request.Options & SliceIncompatibleOptions) != 0 ||
            (program.Request.Settings.Newline is not Pcre2NewlineConvention.Default &&
             program.Request.Settings.Newline is not Pcre2NewlineConvention.Lf) ||
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
            return program;
        }

        // Lazy-gap windows keep the faithful backtracking engine: the shared slice
        // runner plus the greedy window executor cannot reproduce lazy tail selection.
        if (program.SyntaxTree is Pcre2BacktrackingSyntaxTree backtrackingSyntax &&
            Pcre2AsciiOrderedLiteralWindowAnalyzer.IsLazyGapWindow(backtrackingSyntax.Root))
        {
            return program;
        }

        var operations = program.Operations;
        if (operations.IsMatch is Pcre2BacktrackingDirectProgram isMatchBacktracking)
        {
            operations = operations with
            {
                IsMatch = new Pcre2SliceDirectProgram(
                    sliceProgram,
                    isMatchBacktracking.Program),
            };
        }

        if (operations.Count is Pcre2BacktrackingDirectProgram countBacktracking)
        {
            operations = operations with
            {
                Count = new Pcre2SliceDirectProgram(
                    sliceProgram,
                    countBacktracking.Program),
            };
        }

        if (operations.Match is Pcre2BacktrackingDirectProgram matchBacktracking)
        {
            operations = operations with
            {
                Match = new Pcre2SliceDirectProgram(
                    sliceProgram,
                    matchBacktracking.Program),
            };
        }

        if (operations.Enumerate is Pcre2BacktrackingDirectProgram enumerateBacktracking)
        {
            operations = operations with
            {
                Enumerate = new Pcre2SliceDirectProgram(
                    sliceProgram,
                    enumerateBacktracking.Program),
            };
        }

        if (operations.Replace is Pcre2BacktrackingDirectProgram replaceBacktracking)
        {
            operations = operations with
            {
                Replace = new Pcre2SliceDirectProgram(
                    sliceProgram,
                    replaceBacktracking.Program),
            };
        }

        return new Pcre2CompiledProgram(
            program.Request,
            program.PrimaryUtf8,
            operations,
            program.CandidateSearch,
            program.PartialProbe,
            program.SyntaxTree,
            program.GroupNames,
            program.NameEntries);
    }

    // Extended and ExtendedMore decline: native extended skips vertical tab while
    // BCL IgnorePatternWhitespace keeps it significant (probed), so the shared
    // runner would disagree on such patterns.
    private const Pcre2CompileOptions SliceIncompatibleOptions =
        Pcre2CompileOptions.Caseless |
        Pcre2CompileOptions.Extended |
        Pcre2CompileOptions.ExtendedMore |
        Pcre2CompileOptions.Anchored |
        Pcre2CompileOptions.EndAnchored |
        Pcre2CompileOptions.Ungreedy |
        Pcre2CompileOptions.NoAutoCapture |
        Pcre2CompileOptions.FirstLine;
}

internal sealed class Pcre2SingleTokenRepeatProgram
{
    internal Pcre2SingleTokenRepeatProgram(
        Pcre2CharacterToken token,
        int minimum,
        int maximum,
        Pcre2RepeatPreference preference,
        Pcre2BacktrackingProgram fallback)
    {
        Token = token;
        Minimum = minimum;
        Maximum = maximum;
        Preference = preference;
        Fallback = fallback;
        LeadingAsciiByte = token.Kind == Pcre2CharacterTokenKind.Literal &&
            token.Literal.IsAscii &&
            (token.Options & Pcre2CharacterOptions.Caseless) == 0
                ? (byte)token.Literal.Value
                : null;
        GreedyExcludedAsciiByte = TryGetGreedyExcludedAsciiByte(token, minimum, maximum, preference);
    }

    internal Pcre2CharacterToken Token { get; }

    internal int Minimum { get; }

    internal int Maximum { get; }

    internal Pcre2RepeatPreference Preference { get; }

    internal Pcre2BacktrackingProgram Fallback { get; }

    internal Pcre2CompileRequest Request => Fallback.Request;

    internal byte? LeadingAsciiByte { get; }

    internal byte? GreedyExcludedAsciiByte { get; }

    internal bool UsesCodeUnit => Token.Kind == Pcre2CharacterTokenKind.CodeUnit;

    internal bool CanCountGreedyExcludedAsciiDirectly =>
        GreedyExcludedAsciiByte.HasValue &&
        (Request.Options &
            (Pcre2CompileOptions.Anchored |
             Pcre2CompileOptions.EndAnchored |
             Pcre2CompileOptions.FirstLine)) == 0;

    private static byte? TryGetGreedyExcludedAsciiByte(
        Pcre2CharacterToken token,
        int minimum,
        int maximum,
        Pcre2RepeatPreference preference)
    {
        if (minimum != 0 ||
            maximum != int.MaxValue ||
            preference == Pcre2RepeatPreference.Lazy ||
            token.Kind != Pcre2CharacterTokenKind.CharacterClass ||
            (token.Options & Pcre2CharacterOptions.Caseless) != 0 ||
            !token.CharacterClass.Negated ||
            token.CharacterClass.Terms is not
            [
                {
                    Kind: Pcre2CharacterClassTermKind.Range,
                    Negated: false,
                    Range: { Low: var low, High: var high },
                },
            ] ||
            low != high ||
            (uint)low > 0x7F)
        {
            return null;
        }

        return (byte)low;
    }
}

internal static class Pcre2SingleTokenRepeatAnalyzer
{
    internal static Pcre2SingleTokenRepeatProgram? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2BacktrackingProgram fallback)
    {
        if (root is Pcre2RepeatBacktrackingNode
            {
                Body: Pcre2TokenBacktrackingNode token,
            } repeat &&
            IsConsuming(token.Token.Kind))
        {
            return new Pcre2SingleTokenRepeatProgram(
                token.Token,
                repeat.Minimum,
                repeat.Maximum,
                repeat.Preference,
                fallback);
        }

        return null;
    }

    private static bool IsConsuming(Pcre2CharacterTokenKind kind) => kind is
        Pcre2CharacterTokenKind.Literal or
        Pcre2CharacterTokenKind.CharacterClass or
        Pcre2CharacterTokenKind.Any or
        Pcre2CharacterTokenKind.AnyNotNewline or
        Pcre2CharacterTokenKind.NewlineSequence or
        Pcre2CharacterTokenKind.ExtendedGraphemeCluster or
        Pcre2CharacterTokenKind.CodeUnit;
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
        if (directProgram is Pcre2Utf8DirectProgram utf8Program)
        {
            var isOwnedUtf8Program = program.PrimaryUtf8 is Pcre2Utf8ProgramSlot primary &&
                ReferenceEquals(primary.Regex, utf8Program.Regex);
            if (!isOwnedUtf8Program)
            {
                throw new InvalidOperationException("A direct UTF-8 backend must be owned by its compiled PCRE2 program.");
            }
        }

        if (directProgram is Pcre2SliceDirectProgram sliceProgram)
        {
            var isOwnedSliceProgram = program.PrimaryUtf8 is Pcre2Utf8ProgramSlot slicePrimary &&
                ReferenceEquals(
                    slicePrimary.Regex.Inspection.OwnedSliceProgram,
                    sliceProgram.SliceProgram);
            if (!isOwnedSliceProgram)
            {
                throw new InvalidOperationException("A direct slice backend must be owned by its compiled PCRE2 program.");
            }
        }

        if (directProgram is Pcre2AsciiRegularIsMatchDirectProgram asciiRegularProgram &&
            (program.PrimaryUtf8 is not Pcre2Utf8ProgramSlot asciiRegularPrimary ||
             !ReferenceEquals(asciiRegularPrimary.Regex, asciiRegularProgram.Regex)))
        {
            throw new InvalidOperationException("An ASCII-regular IsMatch backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2AsciiOrderedLiteralWindowDirectProgram orderedWindowProgram &&
            (program.PrimaryUtf8 is not Pcre2Utf8ProgramSlot orderedWindowPrimary ||
             !ReferenceEquals(orderedWindowPrimary.Regex, orderedWindowProgram.Regex)))
        {
            throw new InvalidOperationException("An ASCII ordered-window backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2AsciiLiteralAlternationRepeatDirectProgram alternationRepeatProgram &&
            (program.PrimaryUtf8 is not Pcre2Utf8ProgramSlot alternationRepeatPrimary ||
             !ReferenceEquals(alternationRepeatPrimary.Regex, alternationRepeatProgram.Regex)))
        {
            throw new InvalidOperationException("An ASCII literal-alternation repeat backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2LiteralFamilyDirectProgram literalFamilyProgram &&
            directProgram is not Pcre2FiniteLiteralLanguageDirectProgram &&
            (program.PrimaryUtf8 is not Pcre2Utf8ProgramSlot literalFamilyPrimary ||
             !ReferenceEquals(literalFamilyPrimary.Regex, literalFamilyProgram.Regex)))
        {
            throw new InvalidOperationException("A literal-family backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2SameStartLiteralDirectProgram sameStartLiteralProgram &&
            !IsOwnedIsMatchFallback(program, sameStartLiteralProgram.Fallback))
        {
            throw new InvalidOperationException("A same-start literal backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2LiteralPrefixRepeatDirectProgram literalPrefixRepeatProgram &&
            !IsOwnedMatchFallback(program, literalPrefixRepeatProgram.Fallback))
        {
            throw new InvalidOperationException("A literal-prefix repeat backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2PalindromeIsMatchDirectProgram palindromeProgram &&
            !IsOwnedMatchFallback(program, palindromeProgram.Fallback))
        {
            throw new InvalidOperationException("A palindrome IsMatch backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2LeadingDotStarLiteralIsMatchDirectProgram leadingDotStarLiteralProgram &&
            !IsOwnedMatchFallback(program, leadingDotStarLiteralProgram.Fallback))
        {
            throw new InvalidOperationException("A leading dot-star literal IsMatch backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2SeparatedRunsIsMatchDirectProgram separatedRunsProgram &&
            !IsOwnedMatchFallback(program, separatedRunsProgram.Fallback))
        {
            throw new InvalidOperationException("A separated-runs IsMatch backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2AsciiBoundedIsMatchDirectProgram asciiBoundedProgram &&
            !IsOwnedMatchFallback(program, asciiBoundedProgram.Fallback))
        {
            throw new InvalidOperationException("A bounded ASCII IsMatch backend must be owned by its compiled PCRE2 program.");
        }

        if (directProgram is Pcre2AsciiAnchoredDfaIsMatchDirectProgram asciiAnchoredDfaProgram &&
            !IsOwnedMatchFallback(program, asciiAnchoredDfaProgram.Fallback))
        {
            throw new InvalidOperationException("An anchored ASCII DFA IsMatch backend must be owned by its compiled PCRE2 program.");
        }
    }
}

internal static class Pcre2AsciiRegularIsMatchAnalyzer
{
    internal static bool CanReuseCoreExecution(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        Utf8Regex regex)
    {
        return request.Options == Pcre2CompileOptions.None &&
            request.Settings.Newline == Pcre2NewlineConvention.Default &&
            request.Settings.Bsr == Pcre2BsrConvention.Default &&
            !request.Settings.AllowDuplicateNames &&
            request.Settings.BackslashC == Pcre2BackslashCPolicy.Forbid &&
            !request.Settings.AllowLookaroundBackslashK &&
            IsSupportedNode(root) &&
            (Pcre2BacktrackingAnalysis.RestrictsSearchToInitialCandidate(root) ||
             regex.Inspection.PreparedRegex.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch);
    }

    internal static bool IsSupportedNode(IPcre2BacktrackingNode node) => node switch
    {
        Pcre2EmptyBacktrackingNode => true,
        Pcre2TokenBacktrackingNode { Token: var token } => IsSupportedToken(token),
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.All(IsSupportedNode),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.All(IsSupportedNode),
        Pcre2RepeatBacktrackingNode { Preference: not Pcre2RepeatPreference.Possessive } repeat =>
            IsSupportedNode(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => IsSupportedNode(capture.Body),
        _ => false,
    };

    internal static bool IsSupportedToken(Pcre2CharacterToken token)
    {
        if (token.Options != Pcre2CharacterOptions.None)
        {
            return false;
        }

        return token.Kind switch
        {
            Pcre2CharacterTokenKind.Literal => token.Literal.IsAscii,
            Pcre2CharacterTokenKind.CharacterClass =>
                !token.CharacterClass.Negated &&
                token.CharacterClass.Terms.All(static term =>
                    !term.Negated &&
                    term.Kind == Pcre2CharacterClassTermKind.Range &&
                    term.Range.High < 128),
            Pcre2CharacterTokenKind.Any or Pcre2CharacterTokenKind.AnyNotNewline => true,
            Pcre2CharacterTokenKind.BeginningOfLine or Pcre2CharacterTokenKind.EndOfLine => true,
            _ => false,
        };
    }
}

internal static class Pcre2FiniteLiteralLanguageAnalyzer
{
    private const int MaximumAlternatives = 64;
    private const int MaximumLiteralCharacters = 4096;

    private sealed record Pcre2FiniteLiteralCapture(string Name, string Value);

    private sealed record Pcre2FiniteLiteralState(
        string Value,
        int ReportedStartCharacterIndex,
        Dictionary<int, Pcre2FiniteLiteralCapture> Captures);

    internal static Pcre2FiniteLiteralLanguageCompilation? TryCompile(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request)
    {
        if (request.Options != Pcre2CompileOptions.None)
        {
            return null;
        }

        // Large or non-finite languages retain the original VM instead of
        // paying combinatorial construction cost for an auxiliary plan.
        var initialStates = new List<Pcre2FiniteLiteralState>
        {
            new(string.Empty, 0, []),
        };
        // An atomic group can be flattened only when it owns the root: without
        // a following continuation, no later failure can revisit its choice.
        var evaluationRoot = root is Pcre2AtomicBacktrackingNode rootAtomic
            ? rootAtomic.Body
            : root;
        if (!TryEvaluate(evaluationRoot, initialStates, out var states) ||
            states.Count == 0 ||
            states.Any(static state => state.Value.Length == 0))
        {
            return null;
        }

        var distinctValues = new HashSet<string>(StringComparer.Ordinal);
        var alternatives = new List<Pcre2FiniteLiteralState>(states.Count);
        foreach (var state in states)
        {
            if (distinctValues.Add(state.Value))
            {
                alternatives.Add(state);
            }
        }

        var alternativeBytes = alternatives
            .Select(static state => Encoding.UTF8.GetBytes(state.Value))
            .ToArray();
        var escapedAlternatives = alternatives
            .Select(static state => Regex.Escape(state.Value))
            .ToArray();
        var pattern = escapedAlternatives.Length == 1
            ? escapedAlternatives[0]
            : $"(?:{string.Join('|', escapedAlternatives)})";
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant, request.MatchTimeout);
        var boundaryProjection = alternatives.Any(static state => state.ReportedStartCharacterIndex != 0)
            ? new Pcre2FiniteLiteralBoundaryProjection(
                alternatives.Select((state, index) => new Pcre2FiniteLiteralBoundaryAlternative(
                    alternativeBytes[index],
                    Encoding.UTF8.GetByteCount(state.Value.AsSpan(0, state.ReportedStartCharacterIndex))))
                .ToArray())
            : null;
        return new Pcre2FiniteLiteralLanguageCompilation(
            regex,
            new Pcre2FiniteLiteralBooleanSearch(alternativeBytes),
            boundaryProjection);

        bool TryEvaluate(
            IPcre2BacktrackingNode node,
            List<Pcre2FiniteLiteralState> inputs,
            out List<Pcre2FiniteLiteralState> outputs)
        {
            outputs = [];
            switch (node)
            {
                case Pcre2TokenBacktrackingNode token
                    when token.Token.Kind == Pcre2CharacterTokenKind.Literal &&
                         token.Token.Options == Pcre2CharacterOptions.None:
                    var literal = token.Token.Literal.ToString();
                    foreach (var input in inputs)
                    {
                        if (input.Value.Length > MaximumLiteralCharacters - literal.Length)
                        {
                            return false;
                        }

                        outputs.Add(new Pcre2FiniteLiteralState(
                            input.Value + literal,
                            input.ReportedStartCharacterIndex,
                            input.Captures));
                    }
                    return true;

                case Pcre2SequenceBacktrackingNode sequence:
                    outputs = inputs;
                    foreach (var child in sequence.Children)
                    {
                        var childOutputs = new List<Pcre2FiniteLiteralState>();
                        foreach (var input in outputs)
                        {
                            if (!TryEvaluate(child, [input], out var inputOutputs))
                            {
                                return false;
                            }

                            childOutputs.AddRange(inputOutputs);
                            if (childOutputs.Count > MaximumAlternatives)
                            {
                                return false;
                            }
                        }

                        outputs = childOutputs;
                    }
                    return true;

                case Pcre2AlternationBacktrackingNode alternation:
                    foreach (var input in inputs)
                    {
                        foreach (var alternative in alternation.Alternatives)
                        {
                            if (!TryEvaluate(alternative, [input], out var alternativeOutputs))
                            {
                                return false;
                            }

                            outputs.AddRange(alternativeOutputs);
                            if (outputs.Count > MaximumAlternatives)
                            {
                                return false;
                            }
                        }
                    }
                    return true;

                case Pcre2RepeatBacktrackingNode repeat
                    when repeat.Maximum != int.MaxValue &&
                         repeat.Maximum <= MaximumAlternatives &&
                         repeat.Preference != Pcre2RepeatPreference.Possessive:
                    foreach (var input in inputs)
                    {
                        var statesByCount = new List<List<Pcre2FiniteLiteralState>>(repeat.Maximum + 1)
                        {
                            new() { input },
                        };
                        HashSet<int>? bodyRelativeLengths = repeat.Preference == Pcre2RepeatPreference.Greedy &&
                            repeat.Minimum != repeat.Maximum
                            ? new HashSet<int>()
                            : null;
                        for (var count = 1; count <= repeat.Maximum; count++)
                        {
                            var repeatedStates = new List<Pcre2FiniteLiteralState>();
                            foreach (var state in statesByCount[count - 1])
                            {
                                if (!TryEvaluate(repeat.Body, [state], out var bodyOutputs) ||
                                    bodyOutputs.Any(output => output.Value.Length == state.Value.Length))
                                {
                                    return false;
                                }

                                repeatedStates.AddRange(bodyOutputs);
                                if (count == 1 && bodyRelativeLengths is not null)
                                {
                                    foreach (var bodyOutput in bodyOutputs)
                                    {
                                        bodyRelativeLengths.Add(bodyOutput.Value.Length - state.Value.Length);
                                    }
                                }
                                if (repeatedStates.Count > MaximumAlternatives)
                                {
                                    return false;
                                }
                            }

                            statesByCount.Add(repeatedStates);
                        }

                        // R05: greedy ranged repeats over mixed-length derivations would
                        // project longest spans where PCRE2 stops ordered repetition at first
                        // success (probed: (?:a|ab){2,3} enum aaba vs oracle aa). Decline those
                        // shapes to the backtracking program (the slice overlay converts where
                        // it serves); exact counts resolve it1-major like backtracking, uniform
                        // derivations cannot diverge, and lazy starts at minimum like the oracle.
                        if (bodyRelativeLengths is { Count: > 1 })
                        {
                            return false;
                        }

                        var firstCount = repeat.Preference == Pcre2RepeatPreference.Greedy
                            ? repeat.Maximum
                            : repeat.Minimum;
                        var lastCount = repeat.Preference == Pcre2RepeatPreference.Greedy
                            ? repeat.Minimum
                            : repeat.Maximum;
                        var step = repeat.Preference == Pcre2RepeatPreference.Greedy ? -1 : 1;
                        for (var count = firstCount; ; count += step)
                        {
                            outputs.AddRange(statesByCount[count]);
                            if (outputs.Count > MaximumAlternatives)
                            {
                                return false;
                            }

                            if (count == lastCount)
                            {
                                break;
                            }
                        }
                    }
                    return true;

                case Pcre2CaptureBacktrackingNode capture:
                    foreach (var input in inputs)
                    {
                        if (!TryEvaluate(capture.Body, [input], out var captureOutputs))
                        {
                            return false;
                        }

                        foreach (var captureOutput in captureOutputs)
                        {
                            var captures = new Dictionary<int, Pcre2FiniteLiteralCapture>(captureOutput.Captures)
                            {
                                [capture.Slot] = new(
                                    capture.Name,
                                    captureOutput.Value[input.Value.Length..]),
                            };
                            outputs.Add(new Pcre2FiniteLiteralState(
                                captureOutput.Value,
                                captureOutput.ReportedStartCharacterIndex,
                                captures));
                            if (outputs.Count > MaximumAlternatives)
                            {
                                return false;
                            }
                        }
                    }
                    return true;

                case Pcre2BackreferenceBacktrackingNode backreference
                    when backreference.Options == Pcre2CharacterOptions.None:
                    foreach (var input in inputs)
                    {
                        var slot = backreference.Target.Kind switch
                        {
                            Pcre2BackreferenceTargetKind.Absolute => backreference.Target.Number,
                            Pcre2BackreferenceTargetKind.Relative when backreference.Target.Number > 0 =>
                                backreference.Target.CaptureCountAtReference + backreference.Target.Number,
                            Pcre2BackreferenceTargetKind.Relative =>
                                backreference.Target.CaptureCountAtReference + backreference.Target.Number + 1,
                            Pcre2BackreferenceTargetKind.Named => input.Captures
                                .Where(pair => string.Equals(
                                    pair.Value.Name,
                                    backreference.Target.Name,
                                    StringComparison.Ordinal))
                                .Select(static pair => pair.Key)
                                .DefaultIfEmpty()
                                .Min(),
                            _ => 0,
                        };
                        if (slot == 0 || !input.Captures.TryGetValue(slot, out var captured))
                        {
                            continue;
                        }

                        if (input.Value.Length > MaximumLiteralCharacters - captured.Value.Length)
                        {
                            return false;
                        }

                        outputs.Add(new Pcre2FiniteLiteralState(
                            input.Value + captured.Value,
                            input.ReportedStartCharacterIndex,
                            input.Captures));
                    }
                    return true;

                case Pcre2MatchBoundaryResetBacktrackingNode:
                    foreach (var input in inputs)
                    {
                        outputs.Add(input with { ReportedStartCharacterIndex = input.Value.Length });
                    }
                    return true;

                default:
                    return false;
            }
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
        if (request.Options != Pcre2CompileOptions.None ||
            root is not Pcre2AlternationBacktrackingNode { Alternatives.Length: >= 2 } alternation ||
            !IsExactLiteralFamilyPortfolio(portfolioKind))
        {
            return false;
        }

        return alternation.Alternatives.All(IsNonEmptyExactLiteral);
    }

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
        if (request.Options != Pcre2CompileOptions.None || ContainsUnsafeCandidateSemantics(root))
        {
            return default;
        }

        if (TryCompileBranchLeadingLiterals(root, out var branchPlan))
        {
            return branchPlan;
        }

        if (TryCompileLeadingRunThenLiteral(root, request, out var runPlan))
        {
            return runPlan;
        }

        if (TryCompileLeadingAsciiWordBoundaryRun(root, out var wordRunPlan))
        {
            return wordRunPlan;
        }

        if (TryCompileLeadingAsciiSet(root, request, out var asciiSetPlan))
        {
            return asciiSetPlan;
        }

        return TryGetInitialBoundedLiteralConstraint(root, out var windowConstraint)
            ? Pcre2CandidateSearchProgram.FromBoundedLiteralWindow(windowConstraint)
            : default;
    }

    private static bool TryCompileBranchLeadingLiterals(
        IPcre2BacktrackingNode root,
        out Pcre2CandidateSearchProgram plan)
    {
        var branches = root is Pcre2AlternationBacktrackingNode alternation
            ? alternation.Alternatives
            : [root];
        var literals = new byte[branches.Length][];
        for (var i = 0; i < branches.Length; i++)
        {
            if (!TryGetBranchLeadingLiteral(branches[i], out literals[i]))
            {
                plan = default;
                return false;
            }
        }

        plan = Pcre2CandidateSearchProgram.FromBranchLeadingLiterals(literals);
        return true;
    }

    private static bool TryCompileLeadingRunThenLiteral(
        IPcre2BacktrackingNode root,
        Pcre2CompileRequest request,
        out Pcre2CandidateSearchProgram plan)
    {
        plan = default;
        if (root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 2 } sequence)
        {
            return false;
        }

        var runTokens = new List<Pcre2CharacterToken>();
        var lastRunIsUnbounded = false;
        var childIndex = 0;
        while (childIndex < sequence.Children.Length &&
               sequence.Children[childIndex] is Pcre2RepeatBacktrackingNode
               {
                   Minimum: >= 1,
                   Maximum: var runMaximum,
                   Body: Pcre2TokenBacktrackingNode { Token: var runToken },
               } &&
               IsRetreatableRunToken(runToken))
        {
            if (runTokens.Count != 0 && !AreKnownDisjoint(runTokens[^1], runToken))
            {
                return false;
            }

            runTokens.Add(runToken);
            lastRunIsUnbounded = runMaximum == int.MaxValue;
            childIndex++;
        }

        if (runTokens.Count == 0)
        {
            return false;
        }

        var builder = new StringBuilder();
        for (var i = childIndex; i < sequence.Children.Length &&
             sequence.Children[i] is Pcre2TokenBacktrackingNode { Token: var token } &&
             token.Kind == Pcre2CharacterTokenKind.Literal &&
             token.Options == Pcre2CharacterOptions.None; i++)
        {
            builder.Append(token.Literal.ToString());
        }

        if (builder.Length == 0)
        {
            return false;
        }

        var literal = Encoding.UTF8.GetBytes(builder.ToString());
        if (Rune.DecodeFromUtf8(literal, out var firstLiteral, out _) != System.Buffers.OperationStatus.Done ||
            (TokenMatchesRune(runTokens[^1], firstLiteral, request) && !lastRunIsUnbounded))
        {
            return false;
        }

        plan = Pcre2CandidateSearchProgram.FromLeadingRunThenLiteral([.. runTokens], literal);
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

    private static bool TryCompileLeadingAsciiWordBoundaryRun(
        IPcre2BacktrackingNode root,
        out Pcre2CandidateSearchProgram plan)
    {
        if (root is not Pcre2SequenceBacktrackingNode { Children.Length: >= 2 } sequence ||
            sequence.Children[0] is not Pcre2TokenBacktrackingNode
            {
                Token:
                {
                    Kind: Pcre2CharacterTokenKind.WordBoundary,
                    Options: Pcre2CharacterOptions.None,
                },
            } ||
            sequence.Children[1] is not Pcre2RepeatBacktrackingNode
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

        plan = Pcre2CandidateSearchProgram.FromLeadingAsciiWordBoundaryRun(repeat.Minimum);
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
        for (var childIndex = 0; childIndex < sequence.Children.Length; childIndex++)
        {
            var child = sequence.Children[childIndex];
            if (minimumOffset > 0 &&
                child is Pcre2TokenBacktrackingNode { Token: var token } &&
                token.Kind == Pcre2CharacterTokenKind.Literal &&
                token.Options == Pcre2CharacterOptions.None)
            {
                var literal = new StringBuilder();
                for (var literalIndex = childIndex; literalIndex < sequence.Children.Length &&
                     sequence.Children[literalIndex] is Pcre2TokenBacktrackingNode { Token: var literalToken } &&
                     literalToken.Kind == Pcre2CharacterTokenKind.Literal &&
                     literalToken.Options == Pcre2CharacterOptions.None; literalIndex++)
                {
                    literal.Append(literalToken.Literal.ToString());
                }

                constraint = new Pcre2CandidateWindowConstraint(
                    Encoding.UTF8.GetBytes(literal.ToString()),
                    minimumOffset,
                    maximumOffset);
                return maximumOffset <= 64;
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
             token.Kind == Pcre2CharacterTokenKind.CharacterClass && IsAsciiOnlyClass(token.CharacterClass)),
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

    private static bool TokenMatchesRune(
        Pcre2CharacterToken token,
        Rune value,
        Pcre2CompileRequest request) => token.Kind switch
    {
        Pcre2CharacterTokenKind.Literal => token.Literal == value,
        Pcre2CharacterTokenKind.CharacterClass => token.CharacterClass.Matches(
            value,
            (request.Options & Pcre2CompileOptions.Ucp) != 0,
            Pcre2CharacterOptions.None),
        _ => false,
    };

    private static bool TryGetBranchLeadingLiteral(IPcre2BacktrackingNode branch, out byte[] literal)
    {
        var children = branch is Pcre2SequenceBacktrackingNode sequence
            ? sequence.Children
            : [branch];
        var index = 0;
        while (index < children.Length && IsSkippableZeroWidthPrefix(children[index]))
        {
            index++;
        }

        var builder = new StringBuilder();
        while (index < children.Length &&
               children[index] is Pcre2TokenBacktrackingNode { Token: var token } &&
               token.Kind == Pcre2CharacterTokenKind.Literal &&
               token.Options == Pcre2CharacterOptions.None)
        {
            builder.Append(token.Literal.ToString());
            index++;
        }

        literal = builder.Length == 0 ? [] : Encoding.UTF8.GetBytes(builder.ToString());
        return literal.Length >= 2;
    }

    private static bool IsSkippableZeroWidthPrefix(IPcre2BacktrackingNode node) =>
        node is Pcre2TokenBacktrackingNode
        {
            Token.Kind: Pcre2CharacterTokenKind.WordBoundary or Pcre2CharacterTokenKind.NonWordBoundary,
            Token.Options: Pcre2CharacterOptions.None,
        };

    private static bool ContainsUnsafeCandidateSemantics(IPcre2BacktrackingNode node) => node switch
    {
        // A later backreference cannot invalidate a necessary leading-byte set.
        // The leading-byte analyzer still rejects a backreference at the start.
        Pcre2SubroutineCallBacktrackingNode or
        Pcre2ConditionalBacktrackingNode or
        Pcre2ControlVerbBacktrackingNode or
        Pcre2MatchBoundaryResetBacktrackingNode => true,
        Pcre2TokenBacktrackingNode token => token.Token.Kind == Pcre2CharacterTokenKind.CodeUnit,
        Pcre2SequenceBacktrackingNode sequence => sequence.Children.Any(ContainsUnsafeCandidateSemantics),
        Pcre2AlternationBacktrackingNode alternation => alternation.Alternatives.Any(ContainsUnsafeCandidateSemantics),
        Pcre2RepeatBacktrackingNode repeat => ContainsUnsafeCandidateSemantics(repeat.Body),
        Pcre2CaptureBacktrackingNode capture => ContainsUnsafeCandidateSemantics(capture.Body),
        Pcre2AssertionBacktrackingNode assertion =>
            assertion.AssertionKind is Pcre2AssertionKind.PositiveLookbehind or Pcre2AssertionKind.NegativeLookbehind ||
            ContainsUnsafeCandidateSemantics(assertion.Body),
        Pcre2AtomicBacktrackingNode atomic => ContainsUnsafeCandidateSemantics(atomic.Body),
        _ => false,
    };
}

internal static class Pcre2SingleTokenRepeatRunner
{
    internal static int CountGreedyExcludedAscii(
        Pcre2SingleTokenRepeatProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition start)
    {
        var excluded = program.GreedyExcludedAsciiByte.GetValueOrDefault();
        var remaining = input[start.Value..];
        var count = 1;
        while (!remaining.IsEmpty)
        {
            var delimiter = remaining.IndexOf(excluded);
            if (delimiter < 0)
            {
                return checked(count + 1);
            }

            if (delimiter > 0)
            {
                count = checked(count + 1);
            }

            count = checked(count + 1);
            remaining = remaining[(delimiter + 1)..];
        }

        return count;
    }

    internal static Pcre2CharacterMatch Match(
        Pcre2SingleTokenRepeatProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget) =>
        Match(program, ref input, start, start, matchOptions, ref budget);

    internal static Pcre2CharacterMatch Match(
        Pcre2SingleTokenRepeatProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition searchStart,
        Utf8BytePosition firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
    {
        var bytes = input.Bytes;
        var anchored = (program.Request.Options & Pcre2CompileOptions.Anchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;
        var candidate = searchStart.Value;
        while (candidate <= bytes.Length)
        {
            budget.ChargeCandidate();
            if (TryMatchAt(
                    program,
                    bytes,
                    candidate,
                    firstMatchingPosition.Value,
                    matchOptions,
                    out var end))
            {
                return Pcre2CharacterMatch.Create(candidate, end);
            }

            if (anchored || !Pcre2CharacterRunner.TryAdvanceCandidate(
                    program.Request,
                    program.Minimum == 0 ? default(byte?) : program.LeadingAsciiByte,
                    ref input,
                    candidate,
                    allowAcceleration: true,
                    out var nextCandidate))
            {
                break;
            }

            candidate = nextCandidate;
        }

        return Pcre2CharacterMatch.NoMatch;
    }

    private static bool TryMatchAt(
        Pcre2SingleTokenRepeatProgram program,
        ReadOnlySpan<byte> input,
        int candidate,
        int firstMatchingPosition,
        Pcre2MatchOptions matchOptions,
        out int end)
    {
        var emptyDisallowed = (matchOptions & Pcre2MatchOptions.NotEmpty) != 0 ||
            (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0 && candidate == firstMatchingPosition;
        var requiredCount = emptyDisallowed ? Math.Max(1, program.Minimum) : program.Minimum;
        var endAnchored = (program.Request.Options & Pcre2CompileOptions.EndAnchored) != 0 ||
            (matchOptions & Pcre2MatchOptions.EndAnchored) != 0;
        if (program.GreedyExcludedAsciiByte is byte excluded &&
            (program.Request.Options & Pcre2CompileOptions.FirstLine) == 0)
        {
            var relativeEnd = input[candidate..].IndexOf(excluded);
            var fastEnd = relativeEnd < 0 ? input.Length : candidate + relativeEnd;
            if (fastEnd - candidate >= requiredCount && (!endAnchored || fastEnd == input.Length))
            {
                end = fastEnd;
                return true;
            }

            end = 0;
            return false;
        }

        var consumeGreedily = program.Preference != Pcre2RepeatPreference.Lazy || endAnchored;
        var count = 0;
        var index = candidate;
        while (count < program.Maximum &&
               (consumeGreedily || count < requiredCount) &&
               Pcre2CharacterRunner.TryMatchToken(
                   program.Token,
                   program.Request,
                   input,
                   index,
                   firstMatchingPosition,
                   matchOptions,
                   out var nextIndex))
        {
            index = nextIndex;
            count++;
        }

        if (count < requiredCount || endAnchored && index != input.Length)
        {
            end = 0;
            return false;
        }

        end = index;
        return true;
    }
}

internal static partial class Pcre2Runner
{
    internal static bool TryIsMatch(
        Pcre2CompiledProgram compiledProgram,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Utf8SliceInvocation invocation,
        out bool result)
    {
        var budget = new Pcre2ResourceBudget(compiledProgram.Request.DefaultLimits, invocation, false);
        var program = compiledProgram.Operations.IsMatch;
        if (program is Pcre2MultilinePrefixDirectProgram multilinePrefixProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                result = Pcre2MultilinePrefixRunner.TryFind(
                    multilinePrefixProgram,
                    input.Bytes,
                    start.Value,
                    out _,
                    out _);
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                multilinePrefixProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2LiteralPrefixRepeatDirectProgram literalPrefixRepeatProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                result = Pcre2LiteralPrefixRepeatRunner.IsMatch(
                    literalPrefixRepeatProgram,
                    input.Bytes,
                    start.Value);
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                literalPrefixRepeatProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2PalindromeIsMatchDirectProgram palindromeProgram)
        {
            if (start.Value == 0 &&
                matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                result = Pcre2PalindromeIsMatchRunner.IsMatch(palindromeProgram, input.Bytes);
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                palindromeProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2LeadingDotStarLiteralIsMatchDirectProgram leadingDotStarLiteralProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                result = Pcre2LeadingDotStarLiteralIsMatchRunner.IsMatch(
                    leadingDotStarLiteralProgram,
                    input.Bytes,
                    start.Value);
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                leadingDotStarLiteralProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2SeparatedRunsIsMatchDirectProgram separatedRunsProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                result = Pcre2SeparatedRunsIsMatchRunner.IsMatch(
                    separatedRunsProgram,
                    input.Bytes,
                    start.Value);
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                separatedRunsProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2AsciiBoundedIsMatchDirectProgram asciiBoundedProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                result = Pcre2AsciiBoundedIsMatchRunner.IsMatch(
                    asciiBoundedProgram,
                    input.Bytes,
                    start.Value);
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                asciiBoundedProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2AsciiAnchoredDfaIsMatchDirectProgram asciiAnchoredDfaProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                result = Pcre2AsciiAnchoredDfaIsMatchRunner.IsMatch(
                    asciiAnchoredDfaProgram,
                    input.Bytes,
                    start.Value);
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                asciiAnchoredDfaProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2AsciiRegularIsMatchDirectProgram asciiRegularProgram)
        {
            if (asciiRegularProgram.SupportsPreparedOffsets &&
                matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null) &&
                asciiRegularProgram.Regex.ByteOffsetExecution.TryIsMatchPrepared(
                    input,
                    start,
                    out result))
            {
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                asciiRegularProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2FiniteLiteralLanguageDirectProgram finiteLiteralProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null))
        {
            result = finiteLiteralProgram.BooleanSearch.IsMatch(input.Bytes, start.Value);
            return true;
        }

        if (program is Pcre2LiteralFamilyDirectProgram literalFamilyProgram)
        {
            if (matchOptions == Pcre2MatchOptions.None &&
                (invocation.IsUnmetered && invocation.CheckpointHook is null))
            {
                var enumerator = literalFamilyProgram.Regex.ByteOffsetExecution.EnumerateMatches(input, start);
                result = enumerator.MoveNext();
                return true;
            }

            result = Pcre2BacktrackingRunner.Match(
                literalFamilyProgram.Fallback,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2LiteralDirectProgram literalProgram)
        {
            result = Pcre2LiteralRunner.Match(
                literalProgram.Program,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2CharacterDirectProgram characterProgram)
        {
            result = Pcre2CharacterRunner.Match(
                characterProgram.Program,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            result = Pcre2BacktrackingRunner.Match(
                backtrackingProgram.Program,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2SingleTokenRepeatDirectProgram singleTokenRepeatProgram)
        {
            result = ExecuteSingleTokenRepeatOrFallback(
                singleTokenRepeatProgram.Program,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget).Success;
            return true;
        }

        if (program is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                result = Pcre2BacktrackingRunner.Match(
                    sliceProgram.Fallback,
                    compiledProgram.CandidateSearch,
                    ref input,
                    start,
                    matchOptions,
                    ref budget).Success;
                return true;
            }

            result = Pcre2SliceDriver.IsMatch(
                sliceProgram.SliceProgram,
                ref input,
                start,
                compiledProgram.Request,
                ref invocation);
            return true;
        }

        if (program is Pcre2Utf8DirectProgram utf8Program)
        {
            result = Pcre2SliceDriver.IsMatch(utf8Program.ScalarProgram, ref input, start,
                compiledProgram.Request, ref invocation);
            return true;
        }

        result = false;
        return false;
    }

    internal static bool TryMatch(
        Pcre2CompiledProgram compiledProgram,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Utf8SliceInvocation invocation,
        out Pcre2GroupData result)
    {
        if (compiledProgram.Operations.Match is Pcre2Utf8DirectProgram coreProgram)
        {
            var cursor = new Pcre2SliceGlobalMatchCursor(coreProgram.ScalarProgram, invocation, input, start,
                compiledProgram.Request);
            if (!cursor.MoveNext()) { result = default; return true; }
            var projection = input.CreateProjectionCursor();
            result = Pcre2GlobalCursorProjection.Project(cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes,
                ref input, ref projection, new Pcre2CaptureProjectionBudget(ref cursor.WorkingInvocation));
            return true;
        }
        var budget = new Pcre2ResourceBudget(compiledProgram.Request.DefaultLimits, invocation, false);
        Pcre2CharacterMatch directMatch;
        if (compiledProgram.Operations.Match is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                directMatch = Pcre2BacktrackingRunner.Match(
                    sliceProgram.Fallback,
                    compiledProgram.CandidateSearch,
                    ref input,
                    start,
                    matchOptions,
                    ref budget);
            }
            else
            {
                directMatch = Pcre2SliceDriver.FindFirst(
                    sliceProgram.SliceProgram,
                    ref input,
                    start,
                    compiledProgram.Request,
                    ref invocation);
            }
        }

        else if (compiledProgram.Operations.Match is Pcre2LiteralDirectProgram literalProgram)
        {
            var literalMatch = Pcre2LiteralRunner.Match(
                literalProgram.Program,
                ref input,
                start,
                matchOptions,
                ref budget);
            if (!literalMatch.Success) { result = default; return true; }
            // Discovery and required coordinate projection share the original
            // native authority; a short successful kernel is not publication.
            var projection = input.CreateProjectionCursor();
            result = Pcre2GlobalCursorProjection.Project(literalMatch.StartOffsetInBytes,
                literalMatch.EndOffsetInBytes, ref input, ref projection, ref budget);
            budget.CheckDeadline();
            return true;
        }
        else if (compiledProgram.Operations.Match is Pcre2CharacterDirectProgram characterProgram)
        {
            directMatch = Pcre2CharacterRunner.Match(
                characterProgram.Program,
                ref input,
                start,
                matchOptions,
                ref budget);
        }
        else if (compiledProgram.Operations.Match is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            directMatch = Pcre2BacktrackingRunner.Match(
                backtrackingProgram.Program,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget);
        }
        else if (compiledProgram.Operations.Match is Pcre2SingleTokenRepeatDirectProgram singleTokenRepeatProgram)
        {
            directMatch = ExecuteSingleTokenRepeatOrFallback(
                singleTokenRepeatProgram.Program,
                compiledProgram.CandidateSearch,
                ref input,
                start,
                matchOptions,
                ref budget);
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

        if (budget.IsUnmetered)
            result = Pcre2GroupData.FromByteOffsets(input.Bytes, 0,
                directMatch.StartOffsetInBytes, directMatch.EndOffsetInBytes);
        else
        {
            var projection = input.CreateProjectionCursor();
            result = Pcre2GlobalCursorProjection.Project(directMatch.StartOffsetInBytes,
                directMatch.EndOffsetInBytes, ref input, ref projection, ref budget);
        }
        return true;
    }

    internal static bool TryMatchDetailed(
        Pcre2CompiledProgram compiledProgram,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Utf8SliceInvocation invocation,
        out Pcre2GroupData[] result,
        out string? mark)
    {
        if (compiledProgram.Operations.Match is Pcre2Utf8DirectProgram coreProgram)
            return Pcre2SliceDriver.TryMatchDetailed(coreProgram.ScalarProgram, ref input, start,
                ref invocation, out result, out mark);
        if (compiledProgram.Operations.Match is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                var fallbackMatch = ExecuteBacktrackingDetailed(
                    sliceProgram.Fallback,
                    compiledProgram.CandidateSearch,
                    ref input,
                    start,
                    matchOptions,
                    compiledProgram.Request, ref invocation);
                result = fallbackMatch.Success ?
                    fallbackMatch.CaptureResult.ProjectedGroups : [];
                mark = fallbackMatch.Mark;
                return true;
            }

            return Pcre2SliceDriver.TryMatchDetailed(
                sliceProgram.SliceProgram,
                ref input,
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
            ref input,
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

    private static Pcre2CharacterMatch ExecuteSingleTokenRepeatOrFallback(
        Pcre2SingleTokenRepeatProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Pcre2ResourceBudget budget) =>
        budget.IsUnmetered
            ? Pcre2SingleTokenRepeatRunner.Match(program, ref input, start, matchOptions, ref budget)
            : Pcre2BacktrackingRunner.Match(program.Fallback, candidateSearch, ref input, start, matchOptions, ref budget);

    private static Pcre2BacktrackingMatch ExecuteBacktrackingDetailed(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchProgram candidateSearch,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request, scoped ref Utf8SliceInvocation invocation)
    {
        try
        {
            var budget = new Pcre2ResourceBudget(request.DefaultLimits,
                invocation, collectDiagnostics: false);
            var result = Pcre2BacktrackingRunner.MatchDetailed(program, candidateSearch, ref input, start, matchOptions, ref budget);
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
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        scoped ref Utf8SliceInvocation invocation,
        out int result)
    {
        var program = compiledProgram.Operations.Count;
        if (program is Pcre2LiteralPrefixRepeatDirectProgram literalPrefixRepeatProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null))
        {
            result = Pcre2LiteralPrefixRepeatRunner.Count(
                literalPrefixRepeatProgram,
                input.Bytes,
                start.Value);
            return true;
        }

        if (program is Pcre2MultilinePrefixDirectProgram multilinePrefixProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null))
        {
            result = Pcre2MultilinePrefixRunner.Count(
                multilinePrefixProgram,
                input.Bytes,
                start.Value);
            return true;
        }

        if (program is Pcre2AsciiOrderedLiteralWindowDirectProgram orderedWindowProgram &&
            !input.Validation.ContainsSupplementaryScalars &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null))
        {
            result = orderedWindowProgram.Regex.ByteOffsetExecution.CountPrepared(input, start);
            return true;
        }

        if (program is Pcre2AsciiLiteralAlternationRepeatDirectProgram alternationRepeatProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null))
        {
            result = alternationRepeatProgram.Count(input.Bytes, start.Value);
            return true;
        }

        if (program is Pcre2FiniteLiteralLanguageDirectProgram finiteLiteralProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null))
        {
            result = 0;
            var matches = finiteLiteralProgram.Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start);
            while (matches.MoveNext())
            {
                result = checked(result + 1);
            }

            return true;
        }

        if (program is Pcre2LiteralFamilyDirectProgram literalFamilyProgram &&
            matchOptions == Pcre2MatchOptions.None)
        {
            if (invocation.IsUnmetered && invocation.CheckpointHook is null)
            {
                result = literalFamilyProgram.Regex.ByteOffsetExecution.CountPrepared(input, start);
                return true;
            }

            // The finite-language compiler already owns escaped, ordered literals.
            // Reuse their bounded native Count under the original authority; neither
            // this value operation nor its scalar fallback needs projected captures.
            // CountPrepared would start a separate budget.
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
                        input.Bytes[start.Value..], false, ref invocation, out result);
                    Pcre2SliceDriver.ThrowIfSliceBreached(breach);
                    return true;
                }
                result = Pcre2SliceDriver.CountNonEmpty(scalarProgram, searchPlan, ref input, start, ref invocation);
                return true;
            }
        }

        if (program is Pcre2LiteralDirectProgram literalProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null) &&
            literalProgram.Program.CanCountDirectly(input.ByteLength - start.Value))
        {
            result = literalProgram.Program.CountNonOverlapping(input.Bytes[start.Value..]);
            return true;
        }

        if (program is Pcre2CharacterDirectProgram characterProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null) &&
            characterProgram.Program.CanCountSingleCharacterClassDirectly)
        {
            if (characterProgram.Program.TryGetDirectCountCategory(out var category))
            {
                result = Utf8UnicodeCategoryExecutor.CountCategory(
                    input.Bytes[start.Value..],
                    input.Validation.ContainsSupplementaryScalars,
                    category);
            }
            else
            {
                result = Pcre2CharacterRunner.CountSingleCharacterClass(
                    characterProgram.Program,
                    input.Bytes,
                    start);
            }

            return true;
        }

        if (program is Pcre2SingleTokenRepeatDirectProgram singleTokenRepeatProgram &&
            matchOptions == Pcre2MatchOptions.None &&
            (invocation.IsUnmetered && invocation.CheckpointHook is null) &&
            singleTokenRepeatProgram.Program.CanCountGreedyExcludedAsciiDirectly)
        {
            result = Pcre2SingleTokenRepeatRunner.CountGreedyExcludedAscii(
                singleTokenRepeatProgram.Program,
                input.Bytes,
                start);
            return true;
        }

        var budget = new Pcre2ResourceBudget(compiledProgram.Request.DefaultLimits, invocation, false);
        if (TryCreateCursor(compiledProgram, input, start, matchOptions, out var cursor, budget))
        {
            if (program is Pcre2BacktrackingDirectProgram)
            {
                // Count has one lexical owner. Copied public value cursors never
                // acquire owned scratch, and release uses the cursor's budget.
                var frames = Pcre2BacktrackingRunner.CreateFrames(ref budget, 0);
                try
                {
                    result = 0;
                    while (cursor.MoveNext(ref frames)) result = checked(result + 1);
                    return true;
                }
                finally { cursor.ReleaseFrames(ref frames); }
            }
            result = 0;
            while (cursor.MoveNext())
            {
                result = checked(result + 1);
            }

            return true;
        }

        if (program is Pcre2SliceDirectProgram sliceProgram)
        {
            if (matchOptions != Pcre2MatchOptions.None)
            {
                var fallbackCursor = Pcre2GlobalMatchCursor.CreateBacktracking(
                    sliceProgram.Fallback,
                    compiledProgram.CandidateSearchPlan,
                    input,
                    start,
                    matchOptions,
                    compiledProgram.Request,
                    budget);
                result = 0;
                while (fallbackCursor.MoveNext())
                {
                    result = checked(result + 1);
                }

                return true;
            }

            // A necessary leading-byte set can prove the entire suffix impossible.
            // Borrow its existing searcher and original authority before renting VM
            // scratch; finding a byte leaves ordering and verification to Count.
            var candidates = compiledProgram.CandidateSearch;
            if (!sliceProgram.SliceProgram.CanMatchEmpty && !sliceProgram.SliceProgram.RightToLeft &&
                candidates.Kind == Pcre2CandidateSearchKind.LeadingAsciiSet)
            {
                var breach = candidates.Searcher.TryFindMatch(input.Bytes, start.Value, input.ByteLength,
                    false, ref invocation, out _, out var found);
                Pcre2SliceDriver.ThrowIfSliceBreached(breach);
                if (!found)
                {
                    result = 0;
                    return true;
                }
            }

            result = Pcre2SliceDriver.Count(
                sliceProgram.SliceProgram,
                ref input,
                start,
                compiledProgram.Request,
                ref invocation);
            return true;
        }

        if (program is Pcre2Utf8DirectProgram utf8Program)
        {
            result = Pcre2SliceDriver.Count(utf8Program.ScalarProgram, ref input, start,
                compiledProgram.Request, ref invocation);
            return true;
        }

        // The slice overlay claims Enumerate while Count stays on a native
        // program without a metered vehicle: count through the slice shell's
        // backtracking fallback, the exact vehicle the overlay kept, so finite
        // heap limits keep breaching instead of reporting unsupported.
        if (compiledProgram.Operations.Enumerate is Pcre2SliceDirectProgram enumerateSlice)
        {
            var fallbackCursor = Pcre2GlobalMatchCursor.CreateBacktracking(
                enumerateSlice.Fallback,
                compiledProgram.CandidateSearchPlan,
                input,
                start,
                matchOptions,
                compiledProgram.Request,
                budget);
            result = 0;
            while (fallbackCursor.MoveNext())
            {
                result = checked(result + 1);
            }

            return true;
        }

        result = 0;
        return false;
    }

    internal static bool HasUnmeteredExecution(Pcre2CompileRequest request) =>
        request.MatchTimeout == Timeout.InfiniteTimeSpan &&
        request.DefaultLimits.MatchLimit == 0 &&
        request.DefaultLimits.DepthLimit == 0 &&
        request.DefaultLimits.HeapLimitInBytes == 0 &&
        request.DefaultLimits.ResultLimitInBytes == 0;

    internal static bool TryCreateCursor(
        Pcre2CompiledProgram compiledProgram,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        out Pcre2GlobalMatchCursor cursor) =>
        TryCreateCursor(compiledProgram, input, start, matchOptions, out cursor,
            new Pcre2ResourceBudget(compiledProgram.Request.DefaultLimits, compiledProgram.Request.MatchTimeout, false));

    internal static bool TryCreateCursor(
        Pcre2CompiledProgram compiledProgram,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        out Pcre2GlobalMatchCursor cursor,
        Pcre2ResourceBudget budget)
    {
        if (compiledProgram.Operations.Enumerate is Pcre2LiteralPrefixRepeatDirectProgram literalPrefixRepeatProgram)
        {
            cursor = matchOptions == Pcre2MatchOptions.None && budget.IsUnmetered
                ? Pcre2GlobalMatchCursor.CreateLiteralPrefixRepeat(
                    literalPrefixRepeatProgram,
                    input,
                    start,
                    compiledProgram.Request, budget)
                : Pcre2GlobalMatchCursor.CreateBacktracking(
                    literalPrefixRepeatProgram.Fallback,
                    compiledProgram.CandidateSearchPlan,
                    input,
                    start,
                    matchOptions,
                    compiledProgram.Request,
                    budget);
            return true;
        }

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

        if (compiledProgram.Operations.Enumerate is Pcre2MultilinePrefixDirectProgram multilinePrefixProgram)
        {
            cursor = matchOptions == Pcre2MatchOptions.None && budget.IsUnmetered
                ? Pcre2GlobalMatchCursor.CreateMultilinePrefix(
                    multilinePrefixProgram,
                    input,
                    start,
                    compiledProgram.Request, budget)
                : Pcre2GlobalMatchCursor.CreateBacktracking(
                    multilinePrefixProgram.Fallback,
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
                ? Pcre2GlobalMatchCursor.CreateLiteralFamily(literalFamilyProgram, input, start, compiledProgram.Request, budget)
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

        if (compiledProgram.Operations.Enumerate is Pcre2CharacterDirectProgram characterProgram)
        {
            cursor = Pcre2GlobalMatchCursor.CreateCharacter(
                characterProgram.Program, input, start, matchOptions, compiledProgram.Request, budget);
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

        if (compiledProgram.Operations.Enumerate is Pcre2SingleTokenRepeatDirectProgram singleTokenRepeatProgram)
        {
            cursor = budget.IsUnmetered
                ? Pcre2GlobalMatchCursor.CreateSingleTokenRepeat(
                    singleTokenRepeatProgram.Program,
                    input,
                    start,
                    matchOptions,
                    compiledProgram.Request, budget)
                : Pcre2GlobalMatchCursor.CreateBacktracking(
                    singleTokenRepeatProgram.Program.Fallback,
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
    internal Pcre2CaptureProjectionBudget WorkingBufferControl => _kind == Pcre2GlobalCursorKind.Direct
        ? _direct.WorkingBufferControl : _literalFamily.WorkingBufferControl;

    internal static int DebugSizeInBytes => Unsafe.SizeOf<Pcre2GlobalMatchCursor>();

    private readonly Pcre2GlobalCursorKind _kind;
    private Pcre2LiteralFamilyGlobalMatchCursor _literalFamily;
    private Pcre2DirectGlobalMatchCursor _direct;

    private Pcre2GlobalMatchCursor(Pcre2DirectGlobalMatchCursor direct)
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
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateLiteral(program, input, start, matchOptions, request, budget));

    internal static Pcre2GlobalMatchCursor CreateLiteralFamily(
        Pcre2LiteralFamilyDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(new Pcre2LiteralFamilyGlobalMatchCursor(program, input, start, request, budget));

    internal static Pcre2GlobalMatchCursor CreateCharacter(
        Pcre2CharacterProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateCharacter(program, input, start, matchOptions, request, budget));

    internal static Pcre2GlobalMatchCursor CreateSingleTokenRepeat(
        Pcre2SingleTokenRepeatProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateSingleTokenRepeat(program, input, start, matchOptions, request, budget));

    internal static Pcre2GlobalMatchCursor CreateMultilinePrefix(
        Pcre2MultilinePrefixDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateMultilinePrefix(program, input, start, request, budget));

    internal static Pcre2GlobalMatchCursor CreateSameStartLiteral(
        Pcre2SameStartLiteralDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateSameStartLiteral(program, input, start, request, budget));

    internal static Pcre2GlobalMatchCursor CreateLiteralPrefixRepeat(
        Pcre2LiteralPrefixRepeatDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(Pcre2DirectGlobalMatchCursor.CreateLiteralPrefixRepeat(program, input, start, request, budget));

    internal static Pcre2GlobalMatchCursor CreateBacktracking(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        bool collectDiagnostics) =>
        CreateBacktracking(program, candidateSearch, input, start, matchOptions, request,
            new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout, collectDiagnostics));

    internal static Pcre2GlobalMatchCursor CreateBacktracking(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
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

    internal bool MoveNext(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames)
    {
        if (_kind != Pcre2GlobalCursorKind.Direct) return MoveNext();
        var control = WorkingBufferControl;
        Pcre2SliceDriver.CheckInvocation(ref control.WorkingInvocation);
        var moved = _direct.MoveNext(ref frames);
        Pcre2SliceDriver.CheckInvocation(ref control.WorkingInvocation);
        return moved;
    }

    internal void ReleaseFrames(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames)
        => _direct.ReleaseFrames(ref frames);

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
    internal Pcre2CaptureProjectionBudget WorkingBufferControl => new(ref _budget);

    internal static int DebugSizeInBytes => Unsafe.SizeOf<Pcre2DirectGlobalMatchCursor>();

    private readonly Pcre2DirectGlobalCursorKind _kind;
    private readonly Pcre2LiteralProgram? _literalProgram;
    private readonly Pcre2CharacterProgram? _characterProgram;
    private readonly Pcre2SingleTokenRepeatProgram? _singleTokenRepeatProgram;
    private readonly Pcre2MultilinePrefixDirectProgram? _multilinePrefixProgram;
    private readonly Pcre2SameStartLiteralDirectProgram? _sameStartLiteralProgram;
    private readonly Pcre2LiteralPrefixRepeatDirectProgram? _literalPrefixRepeatProgram;
    private readonly Pcre2BacktrackingProgram? _backtrackingProgram;
    private readonly Pcre2CandidateSearchPlan? _candidateSearch;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private readonly Pcre2MatchOptions _matchOptions;
    private Pcre2ResourceBudget _budget;
    private Utf8ProjectionCursor _projection;
    private Utf8BytePosition _restartPosition;
    private Utf8BytePosition _firstMatchingPosition;
    private Pcre2GlobalRetryState _retryState;

    private Pcre2DirectGlobalMatchCursor(
        Pcre2DirectGlobalCursorKind kind,
        Pcre2LiteralProgram? literalProgram,
        Pcre2CharacterProgram? characterProgram,
        Pcre2SingleTokenRepeatProgram? singleTokenRepeatProgram,
        Pcre2MultilinePrefixDirectProgram? multilinePrefixProgram,
        Pcre2SameStartLiteralDirectProgram? sameStartLiteralProgram,
        Pcre2LiteralPrefixRepeatDirectProgram? literalPrefixRepeatProgram,
        Pcre2BacktrackingProgram? backtrackingProgram,
        Pcre2CandidateSearchPlan? candidateSearch,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget)
    {
        _kind = kind;
        _literalProgram = literalProgram;
        _characterProgram = characterProgram;
        _singleTokenRepeatProgram = singleTokenRepeatProgram;
        _multilinePrefixProgram = multilinePrefixProgram;
        _sameStartLiteralProgram = sameStartLiteralProgram;
        _literalPrefixRepeatProgram = literalPrefixRepeatProgram;
        _backtrackingProgram = backtrackingProgram;
        _candidateSearch = candidateSearch;
        _input = input;
        _request = request;
        _matchOptions = matchOptions;
        _budget = budget;
        _projection = input.CreateProjectionCursor();
        _restartPosition = start;
        _firstMatchingPosition = start;
        _retryState = Pcre2GlobalRetryState.Search;
        Current = default;
    }

    internal Pcre2GroupData Current { get; private set; }

    internal readonly Pcre2ExecutionDiagnostics Diagnostics => _budget.Diagnostics;

    internal static Pcre2DirectGlobalMatchCursor CreateLiteral(
        Pcre2LiteralProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.Literal,
            program,
            characterProgram: null,
            singleTokenRepeatProgram: null,
            multilinePrefixProgram: null,
            sameStartLiteralProgram: null,
            literalPrefixRepeatProgram: null,
            backtrackingProgram: null,
            candidateSearch: default,
            input,
            start,
            matchOptions,
            request,
            budget);

    internal static Pcre2DirectGlobalMatchCursor CreateCharacter(
        Pcre2CharacterProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.Character,
            literalProgram: null,
            program,
            singleTokenRepeatProgram: null,
            multilinePrefixProgram: null,
            sameStartLiteralProgram: null,
            literalPrefixRepeatProgram: null,
            backtrackingProgram: null,
            candidateSearch: default,
            input,
            start,
            matchOptions,
            request,
            budget);

    internal static Pcre2DirectGlobalMatchCursor CreateSingleTokenRepeat(
        Pcre2SingleTokenRepeatProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.SingleTokenRepeat,
            literalProgram: null,
            characterProgram: null,
            program,
            multilinePrefixProgram: null,
            sameStartLiteralProgram: null,
            literalPrefixRepeatProgram: null,
            backtrackingProgram: null,
            candidateSearch: default,
            input,
            start,
            matchOptions,
            request,
            budget);

    internal static Pcre2DirectGlobalMatchCursor CreateMultilinePrefix(
        Pcre2MultilinePrefixDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.MultilinePrefix,
            literalProgram: null,
            characterProgram: null,
            singleTokenRepeatProgram: null,
            program,
            sameStartLiteralProgram: null,
            literalPrefixRepeatProgram: null,
            backtrackingProgram: null,
            candidateSearch: default,
            input,
            start,
            Pcre2MatchOptions.None,
            request,
            budget);

    internal static Pcre2DirectGlobalMatchCursor CreateSameStartLiteral(
        Pcre2SameStartLiteralDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.SameStartLiteral,
            literalProgram: null,
            characterProgram: null,
            singleTokenRepeatProgram: null,
            multilinePrefixProgram: null,
            program,
            literalPrefixRepeatProgram: null,
            backtrackingProgram: null,
            candidateSearch: default,
            input,
            start,
            Pcre2MatchOptions.None,
            request,
            budget);

    internal static Pcre2DirectGlobalMatchCursor CreateLiteralPrefixRepeat(
        Pcre2LiteralPrefixRepeatDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.LiteralPrefixRepeat,
            literalProgram: null,
            characterProgram: null,
            singleTokenRepeatProgram: null,
            multilinePrefixProgram: null,
            sameStartLiteralProgram: null,
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
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget) =>
        new(
            Pcre2DirectGlobalCursorKind.Backtracking,
            literalProgram: null,
            characterProgram: null,
            singleTokenRepeatProgram: null,
            multilinePrefixProgram: null,
            sameStartLiteralProgram: null,
            literalPrefixRepeatProgram: null,
            program,
            candidateSearch,
            input,
            start,
            matchOptions,
            request,
            budget);

    internal void ReleaseFrames(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames)
        => frames.Dispose(ref _budget);

    internal bool MoveNext()
    {
        var frames = Pcre2BacktrackingRunner.CreateFrames(ref _budget, 0);
        try { return MoveNext(ref frames); }
        finally { frames.Dispose(ref _budget); }
    }

    internal bool MoveNext(scoped ref Pcre2InvocationStateStack<Pcre2BacktrackingFrame> frames)
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
                            ref _input,
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
                    case Pcre2DirectGlobalCursorKind.Character:
                        var characterProgram = _characterProgram ??
                            throw new InvalidOperationException("The PCRE2 character cursor has no execution program.");
                        var characterMatch = Pcre2CharacterRunner.Match(
                            characterProgram,
                            ref _input,
                            _restartPosition,
                            options,
                            ref _budget);
                        match = new Pcre2DirectGlobalMatch(
                            characterMatch.Success,
                            characterMatch.StartOffsetInBytes,
                            characterMatch.EndOffsetInBytes,
                            characterMatch.ConsumedStartOffsetInBytes,
                            characterMatch.ConsumedEndOffsetInBytes,
                            characterMatch.MatchBoundaryWasReset);
                        break;
                    case Pcre2DirectGlobalCursorKind.SingleTokenRepeat:
                        var repeatProgram = _singleTokenRepeatProgram ??
                            throw new InvalidOperationException("The PCRE2 repeat cursor has no execution program.");
                        var repeatMatch = Pcre2SingleTokenRepeatRunner.Match(
                            repeatProgram,
                            ref _input,
                            _restartPosition,
                            options,
                            ref _budget);
                        match = new Pcre2DirectGlobalMatch(
                            repeatMatch.Success,
                            repeatMatch.StartOffsetInBytes,
                            repeatMatch.EndOffsetInBytes,
                            repeatMatch.ConsumedStartOffsetInBytes,
                            repeatMatch.ConsumedEndOffsetInBytes,
                            repeatMatch.MatchBoundaryWasReset);
                        break;
                    case Pcre2DirectGlobalCursorKind.MultilinePrefix:
                        var multilinePrefixProgram = _multilinePrefixProgram ??
                            throw new InvalidOperationException("The PCRE2 multiline-prefix cursor has no execution program.");
                        var multilinePrefixSuccess = Pcre2MultilinePrefixRunner.TryFind(
                            multilinePrefixProgram,
                            _input.Bytes,
                            _restartPosition.Value,
                            out var multilinePrefixStart,
                            out var multilinePrefixEnd);
                        match = new Pcre2DirectGlobalMatch(
                            multilinePrefixSuccess,
                            multilinePrefixStart,
                            multilinePrefixEnd,
                            multilinePrefixStart,
                            multilinePrefixEnd,
                            MatchBoundaryWasReset: false);
                        break;
                    case Pcre2DirectGlobalCursorKind.SameStartLiteral:
                        var sameStartLiteralProgram = _sameStartLiteralProgram ??
                            throw new InvalidOperationException("The PCRE2 same-start literal cursor has no execution program.");
                        match = Pcre2SameStartLiteralRunner.Match(
                            sameStartLiteralProgram,
                            _input.Bytes,
                            _restartPosition,
                            _firstMatchingPosition,
                            options);
                        break;
                    case Pcre2DirectGlobalCursorKind.LiteralPrefixRepeat:
                        var literalPrefixRepeatProgram = _literalPrefixRepeatProgram ??
                            throw new InvalidOperationException("The PCRE2 literal-prefix repeat cursor has no execution program.");
                        match = Pcre2LiteralPrefixRepeatRunner.Match(
                            literalPrefixRepeatProgram,
                            _input.Bytes,
                            _restartPosition.Value);
                        break;
                    case Pcre2DirectGlobalCursorKind.Backtracking:
                        var backtrackingProgram = _backtrackingProgram ??
                            throw new InvalidOperationException("The PCRE2 backtracking cursor has no execution program.");
                        var candidateSearch = _candidateSearch ??
                            throw new InvalidOperationException("The PCRE2 backtracking cursor has no candidate-search plan.");
                        var backtrackingMatch = Pcre2BacktrackingRunner.Match(
                            backtrackingProgram,
                            candidateSearch.Program,
                            ref _input,
                            _restartPosition,
                            _firstMatchingPosition,
                            options,
                            ref _budget, ref frames);
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
                            ref _input,
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

                Current = Pcre2GlobalCursorProjection.Project(
                    match.StartOffsetInBytes,
                    match.EndOffsetInBytes,
                    ref _input,
                    ref _projection, ref _budget);
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
                    ref _input,
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
        Character = 2,
        SingleTokenRepeat = 3,
        Backtracking = 4,
        MultilinePrefix = 5,
        SameStartLiteral = 6,
        LiteralPrefixRepeat = 7,
    }
}

internal ref struct Pcre2LiteralFamilyGlobalMatchCursor
{
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Pcre2LiteralFamilyGlobalMatchCursor>();

    private Utf8PreparedValueMatchEnumerator _enumerator;
    private Utf8ValidatedInput _input;
    private Utf8ProjectionCursor _projection;
    private readonly Pcre2FiniteLiteralBoundaryProjection? _boundaryProjection;
    private Pcre2ResourceBudget _budget;

    [UnscopedRef]
    internal Pcre2CaptureProjectionBudget WorkingBufferControl => new(ref _budget);

    internal Pcre2LiteralFamilyGlobalMatchCursor(
        Pcre2LiteralFamilyDirectProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2CompileRequest request,
        Pcre2ResourceBudget budget)
    {
        _enumerator = program.Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start);
        _input = input;
        _projection = input.CreateProjectionCursor();
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
            _input.Bytes,
            consumedStart,
            consumedEnd) ?? consumedStart;
        Current = Pcre2GlobalCursorProjection.Project(
            reportedStart,
            consumedEnd,
            ref _input,
            ref _projection, ref _budget);
        return true;
    }
}

internal ref struct Pcre2BacktrackingDetailedGlobalMatchCursor
{
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Pcre2BacktrackingDetailedGlobalMatchCursor>();

    private readonly Pcre2BacktrackingProgram _program;
    private readonly Pcre2CandidateSearchPlan _candidateSearch;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private readonly Pcre2MatchOptions _matchOptions;
    private Pcre2ResourceBudget _budget;
    private Utf8BytePosition _restartPosition;
    private Utf8BytePosition _firstMatchingPosition;
    private Pcre2GlobalRetryState _retryState;

    internal Pcre2BacktrackingDetailedGlobalMatchCursor(
        Pcre2BacktrackingProgram program,
        Pcre2CandidateSearchPlan candidateSearch,
        Utf8ValidatedInput input,
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
        Utf8ValidatedInput input,
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
        _restartPosition = start;
        _firstMatchingPosition = start;
        _retryState = Pcre2GlobalRetryState.Search;
        Current = default;
    }

    internal Pcre2BacktrackingMatch Current { get; private set; }

    [UnscopedRef]
    internal ref Utf8SliceInvocation WorkingInvocation => ref _budget.WorkingInvocation;

    internal Pcre2GroupData[] ProjectCurrentCaptures() => Current.CaptureResult.ProjectedGroups;

    internal bool MoveNext()
    {
        while (_retryState != Pcre2GlobalRetryState.Finished)
        {
            var retryingEmptyAtSamePosition = _retryState == Pcre2GlobalRetryState.EmptyAtSamePosition;
            var options = retryingEmptyAtSamePosition
                ? _matchOptions | Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart
                : _matchOptions;

            Pcre2BacktrackingMatch match;
            try
            {
                match = Pcre2BacktrackingRunner.MatchDetailed(
                    _program,
                    _candidateSearch.Program,
                    ref _input,
                    _restartPosition,
                    _firstMatchingPosition,
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
                if (match.StartOffsetInBytes > match.EndOffsetInBytes)
                {
                    throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
                }

                Current = match;
                _restartPosition = new Utf8BytePosition(match.ConsumedEndOffsetInBytes);
                _firstMatchingPosition = _restartPosition;
                _retryState = match.ConsumedStartOffsetInBytes == match.ConsumedEndOffsetInBytes
                    ? Pcre2GlobalRetryState.EmptyAtSamePosition
                    : Pcre2GlobalRetryState.Search;
                return true;
            }

            if (!retryingEmptyAtSamePosition ||
                !Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(ref _input, _request, _matchOptions, _restartPosition, out _restartPosition))
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
}

internal static class Pcre2GlobalCursorProjection
{
    internal static Pcre2GroupData Project(
        int startOffsetInBytes,
        int endOffsetInBytes,
        ref Utf8ValidatedInput input,
        ref Utf8ProjectionCursor projection,
        ref Pcre2ResourceBudget budget) =>
        Project(startOffsetInBytes, endOffsetInBytes, ref input, ref projection, new Pcre2CaptureProjectionBudget(ref budget));

    internal static Pcre2GroupData Project(
        int startOffsetInBytes,
        int endOffsetInBytes,
        ref Utf8ValidatedInput input,
        ref Utf8ProjectionCursor projection,
        scoped Pcre2CaptureProjectionBudget control)
    {
        var start = new Utf8BytePosition(startOffsetInBytes);
        var end = new Utf8BytePosition(endOffsetInBytes);
        if (!input.IsScalarBoundary(start) || !input.IsScalarBoundary(end))
        {
            return new Pcre2GroupData
            {
                Number = 0,
                Success = true,
                StartOffsetInBytes = startOffsetInBytes,
                EndOffsetInBytes = endOffsetInBytes,
                CoordinateFlagsSpecified = true,
                Utf8SliceIsWellFormed = false,
                Utf16ProjectionIsExact = false,
            };
        }

        var startInUtf16 = Pcre2Runner.ProjectCaptureEndpoint(ref input, ref projection, start.Value, control);
        var endInUtf16 = Pcre2Runner.ProjectCaptureEndpoint(ref input, ref projection, end.Value, control);
        return new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = start.Value,
            EndOffsetInBytes = end.Value,
            StartOffsetInUtf16 = startInUtf16,
            EndOffsetInUtf16 = endInUtf16,
            CoordinateFlagsSpecified = true,
            Utf8SliceIsWellFormed = start.Value <= end.Value,
            Utf16ProjectionIsExact = true,
        };
    }

}

internal static class Pcre2GlobalCursorMovement
{
    internal static bool TryAdvanceAfterEmpty(
        ref Utf8ValidatedInput input,
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
        var bytes = input.Bytes;
        if (!input.IsScalarBoundary(position))
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

        return input.TryAdvanceScalar(position, out next);
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

internal struct Pcre2ResourceBudget
{
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Pcre2ResourceBudget>();

    internal static int DebugDiagnosticsSizeInBytes => Unsafe.SizeOf<Pcre2ExecutionDiagnostics>();

    // Controlled cursor copies share enforcement while their positions and
    // profile diagnostic counts stay local. Default unlimited runs stay inline.
    private Utf8SliceInvocation _invocation;
    private readonly Pcre2ExecutionDiagnosticsCollector? _diagnostics;

    internal Pcre2ResourceBudget(
        Utf8Pcre2ExecutionLimits limits,
        TimeSpan timeout,
        bool collectDiagnostics)
        : this(limits, new Utf8SliceInvocation(
            new Utf8ExecutionLimits(limits.MatchLimit, limits.DepthLimit, limits.HeapLimitInBytes, limits.ResultLimitInBytes),
            timeout, CancellationToken.None), collectDiagnostics)
    {
    }

    internal Pcre2ResourceBudget(
        Utf8Pcre2ExecutionLimits limits,
        Utf8SliceInvocation invocation,
        bool collectDiagnostics)
    {
        Limits = limits;
        _invocation = invocation;
        // This profile's native steps retain their original cadence and do not
        // invoke the diagnostic hook belonging to the shared scalar-slice VM.
        _invocation.CheckpointHook = null;
        _diagnostics = collectDiagnostics ? new Pcre2ExecutionDiagnosticsCollector() : null;
        CandidateSteps = 0;
        BacktrackingSteps = 0;
    }

    internal Utf8Pcre2ExecutionLimits Limits { get; }

    // Required shaping borrows the execution owner, including its inline
    // unlimited path. Never copy this value into a new decoding invocation.
    [UnscopedRef]
    internal ref Utf8SliceInvocation WorkingInvocation => ref _invocation;

    internal ulong CandidateSteps { get; private set; }

    internal ulong BacktrackingSteps { get; private set; }

    internal uint Depth => _invocation.Budget.PeakDepth;

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
        CandidateSteps++;
        Pcre2SliceDriver.ThrowIfSliceBreached(_invocation.Budget.TryChargeStep());
    }

    internal void ChargeBacktracking(
        Pcre2BacktrackingInstructionKind instructionKind,
        Pcre2CharacterTokenKind tokenKind)
    {
        BacktrackingSteps++;
        if (_diagnostics is { } diagnostics)
        {
            switch (instructionKind)
            {
                case Pcre2BacktrackingInstructionKind.Token:
                    diagnostics.VmTokenSteps++;
                    switch (tokenKind)
                    {
                        case Pcre2CharacterTokenKind.Literal:
                            diagnostics.VmLiteralTokenSteps++;
                            break;
                        case Pcre2CharacterTokenKind.CharacterClass:
                            diagnostics.VmClassTokenSteps++;
                            break;
                        case Pcre2CharacterTokenKind.BeginningOfLine:
                        case Pcre2CharacterTokenKind.EndOfLine:
                        case Pcre2CharacterTokenKind.BeginningOfSubject:
                        case Pcre2CharacterTokenKind.EndOfSubjectOrFinalNewline:
                        case Pcre2CharacterTokenKind.EndOfSubject:
                        case Pcre2CharacterTokenKind.FirstMatchingPosition:
                        case Pcre2CharacterTokenKind.WordBoundary:
                        case Pcre2CharacterTokenKind.NonWordBoundary:
                            diagnostics.VmBoundaryAnchorTokenSteps++;
                            break;
                        default:
                            diagnostics.VmOtherTokenSteps++;
                            break;
                    }
                    break;
                case Pcre2BacktrackingInstructionKind.Split:
                case Pcre2BacktrackingInstructionKind.Jump:
                    diagnostics.VmBranchSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.Repeat:
                    diagnostics.VmRepeatSteps++;
                    diagnostics.VmRepeatEnterSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.RepeatEnd:
                    diagnostics.VmRepeatSteps++;
                    diagnostics.VmRepeatEndSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.RepeatExit:
                    diagnostics.VmRepeatSteps++;
                    diagnostics.VmRepeatExitSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.PossessiveTokenRepeat:
                    diagnostics.VmRepeatSteps++;
                    diagnostics.VmRepeatEnterSteps++;
                    diagnostics.VmPossessiveTokenScanSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.CollapsibleTokenRepeat:
                    diagnostics.VmRepeatSteps++;
                    diagnostics.VmRepeatEnterSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.CaptureStart:
                case Pcre2BacktrackingInstructionKind.CaptureEnd:
                case Pcre2BacktrackingInstructionKind.Backreference:
                case Pcre2BacktrackingInstructionKind.BackreferenceSlotSet:
                    diagnostics.VmCaptureSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.Assertion:
                case Pcre2BacktrackingInstructionKind.SubroutineCall:
                case Pcre2BacktrackingInstructionKind.SubroutineReturn:
                    diagnostics.VmAssertionSubroutineSteps++;
                    break;
                case Pcre2BacktrackingInstructionKind.Accept:
                    diagnostics.VmAcceptSteps++;
                    break;
                default:
                    diagnostics.VmControlSteps++;
                    break;
            }
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

    internal bool CanRetainWorkspace => _invocation.Ledger.IsUnlimited;

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

internal readonly record struct Pcre2CountDiagnostics(int Count, Pcre2ExecutionDiagnostics Execution);

internal readonly record struct Pcre2IsMatchDiagnostics(bool IsMatch, Pcre2ExecutionDiagnostics Execution);

internal enum Pcre2SyntaxNodeKind : byte
{
    LegacyPattern = 0,
    Literal = 1,
    CharacterProgram = 2,
    BacktrackingProgram = 3,
}

internal interface IPcre2SyntaxTree
{
    Pcre2SyntaxNodeKind RootKind { get; }
}

internal sealed class Pcre2LegacySyntaxTree : IPcre2SyntaxTree
{
    internal static Pcre2LegacySyntaxTree Instance { get; } = new();

    private Pcre2LegacySyntaxTree()
    {
    }

    public Pcre2SyntaxNodeKind RootKind => Pcre2SyntaxNodeKind.LegacyPattern;
}

internal readonly record struct Pcre2SyntaxNode(Pcre2SyntaxNodeKind Kind, int StartOffset, int Length);

internal readonly record struct Pcre2CompileDiagnostic(Pcre2ErrorKind ErrorKind, int PatternOffset, string Message);
