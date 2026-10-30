using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Every possible match starts with a member of a returned family.
/// Complete means the family also spells the entire consumed fragment: only
/// complete fragments can acquire a following concatenated suffix. Alternation
/// retains every branch, including stopped branches. Trees remain in logical
/// subject order for both execution directions. Indexed pattern ropes avoid
/// repeated intermediate string copies; only final strings are materialized.</summary>
internal static class Utf8OwnedPrefixFamilies
{
    private readonly record struct Family(int Start, int Count, bool Complete);
    private readonly record struct Part(int Left, int Right, string? Text, int Scalar,
        int TextLength, int ScalarLength);
    private readonly record struct Frame(Utf8TreeNode Node, int Next, Family Accumulated, int BaseScalars,
        Utf8ConstructionBuffer<int>? UnionMembers);

    internal static string[]? Find(Utf8TreeNode root, RegexOptions options)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<Frame>.Create(scope.Budget, new(root, -1, default, 0, null));
        using var parts = Utf8ConstructionBuffer<Part>.Create(scope.Budget);
        using var families = Utf8ConstructionBuffer<int>.Create(scope.Budget);
        parts.Add(default); // Empty pattern fragment, shared by every empty family.
        families.Add(0);
        var empty = new Family(0, 1, true);
        var topFold = (options & RegexOptions.IgnoreCase) != 0;
        var unrepresentable = false;
        try
        {
            var unionRoot = root;
            while (Transparent(unionRoot) && unionRoot.ChildCount == 1)
            {
                scope.Budget.ChargeWork(1);
                unionRoot = unionRoot.Child(0);
            }
            var result = empty;
            var haveResult = false;
            while (frames.Count != 0)
            {
                scope.Budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                var node = frame.Node;
                if (!haveResult)
                {
                    while (Transparent(node) && node.ChildCount == 1)
                    {
                        scope.Budget.ChargeWork(1);
                        node = node.Child(0);
                    }
                    frame = frame with { Node = node };
                    var repeat = node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop;
                    if (repeat && node.M == 0 && node.N == 0)
                        result = empty;
                    else if (repeat && node.ChildCount == 1 || Composite(node))
                    {
                        if (node.ChildCount == 0) result = empty;
                        else
                        {
                            frames[index] = frame with { Next = 1,
                                Accumulated = node.Kind == Utf8TreeNodeKind.Alternate ? new(0, 0, true) : empty,
                                UnionMembers = node.Kind == Utf8TreeNodeKind.Alternate ? Utf8ConstructionBuffer<int>.Create(scope.Budget) : null };
                            frames.Push(new(node.Child(0), -1, default, frame.BaseScalars, null));
                            continue;
                        }
                    }
                    else result = Leaf(node, frame.BaseScalars);
                    frames.Pop();
                    haveResult = true;
                    continue;
                }

                if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop)
                {
                    result = Repeat(result, node.M, node.N, frame.BaseScalars);
                    frames.Pop();
                    continue;
                }
                var union = node.Kind == Utf8TreeNodeKind.Alternate;
                var accumulated = union ? AppendUnion(frame, result) : Product(frame.Accumulated, result);
                // A stopped concatenation already covers every remaining suffix.
                // A stopped alternative never permits dropping another branch.
                if (!union && !accumulated.Complete || frame.Next == node.ChildCount)
                {
                    result = union ? FinishUnion(frame, accumulated) : accumulated;
                    frames.Pop();
                    continue;
                }
                frames[index] = frame with { Next = frame.Next + 1, Accumulated = accumulated };
                var preceding = union ? frame.BaseScalars : Saturate((long)frame.BaseScalars + MaximumLength(accumulated));
                frames.Push(new(node.Child(frame.Next), -1, default, preceding, null));
                haveResult = false;
            }

