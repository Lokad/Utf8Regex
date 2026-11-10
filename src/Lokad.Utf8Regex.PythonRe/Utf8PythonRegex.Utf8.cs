using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

// UTF-8 backend engines, split from the Python facade along the backend ownership boundary.
/// <summary>UTF-8 backend engines for <see cref="Utf8PythonRegex"/>.</summary>
public sealed partial class Utf8PythonRegex
{
    // One exact, capture-free literal has no alternative end or capture state.
    // Its complete finder can serve FullMatch after both endpoints are checked.
    private bool CanUseLiteralFullMatch => _scalarProgram.GroupCount == 0 && !_scalarProgram.IgnoreCase &&
        (_canUseZeroOffsetUtf8SearchValueFastPath ||
         _utf8Regex?.CanFindNativeLiteralForResult == true &&
         _utf8Regex.Inspection.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal);

    // Optional Core owns the equivalent prepared exact family, while the caller's
    // invocation supplies every limit. Other controlled bulk paths remain scalar.
    private bool HasPreparedExactLiteralFamily =>
        _utf8Regex?.Inspection.ExecutionKind is NativeExecutionKind.ExactUtf8Literals;

    private Utf8PythonValueMatch MatchLiteralRepeatFullValue(ReadOnlySpan<byte> input,
        int startOffsetInBytes, CancellationToken cancellationToken)
    {
        // Capture the original deadline before the bounded proof or cold fallback
        // preparation. A declined proof must not create a second invocation.
        var invocation = CreateInvocation(cancellationToken);
        // At most 60 scalar instructions survive the structural proof. This
        // fixed stack scratch, like scalar locals, needs no heap/program loan.
        Span<byte> encoded = stackalloc byte[240];
        if (!TryGetFullMatchLiteralRepeat(encoded, out var literal, out var suffix, out var minimum, out var maximum))
        {
            var program = GetScalarFullProgram(input, invocation.Budget.Deadline, cancellationToken);
            var cursor = new PythonReScalarMatchCursor(input, program, startOffsetInBytes,
                MatchTimeout, _utf8Regex, this, invocation, prepareSearch: false);
            try
            {
                var result = cursor.MatchAtCurrentPosition()
                    ? Utf8PythonValueMatch.Create(input, cursor.SnapshotValueData()) : default;
                cursor.Checkpoint();
                return result;
            }
            finally { cursor.Dispose(); }
        }
        try
        {
            var validation = Utf8Validation.Subject(input, ref invocation);
            if (invocation.TryReserveWorking(0) != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Literal repeat exceeded the working budget.");
            var control = new Utf8InvocationBufferControl(ref invocation);
            Utf8InvocationOutput.Checkpoint(control);
            // FullMatch fixes both endpoints. Removing a fixed tail leaves
            // exactly one possible repetition count, even when tail and period overlap.
            var length = input.Length - startOffsetInBytes - suffix.Length;
            var repetitions = length < 0 ? 0 : length / literal.Length;
            var matched = length >= 0 && length % literal.Length == 0 &&
                repetitions >= minimum && repetitions <= maximum &&
                input[^suffix.Length..].SequenceEqual(suffix);
            var position = startOffsetInBytes;
            var periodEnd = input.Length - suffix.Length;
            var perBlock = Math.Min(64, 2048 / literal.Length);
            while (matched && position < periodEnd)
            {
                Utf8InvocationOutput.Checkpoint(control);
                var end = position + Math.Min(perBlock, (periodEnd - position) / literal.Length) * literal.Length;
                for (; position < end; position += literal.Length)
                    if (!input.Slice(position, literal.Length).SequenceEqual(literal))
                    { matched = false; break; }
            }
            Utf8InvocationOutput.Checkpoint(control);
            if (!matched) return default;
            var startUtf16 = validation.IsAscii || startOffsetInBytes == 0 ? startOffsetInBytes
                : Utf8SliceBufferWork.CountUtf16(input[..startOffsetInBytes], ref invocation);
            var completed = Utf8PythonValueMatch.Create(input, new PythonReGroupData
            {
                Number = 0, Success = true,
                StartOffsetInBytes = startOffsetInBytes, EndOffsetInBytes = input.Length,
                StartOffsetInUtf16 = startUtf16, EndOffsetInUtf16 = validation.Utf16Length,
            });
            Utf8InvocationOutput.Checkpoint(control);
            return completed;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw invocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        { throw CreateMatchTimeoutException(input, Pattern); }
    }

    private bool TryGetFullMatchLiteralRepeat(Span<byte> encoded,
        out ReadOnlySpan<byte> literal, out ReadOnlySpan<byte> suffix, out long minimum, out long maximum)
    {
        literal = suffix = default; minimum = maximum = 0;
        var program = _scalarProgram;
        var code = program.Instructions;
        var repeat = code[0];
        if (program.IgnoreCase || program.RightToLeft || program.IsNonBacktracking || program.RepeatCount != 1 ||
            repeat.B != 0 || repeat.A < 3 || repeat.A > code.Length - 2 || repeat.RepeatMinimum < 1 ||
            repeat.RepeatMaximum < repeat.RepeatMinimum ||
            code[repeat.A - 1] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 0 } ||
            code[repeat.A] is not { Kind: Utf8SliceInstructionKind.RepeatExit, A: 0 } ||
            code[^1].Kind != Utf8SliceInstructionKind.Accept)
            return false;
        var offset = 0;
        var literalLength = 0;
        for (var index = 1; index < code.Length - 1; index++)
        {
            if (index == repeat.A - 1)
            {
                literalLength = offset;
                index++; // Skip the proved RepeatEnd and RepeatExit.
                continue;
            }
            if (code[index] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0 } atom ||
                !Rune.TryCreate(atom.A, out var scalar) ||
                !scalar.TryEncodeToUtf8(encoded[offset..], out var width))
                return false;
            offset += width;
        }
        if (literalLength == 0) return false;
        literal = encoded[..literalLength]; suffix = encoded[literalLength..offset];
        minimum = repeat.RepeatMinimum; maximum = repeat.RepeatMaximum;
        return true;
    }

