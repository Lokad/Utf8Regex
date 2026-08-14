using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly record struct Utf8NativeReplacementMatch(
    int Index,
    int Length,
    Utf8CaptureSlots? Captures = null,
    int LiteralId = -1);
