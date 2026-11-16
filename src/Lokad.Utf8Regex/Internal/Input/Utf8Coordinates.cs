namespace Lokad.Utf8Regex.Internal.Input;

internal readonly record struct Utf8BytePosition(int Value)
{
    public static Utf8BytePosition CreateChecked(Utf8ValidationResult validation, int value, string parameterName)
        => CreateChecked(validation.ByteLength, value, parameterName);

    internal static Utf8BytePosition CreateChecked(int byteLength, int value, string parameterName)
    {
        if ((uint)value > (uint)byteLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return new Utf8BytePosition(value);
    }
}

internal readonly record struct Utf16Position(int Value);
