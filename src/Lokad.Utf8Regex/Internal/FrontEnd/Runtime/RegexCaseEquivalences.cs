using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static partial class RegexCaseEquivalences
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static RegexCaseBehavior GetRegexBehavior(CultureInfo culture)
    {
        return culture.Name.Length == 0
            ? RegexCaseBehavior.Invariant
            : IsTurkishOrAzeri(culture.Name)
                ? RegexCaseBehavior.Turkish
                : RegexCaseBehavior.NonTurkish;

        static bool IsTurkishOrAzeri(string cultureName)
        {
            if (cultureName.Length >= 2)
            {
                Debug.Assert(cultureName[0] is >= 'a' and <= 'z');
                Debug.Assert(cultureName[1] is >= 'a' and <= 'z');

                switch (cultureName[0])
                {
                    case 't':
                        return cultureName[1] == 'r' && (cultureName.Length == 2 || cultureName[2] == '-');
                    case 'a':
                        return cultureName[1] == 'z' && (cultureName.Length == 2 || cultureName[2] == '-');
                }
            }

            return false;
        }
    }
}
