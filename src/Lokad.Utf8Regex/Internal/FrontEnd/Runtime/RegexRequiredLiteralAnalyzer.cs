namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal static class RegexRequiredLiteralAnalyzer
{
    public static string[]? FindBestRequiredLiteralFamily(RegexNode root)
    {
        var families = CollectRequiredLiteralFamilies(root);
        if (families.Count == 0)
        {
            return null;
        }

        string[]? bestFamily = null;
        var bestScore = -1;
        foreach (var family in families.Values)
        {
            var score = Score(family);
            if (score > bestScore)
            {
                bestFamily = family;
                bestScore = score;
            }
        }

        return bestFamily;
    }

    private static Dictionary<string, string[]> CollectRequiredLiteralFamilies(RegexNode node)
    {
        if (TryGetLiteralFamily(node, out var family))
        {
            return new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                [GetKey(family)] = family,
            };
        }

        return node.Kind switch
        {
            RegexNodeKind.Capture or RegexNodeKind.Group or RegexNodeKind.Atomic
                => node.ChildCount == 1
                    ? CollectRequiredLiteralFamilies(node.Child(0))
                    : CollectConcatenationFamilies(node),
            RegexNodeKind.Concatenate => CollectConcatenationFamilies(node),
            RegexNodeKind.Alternate => CollectAlternationFamilies(node),
            RegexNodeKind.Loop or RegexNodeKind.Lazyloop when node.M > 0 && node.ChildCount == 1
                => CollectRequiredLiteralFamilies(node.Child(0)),
            _ => [],
        };
    }

    private static Dictionary<string, string[]> CollectConcatenationFamilies(RegexNode node)
    {
        var families = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var i = 0; i < node.ChildCount; i++)
        {
            foreach (var pair in CollectRequiredLiteralFamilies(node.Child(i)))
            {
                families.TryAdd(pair.Key, pair.Value);
            }
        }

        return families;
    }

    private static Dictionary<string, string[]> CollectAlternationFamilies(RegexNode node)
    {
        Dictionary<string, string[]>? common = null;

        for (var i = 0; i < node.ChildCount; i++)
        {
            var branchFamilies = CollectRequiredLiteralFamilies(node.Child(i));
            if (branchFamilies.Count == 0)
            {
                return [];
            }

            if (common is null)
            {
                common = new Dictionary<string, string[]>(branchFamilies, StringComparer.Ordinal);
                continue;
            }

            foreach (var key in common.Keys.ToArray())
            {
                if (!branchFamilies.ContainsKey(key))
                {
                    common.Remove(key);
                }
            }

            if (common.Count == 0)
            {
                return [];
            }
        }

        return common ?? [];
    }

    private static bool TryGetLiteralFamily(RegexNode node, out string[] family)
    {
        family = [];

        if (TryGetLiteralText(node, out var literal))
        {
            family = [literal];
            return true;
        }

        if (node.Kind != RegexNodeKind.Alternate || node.ChildCount == 0)
        {
            return false;
        }

        family = new string[node.ChildCount];
        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryGetLiteralText(node.Child(i), out literal))
            {
                family = [];
                return false;
            }

            family[i] = literal;
        }

        Array.Sort(family, StringComparer.Ordinal);
        return true;
    }

    private static bool TryGetLiteralText(RegexNode node, out string literal)
    {
        switch (node.Kind)
        {
            case RegexNodeKind.One:
                literal = node.Ch.ToString();
                return true;

            case RegexNodeKind.Multi when !string.IsNullOrEmpty(node.Str):
                literal = node.Str;
                return true;

            default:
                literal = string.Empty;
                return false;
        }
    }

    private static string GetKey(string[] family)
    {
        return string.Join("\u001F", family);
    }

    private static int Score(string[] family)
    {
        var total = 0;
        foreach (var literal in family)
        {
            total += literal.Length;
        }

        return total;
    }
}
