namespace Lokad.Utf8Regex.Posix;

/// <summary>Compilation options for invariant scalar POSIX matching.</summary>
[Flags]
public enum PosixCompileOptions
{
    /// <summary>Case-sensitive scalar matching with absolute anchors; dot can match LF.</summary>
    None = 0,
    /// <summary>Anchors also recognize LF edges; dot and negated brackets exclude LF.</summary>
    Newline = 1,
    /// <summary>Use the invariant Unicode scalar case relation without changing POSIX selection or original byte coordinates.</summary>
    IgnoreCase = 2,
    /// <summary>Enable the supported GNU basic syntax, including single-digit backreferences.</summary>
    GnuBasic = 4,
    /// <summary>Enable single-digit backreferences in extended syntax.</summary>
    GnuExtended = 8,
}
