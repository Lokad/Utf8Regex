using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CompletedBackreferenceTests
{
    [Theory]
    [InlineData(@"(a)\1")]
    [InlineData(@"(a*)\1")]
    [InlineData(@"(a|aa)+\1")]
    [InlineData(@"(?<word>[a-z]+)-\k<word>")]
    public void CompletedUniqueCaptureDoesNotNeedAnOpeningJournal(string pattern)
    {
        var program = new Utf8Regex(pattern).Inspection.AvailableOwnedSliceProgram!;
        Assert.Equal(1, program.GroupCount);
        Assert.False(program.UsesCaptureOpeningJournal);
    }

    [Theory]
    [InlineData(@"(a|b\1)+", RegexOptions.None)]
    [InlineData(@"\1(a)", RegexOptions.None)]
    [InlineData(@"(a)(b)\2\1", RegexOptions.None)]
    [InlineData(@"(?<x>a(?<x>b))\k<x>", RegexOptions.None)]
    [InlineData(@"(a)(?<=\1)", RegexOptions.None)]
    [InlineData(@"(?<x>a)(?<-x>b)\k<x>", RegexOptions.None)]
    [InlineData(@"(a)\1", RegexOptions.RightToLeft)]
    public void OpenExternalSharedAndComplexReferencesKeepTheirJournal(string pattern, RegexOptions options)
        => Assert.True(new Utf8Regex(pattern, options).Inspection.AvailableOwnedSliceProgram!.UsesCaptureOpeningJournal);

    [Theory]
    [InlineData(@"([a-z]+)-\1", "x token-token y token-other")]
    [InlineData(@"(a+)a\1", "aaaaaaa aaaa")]
    [InlineData(@"(a|aa)+\1", "aaaaabaaa")]
    [InlineData(@"(ab)+\1", "xabababab y")]
    [InlineData(@"(a*)\1", "baaaa")]
    [InlineData(@"(x)?b\1", "bbb xbxxbx")]
    [InlineData(@"(?<word>é😀)-\k<word>", "中 é😀-é😀 é😀-é")]
    [InlineData(@"(a|b\1)+", "abbaaba")]
    [InlineData(@"(a)(?<=\1)", "ba")]
    [InlineData(@"(a)?(?(1)\1|b)", "aa b ax")]
    public void PublicValuesHistoriesAndReplacementKeepCaptureSemantics(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern);
        var expected = oracle.Matches(text);
        var regex = new Utf8Regex(pattern);
        using var token = new CancellationTokenSource();
        Assert.Equal(expected.Count, regex.Count(input, token.Token));
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, token.Token));
        Assert.Equal(oracle.Replace(text, "<$1>"), Encoding.UTF8.GetString(regex.Replace(input, "<$1>")));
        var index = 0;
        foreach (var actual in regex.EnumerateMatches(input))
        {
            var wanted = expected[index++];
            Assert.Equal((wanted.Index, wanted.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, wanted.Index)), actual.IndexInBytes);
        }
        Assert.Equal(expected.Count, index);
        var detailed = regex.MatchDetailed(input);
        Assert.Equal(expected.Count != 0, detailed.Success);
        if (!detailed.Success) return;
        var group = detailed.GetGroup(1);
        var wantedGroup = expected[0].Groups[1];
        Assert.Equal(wantedGroup.Value, group.GetValueString());
        Assert.Equal(wantedGroup.Captures.Count, group.CaptureCount);
        for (var capture = 0; capture < group.CaptureCount; capture++)
            Assert.Equal(wantedGroup.Captures[capture].Value, group.GetCapture(capture).GetValueString());
        // A later operation cannot rewrite retained history.
        _ = regex.Match(input);
        Assert.Equal(wantedGroup.Value, group.GetValueString());
    }
}
