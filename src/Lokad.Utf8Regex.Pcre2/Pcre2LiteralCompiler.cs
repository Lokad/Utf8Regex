using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Pcre2;

internal enum Pcre2LiteralAnchor : byte
{
    None = 0,
    AbsoluteStart = 1,
    RequestedStart = 2,
}

internal readonly record struct Pcre2LiteralAtom(Rune Scalar, int PatternOffsetInUtf16, int PatternLengthInUtf16);

internal sealed class Pcre2LiteralSyntaxTree : IPcre2SyntaxTree
{
    private readonly Pcre2LiteralAtom[] _atoms;

    internal Pcre2LiteralSyntaxTree(
        Pcre2LiteralAtom[] atoms,
        Pcre2LiteralAnchor startAnchor,
        bool endAnchored)
    {
        _atoms = [.. atoms];
        StartAnchor = startAnchor;
        EndAnchored = endAnchored;
    }

    public Pcre2SyntaxNodeKind RootKind => Pcre2SyntaxNodeKind.Literal;

    internal ReadOnlySpan<Pcre2LiteralAtom> Atoms => _atoms;

    internal Pcre2LiteralAnchor StartAnchor { get; }

    internal bool EndAnchored { get; }
}

internal sealed class Pcre2LiteralProgram
{
    private const int DirectCountThresholdBytes = 1024;
    private readonly byte[] _literalUtf8;

    internal Pcre2LiteralProgram(
        byte[] literalUtf8,
        Pcre2LiteralAnchor startAnchor,
        bool endAnchored,
        bool hasExplicitEndAssertion)
    {
        _literalUtf8 = [.. literalUtf8];
        Search = new PreparedSubstringSearch(_literalUtf8, ignoreCase: false);
        StartAnchor = startAnchor;
        EndAnchored = endAnchored;
        HasExplicitEndAssertion = hasExplicitEndAssertion;
    }

    internal ReadOnlySpan<byte> LiteralUtf8 => _literalUtf8;

    internal PreparedSubstringSearch Search { get; }

    internal Pcre2LiteralAnchor StartAnchor { get; }

    internal bool EndAnchored { get; }

    internal bool HasExplicitEndAssertion { get; }

    internal bool CanCountDirectly(int inputLength) =>
        inputLength >= DirectCountThresholdBytes &&
        _literalUtf8.Length != 0 &&
        StartAnchor == Pcre2LiteralAnchor.None &&
        !EndAnchored;

    internal int CountNonOverlapping(ReadOnlySpan<byte> input) =>
        Search.CountWithMetrics(input, out _, out _);
}

internal readonly record struct Pcre2LiteralMatch(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int ConsumedEndOffsetInBytes)
{
    internal static Pcre2LiteralMatch NoMatch => default;

    internal static Pcre2LiteralMatch Create(int startOffsetInBytes, int lengthInBytes)
        => new(
            true,
            startOffsetInBytes,
            startOffsetInBytes + lengthInBytes,
            startOffsetInBytes + lengthInBytes);
}

internal interface IPcre2LiteralCompileOutcome
{
}

internal sealed class Pcre2NotLiteralOutcome : IPcre2LiteralCompileOutcome
{
    internal static Pcre2NotLiteralOutcome Instance { get; } = new();

    private Pcre2NotLiteralOutcome()
    {
    }
}

internal sealed class Pcre2CompiledLiteralOutcome : IPcre2LiteralCompileOutcome
{
    internal Pcre2CompiledLiteralOutcome(Pcre2LiteralSyntaxTree syntaxTree, Pcre2LiteralProgram program)
    {
        SyntaxTree = syntaxTree;
        Program = program;
    }

    internal Pcre2LiteralSyntaxTree SyntaxTree { get; }

    internal Pcre2LiteralProgram Program { get; }
}

internal static class Pcre2LiteralCompiler
{
    private const Pcre2CompileOptions UnsupportedOptions =
        Pcre2CompileOptions.Caseless |
        Pcre2CompileOptions.Extended |
        Pcre2CompileOptions.ExtendedMore |
        Pcre2CompileOptions.FirstLine;

