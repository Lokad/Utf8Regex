namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class AsciiCharClass
{
    private readonly bool[] _matches;
    private byte[]? _positiveMatchBytes;

    public AsciiCharClass(bool[] matches, bool negated)
    {
        _matches = matches;
        Negated = negated;
    }

    public bool Negated { get; }

    public bool Contains(byte value)
    {
        var isMatch = value < 0x80 && _matches[value];
        return Negated ? !isMatch : isMatch;
    }

    public bool HasSameDefinition(AsciiCharClass other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Negated != other.Negated)
        {
            return false;
        }

        for (var i = 0; i < _matches.Length; i++)
        {
            if (_matches[i] != other._matches[i])
            {
                return false;
            }
        }

        return true;
    }

    public byte[] GetPositiveMatchBytes()
    {
        if (Negated)
        {
            return [];
        }

        if (_positiveMatchBytes is not null)
        {
            return _positiveMatchBytes;
        }

        var count = 0;
        for (var i = 0; i < _matches.Length; i++)
        {
            if (_matches[i])
            {
                count++;
            }
        }

        var values = new byte[count];
        var index = 0;
        for (var i = 0; i < _matches.Length; i++)
        {
            if (_matches[i])
            {
                values[index++] = (byte)i;
            }
        }

        _positiveMatchBytes = values;
        return values;
    }

    public AsciiCharClass ToIgnoreCaseInvariant()
    {
        var clone = (bool[])_matches.Clone();
        for (var i = 0; i < clone.Length; i++)
        {
            if (!clone[i])
            {
                continue;
            }

            clone[Internal.Utilities.AsciiSearch.FoldCase((byte)i)] = true;
            var upper = char.ToUpperInvariant((char)i);
            if (upper < clone.Length)
            {
                clone[upper] = true;
            }
        }

        return new AsciiCharClass(clone, Negated);
    }

    public string ToRuntimeCharClassString()
    {
        var runtime = new Internal.FrontEnd.Runtime.RegexCharClass
        {
            Negate = Negated,
        };

        var start = -1;
        for (var i = 0; i <= 0x7F; i++)
        {
            if (_matches[i])
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                runtime.AddRange((char)start, (char)(i - 1));
                start = -1;
            }
        }

        if (start >= 0)
        {
            runtime.AddRange((char)start, (char)0x7F);
        }

        return runtime.ToStringClass();
    }

    public bool TryGetKnownPredicateKind(out AsciiCharClassPredicateKind predicateKind)
    {
        predicateKind = AsciiCharClassPredicateKind.None;
        if (Negated)
        {
            return false;
        }

        if (MatchesExactly(static b => b is >= (byte)'0' and <= (byte)'9'))
        {
            predicateKind = AsciiCharClassPredicateKind.Digit;
            return true;
        }

        if (MatchesExactly(static b => b is >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z'))
        {
            predicateKind = AsciiCharClassPredicateKind.AsciiLetter;
            return true;
        }

        if (MatchesExactly(static b => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z'))
        {
            predicateKind = AsciiCharClassPredicateKind.AsciiLetterOrDigit;
            return true;
        }

        if (MatchesExactly(static b => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or (byte)'_'))
        {
            predicateKind = AsciiCharClassPredicateKind.AsciiLetterDigitUnderscore;
            return true;
        }

        if (MatchesExactly(static b => b is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'F' or >= (byte)'a' and <= (byte)'f'))
        {
            predicateKind = AsciiCharClassPredicateKind.AsciiHexDigit;
            return true;
        }

        return false;
    }

    private bool MatchesExactly(Func<byte, bool> predicate)
    {
        for (var i = 0; i < _matches.Length; i++)
        {
            if (_matches[i] != predicate((byte)i))
            {
                return false;
            }
        }

        return true;
    }
}

internal enum AsciiCharClassPredicateKind : byte
{
    None = 0,
    Digit = 1,
    AsciiLetter = 2,
    AsciiLetterOrDigit = 3,
    AsciiLetterDigitUnderscore = 4,
    AsciiHexDigit = 5,
}
