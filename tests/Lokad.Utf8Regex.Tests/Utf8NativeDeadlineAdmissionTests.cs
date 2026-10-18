using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeDeadlineAdmissionTests
{
    [Theory]
    [InlineData(@"^[-+]?\d*\.?\d*$", "+12.34")]
    [InlineData(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", "x 12/31/2026 x")]
    [InlineData("^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", "AB1C 2DE")]
    public void FiniteTimeoutUsesInvocationAuthorityForNativeBooleanFamilies(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        foreach (var explicitToken in new[] { false, true })
        foreach (var operation in new[] { 0, 1, 2 })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options, TimeSpan.FromMilliseconds(1));
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
            var error = Assert.Throws<RegexMatchTimeoutException>(() => operation switch
            {
                0 => explicitToken ? regex.IsMatch(input, CancellationToken.None) : regex.IsMatch(input),
                1 => (explicitToken ? regex.Count(input, CancellationToken.None) : regex.Count(input)) != 0,
                _ => (explicitToken ? regex.Match(input, CancellationToken.None) : regex.Match(input)).Success,
            });
            Assert.Equal(pattern, error.Pattern);
            Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        }
    }

    [Theory]
    [InlineData(@"^[-+]?\d*\.?\d*$", "+12.34", "no")]
    [InlineData(@"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b", "x 12/31/2026 x", "no")]
    [InlineData("^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", "AB1C 2DE", "no")]
    public void DeadlineRoutingKeepsScalarResultsStrictTailsAndOriginalCancellation(string pattern, string text, string miss)
    {
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(1));
            var input = Encoding.UTF8.GetBytes(text);
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(static () => 0);
            Assert.True(regex.IsMatch(input));
            Assert.True(regex.IsMatch(input, CancellationToken.None));
            Assert.Equal(1, regex.Count(input));
            Assert.True(regex.Match(input).Success);
            Assert.False(regex.IsMatch(Encoding.UTF8.GetBytes(miss)));
            byte[] malformed = [.. input, 0xff];
            Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
            using var source = new CancellationTokenSource();
            source.Cancel();
            var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(malformed, source.Token));
            Assert.Equal(source.Token, error.CancellationToken);
        }
    }
}
