namespace Lokad.Utf8Regex.Internal.Execution;

// R05: maps owned-tree capture numbers to BCL group numbers for detailed shaping.
// Unnamed captures number identically in both worlds (opening order), but nested named
// captures diverge: the owned front end numbers them by closing order while BCL numbers
// all unnamed captures first (opening order) and then all named captures (opening
// order), as probed on .NET 10 (`(?<x>ab|(?<y>c))d` owns x=2,y=1 while BCL owns x=1,y=2).
// The map is computed purely from the owned tree in opening (pre-order) order, so no
// fallback engine is involved. Explicit-numbered (`(?<2>..)`) and balancing (`(?<a-b>..)`)
// captures decline the whole shape: their slot sharing is outside this model, and any
// other numbering surprise (duplicate numbers, unnamed mismatch) declines as well.
internal static class Utf8BclSlotTranslator
{
    public static Dictionary<int, int>? TryCreateMap(Utf8ExecutionNode body)
    {
        var opener = new List<(int Number, string? Name)>();
        CollectCaptures(body, opener);

        var unnamedCount = 0;
        foreach (var (number, name) in opener)
        {
            if (name is null)
            {
                unnamedCount++;
            }
            else if (IsUnshapableName(name))
            {
                return null;
            }
        }

        var map = new Dictionary<int, int>(opener.Count);
        var unnamedRank = 0;
        var namedRank = 0;
        foreach (var (number, name) in opener)
        {
            if (map.ContainsKey(number))
            {
                return null;
            }

            if (name is null)
            {
                unnamedRank++;
                if (number != unnamedRank)
                {
                    return null;
                }

                map[number] = number;
            }
            else
            {
                namedRank++;
                map[number] = unnamedCount + namedRank;
            }
        }

        return map;
    }

    private static bool IsUnshapableName(string name)
    {
        if (name.IndexOf('-') >= 0)
        {
            return true;
        }

        for (var i = 0; i < name.Length; i++)
        {
            if (!char.IsDigit(name[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static void CollectCaptures(Utf8ExecutionNode node, List<(int Number, string? Name)> opener)
    {
        var pending = new Stack<Utf8ExecutionNode>();
        pending.Push(node);
        while (pending.TryPop(out var current))
        {
            if (current.Kind == Utf8ExecutionNodeKind.Capture && current.CaptureNumber > 0)
                opener.Add((current.CaptureNumber, string.IsNullOrEmpty(current.Text) ? null : current.Text));
            for (var index = current.Children.Count - 1; index >= 0; index--)
                pending.Push(current.Children[index]);
        }
    }
}
