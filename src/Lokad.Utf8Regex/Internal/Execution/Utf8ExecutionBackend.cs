namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8ExecutionBackend : byte
{
    FallbackRegex = 0,
    NativeLiteral = 1,
    NativeSimplePattern = 2,
}
