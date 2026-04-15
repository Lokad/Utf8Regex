using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

internal readonly record struct RegexParityCase(string Pattern, string Input, RegexOptions Options)
{
    public byte[] Utf8Input => Encoding.UTF8.GetBytes(Input);
}

internal sealed class RegexParityContext
{
    private RegexParityContext(RegexParityCase parityCase)
    {
        Case = parityCase;
        Utf8 = new Utf8Regex(parityCase.Pattern, parityCase.Options);
        Utf8Compiled = new Utf8Regex(parityCase.Pattern, parityCase.Options | RegexOptions.Compiled);
        DotNet = new Regex(parityCase.Pattern, parityCase.Options, Regex.InfiniteMatchTimeout);
        DotNetCompiled = new Regex(parityCase.Pattern, parityCase.Options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
    }

    public RegexParityCase Case { get; }

    public Utf8Regex Utf8 { get; }

    public Utf8Regex Utf8Compiled { get; }

    public Regex DotNet { get; }

    public Regex DotNetCompiled { get; }

    public static RegexParityContext Create(string pattern, string input, RegexOptions options)
        => new(new RegexParityCase(pattern, input, options));

    public void AssertIsMatchParity()
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.IsMatch(Case.Input);
        Assert.Equal(expected, DotNetCompiled.IsMatch(Case.Input));
        Assert.Equal(expected, Utf8.IsMatch(bytes));
        Assert.Equal(expected, Utf8Compiled.IsMatch(bytes));
    }

    public void AssertMatchParity()
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.Match(Case.Input);
        var expectedCompiled = DotNetCompiled.Match(Case.Input);
        var actual = Utf8.Match(bytes);
        var actualCompiled = Utf8Compiled.Match(bytes);

        AssertEquivalentMatch(expected, actual);
        AssertEquivalentMatch(expectedCompiled, actualCompiled);
        AssertEquivalentMatch(expected, actualCompiled);
    }

    public void AssertReplaceParity(string replacement)
    {
        var bytes = Case.Utf8Input;

        var expected = Encoding.UTF8.GetBytes(DotNet.Replace(Case.Input, replacement));
        var expectedCompiled = Encoding.UTF8.GetBytes(DotNetCompiled.Replace(Case.Input, replacement));
        var actual = Utf8.Replace(bytes, replacement);
        var actualCompiled = Utf8Compiled.Replace(bytes, replacement);

        Assert.Equal(expected, expectedCompiled);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, actualCompiled);
    }

    public void AssertCountParity(int startAt = 0)
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.Count(Case.Input, startAt);
        var expectedCompiled = DotNetCompiled.Count(Case.Input, startAt);
        var actual = Utf8.CountFromUtf16Offset(bytes, startAt);
        var actualCompiled = Utf8Compiled.CountFromUtf16Offset(bytes, startAt);

        Assert.Equal(expected, expectedCompiled);
        Assert.Equal(expected, actual);
        Assert.Equal(expected, actualCompiled);
    }

    public void AssertDetailedMatchGroupParity()
    {
        var bytes = Case.Utf8Input;

        var expected = DotNet.Match(Case.Input);
        var expectedCompiled = DotNetCompiled.Match(Case.Input);
        var actual = Utf8.MatchDetailed(bytes);
        var actualCompiled = Utf8Compiled.MatchDetailed(bytes);

        AssertEquivalentDetailedMatch(expected, actual);
        AssertEquivalentDetailedMatch(expectedCompiled, actualCompiled);
        AssertEquivalentDetailedMatch(expected, actualCompiled);
    }

    private static void AssertEquivalentMatch(Match expected, Utf8ValueMatch actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
    }

    private static void AssertEquivalentDetailedMatch(Match expected, Utf8MatchContext actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        Assert.Equal(expected.Groups.Count, actual.GroupCount);

        for (var groupIndex = 0; groupIndex < expected.Groups.Count; groupIndex++)
        {
            var expectedGroup = expected.Groups[groupIndex];
            var actualGroup = actual.GetGroup(groupIndex);

            Assert.Equal(expectedGroup.Success, actualGroup.Success);
            Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
            Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
            Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
            Assert.Equal(expectedGroup.Captures.Count, actualGroup.CaptureCount);

            for (var captureIndex = 0; captureIndex < expectedGroup.Captures.Count; captureIndex++)
            {
                var expectedCapture = expectedGroup.Captures[captureIndex];
                var actualCapture = actualGroup.GetCapture(captureIndex);

                Assert.Equal(expectedCapture.Index, actualCapture.IndexInUtf16);
                Assert.Equal(expectedCapture.Length, actualCapture.LengthInUtf16);
                Assert.Equal(expectedCapture.Value, actualCapture.GetValueString());
            }
        }
    }
}
