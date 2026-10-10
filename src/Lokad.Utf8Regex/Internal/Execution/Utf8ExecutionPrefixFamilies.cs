using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// Covering ASCII families, with a separate whole-fragment certificate. Indexed
// ropes avoid intermediate string copies. Stopped alternatives still participate
// and cannot acquire a following suffix. All storage belongs to construction.
internal static class Utf8ExecutionPrefixFamilies
{
    private const int MaxBranches = 32;
    private const int MaxSetChoices = 4;
    private readonly record struct Part(int Left, int Right, string? Text, int Length, char Literal);
    private readonly record struct Family(int Start, int Count, bool Complete);
    private readonly record struct Frame(Utf8ExecutionNode Node, int Next, Family Accumulated);

    internal static string[]? Find(Utf8ExecutionNode root)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(root, 0, default));
        using var parts = Utf8ConstructionBuffer<Part>.Create(budget);
        using var members = Utf8ConstructionBuffer<int>.Create(budget);
        parts.Add(default); members.Add(0);
        var empty = new Family(0, 1, true);
        var stopped = empty with { Complete = false };
        var result = empty; var ready = false;
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; var node = frame.Node;
            var wrapper = Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1;
            var repeat = node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop;
            if (ready)
            {
                if (wrapper || repeat)
                { if (repeat) result = node.Min == 0 ? stopped : result with { Complete = result.Complete && node.Min == 1 && node.Max == 1 }; frames.Pop(); continue; }
                var union = node.Kind == Utf8ExecutionNodeKind.Alternate;
                var combined = union ? Union(frame.Accumulated, result) : Product(frame.Accumulated, result);
                if (combined.Count == 0) combined = union ? stopped : frame.Accumulated with { Complete = false };
                if (!union && !combined.Complete || frame.Next == node.Children.Count)
                { result = combined; frames.Pop(); continue; }
                frame = frame with { Accumulated = combined };
            }
            if (Utf8ExecutionPrefixFacts.ZeroWidth(node) || repeat && node.Max == 0)
            { result = empty; frames.Pop(); ready = true; continue; }
            if (wrapper || repeat && node.Children.Count == 1 || node.Kind is Utf8ExecutionNodeKind.Concatenate or Utf8ExecutionNodeKind.Alternate)
            {
                if (node.Children.Count == 0) { result = stopped; frames.Pop(); ready = true; continue; }
                frames[index] = frame with { Next = frame.Next + 1, Accumulated = frame.Next == 0 ? node.Kind == Utf8ExecutionNodeKind.Alternate ? new(0, 0, true) : empty : frame.Accumulated };
                frames.Push(new(node.Children[frame.Next], 0, default)); ready = false; continue;
            }
            result = Leaf(node);
            if (repeat) result = node.Min == 0 ? stopped : result with { Complete = result.Complete && node.Min == 1 && node.Max == 1 };
            frames.Pop(); ready = true;
        }
        if (result.Count == 0) return null;
        for (var index = 0; index < result.Count; index++)
        { budget.ChargeWork(1); if (parts[members[result.Start + index]].Length == 0) return null; }
        var output = budget.AllocateArray<string>(result.Count, false);
        for (var index = 0; index < output.Length; index++) { budget.ChargeWork(1); output[index] = Render(members[result.Start + index]); }
        Utf8ConstructionSort.Sort(output.AsSpan(), static (left, right) => StringComparer.Ordinal.Compare(left, right), budget);
        scope.Complete();
        return output;

        Family One(int part)
        { budget.ChargeWork(1); var start = members.Count; members.Add(part); return new(start, 1, true); }
        int Literal(char value)
        { budget.ChargeWork(1); var index = parts.Count; parts.Add(new(0, 0, null, 1, value)); return index; }
        Family Choices(AsciiCharClass mask)
        {
            if (mask.Count == 0 || mask.Count > MaxSetChoices) return stopped;
            var start = members.Count;
            for (var value = 0; value < 128; value++)
            { budget.ChargeWork(1); if (mask.Contains((byte)value)) members.Add(Literal((char)value)); }
            return new(start, members.Count - start, true);
        }
        Family Leaf(Utf8ExecutionNode node)
        {
            if (node.ScalarClass is not null) return Utf8ExecutionPrefixFacts.ClassMask(node, out var mask) ? Choices(mask) : stopped;
            if (node.Kind == Utf8ExecutionNodeKind.One || node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop && node.Text.Length == 0)
                return Utf8ExecutionPrefixFacts.ScalarMask(node.Ch, node, out var mask) ? Choices(mask) : stopped;
            if (node.Kind is not (Utf8ExecutionNodeKind.Multi or Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop)) return stopped;
            budget.ChargeWork((ulong)node.Text.Length);
            if (!Utf8ExecutionPrefixFacts.Folded(node))
            {
                var length = 0;
                while (length < node.Text.Length && node.Text[length] < 128) length++;
                if (length == 0) return stopped;
                var index = parts.Count; parts.Add(new(0, 0, node.Text, length, '\0'));
                return One(index) with { Complete = length == node.Text.Length };
            }
            var result = empty;
            for (var offset = 0; offset < node.Text.Length;)
            {
                if (Rune.DecodeFromUtf16(node.Text.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done ||
                    !Utf8ExecutionPrefixFacts.ScalarMask(scalar.Value, node, out var mask)) return result with { Complete = false };
                var next = Product(result, Choices(mask));
                if (next.Count == 0) return result with { Complete = false };
                result = next; offset += consumed;
            }
            return result;
        }
        Family Product(Family left, Family right)
        {
            if (!left.Complete) return left;
            if ((long)left.Count * right.Count > MaxBranches) return default;
            var start = members.Count;
            for (var a = 0; a < left.Count; a++)
            for (var b = 0; b < right.Count; b++)
            {
                budget.ChargeWork(1);
                var l = members[left.Start + a]; var r = members[right.Start + b];
                if (l == 0) { members.Add(r); continue; }
                if (r == 0) { members.Add(l); continue; }
                var length = (long)parts[l].Length + parts[r].Length;
                if (length > int.MaxValue) return default;
                var part = parts.Count; parts.Add(new(l, r, null, (int)length, '\0')); members.Add(part);
            }
            return new(start, members.Count - start, right.Complete);
        }
        Family Union(Family left, Family right)
        {
            var start = members.Count;
            for (var index = 0; index < left.Count; index++) { budget.ChargeWork(1); members.Add(members[left.Start + index]); }
            for (var index = 0; index < right.Count; index++)
            {
                budget.ChargeWork(1); var part = members[right.Start + index]; var duplicate = false;
                for (var existing = start; existing < members.Count; existing++)
                { budget.ChargeWork(1); if (Equal(part, members[existing])) { duplicate = true; break; } }
                if (duplicate) continue;
                if (members.Count - start == MaxBranches) return default;
                members.Add(part);
            }
            return new(start, members.Count - start, left.Complete && right.Complete);
        }
        bool Equal(int left, int right)
        {
            if (left == right) return true;
            var length = parts[left].Length;
            if (length != parts[right].Length) return false;
            for (var index = 0; index < length; index++) { budget.ChargeWork(1); if (At(left, index) != At(right, index)) return false; }
            return true;
        }
        char At(int part, int offset)
        {
            while (parts[part].Left != 0 || parts[part].Right != 0)
            {
                budget.ChargeWork(1); var node = parts[part]; var leftLength = parts[node.Left].Length;
                if (offset < leftLength) part = node.Left;
                else { part = node.Right; offset -= leftLength; }
            }
            return parts[part].Text is { } text ? text[offset] : parts[part].Literal;
        }
        string Render(int part)
        {
            var node = parts[part];
            if (node.Text is { } borrowed && node.Length == borrowed.Length) return borrowed;
            using var walk = Utf8ConstructionFrames<int>.Create(budget, part);
            var characters = budget.AllocateArray<char>(node.Length, false);
            try
            {
                var offset = 0;
                while (walk.Count != 0)
                {
                    budget.ChargeWork(1); var current = parts[walk[walk.Count - 1]]; walk.Pop();
                    if (current.Left != 0 || current.Right != 0) { walk.Push(current.Right); walk.Push(current.Left); continue; }
                    if (current.Text is { } text)
                    { budget.ChargeWork((ulong)current.Length); text.AsSpan(0, current.Length).CopyTo(characters.AsSpan(offset)); offset += current.Length; }
                    else if (current.Length != 0) characters[offset++] = current.Literal;
                }
                return budget.CopyText(characters);
            }
            finally { budget.ReleaseArray(characters, false); }
        }
    }
}
