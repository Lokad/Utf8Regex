using System.Text;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Materializes scalar match results and the literal fast-path result.</summary>
public sealed partial class Utf8PythonRegex
{

    private static Utf8PythonValueMatch Utf8PythonValueMatchFromUtf8Regex(
        ReadOnlySpan<byte> input,
        Utf8ValueMatch match)
    {
        if (!match.Success)
        {
            return default;
        }

        return Utf8PythonValueMatch.Create(input, PythonReGroupData.FromUtf8Match(match));
    }

}
