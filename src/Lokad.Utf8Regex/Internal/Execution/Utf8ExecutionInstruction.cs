namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8ExecutionInstruction(
    Utf8ExecutionInstructionKind Kind,
    Utf8ExecutionNodeKind NodeKind,
    RegexOptions Options,
    string Text,
    char Ch,
    int CaptureNumber,
    int Min,
    int Max,
    int PartnerIndex);
