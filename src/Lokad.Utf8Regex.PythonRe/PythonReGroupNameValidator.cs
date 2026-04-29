using System.Globalization;
using System.Text;

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

        if (!(first.Value == '_' || IsIdentifierStart(first)))
        {
            return false;
        }

        while (index < name.Length)
        {
            if (!TryReadRune(name, ref index, out var rune))
            {
                return false;
            }

            if (!(rune.Value == '_' || IsIdentifierContinue(rune)))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadRune(string text, ref int index, out Rune rune)
    {
        if (!Rune.TryGetRuneAt(text, index, out rune))
        {
            return false;
        }

        index += rune.Utf16SequenceLength;
        return true;
    }

    private static bool IsIdentifierStart(Rune rune)
        => Rune.GetUnicodeCategory(rune) is
            UnicodeCategory.UppercaseLetter or
            UnicodeCategory.LowercaseLetter or
            UnicodeCategory.TitlecaseLetter or
            UnicodeCategory.ModifierLetter or
            UnicodeCategory.OtherLetter or
            UnicodeCategory.LetterNumber;

    private static bool IsIdentifierContinue(Rune rune)
        => IsIdentifierStart(rune) ||
           Rune.GetUnicodeCategory(rune) is
               UnicodeCategory.DecimalDigitNumber or
               UnicodeCategory.NonSpacingMark or
               UnicodeCategory.SpacingCombiningMark or
               UnicodeCategory.ConnectorPunctuation;
}