    internal static IPcre2LiteralCompileOutcome Compile(Pcre2CompileRequest request)
    {
        if ((request.Options & UnsupportedOptions) != 0 ||
            !TryParse(request.Pattern, out var atoms, out var startAnchor, out var endAnchored))
        {
            return Pcre2NotLiteralOutcome.Instance;
        }

        if ((request.Options & Pcre2CompileOptions.Anchored) != 0 && startAnchor == Pcre2LiteralAnchor.None)
        {
            startAnchor = Pcre2LiteralAnchor.RequestedStart;
        }

        var hasExplicitEndAssertion = endAnchored;
        endAnchored |= (request.Options & Pcre2CompileOptions.EndAnchored) != 0;
        var syntaxTree = new Pcre2LiteralSyntaxTree(atoms, startAnchor, endAnchored);

        var literalUtf8 = new byte[atoms.Sum(static atom => atom.Scalar.Utf8SequenceLength)];
        var written = 0;
        foreach (var atom in atoms)
        {
            written += atom.Scalar.EncodeToUtf8(literalUtf8.AsSpan(written));
        }

        var program = new Pcre2LiteralProgram(
            literalUtf8,
            startAnchor,
            endAnchored,
            hasExplicitEndAssertion);
        return new Pcre2CompiledLiteralOutcome(syntaxTree, program);
    }

    private static bool TryParse(
        string pattern,
        out Pcre2LiteralAtom[] atoms,
        out Pcre2LiteralAnchor startAnchor,
        out bool endAnchored)
    {
        var parsedAtoms = new List<Pcre2LiteralAtom>(pattern.Length);
        var position = 0;
        startAnchor = Pcre2LiteralAnchor.None;
        endAnchored = false;

        if (pattern.StartsWith(@"\A", StringComparison.Ordinal))
        {
            startAnchor = Pcre2LiteralAnchor.AbsoluteStart;
            position = 2;
        }
        else if (pattern.StartsWith(@"\G", StringComparison.Ordinal))
        {
            startAnchor = Pcre2LiteralAnchor.RequestedStart;
            position = 2;
        }

        var contentEnd = pattern.EndsWith(@"\z", StringComparison.Ordinal) && pattern.Length - position >= 2
            ? pattern.Length - 2
            : pattern.Length;
        endAnchored = contentEnd != pattern.Length;

        while (position < contentEnd)
        {
            var atomOffset = position;
            if (pattern[position] == '\\')
            {
                if (position + 1 >= contentEnd || !IsEscapedLiteralMetacharacter(pattern[position + 1]))
                {
                    atoms = [];
                    return false;
                }

                parsedAtoms.Add(new Pcre2LiteralAtom(new Rune(pattern[position + 1]), atomOffset, 2));
                position += 2;
                continue;
            }

            if (IsUnescapedMetacharacter(pattern[position]))
            {
                atoms = [];
                return false;
            }

            var status = Rune.DecodeFromUtf16(pattern.AsSpan(position, contentEnd - position), out var scalar, out var consumed);
            if (status != OperationStatus.Done)
            {
                atoms = [];
                return false;
            }

            parsedAtoms.Add(new Pcre2LiteralAtom(scalar, atomOffset, consumed));
            position += consumed;
        }

        atoms = [.. parsedAtoms];
        return true;
    }

    private static bool IsEscapedLiteralMetacharacter(char value)
        => value is '\\' or '.' or '^' or '$' or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '|';

    private static bool IsUnescapedMetacharacter(char value)
        => value is '\\' or '.' or '^' or '$' or '*' or '+' or '?' or '(' or ')' or '[' or ']' or '{' or '}' or '|';
}

internal static class Pcre2LiteralRunner
{
    private const int MeteredSearchChunkLength = 4096;

