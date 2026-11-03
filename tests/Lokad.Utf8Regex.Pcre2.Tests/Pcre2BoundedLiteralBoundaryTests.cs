using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BoundedLiteralBoundaryTests
{
    private const string Date = @"\b\d{1,2}/\d{1,2}/\d{2,4}\b";

    [Theory]
    [InlineData(Date, "😀1/2/2024é 11/18/201A 12/3/2025😀")]
    [InlineData(@"\B\d{1,2}/", "a1/ 2/ _12/ 😀34/")]
    [InlineData(@"\b([0-9]{1,2})/(?<tail>[0-9]{1,2})", "é1/2😀 123/45 6/78")]
    [InlineData(@"\b[\x00-\x7f]{1,3}foo", "😀xfoo\0afooé abfoo")]
    [InlineData(@"\b[0-9]{1,2}\B/", "1/ a12/ 😀3/")]
    [InlineData(@"\b[0-9]{1,64}/", "é" + "12345678901234567890/ 1/")]
    public void WindowPreservesPublicResultsAtEveryScalarOrigin(string pattern, string text)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Equal(Pcre2CandidateSearchKind.BoundedLiteralWindow, regex.DebugCompiledProgram.CandidateSearch.Kind);
        var bytes = Encoding.UTF8.GetBytes(text);
        // Each pattern consumes input. NotEmpty disables candidate search without
        // changing its results, providing the production VM as the control.
        for (var start = 0; start <= bytes.Length; start++)
        {
            if (start < bytes.Length && (bytes[start] & 0xc0) == 0x80) continue;
            Assert.Equal(regex.IsMatch(bytes, start, Pcre2MatchOptions.NotEmpty), regex.IsMatch(bytes, start));
            Assert.Equal(regex.Count(bytes, start, Pcre2MatchOptions.NotEmpty), regex.Count(bytes, start));
            var expected = regex.MatchDetailed(bytes, start, Pcre2MatchOptions.NotEmpty);
            var actual = regex.MatchDetailed(bytes, start);
            Assert.Equal(expected.Success, actual.Success);
            Assert.Equal(expected.CaptureSlotCount, actual.CaptureSlotCount);
            for (var slot = 0; slot < expected.CaptureSlotCount; slot++)
            {
                var left = expected.GetGroup(slot); var right = actual.GetGroup(slot);
                Assert.Equal(left.Success, right.Success);
                Assert.Equal((left.StartOffsetInBytes, left.EndOffsetInBytes),
                    (right.StartOffsetInBytes, right.EndOffsetInBytes));
                Assert.Equal(left.GetValueString(), right.GetValueString());
            }
        }
    }

    [Theory]
    [InlineData(@"\b[0-9]{1,65}/", Pcre2CompileOptions.None)]
    [InlineData(@"\b[0-9]+/", Pcre2CompileOptions.None)]
    [InlineData(@"\b[é]{1,2}/", Pcre2CompileOptions.None)]
    [InlineData(Date, Pcre2CompileOptions.Ucp)]
    [InlineData(Date, Pcre2CompileOptions.Caseless)]
    public void ExistingWindowEligibilityLimitsRemain(string pattern, Pcre2CompileOptions options)
    {
        var regex = new Utf8Pcre2Regex(pattern, options);
        Assert.Equal(Pcre2CandidateSearchKind.None, regex.DebugCompiledProgram.CandidateSearch.Kind);
    }

    [Fact]
    public void FullSubjectValidationAndOriginalCancellationRemainAuthoritative()
    {
        var regex = new Utf8Pcre2Regex(Date);
        byte[] invalid = [.. "1/2/2024 "u8, 0xff];
        Assert.Throws<ArgumentException>(() => regex.IsMatch(invalid));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(invalid, 0, Pcre2MatchOptions.None, canceled.Token));
        Assert.Equal(canceled.Token, failure.CancellationToken);
    }
}
