using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly record struct Utf8ExecutionInstruction(
    Utf8ExecutionInstructionKind Kind,
    Utf8ExecutionNodeKind NodeKind,
    RegexOptions Options,
    object Payload,
    AsciiCharClass? CharClass,
    char Ch,
    int Min,
    int Max,
    int PartnerIndex)
{
    public string Text => Payload as string ?? string.Empty;

    public Utf8ScalarClass? ScalarClass => Payload as Utf8ScalarClass;
}