    private int CountPreparedLiteral(Utf8Regex prepared, ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken)
    {
        var invocation = CreateInvocation(cancellationToken);
        try
        {
            var count = prepared.CountForResult(input, startOffsetInBytes, ref invocation);
            Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
            return count;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw invocation.Budget.CreateResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
    }

    private bool TryFindAllViaUtf8Regex(ReadOnlySpan<byte> input, int startOffsetInBytes, CancellationToken cancellationToken, out Utf8PythonMatchData[] matches)
    {
        if (!TryCollectUtf8MatchRanges(input, startOffsetInBytes, cancellationToken, out var ranges))
        {
            matches = [];
            return false;
        }

        try
        {
            var completed = ranges.AllocateResultArray<Utf8PythonMatchData>(ranges.Count);
            for (var i = 0; i < completed.Length; i++)
            {
                var range = ranges[i];
                completed[i] = new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = range.IndexInBytes,
                    EndOffsetInBytes = range.IndexInBytes + range.LengthInBytes,
                    StartOffsetInUtf16 = range.IndexInUtf16,
                    EndOffsetInUtf16 = range.IndexInUtf16 + range.LengthInUtf16,
                    ValueText = ranges.GetText(input, range),
                };
            }
            ranges.Checkpoint();
            matches = completed;
            return true;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && ranges.HasResourceFailure)
        {
            throw ranges.ResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
        finally
        {
            ranges.Dispose();
        }
    }

    private bool TryFindAllCanonicalLiteral(ReadOnlySpan<byte> input, int startOffsetInBytes,
        CancellationToken cancellationToken, out Utf8PythonFindAllResult result)
    {
        result = default;
        if (_repeatedExactFindAllUtf8 is not { } canonical || _utf8Regex is null) return false;
        var invocation = CreateInvocation(cancellationToken);
        Utf8InvocationResultLedger ledger = default;
        try
        {
            var count = _utf8Regex.CountForResult(input, startOffsetInBytes, ref invocation);
            var control = new Utf8InvocationBufferControl(ref invocation);
            var values = ledger.AllocateArray<string>(count, ref invocation);
            // Text is an explicit output projection, not retained preparation.
            // Count first so misses acquire neither canonical text nor its charge.
            var canonicalText = count == 0 ? string.Empty : Utf8SliceBufferWork.DecodeUtf8(canonical, ref invocation);
            for (var offset = 0; offset < values.Length;)
            {
                Utf8InvocationOutput.Checkpoint(control);
                var length = Math.Min(256, values.Length - offset);
                values.AsSpan(offset, length).Fill(canonicalText);
                offset += length;
            }
            Utf8InvocationOutput.Checkpoint(control);
            result = new Utf8PythonFindAllResult { Shape = Utf8PythonFindAllShape.FullMatch, ScalarValues = values, TupleValues = [] };
            return true;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw invocation.Budget.CreateResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
        finally { ledger.Release(ref invocation); }
    }

    private bool TryCollectUtf8MatchRanges(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        CancellationToken cancellationToken,
        out PythonReUtf8MatchRangeBuffer ranges)
    {
        // Generic Core enumeration rents fresh attempt scratch per value.
        // The existing Python cursor reuses its admitted workspace. Keep real
        // native enumerators, including direct families classified as fallback.
        if (_utf8Regex is null ||
            _utf8Regex.Inspection.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            _utf8Regex.Inspection.PreparedRegex.FallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.None)
        {
            ranges = default;
            return false;
        }

        ranges = default;
        var invocation = default(Utf8SliceInvocation);
        try
        {
            Utf8ValueMatchEnumerator enumerator;
            if (_programMemory is null && startOffsetInBytes == 0 && !HasFiniteExecutionLimits &&
                !cancellationToken.CanBeCanceled && MatchTimeout == Timeout.InfiniteTimeSpan)
                enumerator = _utf8Regex.EnumerateMatches(input);
            else
            {
                invocation = CreateInvocation(cancellationToken);
                enumerator = _utf8Regex.EnumerateMatchesForResult(input, startOffsetInBytes, ref invocation);
            }

            ranges = new PythonReUtf8MatchRangeBuffer(enumerator.ExportWorkingInvocation());
            while (enumerator.MoveNext())
            {
                var match = enumerator.Current;
                if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
                {
                    ranges.Dispose();
                    ranges = default;
                    return false;
                }

                ranges.Add(new PythonReUtf8MatchRange(
                    indexInBytes,
                    lengthInBytes,
                    match.IndexInUtf16,
                    match.LengthInUtf16));
            }

            return true;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && ranges.HasResourceFailure)
        {
            var failure = ranges.ResourceFailure();
            ranges.Dispose();
            ReleaseFailedCollection(ref invocation);
            ranges = default;
            throw failure;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            ranges.Dispose();
            ReleaseFailedCollection(ref invocation);
            ranges = default;
            throw CreateMatchTimeoutException(input, Pattern);
        }
        catch (RegexMatchTimeoutException) when (_programMemory is not null)
        {
            ranges.Dispose();
            ReleaseFailedCollection(ref invocation);
            ranges = default;
            // Core's enumeration wrapper uses its translated pattern. Bound
            // Python operations retain their original diagnostic authority.
            throw CreateMatchTimeoutException(input, Pattern);
        }
        catch
        {
            ranges.Dispose();
            ReleaseFailedCollection(ref invocation);
            ranges = default;
            throw;
        }
    }

    private static void ReleaseFailedCollection(ref Utf8SliceInvocation invocation)
    {
        // Enumeration setup can fail before a range buffer takes the borrowed
        // invocation. No enumerator/value alias is ever published on this path.
        if (invocation.Ledger.HasMemoryAllowance)
            invocation.Ledger.Release(invocation.Ledger.ReservedBytes);
    }

    private bool TryReplaceViaUtf8Literal(
        ReadOnlySpan<byte> input, PythonReReplacementPlan plan, int count, int startOffsetInBytes,
        bool textOutput, ref Utf8SliceInvocation invocation, out byte[] resultBytes, out string resultText, out int replacementCount,
        out Utf8PythonRetainedSubnResult? retained, bool retain = false)
    {
        resultBytes = [];
        resultText = string.Empty;
        replacementCount = 0;
        retained = null;
        if (_canMatchEmpty || count < 0 || startOffsetInBytes != 0 ||
            !_canUseZeroOffsetUtf8ValueFastPath || _utf8Regex is null ||
            !plan.TryGetLiteralUtf8(out var literalBytes)) return false;

        Utf8InvocationResultLedger results = default;
        try
        {
            var bytes = _utf8Regex.ReplaceExactLiteralForResult(input, literalBytes,
                count == 0 ? int.MaxValue : count, ref invocation, ref results, out var replaced, out var charCount);
            var text = string.Empty;
            if (textOutput)
            {
                text = results.DecodeUtf8(bytes is null ? input : bytes, charCount, ref invocation);
                if (bytes is not null) results.ReleaseArray(bytes, ref invocation);
            }
            else if (bytes is null)
            {
                bytes = results.AllocateArray<byte>(input.Length, ref invocation);
                Utf8InvocationOutput.Copy(input, bytes, new Utf8InvocationBufferControl(ref invocation));
            }
            Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
            if (textOutput) resultText = text;
            else resultBytes = bytes!;
            replacementCount = replaced;
            if (retain)
            {
                var receipt = results.AllocateObject((Result: new Utf8PythonSubnUtf8Result { ResultBytes = resultBytes, ReplacementCount = replaced },
                    Allowance: invocation.Ledger.MemoryAllowance!), static state => new Utf8PythonRetainedSubnResult(state.Result, state.Allowance), ref invocation);
                Utf8InvocationOutput.Checkpoint(new Utf8InvocationBufferControl(ref invocation));
                receipt.SetTransferredBytes(results.TransferSince(0, ref invocation));
                retained = receipt;
            }
            return true;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { throw invocation.Budget.CreateResourceFailure(); }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
        finally { results.Release(ref invocation); }
    }

    internal string? MaterializeNativeMatchText(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, ref Utf8SliceInvocation invocation, ref Utf8InvocationResultLedger results)
    {
        try
        {
            var control = new Utf8InvocationBufferControl(ref invocation);
            Utf8InvocationOutput.Checkpoint(control);
            if (!TryDiscoverNativeMatch(input, startOffsetInBytes, kind, ref invocation,
                out var start, out var end, out var validation))
            {
                Utf8InvocationOutput.Checkpoint(control);
                return null;
            }
            var bytes = input[start..end];
            var count = validation.IsAscii ? bytes.Length
                : start == 0 && end == input.Length ? validation.Utf16Length
                : Utf8SliceBufferWork.CountUtf16(bytes, ref invocation);
            var completed = results.DecodeUtf8(bytes, count, ref invocation);
            Utf8InvocationOutput.Checkpoint(control);
            return completed;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw invocation.Budget.CreateResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
    }

    internal Utf8PythonValueMatch MaterializeNativeMatchValue(ReadOnlySpan<byte> input, int startOffsetInBytes,
        int kind, scoped ref Utf8SliceInvocation invocation)
    {
        try
        {
            var control = new Utf8InvocationBufferControl(ref invocation);
            int start, end, startUtf16, endUtf16;
            if (kind == 0 && _canUseZeroOffsetUtf8SearchValueFastPath &&
                _utf8Regex is { CanFindNativeLiteralForResult: true } literal)
            {
                ValidateScalarStartOffset(input, startOffsetInBytes);
                // A miss has no interoperability coordinates. Validate the whole
                // subject before discovery, then project only an actual range.
                Utf8Validation.CheckSubject(input, ref invocation);
                if (!literal.TryFindNativeLiteralForResult(input, startOffsetInBytes,
                    ref invocation, out var found, out start, out end))
                    throw new InvalidOperationException("Prepared literal discovery declined its eligible route.");
                if (!found)
                {
                    Utf8InvocationOutput.Checkpoint(control);
                    return default;
                }
                // Whole-subject validation and complete native literals prove
                // these scalar ranges; derive coordinates without validating again.
                startUtf16 = start == 0 ? 0
                    : Utf8SliceBufferWork.CountUtf16(input[..start], ref invocation);
                endUtf16 = startUtf16 + Utf8SliceBufferWork.CountUtf16(input[start..end], ref invocation);
            }
            else
            {
                if (!TryDiscoverNativeMatch(input, startOffsetInBytes, kind, ref invocation,
                    out start, out end, out var validation))
                {
                    Utf8InvocationOutput.Checkpoint(control);
                    return default;
                }
                startUtf16 = validation.IsAscii || start == 0 ? start
                    : Utf8SliceBufferWork.CountUtf16(input[..start], ref invocation);
                endUtf16 = validation.IsAscii ? end : end == input.Length ? validation.Utf16Length
                    : startUtf16 + Utf8SliceBufferWork.CountUtf16(input[start..end], ref invocation);
            }
            Utf8InvocationOutput.Checkpoint(control);
            return Utf8PythonValueMatch.Create(input, new PythonReGroupData
            {
                Number = 0, Success = true,
                StartOffsetInBytes = start, EndOffsetInBytes = end,
                StartOffsetInUtf16 = startUtf16, EndOffsetInUtf16 = endUtf16,
            });
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw invocation.Budget.CreateResourceFailure();
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input, Pattern);
        }
    }

    private bool TryDiscoverNativeMatch(ReadOnlySpan<byte> input, int startOffsetInBytes, int kind,
        ref Utf8SliceInvocation invocation, out int start, out int end, out Utf8ValidationResult validation)
    {
        ValidateScalarStartOffset(input, startOffsetInBytes);
        var control = new Utf8InvocationBufferControl(ref invocation);
        // Each route enters full validation before its metered comparison/search.
        start = end = -1;
        validation = default;
        if (kind == 1 && _asciiLiteralPrefixDigitMatchPrefix is { } prefix)
        {
            validation = Utf8Validation.Subject(input, ref invocation);
            start = 0;
            end = PythonReAsciiPrefixDigitMatcher.MatchUnchecked(input, prefix, ref invocation);
            return end != 0;
        }
        if (kind == 2 && _asciiDotAllFullMatchPrefix is { } head && _asciiDotAllFullMatchSuffix is { } tail)
        {
            validation = Utf8Validation.Subject(input, ref invocation);
            var subject = input[startOffsetInBytes..];
            if (subject.Length < head.Length || subject.Length - head.Length < tail.Length ||
                !PythonReAsciiPrefixDigitMatcher.MatchesPrefix(subject, head, ref invocation) ||
                !PythonReAsciiPrefixDigitMatcher.MatchesPrefix(subject[(subject.Length - tail.Length)..], tail, ref invocation))
                return false;
            start = startOffsetInBytes;
            end = input.Length;
            return true;
        }
        if (_utf8Regex is null) throw new InvalidOperationException("Native result discovery requires its prepared core.");
        if (kind != 0 && _utf8Regex.Inspection.ExecutionKind is
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal)
        {
            ref readonly var search = ref _utf8Regex.Inspection.PreparedRegex.SearchPlan;
            if (!search.HasBoundaryRequirements && !search.HasTrailingLiteralRequirement &&
                search.LiteralSearch is { IgnoreCase: false, Needle: { Length: >= 1 and <= 256 } literal })
            {
                // A required start permits one bounded comparison of the complete
                // literal, within the profile's existing 256-byte comparison
                // block. Validate the whole subject before any rejection.
                validation = Utf8Validation.Subject(input, ref invocation);
                var remaining = input.Length - startOffsetInBytes;
                if (remaining < literal.Length || kind == 2 && remaining != literal.Length) return false;
                Utf8InvocationOutput.Checkpoint(control);
                if (!input.Slice(startOffsetInBytes, literal.Length).SequenceEqual(literal)) return false;
                start = startOffsetInBytes;
                end = start + literal.Length;
                return true;
            }
        }
        return _utf8Regex.FindLiteralForResult(input, startOffsetInBytes, ref invocation, out start, out end, out validation) &&
            (kind == 0 || start == startOffsetInBytes && (kind != 2 || end == input.Length));
    }
}
