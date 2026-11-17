namespace Lokad.Utf8Regex.Internal.Execution;

// A byte-safe token finder can decline to the scalar program. It never validates
// the subject: all callers already require valid, unchanged UTF-8.
internal enum Utf8DirectMatchResult
{
    NeedsScalarFallback = 0,
    NoMatch = 1,
    Match = 2,
}