            if (unrepresentable || result.Count == 0) return null;
            var minimum = unionRoot.Kind == Utf8TreeNodeKind.Alternate ? 1 : 2;
            for (var index = 0; index < result.Count; index++)
            {
                scope.Budget.ChargeWork(1);
                if (parts[families[result.Start + index]].ScalarLength < minimum) return null;
            }
            var output = scope.Budget.AllocateArray<string>(result.Count, false);
            for (var index = 0; index < result.Count; index++)
            {
                scope.Budget.ChargeWork(1);
                output[index] = Render(families[result.Start + index]);
            }
            scope.Complete();
            return output;
        }
        finally
        {
            // Frames own their union scratch. Unwind it before frame/depth and
            // scope rollback, including failures midway through final copying.
            for (var index = frames.Count - 1; index >= 0; index--) frames[index].UnionMembers?.Dispose();
        }

        Family AppendUnion(Frame frame, Family right)
        {
            var members = frame.UnionMembers ?? throw new InvalidOperationException("Union scratch is missing.");
            for (var index = 0; index < right.Count; index++)
            {
                scope.Budget.ChargeWork(1);
                members.Add(families[right.Start + index]);
            }
            return new(0, members.Count, frame.Accumulated.Complete && right.Complete);
        }

        Family FinishUnion(Frame frame, Family accumulated)
        {
            var members = frame.UnionMembers ?? throw new InvalidOperationException("Union scratch is missing.");
            var start = families.Count;
            for (var index = 0; index < members.Count; index++)
            {
                scope.Budget.ChargeWork(1);
                families.Add(members[index]);
            }
            members.Dispose();
            return new(start, accumulated.Count, accumulated.Complete);
        }

        Family One(int part)
        {
            scope.Budget.ChargeWork(1);
            var start = families.Count;
            families.Add(part);
            return new(start, 1, true);
        }

        int ScalarPart(int scalar)
        {
            scope.Budget.ChargeWork(1);
            var result = parts.Count;
            parts.Add(new(0, 0, null, scalar, new Rune(scalar).Utf16SequenceLength, 1));
            return result;
        }

        int TextPart(string text, int textLength, int scalarLength)
        {
            scope.Budget.ChargeWork(1);
            var result = parts.Count;
            parts.Add(new(0, 0, text, 0, textLength, scalarLength));
            return result;
        }

        int Join(int left, int right)
        {
            scope.Budget.ChargeWork(1);
            if (left == 0) return right;
            if (right == 0) return left;
            var scalarLength = (long)parts[left].ScalarLength + parts[right].ScalarLength;
            var textLength = (long)parts[left].TextLength + parts[right].TextLength;
            // An unrepresentable optional proof must not replace the owned VM.
            if (textLength > int.MaxValue || scalarLength > int.MaxValue) { unrepresentable = true; return 0; }
            var result = parts.Count;
            parts.Add(new(left, right, null, 0, (int)textLength, (int)scalarLength));
            return result;
        }

        Family Product(Family left, Family right)
        {
            scope.Budget.ChargeWork(1);
            if ((long)left.Count * right.Count > 16) return left with { Complete = false };
            var start = families.Count;
            for (var r = 0; r < right.Count; r++)
            for (var l = 0; l < left.Count; l++)
            {
                scope.Budget.ChargeWork(1);
                families.Add(Join(families[left.Start + l], families[right.Start + r]));
            }
            return new(start, left.Count * right.Count, left.Complete && right.Complete);
        }

        Family Union(Family left, Family right)
        {
            scope.Budget.ChargeWork(1);
            if (left.Count == 0) return right;
            var start = families.Count;
            for (var index = 0; index < left.Count; index++)
            {
                scope.Budget.ChargeWork(1);
                families.Add(families[left.Start + index]);
            }
            for (var index = 0; index < right.Count; index++)
            {
                scope.Budget.ChargeWork(1);
                families.Add(families[right.Start + index]);
            }
            return new(start, left.Count + right.Count, left.Complete && right.Complete);
        }

        int MaximumLength(Family family)
        {
            var maximum = 0;
            for (var index = 0; index < family.Count; index++)
            {
                scope.Budget.ChargeWork(1);
                maximum = Math.Max(maximum, parts[families[family.Start + index]].ScalarLength);
            }
            return maximum;
        }

        bool AsciiFiber(int value, RegexOptions lexicalOptions)
        {
            if ((uint)value >= 128) return false;
            var last = (lexicalOptions & RegexOptions.CultureInvariant) != 0 ? 0 : 2;
            for (var behavior = 0; behavior <= last; behavior++)
            {
                scope.Budget.ChargeWork(1);
                foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents((char)value, (Utf8ScalarCaseBehavior)behavior))
                {
                    scope.Budget.ChargeWork(1);
                    if (equivalent >= 128) return false;
                }
            }
            return true;
        }

        Family Literal(int value, bool fold, RegexOptions lexicalOptions)
        {
            if (!Rune.IsValid(value) || fold && !AsciiFiber(value, lexicalOptions)) return empty with { Complete = false };
            if (!fold || topFold) return One(ScalarPart(value));
            var lower = (int)AsciiSearch.FoldCase((byte)value);
            var first = One(ScalarPart(lower));
            return lower is >= 'a' and <= 'z' ? Union(first, One(ScalarPart(lower - 32))) : first;
        }

        Family Class(Utf8TreeNode node)
        {
            if (node.ScalarClass is not { } scalarClass) return empty with { Complete = false };
            var data = scalarClass.Data;
            if (data.Length < 3 || data[0] != 0 || data[2] != 0 || data[1] < 2 ||
                (data[1] & 1) != 0 || 3L + data[1] != data.Length) return empty with { Complete = false };
            var count = 0;
            for (var index = 3; index < data.Length; index += 2)
            {
                scope.Budget.ChargeWork(1);
                var low = data[index]; var high = data[index + 1];
                if (low < 0 || high > 0x110000 || low >= high) return empty with { Complete = false };
                var valid = high - low - Math.Max(0, Math.Min(high, 0xe000) - Math.Max(low, 0xd800));
                if (valid > 256 - count) return empty with { Complete = false };
                count += valid;
                if (topFold && high > 128) return empty with { Complete = false };
                if (node.MatchCaseInsensitive)
                    for (var value = low; value < high; value++)
                        if (!AsciiFiber(value, node.Options)) return empty with { Complete = false };
            }
            var start = families.Count;
            ulong seenLow = 0, seenHigh = 0;
            for (var index = 3; index < data.Length; index += 2)
            for (var value = data[index]; value < data[index + 1]; value++)
            {
                scope.Budget.ChargeWork(1);
                if (value is >= 0xd800 and < 0xe000) { value = Math.Min(data[index + 1], 0xe000) - 1; continue; }
                var member = topFold ? AsciiSearch.FoldCase((byte)value) : value;
                AddMember(member);
                if (!topFold && node.MatchCaseInsensitive)
                {
                    var lower = (int)AsciiSearch.FoldCase((byte)value);
                    AddMember(lower);
                    if (lower is >= 'a' and <= 'z') AddMember(lower - 32);
                }
            }
            var added = families.Count - start;
            return added == 0 ? empty with { Complete = false } : new(start, added, true);

            void AddMember(int value)
            {
                if (value < 128)
                {
                    var bit = 1UL << (value & 63);
                    if (value < 64) { if ((seenLow & bit) != 0) return; seenLow |= bit; }
                    else { if ((seenHigh & bit) != 0) return; seenHigh |= bit; }
                }
                families.Add(ScalarPart(value));
            }
        }

        Family Text(Utf8TreeNode node)
        {
            // A byte literal is consumed text, never an empty fragment. Until
            // this text-only proof carries byte ropes, stop at that boundary.
            if (node.LiteralUtf8 is { Length: > 0 }) return empty with { Complete = false };
            if (node.Str is not { Length: > 0 } text) return empty;
            var scalars = 0;
            for (var offset = 0; offset < text.Length; scalars++)
            {
                scope.Budget.ChargeWork(1);
                if (Rune.DecodeFromUtf16(text.AsSpan(offset), out var scalar, out var consumed) != System.Buffers.OperationStatus.Done ||
                    node.MatchCaseInsensitive && !AsciiFiber(scalar.Value, node.Options)) return empty with { Complete = false };
                offset += consumed;
            }
            if (!node.MatchCaseInsensitive || topFold) return One(TextPart(text, text.Length, scalars));
            var result = empty;
            foreach (var value in text)
            {
                scope.Budget.ChargeWork(1);
                result = Product(result, Literal(value, true, node.Options));
                if (!result.Complete) break;
            }
            return result;
        }

        int Clip(int rootPart, int maximum)
        {
            if (parts[rootPart].ScalarLength <= maximum) return rootPart;
            using var pending = Utf8ConstructionFrames<int>.Create(scope.Budget, rootPart);
            var result = 0;
            while (pending.Count != 0 && maximum > 0)
            {
                scope.Budget.ChargeWork(1);
                var id = pending[pending.Count - 1];
                pending.Pop();
                var part = parts[id];
                if (part.Left != 0 || part.Right != 0)
                {
                    pending.Push(part.Right); pending.Push(part.Left); continue;
                }
                if (part.ScalarLength > maximum && part.Text is { } text)
                {
                    var offset = 0;
                    for (var index = 0; index < maximum; index++)
                    {
                        scope.Budget.ChargeWork(1);
                        _ = Rune.DecodeFromUtf16(text.AsSpan(offset), out _, out var consumed);
                        offset += consumed;
                    }
                    id = TextPart(text, offset, maximum);
                    part = parts[id];
                }
                result = Join(result, id);
                maximum -= part.ScalarLength;
            }
            return result;
        }

        Family Repeat(Family factor, int minimum, int maximum, int preceding)
        {
            if (minimum == 0 && maximum == 0) return empty;
            if (minimum < 0 || maximum < minimum || minimum == 0) return empty with { Complete = false };
            if (!factor.Complete) return factor;
            if (MaximumLength(factor) == 0) return empty;
            var result = empty;
            var complete = true;
            for (var repeat = 0; repeat < minimum; repeat++)
            {
                scope.Budget.ChargeWork(1);
                var room = Math.Max(0, 8 - Math.Min(8, preceding) - MaximumLength(result));
                if (room == 0) return result with { Complete = false };
                var start = families.Count;
                for (var index = 0; index < factor.Count; index++)
                {
                    scope.Budget.ChargeWork(1);
                    var id = families[factor.Start + index];
                    complete &= parts[id].ScalarLength <= room;
                    families.Add(Clip(id, room));
                }
                result = Product(result, new(start, factor.Count, complete));
                if (!result.Complete) return result;
            }
            return result with { Complete = minimum == maximum };
        }

        Family Leaf(Utf8TreeNode node, int preceding) => node.Kind switch
        {
            Utf8TreeNodeKind.One => Literal(node.Ch, node.MatchCaseInsensitive, node.Options),
            Utf8TreeNodeKind.Multi => Text(node),
            Utf8TreeNodeKind.Set => Class(node),
            Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Onelazy or Utf8TreeNodeKind.Oneloopatomic =>
                node.M == 0 && node.N == 0 ? empty : Repeat(Literal(node.Ch, node.MatchCaseInsensitive, node.Options), node.M, node.N, preceding),
            Utf8TreeNodeKind.Setloop or Utf8TreeNodeKind.Setlazy or Utf8TreeNodeKind.Setloopatomic =>
                node.M == 0 && node.N == 0 ? empty : Repeat(Class(node), node.M, node.N, preceding),
            Utf8TreeNodeKind.Empty or Utf8TreeNodeKind.Beginning or Utf8TreeNodeKind.Bol or Utf8TreeNodeKind.Start or
            Utf8TreeNodeKind.End or Utf8TreeNodeKind.EndZ or Utf8TreeNodeKind.Eol or Utf8TreeNodeKind.Boundary or
            Utf8TreeNodeKind.NonBoundary or Utf8TreeNodeKind.ECMABoundary or Utf8TreeNodeKind.NonECMABoundary or
            Utf8TreeNodeKind.PositiveLookaround or Utf8TreeNodeKind.NegativeLookaround or Utf8TreeNodeKind.UpdateBumpalong => empty,
            _ => empty with { Complete = false },
        };

        string Render(int rootPart)
        {
            var chars = scope.Budget.AllocateArray<char>(parts[rootPart].TextLength, false);
            try
            {
                using var pending = Utf8ConstructionFrames<int>.Create(scope.Budget, rootPart);
                var offset = 0;
                while (pending.Count != 0)
                {
                    scope.Budget.ChargeWork(1);
                    var part = parts[pending[pending.Count - 1]];
                    pending.Pop();
                    if (part.Left != 0 || part.Right != 0)
                    {
                        pending.Push(part.Right); pending.Push(part.Left); continue;
                    }
                    scope.Budget.ChargeWork((ulong)part.TextLength);
                    if (part.Text is { } text) text.AsSpan(0, part.TextLength).CopyTo(chars.AsSpan(offset));
                    else if (part.ScalarLength != 0) _ = new Rune(part.Scalar).EncodeToUtf16(chars.AsSpan(offset));
                    offset += part.TextLength;
                }
                return scope.Budget.CopyText(chars);
            }
            finally { scope.Budget.ReleaseArray(chars, false); }
        }
    }

    private static bool Transparent(Utf8TreeNode node) => node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Atomic;
    private static bool Composite(Utf8TreeNode node) => Transparent(node) || node.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate;
    private static int Saturate(long value) => value > int.MaxValue ? int.MaxValue : (int)value;
}
