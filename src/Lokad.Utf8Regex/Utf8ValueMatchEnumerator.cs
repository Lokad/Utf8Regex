using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex;

public ref struct Utf8ValueMatchEnumerator
{
    private Utf8OperationMatchCursor _cursor;
    private ReadOnlySpan<byte> _timeoutInput;
    private string? _timeoutPattern;
    private TimeSpan _timeout;

    internal Utf8ValueMatchEnumerator(Utf8OperationMatchCursor cursor)
    {
        _cursor = cursor;
        _timeoutInput = default;
        _timeoutPattern = null;
        _timeout = default;
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8SearchPlan searchPlan,
        byte[] literal,
        NativeExecutionKind executionKind,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, searchPlan, literal, executionKind, budget))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        string decoded,
        Regex regex,
        Utf8BoundaryMap boundaryMap)
        : this(new Utf8OperationMatchCursor(input, decoded, regex, boundaryMap))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Regex regex,
        string decoded,
        int startAt,
        Utf8BoundaryMap boundaryMap)
        : this(new Utf8OperationMatchCursor(input, regex, decoded, startAt, boundaryMap))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? executionProgram,
        AsciiSimplePatternPlan simplePatternPlan,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, executionProgram, simplePatternPlan, budget))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? executionProgram,
        Utf8SearchPlan searchPlan,
        AsciiSimplePatternPlan simplePatternPlan,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, executionProgram, searchPlan, simplePatternPlan, budget))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8SearchPlan searchPlan,
        byte[] literal,
        int literalUtf16Length,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, searchPlan, literal, literalUtf16Length, budget))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, searchPlan, budget))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8SearchPlan searchPlan,
        NativeExecutionKind executionKind,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, searchPlan, executionKind, budget))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8StructuralLinearProgram structuralLinearProgram,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, structuralLinearProgram, budget))
    {
    }

    internal Utf8ValueMatchEnumerator(
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap? boundaryMap,
        Utf8ExecutionDeadline budget)
        : this(new Utf8OperationMatchCursor(input, boundaryMap, budget))
    {
    }

    public Utf8ValueMatch Current => _cursor.Current.ToValueMatch();

    public Utf8ValueMatchEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        try
        {
            return _cursor.MoveNext();
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (_timeoutPattern is not null)
        {
            throw new RegexMatchTimeoutException(
                Encoding.UTF8.GetString(_timeoutInput),
                _timeoutPattern,
                _timeout);
        }
    }

    internal Utf8ValueMatchEnumerator WithBaseOffsets(int byteOffset, int utf16Offset)
    {
        _cursor = _cursor.WithBaseOffsets(byteOffset, utf16Offset);
        return this;
    }

    internal Utf8ValueMatchEnumerator WithTimeoutMapping(
        ReadOnlySpan<byte> input,
        string pattern,
        TimeSpan timeout)
    {
        _timeoutInput = input;
        _timeoutPattern = pattern;
        _timeout = timeout;
        return this;
    }
}
