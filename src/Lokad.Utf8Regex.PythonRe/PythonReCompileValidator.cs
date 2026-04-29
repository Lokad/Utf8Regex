namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReCompileValidator
{
    public static void Validate(string pattern, PythonReCompileOptions options)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        if ((options & PythonReCompileOptions.Locale) != 0)
        {
            throw new PythonRePatternException("LOCALE mode is intentionally unsupported; this profile is culture-invariant.");
        }

    }
}
