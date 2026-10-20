using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2GrammarBoundaryTests
{
    [Theory]
    [InlineData(@"\u0041+")]
    [InlineData(@"[\u0041]")]
    [InlineData(@"\p{IsGreek}+")]
    [InlineData("(?<a>x)(?<b-a>y)")]
    public void CoreOnlyGrammarCannotEnterThePcre2Profile(string pattern)
    {
        Assert.Throws<NotSupportedException>(() => new Utf8Pcre2Regex(pattern));
        Assert.Throws<NotSupportedException>(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern)));
        Assert.Throws<NotSupportedException>(() => Utf8Pcre2Regex.Compile(pattern, default, default,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            Timeout.InfiniteTimeSpan, default));
        Assert.Throws<NotSupportedException>(() => Utf8Pcre2Regex.Compile(Encoding.UTF8.GetBytes(pattern), default, default,
            Timeout.InfiniteTimeSpan, Utf8ResourcePolicy.Unlimited, Utf8ConstructionPolicy.Default,
            Timeout.InfiniteTimeSpan, default));
    }

    [Theory]
    [InlineData(@"\x{41}+", "AAA")]
    [InlineData(@"\p{L}+", "αβ")]
    [InlineData("(?<a>x)(?<b>y)", "xy")]
    public void ProfileGrammarKeepsItsOwnedExecution(string pattern, string subject)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var bytes = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(subject, regex.Match(bytes).GetValueString());
        Assert.Equal(1, regex.Count(bytes));
        Assert.True(regex.MatchDetailed(bytes).Success);
    }
}