    internal static Pcre2LiteralMatch Match(
        Pcre2LiteralProgram program,
        ref Utf8ValidatedInput input,
        Utf8BytePosition requestedStart,
        Pcre2MatchOptions matchOptions,
        ref Pcre2ResourceBudget budget)
    {
        var literal = program.LiteralUtf8;
        var startAnchor = GetEffectiveStartAnchor(program.StartAnchor, matchOptions);
        var endAnchored = program.EndAnchored || (matchOptions & Pcre2MatchOptions.EndAnchored) != 0;

        if ((matchOptions & Pcre2MatchOptions.NotEmpty) != 0 && literal.Length == 0)
        {
            return Pcre2LiteralMatch.NoMatch;
        }

        if (startAnchor == Pcre2LiteralAnchor.AbsoluteStart)
        {
            budget.ChargeCandidate();
            return requestedStart.Value == 0 && MatchesAt(program, input.Bytes, 0, endAnchored)
                ? Pcre2LiteralMatch.Create(0, literal.Length)
                : Pcre2LiteralMatch.NoMatch;
        }

        if (startAnchor == Pcre2LiteralAnchor.RequestedStart)
        {
            budget.ChargeCandidate();
            return MatchesAt(program, input.Bytes, requestedStart.Value, endAnchored) &&
                   !RejectsEmptyAtRequestedStart(literal, matchOptions)
                ? Pcre2LiteralMatch.Create(requestedStart.Value, literal.Length)
                : Pcre2LiteralMatch.NoMatch;
        }

        if (endAnchored)
        {
            budget.ChargeCandidate();
            var candidate = input.ByteLength - literal.Length;
            return candidate >= requestedStart.Value &&
                   MatchesAt(program, input.Bytes, candidate, endAnchored: true) &&
                   !(candidate == requestedStart.Value && RejectsEmptyAtRequestedStart(literal, matchOptions))
                ? Pcre2LiteralMatch.Create(candidate, literal.Length)
                : Pcre2LiteralMatch.NoMatch;
        }

        var searchStart = requestedStart;
        if (RejectsEmptyAtRequestedStart(literal, matchOptions))
        {
            if (!input.TryAdvanceScalar(requestedStart, out searchStart))
            {
                return Pcre2LiteralMatch.NoMatch;
            }
        }

        if (literal.Length == 0)
        {
            budget.ChargeCandidate();
            return Pcre2LiteralMatch.Create(searchStart.Value, 0);
        }

        var relative = IndexOfLiteral(program, input.Bytes[searchStart.Value..], ref budget);
        return relative >= 0
            ? Pcre2LiteralMatch.Create(searchStart.Value + relative, literal.Length)
            : Pcre2LiteralMatch.NoMatch;
    }

    private static int IndexOfLiteral(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        ref Pcre2ResourceBudget budget)
    {
        if (!budget.RequiresCandidateMetering)
        {
            return Utf8SearchKernel.IndexOfLiteral(input, program.Search);
        }

        var literalLength = program.LiteralUtf8.Length;
        var offset = 0;
        while (offset <= input.Length - literalLength)
        {
            budget.ChargeCandidate();
            var searchLength = Math.Min(
                input.Length - offset,
                MeteredSearchChunkLength + literalLength - 1);
            var relative = Utf8SearchKernel.IndexOfLiteral(input.Slice(offset, searchLength), program.Search);
            if (relative >= 0)
            {
                return offset + relative;
            }

            offset += MeteredSearchChunkLength;
        }

        return -1;
    }

    private static Pcre2LiteralAnchor GetEffectiveStartAnchor(
        Pcre2LiteralAnchor compiledAnchor,
        Pcre2MatchOptions matchOptions)
    {
        if (compiledAnchor == Pcre2LiteralAnchor.AbsoluteStart)
        {
            return compiledAnchor;
        }

        return compiledAnchor == Pcre2LiteralAnchor.RequestedStart ||
               (matchOptions & Pcre2MatchOptions.Anchored) != 0
            ? Pcre2LiteralAnchor.RequestedStart
            : Pcre2LiteralAnchor.None;
    }

    private static bool MatchesAt(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        int candidate,
        bool endAnchored)
    {
        var literal = program.LiteralUtf8;
        return candidate >= 0 &&
               candidate <= input.Length - literal.Length &&
               (!endAnchored || candidate + literal.Length == input.Length) &&
               input.Slice(candidate, literal.Length).SequenceEqual(literal);
    }

    private static bool RejectsEmptyAtRequestedStart(ReadOnlySpan<byte> literal, Pcre2MatchOptions matchOptions)
        => literal.Length == 0 && (matchOptions & Pcre2MatchOptions.NotEmptyAtStart) != 0;
}

