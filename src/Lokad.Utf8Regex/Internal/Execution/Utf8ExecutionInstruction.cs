namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8ExecutionInstruction(
    Utf8ExecutionInstructionKind Kind,
    Utf8ExecutionNodeKind NodeKind,
    RegexOptions Options,
    string? Text = null,
    char Ch = default,
    int CaptureNumber = 0,
    int Min = 0,
    int Max = 0,
    int PartnerIndex = -1);
