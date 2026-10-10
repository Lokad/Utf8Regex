using System.Globalization;
using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

// BCL replacement syntax belongs to the comparator, outside shipped adapters.
internal static class PythonReBenchmarkReplacementFormatting
{
    internal static string GetLiteralText(PythonReReplacementToken token)
        => Encoding.UTF8.GetString(token.RequiredUtf8);

    internal static string ToDotNetReplacementString(this PythonReReplacementPlan plan)
    {
        var text = new StringBuilder();
        foreach (var token in plan.Tokens)
            if (token.Kind == PythonReReplacementTokenKind.Literal)
                text.Append(GetLiteralText(token).Replace("$", "$$", StringComparison.Ordinal));
            else
                text.Append("${").Append(token.Number.ToString(CultureInfo.InvariantCulture)).Append('}');
        return text.ToString();
    }
}
