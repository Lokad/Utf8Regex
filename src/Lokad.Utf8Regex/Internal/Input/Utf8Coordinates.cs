namespace Lokad.Utf8Regex.Internal.Input;

internal readonly record struct Utf8BytePosition(int Value)
{
    public static Utf8BytePosition CreateChecked(Utf8ValidationResult validation, int value, string parameterName)
    {
        if ((uint)value > (uint)validation.ByteLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return new Utf8BytePosition(value);
    }
}

internal readonly record struct Utf16Position(int Value)
{
    public static Utf16Position CreateChecked(Utf8ValidationResult validation, int value, string parameterName)
    {
        if ((uint)value > (uint)validation.Utf16Length)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return new Utf16Position(value);
    }
}

internal readonly record struct Utf8ByteRange(Utf8BytePosition Start, Utf8BytePosition End)
{
    public int Length => End.Value - Start.Value;

    public static Utf8ByteRange CreateChecked(
        Utf8ValidationResult validation,
        int start,
        int length,
        string startParameterName,
        string lengthParameterName)
    {
        var checkedStart = Utf8BytePosition.CreateChecked(validation, start, startParameterName);
        if (length < 0 || length > validation.ByteLength - start)
        {
            throw new ArgumentOutOfRangeException(lengthParameterName);
        }

        return new Utf8ByteRange(checkedStart, new Utf8BytePosition(start + length));
    }
}

internal readonly record struct Utf16Range(Utf16Position Start, Utf16Position End)
{
    public int Length => End.Value - Start.Value;
}
