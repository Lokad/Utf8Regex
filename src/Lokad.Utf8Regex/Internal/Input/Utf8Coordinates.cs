namespace Lokad.Utf8Regex.Internal.Input;

internal readonly record struct Utf8BytePosition(int Value)
{
    internal static Utf8BytePosition CreateChecked(int byteLength, int value, string parameterName)
    {
        if ((uint)value > (uint)byteLength)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return new Utf8BytePosition(value);
    }
}

