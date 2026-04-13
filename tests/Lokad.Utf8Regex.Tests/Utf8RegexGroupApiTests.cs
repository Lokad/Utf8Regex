using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexGroupApiTests
{
    [Fact]
    public void GroupNameAndNumberApisMirrorRegexMetadata()
    {
        const string pattern = "(?<word>ab)(cd)";
        var utf8Regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var groupNumber = regex.GroupNumberFromName("word");

        Assert.Equal(groupNumber, utf8Regex.GroupNumberFromName("word"));
        Assert.Equal(regex.GroupNameFromNumber(groupNumber), utf8Regex.GroupNameFromNumber(groupNumber));
        Assert.Equal(regex.GetGroupNames(), utf8Regex.GetGroupNames());
        Assert.Equal(regex.GetGroupNumbers(), utf8Regex.GetGroupNumbers());
    }
}
