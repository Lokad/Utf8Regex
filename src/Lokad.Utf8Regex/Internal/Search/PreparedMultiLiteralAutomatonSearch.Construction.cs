using System.Numerics;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedMultiLiteralAutomatonSearch
{
    private readonly record struct ConstructionState(int Failure, int BestOutputOrder, int BestOutputLength, int ShortestOutputLength);

    // A supplied trie was built from the same ordered literal vector.
    public static PreparedMultiLiteralAutomatonSearch Create(byte[][] literals, in PreparedMultiLiteralTrieSearch trie = default)
    {
        if (literals.Length == 0) return new([], [], 0, [], [], [], int.MaxValue, 0);
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var columns = CreateColumns(literals, budget, out var stride);
            using var states = Utf8ConstructionBuffer<ConstructionState>.Create(budget);
            using var transitions = Utf8ConstructionBuffer<int>.Create(budget);
            budget.Checkpoint();
            Span<int> emptyRow = stackalloc int[stride];
            emptyRow.Clear();
            var (shortestLength, longestLength) = trie.StateCount == 0
                ? InsertLiteralRows(literals, columns, emptyRow, states, transitions, budget)
                : (trie.ShortestLength, CopyTrieRows(trie.Nodes, columns, emptyRow, states, transitions, budget));

            // Each non-root state is queued once. The exact queue is temporary,
            // admitted before acquisition and released before final projection.
            var queue = budget.AllocateArray<int>(states.Count - 1, false);
            try
            {
                var head = 0; var tail = 0;
                budget.Checkpoint();
                for (var column = 0; column < stride; column++)
                {
                    var next = transitions[column];
                    if (next != 0) queue[tail++] = next;
                }
                while (head < tail)
                {
                    budget.Checkpoint();
                    var state = queue[head++];
                    var current = states[state];
                    var row = state * stride;
                    // The failure state is a shorter suffix. Breadth-first
                    // completion has already filled its entire row.
                    var failureRow = current.Failure * stride;
                    budget.Checkpoint();
                    for (var column = 0; column < stride; column++)
                    {
                        var index = row + column;
                        var next = transitions[index];
                        var failureTarget = transitions[failureRow + column];
                        if (next == 0)
                        {
                            transitions[index] = failureTarget;
                            continue;
                        }
                        budget.Checkpoint();
                        var failure = states[failureTarget];
                        var output = states[next] with { Failure = failureTarget };
                        if (failure.BestOutputOrder >= 0 && (output.BestOutputLength == 0 ||
                            failure.BestOutputLength > output.BestOutputLength ||
                            failure.BestOutputLength == output.BestOutputLength &&
                            (output.BestOutputOrder < 0 || failure.BestOutputOrder < output.BestOutputOrder)))
                            output = output with { BestOutputOrder = failure.BestOutputOrder, BestOutputLength = failure.BestOutputLength };
                        states[next] = output with { ShortestOutputLength = Math.Min(output.ShortestOutputLength, failure.ShortestOutputLength) };
                        queue[tail++] = next;
                    }
                }
            }
            finally { budget.ReleaseArray(queue, false); }

            var bestOrders = budget.AllocateArray<int>(states.Count, false);
            var bestLengths = budget.AllocateArray<int>(states.Count, false);
            var shortestLengths = budget.AllocateArray<int>(states.Count, false);
            budget.Checkpoint();
            for (var index = 0; index < states.Count; index++)
            {
                var state = states[index];
                bestOrders[index] = state.BestOutputOrder;
                bestLengths[index] = state.BestOutputLength;
                shortestLengths[index] = state.ShortestOutputLength;
            }
            var result = new PreparedMultiLiteralAutomatonSearch(transitions.Take(), columns, BitOperations.Log2((uint)stride),
                bestOrders, bestLengths, shortestLengths, shortestLength, longestLength);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    private static (int Shortest, int Longest) InsertLiteralRows(byte[][] literals, byte[] columns,
        ReadOnlySpan<int> emptyRow, Utf8ConstructionBuffer<ConstructionState> states,
        Utf8ConstructionBuffer<int> transitions, Utf8ConstructionBudget budget)
    {
        states.Add(new(0, -1, 0, int.MaxValue));
        transitions.AddRange(emptyRow);
        var shortestLength = int.MaxValue;
        var longestLength = 0;
        for (var order = 0; order < literals.Length; order++)
        {
            budget.Checkpoint();
            var literal = literals[order];
            shortestLength = Math.Min(shortestLength, literal.Length);
            longestLength = Math.Max(longestLength, literal.Length);
            var state = 0;
            foreach (var key in literal)
            {
                budget.Checkpoint();
                var index = state * emptyRow.Length + columns[key];
                var next = transitions[index];
                // Direct trie edges always point to non-root states.
                if (next == 0)
                {
                    next = states.Count;
                    states.Add(new(0, -1, 0, int.MaxValue));
                    transitions.AddRange(emptyRow);
                    transitions[index] = next;
                }
                state = next;
            }
            budget.Checkpoint();
            var output = states[state];
            if (output.BestOutputLength == 0 || literal.Length > output.BestOutputLength ||
                literal.Length == output.BestOutputLength && (output.BestOutputOrder < 0 || order < output.BestOutputOrder))
                output = output with { BestOutputOrder = order, BestOutputLength = literal.Length };
            states[state] = output with { ShortestOutputLength = Math.Min(output.ShortestOutputLength, literal.Length) };
        }
        return (shortestLength, longestLength);
    }

    private static int CopyTrieRows(ReadOnlySpan<PreparedMultiLiteralTrieNode> nodes, byte[] columns,
        ReadOnlySpan<int> emptyRow, Utf8ConstructionBuffer<ConstructionState> states,
        Utf8ConstructionBuffer<int> transitions, Utf8ConstructionBudget budget)
    {
        states.EnsureCapacity(nodes.Length);
        transitions.EnsureCapacity(checked(nodes.Length * emptyRow.Length));
        var longestLength = 0;
        foreach (ref readonly var node in nodes)
        {
            budget.Checkpoint();
            longestLength = Math.Max(longestLength, node.TerminalLength);
            var shortestOutput = node.TerminalOrder >= 0 ? node.TerminalLength : int.MaxValue;
            states.Add(new(0, node.TerminalOrder, node.TerminalLength, shortestOutput));
            var row = transitions.Count;
            transitions.AddRange(emptyRow);
            for (var edge = 0; edge < node.Keys.Length; edge++)
                transitions[row + columns[node.Keys[edge]]] = node.Children[edge];
        }
        return longestLength;
    }

    private static byte[] CreateColumns(byte[][] literals, Utf8ConstructionBudget budget, out int stride)
    {
        var columns = budget.AllocateArray<byte>(256, false);
        var distinct = 0;
        foreach (var literal in literals)
        {
            budget.Checkpoint();
            foreach (var value in literal)
            {
                budget.Checkpoint();
                if (columns[value] != 0) continue;
                columns[value] = 1;
                distinct++;
            }
        }

        // A byte used by no needle always returns to the root, so all such
        // bytes share one column. Round the row to a power of two so matching
        // can find a row by shifting the state. A full alphabet fits as is.
        var required = Math.Min(columns.Length, distinct + 1);
        stride = (int)BitOperations.RoundUpToPowerOf2((uint)required);
        budget.Checkpoint();
        var next = 0;
        for (var value = 0; value < columns.Length; value++)
            columns[value] = columns[value] != 0 ? (byte)next++ : checked((byte)distinct);
        return columns;
    }
}
