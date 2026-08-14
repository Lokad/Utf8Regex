using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal readonly record struct Pcre2CompileRequest(
    string Pattern,
    Pcre2CompileOptions Options,
    Utf8Pcre2CompileSettings Settings,
    Utf8Pcre2ExecutionLimits DefaultLimits,
    TimeSpan MatchTimeout);

internal delegate Pcre2CompiledProgram Pcre2LegacyProgramFactory(Pcre2CompileRequest request);

internal static class Pcre2Compiler
{
    internal static Pcre2CompiledProgram Compile(Pcre2CompileRequest request, Pcre2LegacyProgramFactory legacyProgramFactory)
    {
        ArgumentNullException.ThrowIfNull(request.Pattern);
        ArgumentNullException.ThrowIfNull(legacyProgramFactory);
        Pcre2CompileValidator.Validate(request.Pattern, request.Settings);
        if (Pcre2LiteralCompiler.Compile(request) is Pcre2CompiledLiteralOutcome literal)
        {
            var literalProgram = legacyProgramFactory(request);
            literalProgram = Pcre2CompiledProgramOverlay.WithLiteralOneShot(literalProgram, literal.SyntaxTree, literal.Program);
            Pcre2ProgramInvariant.Validate(literalProgram);
            return literalProgram;
        }

        if (Pcre2CharacterCompiler.Compile(request) is Pcre2CompiledCharacterOutcome character)
        {
            Pcre2CompiledProgram foundation;
            try
            {
                foundation = legacyProgramFactory(request);
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
            try
            {
                foundation = legacyProgramFactory(request);
            }
            catch (RegexParseException)
            {
                foundation = Pcre2CompiledProgramOverlay.CreateGenericFoundation(request);
            }

            foundation = Pcre2CompiledProgramOverlay.WithBacktracking(
                foundation,
                backtracking.SyntaxTree,
                backtracking.Program);
            Pcre2ProgramInvariant.Validate(foundation);
            return foundation;
        }

        var program = legacyProgramFactory(request);
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

internal interface IPcre2ManagedProgramSlot
{
    bool IsPresent { get; }
}

internal sealed class Pcre2EmptyManagedProgramSlot : IPcre2ManagedProgramSlot
{
    internal static Pcre2EmptyManagedProgramSlot Instance { get; } = new();

    private Pcre2EmptyManagedProgramSlot()
    {
    }

    public bool IsPresent => false;
}

internal sealed class Pcre2ManagedProgramSlot : IPcre2ManagedProgramSlot
{
    internal Pcre2ManagedProgramSlot(Regex regex)
    {
        Regex = regex;
    }

    public bool IsPresent => true;

    internal Regex Regex { get; }
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
    internal Pcre2Utf8DirectProgram(Utf8Regex regex, Pcre2DirectProgramKind kind)
    {
        if (kind is not (Pcre2DirectProgramKind.Utf8Regex or Pcre2DirectProgramKind.Utf8RegexEquivalent))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        Regex = regex;
        Kind = kind;
    }

    public Pcre2DirectProgramKind Kind { get; }

    internal Utf8Regex Regex { get; }
}

internal sealed class Pcre2ManagedDirectProgram : IPcre2DirectProgram
{
    internal Pcre2ManagedDirectProgram(Regex regex)
    {
        Regex = regex;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.ManagedRegex;

    internal Regex Regex { get; }
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

internal sealed class Pcre2BacktrackingDirectProgram : IPcre2DirectProgram
{
    internal Pcre2BacktrackingDirectProgram(Pcre2BacktrackingProgram program)
    {
        Program = program;
    }

    public Pcre2DirectProgramKind Kind => Pcre2DirectProgramKind.Pcre2Backtracking;

    internal Pcre2BacktrackingProgram Program { get; }
}

internal enum Pcre2DirectProgramKind : byte
{
    None = 0,
    Utf8Regex = 1,
    ManagedRegex = 2,
    Utf8RegexEquivalent = 3,
    Pcre2Literal = 4,
    Pcre2Character = 5,
    Pcre2Backtracking = 6,
}

internal readonly record struct Pcre2OperationPrograms(
    IPcre2DirectProgram IsMatch,
    IPcre2DirectProgram Count,
    IPcre2DirectProgram Enumerate,
    IPcre2DirectProgram Match,
    IPcre2DirectProgram Replace);

internal readonly record struct Pcre2CandidateSearchProgram(IPcre2DirectProgram Program);

internal readonly record struct Pcre2FullVerificationProgram(Utf8Pcre2Regex.Pcre2ExecutionKind LegacyExecutionKind);

internal interface IPcre2TranslationProgram
{
    bool IsActive { get; }
}

internal sealed class Pcre2NoTranslationProgram : IPcre2TranslationProgram
{
    internal static Pcre2NoTranslationProgram Instance { get; } = new();

    private Pcre2NoTranslationProgram()
    {
    }

    public bool IsActive => false;
}

internal sealed class Pcre2Utf8TranslationProgram : IPcre2TranslationProgram
{
    internal Pcre2Utf8TranslationProgram(string pattern, RegexOptions options, Utf8Regex regex)
    {
        Pattern = pattern;
        Options = options;
        Regex = regex;
    }

    public bool IsActive => true;

    internal string Pattern { get; }

    internal RegexOptions Options { get; }

    internal Utf8Regex Regex { get; }
}

internal sealed class Pcre2CompiledProgram
{
    internal Pcre2CompiledProgram(
        Pcre2CompileRequest request,
        IPcre2Utf8ProgramSlot primaryUtf8,
        IPcre2Utf8ProgramSlot searchEquivalentUtf8,
        IPcre2ManagedProgramSlot managed,
        IPcre2TranslationProgram translation,
        Pcre2OperationPrograms operations,
        Pcre2CandidateSearchProgram candidateSearch,
        Pcre2FullVerificationProgram fullVerification,
        IPcre2SyntaxTree syntaxTree,
        string[] groupNames,
        Pcre2NameEntry[] nameEntries)
    {
        Request = request;
        PrimaryUtf8 = primaryUtf8;
        SearchEquivalentUtf8 = searchEquivalentUtf8;
        Managed = managed;
        Translation = translation;
        Operations = operations;
        CandidateSearch = candidateSearch;
        FullVerification = fullVerification;
        SyntaxTree = syntaxTree;
        GroupNames = [.. groupNames];
        NameEntries = [.. nameEntries];
    }

    internal Pcre2CompileRequest Request { get; }

    internal IPcre2Utf8ProgramSlot PrimaryUtf8 { get; }

    internal IPcre2Utf8ProgramSlot SearchEquivalentUtf8 { get; }

    internal IPcre2ManagedProgramSlot Managed { get; }

    internal IPcre2TranslationProgram Translation { get; }

    internal Pcre2OperationPrograms Operations { get; }

    internal Pcre2CandidateSearchProgram CandidateSearch { get; }

    internal Pcre2FullVerificationProgram FullVerification { get; }

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
            Pcre2EmptyUtf8ProgramSlot.Instance,
            Pcre2EmptyManagedProgramSlot.Instance,
            Pcre2NoTranslationProgram.Instance,
            new Pcre2OperationPrograms(none, none, none, none, none),
            new Pcre2CandidateSearchProgram(none),
            new Pcre2FullVerificationProgram(Utf8Pcre2Regex.Pcre2ExecutionKind.Unimplemented),
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
            legacy.SearchEquivalentUtf8,
            legacy.Managed,
            legacy.Translation,
            operations,
            new Pcre2CandidateSearchProgram(direct),
            legacy.FullVerification,
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
            legacy.SearchEquivalentUtf8,
            legacy.Managed,
            legacy.Translation,
            operations,
            new Pcre2CandidateSearchProgram(direct),
            legacy.FullVerification,
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
        return new Pcre2CompiledProgram(
            legacy.Request,
            legacy.PrimaryUtf8,
            legacy.SearchEquivalentUtf8,
            legacy.Managed,
            legacy.Translation,
            operations,
            new Pcre2CandidateSearchProgram(direct),
            legacy.FullVerification,
            syntaxTree,
            legacy.GroupNames,
            legacy.NameEntries);
    }
}

internal static class Pcre2ProgramInvariant
{
    internal static void Validate(Pcre2CompiledProgram program)
    {
        ArgumentNullException.ThrowIfNull(program);
        ValidateDirectProgram(program.Operations.IsMatch, program);
        ValidateDirectProgram(program.Operations.Count, program);
        ValidateDirectProgram(program.Operations.Enumerate, program);
        ValidateDirectProgram(program.Operations.Match, program);
        ValidateDirectProgram(program.Operations.Replace, program);
    }

    private static void ValidateDirectProgram(IPcre2DirectProgram directProgram, Pcre2CompiledProgram program)
    {
        ArgumentNullException.ThrowIfNull(directProgram);
        if (directProgram is Pcre2Utf8DirectProgram utf8Program)
        {
            var isOwnedUtf8Program = program.PrimaryUtf8 is Pcre2Utf8ProgramSlot primary && ReferenceEquals(primary.Regex, utf8Program.Regex) ||
                program.SearchEquivalentUtf8 is Pcre2Utf8ProgramSlot equivalent && ReferenceEquals(equivalent.Regex, utf8Program.Regex);
            if (!isOwnedUtf8Program)
            {
                throw new InvalidOperationException("A direct UTF-8 backend must be owned by its compiled PCRE2 program.");
            }
        }

        if (directProgram is Pcre2ManagedDirectProgram managedProgram &&
            (program.Managed is not Pcre2ManagedProgramSlot managed || !ReferenceEquals(managed.Regex, managedProgram.Regex)))
        {
            throw new InvalidOperationException("A direct managed backend must be owned by its compiled PCRE2 program.");
        }
    }
}

internal static class Pcre2Runner
{
    internal static bool TryIsMatch(
        Pcre2CompiledProgram compiledProgram,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        out bool result)
    {
        var program = compiledProgram.Operations.IsMatch;
        if (program is Pcre2LiteralDirectProgram literalProgram)
        {
            result = ExecuteLiteral(
                literalProgram.Program,
                ref input,
                start,
                matchOptions,
                compiledProgram.Request).Success;
            return true;
        }

        if (program is Pcre2CharacterDirectProgram characterProgram)
        {
            result = ExecuteCharacter(
                characterProgram.Program,
                ref input,
                start,
                matchOptions,
                compiledProgram.Request).Success;
            return true;
        }

        if (program is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            result = ExecuteBacktracking(
                backtrackingProgram.Program,
                ref input,
                start,
                matchOptions,
                compiledProgram.Request).Success;
            return true;
        }

        if (program is Pcre2Utf8DirectProgram utf8Program)
        {
            result = utf8Program.Regex.ByteOffsetExecution.IsMatch(input, start);
            return true;
        }

        if (program is Pcre2ManagedDirectProgram managedProgram)
        {
            var subject = input.GetDecodedString();
            result = managedProgram.Regex.IsMatch(subject, input.Project(start).Value);
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
        out Pcre2GroupData result)
    {
        Pcre2CharacterMatch directMatch;
        if (compiledProgram.Operations.Match is Pcre2LiteralDirectProgram literalProgram)
        {
            var literalMatch = ExecuteLiteral(
                literalProgram.Program,
                ref input,
                start,
                matchOptions,
                compiledProgram.Request);
            directMatch = literalMatch.Success
                ? Pcre2CharacterMatch.Create(literalMatch.StartOffsetInBytes, literalMatch.EndOffsetInBytes)
                : Pcre2CharacterMatch.NoMatch;
        }
        else if (compiledProgram.Operations.Match is Pcre2CharacterDirectProgram characterProgram)
        {
            directMatch = ExecuteCharacter(
                characterProgram.Program,
                ref input,
                start,
                matchOptions,
                compiledProgram.Request);
        }
        else if (compiledProgram.Operations.Match is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            directMatch = ExecuteBacktracking(
                backtrackingProgram.Program,
                ref input,
                start,
                matchOptions,
                compiledProgram.Request);
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

        var byteRange = new Utf8ByteRange(
            new Utf8BytePosition(directMatch.StartOffsetInBytes),
            new Utf8BytePosition(directMatch.EndOffsetInBytes));
        var utf16Range = input.ProjectRange(byteRange);
        result = new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = directMatch.StartOffsetInBytes,
            EndOffsetInBytes = directMatch.EndOffsetInBytes,
            StartOffsetInUtf16 = utf16Range.Start.Value,
            EndOffsetInUtf16 = utf16Range.End.Value,
        };
        return true;
    }

    private static Pcre2LiteralMatch ExecuteLiteral(
        Pcre2LiteralProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
    {
        try
        {
            var budget = new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout);
            return Pcre2LiteralRunner.Match(program, ref input, start, matchOptions, ref budget);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
        }
    }

    private static Pcre2CharacterMatch ExecuteCharacter(
        Pcre2CharacterProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
    {
        try
        {
            var budget = new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout);
            return Pcre2CharacterRunner.Match(program, ref input, start, matchOptions, ref budget);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
        }
    }

    private static Pcre2CharacterMatch ExecuteBacktracking(
        Pcre2BacktrackingProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
    {
        try
        {
            var budget = new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout);
            return Pcre2BacktrackingRunner.Match(program, ref input, start, matchOptions, ref budget);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
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
        out int result)
    {
        var program = compiledProgram.Operations.Count;
        if (TryCreateCursor(compiledProgram, input, start, matchOptions, out var cursor))
        {
            result = 0;
            while (cursor.MoveNext())
            {
                result = checked(result + 1);
            }

            return true;
        }

        if (program is Pcre2Utf8DirectProgram utf8Program)
        {
            result = utf8Program.Regex.ByteOffsetExecution.Count(input, start);
            return true;
        }

        if (program is Pcre2ManagedDirectProgram managedProgram)
        {
            var subject = input.GetDecodedString();
            result = managedProgram.Regex.Count(subject, input.Project(start).Value);
            return true;
        }

        result = 0;
        return false;
    }

    internal static bool TryCreateCursor(
        Pcre2CompiledProgram compiledProgram,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        out Pcre2GlobalMatchCursor cursor)
    {
        if (compiledProgram.Operations.Enumerate is Pcre2LiteralDirectProgram literalProgram)
        {
            cursor = Pcre2GlobalMatchCursor.CreateLiteral(
                literalProgram.Program, input, start, matchOptions, compiledProgram.Request);
            return true;
        }

        if (compiledProgram.Operations.Enumerate is Pcre2CharacterDirectProgram characterProgram)
        {
            cursor = Pcre2GlobalMatchCursor.CreateCharacter(
                characterProgram.Program, input, start, matchOptions, compiledProgram.Request);
            return true;
        }

        if (compiledProgram.Operations.Enumerate is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            cursor = Pcre2GlobalMatchCursor.CreateBacktracking(
                backtrackingProgram.Program, input, start, matchOptions, compiledProgram.Request);
            return true;
        }

        cursor = default;
        return false;
    }
}

internal ref struct Pcre2GlobalMatchCursor
{
    private readonly Pcre2GlobalCursorKind _kind;
    private Pcre2LiteralGlobalMatchCursor _literal;
    private Pcre2CharacterGlobalMatchCursor _character;
    private Pcre2BacktrackingGlobalMatchCursor _backtracking;

    private Pcre2GlobalMatchCursor(Pcre2LiteralGlobalMatchCursor literal)
    {
        _kind = Pcre2GlobalCursorKind.Literal;
        _literal = literal;
        _character = default;
        _backtracking = default;
    }

    private Pcre2GlobalMatchCursor(Pcre2CharacterGlobalMatchCursor character)
    {
        _kind = Pcre2GlobalCursorKind.Character;
        _literal = default;
        _character = character;
        _backtracking = default;
    }

    private Pcre2GlobalMatchCursor(Pcre2BacktrackingGlobalMatchCursor backtracking)
    {
        _kind = Pcre2GlobalCursorKind.Backtracking;
        _literal = default;
        _character = default;
        _backtracking = backtracking;
    }

    internal Pcre2GroupData Current => _kind switch
    {
        Pcre2GlobalCursorKind.Literal => _literal.Current,
        Pcre2GlobalCursorKind.Character => _character.Current,
        Pcre2GlobalCursorKind.Backtracking => _backtracking.Current,
        _ => default,
    };

    internal static Pcre2GlobalMatchCursor CreateLiteral(
        Pcre2LiteralProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request) =>
        new(new Pcre2LiteralGlobalMatchCursor(program, input, start, matchOptions, request));

    internal static Pcre2GlobalMatchCursor CreateCharacter(
        Pcre2CharacterProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request) =>
        new(new Pcre2CharacterGlobalMatchCursor(program, input, start, matchOptions, request));

    internal static Pcre2GlobalMatchCursor CreateBacktracking(
        Pcre2BacktrackingProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request) =>
        new(new Pcre2BacktrackingGlobalMatchCursor(program, input, start, matchOptions, request));

    internal bool MoveNext() => _kind switch
    {
        Pcre2GlobalCursorKind.Literal => _literal.MoveNext(),
        Pcre2GlobalCursorKind.Character => _character.MoveNext(),
        Pcre2GlobalCursorKind.Backtracking => _backtracking.MoveNext(),
        _ => false,
    };

    private enum Pcre2GlobalCursorKind : byte
    {
        None = 0,
        Literal = 1,
        Character = 2,
        Backtracking = 3,
    }
}

internal ref struct Pcre2BacktrackingGlobalMatchCursor
{
    private readonly Pcre2BacktrackingProgram _program;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private readonly Pcre2MatchOptions _matchOptions;
    private Pcre2ResourceBudget _budget;
    private Utf8ProjectionCursor _projection;
    private Utf8BytePosition _restartPosition;
    private Pcre2GlobalRetryState _retryState;

    internal Pcre2BacktrackingGlobalMatchCursor(
        Pcre2BacktrackingProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
    {
        _program = program;
        _input = input;
        _request = request;
        _matchOptions = matchOptions;
        _budget = new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout);
        _projection = input.CreateProjectionCursor();
        _restartPosition = start;
        _retryState = Pcre2GlobalRetryState.Search;
        Current = default;
    }

    internal Pcre2GroupData Current { get; private set; }

    internal bool MoveNext()
    {
        while (_retryState != Pcre2GlobalRetryState.Finished)
        {
            var retryingEmptyAtSamePosition = _retryState == Pcre2GlobalRetryState.EmptyAtSamePosition;
            var options = retryingEmptyAtSamePosition
                ? _matchOptions | Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart
                : _matchOptions;

            Pcre2CharacterMatch match;
            try
            {
                match = Pcre2BacktrackingRunner.Match(
                    _program,
                    ref _input,
                    _restartPosition,
                    options,
                    ref _budget);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
            }

            if (match.Success)
            {
                Current = Pcre2GlobalCursorProjection.Project(match.StartOffsetInBytes, match.EndOffsetInBytes, ref _projection);
                _restartPosition = new Utf8BytePosition(match.ConsumedEndOffsetInBytes);
                _retryState = match.StartOffsetInBytes == match.EndOffsetInBytes
                    ? Pcre2GlobalRetryState.EmptyAtSamePosition
                    : Pcre2GlobalRetryState.Search;
                return true;
            }

            if (!retryingEmptyAtSamePosition ||
                !Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(ref _input, _request.Settings, _restartPosition, out _restartPosition))
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

internal ref struct Pcre2CharacterGlobalMatchCursor
{
    private readonly Pcre2CharacterProgram _program;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private readonly Pcre2MatchOptions _matchOptions;
    private Pcre2ResourceBudget _budget;
    private Utf8ProjectionCursor _projection;
    private Utf8BytePosition _restartPosition;
    private Pcre2GlobalRetryState _retryState;

    internal Pcre2CharacterGlobalMatchCursor(
        Pcre2CharacterProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
    {
        _program = program;
        _input = input;
        _request = request;
        _matchOptions = matchOptions;
        _budget = new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout);
        _projection = input.CreateProjectionCursor();
        _restartPosition = start;
        _retryState = Pcre2GlobalRetryState.Search;
        Current = default;
    }

    internal Pcre2GroupData Current { get; private set; }

    internal bool MoveNext()
    {
        while (_retryState != Pcre2GlobalRetryState.Finished)
        {
            var retryingEmptyAtSamePosition = _retryState == Pcre2GlobalRetryState.EmptyAtSamePosition;
            var options = retryingEmptyAtSamePosition
                ? _matchOptions | Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart
                : _matchOptions;

            Pcre2CharacterMatch match;
            try
            {
                match = Pcre2CharacterRunner.Match(
                    _program,
                    ref _input,
                    _restartPosition,
                    options,
                    ref _budget);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
            }

            if (match.Success)
            {
                Current = Pcre2GlobalCursorProjection.Project(match.StartOffsetInBytes, match.EndOffsetInBytes, ref _projection);
                _restartPosition = new Utf8BytePosition(match.ConsumedEndOffsetInBytes);
                _retryState = match.StartOffsetInBytes == match.EndOffsetInBytes
                    ? Pcre2GlobalRetryState.EmptyAtSamePosition
                    : Pcre2GlobalRetryState.Search;
                return true;
            }

            if (!retryingEmptyAtSamePosition ||
                !Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(ref _input, _request.Settings, _restartPosition, out _restartPosition))
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

internal ref struct Pcre2LiteralGlobalMatchCursor
{
    private readonly Pcre2LiteralProgram _program;
    private Utf8ValidatedInput _input;
    private readonly Pcre2CompileRequest _request;
    private readonly Pcre2MatchOptions _matchOptions;
    private Pcre2ResourceBudget _budget;
    private Utf8ProjectionCursor _projection;
    private Utf8BytePosition _restartPosition;
    private Pcre2GlobalRetryState _retryState;

    internal Pcre2LiteralGlobalMatchCursor(
        Pcre2LiteralProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition start,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
    {
        _program = program;
        _input = input;
        _request = request;
        _matchOptions = matchOptions;
        _budget = new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout);
        _projection = input.CreateProjectionCursor();
        _restartPosition = start;
        _retryState = Pcre2GlobalRetryState.Search;
        Current = default;
    }

    internal Pcre2GroupData Current { get; private set; }

    internal bool MoveNext()
    {
        while (_retryState != Pcre2GlobalRetryState.Finished)
        {
            var retryingEmptyAtSamePosition = _retryState == Pcre2GlobalRetryState.EmptyAtSamePosition;
            var options = retryingEmptyAtSamePosition
                ? _matchOptions | Pcre2MatchOptions.Anchored | Pcre2MatchOptions.NotEmptyAtStart
                : _matchOptions;

            Pcre2LiteralMatch match;
            try
            {
                match = Pcre2LiteralRunner.Match(
                    _program,
                    ref _input,
                    _restartPosition,
                    options,
                    ref _budget);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
            }

            if (match.Success)
            {
                Current = Pcre2GlobalCursorProjection.Project(match.StartOffsetInBytes, match.EndOffsetInBytes, ref _projection);
                _restartPosition = new Utf8BytePosition(match.ConsumedEndOffsetInBytes);
                _retryState = match.StartOffsetInBytes == match.EndOffsetInBytes
                    ? Pcre2GlobalRetryState.EmptyAtSamePosition
                    : Pcre2GlobalRetryState.Search;
                return true;
            }

            if (!retryingEmptyAtSamePosition ||
                !Pcre2GlobalCursorMovement.TryAdvanceAfterEmpty(ref _input, _request.Settings, _restartPosition, out _restartPosition))
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
    internal static Pcre2GroupData Project(int startOffsetInBytes, int endOffsetInBytes, ref Utf8ProjectionCursor projection)
    {
        var start = new Utf8BytePosition(startOffsetInBytes);
        var end = new Utf8BytePosition(endOffsetInBytes);
        var startInUtf16 = projection.Project(start);
        var endInUtf16 = projection.Project(end);
        return new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = start.Value,
            EndOffsetInBytes = end.Value,
            StartOffsetInUtf16 = startInUtf16.Value,
            EndOffsetInUtf16 = endInUtf16.Value,
        };
    }
}

internal static class Pcre2GlobalCursorMovement
{
    internal static bool TryAdvanceAfterEmpty(
        ref Utf8ValidatedInput input,
        Utf8Pcre2CompileSettings settings,
        Utf8BytePosition position,
        out Utf8BytePosition next)
    {
        var bytes = input.Bytes;
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

internal sealed class Pcre2InvocationState
{
    internal Pcre2InvocationState(int captureSlotCount, Utf8Pcre2ExecutionLimits limits, TimeSpan timeout)
    {
        Captures = new Pcre2GroupData[captureSlotCount];
        Backtracking = [];
        GlobalIteration = new Pcre2GlobalIterationState();
        Budget = new Pcre2ResourceBudget(limits, timeout);
    }

    internal Pcre2GroupData[] Captures { get; }

    internal List<Pcre2BacktrackingState> Backtracking { get; }

    internal Pcre2GlobalIterationState GlobalIteration { get; }

    internal Pcre2ResourceBudget Budget { get; }
}

internal readonly record struct Pcre2BacktrackingState(int Instruction, int InputOffsetInBytes, int CaptureCheckpoint);

internal sealed class Pcre2GlobalIterationState
{
    internal int SearchOffsetInBytes { get; set; }

    internal int PreviousStartOffsetInBytes { get; set; } = -1;

    internal int PreviousEndOffsetInBytes { get; set; } = -1;
}

internal struct Pcre2ResourceBudget
{
    private readonly Utf8ExecutionDeadline _deadline;

    internal Pcre2ResourceBudget(Utf8Pcre2ExecutionLimits limits, TimeSpan timeout)
    {
        Limits = limits;
        Timeout = timeout;
        _deadline = Utf8ExecutionDeadline.Start(timeout);
        CandidateSteps = 0;
        BacktrackingSteps = 0;
        Depth = 0;
        HeapBytes = 0;
    }

    internal Utf8Pcre2ExecutionLimits Limits { get; }

    internal TimeSpan Timeout { get; }

    internal ulong CandidateSteps { get; private set; }

    internal ulong BacktrackingSteps { get; private set; }

    internal uint Depth { get; private set; }

    internal ulong HeapBytes { get; private set; }

    internal bool RequiresCandidateMetering => !_deadline.IsInfinite || Limits.MatchLimit != 0;

    internal void ChargeCandidate()
    {
        CandidateSteps++;
        _deadline.Step();
        if (Limits.MatchLimit != 0 && CandidateSteps > Limits.MatchLimit)
        {
            throw new Pcre2MatchException("The PCRE2 match limit was exceeded.", "MatchLimit");
        }
    }

    internal void ChargeBacktracking()
    {
        BacktrackingSteps++;
        _deadline.Step();
        if (Limits.MatchLimit != 0 &&
            (CandidateSteps > Limits.MatchLimit || BacktrackingSteps > Limits.MatchLimit - CandidateSteps))
        {
            throw new Pcre2MatchException("The PCRE2 match limit was exceeded.", "MatchLimit");
        }
    }

    internal void ChargeFrame(uint depth, ulong heapBytes)
    {
        Depth = Math.Max(Depth, depth);
        if (Limits.DepthLimit != 0 && depth > Limits.DepthLimit)
        {
            throw new Pcre2MatchException("The PCRE2 depth limit was exceeded.", "DepthLimit");
        }

        ChargeHeap(heapBytes);
    }

    internal void ChargeHeap(ulong heapBytes)
    {
        HeapBytes = Math.Max(HeapBytes, heapBytes);
        if (Limits.HeapLimitInBytes != 0 && heapBytes > Limits.HeapLimitInBytes)
        {
            throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", "HeapLimit");
        }
    }

    internal void SetDepth(uint depth) => Depth = depth;

    internal void SetHeapBytes(ulong heapBytes) => HeapBytes = heapBytes;
}

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

internal enum Pcre2BackendCapability : byte
{
    CandidateSearch = 0,
    FullVerification = 1,
    GlobalIteration = 2,
    Replacement = 3,
}
