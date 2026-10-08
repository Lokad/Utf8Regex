namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedMultiLiteralTrieSearch
{
    // Temporary value rows replace separately allocated nodes and dictionaries.
    // Each node's sibling chain is bounded by the 256-byte alphabet.
    private readonly record struct ConstructionNode(int FirstEdge, int EdgeCount, int TerminalOrder, int TerminalLength);
    private readonly record struct ConstructionEdge(byte Key, int Child, int Next);
    private readonly record struct ConstructionChild(byte Key, int Child);

    public static PreparedMultiLiteralTrieSearch Create(byte[][] literals)
    {
        // No runtime route reads the root map of an empty trie.
        if (literals.Length == 0) return new(default, [], [], int.MaxValue);
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            using var nodes = Utf8ConstructionBuffer<ConstructionNode>.Create(budget);
            using var edges = Utf8ConstructionBuffer<ConstructionEdge>.Create(budget);
            nodes.Add(new(-1, 0, -1, 0));
            var shortestLength = int.MaxValue;
            for (var order = 0; order < literals.Length; order++)
            {
                budget.ChargeWork(1);
                var literal = literals[order];
                shortestLength = Math.Min(shortestLength, literal.Length);
                var nodeIndex = 0;
                foreach (var key in literal)
                {
                    budget.ChargeWork(1);
                    var parent = nodes[nodeIndex];
                    var edgeIndex = parent.FirstEdge;
                    while (edgeIndex >= 0)
                    {
                        budget.ChargeWork(1);
                        if (edges[edgeIndex].Key == key) break;
                        edgeIndex = edges[edgeIndex].Next;
                    }
                    if (edgeIndex < 0)
                    {
                        var child = nodes.Count;
                        nodes.Add(new(-1, 0, -1, 0));
                        edgeIndex = edges.Count;
                        edges.Add(new(key, child, parent.FirstEdge));
                        nodes[nodeIndex] = parent with { FirstEdge = edgeIndex, EdgeCount = parent.EdgeCount + 1 };
                    }
                    nodeIndex = edges[edgeIndex].Child;
                }
                budget.ChargeWork(1);
                var terminal = nodes[nodeIndex];
                if (terminal.TerminalOrder < 0 || order < terminal.TerminalOrder)
                    nodes[nodeIndex] = terminal with { TerminalOrder = order, TerminalLength = literal.Length };
            }

            var flattened = budget.AllocateArray<PreparedMultiLiteralTrieNode>(nodes.Count, false);
            budget.ChargeWork(256);
            Span<ConstructionChild> ordered = stackalloc ConstructionChild[256];
            for (var index = 0; index < nodes.Count; index++)
            {
                budget.ChargeWork(1);
                var node = nodes[index];
                var keys = budget.AllocateArray<byte>(node.EdgeCount, false);
                var children = budget.AllocateArray<int>(node.EdgeCount, false);
                var count = 0;
                for (var edgeIndex = node.FirstEdge; edgeIndex >= 0; edgeIndex = edges[edgeIndex].Next)
                {
                    budget.ChargeWork(1);
                    var edge = edges[edgeIndex];
                    ordered[count++] = new(edge.Key, edge.Child);
                }
                Utf8ConstructionSort.Sort(ordered[..count], static (left, right) => left.Key.CompareTo(right.Key), budget);
                budget.ChargeWork((ulong)count);
                for (var child = 0; child < count; child++)
                {
                    keys[child] = ordered[child].Key;
                    children[child] = ordered[child].Child;
                }
                flattened[index] = new(keys, children, node.TerminalOrder, node.TerminalLength);
            }

            var root = flattened[0];
            var rootMap = budget.AllocateArray<int>(256, false);
            budget.ChargeWork(256 + (ulong)root.Keys.Length);
            Array.Fill(rootMap, -1);
            for (var index = 0; index < root.Keys.Length; index++) rootMap[root.Keys[index]] = root.Children[index];
            var result = new PreparedMultiLiteralTrieSearch(PreparedByteSearch.Create(root.Keys), rootMap, flattened, shortestLength);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
