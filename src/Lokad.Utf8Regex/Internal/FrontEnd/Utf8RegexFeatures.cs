namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly record struct Utf8RegexFeatures(
    int CaptureCount,
    bool HasNamedCaptures,
    bool HasBackreferences,
    bool HasLookarounds,
    bool HasAtomicGroups,
    bool HasConditionals,
    bool HasLoops,
    bool HasAlternation);
