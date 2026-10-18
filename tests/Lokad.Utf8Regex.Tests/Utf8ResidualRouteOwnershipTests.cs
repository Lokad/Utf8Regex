using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ResidualRouteOwnershipTests
{
    [Theory]
    [InlineData("(?#comment)ab", false)]
    [InlineData("(?#comment)ab", true)]
    [InlineData("(?#(?C)ab", false)]
    [InlineData("(?#(?C)ab", true)]
    [InlineData("(?#(?z)ab", false)]
    [InlineData("(?#(?z)ab", true)]
    [InlineData("(?#(?<100>a)a", false)]
    [InlineData("(?#(?<100>a)a", true)]
    public void CommentPatternsKeepOwnedOffsetDiscovery(string pattern, bool compiled)
    {
        var regex = new Utf8Regex(pattern, Options(compiled), TimeSpan.FromSeconds(1));
        var bytes = "😀xab 😀ab"u8;
        var subject = Utf8ValidatedInput.Create(bytes);
        var start = subject.GetBytePosition(7, "startOffsetInBytes");
        var startUtf16 = subject.Project(start).Value;
        var match = regex.MatchFromUtf16Offset(bytes, startUtf16);
        Assert.True(match.Success);
        // The fourth comment terminates at the first closing parenthesis,
        // leaving "a" as its literal; the other three leave "ab".
        var length = pattern == "(?#(?<100>a)a" ? 1 : 2;
        Assert.Equal((12, length), (match.IndexInBytes, match.LengthInBytes));
        Assert.True(regex.IsMatchFromUtf16Offset(bytes, startUtf16));
        var detailed = regex.MatchDetailedFromUtf16Offset(bytes, startUtf16);
        Assert.True(detailed.Success);
        Assert.Equal((12, length), (detailed.IndexInBytes, detailed.LengthInBytes));
        Assert.Equal(1, regex.CountFromUtf16Offset(bytes, startUtf16));
        var enumerated = new List<(int, int)>();
        foreach (var value in regex.ByteOffsetExecution.EnumerateMatches(subject, start))
            enumerated.Add((value.IndexInBytes, value.LengthInBytes));
        Assert.Equal([(12, length)], enumerated);
        Assert.False(regex.MatchFromUtf16Offset(bytes, subject.Utf16Length).Success);
        AssertNoBclEngine(regex);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RtlBoundaryEnumerationRetainsWholeScalarContext(bool compiled, bool supplementary)
    {
        var regex = new Utf8Regex(@"\bword\b", Options(compiled) | RegexOptions.RightToLeft);
        var input = Encoding.UTF8.GetBytes(supplementary ? "𐐀word word😀 word" : "word word");
        var actual = new List<(int, int)>();
        foreach (var match in regex.EnumerateMatches(input)) actual.Add((match.IndexInBytes, match.LengthInBytes));
        (int, int)[] expected = supplementary ? [(18, 4), (9, 4)] : [(5, 4), (0, 4)];
        Assert.Equal(expected, actual);
        AssertNoBclEngine(regex);
    }

    [Theory]
    [InlineData(false, "#abcdef", true)]
    [InlineData(true, "#abcdef", true)]
    [InlineData(false, "#abcd", false)]
    [InlineData(true, "#abcd", false)]
    [InlineData(false, "#ab", false)]
    [InlineData(true, "#ab", false)]
    public void HexColorNativeMissesRemainOwned(bool compiled, string text, bool success)
    {
        var regex = new Utf8Regex("^#?([a-f0-9]{6}|[a-f0-9]{3})$", Options(compiled));
        var match = regex.Match(Encoding.UTF8.GetBytes(text));
        Assert.Equal(success, match.Success);
        if (success) Assert.Equal((0, 7), (match.IndexInBytes, match.LengthInBytes));
        AssertNoBclEngine(regex);
    }

    [Theory]
    [InlineData("^(?:[ab]{2}|[ab]{4})$", "aa", true, false)]
    [InlineData("^(?:[ab]{2}|[ab]{4})$", "aaa", false, false)]
    [InlineData("^(?:[ab]{2}|[ab]{4})$", "aaaa", true, false)]
    [InlineData("^(?:[ab]{2}|[ab]{3})$", "aa", true, true)]
    [InlineData("^(?:[ab]{2}|[ab]{3})$", "aaa", true, true)]
    [InlineData("^(?:[ab]{2}|[ab]{3})$", "aaaa", false, true)]
    [InlineData("^(?:[ab]{2}[cd]{2}|[ab]{3}[cd]{3})$", "aacc", true, false)]
    [InlineData("^(?:[ab]{2}[cd]{2}|[ab]{3}[cd]{3})$", "aaaccc", true, false)]
    [InlineData("^(?:[ab]{2}[cd]{2}|[ab]{3}[cd]{3})$", "aaccc", false, false)]
    [InlineData("^(?:[ab]{2}[cd]{2}|[ab]{3}[cd]{3})$", "aaacc", false, false)]
    [InlineData("^(?:x|[ab]{3}x)$", "x", true, false)]
    [InlineData("^(?:x|[ab]{3}x)$", "aaax", true, false)]
    [InlineData("^(?:x|[ab]{3}x)$", "ax", false, false)]
    [InlineData("^(?:x|[ab]{3}x)$", "aax", false, false)]
    [InlineData("^(?:[0-9]{2}/[0-9]{2}/[0-9]{4}|[0-9]{4}/[0-9]{2}/[0-9]{4})$", "12/34/5678", true, false)]
    [InlineData("^(?:[0-9]{2}/[0-9]{2}/[0-9]{4}|[0-9]{4}/[0-9]{2}/[0-9]{4})$", "123/34/5678", false, false)]
    [InlineData("^(?:[0-9]{1}/[0-9]{1}/[0-9]{4}|[0-9]{2}/[0-9]{2}/[0-9]{4})$", "1/2/3456", true, false)]
    [InlineData("^(?:[0-9]{1}/[0-9]{1}/[0-9]{4}|[0-9]{2}/[0-9]{2}/[0-9]{4})$", "12/2/3456", false, false)]
    [InlineData("^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$", "12/2/3456", true, true)]
    [InlineData("^[0-9]{1,2}/[0-9]{1,2}/[0-9]{4}$", "1/23/4567", true, true)]
    [InlineData("^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", "SW1 1ZZ", true, true)]
    [InlineData("^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", "W1A0AX", true, true)]
    [InlineData("^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$", "AA11B2ZZ", false, true)]
    public void NativeLengthUnionsPreserveGapsAndBranchCorrelation(
        string pattern, string text, bool success, bool nativeValidator)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, Options(compiled));
            Assert.Equal(nativeValidator, regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue);
            Assert.Equal(success, regex.IsMatch(bytes));
            Assert.Equal(success ? 1 : 0, regex.Count(bytes));
            var value = regex.Match(bytes);
            Assert.Equal(success, value.Success);
            if (success) Assert.Equal((0, bytes.Length), (value.IndexInBytes, value.LengthInBytes));
            Assert.Equal(success, regex.MatchDetailed(bytes).Success);
            Assert.Equal(success ? "Q" : text, regex.ReplaceToString(bytes, "Q"));
            AssertNoBclEngine(regex);
        }
    }

    private static RegexOptions Options(bool compiled) =>
        RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);

    private static void AssertNoBclEngine(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
