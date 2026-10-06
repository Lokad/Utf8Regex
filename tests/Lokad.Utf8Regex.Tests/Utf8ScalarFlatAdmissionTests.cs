using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarFlatAdmissionTests
{
    public static IEnumerable<object[]> ImpossibleLiterals()
    {
        // xUnit transports theory strings through UTF-8 and substitutes lone
        // surrogates. Carry stable IDs and construct the original atoms below.
        foreach (var patternId in Enumerable.Range(0, 7))
        foreach (var compiled in new[] { false, true })
        foreach (var nonBacktracking in new[] { false, true })
            yield return [patternId, compiled, nonBacktracking];
    }

    [Theory]
    [MemberData(nameof(ImpossibleLiterals))]
    public void ImpossibleAtomsCannotBecomeReplacementCharacterLiterals(int patternId, bool compiled, bool nonBacktracking)
    {
        string[] patterns = ["\ud83d", "\ude00", @"\ud83d", "(\ud83d)", "(?<x>\ude00)", "a\ud83d", "\ud83db"];
        var pattern = patterns[patternId];
        var regex = new Utf8Regex(pattern, Options(compiled, nonBacktracking));
        var input = "\ufffd a\ufffd \ufffdb \U0001f600"u8.ToArray();
        using var source = new CancellationTokenSource();
        Assert.False(regex.IsMatch(input));
        Assert.False(regex.IsMatch(input, source.Token));
        Assert.Equal(0, regex.Count(input));
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.MatchDetailed(input).Success);
        var cursor = regex.EnumerateMatches(input);
        Assert.False(cursor.MoveNext());
        Assert.Equal(input, regex.Replace(input, "Q"));
        Assert.Equal(Encoding.UTF8.GetString(input), regex.ReplaceToString(input, "$&"));
        Assert.NotNull(regex.Inspection.AvailableOwnedSliceProgram);
        AssertOwned(regex);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonBacktrackingFlatGraphsOwnScalarOperationsAndConstruction(bool compiled)
    {
        using var source = new CancellationTokenSource();
        foreach (var pattern in new[] { ".", "(.)", "^.$", "[\U0001f600]", "(?<x>\U0001f600)", "\ud83d|\U0001f600" })
        {
            var regex = new Utf8Regex(pattern, Options(compiled, true));
            AssertOwned(regex);
            Assert.True(regex.IsMatch("\U0001f600"u8, source.Token));
            Assert.Equal(1, regex.Count("\U0001f600"u8, source.Token));
            var match = regex.MatchDetailed("\U0001f600"u8, source.Token);
            Assert.True(match.Success);
            Assert.Equal((0, 4, 0, 2), (match.IndexInBytes, match.LengthInBytes, match.IndexInUtf16, match.LengthInUtf16));
            Assert.Equal("\U0001f600", match.GetValueString());
            if (regex.GetGroupNumbers().Length > 1) Assert.Equal("\U0001f600", match.GetGroup(1).GetValueString());
            Assert.Equal("<\U0001f600>", regex.ReplaceToString("\U0001f600"u8, "<$&>", source.Token));
            Assert.Equal("<\U0001f600>"u8.ToArray(), regex.Replace("\U0001f600"u8, "<$&>", source.Token));
            AssertOwned(regex);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ImpossibleRepetitionAndAssertionsUseScalarFailure(bool compiled, bool rtl)
    {
        var options = Options(compiled, false) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None);
        foreach (var pattern in new[] { "\ud83d?", "(?:\ude00){0}", "(?=\ud83d)", "(?<=\ude00)" })
        {
            var regex = new Utf8Regex(pattern, options);
            var expected = pattern.StartsWith("(?=", StringComparison.Ordinal) || pattern.StartsWith("(?<=", StringComparison.Ordinal) ? 0 : 3;
            Assert.Equal(expected, regex.Count("\U0001f600\ufffd"u8));
            AssertOwned(regex);
        }
        var alternative = new Utf8Regex("\ud83d|a", options);
        var match = alternative.Match("\ufffda"u8);
        Assert.Equal((3, 1), (match.IndexInBytes, match.LengthInBytes));
        AssertOwned(alternative);
    }

    private static RegexOptions Options(bool compiled, bool nonBacktracking) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None) | (nonBacktracking ? RegexOptions.NonBacktracking : RegexOptions.None);

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }
}
