using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2GrammarBoundaryTests
{
    [Theory]
    [InlineData(@"\u0041+")]
    [InlineData(@"[\u0041]")]
    [InlineData(@"\p{IsGreek}+")]
    [InlineData("(?<a>x)(?<b-a>y)")]
    [InlineData("(?<a>x)(?<-a>y)")]
    [InlineData(@"\p{IsBasicLatin}+")]
    [InlineData(@"\p{_xmlC}+")]
    public void CoreOnlyGrammarCannotEnterThePcre2Profile(string pattern)
    {
        Assert.Throws<NotSupportedException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Throws<NotSupportedException>(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern)));
        Assert.Throws<NotSupportedException>(() => Utf8Pcre2Regex.Compile(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = Utf8ConstructionPolicy.Default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default));
        Assert.Throws<NotSupportedException>(() => Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern), options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited,
            ConstructionPolicy = Utf8ConstructionPolicy.Default,
            ConstructionTimeout = Timeout.InfiniteTimeSpan
        }, cancellationToken: default));
    }

    [Theory]
    [InlineData(@"\x{41}+", "AAA")]
    [InlineData(@"\p{L}+", "αβ")]
    [InlineData("(?<a>x)(?<b>y)", "xy")]
    public void ProfileGrammarKeepsItsOwnedExecution(string pattern, string subject)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var bytes = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(subject, System.Text.Encoding.UTF8.GetString(regex.Match(bytes).GetValueBytes()));
        Assert.Equal(1, regex.Count(bytes));
        Assert.True(regex.Match(bytes).Success);
    }

    [Theory]
    [InlineData(@"\/", "/")]
    [InlineData(@"\!", "!")]
    [InlineData(@"\é", "é")]
    [InlineData(@"\😀", "😀")]
    public void QuotedLiteralScalarsAreEstablishedByTheProfileParser(string pattern, string subject)
        => Assert.Equal(subject, System.Text.Encoding.UTF8.GetString(new Utf8Pcre2Regex(pattern).Match(Encoding.UTF8.GetBytes(subject)).GetValueBytes()));
}
