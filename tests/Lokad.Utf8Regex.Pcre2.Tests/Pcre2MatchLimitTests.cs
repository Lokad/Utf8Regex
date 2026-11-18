using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Search and owned cursor publication share one work allowance; sufficient
// primary-match budgets still return the first hit.
public sealed class Pcre2MatchLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
    }

    [Fact]
    public void BacktrackingCursorBreachesUnderTinyLimit()
    {
        var input = Encoding.UTF8.GetBytes("aaab aaab");
        var regex = Create("(a+)+b", new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result1Input = input;
            using var result1Cursor = regex.CreateMatchCursor(result1Input);
            result1Cursor.MoveNext(out var result1Result);
            result1Cursor.Dispose();
            _ = result1Result;
        }).ErrorKind);

    }

    [Fact]
    public void LiteralSearchAndSnapshotPublicationKeepWorkLimits()
    {
        var input = Encoding.UTF8.GetBytes("foo foo foo");
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() =>
            {
                var result3Regex = Create("foo", new Utf8Pcre2ExecutionLimits { MatchLimit = 2 });
                ReadOnlyMemory<byte> result3Input = input;
                using var result3Cursor = result3Regex.CreateMatchCursor(result3Input);
                result3Cursor.MoveNext(out var result3Result);
                result3Cursor.Dispose();
                _ = result3Result;
            }).ErrorKind);
        var regex = Create("foo", new Utf8Pcre2ExecutionLimits { MatchLimit = 64 });
        var actual = regex.Match(input);
        Assert.True(actual.Success);
        var expected = Create("foo", default).Match(input);
        Assert.Equal(System.Text.Encoding.UTF8.GetString(expected.GetValueBytes()), System.Text.Encoding.UTF8.GetString(actual.GetValueBytes()));
    }
}
