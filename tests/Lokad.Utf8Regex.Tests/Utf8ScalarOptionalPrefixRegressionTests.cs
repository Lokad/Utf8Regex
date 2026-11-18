using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarOptionalPrefixRegressionTests
{
    [Fact]
    public void OptionalPrefixSharingRequiresBacktrackingAndPublicScalarResultsRemainExact()
    {
        foreach (var pattern in new[] { "^(?:a|ab)?b$", "^(?:ab|a)?b$" })
        {
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(pattern, options: options | RegexOptions.CultureInvariant);
                foreach (var subject in new[] { "b", "ab", "abb" })
                {
                    var input = Encoding.UTF8.GetBytes(subject); Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
                    var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                    Assert.Equal((0, input.Length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((0, input.Length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
                }
            }
        }
        foreach (var pattern in new[] { "^(?:a|ab)$", "^(?:ab|a)$", "^(?:a|ab)?$", "^(?:ab|a)?$" })
        {
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(pattern, options: options | RegexOptions.CultureInvariant);
                foreach (var subject in new[] { "a", "ab" })
                {
                    var input = Encoding.UTF8.GetBytes(subject); Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
                    var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
                    Assert.Equal((0, input.Length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))); Assert.Equal((0, input.Length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
                }
            }
        }
    }
}
