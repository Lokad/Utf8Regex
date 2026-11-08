namespace Lokad.Utf8Regex.Posix;

/// <summary>Compilation options for the invariant scalar POSIX ERE subset.</summary>
[Flags]
public enum PosixCompileOptions
{
    /// <summary>Case-sensitive scalar matching with absolute anchors; dot can match LF.</summary>
    None = 0,
    /// <summary>Anchors also recognize LF edges; dot and negated brackets exclude LF.</summary>
    Newline = 1,
}
