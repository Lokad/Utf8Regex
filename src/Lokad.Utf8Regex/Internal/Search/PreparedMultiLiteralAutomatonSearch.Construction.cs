namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedMultiLiteralAutomatonSearch
{
    private readonly record struct ConstructionState(int Failure, int BestOutputOrder, int BestOutputLength, int ShortestOutputLength);

    public static PreparedMultiLiteralAutomatonSearch Create(byte[][] literals)
    {
        if (literals.Length == 0) return new([], [], [], [], int.MaxValue, 0);
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            using var states = Utf8ConstructionBuffer<ConstructionState>.Create(budget);
            using var transitions = Utf8ConstructionBuffer<int>.Create(budget);
            budget.ChargeWork(256);
            Span<int> emptyRow = stackalloc int[256];
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
                    var index = state * 256 + key;
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
                budget.ChargeWork(256);
                for (var key = 0; key < 256; key++)
                {
                    var next = transitions[key];
                    if (next != 0) queue[tail++] = next;
                }
                while (head < tail)
                {
                    budget.ChargeWork(1);
                    var state = queue[head++];
                    var current = states[state];
                    budget.ChargeWork(256);
                    for (var key = 0; key < 256; key++)
                    {
                        var index = state * 256 + key;
                        var next = transitions[index];
                        if (next == 0)
                        {
                            transitions[index] = transitions[current.Failure * 256 + key];
                            continue;
                        }
                        var fail = current.Failure;
                        while (fail != 0 && transitions[fail * 256 + key] == 0)
                        {
                            budget.ChargeWork(1);
                            fail = states[fail].Failure;
                        }
                        budget.ChargeWork(1);
                        var failureTarget = transitions[fail * 256 + key];
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
            var result = new PreparedMultiLiteralAutomatonSearch(transitions.Take(), bestOrders, bestLengths,
                shortestLengths, shortestLength, longestLength);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
