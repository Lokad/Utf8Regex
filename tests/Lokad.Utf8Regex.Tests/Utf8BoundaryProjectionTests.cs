using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8BoundaryProjectionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BoundaryWrappedUtf8LiteralProjectsUtf16AndByteCoordinates(bool compiled)
    {
        const string pattern = @"\bé\b";
        const string input = "é z é";

        var regex = CreateUtf8Regex(pattern, compiled);
        var runtime = CreateRuntimeRegex(pattern, compiled);
        var bytes = Encoding.UTF8.GetBytes(input);
        var match = regex.MatchDetailed(bytes);
        var expected = runtime.Match(input);

        Assert.True(match.Success);
        Assert.Equal(expected.Index, match.IndexInUtf16);
        Assert.Equal(expected.Length, match.LengthInUtf16);
        Assert.Equal(expected.Value, match.GetValueString());
        Assert.True(match.IsByteAligned);
        Assert.True(match.TryGetByteRange(out var indexInBytes, out var lengthInBytes));
        Assert.Equal(Encoding.UTF8.GetByteCount(input[..expected.Index]), indexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), lengthInBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodeWordBoundaryProjectsCorrectlyForHebrewWord(bool compiled)
    {
        const string pattern = @"\b\w+\b";
        const string input = "אבג xyz";

        var regex = CreateUtf8Regex(pattern, compiled);
        var runtime = CreateRuntimeRegex(pattern, compiled);
        var bytes = Encoding.UTF8.GetBytes(input);
        var match = regex.MatchDetailed(bytes);
        var expected = runtime.Match(input);

        Assert.True(match.Success);
        Assert.Equal("אבג", expected.Value);
        Assert.Equal(expected.Index, match.IndexInUtf16);
        Assert.Equal(expected.Length, match.LengthInUtf16);
        Assert.Equal(expected.Value, match.GetValueString());
        Assert.True(match.IsByteAligned);
        Assert.True(match.TryGetByteRange(out var indexInBytes, out var lengthInBytes));
        Assert.Equal(Encoding.UTF8.GetByteCount(input[..expected.Index]), indexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), lengthInBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodeDigitBoundaryProjectsCorrectlyForArabicIndicDigits(bool compiled)
    {
        const string pattern = @"\b\d+\b";
        const string input = "١٢٣ x";

        var regex = CreateUtf8Regex(pattern, compiled);
        var runtime = CreateRuntimeRegex(pattern, compiled);
        var bytes = Encoding.UTF8.GetBytes(input);
        var match = regex.MatchDetailed(bytes);
        var expected = runtime.Match(input);

        Assert.True(match.Success);
        Assert.Equal("١٢٣", expected.Value);
        Assert.Equal(expected.Index, match.IndexInUtf16);
        Assert.Equal(expected.Length, match.LengthInUtf16);
        Assert.Equal(expected.Value, match.GetValueString());
        Assert.True(match.IsByteAligned);
        Assert.True(match.TryGetByteRange(out var indexInBytes, out var lengthInBytes));
        Assert.Equal(Encoding.UTF8.GetByteCount(input[..expected.Index]), indexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), lengthInBytes);
    }

    private static Utf8Regex CreateUtf8Regex(string pattern, bool compiled)
        => new(pattern, compiled ? RegexOptions.Compiled | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant);

    private static Regex CreateRuntimeRegex(string pattern, bool compiled)
        => new(pattern, compiled ? RegexOptions.Compiled | RegexOptions.CultureInvariant : RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
}
