using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SubstringBoundarySearchTests
{
    [Fact]
    public void RejectedAnchorDoesNotSkipLaterCandidateStarts()
    {
        const string literal = "abababababababa";
        var search = new PreparedSubstringSearch(Encoding.ASCII.GetBytes(literal), ignoreCase: true);
        var input = Encoding.ASCII.GetBytes("xx" + literal);

        Assert.Equal(1, search.Count(input));
        Assert.Equal(2, search.IndexOf(input));
    }

    [Theory]
    [InlineData(9)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(15)]
    [InlineData(16)]
    [InlineData(17)]
    [InlineData(32)]
    [InlineData(33)]
    public void ProductionCountAgreesAtBorrowedOffsetsAndIncompleteTails(int length)
    {
        var literal = new string(Enumerable.Range(0, length).Select(i => i % 2 == 0 ? 'a' : 'b').ToArray());
        var search = new PreparedSubstringSearch(Encoding.ASCII.GetBytes(literal), ignoreCase: true);
        var reference = new Regex(Regex.Escape(literal), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        foreach (var prefix in new[] { 0, 2, 31, 32, 33, 63, 64, 65, 127, 128, 129 })
        foreach (var tail in new[] { 0, 1, length - 1 })
        {
            var text = new string('x', prefix) + literal.ToUpperInvariant() + "!" + literal + "!" + literal[..tail];
            var expected = reference.Count(text);
            Assert.Equal(2, expected);
            var bytes = Encoding.ASCII.GetBytes(text);
            foreach (var offset in new[] { 0, 1, 31, 63 })
            {
                // Matching bytes outside the borrowed span must be ignored.
                var storage = Enumerable.Repeat((byte)'a', offset + bytes.Length + length).ToArray();
                bytes.CopyTo(storage.AsSpan(offset));
                var input = storage.AsSpan(offset, bytes.Length);
                Assert.Equal(expected, search.Count(input));
            }
        }
    }
}
