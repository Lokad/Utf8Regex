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
                MatchLimit = 1_000_000,
                DepthLimit = 256,
                HeapLimitInBytes = 1_048_576,
                ResultLimitInBytes = 1_048_576
            } : default;
            var fromText = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                    MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                    MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                    MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
            var fromBytes = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
            {
                ResourcePolicy = new Utf8ResourcePolicy
                {
                    MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                    MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                    MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                    MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
                },
                MatchTimeout = Timeout.InfiniteTimeSpan
            });
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
        var regex = new Utf8Pcre2Regex("([😀-😃]+)", options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1_000_000,
                MaxDepth = 256,
                MaxWorkingBytes = 1_048_576,
                MaxResultBytes = 1_048_576
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes("é😁😃x😀");
        ReadOnlySpan<byte> first1Input = input;
        var first1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var first = regex.Match(first1Input, captures: first1Captures);
        var group = first1Captures[1];
        Assert.Equal("😁😃", System.Text.Encoding.UTF8.GetString((group.Success ? first1Input.Slice(group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal((2, 10), (group.StartOffsetInBytes, group.EndOffsetInBytes));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("é<😁😃>x<😀>", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "<$1>")));
        Assert.Equal("😁😃", System.Text.Encoding.UTF8.GetString((group.Success ? first1Input.Slice(group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));

        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, 0, Pcre2MatchOptions.None, canceled.Token)).CancellationToken);
        var denied = new Utf8Pcre2Regex("([😀-😃]+)", options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => denied.Count(input)).ErrorKind);
    }
}
