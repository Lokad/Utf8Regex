using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReShortFoldedPrefixTests
{
    private static Utf8ResourcePolicy Generous => new() {
        MaxWorkSteps = 1_000_000, MaxDepth = 1024,
        MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20
    };

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(220)] [InlineData(230)]
    [InlineData(248)] [InlineData(255)] [InlineData(256)] [InlineData(257)] [InlineData(4097)]
    public void PublicSearchAndCapturesKeepUnicodePartnersAndNonzeroStarts(int padding)
    {
        var text = "π" + new string('x', padding) + " watson sxx ſherlocK xx SHERLOCK";
        var bytes = Encoding.UTF8.GetBytes(text); var storage = new byte[bytes.Length + 9]; bytes.CopyTo(storage, 5);
        var input = storage.AsSpan(5, bytes.Length);
        var first = text.IndexOf('ſ');
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        foreach (var start in new[] { 0, 2, bytes.Length })
        {
            using var token = new CancellationTokenSource();
            using var owner = new Utf8PythonRegex("(?P<n>sherlock)", PythonReCompileOptions.IgnoreCase,
                Timeout.InfiniteTimeSpan, policy);
            var expected = start == bytes.Length ? Array.Empty<string>() : ["ſherlocK", "SHERLOCK"];
            var actual = owner.Search(input, start, token.Token);
            Assert.Equal(expected.Length != 0, actual.Success);
            if (actual.Success)
            {
                Assert.Equal(first, actual.StartOffsetInUtf16);
                Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, first)), actual.StartOffsetInBytes);
                Assert.Equal(Encoding.UTF8.GetByteCount(expected[0]), actual.EndOffsetInBytes - actual.StartOffsetInBytes);
            }
            Assert.Equal(expected.Length, owner.Count(input, start, token.Token));
            Assert.Equal(expected, owner.FindAllToStrings(input, start, token.Token).ScalarValues);
        }
    }

    [Theory]
    [InlineData("sherlock|she", "SHErlock shex", "SHErlock")]
    [InlineData("she|sherlock", "SHErlock shex", "SHE")]
    [InlineData("sherlock", "sxx shex ſherlocK", "ſherlocK")]
    public void CandidatePrefixDoesNotChooseAnOrderedMatch(string pattern, string text, string expected)
    {
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        {
            using var owner = new Utf8PythonRegex("(?P<n>" + pattern + ")", PythonReCompileOptions.IgnoreCase,
                Timeout.InfiniteTimeSpan, policy);
            Assert.Equal(expected, owner.SearchToString(Encoding.UTF8.GetBytes(text)));
        }
    }

    [Fact]
    public void SmallPoliciesAllowFixedValuesButStillAdmitPublishedText()
    {
        using var token = new CancellationTokenSource();
        using var owner = new Utf8PythonRegex("sherlock", PythonReCompileOptions.IgnoreCase,
            Timeout.InfiniteTimeSpan, new() {
                MaxWorkSteps = 10000, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1
            });
        Assert.False(owner.Search("prefix watson suffix"u8, 0, token.Token).Success);
        Assert.True(owner.Search("SHERLOCK"u8, 0, token.Token).Success);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(
                () => owner.SearchToString("SHERLOCK"u8, 0, token.Token)).ResourceKind);
    }

    [Fact]
    public void EarlyHitStillRejectsMalformedTailAndPreservesOriginalCancellation()
    {
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        {
            using var owner = new Utf8PythonRegex("sherlock", PythonReCompileOptions.IgnoreCase,
                Timeout.InfiniteTimeSpan, policy);
            var malformed = "SHERLOCK"u8.ToArray().Append((byte)0xff).ToArray();
            Assert.Contains("byte offset 8.", Assert.Throws<ArgumentException>(() => owner.Search(malformed)).Message);
            using var token = new CancellationTokenSource(); token.Cancel();
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(
                () => owner.Search(malformed, 0, token.Token)).CancellationToken);
        }
    }
}
