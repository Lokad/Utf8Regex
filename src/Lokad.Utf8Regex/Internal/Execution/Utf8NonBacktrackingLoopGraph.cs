using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Lexical context of a nullable unbounded iteration in the temporary
/// graph. An atom advances all active contexts. Their progress bits therefore
/// form a consumed prefix, represented by its depth rather than a bit vector.</summary>
internal sealed class Utf8NonBacktrackingLoopScope
{
    internal Utf8NonBacktrackingLoopScope(Utf8NonBacktrackingLoopScope? parent, int fork)
    {
        Depth = (parent?.Depth ?? 0) + 1;
        Fork = fork;
    }

    internal int Depth { get; }
    internal int Fork { get; }
    internal int End { get; set; } = -1;
}

/// <summary>Lifts a regular graph into progress states. A completed empty
/// iteration exits; a consuming one repeats. The scalar runner can then use
/// ordinary instruction identity, and the epsilon graph is acyclic. Shared
/// nested bodies avoid the former repeated whole-body duplication.</summary>
internal static class Utf8NonBacktrackingLoopGraph
{
    internal static Utf8SliceInstruction[] Lift(Utf8SliceInstruction[] instructions,
        Utf8ConstructionBuffer<Utf8NonBacktrackingLoopScope?> scopes, Utf8ConstructionBudget budget)
    {
        using var states = new Utf8ConstructionBuffer<(int Pc, int ConsumedDepth)>(budget);
        using var addresses = new Utf8ConstructionBuffer<int>(budget);
        using var lookup = new Utf8ConstructionMap<(int Pc, int ConsumedDepth), int>(budget, EqualityComparer<(int, int)>.Default);
        var instructionsNeeded = 0;
        Add(0, 0);
        // Discovery computes every final block width and target before allocating
        // the final array. Edges into a child retain the parent's consumed prefix;
        // edges out drop inactive contexts. Only atoms increase that prefix.
        for (var index = 0; index < states.Count; index++)
        {
            var (pc, consumed) = states[index];
            var instruction = instructions[pc];
            var scope = scopes[pc];
            if (scope is not null && pc == scope.End)
            {
                Add(consumed == scope.Depth ? scope.Fork : instructions[scope.Fork].A, consumed);
                continue;
            }
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Accept:
                case Utf8SliceInstructionKind.Fail:
                    break;
                case Utf8SliceInstructionKind.Jump:
                    Add(instruction.A, consumed);
                    break;
                case Utf8SliceInstructionKind.Fork:
                    Add(pc + 1, consumed);
                    Add(instruction.A, consumed);
                    break;
                default:
                    Add(pc + 1, IsAtom(instruction.Kind) ? scope?.Depth ?? 0 : consumed);
                    break;
            }
        }
        var result = budget.AllocateArray<Utf8SliceInstruction>(instructionsNeeded, false);
        for (var index = 0; index < states.Count; index++)
        {
            var (pc, consumed) = states[index];
            var instruction = instructions[pc];
            var scope = scopes[pc];
            var address = addresses[index];
            if (scope is not null && pc == scope.End)
            {
                var target = consumed == scope.Depth ? scope.Fork : instructions[scope.Fork].A;
                result[address] = new(Utf8SliceInstructionKind.Jump, Address(target, consumed), 0, 0, 0);
                continue;
            }
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Accept:
                case Utf8SliceInstructionKind.Fail:
                    result[address] = instruction;
                    break;
                case Utf8SliceInstructionKind.Jump:
                    result[address] = new(instruction.Kind, Address(instruction.A, consumed), 0, 0, 0);
                    break;
                case Utf8SliceInstructionKind.Fork:
                    result[address] = new(instruction.Kind, Address(instruction.A, consumed), instruction.B, 0, 0);
                    result[address + 1] = new(Utf8SliceInstructionKind.Jump, Address(pc + 1, consumed), 0, 0, 0);
                    break;
                default:
                    result[address] = instruction;
                    var nextConsumed = IsAtom(instruction.Kind) ? scope?.Depth ?? 0 : consumed;
                    result[address + 1] = new(Utf8SliceInstructionKind.Jump, Address(pc + 1, nextConsumed), 0, 0, 0);
                    break;
            }
        }
        return result;

        void Add(int pc, int consumed)
        {
            var key = (pc, Math.Min(consumed, scopes[pc]?.Depth ?? 0));
            if (lookup.ContainsKey(key)) return;
            var width = instructions[pc].Kind is Utf8SliceInstructionKind.Accept or
                Utf8SliceInstructionKind.Fail or Utf8SliceInstructionKind.Jump ? 1 : 2;
            // Admission precedes discovery storage as well as the final array.
            // The source graph remains charged while its lifted copy is built.
            budget.Reserve((ulong)width);
            lookup.Add(key, states.Count);
            states.Add(key);
            addresses.Add(instructionsNeeded);
            instructionsNeeded += width;
        }

        int Address(int pc, int consumed)
            => addresses[lookup[(pc, Math.Min(consumed, scopes[pc]?.Depth ?? 0))]];

        static bool IsAtom(Utf8SliceInstructionKind kind)
            => kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or
                Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Shorthand or
                Utf8SliceInstructionKind.Category or Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid;
    }
}
