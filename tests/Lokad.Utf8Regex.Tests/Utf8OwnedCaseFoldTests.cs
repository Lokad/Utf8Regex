using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Tests;

// The owned fold table must reproduce the provider IgnoreCase mapping over
// the whole BMP: every folding character yields exactly the probed
// (lower, lower+1) plus (upper, upper+1) pairs, and no other character folds.
public sealed class Utf8OwnedCaseFoldTests
{
    [Fact]
    public void FoldTableMatchesProviderAcrossBmp()
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        var failures = new List<string>();
        for (var n = 0; n <= 0xFFFF; n++)
        {
            var c = (char)n;
            var folds = Utf8OwnedCaseFold.TryGetFold(c, out var lower, out var upper);
            RuntimeFrontEnd.RegexTree? tree = null;
            var threw = false;
            try
            {
                tree = RuntimeFrontEnd.RegexParser.Parse(c.ToString(), options, RuntimeFrontEnd.RegexParser.GetTargetCulture(options));
            }
            catch (RuntimeFrontEnd.RegexParseException)
            {
                threw = true;
            }

            if (threw)
            {
                if (folds)
                {
                    failures.Add(n.ToString("X4") + ": table folds but provider throws");
                }

                continue;
            }

            var wire = !threw && tree!.Root.ChildCount == 1 && tree.Root.Child(0).Kind == RuntimeFrontEnd.RegexNodeKind.Set
                ? tree.Root.Child(0).Str ?? string.Empty
                : string.Empty;
            var isFoldShape = wire.Length == 7 && wire[0] == (char)0 && wire[1] == (char)4 && wire[2] == (char)0;
            if (folds)
            {
                var expected = new string(new char[] { (char)0, (char)4, (char)0, lower, (char)(lower + 1), upper, (char)(upper + 1) });
                if (!isFoldShape || wire != expected)
                {
                    failures.Add(n.ToString("X4") + ": table folds but provider wire differs");
                }
            }
            else if (isFoldShape)
            {
                failures.Add(n.ToString("X4") + ": provider folds but table has no entry");
            }
        }

        Assert.True(failures.Count == 0, "Fold table mismatches:" + Environment.NewLine + string.Join(Environment.NewLine, failures.Take(20)));
    }
}