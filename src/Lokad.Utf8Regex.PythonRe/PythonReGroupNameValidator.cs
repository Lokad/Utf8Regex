using System.Text;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReGroupNameValidator
{
    public static bool IsValid(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return false;
        }

        var index = 0;
        if (!TryReadRune(name, ref index, out var first))
        {
            return false;
        }

        if (!PythonReIdentifierData.IsStart(first.Value))
        {
            return false;
        }

        while (index < name.Length)
        {
            if (!TryReadRune(name, ref index, out var rune))
            {
                return false;
            }

            if (!PythonReIdentifierData.IsContinue(rune.Value))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadRune(string text, ref int index, out Rune rune)
    {
        Utf8ConstructionScope.ChargeWork(1);
        if (!Rune.TryGetRuneAt(text, index, out rune))
        {
            return false;
        }

        index += rune.Utf16SequenceLength;
        return true;
    }

}
