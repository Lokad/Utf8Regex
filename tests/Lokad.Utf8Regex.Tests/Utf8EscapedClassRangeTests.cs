using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8EscapedClassRangeTests
{
    [Theory]
    [InlineData(@"[\[-\]]")]
    [InlineData(@"[\x5B-\x5D]")]
    [InlineData(@"[\u005B-\u005D]")]
    [InlineData(@"[[-\]]")]
    public void LiteralEndpointSpellingsHaveOneScalarRange(string characterClass)
    {
        var pattern = "(?<hit>" + characterClass + ")";
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
        foreach (var controlled in new[] { false, true })
        {
            var options = mode | RegexOptions.CultureInvariant;
            var policy = controlled ? new Utf8ResourcePolicy
            {
                MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20,
            } : default;
            var oracle = new Regex(pattern, options);
            var textPattern = new Utf8Regex(pattern, options: options, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = policy
            });
            var bytePattern = Utf8Regex.Compile(Encoding.UTF8.GetBytes(pattern), options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Regex.InfiniteMatchTimeout,
                ResourcePolicy = policy,
                ConstructionPolicy = default,
                ConstructionTimeout = Timeout.InfiniteTimeSpan
            }, cancellationToken: default);
            using var token = new CancellationTokenSource();
            foreach (var regex in new[] { textPattern, bytePattern })
            foreach (var scalar in new[] { 'Z', '[', '\\', ']', '^', '-' })
            foreach (var embedded in new[] { false, true })
            {
                var member = scalar is '[' or '\\' or ']';
                var text = embedded ? "é" + scalar + "😀" : scalar.ToString();
                var input = Encoding.UTF8.GetBytes(text);
                var expected = oracle.Match(text);
                Assert.Equal(member, expected.Success);
                var observedToken = controlled ? token.Token : default;
                Assert.Equal(member, regex.IsMatch(input, observedToken));
                Assert.Equal(member ? 1 : 0, regex.Count(input, observedToken));
                var slots = new Utf8CaptureRange[2];
                var whole = regex.Match(input, captures: slots, cancellationToken: observedToken);
                Assert.Equal(member, whole.Success);
                Assert.Equal(whole, slots[0]);
                Assert.Equal(whole, slots[1]);
                if (member)
                {
                    var start = embedded ? 2 : 0;
                    Assert.Equal((start, start + 1), (whole.StartOffsetInBytes, whole.EndOffsetInBytes));
                }
                var values = regex.EnumerateMatches(input, cancellationToken: observedToken);
                Assert.Equal(member, values.MoveNext());
                if (member) Assert.Equal((whole.StartOffsetInBytes, whole.EndOffsetInBytes),
                    (values.Current.StartOffsetInBytes, values.Current.EndOffsetInBytes));
                Assert.False(values.MoveNext());
                using var cursor = regex.CreateMatchCursor(input, cancellationToken: observedToken);
                Assert.Equal(member, cursor.MoveNext(out var match));
                if (member)
                {
                    Assert.Equal(whole, match.Value);
                    Assert.True(match.TryGetGroup(1, out var capture));
                    Assert.Equal(whole, capture);
                }
                Assert.False(cursor.MoveNext(out _));
            }
        }
    }

    [Fact]
    public void EscapedHyphenRemainsLiteralAsLowerMemberAndMayEndARange()
    {
        var literalLower = new Utf8Regex(@"[\--0]");
        var rangeUpper = new Utf8Regex(@"[+-\-]");
        foreach (var value in new[] { '+', ',', '-', '.', '/', '0' })
        {
            var input = Encoding.UTF8.GetBytes(value.ToString());
            Assert.Equal(value is '-' or '0', literalLower.IsMatch(input));
            Assert.Equal(value is '+' or ',' or '-', rangeUpper.IsMatch(input));
        }
    }

    [Theory]
    [InlineData(@"[\.-0]", '.', '0')]
    [InlineData(@"[\^-a]", '^', 'a')]
    [InlineData(@"[\(-\)]", '(', ')')]
    public void OtherEscapedPunctuationUsesItsDecodedScalar(string pattern, char lower, char upper)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern);
        for (var value = 0; value < 128; value++)
        {
            var text = ((char)value).ToString();
            var expected = value >= lower && value <= upper;
            Assert.Equal(expected, oracle.IsMatch(text));
            Assert.Equal(expected, regex.IsMatch(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void OriginalCancellationPrecedesRangeMatchingAndCaptureWrites()
    {
        var regex = new Utf8Regex(@"([\[-\]])");
        using var source = new CancellationTokenSource(); source.Cancel();
        var sentinel = new Utf8CaptureRange(17, 18);
        var slots = new[] { sentinel, sentinel };
        var failure = Assert.Throws<OperationCanceledException>(() =>
            regex.Match("\\"u8, captures: slots, cancellationToken: source.Token));
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.Equal(new[] { sentinel, sentinel }, slots);
    }
}
