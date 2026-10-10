using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

// Fixed templates shape directly into bytes. Text here is template/mark interop;
// captures and subject gaps remain byte ranges. General grammar preparation,
// aggregate matching/shaping authority and retained result capacity remain open.
internal ref struct Pcre2ReplacementTemplateWriter
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Pcre2NameEntry[] _names;
    private Utf8SliceOutputWriter _writer;
    private Utf8SliceInvocation _invocation;
    private Pcre2GroupData[] _groups;
    private string? _mark;

    internal Pcre2ReplacementTemplateWriter(ReadOnlySpan<byte> input, Pcre2NameEntry[] names,
        ulong heapLimit, TimeSpan timeout, CancellationToken token)
    {
        _input = input; _names = names; _groups = []; _mark = null;
        _invocation = new(new Utf8ExecutionLimits(0, 0, heapLimit), timeout, token);
        if (!Utf8SliceOutputWriter.Rent(input.Length, ref _invocation, out _writer)) ThrowHeapLimitExceeded();
    }

    internal int Length => _writer.Length;
    internal ulong ReservedWorkingBytes => _invocation.Ledger.ReservedBytes;
    internal void AppendGap(int start, int length) => AppendBytes(_input.Slice(start, length));

    internal void AppendTemplate(string replacement, Pcre2SubstitutionOptions options,
        Pcre2SimpleReplacementPlan? simple, Pcre2GroupData[] groups, int matchStart, int matchLength, string? mark)
    {
        _groups = groups; _mark = mark;
        if (simple is { } plan)
        {
            foreach (var segment in plan.Segments)
                AppendBytes(segment.IsWholeMatch ? _input.Slice(matchStart, matchLength) : segment.LiteralUtf8);
            return;
        }
        var nextCase = Pcre2ReplacementCaseTransformMode.None;
        var continuousCase = Pcre2ReplacementCaseTransformMode.None;
        var quoted = false;
        AppendEvaluatedReplacementTemplate(replacement, options, _input, matchStart, matchLength,
            mark, ref nextCase, ref continuousCase, ref quoted, false);
    }

    internal byte[] Complete() => _writer.ToArrayAndRelease(ref _invocation);
    internal void Release() { _writer.Release(ref _invocation); _groups = []; _mark = null; }

    private void AppendBytes(scoped ReadOnlySpan<byte> bytes)
    {
        CheckWork();
        if (!_writer.Append(bytes, ref _invocation)) ThrowHeapLimitExceeded();
    }

    private void CheckWork()
    {
        try
        {
            if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw new Pcre2MatchException("The PCRE2 match limit was exceeded.", Pcre2ErrorKind.MatchLimit);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout); }
    }

    private static void ThrowHeapLimitExceeded()
        => throw new Pcre2MatchException("The PCRE2 heap limit was exceeded.", Pcre2ErrorKind.HeapLimit);

    private static Rune ReadScalarEscape(string digits, int radix)
    {
        int value;
        try { value = Convert.ToInt32(digits, radix); }
        catch (Exception error) when (error is FormatException or OverflowException or ArgumentException)
        { throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape); }
        if (!Rune.TryCreate(value, out var scalar))
            throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
        return scalar;
    }

    private void AppendResolvedValue(Pcre2ReplacementReferenceResolution resolution,
        ref Pcre2ReplacementCaseTransformMode nextCase, ref Pcre2ReplacementCaseTransformMode continuousCase, bool syntaxOnly)
    {
        if (resolution.IsMark) AppendReplacementText(_mark.AsSpan(), ref nextCase, ref continuousCase, syntaxOnly);
        else AppendReplacementBytes(_input.Slice(resolution.Start, resolution.Length), ref nextCase, ref continuousCase, syntaxOnly);
    }

    private void AppendReplacementBytes(ReadOnlySpan<byte> bytes,
        ref Pcre2ReplacementCaseTransformMode nextCase, ref Pcre2ReplacementCaseTransformMode continuousCase, bool syntaxOnly)
    {
        if (nextCase == Pcre2ReplacementCaseTransformMode.None && continuousCase == Pcre2ReplacementCaseTransformMode.None)
        { if (!syntaxOnly) AppendBytes(bytes); return; }
        while (!bytes.IsEmpty)
        {
            CheckWork();
            if (Rune.DecodeFromUtf8(bytes, out var scalar, out var consumed) != OperationStatus.Done)
                _ = Utf8Validation.Validate(bytes); // Explicit byte captures cannot force the case of a partial scalar.
            AppendReplacementScalar(scalar, ref nextCase, ref continuousCase, syntaxOnly);
            bytes = bytes[consumed..];
        }
    }

    private Pcre2ReplacementReferenceResolution ResolveNumber(int number)
    {
        if ((uint)number >= (uint)_groups.Length) return default;
        var group = _groups[number];
        return new(true, group.Success, group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes, false);
    }
    private Pcre2ReplacementReferenceResolution ResolveName(string name)
    {
        var known = false;
        foreach (var entry in _names)
        {
            CheckWork();
            if (!string.Equals(entry.Name, name, StringComparison.Ordinal)) continue;
            known = true;
            if ((uint)entry.Number < (uint)_groups.Length && _groups[entry.Number].Success) return ResolveNumber(entry.Number);
        }
        return known ? new(true, false, 0, 0, false) : default;
    }
    private Pcre2ReplacementReferenceResolution ResolveLastCapture()
    {
        if (_groups.Length <= 1) return default;
        for (var index = _groups.Length - 1; index >= 1; index--)
        { CheckWork(); if (_groups[index].Success) return ResolveNumber(index); }
        return new(true, false, 0, 0, false);
    }
    private static Pcre2ReplacementReferenceResolution ResolveMarkReference(string? mark) => new(true, mark is not null, 0, 0, true);

    private void AppendEvaluatedReplacementTemplate(
        string replacement,
        Pcre2SubstitutionOptions substitutionOptions,
        ReadOnlySpan<byte> subject,
        int matchIndex,
        int matchLength,
        string? mark,
        ref Pcre2ReplacementCaseTransformMode nextCaseTransform,
        ref Pcre2ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        for (var i = 0; i < replacement.Length; i++)
        {
            CheckWork();
            var current = replacement[i];
            if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0 && quoted)
            {
                if (current == '\\' && i + 1 < replacement.Length && replacement[i + 1] == 'E')
                {
                    quoted = false;
                    caseTransform = Pcre2ReplacementCaseTransformMode.None;
                    i++;
                    continue;
                }

                AppendReplacementLiteral(replacement, ref i, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                continue;
            }

            if ((substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0 && current == '\\')
            {
                i = AppendExtendedEscape(
                    replacement,
                    i,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
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
                    replacement,
                    i,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
                continue;
            }

            AppendReplacementLiteral(replacement, ref i, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }
    }

    private int AppendExtendedEscape(
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        ReadOnlySpan<byte> subject,
        int matchIndex,
        int matchLength,
        string? mark,
        ref Pcre2ReplacementCaseTransformMode nextCaseTransform,
        ref Pcre2ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        if (index + 1 >= replacement.Length)
        {
            AppendReplacementChar('\\', ref nextCaseTransform, ref caseTransform, syntaxOnly);
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
                AppendReplacementScalar(ReadScalarEscape(token, 8), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return end - 1;
            }

            AppendResolvedReference(
                ResolveNumber(int.Parse(token)),
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

            AppendResolvedSelector(token, substitutionOptions, mark, ref nextCaseTransform, ref caseTransform, syntaxOnly);
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

            AppendReplacementScalar(ReadScalarEscape(octalDigits, 8), ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return closing;
        }

        switch (next)
        {
            case 'a':
                AppendReplacementChar('\a', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'b':
                AppendReplacementChar('\b', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'e':
                AppendReplacementChar('\u001B', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'f':
                AppendReplacementChar('\f', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'n':
                AppendReplacementChar('\n', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'r':
                AppendReplacementChar('\r', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 't':
                AppendReplacementChar('\t', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'v':
                AppendReplacementChar('\v', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '\\':
                AppendReplacementChar('\\', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case 'Q':
                quoted = true;
                return index + 1;
            case 'E':
                quoted = false;
                nextCaseTransform = Pcre2ReplacementCaseTransformMode.None;
                caseTransform = Pcre2ReplacementCaseTransformMode.None;
                return index + 1;
            case 'U':
                caseTransform = Pcre2ReplacementCaseTransformMode.UpperUntilEnd;
                return index + 1;
            case 'L':
                caseTransform = Pcre2ReplacementCaseTransformMode.LowerUntilEnd;
                return index + 1;
            case 'u':
                nextCaseTransform = Pcre2ReplacementCaseTransformMode.UpperNext;
                caseTransform = Pcre2ReplacementCaseTransformMode.None;
                return index + 1;
            case 'l':
                nextCaseTransform = Pcre2ReplacementCaseTransformMode.LowerNext;
                caseTransform = Pcre2ReplacementCaseTransformMode.None;
                return index + 1;
        }

        if (char.IsAsciiLetter(next))
        {
            throw new Pcre2SubstitutionException("Bad replacement escape.", Pcre2ErrorKind.BadReplacementEscape);
        }

        var literalIndex = index + 1;
        AppendReplacementLiteral(replacement, ref literalIndex, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return literalIndex;
    }

    private int AppendDollarReplacement(
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        ReadOnlySpan<byte> subject,
        int matchIndex,
        int matchLength,
        string? mark,
        ref Pcre2ReplacementCaseTransformMode nextCaseTransform,
        ref Pcre2ReplacementCaseTransformMode caseTransform,
        ref bool quoted,
        bool syntaxOnly)
    {
        if (index + 1 >= replacement.Length)
        {
            AppendReplacementChar('$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return index;
        }

        var next = replacement[index + 1];
        switch (next)
        {
            case '$':
                AppendReplacementChar('$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '&':
                AppendReplacementBytes(subject.Slice(matchIndex, matchLength), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '`':
                AppendReplacementBytes(subject.Slice(0, matchIndex), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '\'':
                AppendReplacementBytes(subject.Slice(matchIndex + matchLength, subject.Length - matchIndex - matchLength), ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '_':
                AppendReplacementBytes(subject, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '+':
                if (index + 2 < replacement.Length && replacement[index + 2] == '+')
                {
                    throw new Pcre2SubstitutionException("Invalid replacement reference.", Pcre2ErrorKind.InvalidReplacementReference);
                }

                AppendResolvedPlusReference(ResolveLastCapture(), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                return index + 1;
            case '{':
                return AppendBracedReplacement(
                    replacement,
                    index,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
                    mark,
                    ref nextCaseTransform,
                    ref caseTransform,
                    ref quoted,
                    syntaxOnly);
            case '<' when (substitutionOptions & Pcre2SubstitutionOptions.Extended) != 0:
                return AppendAngleReplacement(replacement, index, substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }

        if (char.IsAsciiDigit(next))
        {
            var end = index + 2;
            while (end < replacement.Length && char.IsAsciiDigit(replacement[end]))
            {
                end++;
            }

            AppendResolvedReference(ResolveNumber(int.Parse(replacement[(index + 1)..end])), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return end - 1;
        }

        if (IsReplacementNameStart(next))
        {
            var end = index + 2;
            while (end < replacement.Length && IsReplacementNamePart(replacement[end]))
            {
                end++;
            }

            AppendResolvedReference(ResolveName(replacement[(index + 1)..end]), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return end - 1;
        }

        AppendReplacementChar('$', ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return index;
    }

    private int AppendBracedReplacement(
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        ReadOnlySpan<byte> subject,
        int matchIndex,
        int matchLength,
        string? mark,
        ref Pcre2ReplacementCaseTransformMode nextCaseTransform,
        ref Pcre2ReplacementCaseTransformMode caseTransform,
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

                var resolution = ResolveSelector(selector, mark);
                if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                {
                    throw new Pcre2SubstitutionException("Unknown replacement group.", Pcre2ErrorKind.UnknownReplacementGroup);
                }

                var validationNextTransform = nextCaseTransform;
                var validationTransform = caseTransform;
                var validationQuoted = quoted;
                AppendEvaluatedReplacementTemplate(
                    thenBranch,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
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
                        elseBranch,
                        substitutionOptions,
                        subject,
                        matchIndex,
                        matchLength,
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
                    chosen,
                    substitutionOptions,
                    subject,
                    matchIndex,
                    matchLength,
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
                var resolution = ResolveSelector(selector, mark);
                if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) == 0)
                {
                    throw new Pcre2SubstitutionException("Unknown replacement group.", Pcre2ErrorKind.UnknownReplacementGroup);
                }

                if (resolution.Known && resolution.Success)
                {
                    AppendResolvedValue(resolution, ref nextCaseTransform, ref caseTransform, syntaxOnly);
                }
                else
                {
                    AppendEvaluatedReplacementTemplate(
                        fallback,
                        substitutionOptions,
                        subject,
                        matchIndex,
                        matchLength,
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

        AppendResolvedSelector(content, substitutionOptions, mark, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return closing;
    }

    private int AppendAngleReplacement(
        string replacement,
        int index,
        Pcre2SubstitutionOptions substitutionOptions,
        ref Pcre2ReplacementCaseTransformMode nextCaseTransform,
        ref Pcre2ReplacementCaseTransformMode caseTransform,
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

        AppendResolvedReference(ResolveName(name), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        return closing;
    }

    private void AppendResolvedSelector(
        string token,
        Pcre2SubstitutionOptions substitutionOptions,
        string? mark,
        ref Pcre2ReplacementCaseTransformMode nextCaseTransform,
        ref Pcre2ReplacementCaseTransformMode caseTransform,
        bool syntaxOnly)
    {
        if (string.Equals(token, "*MARK", StringComparison.Ordinal))
        {
            AppendResolvedReference(ResolveMarkReference(mark), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
            return;
        }

        AppendResolvedReference(ResolveSelector(token, mark), substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private Pcre2ReplacementReferenceResolution ResolveSelector(
        string token,
        string? mark)
    {
        if (string.Equals(token, "*MARK", StringComparison.Ordinal))
        {
            return ResolveMarkReference(mark);
        }

        return int.TryParse(token, out var number) ? ResolveNumber(number) : ResolveName(token);
    }

    private void AppendResolvedReference(Pcre2ReplacementReferenceResolution resolution, Pcre2SubstitutionOptions substitutionOptions, ref Pcre2ReplacementCaseTransformMode nextCaseTransform, ref Pcre2ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
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

        AppendResolvedValue(resolution, ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private void AppendResolvedPlusReference(Pcre2ReplacementReferenceResolution resolution, Pcre2SubstitutionOptions substitutionOptions, ref Pcre2ReplacementCaseTransformMode nextCaseTransform, ref Pcre2ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        if (!resolution.Known && (substitutionOptions & Pcre2SubstitutionOptions.UnknownUnset) != 0)
        {
            return;
        }

        AppendResolvedReference(resolution, substitutionOptions, ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private (string ThenBranch, string ElseBranch, bool HasElseBranch) SplitConditionalBranches(string branchPayload)
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

    private void AppendReplacementText(ReadOnlySpan<char> text, ref Pcre2ReplacementCaseTransformMode nextCaseTransform, ref Pcre2ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        for (var index = 0; index < text.Length; index++)
        {
            CheckWork();
            AppendReplacementLiteral(text, ref index, ref nextCaseTransform, ref caseTransform, syntaxOnly);
        }
    }

    private void AppendReplacementLiteral(ReadOnlySpan<char> text, ref int index, ref Pcre2ReplacementCaseTransformMode nextCaseTransform, ref Pcre2ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        var status = Rune.DecodeFromUtf16(text[index..], out var scalar, out var consumed);
        index += Math.Max(1, consumed) - 1;
        AppendReplacementScalar(status == OperationStatus.Done ? scalar : Rune.ReplacementChar,
            ref nextCaseTransform, ref caseTransform, syntaxOnly);
    }

    private void AppendReplacementChar(char value, ref Pcre2ReplacementCaseTransformMode nextCaseTransform, ref Pcre2ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
        => AppendReplacementScalar(Rune.TryCreate(value, out var scalar) ? scalar : Rune.ReplacementChar,
            ref nextCaseTransform, ref caseTransform, syntaxOnly);

    private void AppendReplacementScalar(Rune value, ref Pcre2ReplacementCaseTransformMode nextCaseTransform, ref Pcre2ReplacementCaseTransformMode caseTransform, bool syntaxOnly)
    {
        var effectiveTransform = nextCaseTransform is not Pcre2ReplacementCaseTransformMode.None
            ? nextCaseTransform
            : caseTransform;
        var transformed = effectiveTransform switch
        {
            Pcre2ReplacementCaseTransformMode.UpperNext or Pcre2ReplacementCaseTransformMode.UpperUntilEnd => Rune.ToUpperInvariant(value),
            Pcre2ReplacementCaseTransformMode.LowerNext or Pcre2ReplacementCaseTransformMode.LowerUntilEnd => Rune.ToLowerInvariant(value),
            _ => value,
        };

        if (!syntaxOnly)
        {
            Span<byte> scalarBytes = stackalloc byte[4];
            AppendBytes(scalarBytes[..transformed.EncodeToUtf8(scalarBytes)]);
        }

        if (nextCaseTransform is Pcre2ReplacementCaseTransformMode.UpperNext or Pcre2ReplacementCaseTransformMode.LowerNext)
        {
            nextCaseTransform = Pcre2ReplacementCaseTransformMode.None;
        }
    }

    private static bool IsReplacementNameStart(char value)
        => char.IsAsciiLetter(value) || value == '_';

    private static bool IsReplacementNamePart(char value)
        => char.IsAsciiLetterOrDigit(value) || value == '_';

    private int FindReplacementBraceClose(string replacement, int startIndex)
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

    private readonly record struct Pcre2ReplacementReferenceResolution(bool Known, bool Success, int Start, int Length, bool IsMark);
    private enum Pcre2ReplacementCaseTransformMode { None, UpperNext, LowerNext, UpperUntilEnd, LowerUntilEnd }
}
