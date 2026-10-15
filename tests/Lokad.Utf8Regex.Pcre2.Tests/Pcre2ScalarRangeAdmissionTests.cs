using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ScalarRangeAdmissionTests
{
    // Frozen native PCRE2 10.47 UTF/UCP observations; no native test dependency.
    [Theory]
    [InlineData("[😀-😃]", "😀", true)]
    [InlineData("[😀-😃]", "😁", true)]
    [InlineData("[😀-😃]", "😃", true)]
    [InlineData("[😀-😃]", "😄", false)]
    [InlineData("[😀-😀]", "😀", true)]
    [InlineData("[😀-😀]", "😁", false)]
    [InlineData("[😀-\U0010FFFF]", "\U0010FFFF", true)]
    [InlineData("[A-😀]", "a", true)]
    [InlineData(@"[\x{1F600}-\x{1F603}]", "😁", true)]
    [InlineData(@"[\x{1F600}-\x{1F603}]", "😄", false)]
    [InlineData(@"[\x{41}-\x{5A}]", "Z", true)]
    [InlineData(@"[\x{41}-\x{5A}]", "[", false)]
    [InlineData(@"[\o{101}-\o{132}]", "A", true)]
    [InlineData("[\uE000-😀]", "😀", true)]
    [InlineData("[\uE000-😀]", "\uE000", true)]
    [InlineData(@"[\d-]", "1", true)]
    [InlineData(@"[\d-]", "-", true)]
    [InlineData(@"[-\d]", "a", false)]
    public void RangeEndpointsAreScalarsForTextAndUtf8Patterns(string body, string text, bool expected)
    {
        var pattern = "^" + body + "$";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var finite in new[] { false, true })
        {
            var limits = finite ? new Utf8Pcre2ExecutionLimits
            {
                MatchLimit = 1_000_000, DepthLimit = 256,
                HeapLimitInBytes = 1_048_576, ResultLimitInBytes = 1_048_576,
            } : default;
            var fromText = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Ucp, default,
                limits, Timeout.InfiniteTimeSpan);
            var fromBytes = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), Pcre2CompileOptions.Ucp,
                default, limits, Timeout.InfiniteTimeSpan);
            Assert.Equal(expected, fromText.IsMatch(input));
            Assert.Equal(expected, fromBytes.IsMatch(input));
            Assert.Equal(expected ? 1 : 0, fromText.Count(input));
        }
    }

    [Theory]
    [InlineData("[😃-😀]")]
    [InlineData("[😀-A]")]
    [InlineData("[z-a]")]
    [InlineData(@"[\x{1F603}-\x{1F600}]")]
    [InlineData(@"[\x{5A}-\x{41}]")]
    [InlineData(@"[\d-a]")]
    [InlineData(@"[\d-z]")]
    [InlineData(@"[a-\d]")]
    [InlineData(@"[[:digit:]-z]")]
    [InlineData(@"[a-[:digit:]]")]
    public void InvalidOrReversedRangesRetainTheirStructuredCompileError(string pattern)
    {
        Assert.Equal(Pcre2ErrorKind.ClassRangeOrder,
            Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.ClassRangeOrder,
            Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern))).ErrorKind);
    }

    [Fact]
    public void CapturedRangeKeepsScalarCoordinatesAndFiniteAuthority()
    {
        var regex = Utf8Pcre2Regex.Create("([😀-😃]+)", Pcre2CompileOptions.Ucp, default,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 256,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("é😁😃x😀");
        var first = regex.MatchDetailed(input);
        var group = first.GetGroup(1);
        Assert.Equal("😁😃", group.GetValueString());
        Assert.Equal((2, 10, 1, 5), (group.StartOffsetInBytes, group.EndOffsetInBytes,
            group.StartOffsetInUtf16, group.EndOffsetInUtf16));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("é<😁😃>x<😀>", regex.ReplaceToString(input, "<$1>"));
        Assert.Equal("😁😃", group.GetValueString());

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, 0, Pcre2MatchOptions.None, canceled.Token)).CancellationToken);
        var denied = Utf8Pcre2Regex.Create("([😀-😃]+)", Pcre2CompileOptions.Ucp, default,
            new() { MaxWorkSteps = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => denied.Count(input)).ErrorKind);
    }
}
