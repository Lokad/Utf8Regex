namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8ExecutionNode
{
    public Utf8ExecutionNode(
        Utf8ExecutionNodeKind kind,
        RegexOptions options,
        IReadOnlyList<Utf8ExecutionNode>? children = null,
        string? text = null,
        char ch = default,
        int captureNumber = 0,
        int min = 0,
        int max = 0)
    {
        Kind = kind;
        Options = options;
        Children = children ?? [];
        Text = text;
        Ch = ch;
        CaptureNumber = captureNumber;
        Min = min;
        Max = max;
    }

    public Utf8ExecutionNodeKind Kind { get; }

    public RegexOptions Options { get; }

    public IReadOnlyList<Utf8ExecutionNode> Children { get; }

    public string? Text { get; }

    public char Ch { get; }

    public int CaptureNumber { get; }

    public int Min { get; }

    public int Max { get; }
}
