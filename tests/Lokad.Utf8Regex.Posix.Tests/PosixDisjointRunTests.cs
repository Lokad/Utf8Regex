using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixDisjointRunTests
{
    [Fact]
    public void NewlineAnchorsSelectEachCompleteDisjointRunPair()
    {
        var regex = new Utf8PosixRegex("^([a-z]+)([0-9]+)$", PosixCompileOptions.Newline,
            Timeout.InfiniteTimeSpan, default);
        var captures = new Utf8ValueMatch[3];
        var whole = regex.Match("bad\nsku007\nx9\n"u8, captures);
        Assert.Equal((4, 6), (whole.IndexInBytes, whole.LengthInBytes));
        Assert.Equal((4, 3), (captures[1].IndexInBytes, captures[1].LengthInBytes));
        Assert.Equal((7, 3), (captures[2].IndexInBytes, captures[2].LengthInBytes));
        Assert.Equal(2, regex.Count("bad\nsku007\nx9\n"u8));
    }

    [Fact]
    public void OnlyFlatPositiveDisjointAsciiClassesAdmitUniqueRunBoundaries()
    {
        Assert.True(PosixProgram.Compile("^([[:alpha:]]+)([0-9]+)$", default).FirstAcceptanceIsLongest);
        Assert.True(PosixProgram.Compile("([a-z]{1,3})([0-9]{2,4})([!]{1,2})", default).FirstAcceptanceIsLongest);
        foreach (var pattern in new[] { "([a-z]+)([a-z0-9]+)", "([a-z]*)([0-9]+)",
            "([a-zé]+)([0-9]+)", "([^0-9]+)([0-9]+)", "([a-z])+([0-9]+)", "([a-z]+)|([0-9]+)" })
            Assert.False(PosixProgram.Compile(pattern, default).FirstAcceptanceIsLongest);
    }

    [Theory]
    [InlineData("^([[:alpha:]]+)([0-9]+)$", "sku007", 0, 0, 6, 0, 3, 3, 3)]
    [InlineData("([a-z]+)([0-9]+)", "😀sku007!", 4, 4, 6, 4, 3, 7, 3)]
    [InlineData("([a-z]{1,3})([0-9]{2,4})", "abcd12345", 0, 1, 7, 1, 3, 4, 4)]
    [InlineData("(([a-z]+))([0-9]+)", "ab123", 0, 0, 5, 0, 2, 0, 2)]
    public void LongestCapturesKeepIndependentByteCoordinates(string pattern, string text, int floor,
        int wholeStart, int wholeLength, int firstStart, int firstLength, int secondStart, int secondLength)
    {
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        var regex = new Utf8PosixRegex(pattern);
        var captures = new Utf8ValueMatch[regex.GroupCount + 1];
        var whole = regex.Match(input, captures, floor);
        Assert.True(whole.Success);
        Assert.Equal((wholeStart, wholeLength), (whole.IndexInBytes, whole.LengthInBytes));
        Assert.Equal((firstStart, firstLength), (captures[1].IndexInBytes, captures[1].LengthInBytes));
        Assert.Equal((secondStart, secondLength), (captures[2].IndexInBytes, captures[2].LengthInBytes));
        using var token = new CancellationTokenSource();
        Assert.Equal(whole, regex.Match(input, captures, floor, token.Token));
        Assert.Equal(1, regex.Count(input, floor, token.Token));
        Assert.True(regex.IsMatch(input, token.Token));
    }
}
