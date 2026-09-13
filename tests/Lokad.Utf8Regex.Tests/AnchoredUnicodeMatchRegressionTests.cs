using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// PLAN.md C01: core anchored Unicode Match loses valid matches.
// `^\w$` on non-ASCII word characters selects AsciiSimplePattern with a
// trailing-anchor fixed-length search. IsMatch and Count agreed with .NET
// while Match reported no match.
public sealed class AnchoredUnicodeMatchRegressionTests
{
    [Theory]
    [InlineData(@"^\w$", "é")]
    [InlineData(@"^\w$", "\u0301")]
    [InlineData(@"^\w$", "\u203F")]
    [InlineData(@"\A(?:\w)\z", "é")]
    [InlineData(@"\A(?:\w)\z", "\u0301")]
    [InlineData(@"\A(?:\w)\z", "\u203F")]
    public void AnchoredUnicodeWordMatchAgreesWithDotNetOnAllOperations(string pattern, string input)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var expected = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var actual = new Utf8Regex(pattern, options);
            var utf8 = Encoding.UTF8.GetBytes(input);

            var expectedMatch = expected.Match(input);
            var actualMatch = actual.Match(utf8);
            var actualDetailed = actual.MatchDetailed(utf8);

            Assert.Equal(expected.IsMatch(input), actual.IsMatch(utf8));
            Assert.Equal(expected.Count(input), actual.Count(utf8));
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            Assert.Equal(expectedMatch.Success, actualDetailed.Success);
            if (expectedMatch.Success)
            {
                Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);
                Assert.Equal(expectedMatch.Index, actualDetailed.IndexInUtf16);
                Assert.Equal(expectedMatch.Length, actualDetailed.LengthInUtf16);
                Assert.True(actualMatch.TryGetByteRange(out var byteIndex, out var byteLength));
                Assert.Equal(0, byteIndex);
                Assert.Equal(utf8.Length, byteLength);
            }

            var expectedValues = expected.Matches(input).Select(static match => match.Value).ToList();
            var actualValues = new List<string>();
            foreach (var match in actual.EnumerateMatches(utf8))
            {
                actualValues.Add(Encoding.UTF8.GetString(utf8.AsSpan(match.IndexInBytes, match.LengthInBytes)));
            }
            Assert.Equal(expectedValues, actualValues);

            var actualOffsetValues = new List<string>();
            foreach (var match in actual.EnumerateMatchesFromUtf16Offset(utf8, 0))
            {
                actualOffsetValues.Add(Encoding.UTF8.GetString(utf8.AsSpan(match.IndexInBytes, match.LengthInBytes)));
            }
            Assert.Equal(expectedValues, actualOffsetValues);
        }
    }
}
