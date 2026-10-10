using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8ExecutionNode
{
    public Utf8ExecutionNode(
        Utf8ExecutionNodeKind kind,
        RegexOptions options,
        IReadOnlyList<Utf8ExecutionNode> children,
        object payload,
        char ch,
        int captureNumber,
        int min,
        int max)
    {
        Kind = kind;
        Options = options;
        Children = children;
        Payload = payload;
        Ch = ch;
        CaptureNumber = captureNumber;
        Min = min;
        Max = max;
    }

    public Utf8ExecutionNodeKind Kind { get; }

    public RegexOptions Options { get; }

    public IReadOnlyList<Utf8ExecutionNode> Children { get; }

    // Literal text and canonical scalar classes borrow one payload slot. A class
    // never exposes its temporary wire projection through the literal getter.
    public object Payload { get; }

    public string Text => Payload as string ?? string.Empty;

    public Utf8ScalarClass? ScalarClass => Payload as Utf8ScalarClass;

    public char Ch { get; }

    public int CaptureNumber { get; }

    public int Min { get; }

    public int Max { get; }
}