internal static class Pcre2LiteralProbeRunner
{
    internal static Utf8Pcre2ProbeResult Probe(
        ReadOnlySpan<byte> subject,
        Pcre2LiteralProgram program,
        Utf8ValidatedInput input,
        Utf8BytePosition requestedStart,
        Pcre2PartialMode partialMode,
        Pcre2MatchOptions matchOptions,
        Pcre2CompileRequest request)
    {
        var budget = new Pcre2ResourceBudget(request.DefaultLimits, request.MatchTimeout, collectDiagnostics: false);
        Pcre2LiteralMatch fullMatch;
        try
        {
            fullMatch = Pcre2LiteralRunner.Match(
                program,
                ref input,
                requestedStart,
                matchOptions,
                ref budget);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
        }

        if (fullMatch.Success)
        {
            var projected = Project(ref input, fullMatch.StartOffsetInBytes, fullMatch.EndOffsetInBytes);
            if (partialMode == Pcre2PartialMode.Hard &&
                program.HasExplicitEndAssertion &&
                fullMatch.EndOffsetInBytes == input.ByteLength)
            {
                return Utf8Pcre2ProbeResult.CreatePartial(subject, projected);
            }

            return Utf8Pcre2ProbeResult.CreateFullMatch(
                subject,
                [projected]);
        }

        if (partialMode == Pcre2PartialMode.None || program.LiteralUtf8.IsEmpty)
        {
            return Utf8Pcre2ProbeResult.CreateNoMatch(subject);
        }

        var literal = program.LiteralUtf8;
        var candidate = GetFirstPartialCandidate(program, input.Bytes, requestedStart, matchOptions);
        while (candidate >= 0 && candidate < input.ByteLength)
        {
            try
            {
                budget.ChargeCandidate();
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw new Pcre2MatchException("The PCRE2 match deadline expired.", "Timeout");
            }
            var suffix = input.Bytes[candidate..];
            if (suffix.Length < literal.Length && literal.StartsWith(suffix))
            {
                return Utf8Pcre2ProbeResult.CreatePartial(
                    subject,
                    Project(ref input, candidate, input.ByteLength));
            }

            if (IsEffectivelyAnchored(program, matchOptions) ||
                !input.TryAdvanceScalar(new Utf8BytePosition(candidate), out var next))
            {
                break;
            }

            candidate = next.Value;
        }

        return Utf8Pcre2ProbeResult.CreateNoMatch(subject);
    }

    private static int GetFirstPartialCandidate(
        Pcre2LiteralProgram program,
        ReadOnlySpan<byte> input,
        Utf8BytePosition requestedStart,
        Pcre2MatchOptions matchOptions)
    {
        if (program.StartAnchor == Pcre2LiteralAnchor.AbsoluteStart)
        {
            return requestedStart.Value == 0 ? 0 : -1;
        }

        if (IsEffectivelyAnchored(program, matchOptions))
        {
            return requestedStart.Value;
        }

        var earliestSuffix = Math.Max(requestedStart.Value, input.Length - program.LiteralUtf8.Length + 1);
        while (earliestSuffix < input.Length && (input[earliestSuffix] & 0xC0) == 0x80)
        {
            earliestSuffix++;
        }

        return earliestSuffix;
    }

    private static bool IsEffectivelyAnchored(Pcre2LiteralProgram program, Pcre2MatchOptions matchOptions)
        => program.StartAnchor != Pcre2LiteralAnchor.None ||
            (matchOptions & Pcre2MatchOptions.Anchored) != 0;

    private static Pcre2GroupData Project(
        ref Utf8ValidatedInput input,
        int startOffsetInBytes,
        int endOffsetInBytes)
    {
        var projection = input.CreateProjectionCursor();
        var startInUtf16 = projection.Project(new Utf8BytePosition(startOffsetInBytes));
        var endInUtf16 = projection.Project(new Utf8BytePosition(endOffsetInBytes));
        return new Pcre2GroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes,
            EndOffsetInBytes = endOffsetInBytes,
            StartOffsetInUtf16 = startInUtf16.Value,
            EndOffsetInUtf16 = endInUtf16.Value,
        };
    }
}
