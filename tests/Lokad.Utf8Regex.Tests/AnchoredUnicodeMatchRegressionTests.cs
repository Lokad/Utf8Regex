using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// core anchored Unicode Match loses valid matches.
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
            var actualDetailed = Utf8MatchSequence.First(actual.CreateMatchCursor(utf8));

            Assert.Equal(expected.IsMatch(input), actual.IsMatch(utf8));
            Assert.Equal(expected.Count(input), actual.Count(utf8));
            Assert.Equal(expectedMatch.Success, actualMatch.Success);
            Utf8MatchSequence.AssertCaptures(expectedMatch, actualDetailed, utf8, input);
            if (expectedMatch.Success)
            {
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(0, expectedMatch.Index)), actualMatch.StartOffsetInBytes);
                Assert.Equal(System.Text.Encoding.UTF8.GetByteCount(input.AsSpan(expectedMatch.Index, expectedMatch.Length)), (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes));
                var byteIndex = actualMatch.StartOffsetInBytes;
                var byteLength = (actualMatch.EndOffsetInBytes - actualMatch.StartOffsetInBytes);
                Assert.True(actualMatch.Success);
                Assert.Equal(0, byteIndex);
                Assert.Equal(utf8.Length, byteLength);
            }

            var expectedValues = expected.Matches(input).Select(static match => match.Value).ToList();
            var actualValues = new List<string>();
            foreach (var match in actual.EnumerateMatches(utf8))
            {
                actualValues.Add(Encoding.UTF8.GetString(utf8.AsSpan(match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))));
            }
            Assert.Equal(expectedValues, actualValues);

            var actualOffsetValues = new List<string>();
            foreach (var match in actual.EnumerateMatches(utf8, 0))
            {
                actualOffsetValues.Add(Encoding.UTF8.GetString(utf8.AsSpan(match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes))));
            }
            Assert.Equal(expectedValues, actualOffsetValues);
        }
    }
}
