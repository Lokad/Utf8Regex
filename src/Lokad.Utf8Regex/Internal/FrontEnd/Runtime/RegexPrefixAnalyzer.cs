using System.Text;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

/// <summary>
/// Vendored/adapted from dotnet/runtime. Detects useful leading prefixes on runtime-shaped regex trees.
/// </summary>
internal static class RegexPrefixAnalyzer
{
    public static string[]? FindPrefixes(RegexNode node, bool ignoreCase)
    {
        const int MinPrefixLength = 2;
        const int MaxPrefixes = 16;

        List<StringBuilder> results = [new StringBuilder()];
        FindPrefixesCore(node, results, ignoreCase);

        if (results.Count > MaxPrefixes || !results.TrueForAll(sb => sb.Length >= MinPrefixLength))
        {
            return null;
        }

        var prefixes = new string[results.Count];
        for (var i = 0; i < results.Count; i++)
        {
            prefixes[i] = results[i].ToString();
        }

        return prefixes;

        static bool FindPrefixesCore(RegexNode node, List<StringBuilder> results, bool ignoreCase)
        {
            const int MaxPrefixLength = 8;
            const int MaxPrefixes = 16;

            if (!results.TrueForAll(sb => sb.Length < MaxPrefixLength) ||
                (node.Options & RegexOptions.RightToLeft) != 0 ||
                results.Count > MaxPrefixes)
            {
                return false;
            }

            while (true)
            {
                switch (node.Kind)
                {
                    case RegexNodeKind.Atomic:
                    case RegexNodeKind.Capture:
                    case RegexNodeKind.Group:
                        if (node.ChildCount == 1)
                        {
                            node = node.Child(0);
                            continue;
                        }
                        return false;

                    case RegexNodeKind.Bol:
                    case RegexNodeKind.Eol:
                    case RegexNodeKind.Boundary:
                    case RegexNodeKind.ECMABoundary:
                    case RegexNodeKind.NonBoundary:
                    case RegexNodeKind.NonECMABoundary:
                    case RegexNodeKind.Beginning:
                    case RegexNodeKind.Start:
                    case RegexNodeKind.EndZ:
                    case RegexNodeKind.End:
                    case RegexNodeKind.Empty:
                    case RegexNodeKind.UpdateBumpalong:
                    case RegexNodeKind.PositiveLookaround:
                    case RegexNodeKind.NegativeLookaround:
                        return true;

                    case RegexNodeKind.One or RegexNodeKind.Oneloop or RegexNodeKind.Onelazy or RegexNodeKind.Oneloopatomic
                        when !ignoreCase || !RegexCharClass.ParticipatesInCaseConversion(node.Ch):
                    {
                        var reps = node.Kind is RegexNodeKind.One ? 1 : Math.Min(node.M, MaxPrefixLength);
                        foreach (var sb in results)
                        {
                            sb.Append(node.Ch, reps);
                        }

                        return node.Kind is RegexNodeKind.One || reps == node.N;
                    }

                    case RegexNodeKind.Multi:
                        if (!ignoreCase)
                        {
                            foreach (var sb in results)
                            {
                                sb.Append(node.Str);
                            }
                        }
                        else
                        {
                            foreach (var c in node.Str!)
                            {
                                if (RegexCharClass.ParticipatesInCaseConversion(c))
                                {
                                    return false;
                                }

                                foreach (var sb in results)
                                {
                                    sb.Append(c);
                                }
                            }
                        }

                        return true;

                    case RegexNodeKind.Set or RegexNodeKind.Setloop or RegexNodeKind.Setlazy or RegexNodeKind.Setloopatomic
                        when !RegexCharClass.IsNegated(node.Str!):
                    {
                        if (!RegexCharClass.CanEasilyEnumerateSetContents(node.Str!))
                        {
                            return false;
                        }

                        var setChars = RegexCharClass.GetSetChars(node.Str!);
                        if (setChars.Length == 0)
                        {
                            return false;
                        }

                        var reps = node.Kind is RegexNodeKind.Set ? 1 : Math.Min(node.M, MaxPrefixLength);
                        if (!ignoreCase)
                        {
                            for (var rep = 0; rep < reps; rep++)
                            {
                                var existingCount = results.Count;
                                if (existingCount * setChars.Length > MaxPrefixes)
                                {
                                    return false;
                                }

                                foreach (var suffix in setChars.AsSpan(1))
                                {
                                    for (var existing = 0; existing < existingCount; existing++)
                                    {
                                        var newSb = new StringBuilder().Append(results[existing]);
                                        newSb.Append(suffix);
                                        results.Add(newSb);
                                    }
                                }

                                for (var existing = 0; existing < existingCount; existing++)
                                {
                                    results[existing].Append(setChars[0]);
                                }
                            }
                        }
                        else
                        {
                            if (!TryFindCaseInsensitiveAsciiPrefixCharacter(node.Str!, out var prefixChar))
                            {
                                return false;
                            }

                            foreach (var sb in results)
                            {
                                sb.Append(prefixChar, reps);
                            }
                        }

                        return node.Kind is RegexNodeKind.Set || reps == node.N;
                    }

                    case RegexNodeKind.Concatenate:
                        for (var i = 0; i < node.ChildCount; i++)
                        {
                            if (!FindPrefixesCore(node.Child(i), results, ignoreCase))
                            {
                                return false;
                            }
                        }
                        return true;

                    case RegexNodeKind.Alternate:
                    {
                        if (node.ChildCount == 0)
                        {
                            return false;
                        }

                        var currentResults = results.ToArray();
                        results.Clear();

                        foreach (var branch in node.ChildList)
                        {
                            var branchResults = new List<StringBuilder>(currentResults.Length);
                            foreach (var existing in currentResults)
                            {
                                branchResults.Add(new StringBuilder(existing.ToString()));
                            }

                            if (!FindPrefixesCore(branch, branchResults, ignoreCase))
                            {
                                return false;
                            }

                            results.AddRange(branchResults);
                            if (results.Count > MaxPrefixes)
                            {
                                return false;
                            }
                        }

                        return true;
                    }

                    default:
                        return false;
                }
            }
        }
    }

    private static bool TryFindCaseInsensitiveAsciiPrefixCharacter(string set, out char prefixChar)
    {
        prefixChar = default;
        if (!RegexCharClass.CanEasilyEnumerateSetContents(set))
        {
            return false;
        }

        foreach (var ch in RegexCharClass.GetSetChars(set))
        {
            if (ch <= 0x7F && RegexCharClass.SetContainsAsciiOrdinalIgnoreCaseCharacter(set, ch))
            {
                prefixChar = char.ToLowerInvariant(ch);
                return true;
            }
        }

        return false;
    }
}
