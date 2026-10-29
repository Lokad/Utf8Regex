namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReCompileValidator
{
    public static void Validate(PythonReCompileOptions options,
        PythonReErrorSource source = PythonReErrorSource.CompileOptions)
    {
        if ((options & PythonReCompileOptions.Locale) != 0)
        {
            throw new PythonRePatternException("LOCALE mode is intentionally unsupported; this profile is culture-invariant.",
                -1, source, PythonReErrorKind.UnsupportedFeature);
        }

        if ((options & (PythonReCompileOptions.Ascii | PythonReCompileOptions.Unicode)) ==
            (PythonReCompileOptions.Ascii | PythonReCompileOptions.Unicode))
        {
            throw new PythonRePatternException("ASCII and UNICODE flags are incompatible",
                -1, source, PythonReErrorKind.InvalidOptions);
        }
    }
}
