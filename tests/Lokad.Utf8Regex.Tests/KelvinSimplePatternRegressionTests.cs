using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// The static Kelvin fallback fenced only literal execution kinds, so anchored
// simple-pattern routes missed U+212A under IgnoreCase while .NET matches it.
public sealed class KelvinSimplePatternRegressionTests
{
    [Theory]
    [InlineData(@"\A(?:k)\z")]
    [InlineData(@"\A(?:[k])\z")]
    [InlineData(@"\A(?:[a-z])\z")]
    [InlineData(@"^(?:k)$")]
    [InlineData(@"^k$")]
    [InlineData(@"k")]
    public void AnchoredSimplePatternMatchesKelvinLikeDotNet(string pattern)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var expected = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var actual = new Utf8Regex(pattern, options);
            var utf8 = Encoding.UTF8.GetBytes("\u212A");

            Assert.Equal(expected.IsMatch("\u212A"), actual.IsMatch(utf8));
            Assert.Equal(expected.Count("\u212A"), actual.Count(utf8));

            var expectedMatch = expected.Match("\u212A");
            var actualMatch = actual.Match(utf8);
            Assert.Equal(expectedMatch.Success, actualMatch.Success);

            var expectedValues = expected.Matches("\u212A").Select(static match => match.Value).ToList();
            var actualValues = new List<string>();
            foreach (var match in actual.EnumerateMatches(utf8))
            {
                actualValues.Add(Encoding.UTF8.GetString(utf8.AsSpan(match.IndexInBytes, match.LengthInBytes)));
            }
            Assert.Equal(expectedValues, actualValues);
        }
    }
}
