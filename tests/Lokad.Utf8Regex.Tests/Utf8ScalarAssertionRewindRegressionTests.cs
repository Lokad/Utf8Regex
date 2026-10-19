using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarAssertionRewindRegressionTests
{
    private static readonly (string Pattern, string Input, int Length)[] Rewinding =
    [
        (@"^a*\b", "abc", 0), (@"^a+\B", "aa!", 1), (@"^a*\A", "a", 0),
        (@"(?m)^(?:a|\n)*$", "a\nb", 1), (@"^(?:a|\n)*$\b", "a\n", 1),
        (@"^a*(?:b)?\b", "aaac", 0), (@"^(?:ab)?\b", "abc", 0),
        (@"^(?:ab|cd)?\b", "abc", 0), (@"^(?:ab)?\A", "ab", 0),
        (@"(?m)^(?:\nabc)?$", "\nabcx", 0),
        (@"^(?:ab|cd)?a", "ab", 1), (@"^a*(?:bc|de)?b", "aaabc", 4),
        (@"a(?:\nb)?(?m:$)", "a\nbx", 1),
    ];
    [Fact]
    public void AssertionRewindsRequireOwnedScalarExecutionAndPublicResultsUseByteRanges()
    {
        foreach (var (pattern, inputText, length) in Rewinding)
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
                Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
                var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
                Assert.Equal((0, length), (match.IndexInBytes, match.LengthInBytes));
                Assert.Equal((0, length), (detailed.IndexInBytes, detailed.LengthInBytes));
            }
        }
    }
}
