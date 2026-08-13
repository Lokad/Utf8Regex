using System.Text;
using System.Text.RegularExpressions;

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

internal enum Pcre2DirectProgramKind : byte
{
    None = 0,
    Utf8Regex = 1,
    ManagedRegex = 2,
    Utf8RegexEquivalent = 3,
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

    internal string[] GroupNames { get; }

    internal Pcre2NameEntry[] NameEntries { get; }
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
        IPcre2DirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out bool result)
    {
        if (program is Pcre2Utf8DirectProgram utf8Program)
        {
            result = utf8Program.Regex.IsMatchFromUtf16Offset(input, Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]));
            return true;
        }

        if (program is Pcre2ManagedDirectProgram managedProgram)
        {
            var subject = Encoding.UTF8.GetString(input);
            result = managedProgram.Regex.IsMatch(subject, Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]));
            return true;
        }

        result = false;
        return false;
    }
}

internal static class Pcre2GlobalOperationDriver
{
    internal static bool TryCount(
        IPcre2DirectProgram program,
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out int result)
    {
        if (program is Pcre2Utf8DirectProgram utf8Program)
        {
            result = utf8Program.Regex.CountFromUtf16Offset(input, Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]));
            return true;
        }

        if (program is Pcre2ManagedDirectProgram managedProgram)
        {
            var subject = Encoding.UTF8.GetString(input);
            result = managedProgram.Regex.Count(subject, Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]));
            return true;
        }

        result = 0;
        return false;
    }
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

internal sealed class Pcre2ResourceBudget
{
    internal Pcre2ResourceBudget(Utf8Pcre2ExecutionLimits limits, TimeSpan timeout)
    {
        Limits = limits;
        Timeout = timeout;
    }

    internal Utf8Pcre2ExecutionLimits Limits { get; }

    internal TimeSpan Timeout { get; }

    internal ulong CandidateSteps { get; private set; }

    internal ulong BacktrackingSteps { get; private set; }

    internal uint Depth { get; private set; }

    internal ulong HeapBytes { get; private set; }

    internal void ChargeCandidate() => CandidateSteps++;

    internal void ChargeBacktracking() => BacktrackingSteps++;

    internal void SetDepth(uint depth) => Depth = depth;

    internal void SetHeapBytes(ulong heapBytes) => HeapBytes = heapBytes;
}

internal enum Pcre2SyntaxNodeKind : byte
{
    LegacyPattern = 0,
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
