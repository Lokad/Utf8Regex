using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Host text projection only; tests execute retained template APIs directly.
internal static class PythonReSubstitutionAssert
{
    public static (string ResultText, int ReplacementCount) Decode(Utf8PythonSubnUtf8Result result) =>
        (Encoding.UTF8.GetString(result.ResultBytes), result.ReplacementCount);
}
