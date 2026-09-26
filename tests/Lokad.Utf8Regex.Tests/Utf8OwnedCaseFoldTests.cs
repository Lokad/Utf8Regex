using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// The owned fold table must reproduce BCL invariant IgnoreCase folding over
// the whole BMP: every table entry matches its partners through the BCL engine,
// and every character with a distinct invariant case variant folds.
public sealed class Utf8OwnedCaseFoldTests
{
    [Fact]
    public void FoldTableMatchesBclCaseFolding()
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        var failures = new System.Collections.Generic.List<string>();
        for (var n = 0; n <= 0xFFFF; n++)
        {
            var c = (char)n;
            if (!Utf8OwnedCaseFold.TryGetFold(c, out var lower, out var upper))
            {
                continue;
            }

            var oracle = new Regex(Regex.Escape(c.ToString()), options);
            if (!oracle.IsMatch(lower.ToString()))
            {
                failures.Add(n.ToString("X4") + ": BCL misses table lower partner");
            }

            if (!oracle.IsMatch(upper.ToString()))
            {
                failures.Add(n.ToString("X4") + ": BCL misses table upper partner");
            }
        }

        Assert.True(failures.Count == 0, "Fold table mismatches:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, failures.Take(20)));
    }

    [Fact]
    public void FoldTableCoversCaseVariants()
    {
        // Twenty-two symbols have distinct invariant case variants yet match only
        // themselves through BCL (machine-probed matrices): they stay unfolded.
        var intentionallyUnfolded = new System.Collections.Generic.HashSet<int>(new[] { 0xB5, 0x17F, 0x345, 0x3C2, 0x3D0, 0x3D1, 0x3D5, 0x3D6, 0x3F0, 0x3F1, 0x3F5, 0x1C80, 0x1C81, 0x1C82, 0x1C83, 0x1C84, 0x1C85, 0x1C86, 0x1C87, 0x1C88, 0x1E9B, 0x1FBE });
        var missing = new System.Collections.Generic.List<string>();
        for (var n = 0; n <= 0xFFFF; n++)
        {
            var c = (char)n;
            if ((char.ToLowerInvariant(c) != c || char.ToUpperInvariant(c) != c) && !intentionallyUnfolded.Contains(n))
            {
                if (!Utf8OwnedCaseFold.TryGetFold(c, out _, out _))
                {
                    missing.Add(n.ToString("X4"));
                }
            }
        }

        Assert.True(missing.Count == 0, "Unfolded case variants:" + System.Environment.NewLine + string.Join(System.Environment.NewLine, missing.Take(20)));
    }
}
