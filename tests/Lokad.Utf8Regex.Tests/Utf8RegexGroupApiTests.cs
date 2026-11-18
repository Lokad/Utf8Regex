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

        Assert.Equal(groupNumber, utf8Regex.GroupNumberFromName("word"));
        Assert.Equal(regex.GroupNameFromNumber(groupNumber), utf8Regex.GroupNameFromNumber(groupNumber));
        var copiedNames15 = new string[utf8Regex.CaptureGroupCount + 1];
        for (var captureSlot15 = 0; captureSlot15 < copiedNames15.Length; captureSlot15++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                utf8Regex.TryCopyGroupNameUtf8(captureSlot15, [], out var nameLength15));
            var nameBytes15 = new byte[nameLength15];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                utf8Regex.TryCopyGroupNameUtf8(captureSlot15, nameBytes15, out var written15));
            Assert.Equal(nameLength15, written15);
            copiedNames15[captureSlot15] = System.Text.Encoding.UTF8.GetString(nameBytes15);
        }
        Assert.Equal(regex.GetGroupNames(), copiedNames15);
        Assert.Equal(regex.GetGroupNumbers(), Enumerable.Range(0, utf8Regex.CaptureGroupCount + 1).Select(utf8Regex.GroupNumberFromCaptureSlot).ToArray());
    }
}
