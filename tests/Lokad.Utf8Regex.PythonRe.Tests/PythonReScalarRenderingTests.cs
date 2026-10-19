using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarRenderingTests
{
    [Theory]
    [InlineData("[\U0001f600-\U0001f64f]", "\U0001f601", true)]
    [InlineData("[\U0001f600-\U0001f64f]", "\U0001f650", false)]
    [InlineData("[\uffff-\U00010001]", "\U00010000", true)]
    [InlineData("[\uffff-\U00010001]", "\U00010002", false)]
    [InlineData("[\U0010fffe-\U0010ffff]", "\U0010ffff", true)]
    [InlineData("[\u0000-\U0010ffff]", "\U0010ffff", true)]
    [InlineData("[\u0000-\uffff]", "\U0001f600", false)]
    [InlineData("[^\U0001f600-\U0001f64f]", "\U00010400", true)]
    [InlineData("[^\U0001f600-\U0001f64f]", "\U0001f601", false)]
    [InlineData("[a\U0001f600-\U0001f64f]", "a", true)]
    [InlineData("[a\U0001f600-\U0001f64f]", "\U0001f601", true)]
    [InlineData("[^\u0000-\uffff]", "\U00010400", true)]
    [InlineData(@"[\w\U0001f600-\U0001f64f]", "\U00010400", true)]
    [InlineData(@"[^\w\U0001f600-\U0001f64f]", "\U00010400", false)]
    [InlineData(@"\w", "\U00010400", true)]
    [InlineData(@"\W", "\U00010400", false)]
    [InlineData(@"\d", "\U0001d7ce", true)]
    [InlineData(@"\D", "\U0001d7ce", false)]
    [InlineData(@"[^\W]", "\U00010400", true)]
    [InlineData(@"[^\D]", "\U0001d7ce", true)]
    [InlineData(@"\b\w\b", "\U00010400", true)]
    [InlineData(@"(?a:\w)", "\U00010400", false)]
    [InlineData(@"(?a:\d)", "\U0001d7ce", false)]
    [InlineData(@"(?a:\W)", "\U00010400", true)]
    [InlineData(@"(?a:\D)", "\U0001d7ce", true)]
    [InlineData(@"\ud83d\ude00", "\U0001f600", false)]
    [InlineData(@"[\ud83d\ude00]", "\U0001f600", false)]
    [InlineData(@"[^\ud800-\udfff]", "\U0001f600", true)]
    [InlineData(@"\ud800", "\U0001f600", false)]
    [InlineData("[\U0001f600]", "\U0001f600", true)]
    public void ScalarClassesAndPythonEscapesAgreeAcrossDirectOperations(string pattern, string subject, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        using var source = new CancellationTokenSource();
        foreach (var token in new[] { CancellationToken.None, source.Token })
        {
            Assert.Equal(expected, regex.IsMatch(input, 0, token));
            var found = regex.Search(input, 0, token);
            Assert.Equal(expected, found.Success);
            Assert.Equal(expected, regex.Match(input, 0, token).Success);
            Assert.Equal(expected, regex.FullMatch(input, 0, token).Success);
            Assert.Equal(expected ? 1 : 0, regex.Count(input, 0, token));
            if (found.Success)
            {
                Assert.Equal(0, found.StartOffsetInBytes);
                Assert.Equal(input.Length, found.EndOffsetInBytes);
                Assert.Equal(0, found.StartOffsetInUtf16);
                Assert.Equal(subject.Length, found.EndOffsetInUtf16);
                Assert.Equal(subject, found.GetValueString());
            }
            var detailed = regex.SearchDetailedData(input, 0, token);
            Assert.Equal(expected, detailed.Success);
            if (detailed.Success)
            {
                Assert.Equal(0, detailed.Value.StartOffsetInBytes);
                Assert.Equal(input.Length, detailed.Value.EndOffsetInBytes);
                Assert.Equal(subject, detailed.Value.ValueText);
            }
        }
        Assert.Equal(expected ? new[] { subject } : [], regex.FindAllToStrings(input).ScalarValues);
    }

    [Theory]
    [InlineData("a.*b", "a\nb", false)]
    [InlineData("a.+b", "a\U0001f600\nb", false)]
    [InlineData("(?s:a.*b)", "a\U0001f600\nb", true)]
    [InlineData("(?s:a(?-s:.*)b)", "a\U0001f600\nb", false)]
    [InlineData("(?s:a(?-s:.*)b)", "a\U0001f600b", true)]
    public void QuantifiedDotKeepsNewlineScopeOnEveryScalar(string pattern, string subject, bool expected)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(expected, regex.FullMatch(input).Success);
        Assert.Equal(expected ? new[] { subject } : [], regex.FindAllToStrings(input).ScalarValues);
    }

    [Fact]
    public void NamedCaptureAndReferenceUseCompleteByteRanges()
    {
        var regex = new Utf8PythonRegex(@"(?P<x>[\U00010400-\U00010428])(?P=x)");
        var input = Encoding.UTF8.GetBytes("\U00010400\U00010400");
        var match = regex.FullMatchDetailed(input);
        Assert.True(match.Success);
        var group = match.GetGroup(1);
        Assert.Equal("\U00010400", group.GetValueString());
        Assert.Equal(0, group.Value.StartOffsetInBytes);
        Assert.Equal(4, group.Value.EndOffsetInBytes);
    }

    [Theory]
    [InlineData(@"\ud800*")]
    [InlineData(@"\U0000d800*")]
    public void SurrogateAtomCanRepeatZeroTimesWithScalarEmptyProgress(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern);
        Assert.True(regex.FullMatch(ReadOnlySpan<byte>.Empty).Success);
        Assert.Equal(2, regex.Count("😀"u8));
    }

    [Theory]
    [InlineData(@"\Uffffffff")]
    [InlineData(@"\U80000000")]
    [InlineData(@"[\U80000000]")]
    public void OutOfDomainEscapesRejectWithPythonPatternError(string pattern)
    {
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
    }
}
