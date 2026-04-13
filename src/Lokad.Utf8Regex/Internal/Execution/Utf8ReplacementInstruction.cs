namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8ReplacementInstruction(
    Utf8ReplacementInstructionKind Kind,
    byte[]? LiteralUtf8 = null,
    int GroupNumber = -1,
    string? GroupName = null,
    bool IsBraceEnclosed = false);
