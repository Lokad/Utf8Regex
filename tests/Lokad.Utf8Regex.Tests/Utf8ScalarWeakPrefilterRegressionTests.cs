using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarWeakPrefilterRegressionTests
{
    [Fact]
    public void PublicWeakPrefilterRoutesKeepScalarCapturesStrictInputAndCancellation()
    {
        const string email = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        const string uri = @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?";
        foreach (var item in new[] { (email, "😀 𐐀@é.org tail", 5, 11), (uri, "😀 https://é😀/x?q=𐐀#尾 rest", 5, 27) })
        foreach (var lane in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("(?<value>" + item.Item1 + ")", RegexOptions.CultureInvariant | lane);
            var input = Encoding.UTF8.GetBytes(item.Item2);
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
            Assert.Equal((item.Item3, item.Item4), (match.IndexInBytes, match.LengthInBytes));
            Assert.Equal((item.Item3, item.Item4), (detailed.IndexInBytes, detailed.LengthInBytes));
            var capture = detailed.GetGroup("value");
            Assert.Equal((item.Item3, item.Item4), (capture.IndexInBytes, capture.LengthInBytes));
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Equal(cancelled.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token)).CancellationToken);
            Assert.Equal(1, regex.Count(input));
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
        }
        foreach (var pattern in new[] { email, uri })
        foreach (var text in new[] { "", "admin@example.org", "https://example.org/a?q=x#z", "miss / @", "a@b.co a@c.co" })
        {
            var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
            var owned = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var input = Encoding.ASCII.GetBytes(text);
            Assert.Equal(oracle.Count(text), owned.Count(input)); Assert.Equal(oracle.IsMatch(text), owned.IsMatch(input));
        }
    }
}
