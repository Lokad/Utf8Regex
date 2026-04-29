namespace Lokad.Utf8Regex.PythonRe;

[Flags]
public enum PythonReCompileOptions
{
    None = 0,
    IgnoreCase = 1 << 0,
    Locale = 1 << 1,
    Multiline = 1 << 2,
    DotAll = 1 << 3,
    Verbose = 1 << 4,
    Ascii = 1 << 5,
    Unicode = 1 << 6,
}
