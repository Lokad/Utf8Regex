using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2QuotedClassTests
{
    // Frozen native PCRE2 10.47 UTF/UCP observations, without native test dependencies.
    [Theory]
    [InlineData(@"^[\Q(?<100>a)\E]$", "1", true)]
    [InlineData(@"^[\Q(?<100>a)\E]$", "x", false)]
    [InlineData(@"^[\Qa-z\E]$", "-", true)]
    [InlineData(@"^[\Qa-z\E]$", "m", false)]
    [InlineData(@"^[a-\Qz\E]$", "m", true)]
    [InlineData(@"^[\Qab\E-z]$", "m", true)]
    [InlineData(@"^[a\Q-\Ez]$", "-", true)]
    [InlineData(@"^[a\Q-\Ez]$", "m", false)]
    [InlineData(@"^[a-\Q\E]$", "-", true)]
    [InlineData(@"^[a-\Q\Ez]$", "m", true)]
    [InlineData(@"^[\Q]\E]$", "]", true)]
    [InlineData(@"^[\Q^\E]$", "^", true)]
    [InlineData(@"^[\Q\\E]$", @"\", true)]
    [InlineData(@"^[\Q\Qa\E]$", "Q", true)]
    [InlineData(@"^[\Q\C\E]$", @"\", true)]
    [InlineData(@"^[\Q[\C]\E]$", "]", true)]
    [InlineData(@"^[\Qa-b\E-z]$", "-", true)]
    [InlineData(@"^[\Qa-b\E-z]$", "m", true)]
    [InlineData(@"^[\Q\E^a]$", "a", false)]
    [InlineData(@"^[\Q\E^a]$", "^", true)]
    [InlineData(@"^[\E^a]$", "a", false)]
    [InlineData(@"^[\E^a]$", "^", true)]
    [InlineData(@"^[\Q😀-😃\E]$", "-", true)]
    [InlineData(@"^[\Q😀-😃\E]$", "😀", true)]
    [InlineData(@"^[\Q😀-😃\E]$", "😁", false)]
    [InlineData(@"^[😀-\Q😃\E]$", "😁", true)]
    [InlineData(@"^[^\Q😀]\E]$", "]", false)]
    [InlineData(@"^[^\Q😀]\E]$", "😁", true)]
    [InlineData(@"(?xx)^[ \Q a-] \E ]$", " ", true)]
    [InlineData(@"(?xx)^[ \Q a-] \E ]$", "]", true)]
    [InlineData(@"(?xx)^[ a - \Qz\E ]$", "m", true)]
    [InlineData(@"^[a\E-z]$", "m", true)]
    [InlineData(@"^[a\Q\E-z]$", "m", true)]
    [InlineData(@"^[\Q\E-a]$", "-", true)]
    [InlineData(@"^\Qa\E+$", "aa", true)]
    [InlineData(@"^(?=[\Q\K\E])\Q\\E$", @"\", true)]
    public void QuotedClassMembershipMatchesScalarOracle(string pattern, string text, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var finite in new[] { false, true })
        {
            var limits = finite ? new Utf8Pcre2ExecutionLimits
            {
                MatchLimit = 1_000_000, DepthLimit = 256,
                HeapLimitInBytes = 1_048_576, ResultLimitInBytes = 1_048_576,
            } : default;
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Ucp, default,
                limits, Timeout.InfiniteTimeSpan);
            var bytes = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), Pcre2CompileOptions.Ucp,
                default, limits, Timeout.InfiniteTimeSpan);
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.Equal(expected, bytes.IsMatch(input));
        }
    }

    [Theory]
    [InlineData(@"^[\Q\E]$", Pcre2ErrorKind.MissingCharacterClassTerminator)]
    [InlineData(@"^[\Qabc]$", Pcre2ErrorKind.MissingCharacterClassTerminator)]
    [InlineData(@"^[\E]$", Pcre2ErrorKind.MissingCharacterClassTerminator)]
    [InlineData(@"^\Q\E+$", Pcre2ErrorKind.QuantifierInvalid)]
    public void EmptyAndUnterminatedQuotesRetainStructuredErrors(string pattern, Pcre2ErrorKind kind)
    {
        Assert.Equal(kind, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern)).ErrorKind);
    }

    [Theory]
    [InlineData(@"[\Q\C\E]\C", Pcre2ErrorKind.BackslashCDisabled)]
    [InlineData(@"[\Q(?C]\E](?C)", Pcre2ErrorKind.CalloutUnsupported)]
    public void ActualCodeAfterQuotedClassesKeepsItsValidation(string pattern, Pcre2ErrorKind kind)
    {
        Assert.Equal(kind, Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex(pattern)).ErrorKind);
    }

    [Fact]
    public void CapturedQuotedClassKeepsScalarRangesAndFiniteAuthority()
    {
        var regex = Utf8Pcre2Regex.Create(@"([\Q😀-😃\E]+)", Pcre2CompileOptions.Ucp, default,
            new() { MaxWorkSteps = 1_000_000, MaxDepth = 256,
                MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("é😀-😃x😀");
        var first = regex.MatchDetailed(input);
        var group = first.GetGroup(1);
        Assert.Equal("😀-😃", group.GetValueString());
        Assert.Equal((2, 11, 1, 6), (group.StartOffsetInBytes, group.EndOffsetInBytes,
            group.StartOffsetInUtf16, group.EndOffsetInUtf16));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("é<😀-😃>x<😀>", regex.ReplaceToString(input, "<$1>"));
        Assert.Equal("😀-😃", group.GetValueString());
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, Pcre2MatchOptions.None, cancellation.Token)).CancellationToken);
    }
}
