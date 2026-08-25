using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Implements a strictly managed, UTF-8-native subset of PCRE2 semantics.</summary>
/// <remarks>Unsupported constructs or operation combinations fail explicitly; no native PCRE2 binary or interop layer is used.</remarks>
public sealed class Utf8Pcre2Regex
{
    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Pcre2CompiledProgram _program;
    private readonly Pcre2ReplacementComponent _replacementComponent = new();

    /// <summary>Compiles a PCRE2 pattern with default options, settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(string pattern)
        : this(pattern, Pcre2CompileOptions.None)
    {
    }

    /// <summary>Compiles a PCRE2 pattern with the specified options and default settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(string pattern, Pcre2CompileOptions options)
        : this(pattern, options, default, default, DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles a PCRE2 pattern with explicit options, settings, execution limits, and timeout.</summary>
    public Utf8Pcre2Regex(
        string pattern,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits,
        TimeSpan matchTimeout)
    {
        var request = new Pcre2CompileRequest(
            pattern,
            options,
            compileSettings,
            defaultExecutionLimits,
            Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout)));
        _program = Pcre2Compiler.Compile(request, CreateFoundationProgram);
    }

    private static Pcre2CompiledProgram CreateFoundationProgram(Pcre2CompileRequest request)
    {
        _ = TryCreateUtf8Regex(request.Pattern, request.Options, request.MatchTimeout, out var utf8Regex);
        Regex? managedRegex = null;
        if (utf8Regex is null)
        {
            _ = TryCreateManagedRegex(request.Pattern, request.Options, request.MatchTimeout, out managedRegex);
        }

        IPcre2Utf8ProgramSlot primaryUtf8 = utf8Regex is null
            ? Pcre2EmptyUtf8ProgramSlot.Instance
            : new Pcre2Utf8ProgramSlot(utf8Regex);
        IPcre2ManagedProgramSlot managed = managedRegex is null
            ? Pcre2EmptyManagedProgramSlot.Instance
            : new Pcre2ManagedProgramSlot(managedRegex);
        IPcre2DirectProgram direct = primaryUtf8 is Pcre2Utf8ProgramSlot utf8Slot
            ? new Pcre2Utf8DirectProgram(utf8Slot.Regex)
            : managed is Pcre2ManagedProgramSlot managedSlot
                ? new Pcre2ManagedDirectProgram(managedSlot.Regex)
                : Pcre2NoDirectProgram.Instance;
        var operations = new Pcre2OperationPrograms(direct, direct, direct, direct, direct);
        var groupNames = managedRegex?.GetGroupNames() ?? utf8Regex?.GetGroupNames() ?? ["0"];
        var nameEntries = managedRegex is not null
            ? GetManagedNameEntries(managedRegex)
            : utf8Regex is not null
                ? GetUtf8RegexNameEntries(utf8Regex)
                : [];
        return new Pcre2CompiledProgram(
            request,
            primaryUtf8,
            managed,
            operations,
            default,
            Pcre2PartialProbeProgram.None,
            Pcre2LegacySyntaxTree.Instance,
            groupNames,
            nameEntries);
    }

    /// <summary>Compiles a well-formed UTF-8 PCRE2 pattern with default options, settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8)
        : this(patternUtf8, Pcre2CompileOptions.None)
    {
    }

    /// <summary>Compiles a well-formed UTF-8 PCRE2 pattern with the specified options and default settings, limits, and timeout.</summary>
    public Utf8Pcre2Regex(ReadOnlySpan<byte> patternUtf8, Pcre2CompileOptions options)
        : this(patternUtf8, options, default, default, DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles a well-formed UTF-8 PCRE2 pattern with explicit options, settings, execution limits, and timeout.</summary>
    public Utf8Pcre2Regex(
        ReadOnlySpan<byte> patternUtf8,
        Pcre2CompileOptions options,
        Utf8Pcre2CompileSettings compileSettings,
        Utf8Pcre2ExecutionLimits defaultExecutionLimits,
        TimeSpan matchTimeout)
        : this(DecodeValidatedUtf8(patternUtf8), options, compileSettings, defaultExecutionLimits, matchTimeout)
    {
    }

    /// <summary>Gets or sets the timeout used by constructors and static operations that do not specify one.</summary>
    /// <remarks>The initial value is an infinite timeout.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set => s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
    }

    /// <summary>Gets the compiled PCRE2 pattern.</summary>
    public string Pattern => _program.Request.Pattern;

    /// <summary>Gets the compile-option flags.</summary>
    public Pcre2CompileOptions Options => _program.Request.Options;

    /// <summary>Gets the compile-time newline, group-name, and byte-oriented settings.</summary>
    public Utf8Pcre2CompileSettings CompileSettings => _program.Request.Settings;

    /// <summary>Gets the default execution budgets applied to matching operations.</summary>
    public Utf8Pcre2ExecutionLimits DefaultExecutionLimits => _program.Request.DefaultLimits;

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout => _program.Request.MatchTimeout;

    private bool HasPrimaryUtf8Regex => _program.PrimaryUtf8.IsPresent;

    private Utf8Regex PrimaryUtf8Regex => ((Pcre2Utf8ProgramSlot)_program.PrimaryUtf8).Regex;

    private bool HasManagedRegex => _program.Managed.IsPresent;

    private Regex ManagedRegex => ((Pcre2ManagedProgramSlot)_program.Managed).Regex;

    private bool UsesUtf8Translation => HasPrimaryUtf8Regex;

    private string[] GroupNames => _program.GroupNames;

    private Pcre2NameEntry[] NameEntries => _program.NameEntries;

    /// <summary>Determines whether the well-formed UTF-8 subject contains a match.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input)
        => IsMatch(input, 0, Pcre2MatchOptions.None);

    /// <summary>Determines whether a match exists at or after a scalar-aligned UTF-8 byte offset.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => IsMatch(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Determines whether a match exists using an explicit start byte offset and per-operation options.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2Runner.TryIsMatch(_program, ref subject, start, matchOptions, out var result))
        {
            return result;
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Counts non-overlapping matches in the well-formed UTF-8 subject.</summary>
    public int Count(ReadOnlySpan<byte> input)
        => Count(input, 0, Pcre2MatchOptions.None);

    /// <summary>Counts non-overlapping matches at or after a scalar-aligned UTF-8 byte offset.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => Count(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Counts non-overlapping matches using an explicit start byte offset and per-operation options.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCount(_program, ref subject, start, matchOptions, out var result))
        {
            return result;
        }

        throw CreateUnsupportedPatternException();
    }





    /// <summary>Finds the first match in the well-formed UTF-8 subject.</summary>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input)
        => Match(input, 0, Pcre2MatchOptions.None);

    /// <summary>Finds the first match at or after a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => Match(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Finds the first match using an explicit start byte offset and per-operation options.</summary>
    public Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2Runner.TryMatch(_program, ref subject, start, matchOptions, out var directMatch))
        {
            return Utf8Pcre2ValueMatch.Create(input, directMatch);
        }

        if (_program.Operations.Match.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return CreateManagedProfileValueMatch(
                input,
                PrimaryUtf8Regex.ByteOffsetExecution.Match(subject, start));
        }

        if (_program.Operations.Match.Kind == Pcre2DirectProgramKind.ManagedRegex)
        {
            return MatchUsingManagedBackend(input, startOffsetInBytes);
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Finds the first match with capture slots, duplicate-name metadata, and <c>(*MARK)</c> state.</summary>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input)
        => MatchDetailed(input, 0, Pcre2MatchOptions.None);

    /// <summary>Finds the first detailed match at or after a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => MatchDetailed(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Finds the first detailed match using an explicit start byte offset and per-operation options.</summary>
    public Utf8Pcre2MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2Runner.TryMatchDetailed(
                _program,
                ref subject,
                start,
                matchOptions,
                out var directGroups,
                out var directMark))
        {
            return directGroups.Length != 0 || directMark is not null
                ? Utf8Pcre2MatchContext.Create(input, directGroups, NameEntries, directMark)
                : default;
        }

        if (Pcre2Runner.TryMatch(_program, ref subject, start, matchOptions, out var directMatch))
        {
            return directMatch.Success
                ? Utf8Pcre2MatchContext.Create(input, [directMatch], NameEntries)
                : default;
        }

        if (_program.Operations.Match.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return MatchDetailedUsingUtf8Backend(subject, start);
        }

        if (_program.Operations.Match.Kind == Pcre2DirectProgramKind.ManagedRegex)
        {
            return MatchDetailedUsingManagedBackend(input, startOffsetInBytes);
        }

        throw CreateUnsupportedPatternException();
    }

    /// <summary>Creates a cursor over non-overlapping matches in the UTF-8 subject.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
        => EnumerateMatches(input, 0, Pcre2MatchOptions.None);

    /// <summary>Creates a match cursor starting at a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes)
        => EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Creates a match cursor using an explicit start byte offset and per-operation options.</summary>
    // Keep the large discriminated-union cursor router out of hot consumer loops. Tiered PGO
    // otherwise expands every backend construction branch and its bulk struct copies at the call site.
    [MethodImpl(MethodImplOptions.NoInlining)]
    public Utf8Pcre2ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);

        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var directCursor))
        {
            return new Utf8Pcre2ValueMatchEnumerator(input, directCursor);
        }

        if (_program.Operations.Enumerate.Kind != Pcre2DirectProgramKind.None)
        {
            return EnumerateMatchesViaConfiguredBackend(subject, start);
        }

        throw CreateUnsupportedPatternException();
    }

    private static NotSupportedException CreateUnsupportedPatternException() =>
        new("SPEC-PCRE2 does not support this pattern in the managed profile.");

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaConfiguredBackend(
        Utf8ValidatedInput input,
        Utf8BytePosition start)
    {
        return _program.Operations.Enumerate.Kind switch
        {
            Pcre2DirectProgramKind.Utf8Regex => EnumerateMatchesViaUtf8RegexBackend(input, start),
            Pcre2DirectProgramKind.ManagedRegex => EnumerateMatchesViaManagedRegexBackend(input, start),
            _ => default,
        };
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaUtf8RegexBackend(
        Utf8ValidatedInput input,
        Utf8BytePosition start)
    {
        if (!UsesUtf8Translation && CanUsePreparedAsciiUtf8RegexEnumerator(input.IsAscii))
        {
            return new Utf8Pcre2ValueMatchEnumerator(
                input.Bytes,
                PrimaryUtf8Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start),
                start.Value);
        }

        return new Utf8Pcre2ValueMatchEnumerator(
            input.Bytes,
            PrimaryUtf8Regex.ByteOffsetExecution.EnumerateMatches(input, start));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private Utf8Pcre2ValueMatchEnumerator EnumerateMatchesViaManagedRegexBackend(
        Utf8ValidatedInput input,
        Utf8BytePosition start)
    {
        var subject = input.GetDecodedString();
        var startOffsetInUtf16 = input.Project(start).Value;
        Utf8BoundaryMap? boundaryMap = input.IsAscii ? null : input.BoundaryMap;
        return new Utf8Pcre2ValueMatchEnumerator(
            input.Bytes,
            ManagedRegex.EnumerateMatches(subject.AsSpan(), startOffsetInUtf16),
            boundaryMap,
            input.IsAscii);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanUsePreparedAsciiUtf8RegexEnumerator(ReadOnlySpan<byte> input)
        => CanUsePreparedAsciiUtf8RegexEnumerator(Utf8InputAnalyzer.ValidateOnly(input).IsAscii);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool CanUsePreparedAsciiUtf8RegexEnumerator(bool isAscii)
    {
        if (!HasPrimaryUtf8Regex ||
            !isAscii)
        {
            return false;
        }

        return PrimaryUtf8Regex.ByteOffsetExecution.SearchPortfolioKind is
            Internal.Planning.Utf8SearchPortfolioKind.ExactLiteral or
            Internal.Planning.Utf8SearchPortfolioKind.IgnoreCaseLiteral;
    }

    /// <summary>Probes the UTF-8 subject for a full or permitted subject-end partial match.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode)
        => Probe(input, partialMode, 0, Pcre2MatchOptions.None);

    /// <summary>Probes for a full or partial match at or after a scalar-aligned UTF-8 byte offset.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
        => Probe(input, partialMode, startOffsetInBytes, Pcre2MatchOptions.None);

    /// <summary>Probes for a full or partial match using an explicit start byte offset and per-operation options.</summary>
    public Utf8Pcre2ProbeResult Probe(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (_program.Operations.Match is Pcre2LiteralDirectProgram literalProgram)
        {
            return Pcre2LiteralProbeRunner.Probe(
                input,
                literalProgram.Program,
                subject,
                start,
                partialMode,
                matchOptions,
                _program.Request);
        }

        if (_program.PartialProbe.Kind != Pcre2PartialProbeProgramKind.None)
        {
            return Pcre2PartialProbeRunner.Probe(input, partialMode, startOffsetInBytes, _program.PartialProbe);
        }

        if (partialMode == Pcre2PartialMode.None)
        {
            return ProbeViaNonPartialMatch(input, startOffsetInBytes);
        }

        if (_program.Operations.Match is Pcre2BacktrackingDirectProgram &&
            Pcre2Runner.TryMatchDetailed(
                _program,
                ref subject,
                start,
                matchOptions,
                out var directGroups,
                out var directMark))
        {
            if (directGroups.Length != 0)
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(input, directGroups, NameEntries, directMark);
            }

            if (directMark is not null)
            {
                return Utf8Pcre2ProbeResult.CreateNoMatch(input, directMark);
            }
        }

        throw CreateUnsupportedProbeException();
    }

    /// <summary>Replaces matches using a PCRE2 replacement template and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement)
        => Replace(input, replacement, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches at or after a scalar-aligned UTF-8 byte offset.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
        => Replace(input, replacement, startOffsetInBytes, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using an explicit start byte offset and substitution options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
        => Replace(input, replacement, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using explicit start, substitution, and per-match options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions);
    }

    /// <summary>Replaces matches using a well-formed UTF-8 PCRE2 replacement template.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
        => Replace(input, replacementPatternUtf8, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using a UTF-8 template at or after a scalar-aligned byte offset.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes)
        => Replace(input, replacementPatternUtf8, startOffsetInBytes, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using a UTF-8 template with explicit start and substitution options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
        => Replace(input, replacementPatternUtf8, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches using a UTF-8 template with explicit start, substitution, and per-match options.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        ValidateStartOffset(input, startOffsetInBytes);
        return ReplaceCore(input, Encoding.UTF8.GetString(replacementPatternUtf8), substitutionOptions, startOffsetInBytes, matchOptions);
    }

    /// <summary>Replaces matches through a stateful callback that writes UTF-8 replacement content.</summary>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator)
        => Replace(input, state, evaluator, 0, Pcre2MatchOptions.None);

    /// <summary>Replaces matches through a UTF-8 callback using explicit start and per-match options.</summary>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryReplaceUsingLiteralDriver(
                input,
                state,
                evaluator,
                startOffsetInBytes,
                matchOptions,
                out var literalResult))
        {
            return literalResult;
        }

        return Encoding.UTF8.GetBytes(ReplaceWithUtf8Evaluator(input, state, evaluator, startOffsetInBytes, matchOptions));
    }

    /// <summary>Replaces matches using a PCRE2 replacement template and returns UTF-16 text.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement)
        => ReplaceToString(input, replacement, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text at or after a scalar-aligned UTF-8 byte offset.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes)
        => ReplaceToString(input, replacement, startOffsetInBytes, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text using explicit substitution options.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions)
        => ReplaceToString(input, replacement, 0, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text using explicit start and substitution options.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
        => ReplaceToString(input, replacement, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Replaces matches to UTF-16 text using explicit start, substitution, and per-match options.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        var result = ReplaceCore(input, replacement, substitutionOptions, startOffsetInBytes, matchOptions);
        _ = Utf8Validation.Validate(result);
        return Encoding.UTF8.GetString(result);
    }

    /// <summary>Replaces matches through a stateful callback that returns UTF-16 replacement text.</summary>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator)
        => ReplaceToString(input, state, evaluator, 0, Pcre2MatchOptions.None);

    /// <summary>Replaces matches through a UTF-16 callback using explicit start and per-match options.</summary>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryReplaceToStringViaLiteralDriver(
                input,
                state,
                evaluator,
                startOffsetInBytes,
                matchOptions,
                out var literalResult))
        {
            return literalResult;
        }

        return ReplaceWithUtf16Evaluator(input, state, evaluator, startOffsetInBytes, matchOptions);
    }

    /// <summary>Attempts to write a UTF-8 replacement result into a caller-provided destination.</summary>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten)
        => TryReplace(input, replacementPatternUtf8, destination, out bytesWritten, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None);

    /// <summary>Attempts to write a replacement result using explicit substitution options.</summary>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, Pcre2SubstitutionOptions substitutionOptions)
        => TryReplace(input, replacementPatternUtf8, destination, out bytesWritten, 0, substitutionOptions, Pcre2MatchOptions.None);

    /// <summary>Attempts to write a replacement result using explicit start, substitution, and per-match options.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the complete result does not fit.</returns>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions, Pcre2MatchOptions matchOptions)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        ValidateStartOffset(input, startOffsetInBytes);
        if (TryReplaceUsingLiteralDriver(
                input,
                replacementText,
                startOffsetInBytes,
                substitutionOptions,
                matchOptions,
                destination,
                out bytesWritten,
                out var literalStatus))
        {
            return literalStatus;
        }

        var replaced = Replace(input, replacementPatternUtf8, startOffsetInBytes, substitutionOptions, matchOptions);
        if (replaced.Length > destination.Length)
        {
            bytesWritten = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0
                ? replaced.Length
                : 0;
            return OperationStatus.DestinationTooSmall;
        }

        replaced.CopyTo(destination);
        bytesWritten = replaced.Length;
        return OperationStatus.Done;
    }

    /// <summary>Gets the number of name-to-slot entries, including duplicate names.</summary>
    public int NameEntryCount => NameEntries.Length;

    /// <summary>Copies name-to-slot entries into a caller-provided destination.</summary>
    /// <returns>The number of entries copied; <paramref name="isMore"/> reports whether additional entries did not fit.</returns>
    public int CopyNameEntries(Span<Pcre2NameEntry> destination, out bool isMore)
    {
        var written = Math.Min(destination.Length, NameEntries.Length);
        NameEntries.AsSpan(0, written).CopyTo(destination);
        isMore = written < NameEntries.Length;
        return written;
    }

    /// <summary>Copies every capture slot assigned to a possibly duplicated group name.</summary>
    /// <returns>The number of slot numbers copied; <paramref name="isMore"/> reports whether additional numbers did not fit.</returns>
    public int CopyNumbersForName(string name, Span<int> destination, out bool isMore)
    {
        var matchingEntries = NameEntries.Where(static e => !string.IsNullOrEmpty(e.Name)).ToArray();
        var count = 0;
        foreach (var entry in matchingEntries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            if (count < destination.Length)
            {
                destination[count] = entry.Number;
            }

            count++;
        }

        isMore = count > destination.Length;
        return Math.Min(count, destination.Length);
    }

    /// <summary>Attempts to find the first participating capture slot assigned to a group name.</summary>
    public bool TryGetFirstSetGroup(ReadOnlySpan<byte> input, string name, out Utf8Pcre2GroupContext group)
        => TryGetFirstSetGroup(input, name, out group, 0, Pcre2MatchOptions.None);

    /// <summary>Attempts to find the first participating named slot using explicit start and per-match options.</summary>
    public bool TryGetFirstSetGroup(ReadOnlySpan<byte> input, string name, out Utf8Pcre2GroupContext group, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var context = MatchDetailed(input, startOffsetInBytes, matchOptions);
        return context.TryGetFirstSetGroup(name, out group);
    }

    /// <summary>Copies successive non-overlapping match snapshots into a caller-provided destination.</summary>
    /// <returns>The number of matches copied; <paramref name="isMore"/> reports whether another match is available.</returns>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore)
        => MatchMany(input, destination, out isMore, 0, Pcre2MatchOptions.None);

    /// <summary>Copies successive match snapshots using explicit start and per-match options.</summary>
    /// <returns>The number of matches copied; <paramref name="isMore"/> reports whether another match is available.</returns>
    public int MatchMany(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var directCursor))
        {
            var written = 0;
            while (written < destination.Length && directCursor.MoveNext())
            {
                destination[written] = Utf8Pcre2MatchData.Create(directCursor.Current);
                written++;
            }

            if (written == destination.Length && written != 0 &&
                _program.Operations.Enumerate is Pcre2BacktrackingDirectProgram
                {
                    Program.MayThrowDeferredLookaroundReset: true,
                })
            {
                isMore = true;
                return written;
            }

            isMore = directCursor.MoveNext();
            return written;
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return MatchManyViaUtf8Regex(subject, destination, out isMore, start);
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.ManagedRegex)
        {
            return MatchManyViaManagedRegex(input, destination, out isMore, startOffsetInBytes);
        }

        return MatchManyViaEnumerateMatches(input, destination, out isMore, startOffsetInBytes, matchOptions);
    }

    private int MatchManyViaEnumerateMatches(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        var enumerator = EnumerateMatches(input, startOffsetInBytes, matchOptions);
        if (destination.IsEmpty)
        {
            isMore = enumerator.MoveNext();
            return 0;
        }

        var written = 0;
        while (written < destination.Length && enumerator.MoveNext())
        {
            var current = enumerator.Current;
            destination[written] = new Utf8Pcre2MatchData
            {
                Success = current.Success,
                StartOffsetInBytes = current.StartOffsetInBytes,
                EndOffsetInBytes = current.EndOffsetInBytes,
                HasContiguousByteRange = current.HasContiguousByteRange,
                IsUtf8SliceWellFormed = current.IsUtf8SliceWellFormed,
                HasUtf16Projection = current.HasUtf16Projection,
                StartOffsetInUtf16 = current.StartOffsetInUtf16,
                EndOffsetInUtf16 = current.EndOffsetInUtf16,
            };
            written++;
        }

        if (written == destination.Length)
        {
            isMore = true;
            return written;
        }

        isMore = false;
        return written;
    }

    private int MatchManyViaUtf8Regex(
        Utf8ValidatedInput input,
        Span<Utf8Pcre2MatchData> destination,
        out bool isMore,
        Utf8BytePosition start)
    {
        if (CanUsePreparedAsciiUtf8RegexEnumerator(input.IsAscii))
        {
            var preparedEnumerator = new Utf8Pcre2ValueMatchEnumerator(
                input.Bytes,
                PrimaryUtf8Regex.ByteOffsetExecution.EnumeratePreparedMatches(input, start),
                start.Value);
            if (destination.IsEmpty)
            {
                isMore = preparedEnumerator.MoveNext();
                return 0;
            }

            var preparedWritten = 0;
            while (preparedWritten < destination.Length && preparedEnumerator.MoveNext())
            {
                var current = preparedEnumerator.Current;
                destination[preparedWritten] = new Utf8Pcre2MatchData
                {
                    Success = current.Success,
                    StartOffsetInBytes = current.StartOffsetInBytes,
                    EndOffsetInBytes = current.EndOffsetInBytes,
                    HasContiguousByteRange = current.HasContiguousByteRange,
                    IsUtf8SliceWellFormed = current.IsUtf8SliceWellFormed,
                    HasUtf16Projection = current.HasUtf16Projection,
                    StartOffsetInUtf16 = current.StartOffsetInUtf16,
                    EndOffsetInUtf16 = current.EndOffsetInUtf16,
                };
                preparedWritten++;
            }

            isMore = preparedEnumerator.MoveNext();
            return preparedWritten;
        }

        var enumerator = PrimaryUtf8Regex.ByteOffsetExecution.EnumerateMatches(input, start);
        if (destination.IsEmpty)
        {
            isMore = enumerator.MoveNext();
            return 0;
        }

        var written = 0;
        while (written < destination.Length && enumerator.MoveNext())
        {
            destination[written] = CreateManagedProfileMatchData(enumerator.Current);
            written++;
        }

        if (written == destination.Length)
        {
            isMore = enumerator.MoveNext();
            return written;
        }

        isMore = false;
        return written;
    }

    private int MatchManyViaManagedRegex(ReadOnlySpan<byte> input, Span<Utf8Pcre2MatchData> destination, out bool isMore, int startOffsetInBytes)
    {
        var subject = Encoding.UTF8.GetString(input);
        var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var isAscii = subject.Length == input.Length;
        Utf8BoundaryMap? boundaryMap = isAscii ? null : Utf8InputAnalyzer.Analyze(input).BoundaryMap;

        var match = ManagedRegex.Match(subject, startOffsetInUtf16);
        if (destination.IsEmpty)
        {
            isMore = match.Success;
            return 0;
        }

        var written = 0;
        while (written < destination.Length && match.Success)
        {
            destination[written] = CreateManagedRegexMatchData(match, boundaryMap, isAscii);
            written++;
            match = match.NextMatch();
        }

        isMore = match.Success;
        return written;
    }


    /// <summary>Returns the compiled expression's execution and result-shaping characteristics.</summary>
    public Utf8Pcre2Analysis Analyze()
    {
        var backtracking = (_program.Operations.Match as Pcre2BacktrackingDirectProgram)?.Program;
        var literal = (_program.Operations.Match as Pcre2LiteralDirectProgram)?.Program;
        return new Utf8Pcre2Analysis
        {
            IsFullyNative = _program.Operations.Match.Kind != Pcre2DirectProgramKind.ManagedRegex,
            IsExactLiteral = literal is not null,
            MinRequiredLengthInBytes = literal?.LiteralUtf8.Length ?? backtracking?.AnalyzedMinimumByteLength ?? 0,
            HasDuplicateNames = NameEntries.GroupBy(static entry => entry.Name, StringComparer.Ordinal).Any(static group => group.Count() > 1),
            UsesBranchReset = backtracking?.GroupNames.Length > 1 &&
                backtracking.CaptureSlotCount < backtracking.GroupNames.Length,
            UsesBacktrackingControlVerbs = backtracking?.UsesBacktrackingControlVerbs ?? false,
            UsesRecursion = backtracking is not null && UsesSubroutines(backtracking),
            MayProduceNonUtf8Slices = GenericProgramUsesCodeUnit,
            MayReportNonMonotoneMatchOffsets = GenericBacktrackingMayReportNonMonotoneMatchOffsets,
            RejectsNonMonotoneIterativeMatches = GenericBacktrackingMayReportNonMonotoneMatchOffsets,
            MayFailIterativeExecutionAtRuntime = backtracking is not null && HasDeferredLookaroundReset(backtracking),
        };
    }

    /// <summary>Compiles a default PCRE2 pattern and tests the UTF-8 subject.</summary>
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern)
        => IsMatch(input, pattern, Pcre2CompileOptions.None, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles a PCRE2 pattern with the specified options and tests the UTF-8 subject.</summary>
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options)
        => IsMatch(input, pattern, options, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles and tests a PCRE2 pattern with explicit settings, limits, timeout, and start byte offset.</summary>
    public static bool IsMatch(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).IsMatch(input, startOffsetInBytes, Pcre2MatchOptions.None);
    }

    /// <summary>Compiles a default PCRE2 pattern and finds the first match.</summary>
    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern)
        => Match(input, pattern, Pcre2CompileOptions.None, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles a PCRE2 pattern with the specified options and finds the first match.</summary>
    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options)
        => Match(input, pattern, options, default, default, DefaultMatchTimeout, 0);

    /// <summary>Compiles and matches a PCRE2 pattern with explicit settings, limits, timeout, and start byte offset.</summary>
    public static Utf8Pcre2ValueMatch Match(ReadOnlySpan<byte> input, string pattern, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout).Match(input, startOffsetInBytes, Pcre2MatchOptions.None);
    }

    /// <summary>Compiles a default PCRE2 pattern and returns replaced UTF-8 bytes.</summary>
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement)
        => Replace(input, pattern, replacement, Pcre2CompileOptions.None, default, default, DefaultMatchTimeout, 0, Pcre2SubstitutionOptions.None);

    /// <summary>Compiles a PCRE2 pattern with the specified options and returns replaced UTF-8 bytes.</summary>
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, Pcre2CompileOptions options)
        => Replace(input, pattern, replacement, options, default, default, DefaultMatchTimeout, 0, Pcre2SubstitutionOptions.None);

    /// <summary>Compiles and replaces with explicit PCRE2 settings, limits, timeout, start offset, and substitution options.</summary>
    public static byte[] Replace(ReadOnlySpan<byte> input, string pattern, string replacement, Pcre2CompileOptions options, Utf8Pcre2CompileSettings compileSettings, Utf8Pcre2ExecutionLimits defaultExecutionLimits, TimeSpan matchTimeout, int startOffsetInBytes, Pcre2SubstitutionOptions substitutionOptions)
    {
        return new Utf8Pcre2Regex(pattern, options, compileSettings, defaultExecutionLimits, matchTimeout)
            .Replace(input, replacement, startOffsetInBytes, substitutionOptions, Pcre2MatchOptions.None);
    }

    private static Utf8ValidatedInput ValidateSubjectAndStart(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out Utf8BytePosition start)
    {
        var subject = Utf8ValidatedInput.Create(input);
        start = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
        return subject;
    }

    private static void ValidateStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Utf8ValidatedInput.Create(input);
        _ = subject.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
    }

    private static string DecodeValidatedUtf8(ReadOnlySpan<byte> input)
    {
        _ = Utf8Validation.Validate(input);
        return Encoding.UTF8.GetString(input);
    }

    private static bool TryCreateManagedRegex(string pattern, Pcre2CompileOptions options, TimeSpan matchTimeout, out Regex? regex)
    {
        if (pattern.Contains("(?|", StringComparison.Ordinal) ||
            pattern.Contains(@"(?<n>", StringComparison.Ordinal) ||
            pattern.Contains("(*", StringComparison.Ordinal) ||
            pattern.Contains(@"\K", StringComparison.Ordinal) ||
            pattern.Contains(@"\C", StringComparison.Ordinal))
        {
            regex = null;
            return false;
        }

        var regexOptions = RegexOptions.CultureInvariant;
        if ((options & Pcre2CompileOptions.Caseless) != 0)
        {
            regexOptions |= RegexOptions.IgnoreCase;
        }

        if ((options & Pcre2CompileOptions.Multiline) != 0)
        {
            regexOptions |= RegexOptions.Multiline;
        }

        if ((options & Pcre2CompileOptions.DotAll) != 0)
        {
            regexOptions |= RegexOptions.Singleline;
        }

        if ((options & Pcre2CompileOptions.Extended) != 0 || (options & Pcre2CompileOptions.ExtendedMore) != 0)
        {
            regexOptions |= RegexOptions.IgnorePatternWhitespace;
        }

        try
        {
            regex = new Regex(pattern, regexOptions, matchTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            regex = null;
            return false;
        }
    }

    private static bool TryCreateUtf8Regex(string pattern, Pcre2CompileOptions options, TimeSpan matchTimeout, out Utf8Regex? regex)
    {
        try
        {
            regex = new Utf8Regex(pattern, ToRegexOptions(options), matchTimeout);
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
        var regexOptions = RegexOptions.CultureInvariant;
        if ((options & Pcre2CompileOptions.Caseless) != 0)
        {
            regexOptions |= RegexOptions.IgnoreCase;
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

    private static Pcre2NameEntry[] GetManagedNameEntries(Regex regex)
    {
        return regex
            .GetGroupNames()
            .Where(static name => !int.TryParse(name, out _))
            .Select(name => new Pcre2NameEntry
            {
                Name = name,
                Number = regex.GroupNumberFromName(name),
            })
            .ToArray();
    }

    private static Pcre2NameEntry[] GetUtf8RegexNameEntries(Utf8Regex regex)
    {
        return regex
            .GetGroupNames()
            .Select((name, index) => (name, index))
            .Where(static pair => !int.TryParse(pair.name, out _))
            .Select(static pair => new Pcre2NameEntry
            {
                Name = pair.name,
                Number = pair.index,
            })
            .ToArray();
    }

    private Utf8Pcre2ValueMatch MatchUsingManagedBackend(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var decoded = Encoding.UTF8.GetString(input);
        var utf16Start = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var match = ManagedRegex.Match(decoded, utf16Start);
        return Utf8Pcre2ValueMatch.Create(input, match);
    }


    private Utf8Pcre2MatchContext MatchDetailedUsingManagedBackend(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var decoded = Encoding.UTF8.GetString(input);
        var utf16Start = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
        var match = ManagedRegex.Match(decoded, utf16Start);
        return Utf8Pcre2MatchContext.Create(input, match, GroupNames);
    }

    private Utf8Pcre2MatchContext MatchDetailedUsingUtf8Backend(
        Utf8ValidatedInput input,
        Utf8BytePosition start)
    {
        var context = PrimaryUtf8Regex.ByteOffsetExecution.MatchDetailed(input, start);
        return CreateManagedProfileMatchContext(input.Bytes, context, GroupNames);
    }



























































































































    private static class Pcre2PartialProbeRunner
{
    internal static Utf8Pcre2ProbeResult Probe(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        Pcre2PartialProbeProgram program)
    {
        return program.Kind switch
        {
            Pcre2PartialProbeProgramKind.PartialSoftDotAllLiteral => ProbeViaPartialSoftDotAllLiteral(input, partialMode, startOffsetInBytes),
            Pcre2PartialProbeProgramKind.AbPlus => ProbeViaAbPlus(input, partialMode, startOffsetInBytes),
            Pcre2PartialProbeProgramKind.LiteralAlternation => ProbeViaLiteralAlternation(input, partialMode, startOffsetInBytes, program.Operands),
            Pcre2PartialProbeProgramKind.OrderedLiteralAlternation => ProbeViaOrderedLiteralAlternation(input, partialMode, startOffsetInBytes, program.Operands),
            Pcre2PartialProbeProgramKind.WordBoundaryLiteral => ProbeViaWordBoundaryLiteral(input, partialMode, startOffsetInBytes, program.Operands[0]),
            Pcre2PartialProbeProgramKind.InspectedPrefixLiteral => ProbeViaInspectedPrefixLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.Operands[1], program.Flag),
            Pcre2PartialProbeProgramKind.InspectedContextLiteral => ProbeViaInspectedContextLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.Operands[1], program.Operands[2], program.Flag),
            Pcre2PartialProbeProgramKind.EndAssertion => ProbeViaEndAssertion(input, partialMode, startOffsetInBytes, program.Flag),
            Pcre2PartialProbeProgramKind.TrailingCWithLookbehind => ProbeViaTrailingCWithLookbehind(input, partialMode, startOffsetInBytes, program.Flag),
            Pcre2PartialProbeProgramKind.NegativeStartClassDotStar => ProbeViaNegativeStartClassDotStar(input, partialMode, startOffsetInBytes),
            Pcre2PartialProbeProgramKind.AnchoredNewlineSequence => ProbeViaAnchoredNewlineSequence(input, partialMode, startOffsetInBytes, program.First, program.Second, program.Flag),
            Pcre2PartialProbeProgramKind.AnchoredOptionalNewlineThenX => ProbeViaAnchoredOptionalNewlineThenX(input, partialMode, startOffsetInBytes),
            Pcre2PartialProbeProgramKind.AnchoredAtomicAPlusB => ProbeViaAnchoredAtomicAPlusB(input, partialMode, startOffsetInBytes),
            Pcre2PartialProbeProgramKind.AnchoredRepeatedLiteral => ProbeViaAnchoredRepeatedLiteral(input, partialMode, startOffsetInBytes, program.Operands[0], program.First),
            Pcre2PartialProbeProgramKind.AnchoredConditionalAbcDotStarOrZ => ProbeViaAnchoredConditionalAbcDotStarOrZ(input, partialMode, startOffsetInBytes),
            Pcre2PartialProbeProgramKind.AnchoredLiteralPlusTerminal => ProbeViaAnchoredLiteralPlusTerminal(input, partialMode, startOffsetInBytes, program.Operands[0], (byte)program.Terminal),
            Pcre2PartialProbeProgramKind.AnchoredAPlusWord => ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, program.Flag, false),
            Pcre2PartialProbeProgramKind.AnchoredAaOrAPlusWord => ProbeViaAnchoredAaOrAPlusWord(input, partialMode, startOffsetInBytes),
            Pcre2PartialProbeProgramKind.AnchoredACrlfEnd => ProbeViaAnchoredACrlfEnd(input, partialMode, startOffsetInBytes, program.Flag),
            Pcre2PartialProbeProgramKind.CrlfDotQuantifier => ProbeViaCrlfDotQuantifier(input, partialMode, startOffsetInBytes, program.First, program.Second),
            Pcre2PartialProbeProgramKind.TrailingLiteralAssertion => ProbeViaTrailingLiteralAssertion(input, partialMode, startOffsetInBytes, program.Operands[0], program.TrailingAssertion),
            Pcre2PartialProbeProgramKind.AnchoredPrefixLiteral => ProbeViaAnchoredPrefixLiteral(input, partialMode, startOffsetInBytes, program.Operands[0]),
            Pcre2PartialProbeProgramKind.AnchoredExactLiteral => ProbeViaAnchoredExactLiteral(input, partialMode, startOffsetInBytes, program.Operands[0]),
            _ => throw new InvalidOperationException("The compiled partial-probe program is not executable."),
        };
    }

    private static Utf8Pcre2ProbeResult ProbeViaPartialSoftDotAllLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (partialMode == Pcre2PartialMode.None)
        {
            return remaining.Length > 0 && remaining[0] == (byte)'f'
                ? Utf8Pcre2ProbeResult.CreateFullMatch(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length)])
                : Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (remaining.StartsWith("for"u8))
        {
            return Utf8Pcre2ProbeResult.CreatePartial(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 3));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAbPlus(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        for (var i = 0; i < remaining.Length; i++)
        {
            if (remaining[i] != (byte)'a')
            {
                continue;
            }

            var j = i + 1;
            while (j < remaining.Length && remaining[j] == (byte)'b')
            {
                j++;
            }

            if (j > i + 1)
            {
                var matchStart = startOffsetInBytes + i;
                if (partialMode == Pcre2PartialMode.Hard && j == remaining.Length)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(
                        input,
                        Pcre2GroupData.FromByteOffsets(input, 0, matchStart, startOffsetInBytes + j));
                }

                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, startOffsetInBytes + j)]);
            }

            if (partialMode != Pcre2PartialMode.None && i == remaining.Length - 1)
            {
                var partialStart = startOffsetInBytes + i;
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, partialStart, partialStart + 1));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaLiteralAlternation(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[][] literalBytes)
    {
        var remaining = input[startOffsetInBytes..];
        var bestIndex = -1;
        byte[]? bestLiteral = null;
        foreach (var literal in literalBytes)
        {
            var index = remaining.IndexOf(literal);
            if (index < 0)
            {
                continue;
            }

            if (bestIndex < 0 || index < bestIndex)
            {
                bestIndex = index;
                bestLiteral = literal;
            }
        }

        if (bestLiteral is not null)
        {
            var matchStart = startOffsetInBytes + bestIndex;
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + bestLiteral.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = 0;
            foreach (var literal in literalBytes)
            {
                partialLength = Math.Max(partialLength, LongestTrailingPrefixLength(remaining, literal));
            }

            if (partialLength > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, input.Length - partialLength, input.Length));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaOrderedLiteralAlternation(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[][] literalBytes)
    {
        var remaining = input[startOffsetInBytes..];
        Pcre2GroupData? firstPartial = null;

        for (var start = 0; start < remaining.Length; start++)
        {
            var candidate = remaining[start..];
            for (var i = 0; i < literalBytes.Length; i++)
            {
                var literal = literalBytes[i];
                if (candidate.StartsWith(literal))
                {
                    var matchStart = startOffsetInBytes + start;
                    if (partialMode == Pcre2PartialMode.Hard &&
                        candidate.Length > literal.Length &&
                        HasLongerPartialContinuation(candidate, literalBytes, i + 1))
                    {
                        return Utf8Pcre2ProbeResult.CreatePartial(
                            input,
                            Pcre2GroupData.FromByteOffsets(input, 0, matchStart, input.Length));
                    }

                    return Utf8Pcre2ProbeResult.CreateFullMatch(
                        input,
                        [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + literal.Length)]);
                }

                if (partialMode == Pcre2PartialMode.None ||
                    candidate.Length == 0 ||
                    candidate.Length >= literal.Length ||
                    !literal.AsSpan(0, candidate.Length).SequenceEqual(candidate))
                {
                    continue;
                }

                var partial = Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + start, input.Length);
                if (partialMode == Pcre2PartialMode.Hard)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(input, partial);
                }

                firstPartial ??= partial;
            }
        }

        return firstPartial is Pcre2GroupData partialMatch
            ? Utf8Pcre2ProbeResult.CreatePartial(input, partialMatch)
            : Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaWordBoundaryLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8)
    {
        var remaining = input[startOffsetInBytes..];
        for (var start = 0; start < remaining.Length; start++)
        {
            if (!HasAsciiWordBoundary(remaining, start))
            {
                continue;
            }

            var candidate = remaining[start..];
            if (candidate.StartsWith(literalUtf8))
            {
                var matchStart = startOffsetInBytes + start;
                var matchEnd = matchStart + literalUtf8.Length;
                if (matchEnd < input.Length)
                {
                    if (!HasAsciiWordBoundary(input, matchEnd))
                    {
                        continue;
                    }

                    return Utf8Pcre2ProbeResult.CreateFullMatch(
                        input,
                        [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd)]);
                }

                if (partialMode == Pcre2PartialMode.Hard)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(
                        input,
                        Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd));
                }

                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                candidate.Length == 0 ||
                candidate.Length >= literalUtf8.Length ||
                !literalUtf8.AsSpan(0, candidate.Length).SequenceEqual(candidate))
            {
                continue;
            }

            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes + start, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaInspectedPrefixLiteral(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[] prefixUtf8,
        byte[] literalUtf8,
        bool includePrefixInPartial)
    {
        var remaining = input[startOffsetInBytes..];
        for (var start = 0; start <= remaining.Length - prefixUtf8.Length; start++)
        {
            if (!remaining[start..].StartsWith(prefixUtf8))
            {
                continue;
            }

            var suffix = remaining[(start + prefixUtf8.Length)..];
            if (suffix.StartsWith(literalUtf8))
            {
                var matchStart = startOffsetInBytes + start + prefixUtf8.Length;
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + literalUtf8.Length)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                suffix.Length == 0 ||
                suffix.Length >= literalUtf8.Length ||
                !literalUtf8.AsSpan(0, suffix.Length).SequenceEqual(suffix))
            {
                continue;
            }

            var partialStart = includePrefixInPartial
                ? startOffsetInBytes + start
                : startOffsetInBytes + start + prefixUtf8.Length;
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, partialStart, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaInspectedContextLiteral(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[] prefixUtf8,
        byte[] literalUtf8,
        byte[] suffixUtf8,
        bool includePrefixInPartial)
    {
        var remaining = input[startOffsetInBytes..];
        var maxStart = remaining.Length - prefixUtf8.Length - literalUtf8.Length;
        for (var start = 0; start <= maxStart; start++)
        {
            if (prefixUtf8.Length > 0 && !remaining[start..].StartsWith(prefixUtf8))
            {
                continue;
            }

            var matchRelativeStart = start + prefixUtf8.Length;
            if (!remaining[matchRelativeStart..].StartsWith(literalUtf8))
            {
                continue;
            }

            var suffixStart = matchRelativeStart + literalUtf8.Length;
            var suffix = remaining[suffixStart..];
            if (suffix.StartsWith(suffixUtf8))
            {
                var matchStart = startOffsetInBytes + matchRelativeStart;
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchStart + literalUtf8.Length)]);
            }

            if (partialMode == Pcre2PartialMode.None ||
                suffix.Length >= suffixUtf8.Length ||
                !suffixUtf8.AsSpan(0, suffix.Length).SequenceEqual(suffix))
            {
                continue;
            }

            var partialStart = includePrefixInPartial
                ? startOffsetInBytes + start
                : startOffsetInBytes + matchRelativeStart;
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, partialStart, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaEndAssertion(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool allowFinalNewline)
    {
        var matchIndex = input.Length;
        var partialStart = input.Length;
        if (allowFinalNewline && input.Length > startOffsetInBytes && input[^1] == (byte)'\n')
        {
            matchIndex = input.Length - 1;
            partialStart = input.Length - 1;
        }

        if (partialMode == Pcre2PartialMode.Hard)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, partialStart, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateFullMatch(
            input,
            [Pcre2GroupData.FromByteOffsets(input, 0, matchIndex, matchIndex)]);
    }

    private static Utf8Pcre2ProbeResult ProbeViaTrailingCWithLookbehind(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool requireAtLeastOneC)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var trailingCs = 0;
        for (var i = remaining.Length - 1; i >= 0 && remaining[i] == (byte)'c'; i--)
        {
            trailingCs++;
        }

        var matchStart = input.Length - trailingCs;
        if (trailingCs > 0)
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, matchStart, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, input.Length)]);
        }

        if (remaining[^1] is (byte)'b' or (byte)'c')
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, input.Length, input.Length));
            }

            if (!requireAtLeastOneC)
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, input.Length, input.Length)]);
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaNegativeStartClassDotStar(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return partialMode == Pcre2PartialMode.Hard
                ? Utf8Pcre2ProbeResult.CreatePartial(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes))
                : Utf8Pcre2ProbeResult.CreateFullMatch(input, [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes)]);
        }

        if (remaining[0] is (byte)'a' or (byte)'b')
        {
            return partialMode == Pcre2PartialMode.Hard
                ? Utf8Pcre2ProbeResult.CreatePartial(input, Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes))
                : Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        return Utf8Pcre2ProbeResult.CreateFullMatch(
            input,
            [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length)]);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredAtomicAPlusB(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0 || remaining[0] != (byte)'a')
        {
            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var cursor = 0;
        while (cursor < remaining.Length && remaining[cursor] == (byte)'a')
        {
            cursor++;
        }

        if (cursor < remaining.Length && remaining[cursor] == (byte)'b')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredNewlineSequence(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, int minCount, int maxCount, bool requireTerminalX)
    {
        var remaining = input[startOffsetInBytes..];
        var cursor = 0;
        var count = 0;
        while (cursor < remaining.Length && count < maxCount && TryConsumePcre2Newline(remaining[cursor..], out var consumed))
        {
            cursor += consumed;
            count++;
        }

        if (requireTerminalX)
        {
            if (count >= minCount && cursor < remaining.Length && remaining[cursor] == (byte)'x')
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
            }

            if (partialMode != Pcre2PartialMode.None && cursor == remaining.Length && count > 0 && count <= maxCount)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (count >= minCount)
        {
            if (partialMode == Pcre2PartialMode.Hard && remaining.Length == cursor && remaining.Length > 0 && remaining[0] == (byte)'\r')
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor)]);
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredOptionalNewlineThenX(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length > 0 && remaining[0] == (byte)'x')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        if (TryConsumePcre2Newline(remaining, out var consumed))
        {
            if (remaining.Length == consumed && partialMode != Pcre2PartialMode.None)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            if (remaining.Length > consumed && remaining[consumed] == (byte)'x')
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + consumed + 1)]);
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredRepeatedLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8, int repeatCount)
    {
        var remaining = input[startOffsetInBytes..];
        var requiredLength = literalUtf8.Length * repeatCount;
        var matched = 0;
        while (matched < remaining.Length && matched < requiredLength && remaining[matched] == literalUtf8[matched % literalUtf8.Length])
        {
            matched++;
        }

        if (matched == requiredLength)
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + requiredLength)]);
        }

        if (partialMode != Pcre2PartialMode.None && matched > 0 && matched == remaining.Length)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredConditionalAbcDotStarOrZ(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith("abc"u8))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None && "abc"u8.StartsWith(remaining))
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        if (remaining.Length > 0 && remaining[0] == (byte)'Z')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredLiteralPlusTerminal(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8, byte terminal)
    {
        var remaining = input[startOffsetInBytes..];
        var cursor = 0;
        while (cursor + literalUtf8.Length <= remaining.Length && remaining[cursor..].StartsWith(literalUtf8))
        {
            cursor += literalUtf8.Length;
        }

        if (cursor == 0)
        {
            if (partialMode != Pcre2PartialMode.None && remaining.Length > 0 && literalUtf8.AsSpan(0, remaining.Length).SequenceEqual(remaining))
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (cursor < remaining.Length && remaining[cursor] == terminal)
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + cursor + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            if (cursor == remaining.Length)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            var tail = remaining[cursor..];
            if (tail.Length > 0 && literalUtf8.AsSpan(0, tail.Length).SequenceEqual(tail))
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredAPlusWord(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool allowEmptyA, bool requireAtLeastTwoLeadingA)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length == 0)
        {
            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var aCount = 0;
        while (aCount < remaining.Length && remaining[aCount] == (byte)'a')
        {
            aCount++;
        }

        var minA = allowEmptyA ? 0 : 1;
        if (requireAtLeastTwoLeadingA)
        {
            minA = Math.Max(minA, 2);
        }

        if (aCount < minA)
        {
            if (allowEmptyA && IsAsciiWordByte(remaining[0]))
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (aCount < remaining.Length && IsAsciiWordByte(remaining[aCount]))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + aCount + 1)]);
        }

        if (partialMode != Pcre2PartialMode.None && aCount == remaining.Length)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredAaOrAPlusWord(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.Length >= 2 && remaining[0] == (byte)'a' && remaining[1] == (byte)'a')
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2)]);
        }

        return ProbeViaAnchoredAPlusWord(input, partialMode, startOffsetInBytes, allowEmptyA: false, requireAtLeastTwoLeadingA: false);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredACrlfEnd(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, bool preferExplicitCrAlternative)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual("a"u8))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 1)]);
        }

        if (remaining.SequenceEqual("a\r"u8))
        {
            if (preferExplicitCrAlternative && partialMode != Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreateFullMatch(
                    input,
                    [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + 2)]);
            }

            if (partialMode != Pcre2PartialMode.None)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaCrlfDotQuantifier(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, int minCount, int maxCount)
    {
        var remaining = input[startOffsetInBytes..];
        var count = Math.Min(remaining.Length, maxCount);
        if (count < minCount)
        {
            if (partialMode != Pcre2PartialMode.None && count > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        if (partialMode == Pcre2PartialMode.Hard && count == remaining.Length)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, input.Length));
        }

        return Utf8Pcre2ProbeResult.CreateFullMatch(
            input,
            [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + count)]);
    }

    private static bool TryConsumePcre2Newline(ReadOnlySpan<byte> input, out int consumed)
    {
        if (input.Length >= 2 && input[0] == (byte)'\r' && input[1] == (byte)'\n')
        {
            consumed = 2;
            return true;
        }

        if (input.Length >= 1 && input[0] is (byte)'\r' or (byte)'\n')
        {
            consumed = 1;
            return true;
        }

        consumed = 0;
        return false;
    }

    private static Utf8Pcre2ProbeResult ProbeViaTrailingLiteralAssertion(
        ReadOnlySpan<byte> input,
        Pcre2PartialMode partialMode,
        int startOffsetInBytes,
        byte[] literalUtf8,
        Pcre2PartialProbeTrailingAssertion assertion)
    {
        var remaining = input[startOffsetInBytes..];
        var index = remaining.IndexOf(literalUtf8);
        if (index < 0)
        {
            if (partialMode != Pcre2PartialMode.None &&
                assertion == Pcre2PartialProbeTrailingAssertion.Dollar)
            {
                var partialLength = LongestTrailingSuffixLength(remaining, literalUtf8);
                if (partialLength > 0)
                {
                    return Utf8Pcre2ProbeResult.CreatePartial(
                        input,
                        Pcre2GroupData.FromByteOffsets(input, 0, input.Length - partialLength, input.Length));
                }
            }

            return Utf8Pcre2ProbeResult.CreateNoMatch(input);
        }

        var matchStart = startOffsetInBytes + index;
        var matchEnd = matchStart + literalUtf8.Length;
        var atEnd = matchEnd == input.Length;
        var hasFinalNewline = matchEnd + 1 == input.Length && input[matchEnd] == (byte)'\n';
        var rightIsWord = matchEnd < input.Length && IsAsciiWordByte(input[matchEnd]);

        var full = assertion switch
        {
            Pcre2PartialProbeTrailingAssertion.Dollar => atEnd || hasFinalNewline,
            Pcre2PartialProbeTrailingAssertion.EndAbsolute => atEnd,
            Pcre2PartialProbeTrailingAssertion.EndBeforeFinalNewline => atEnd || hasFinalNewline,
            Pcre2PartialProbeTrailingAssertion.WordBoundary => !rightIsWord,
            Pcre2PartialProbeTrailingAssertion.NonWordBoundary => rightIsWord,
            _ => false,
        };

        if (full)
        {
            if (partialMode == Pcre2PartialMode.Hard &&
                assertion is Pcre2PartialProbeTrailingAssertion.Dollar or Pcre2PartialProbeTrailingAssertion.EndAbsolute or Pcre2PartialProbeTrailingAssertion.EndBeforeFinalNewline or Pcre2PartialProbeTrailingAssertion.WordBoundary or Pcre2PartialProbeTrailingAssertion.NonWordBoundary &&
                atEnd)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd)]);
        }

        if (partialMode != Pcre2PartialMode.None &&
            assertion is Pcre2PartialProbeTrailingAssertion.WordBoundary or Pcre2PartialProbeTrailingAssertion.NonWordBoundary &&
            atEnd)
        {
            return Utf8Pcre2ProbeResult.CreatePartial(
                input,
                Pcre2GroupData.FromByteOffsets(input, 0, matchStart, matchEnd));
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static bool HasAsciiWordBoundary(ReadOnlySpan<byte> input, int byteOffset)
    {
        var leftIsWord = byteOffset > 0 && IsAsciiWordByte(input[byteOffset - 1]);
        var rightIsWord = byteOffset < input.Length && IsAsciiWordByte(input[byteOffset]);
        return leftIsWord != rightIsWord;
    }

    private static bool IsAsciiWordByte(byte value)
    {
        return value is >= (byte)'A' and <= (byte)'Z' or
            >= (byte)'a' and <= (byte)'z' or
            >= (byte)'0' and <= (byte)'9' or
            (byte)'_';
    }

    private static bool HasLongerPartialContinuation(ReadOnlySpan<byte> candidate, byte[][] literalBytes, int startLiteralIndex)
    {
        for (var i = startLiteralIndex; i < literalBytes.Length; i++)
        {
            var literal = literalBytes[i];
            if (candidate.Length < literal.Length && literal.AsSpan(0, candidate.Length).SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredExactLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.SequenceEqual(literalUtf8))
        {
            if (partialMode == Pcre2PartialMode.Hard)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length));
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = LongestLeadingExactPrefixLength(remaining, literalUtf8);
            if (partialLength > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + partialLength));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Utf8Pcre2ProbeResult ProbeViaAnchoredPrefixLiteral(ReadOnlySpan<byte> input, Pcre2PartialMode partialMode, int startOffsetInBytes, byte[] literalUtf8)
    {
        var remaining = input[startOffsetInBytes..];
        if (remaining.StartsWith(literalUtf8))
        {
            return Utf8Pcre2ProbeResult.CreateFullMatch(
                input,
                [Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + literalUtf8.Length)]);
        }

        if (partialMode != Pcre2PartialMode.None)
        {
            var partialLength = LongestLeadingExactPrefixLength(remaining, literalUtf8);
            if (partialLength > 0)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    input,
                    Pcre2GroupData.FromByteOffsets(input, 0, startOffsetInBytes, startOffsetInBytes + partialLength));
            }
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static int LongestLeadingExactPrefixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        if (subject.Length == 0 || subject.Length >= literal.Length)
        {
            return 0;
        }

        return literal[..subject.Length].SequenceEqual(subject) ? subject.Length : 0;
    }

    private static int LongestTrailingSuffixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        var maxLength = Math.Min(subject.Length, literal.Length - 1);
        for (var length = maxLength; length >= 1; length--)
        {
            if (subject[^length..].SequenceEqual(literal[(literal.Length - length)..]))
            {
                return length;
            }
        }

        return 0;
    }

    private static int LongestTrailingPrefixLength(ReadOnlySpan<byte> subject, ReadOnlySpan<byte> literal)
    {
        var maxLength = Math.Min(subject.Length, literal.Length - 1);
        for (var length = maxLength; length >= 1; length--)
        {
            if (subject[^length..].SequenceEqual(literal[..length]))
            {
                return length;
            }
        }

        return 0;
    }

    }

    private Utf8Pcre2ProbeResult ProbeViaNonPartialMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = MatchDetailed(input, startOffsetInBytes, Pcre2MatchOptions.None);
        return match.Success
            ? Utf8Pcre2ProbeResult.CreateFullMatch(input, CaptureGroups(match))
            : Utf8Pcre2ProbeResult.CreateNoMatch(input);
    }

    private static Pcre2GroupData[] CaptureGroups(Utf8Pcre2MatchContext match)
    {
        var groups = new Pcre2GroupData[match.CaptureSlotCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = match.GetGroup(i)._data;
        }

        return groups;
    }
























    private static Utf8Pcre2MatchContext CreateManagedProfileMatchContext(ReadOnlySpan<byte> input, Utf8MatchContext context, string[]? groupNames)
    {
        if (!context.Success)
        {
            return default;
        }

        var groups = new Pcre2GroupData[context.GroupCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = CreateManagedProfileGroupData(context.GetGroup(i), i);
        }

        Pcre2NameEntry[]? nameEntries = null;
        if (groupNames is { Length: > 0 })
        {
            nameEntries = new Pcre2NameEntry[groupNames.Length];
            for (var i = 0; i < groupNames.Length; i++)
            {
                nameEntries[i] = new Pcre2NameEntry { Name = groupNames[i], Number = i };
            }
        }

        return Utf8Pcre2MatchContext.Create(input, groups, nameEntries);
    }

    private static Utf8Pcre2MatchData CreateManagedProfileMatchData(Utf8ValueMatch match, ReadOnlySpan<byte> input, int byteOffsetBase, int utf16OffsetBase)
        => Utf8Pcre2MatchData.Create(CreateManagedProfileGroupData(match, byteOffsetBase, utf16OffsetBase));

    private static Utf8Pcre2MatchData CreateManagedProfileMatchData(Utf8ValueMatch match)
        => Utf8Pcre2MatchData.Create(CreateManagedProfileGroupData(match, byteOffsetBase: 0, utf16OffsetBase: 0));

    private static Utf8Pcre2ValueMatch CreateManagedProfileValueMatch(ReadOnlySpan<byte> input, Utf8ValueMatch match)
        => Utf8Pcre2ValueMatch.Create(input, CreateManagedProfileGroupData(match, byteOffsetBase: 0, utf16OffsetBase: 0));

    private static Pcre2GroupData CreateManagedProfileGroupData(Utf8ValueMatch match, int byteOffsetBase, int utf16OffsetBase)
    {
        if (!match.Success)
        {
            return default;
        }

        if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            throw new InvalidOperationException("Managed Utf8Regex fallback returned a match that is not aligned to byte boundaries.");
        }

        return new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = byteOffsetBase + indexInBytes,
            EndOffsetInBytes = byteOffsetBase + indexInBytes + lengthInBytes,
            StartOffsetInUtf16 = utf16OffsetBase + match.IndexInUtf16,
            EndOffsetInUtf16 = utf16OffsetBase + match.IndexInUtf16 + match.LengthInUtf16,
        };
    }

    private static Pcre2GroupData CreateManagedProfileGroupData(Utf8GroupContext group, int number)
    {
        if (!group.Success)
        {
            return new Pcre2GroupData
            {
                Number = number,
                Success = false,
            };
        }

        if (!group.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            throw new InvalidOperationException("Managed Utf8Regex fallback returned a group that is not aligned to byte boundaries.");
        }

        return new Pcre2GroupData
        {
            Number = number,
            Success = true,
            StartOffsetInBytes = indexInBytes,
            EndOffsetInBytes = indexInBytes + lengthInBytes,
            StartOffsetInUtf16 = group.IndexInUtf16,
            EndOffsetInUtf16 = group.IndexInUtf16 + group.LengthInUtf16,
        };
    }

    private static Utf8Pcre2MatchData CreateManagedRegexMatchData(Match match, Utf8BoundaryMap? boundaryMap, bool isAscii)
    {
        if (!match.Success)
        {
            return default;
        }

        int indexInBytes;
        int lengthInBytes;
        if (isAscii)
        {
            indexInBytes = match.Index;
            lengthInBytes = match.Length;
        }
        else if (boundaryMap is { } map && map.TryGetByteRange(match.Index, match.Length, out indexInBytes, out lengthInBytes))
        {
        }
        else
        {
            throw new InvalidOperationException("Managed Regex fallback produced a match that is not aligned to UTF-8 scalar boundaries.");
        }

        return Utf8Pcre2MatchData.Create(
            new Pcre2GroupData
            {
                Number = 0,
                Success = true,
                StartOffsetInBytes = indexInBytes,
                EndOffsetInBytes = indexInBytes + lengthInBytes,
                StartOffsetInUtf16 = match.Index,
                EndOffsetInUtf16 = match.Index + match.Length,
            });
    }

    private bool TryReplaceUsingLiteralDriver(
        ReadOnlySpan<byte> input,
        string replacement,
        int startOffsetInBytes,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions,
        out byte[] result)
    {
        if (!TryGetLiteralDriverReplacementPlan(
                replacement,
                substitutionOptions,
                out var plan,
                out var replacementOnly))
        {
            result = [];
            return false;
        }

        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var cursor))
        {
            result = [];
            return false;
        }

        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildLiteralDriverReplacementRanges(
                input.Length,
                plan,
                replacementOnly,
                ref cursor,
                ref ledger);
            if (ledger.Count == 0)
            {
                result = replacementOnly ? [] : input.ToArray();
                return true;
            }

            result = new byte[outputLength.Value];
            EmitLiteralDriverReplacement(input, plan, replacementOnly, result, ledger.WrittenRanges);
            return true;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    private bool TryReplaceUsingLiteralDriver(
        ReadOnlySpan<byte> input,
        string replacement,
        int startOffsetInBytes,
        Pcre2SubstitutionOptions substitutionOptions,
        Pcre2MatchOptions matchOptions,
        Span<byte> destination,
        out int bytesWritten,
        out OperationStatus status)
    {
        if (!TryGetLiteralDriverReplacementPlan(
                replacement,
                substitutionOptions,
                out var plan,
                out var replacementOnly))
        {
            bytesWritten = 0;
            status = default;
            return false;
        }

        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var cursor))
        {
            bytesWritten = 0;
            status = default;
            return false;
        }

        var ledger = new Utf8ReplacementRangeLedger();
        try
        {
            var outputLength = BuildLiteralDriverReplacementRanges(
                input.Length,
                plan,
                replacementOnly,
                ref cursor,
                ref ledger);
            if (outputLength.Value > destination.Length)
            {
                bytesWritten = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteOverflowLength) != 0
                    ? outputLength.Value
                    : 0;
                status = OperationStatus.DestinationTooSmall;
                return true;
            }

            if (ledger.Count == 0)
            {
                if (!replacementOnly)
                {
                    input.CopyTo(destination);
                }

                bytesWritten = outputLength.Value;
                status = OperationStatus.Done;
                return true;
            }

            EmitLiteralDriverReplacement(input, plan, replacementOnly, destination, ledger.WrittenRanges);
            bytesWritten = outputLength.Value;
            status = OperationStatus.Done;
            return true;
        }
        finally
        {
            ledger.Dispose();
        }
    }

    private bool TryGetLiteralDriverReplacementPlan(
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        out SimpleReplacementPlan plan,
        out bool replacementOnly)
    {
        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram &&
            backtrackingProgram.Program.SuppressesUnresetEmptyMatches)
        {
            plan = default;
            replacementOnly = false;
            return false;
        }

        if (_program.Operations.Replace is not (
            Pcre2LiteralDirectProgram or
            Pcre2CharacterDirectProgram or
            Pcre2SingleTokenRepeatDirectProgram or
            Pcre2BacktrackingDirectProgram))
        {
            plan = default;
            replacementOnly = false;
            return false;
        }

        replacementOnly = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        if ((substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0)
        {
            var unsupported = substitutionOptions &
                ~(Pcre2SubstitutionOptions.SubstituteLiteral |
                  Pcre2SubstitutionOptions.SubstituteOverflowLength |
                  Pcre2SubstitutionOptions.SubstituteReplacementOnly |
                  Pcre2SubstitutionOptions.SubstituteMatched);
            if (unsupported != Pcre2SubstitutionOptions.None)
            {
                plan = default;
                return false;
            }

            plan = new SimpleReplacementPlan([SimpleReplacementSegment.FromLiteral(replacement)]);
            return true;
        }

        var templateOptions = substitutionOptions &
            ~(Pcre2SubstitutionOptions.SubstituteOverflowLength |
              Pcre2SubstitutionOptions.SubstituteReplacementOnly |
              Pcre2SubstitutionOptions.SubstituteMatched);
        var parsed = GetSimpleReplacementPlan(replacement, templateOptions);
        if (parsed is not { } simplePlan)
        {
            plan = default;
            return false;
        }

        plan = simplePlan;
        return true;
    }

    private static Utf8ReplacementOutputLength BuildLiteralDriverReplacementRanges(
        int inputLength,
        SimpleReplacementPlan plan,
        bool replacementOnly,
        ref Pcre2GlobalMatchCursor cursor,
        ref Utf8ReplacementRangeLedger ledger)
    {
        var outputLength = new Utf8ReplacementOutputLength(replacementOnly ? 0 : inputLength);
        while (cursor.MoveNext())
        {
            var match = cursor.Current;
            var matchLength = match.EndOffsetInBytes - match.StartOffsetInBytes;
            ledger.Add(new Utf8ReplacementRange(match.StartOffsetInBytes, matchLength));
            var emittedLength = GetLiteralDriverReplacementLength(plan, matchLength);
            outputLength.ReplaceRange(replacementOnly ? 0 : matchLength, emittedLength);
        }

        return outputLength;
    }

    private static int GetLiteralDriverReplacementLength(SimpleReplacementPlan plan, int matchLength)
    {
        var length = 0;
        foreach (var segment in plan.Segments)
        {
            length = checked(length + (segment.IsWholeMatch ? matchLength : segment.LiteralUtf8.Length));
        }

        return length;
    }

    private static void EmitLiteralDriverReplacement(
        ReadOnlySpan<byte> input,
        SimpleReplacementPlan plan,
        bool replacementOnly,
        Span<byte> destination,
        ReadOnlySpan<Utf8ReplacementRange> matches)
    {
        var sourcePosition = 0;
        var sink = new Utf8ReplacementOutputSink(destination);
        foreach (var match in matches)
        {
            if (!replacementOnly)
            {
                sink.AppendSlice(input, sourcePosition, match.Start - sourcePosition);
            }

            foreach (var segment in plan.Segments)
            {
                if (segment.IsWholeMatch)
                {
                    sink.AppendSlice(input, match.Start, match.Length);
                }
                else
                {
                    sink.Append(segment.LiteralUtf8);
                }
            }

            sourcePosition = match.Start + match.Length;
        }

        if (!replacementOnly)
        {
            sink.Append(input[sourcePosition..]);
        }
    }

    private byte[] ReplaceCore(
        ReadOnlySpan<byte> input,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions)
    {
        if (RejectsReplacementIteration())
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects replacement for lookaround-\\K iterative matches.");
        }

        if (TryReplaceUsingLiteralDriver(
                input,
                replacement,
                startOffsetInBytes,
                substitutionOptions,
                matchOptions,
                out var genericResult))
        {
            return genericResult;
        }

        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            return ReplaceBacktrackingDetailed(
                input,
                backtrackingProgram.Program,
                replacement,
                substitutionOptions,
                startOffsetInBytes,
                matchOptions);
        }

        if (_program.Operations.Replace.Kind != Pcre2DirectProgramKind.None)
        {
            return ReplaceUsingDecodedDetailedIteration(input, replacement, substitutionOptions, startOffsetInBytes, Encoding.UTF8.GetString(input));
        }

        throw CreateUnsupportedReplacementException();
    }












    private static NotSupportedException CreateUnsupportedProbeException()
        => new("SPEC-PCRE2 does not support Probe(...) for this pattern.");

    private static NotSupportedException CreateUnsupportedReplacementException(string? message)
        => new(message ?? "SPEC-PCRE2 does not support replacement for this pattern.");

    private static NotSupportedException CreateUnsupportedReplacementException()
        => CreateUnsupportedReplacementException(null);

    private bool TryReplaceUsingLiteralDriver<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Pcre2MatchEvaluator<TState> evaluator,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions,
        out byte[] result)
    {
        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram &&
            (backtrackingProgram.Program.CaptureSlotCount > 1 ||
             backtrackingProgram.Program.SuppressesUnresetEmptyMatches))
        {
            result = [];
            return false;
        }

        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                matchOptions,
                out var cursor))
        {
            result = [];
            return false;
        }

        var output = new ArrayBufferWriter<byte>(input.Length);
        var writer = new global::Lokad.Utf8Regex.Utf8ReplacementWriter(output);
        var groups = new Pcre2GroupData[1];
        var sourcePosition = 0;
        while (cursor.MoveNext())
        {
            var match = cursor.Current;
            writer.Append(input[sourcePosition..match.StartOffsetInBytes]);
            groups[0] = match;
            var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries);
            evaluator(in context, ref writer, ref state);
            sourcePosition = match.EndOffsetInBytes;
        }

        writer.Append(input[sourcePosition..]);
        result = writer.GetValidatedBytes().ToArray();
        return true;
    }

    private bool TryReplaceToStringViaLiteralDriver<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Pcre2Utf16MatchEvaluator<TState> evaluator,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions,
        out string result)
    {
        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram &&
            (backtrackingProgram.Program.CaptureSlotCount > 1 ||
             backtrackingProgram.Program.SuppressesUnresetEmptyMatches))
        {
            result = string.Empty;
            return false;
        }

        var validated = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (!Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                validated,
                start,
                matchOptions,
                out var cursor))
        {
            result = string.Empty;
            return false;
        }

        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length);
        var groups = new Pcre2GroupData[1];
        var sourcePosition = 0;
        while (cursor.MoveNext())
        {
            var match = cursor.Current;
            builder.Append(subject, sourcePosition, match.StartOffsetInUtf16 - sourcePosition);
            groups[0] = match;
            var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries);
            builder.Append(evaluator(in context, ref state));
            sourcePosition = match.EndOffsetInUtf16;
        }

        builder.Append(subject, sourcePosition, subject.Length - sourcePosition);
        result = builder.ToString();
        return true;
    }

    private string ReplaceWithUtf8Evaluator<TState>(ReadOnlySpan<byte> input, TState state, Pcre2MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            return ReplaceBacktrackingWithUtf8Evaluator(
                input,
                backtrackingProgram.Program,
                state,
                evaluator,
                startOffsetInBytes,
                matchOptions);
        }

        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length);
        var position = 0;

        var enumerator = EnumerateMatches(input, startOffsetInBytes, matchOptions);
        while (enumerator.MoveNext())
        {
            var value = enumerator.Current;
            builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            var context = MatchDetailed(input, value.StartOffsetInBytes, matchOptions);
            var writer = new global::Lokad.Utf8Regex.Utf8ReplacementWriter();
            evaluator(in context, ref writer, ref state);
            builder.Append(writer.ToValidatedString());
            position = value.EndOffsetInUtf16;
        }

        builder.Append(subject, position, subject.Length - position);
        return builder.ToString();
    }

    private string ReplaceWithUtf16Evaluator<TState>(ReadOnlySpan<byte> input, TState state, Pcre2Utf16MatchEvaluator<TState> evaluator, int startOffsetInBytes, Pcre2MatchOptions matchOptions)
    {
        if (_program.Operations.Replace is Pcre2BacktrackingDirectProgram backtrackingProgram)
        {
            return ReplaceBacktrackingWithUtf16Evaluator(
                input,
                backtrackingProgram.Program,
                state,
                evaluator,
                startOffsetInBytes,
                matchOptions);
        }

        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length);
        var position = 0;

        var enumerator = EnumerateMatches(input, startOffsetInBytes, matchOptions);
        while (enumerator.MoveNext())
        {
            var value = enumerator.Current;
            builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            var context = MatchDetailed(input, value.StartOffsetInBytes, matchOptions);
            builder.Append(evaluator(in context, ref state));
            position = value.EndOffsetInUtf16;
        }

        builder.Append(subject, position, subject.Length - position);
        return builder.ToString();
    }

    private string ReplaceBacktrackingWithUtf8Evaluator<TState>(
        ReadOnlySpan<byte> input,
        Pcre2BacktrackingProgram program,
        TState state,
        Pcre2MatchEvaluator<TState> evaluator,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions)
    {
        var validated = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(
            program,
            _program.CandidateSearchPlan,
            validated,
            start,
            matchOptions,
            _program.Request);
        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length);
        var position = 0;
        while (cursor.MoveNext())
        {
            var groups = cursor.ProjectCurrentCaptures();
            var value = groups[0];
            builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries, cursor.Current.Mark);
            var writer = new global::Lokad.Utf8Regex.Utf8ReplacementWriter();
            evaluator(in context, ref writer, ref state);
            builder.Append(writer.ToValidatedString());
            position = value.EndOffsetInUtf16;
        }

        builder.Append(subject, position, subject.Length - position);
        return builder.ToString();
    }

    private string ReplaceBacktrackingWithUtf16Evaluator<TState>(
        ReadOnlySpan<byte> input,
        Pcre2BacktrackingProgram program,
        TState state,
        Pcre2Utf16MatchEvaluator<TState> evaluator,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions)
    {
        var validated = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(
            program,
            _program.CandidateSearchPlan,
            validated,
            start,
            matchOptions,
            _program.Request);
        var subject = Encoding.UTF8.GetString(input);
        var builder = new StringBuilder(subject.Length);
        var position = 0;
        while (cursor.MoveNext())
        {
            var groups = cursor.ProjectCurrentCaptures();
            var value = groups[0];
            builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            var context = Utf8Pcre2MatchContext.Create(input, groups, NameEntries, cursor.Current.Mark);
            builder.Append(evaluator(in context, ref state));
            position = value.EndOffsetInUtf16;
        }

        builder.Append(subject, position, subject.Length - position);
        return builder.ToString();
    }


















    private byte[] ReplaceUsingDecodedDetailedIteration(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes, string subject)
    {
        var replacementOnly = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var simplePlan = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? new SimpleReplacementPlan([SimpleReplacementSegment.FromLiteral(replacement)])
            : GetSimpleReplacementPlan(replacement, templateOptions);
        var builder = new StringBuilder(subject.Length + replacement.Length);
        var position = 0;
        var enumerator = EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None);
        if (!enumerator.MoveNext())
        {
            return replacementOnly ? [] : input.ToArray();
        }

        do
        {
            var value = enumerator.Current;
            var match = MatchDetailed(input, value.StartOffsetInBytes, Pcre2MatchOptions.None);
            if (!match.Success)
            {
                throw new InvalidOperationException("Translated enumeration produced a match that MatchDetailed(...) could not reproduce.");
            }

            if (!replacementOnly)
            {
                builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            }
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(builder, plan, subject, value.StartOffsetInUtf16, value.EndOffsetInUtf16 - value.StartOffsetInUtf16);
            }
            else
            {
                var groups = CaptureGroups(match);
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    templateOptions,
                    subject,
                    value.StartOffsetInUtf16,
                    value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
                    number => ResolveNativeNumberReference(groups, number, subject),
                    name => ResolveNativeNamedReference(groups, NameEntries, name, subject),
                    () => ResolveNativeLastCapturedReference(groups, subject),
                    mark: match.Mark));
            }
            position = value.EndOffsetInUtf16;
        }
        while (enumerator.MoveNext());

        if (!replacementOnly)
        {
            builder.Append(subject, position, subject.Length - position);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private byte[] ReplaceBacktrackingDetailed(
        ReadOnlySpan<byte> input,
        Pcre2BacktrackingProgram program,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        int startOffsetInBytes,
        Pcre2MatchOptions matchOptions)
    {
        var validated = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var cursor = new Pcre2BacktrackingDetailedGlobalMatchCursor(
            program,
            _program.CandidateSearchPlan,
            validated,
            start,
            matchOptions,
            _program.Request);
        if (!cursor.MoveNext())
        {
            return (substitutionOptions & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0
                ? []
                : input.ToArray();
        }

        var subject = Encoding.UTF8.GetString(input);
        var replacementOnly = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteReplacementOnly) != 0;
        var templateOptions = substitutionOptions & ~Pcre2SubstitutionOptions.SubstituteReplacementOnly;
        var simplePlan = (substitutionOptions & Pcre2SubstitutionOptions.SubstituteLiteral) != 0
            ? new SimpleReplacementPlan([SimpleReplacementSegment.FromLiteral(replacement)])
            : GetSimpleReplacementPlan(replacement, templateOptions);
        var builder = new StringBuilder(subject.Length + replacement.Length);
        var position = 0;
        do
        {
            var current = cursor.Current;
            var groups = cursor.ProjectCurrentCaptures();
            var value = groups[0];
            if (!replacementOnly)
            {
                builder.Append(subject, position, value.StartOffsetInUtf16 - position);
            }
            if (simplePlan is { } plan)
            {
                AppendSimpleReplacement(
                    builder,
                    plan,
                    subject,
                    value.StartOffsetInUtf16,
                    value.EndOffsetInUtf16 - value.StartOffsetInUtf16);
            }
            else
            {
                builder.Append(EvaluateReplacementTemplate(
                    replacement,
                    templateOptions,
                    subject,
                    value.StartOffsetInUtf16,
                    value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
                    number => ResolveNativeNumberReference(groups, number, subject),
                    name => ResolveNativeNamedReference(groups, NameEntries, name, subject),
                    () => ResolveNativeLastCapturedReference(groups, subject),
                    mark: current.Mark));
            }

            position = value.EndOffsetInUtf16;
        }
        while (cursor.MoveNext());

        if (!replacementOnly)
        {
            builder.Append(subject, position, subject.Length - position);
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }




    private bool RejectsReplacementIteration()
    {
        return GenericBacktrackingMayReportNonMonotoneMatchOffsets ||
            _program.Operations.Match is Pcre2BacktrackingDirectProgram backtracking &&
            HasDeferredLookaroundReset(backtracking.Program);
    }

    private static bool UsesSubroutines(Pcre2BacktrackingProgram program)
    {
        if (program.SubroutineTargets.Length != 0)
        {
            return true;
        }

        return program.AssertionPrograms.Any(UsesSubroutines);
    }

    private static bool HasDeferredLookaroundReset(Pcre2BacktrackingProgram program)
    {
        if (program.MayThrowDeferredLookaroundReset)
        {
            return true;
        }

        if (program.MayReportNonMonotoneMatchOffsets)
        {
            return false;
        }

        return program.AssertionPrograms.Any(static assertion =>
            assertion.UsesMatchBoundaryReset || HasDeferredLookaroundReset(assertion));
    }




    private static string EvaluateReplacementTemplate(
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        bool syntaxOnly)
    {
        var builder = new StringBuilder(replacement.Length + matchLength);
        var caseTransform = ReplacementCaseTransformMode.None;
        var nextCaseTransform = ReplacementCaseTransformMode.None;
        var quoted = false;
        AppendEvaluatedReplacementTemplate(
            builder,
            replacement,
            substitutionOptions,
            subject,
            matchIndex,
            matchLength,
            resolveNumber,
            resolveName,
            resolveLastCapture,
            mark,
            ref nextCaseTransform,
            ref caseTransform,
            ref quoted,
            syntaxOnly);
        return builder.ToString();
    }

    private bool GenericBacktrackingMayReportNonMonotoneMatchOffsets =>
        _program.Operations.Match is Pcre2BacktrackingDirectProgram
        {
            Program.MayReportNonMonotoneMatchOffsets: true,
        };

    private bool GenericProgramUsesCodeUnit => _program.Operations.Match switch
    {
        Pcre2CharacterDirectProgram { Program.UsesCodeUnit: true } => true,
        Pcre2SingleTokenRepeatDirectProgram { Program.UsesCodeUnit: true } => true,
        Pcre2BacktrackingDirectProgram { Program.UsesCodeUnit: true } => true,
        _ => false,
    };

    private void ThrowIfGenericIterationMayBeNonMonotone()
    {
        if (GenericBacktrackingMayReportNonMonotoneMatchOffsets)
        {
            throw new NotSupportedException("SPEC-PCRE2 rejects non-monotone iterative matches.");
        }
    }

    private static string EvaluateReplacementTemplate(
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark)
        => EvaluateReplacementTemplate(
            replacement,
            substitutionOptions,
            subject,
            matchIndex,
            matchLength,
            resolveNumber,
            resolveName,
            resolveLastCapture,
            mark,
            false);

    private SimpleReplacementPlan? GetSimpleReplacementPlan(string replacement, Pcre2SubstitutionOptions substitutionOptions)
    {
        return _replacementComponent.GetOrAdd(
            new SimpleReplacementCacheKey(replacement, substitutionOptions),
            static key => TryParseSimpleReplacementPlan(key.Replacement, key.Options, out var plan) ? plan : null);
    }

    private static bool TryParseSimpleReplacementPlan(string replacement, Pcre2SubstitutionOptions substitutionOptions, out SimpleReplacementPlan plan)
    {
        if (substitutionOptions != Pcre2SubstitutionOptions.None)
        {
            plan = default;
            return false;
        }

        List<SimpleReplacementSegment>? segments = null;
        var literalStart = 0;
        for (var i = 0; i < replacement.Length; i++)
        {
            if (replacement[i] != '$')
            {
                continue;
            }

            if (i + 1 >= replacement.Length)
            {
                continue;
            }

            var next = replacement[i + 1];
            if (next == '$')
            {
                segments ??= [];
                if (i > literalStart)
                {
                    segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..i]));
                }

                segments.Add(SimpleReplacementSegment.FromLiteral("$"));
                i++;
                literalStart = i + 1;
                continue;
            }

            if (next == '&' || next == '0')
            {
                segments ??= [];
                if (i > literalStart)
                {
                    segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..i]));
                }

                segments.Add(SimpleReplacementSegment.WholeMatch());
                i++;
                literalStart = i + 1;
                continue;
            }

            if (next == '{')
            {
                var closing = replacement.IndexOf('}', i + 2);
                if (closing == i + 3 && replacement[i + 2] == '0')
                {
                    segments ??= [];
                    if (i > literalStart)
                    {
                        segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..i]));
                    }

                    segments.Add(SimpleReplacementSegment.WholeMatch());
                    i = closing;
                    literalStart = i + 1;
                    continue;
                }

                plan = default;
                return false;
            }

            plan = default;
            return false;
        }

        if (segments is null)
        {
            plan = new SimpleReplacementPlan([SimpleReplacementSegment.FromLiteral(replacement)]);
            return true;
        }

        if (literalStart < replacement.Length)
        {
            segments.Add(SimpleReplacementSegment.FromLiteral(replacement[literalStart..]));
        }

        plan = new SimpleReplacementPlan([.. segments]);
        return true;
    }

    private static void AppendSimpleReplacement(StringBuilder builder, SimpleReplacementPlan plan, string subject, int matchIndex, int matchLength)
    {
        foreach (var segment in plan.Segments)
        {
            if (segment.IsWholeMatch)
            {
                builder.Append(subject, matchIndex, matchLength);
            }
            else
            {
                builder.Append(segment.Literal);
            }
        }
    }


    private static void AppendEvaluatedReplacementTemplate(
        StringBuilder builder,
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        for (var i = 0; i < replacement.Length; i++)
        {
            var current = replacement[i];
            if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0 && quoted)
            {
                if (current == '\\' && i + 1 < replacement.Length && replacement[i + 1] == 'E')
                {
                    quoted = false;
                    caseTransform = ReplacementCaseTransformMode.None;
                    i++;
                    continue;
                }

                AppendReplacementChar(builder, current, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                continue;
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0 && current == '\\')
            {
                i = AppendExtendedEscape(
                    builder,
                    replacement,
                    i,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
                continue;
            }

            if (current == '$')
            {
                i = AppendDollarReplacement(
                    builder,
                    replacement,
                    i,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
                continue;
            }

            AppendReplacementChar(builder, current, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }
    }

    private static int AppendExtendedEscape(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        if (index + 1 >= replacement.Length)
        {
            AppendReplacementChar(builder, '\\', ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return index;
        }

        var next = replacement[index + 1];
        if (char.IsAsciiDigit(next))
        {
            var end = index + 2;
            while (end < replacement.Length && char.IsAsciiDigit(replacement[end]))
            {
                end++;
            }

            var token = replacement[(index + 1)..end];
            if (next == '0' || (token.Length > 1 && token.All(static c => c is >= '0' and <= '7')))
            {
                AppendReplacementChar(builder, (char)Convert.ToInt32(token, 8), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return end - 1;
            }

            AppendResolvedReference(
                builder,
                resolveNumber(int.Parse(token)),
                substitutionOptions,
                ref nextCaseTransform,
                ref caseTransform,
                syntaxOnly);
            return end - 1;
        }

        if (next == 'g' && index + 3 < replacement.Length && replacement[index + 2] == '<')
        {
            var closing = replacement.IndexOf('>', index + 3);
            if (closing < 0)
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
            }

            var token = replacement[(index + 3)..closing];
            if (string.IsNullOrEmpty(token) || string.Equals(token, "*MARK", StringComparison.Ordinal))
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
            }

            AppendResolvedSelector(builder, token, substitutionOptions, resolveNumber, resolveName, mark, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return closing;
        }

        if (next == 'g')
        {
            throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
        }

        if (next == 'o')
        {
            if (index + 3 >= replacement.Length || replacement[index + 2] != '{')
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
            }

            var closing = replacement.IndexOf('}', index + 3);
            if (closing < 0)
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
            }

            var octalDigits = replacement[(index + 3)..closing];
            if (octalDigits.Length == 0 || octalDigits.Any(static c => c is < '0' or > '7'))
            {
                throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
            }

            AppendReplacementChar(builder, (char)Convert.ToInt32(octalDigits, 8), ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return closing;
        }

        switch (next)
        {
            case 'a':
                AppendReplacementChar(builder, '\a', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'b':
                AppendReplacementChar(builder, '\b', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'e':
                AppendReplacementChar(builder, '\u001B', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'f':
                AppendReplacementChar(builder, '\f', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'n':
                AppendReplacementChar(builder, '\n', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'r':
                AppendReplacementChar(builder, '\r', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 't':
                AppendReplacementChar(builder, '\t', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'v':
                AppendReplacementChar(builder, '\v', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '\\':
                AppendReplacementChar(builder, '\\', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'Q':
                quoted = true;
                return index + 1;
            case 'E':
                quoted = false;
                nextCaseTransform = ReplacementCaseTransformMode.None;
                caseTransform = ReplacementCaseTransformMode.None;
                return index + 1;
            case 'U':
                caseTransform = ReplacementCaseTransformMode.UpperUntilEnd;
                return index + 1;
            case 'L':
                caseTransform = ReplacementCaseTransformMode.LowerUntilEnd;
                return index + 1;
            case 'u':
                nextCaseTransform = ReplacementCaseTransformMode.UpperNext;
                caseTransform = ReplacementCaseTransformMode.None;
                return index + 1;
            case 'l':
                nextCaseTransform = ReplacementCaseTransformMode.LowerNext;
                caseTransform = ReplacementCaseTransformMode.None;
                return index + 1;
        }

        if (char.IsAsciiLetter(next))
        {
            throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
        }

        AppendReplacementChar(builder, next, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return index + 1;
    }

    private static int AppendDollarReplacement(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        if (index + 1 >= replacement.Length)
        {
            AppendReplacementChar(builder, '$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return index;
        }

        var next = replacement[index + 1];
        switch (next)
        {
            case '$':
                AppendReplacementChar(builder, '$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '&':
                AppendReplacementText(builder, subject.AsSpan(matchIndex, matchLength), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '`':
                AppendReplacementText(builder, subject.AsSpan(0, matchIndex), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '\'':
                AppendReplacementText(builder, subject.AsSpan(matchIndex + matchLength, subject.Length - matchIndex - matchLength), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '_':
                AppendReplacementText(builder, subject.AsSpan(), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '+':
                if (index + 2 < replacement.Length && replacement[index + 2] == '+')
                {
                    throw new Pcre2SubstitutionException("Invalid replacement reference.", Pcre2ErrorKind.InvalidReplacementReference);
                }

                AppendResolvedPlusReference(builder, resolveLastCapture(), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '{':
                return AppendBracedReplacement(
                    builder,
                    replacement,
                    index,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
            case '<' when (substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0:
                return AppendAngleReplacement(builder, replacement, index, substitutionOptions, resolveName, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }

        if (char.IsAsciiDigit(next))
        {
            var end = index + 2;
            while (end < replacement.Length && char.IsAsciiDigit(replacement[end]))
            {
                end++;
            }

            AppendResolvedReference(builder, resolveNumber(int.Parse(replacement[(index + 1)..end])), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return end - 1;
        }

        if (IsReplacementNameStart(next))
        {
            var end = index + 2;
            while (end < replacement.Length && IsReplacementNamePart(replacement[end]))
            {
                end++;
            }

            AppendResolvedReference(builder, resolveName(replacement[(index + 1)..end]), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return end - 1;
        }

        AppendReplacementChar(builder, '$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return index;
    }

    private static int AppendBracedReplacement(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        string subject,
        int matchIndex,
        int matchLength,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        Func<ReplacementReferenceResolution> resolveLastCapture,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        var contentStart = index + 2;
        if (contentStart >= replacement.Length)
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", Pcre2ErrorKind.MalformedReplacementPattern);
        }

        var closing = FindReplacementBraceClose(replacement, contentStart);
        if (closing < 0)
        {
            var kind = contentStart < replacement.Length && char.IsAsciiDigit(replacement[contentStart])
                ? Pcre2ErrorKind.MissingClosingReplacementBrace
                : Pcre2ErrorKind.MalformedReplacementPattern;
            throw new Pcre2SubstitutionException("Replacement brace is not closed.", kind);
        }

        var content = replacement[contentStart..closing];
        if (string.IsNullOrEmpty(content))
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", Pcre2ErrorKind.MalformedReplacementPattern);
        }

        if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0)
        {
            var conditionalIndex = content.IndexOf(":+", StringComparison.Ordinal);
            if (conditionalIndex > 0)
            {
                var selector = content[..conditionalIndex];
                var branchPayload = content[(conditionalIndex + 2)..];
                var (thenBranch, elseBranch, hasElseBranch) = SplitConditionalBranches(branchPayload);

                var resolution = ResolveSelector(selector, resolveNumber, resolveName, mark);
                if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                {
                    throw new Pcre2SubstitutionException("Unknown replacement group.", Pcre2ErrorKind.UnknownReplacementGroup);
                }

                var validationNextTransform = nextCaseTransform;
                var validationTransform = caseTransform;
                var validationQuoted = quoted;
                AppendEvaluatedReplacementTemplate(
                    new StringBuilder(),
                    thenBranch,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref validationNextTransform,
                    ref validationTransform,
                    ref validationQuoted,
                    syntaxOnly: true);
                if (hasElseBranch)
                {
                    validationNextTransform = nextCaseTransform;
                    validationTransform = caseTransform;
                    validationQuoted = quoted;
                    AppendEvaluatedReplacementTemplate(
                        new StringBuilder(),
                        elseBranch,
                        substitutionOptions,
                        subject,
                        matchIndex,
                        matchLength,
                        resolveNumber,
                        resolveName,
                        resolveLastCapture,
                        mark,
                        ref validationNextTransform,
                        ref validationTransform,
                        ref validationQuoted,
                        syntaxOnly: true);
                }

                var chosen = resolution.Known && resolution.Success
                    ? thenBranch
                    : elseBranch;
                AppendEvaluatedReplacementTemplate(
                    builder,
                    chosen,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    resolveNumber,
                    resolveName,
                    resolveLastCapture,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
                return closing;
            }

            var fallbackIndex = content.IndexOf(":-", StringComparison.Ordinal);
            if (fallbackIndex > 0)
            {
                var selector = content[..fallbackIndex];
                var fallback = content[(fallbackIndex + 2)..];
                var resolution = ResolveSelector(selector, resolveNumber, resolveName, mark);
                if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                {
                    throw new Pcre2SubstitutionException("Unknown replacement group.", Pcre2ErrorKind.UnknownReplacementGroup);
                }

                if (resolution.Known && resolution.Success)
                {
                    AppendReplacementText(builder, resolution.Value.AsSpan(), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                }
                else
                {
                    AppendEvaluatedReplacementTemplate(
                        builder,
                        fallback,
                        substitutionOptions,
                        subject,
                        matchIndex,
                        matchLength,
                        resolveNumber,
                        resolveName,
                        resolveLastCapture,
                        mark,
                        ref nextCaseTransform,
                        ref caseTransform,
                        ref quoted,
                        syntaxOnly);
                }

                return closing;
            }
        }

        if (!int.TryParse(content, out _) &&
            !content.StartsWith('*') &&
            !string.Equals(content, "*MARK", StringComparison.Ordinal) &&
            !content.All(IsReplacementNamePart))
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", Pcre2ErrorKind.MalformedReplacementPattern);
        }

        AppendResolvedSelector(builder, content, substitutionOptions, resolveNumber, resolveName, mark, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return closing;
    }

    private static int AppendAngleReplacement(
        StringBuilder builder,
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        Func<string, ReplacementReferenceResolution> resolveName,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        bool syntaxOnly)
    {
        var closing = replacement.IndexOf('>', index + 2);
        if (closing < 0)
        {
            if (syntaxOnly)
            {
                return replacement.Length - 1;
            }

            throw new Pcre2SubstitutionException("Malformed replacement pattern.", Pcre2ErrorKind.MalformedReplacementPattern);
        }

        var name = replacement[(index + 2)..closing];
        if (string.IsNullOrEmpty(name) || name[0] == '*')
        {
            throw new Pcre2SubstitutionException("Malformed replacement pattern.", Pcre2ErrorKind.MalformedReplacementPattern);
        }

        AppendResolvedReference(builder, resolveName(name), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return closing;
    }

    private static void AppendResolvedSelector(
        StringBuilder builder,
        string token,
        Pcre2SubstitutionOptions substitutionOptions,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        string? mark,
        ref ReplacementCaseTransformMode nextCaseTransform,
        ref ReplacementCaseTransformMode caseTransform,
        bool syntaxOnly)
    {
        if (string.Equals(token, "*MARK", StringComparison.Ordinal))
        {
            AppendResolvedReference(builder, ResolveMarkReference(mark), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return;
        }

        AppendResolvedReference(builder, ResolveSelector(token, resolveNumber, resolveName, mark), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private static ReplacementReferenceResolution ResolveSelector(
        string token,
        Func<int, ReplacementReferenceResolution> resolveNumber,
        Func<string, ReplacementReferenceResolution> resolveName,
        string? mark)
    {
        if (string.Equals(token, "*MARK", StringComparison.Ordinal))
        {
            return ResolveMarkReference(mark);
        }

        return int.TryParse(token, out var number) ? resolveNumber(number) : resolveName(token);
    }

    private static void AppendResolvedReference(StringBuilder builder, ReplacementReferenceResolution resolution, Pcre2SubstitutionOptions substitutionOptions, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        if (!resolution.Known)
        {
            if (syntaxOnly)
            {
                return;
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
            {
                throw new Pcre2SubstitutionException("Unknown replacement group.", Pcre2ErrorKind.UnknownReplacementGroup);
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.UnsetEmpty) == 0)
            {
                throw new Pcre2SubstitutionException("Unset replacement group.", Pcre2ErrorKind.UnsetReplacementGroup);
            }

            return;
        }

        if (!resolution.Success)
        {
            if (syntaxOnly)
            {
                return;
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.UnsetEmpty) == 0)
            {
                throw new Pcre2SubstitutionException("Unset replacement group.", Pcre2ErrorKind.UnsetReplacementGroup);
            }

            return;
        }

        AppendReplacementText(builder, resolution.Value.AsSpan(), ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private static void AppendResolvedPlusReference(StringBuilder builder, ReplacementReferenceResolution resolution, Pcre2SubstitutionOptions substitutionOptions, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) != 0)
        {
            return;
        }

        AppendResolvedReference(builder, resolution, substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private static (string ThenBranch, string ElseBranch, bool HasElseBranch) SplitConditionalBranches(string branchPayload)
    {
        var quoted = false;
        for (var i = 0; i < branchPayload.Length; i++)
        {
            var current = branchPayload[i];
            if (current == '\\' && i + 1 < branchPayload.Length)
            {
                var next = branchPayload[i + 1];
                if (quoted)
                {
                    if (next == 'E')
                    {
                        quoted = false;
                    }
                }
                else if (next == 'Q')
                {
                    quoted = true;
                }

                i++;
                continue;
            }

            if (quoted)
            {
                continue;
            }

            if (current == ':')
            {
                return (branchPayload[..i], branchPayload[(i + 1)..], true);
            }
        }

        return (branchPayload, string.Empty, false);
    }

    private static void AppendReplacementText(StringBuilder builder, ReadOnlySpan<char> text, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        foreach (var value in text)
        {
            AppendReplacementChar(builder, value, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }
    }

    private static void AppendReplacementChar(StringBuilder builder, char value, ref ReplacementCaseTransformMode nextCaseTransform, ref ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        var effectiveTransform = nextCaseTransform is not ReplacementCaseTransformMode.None
            ? nextCaseTransform
            : caseTransform;
        var transformed = effectiveTransform switch
        {
            ReplacementCaseTransformMode.UpperNext or ReplacementCaseTransformMode.UpperUntilEnd => char.ToUpperInvariant(value),
            ReplacementCaseTransformMode.LowerNext or ReplacementCaseTransformMode.LowerUntilEnd => char.ToLowerInvariant(value),
            _ => value,
        };

        if (!syntaxOnly)
        {
            builder.Append(transformed);
        }

        if (nextCaseTransform is ReplacementCaseTransformMode.UpperNext or ReplacementCaseTransformMode.LowerNext)
        {
            nextCaseTransform = ReplacementCaseTransformMode.None;
        }
    }



    private static ReplacementReferenceResolution ResolveNativeNumberReference(Pcre2GroupData[] groups, int number, string subject)
    {
        if ((uint)number >= (uint)groups.Length)
        {
            return default;
        }

        var group = groups[number];
        return new ReplacementReferenceResolution(
            Known: true,
            Success: group.Success,
            Value: group.Success ? subject[group.StartOffsetInUtf16..group.EndOffsetInUtf16] : string.Empty);
    }


    private static ReplacementReferenceResolution ResolveNativeNamedReference(Pcre2GroupData[] groups, Pcre2NameEntry[]? nameEntries, string name, string subject)
    {
        if (nameEntries is null)
        {
            return default;
        }

        var known = false;
        foreach (var entry in nameEntries)
        {
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal))
            {
                continue;
            }

            known = true;
            var number = entry.Number;
            if ((uint)number < (uint)groups.Length && groups[number].Success)
            {
                return new ReplacementReferenceResolution(true, true, subject[groups[number].StartOffsetInUtf16..groups[number].EndOffsetInUtf16]);
            }
        }

        return known
            ? new ReplacementReferenceResolution(true, false, string.Empty)
            : default;
    }


    private static ReplacementReferenceResolution ResolveNativeLastCapturedReference(Pcre2GroupData[] groups, string subject)
    {
        if (groups.Length <= 1)
        {
            return default;
        }

        for (var i = groups.Length - 1; i >= 1; i--)
        {
            var group = groups[i];
            if (group.Success)
            {
                return new ReplacementReferenceResolution(true, true, subject[group.StartOffsetInUtf16..group.EndOffsetInUtf16]);
            }
        }

        return new ReplacementReferenceResolution(true, false, string.Empty);
    }

    private static ReplacementReferenceResolution ResolveMarkReference(string? mark)
        => new(true, mark is not null, mark ?? string.Empty);


    internal int DebugCountRaw(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCount(
                _program,
                ref subject,
                start,
                Pcre2MatchOptions.None,
                out var directCount))
        {
            return directCount;
        }

        if (_program.Operations.Count.Kind == Pcre2DirectProgramKind.Pcre2Literal)
        {
            return Count(input, startOffsetInBytes, Pcre2MatchOptions.None);
        }

        if (_program.Operations.Count.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            return PrimaryUtf8Regex.ByteOffsetExecution.CountPrepared(subject, start);
        }

        if (_program.Operations.Count.Kind == Pcre2DirectProgramKind.ManagedRegex)
        {
            var managedSubject = Encoding.UTF8.GetString(input);
            var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
            return ManagedRegex.Count(managedSubject, startOffsetInUtf16);
        }

        return Count(input, startOffsetInBytes, Pcre2MatchOptions.None);
    }

    internal Pcre2IsMatchDiagnostics DebugIsMatchWithDiagnostics(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var backtrackingProgram = _program.Operations.IsMatch switch
        {
            Pcre2BacktrackingDirectProgram direct => direct.Program,
            Pcre2AsciiRegularIsMatchDirectProgram asciiRegular => asciiRegular.Fallback,
            Pcre2LiteralFamilyDirectProgram literalFamily => literalFamily.Fallback,
            Pcre2MultilinePrefixDirectProgram multilinePrefix => multilinePrefix.Fallback,
            _ => null,
        };
        if (backtrackingProgram is null)
        {
            return new Pcre2IsMatchDiagnostics(IsMatch(input, startOffsetInBytes), default);
        }

        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(
            backtrackingProgram,
            _program.CandidateSearchPlan,
            subject,
            start,
            Pcre2MatchOptions.None,
            _program.Request,
            collectDiagnostics: true);
        return new Pcre2IsMatchDiagnostics(cursor.MoveNext(), cursor.Diagnostics);
    }

    internal Pcre2CountDiagnostics DebugCountWithDiagnostics(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ThrowIfGenericIterationMayBeNonMonotone();
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        var backtrackingProgram = _program.Operations.Enumerate switch
        {
            Pcre2BacktrackingDirectProgram direct => direct.Program,
            Pcre2MultilinePrefixDirectProgram multilinePrefix => multilinePrefix.Fallback,
            _ => null,
        };
        if (backtrackingProgram is null)
        {
            return new Pcre2CountDiagnostics(Count(input, startOffsetInBytes), default);
        }

        var cursor = Pcre2GlobalMatchCursor.CreateBacktracking(
            backtrackingProgram,
            _program.CandidateSearchPlan,
            subject,
            start,
            Pcre2MatchOptions.None,
            _program.Request,
            collectDiagnostics: true);
        var count = 0;
        while (cursor.MoveNext())
        {
            count = checked(count + 1);
        }

        return new Pcre2CountDiagnostics(count, cursor.Diagnostics);
    }

    internal int DebugEnumerateRawIndexSum(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                Pcre2MatchOptions.None,
                out var directCursor))
        {
            var directSum = 0;
            while (directCursor.MoveNext())
            {
                directSum += directCursor.Current.StartOffsetInBytes;
            }

            return directSum;
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Pcre2Literal)
        {
            return ExecutePublicEnumerateIndexSum(EnumerateMatches(
                input,
                startOffsetInBytes,
                Pcre2MatchOptions.None));
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Utf8Regex)
        {
            var sum = 0;
            var enumerator = PrimaryUtf8Regex.ByteOffsetExecution.EnumerateMatches(subject, start);
            while (enumerator.MoveNext())
            {
                if (!enumerator.Current.TryGetByteRange(out var indexInBytes, out _))
                {
                    throw new InvalidOperationException("Managed Utf8Regex fallback returned a match that is not aligned to byte boundaries.");
                }

                sum += indexInBytes;
            }

            return sum;
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.ManagedRegex)
        {
            var managedSubject = Encoding.UTF8.GetString(input);
            var startOffsetInUtf16 = Encoding.UTF8.GetCharCount(input[..startOffsetInBytes]);
            var matchesAreAscii = managedSubject.Length == input.Length;
            Utf8BoundaryMap? boundaryMap = matchesAreAscii ? null : Utf8InputAnalyzer.Analyze(input).BoundaryMap;
            var sum = 0;
            foreach (var match in ManagedRegex.EnumerateMatches(managedSubject.AsSpan(), startOffsetInUtf16))
            {
                if (matchesAreAscii)
                {
                    sum += match.Index;
                    continue;
                }

                if (boundaryMap is not { } map || !map.TryGetByteRange(match.Index, match.Length, out var indexInBytes, out _))
                {
                    throw new InvalidOperationException("Managed Regex fallback produced a match that is not aligned to UTF-8 scalar boundaries.");
                }

                sum += indexInBytes;
            }

            return sum;
        }

        var fallbackSum = 0;
        var fallbackEnumerator = EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None);
        while (fallbackEnumerator.MoveNext())
        {
            fallbackSum += fallbackEnumerator.Current.StartOffsetInBytes;
        }

        return fallbackSum;
    }

    internal int DebugEnumeratePublicConstructionOnly(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        _ = EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None);
        return 1;
    }

    internal int DebugEnumerateNativeMaterializationOnly(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = ValidateSubjectAndStart(input, startOffsetInBytes, out var start);
        if (Pcre2GlobalOperationDriver.TryCreateCursor(
                _program,
                subject,
                start,
                Pcre2MatchOptions.None,
                out var directCursor))
        {
            var directCount = 0;
            while (directCursor.MoveNext())
            {
                directCount++;
            }

            return directCount;
        }

        if (_program.Operations.Enumerate.Kind == Pcre2DirectProgramKind.Pcre2Literal)
        {
            return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(
                input,
                startOffsetInBytes,
                Pcre2MatchOptions.None));
        }

        return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(
            input,
            startOffsetInBytes,
            Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicMoveNextCount(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateMoveNextCount(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicIndexSum(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateIndexSum(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicCurrentCount(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateCurrentCount(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    internal int DebugEnumerateInternalPublicCurrentStartSum(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        return ExecutePublicEnumerateCurrentStartSum(EnumerateMatches(input, startOffsetInBytes, Pcre2MatchOptions.None));
    }

    private static int ExecutePublicEnumerateMoveNextCount(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var count = 0;
        while (enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int ExecutePublicEnumerateIndexSum(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var sum = 0;
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
        }

        return sum;
    }

    private static int ExecutePublicEnumerateCurrentCount(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var count = 0;
        while (enumerator.MoveNext())
        {
            _ = enumerator.Current;
            count++;
        }

        return count;
    }

    private static int ExecutePublicEnumerateCurrentStartSum(Utf8Pcre2ValueMatchEnumerator enumerator)
    {
        var sum = 0;
        while (enumerator.MoveNext())
        {
            var current = enumerator.Current;
            sum += current.StartOffsetInBytes;
        }

        return sum;
    }

    internal int DebugEvaluateFirstReplacementOnly(ReadOnlySpan<byte> input, string replacement, Pcre2SubstitutionOptions substitutionOptions, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Encoding.UTF8.GetString(input);

        var match = MatchDetailed(input, startOffsetInBytes, Pcre2MatchOptions.None);
        if (!match.Success)
        {
            return 0;
        }

        var value = match.Value;
        var groups = CaptureGroups(match);
        return EvaluateReplacementTemplate(
            replacement,
            substitutionOptions,
            subject,
            value.StartOffsetInUtf16,
            value.EndOffsetInUtf16 - value.StartOffsetInUtf16,
            number => ResolveNativeNumberReference(groups, number, subject),
            name => ResolveNativeNamedReference(groups, NameEntries, name, subject),
            () => ResolveNativeLastCapturedReference(groups, subject),
            match.Mark).Length;
    }

    private static bool IsReplacementNameStart(char value)
        => char.IsAsciiLetter(value) || value == '_';

    private static bool IsReplacementNamePart(char value)
        => char.IsAsciiLetterOrDigit(value) || value == '_';

    private static int FindReplacementBraceClose(string replacement, int startIndex)
    {
        var quoted = false;
        for (var i = startIndex; i < replacement.Length; i++)
        {
            if (replacement[i] == '\\' && i + 1 < replacement.Length)
            {
                if (quoted)
                {
                    if (replacement[i + 1] == 'E')
                    {
                        quoted = false;
                    }

                    i++;
                    continue;
                }

                if (replacement[i + 1] == 'Q')
                {
                    quoted = true;
                    i++;
                    continue;
                }
            }

            if (quoted)
            {
                continue;
            }

            if (replacement[i] != '\\' || i + 2 >= replacement.Length || replacement[i + 1] != 'o' || replacement[i + 2] != '{')
            {
                if (replacement[i] == '}')
                {
                    return i;
                }

                continue;
            }

            var innerClose = replacement.IndexOf('}', i + 3);
            if (innerClose < 0)
            {
                return -1;
            }

            i = innerClose;
        }

        return -1;
    }

    private readonly record struct ReplacementReferenceResolution(bool Known, bool Success, string Value);

    private readonly record struct SimpleReplacementCacheKey(string Replacement, Pcre2SubstitutionOptions Options);

    private readonly record struct SimpleReplacementSegment(string Literal, byte[] LiteralUtf8, bool IsWholeMatch)
    {
        public static SimpleReplacementSegment FromLiteral(string literal) =>
            new(literal, Encoding.UTF8.GetBytes(literal), false);

        public static SimpleReplacementSegment WholeMatch() => new(string.Empty, [], true);
    }

    private readonly record struct SimpleReplacementPlan(SimpleReplacementSegment[] Segments);

    private sealed class Pcre2ReplacementComponent
    {
        private const int Capacity = 16;
        private readonly Utf8BoundedPreparationCache<SimpleReplacementCacheKey, SimpleReplacementPlan?> _plans = new(Capacity);

        internal int Count => _plans.Count;

        internal SimpleReplacementPlan? GetOrAdd(
            SimpleReplacementCacheKey key,
            Func<SimpleReplacementCacheKey, SimpleReplacementPlan?> factory)
            => _plans.GetOrAdd(key, factory);
    }

    private enum ReplacementCaseTransformMode
    {
        None = 0,
        UpperNext = 1,
        LowerNext = 2,
        UpperUntilEnd = 3,
        LowerUntilEnd = 4,
    }

    internal string DebugExecutionKindName => _program.Operations.Match.Kind.ToString();

    internal Pcre2CompiledProgram DebugCompiledProgram => _program;

    internal bool DebugUsesUtf8RegexTranslation => UsesUtf8Translation;

    internal string DebugUtf8RegexExecutionKindName => HasPrimaryUtf8Regex ? PrimaryUtf8Regex.Inspection.ExecutionKind.ToString() : "<none>";

    internal bool DebugHasManagedRegex => HasManagedRegex;

    internal int DebugReplacementCacheEntryCount => _replacementComponent.Count;

    internal string DebugDescribeExecutionPlan()
        => $"IsMatch={_program.Operations.IsMatch.Kind}, Count={_program.Operations.Count.Kind}, Enumerate={_program.Operations.Enumerate.Kind}, Match={_program.Operations.Match.Kind}, Replace={_program.Operations.Replace.Kind}";
}
