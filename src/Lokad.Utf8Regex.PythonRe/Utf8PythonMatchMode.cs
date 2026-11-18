namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Selects Python search, anchored match or complete-range matching.</summary>
public enum Utf8PythonMatchMode
{
    /// <summary>Searches at or after the byte start; a capture cursor advances through successive non-overlapping results.</summary>
    Search = 0,
    /// <summary>Matches only at the supplied byte start and publishes at most one result.</summary>
    Match = 1,
    /// <summary>Matches the complete selected byte range and publishes at most one result.</summary>
    FullMatch = 2,
}
