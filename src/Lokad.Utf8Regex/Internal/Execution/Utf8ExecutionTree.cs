namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8ExecutionTree
{
    public Utf8ExecutionTree(Utf8ExecutionNode root)
    {
        Root = root;
    }

    public Utf8ExecutionNode Root { get; }
}
