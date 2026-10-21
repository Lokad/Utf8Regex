using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R07 concurrent calls: one shared instance must answer identically under
// parallel use on every route exercised here (owned literal, scalar
// loop, and replacement through the instance plan cache).
public sealed class Utf8ConcurrentSharedInstanceTests
{
    [Fact]
    public void SharedLiteralInstanceAnswersConcurrently()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("xxabcxx");
        var miss = Encoding.UTF8.GetBytes("xxabxx");

        Parallel.For(0, 256, iteration =>
        {
            var input = iteration % 2 == 0 ? hit : miss;
            var expected = iteration % 2 == 0;
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected, regex.Match(input).Success);
            Assert.Equal(expected ? 1 : 0, regex.Count(input));
        });
    }

    [Fact]
    public void SharedFallbackInstanceAnswersConcurrently()
    {
        var regex = new Utf8Regex("[a-z]+@[a-z]+", RegexOptions.CultureInvariant);
        var hit = Encoding.UTF8.GetBytes("mail a@b ok");
        var miss = Encoding.UTF8.GetBytes("no address here 123");
        var expectedReplacement = regex.Replace(hit, "Z");

        Parallel.For(0, 256, iteration =>
        {
            var input = iteration % 2 == 0 ? hit : miss;
            var expected = iteration % 2 == 0;
            Assert.Equal(expected, regex.IsMatch(input));
            var match = regex.Match(input);
            Assert.Equal(expected, match.Success);
            Assert.Equal(expected ? 1 : 0, regex.Count(input));
            if (expected)
            {
                Assert.Equal(expectedReplacement, regex.Replace(input, "Z"));
            }
        });
    }
}
