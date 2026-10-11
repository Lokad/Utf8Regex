using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

// Complete library-owned execution IR, including physical instruction/child
// arrays, inline capture layout and canonical scalar class payloads. Prepared
// search plans, runtime helpers and opaque CLR graphs have separate ownership.
internal static class Utf8ExecutionRetainedStorage
{
    internal static void Collect<TControl>(Utf8ExecutionTree? tree, Utf8ExecutionProgram? program,
        Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        using var pending = Utf8ConstructionBuffer<object>.Create(Utf8ConstructionScope.CurrentBudget);
        if (tree is not null) pending.Push(tree);
        if (program is not null) pending.Push(program);
        while (!storage.Exceeded && pending.TryPop(out var value))
        {
            switch (value)
            {
                case Utf8ExecutionTree executionTree:
                    if (storage.TryAdd(executionTree, Utf8ExecutionTree.ObjectBytes)) pending.Push(executionTree.Root);
                    break;
                case Utf8ExecutionNode node:
                    if (!storage.TryAdd(node, Utf8ExecutionNode.ObjectBytes)) break;
                    if (node.Children is not Utf8ExecutionNode[] children)
                        throw new InvalidOperationException("Execution children require a qualified physical array.");
                    if (storage.TryAdd(children, Utf8ConstructionBudget.ArrayBytes<Utf8ExecutionNode>(children.Length)))
                        foreach (var child in children) { storage.Check(); pending.Push(child); }
                    pending.Push(node.Payload);
                    break;
                case Utf8ExecutionProgram body:
                    if (!storage.TryAdd(body, Utf8ExecutionProgram.ObjectBytes)) break;
                    if (body.Instructions is not Utf8ExecutionInstruction[] instructions)
                        throw new InvalidOperationException("Execution instructions require a qualified physical array.");
                    if (storage.TryAdd(instructions, Utf8ConstructionBudget.ArrayBytes<Utf8ExecutionInstruction>(instructions.Length)))
                        foreach (var instruction in instructions) { storage.Check(); pending.Push(instruction.Payload); }
                    if (body.DeterministicCaptureLayout is { } layout) storage.Array(layout.Captures);
                    break;
                case Utf8ScalarClass scalarClass:
                    if (storage.TryAdd(scalarClass, Utf8ScalarClass.ObjectBytes)) storage.Array(scalarClass.Data);
                    break;
                case string text:
                    storage.Text(text);
                    break;
                default:
                    throw new InvalidOperationException("Unqualified execution retained graph child.");
            }
        }
    }
}
