using System.Numerics;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedMultiLiteralAutomatonSearch
{
    private readonly record struct ConstructionState(int Failure, int BestOutputOrder, int BestOutputLength, int ShortestOutputLength);

    public static PreparedMultiLiteralAutomatonSearch Create(byte[][] literals, int knownStateCount = 0)
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
            if (knownStateCount != 0)
            {
                // The exact trie for these literals already counted the distinct
                // prefixes. Admit the complete matrix once and publish it in place.
                states.EnsureCapacity(knownStateCount);
                transitions.EnsureCapacity(checked(knownStateCount * stride));
            }
            budget.ChargeWork((ulong)stride);
            Span<int> emptyRow = stackalloc int[stride];
            emptyRow.Clear();
            states.Add(new(0, -1, 0, int.MaxValue));
            transitions.AddRange(emptyRow);
            var shortestLength = int.MaxValue;
            var longestLength = 0;
            for (var order = 0; order < literals.Length; order++)
            {
                budget.ChargeWork(1);
                var literal = literals[order];
                shortestLength = Math.Min(shortestLength, literal.Length);
                longestLength = Math.Max(longestLength, literal.Length);
                var state = 0;
                foreach (var key in literal)
                {
                    budget.ChargeWork(1);
                    var index = state * stride + columns[key];
                    var next = transitions[index];
                    // Direct trie edges always point to non-root states. A zero
                    // entry means absent; no separate transition flags are needed.
                    if (next == 0)
                    {
                        next = states.Count;
                        states.Add(new(0, -1, 0, int.MaxValue));
                        transitions.AddRange(emptyRow);
                        transitions[index] = next;
                    }
                    state = next;
                }
                budget.ChargeWork(1);
                var output = states[state];
                if (output.BestOutputLength == 0 || literal.Length > output.BestOutputLength ||
                    literal.Length == output.BestOutputLength && (output.BestOutputOrder < 0 || order < output.BestOutputOrder))
                    output = output with { BestOutputOrder = order, BestOutputLength = literal.Length };
                states[state] = output with { ShortestOutputLength = Math.Min(output.ShortestOutputLength, literal.Length) };
            }

            // Each non-root state is queued once. The exact queue is temporary,
            // admitted before acquisition and released before final projection.
            var queue = budget.AllocateArray<int>(states.Count - 1, false);
            try
            {
                var head = 0; var tail = 0;
                budget.ChargeWork((ulong)stride);
                for (var column = 0; column < stride; column++)
                {
                    var next = transitions[column];
                    if (next != 0) queue[tail++] = next;
                }
                while (head < tail)
                {
                    budget.ChargeWork(1);
                    var state = queue[head++];
                    var current = states[state];
                    budget.ChargeWork((ulong)stride);
                    for (var column = 0; column < stride; column++)
                    {
                        var index = state * stride + column;
                        var next = transitions[index];
                        if (next == 0)
                        {
                            transitions[index] = transitions[current.Failure * stride + column];
                            continue;
                        }
                        var fail = current.Failure;
                        while (fail != 0 && transitions[fail * stride + column] == 0)
                        {
                            budget.ChargeWork(1);
                            fail = states[fail].Failure;
                        }
                        budget.ChargeWork(1);
                        var failureTarget = transitions[fail * stride + column];
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
            budget.ChargeWork((ulong)states.Count);
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

    private static byte[] CreateColumns(byte[][] literals, Utf8ConstructionBudget budget, out int stride)
    {
        var columns = budget.AllocateArray<byte>(256, false);
        var distinct = 0;
        foreach (var literal in literals)
        {
            budget.ChargeWork(1);
            foreach (var value in literal)
            {
                budget.ChargeWork(1);
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
        budget.ChargeWork((ulong)columns.Length);
        var next = 0;
        for (var value = 0; value < columns.Length; value++)
            columns[value] = columns[value] != 0 ? (byte)next++ : checked((byte)distinct);
        return columns;
    }
}
