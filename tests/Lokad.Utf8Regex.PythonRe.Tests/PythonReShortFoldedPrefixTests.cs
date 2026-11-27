using System.Text;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReShortFoldedPrefixTests
{
    private static Utf8ResourcePolicy Generous => new()
    {
        MaxDepth = 1024,
        MaxWorkingBytes = 1 << 20,
        MaxResultBytes = 1 << 20
    };

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(220)]
    [InlineData(230)]
    [InlineData(248)]
    [InlineData(255)]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(4097)]
    public void PublicSearchAndCapturesKeepUnicodePartnersAndNonzeroStarts(int padding)
    {
        var text = "π" + new string('x', padding) + " watson sxx ſherlocK xx SHERLOCK";
        var bytes = Encoding.UTF8.GetBytes(text);
        var storage = new byte[bytes.Length + 9];
        bytes.CopyTo(storage, 5);
        var input = storage.AsSpan(5, bytes.Length);
        var first = text.IndexOf('ſ');
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        {
            foreach (var start in new[] { 0, 2, bytes.Length })
            {
                using var token = new CancellationTokenSource();
                using var owner = new Utf8PythonRegex("(?P<n>sherlock)", options: PythonReCompileOptions.IgnoreCase, policy: new Utf8RegexPolicy()
                {
                    MatchTimeout = Timeout.InfiniteTimeSpan,
                    ResourcePolicy = policy
                });
                var expected = start == bytes.Length ? Array.Empty<string>() : ["ſherlocK", "SHERLOCK"];
                var actual = owner.Match(input, start, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search);
                Assert.Equal(expected.Length != 0, actual.Success);
                if (actual.Success)
                {
                    Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, first)), actual.StartOffsetInBytes);
                    Assert.Equal(Encoding.UTF8.GetByteCount(expected[0]), actual.EndOffsetInBytes - actual.StartOffsetInBytes);
                }
                Assert.Equal(expected.Length, owner.Count(input, start, token.Token));
                ReadOnlyMemory<byte> snapshots1Input = (input).ToArray();
                var snapshots1 = new List<Utf8PythonRangeMatch>();
                using (var snapshots1Cursor = owner.CreateMatchCursor(snapshots1Input, start, token.Token))
                {
                    while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                        snapshots1.Add(snapshots1Match);
                }
                Assert.Equal(expected, snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
            }
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
            using var owner = new Utf8PythonRegex("(?P<n>" + pattern + ")", options: PythonReCompileOptions.IgnoreCase, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });
            PythonReMatchAssert.Equal(expected, owner.Match(Encoding.UTF8.GetBytes(text), mode: Utf8PythonMatchMode.Search));
        }
    }

    [Fact]
    public void SmallPoliciesAllowBorrowedValuesWithoutTextAdmission()
    {
        using var token = new CancellationTokenSource();
        using var owner = new Utf8PythonRegex("sherlock", options: PythonReCompileOptions.IgnoreCase, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new()
            {
                MaxDepth = 1,
                MaxWorkingBytes = 1,
                MaxResultBytes = 1
            }
        });
        Assert.False(owner.Match("prefix watson suffix"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(owner.Match("SHERLOCK"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search).Success);
        PythonReMatchAssert.Equal("SHERLOCK", owner.Match("SHERLOCK"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search));
    }

    [Fact]
    public void EarlyHitPreservesOriginalCancellation()
    {
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        {
            using var owner = new Utf8PythonRegex("sherlock", options: PythonReCompileOptions.IgnoreCase, policy: new Utf8RegexPolicy()
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = policy
            });

            using var token = new CancellationTokenSource();
            token.Cancel();
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(
                () => owner.Match("SHERLOCK"u8, 0, cancellationToken: token.Token, mode: Utf8PythonMatchMode.Search)).CancellationToken);
        }
    }
}
