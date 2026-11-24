using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexGroupApiTests
{
    [Fact]
    public void GroupNameAndNumberApisMirrorRegexMetadata()
    {
        const string pattern = "(?<word>ab)(cd)";
        var utf8Regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var regex = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        var groupNumber = regex.GroupNumberFromName("word");

        Assert.Equal(groupNumber, utf8Regex.GroupNumberFromUtf8Name("word"u8));
        Assert.Equal(regex.GetGroupNames(), Utf8GroupNames.Read(utf8Regex));
        Assert.Equal(regex.GetGroupNumbers(), Enumerable.Range(0, utf8Regex.CaptureGroupCount + 1).Select(utf8Regex.GroupNumberFromCaptureSlot).ToArray());
    }
}
