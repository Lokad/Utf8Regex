using System.Text.RegularExpressions;
using System.Text;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Parser hints are conservative; a missing hint keeps the owned scalar route.
// Validate the hint subset against overlapping regex semantics and validate
// public Unicode 16 behavior independently of host NLS/ICU char conversion.
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

    [Theory]
    [InlineData("\u1C89", "\u1C8A")]
    [InlineData("\u0264", "\uA7CB")]
    [InlineData("\uA7CC", "\uA7CD")]
    [InlineData("\u019B", "\uA7DC")]
    [InlineData("\uA7DA", "\uA7DB")]
    public void Unicode16MatchingUsesCompleteScalarPairs(string left, string right)
    {
        foreach (var (pattern, subject) in new[] { (left, right), (right, left) })
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase |
                (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var input = Encoding.UTF8.GetBytes(subject);
            var match = regex.Match(input);
            Assert.True(match.Success);
            Assert.Equal(input.Length, match.LengthInBytes);
            Assert.Equal(1, match.LengthInUtf16);
            Assert.Equal(1, regex.Count(input));
        }
    }

    [Theory]
    [InlineData("\uA7CE", "\uA7CF")]
    [InlineData("\uA7D2", "\uA7D3")]
    [InlineData("\uA7D4", "\uA7D5")]
    public void NewerHostCasingDoesNotChangeOwnedUnicode16Matching(string left, string right)
    {
        var flags = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
        Assert.False(new Utf8Regex(left, flags).IsMatch(Encoding.UTF8.GetBytes(right)));
        Assert.False(new Utf8Regex(right, flags).IsMatch(Encoding.UTF8.GetBytes(left)));
    }
}
