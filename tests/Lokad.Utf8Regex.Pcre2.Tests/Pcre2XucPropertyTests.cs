using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2XucPropertyTests
{
    // PCRE2 10.47 f454e231, testinput7:1990 and PT_UCNC. Native UTF8
    // observations cover these boundaries, both UCP modes and caseless classes.
    [Theory]
    [InlineData(0x0000, false)]
    [InlineData(0x0023, false)]
    [InlineData(0x0024, true)]
    [InlineData(0x0025, false)]
    [InlineData(0x003F, false)]
    [InlineData(0x0040, true)]
    [InlineData(0x0041, false)]
    [InlineData(0x004B, false)]
    [InlineData(0x005F, false)]
    [InlineData(0x0060, true)]
    [InlineData(0x0061, false)]
    [InlineData(0x007F, false)]
    [InlineData(0x009F, false)]
    [InlineData(0x00A0, true)]
    [InlineData(0x0345, true)]
    [InlineData(0x180E, true)]
    [InlineData(0x212A, true)]
    [InlineData(0xD7FF, true)]
    [InlineData(0xE000, true)]
    [InlineData(0xFFFF, true)]
    [InlineData(0x10000, true)]
    [InlineData(0x1F600, true)]
    [InlineData(0x10FFFF, true)]
    public void PropertyComplementsAndCaselessClassesFollowNativeScalars(int scalar, bool expected)
    {
        var value = char.ConvertFromUtf32(scalar);
        var input = Encoding.UTF8.GetBytes("prefix" + value + "tail");
        var subject = input.AsSpan(6, Encoding.UTF8.GetByteCount(value));
        using var token = new CancellationTokenSource();
        foreach (var ucp in new[] { false, true })
            foreach (var body in new[] { @"\p{Xuc}", @"\P{Xuc}", @"[\p{Xuc}]", @"[^\p{Xuc}]", @"(?i:\p{Xuc})", @"(?i:[\p{Xuc}])" })
            {
                var hit = body is @"\P{Xuc}" or @"[^\p{Xuc}]" ? !expected : expected;
                var pattern = @"\A(?:" + body + @")\z";
                foreach (var finite in new[] { false, true })
                {
                    var limits = finite ? new Utf8Pcre2ExecutionLimits
                    {
                        MatchLimit = 1000000,
                        DepthLimit = 256,
                        HeapLimitInBytes = 1048576,
                        ResultLimitInBytes = 1048576
                    } : default;
                    var options = ucp ? Pcre2CompileOptions.Ucp : Pcre2CompileOptions.None;
                    var fromText = new Utf8Pcre2Regex(pattern, options: options, compileSettings: default, policy: new Utf8RegexPolicy
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
                    var fromBytes = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options: options, compileSettings: default, policy: new Utf8RegexPolicy
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
                    Assert.Equal(hit, fromText.IsMatch(subject, 0, Pcre2MatchOptions.None, token.Token));
                    Assert.Equal(hit ? 1 : 0, fromBytes.Count(subject, 0, Pcre2MatchOptions.None, token.Token));
                    var match = fromText.Match(subject, cancellationToken: token.Token);
                    Assert.Equal(hit, match.Success);
                    if (hit)
                    {
                        Assert.Equal(value, System.Text.Encoding.UTF8.GetString(match.GetValueBytes()));
                        Assert.Equal((0, subject.Length), (match.StartOffsetInBytes, match.EndOffsetInBytes));
                    }
                }
            }
    }

    [Theory]
    [InlineData(@"\p{Xuc}+")]
    [InlineData(@"\p{Xuc}+?")]
    [InlineData(@"\p{Xuc}++")]
    public void CapturedRunsAndComplementsKeepByteOffsetsAndRetainedGroups(string body)
    {
        var pattern = "(?<x>" + body + @")(?<y>\P{Xuc}+)";
        var input = Encoding.UTF8.GetBytes("z😀$@`\u00A0é--𐐀!");
        using var token = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 256,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        ReadOnlySpan<byte> retained2Input = input;
        var retained2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained2Input, startOffsetInBytes: 1, captures: retained2Captures, cancellationToken: token.Token);
        Assert.True(retained.Success);
        Assert.Equal("😀$@`\u00A0é", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("--", System.Text.Encoding.UTF8.GetString((retained2Captures[2].Success ? retained2Input.Slice(retained2Captures[2].StartOffsetInBytes, retained2Captures[2].EndOffsetInBytes - retained2Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal((1, 14), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
        Assert.Equal(2, regex.Count(input, 1, Pcre2MatchOptions.None, token.Token));
        var cursor = regex.EnumerateMatches(input, 1, Pcre2MatchOptions.None, token.Token);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current.StartOffsetInBytes, alias.Current.StartOffsetInBytes);
        Assert.True(cursor.MoveNext());
        Assert.Equal((14, 19), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.False(cursor.MoveNext());
        Assert.Equal("😀$@`\u00A0é", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("--", System.Text.Encoding.UTF8.GetString((retained2Captures[2].Success ? retained2Input.Slice(retained2Captures[2].StartOffsetInBytes, retained2Captures[2].EndOffsetInBytes - retained2Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Theory]
    [InlineData(0, Pcre2ErrorKind.MatchLimit)]
    [InlineData(1, Pcre2ErrorKind.DepthLimit)]
    [InlineData(2, Pcre2ErrorKind.HeapLimit)]
    [InlineData(3, Pcre2ErrorKind.ResultLimit)]
    public void PropertyExecutionPreservesFiniteAdmissionAndFreshCallRecovery(int control, Pcre2ErrorKind expected)
    {
        const string pattern = @"(?:(?<x>\p{Xuc})|(?<y>\p{Xuc}{2}))+!";
        var policy = control switch
        {
            0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxDepth = 1 },
            2 => new Utf8ResourcePolicy { MaxWorkingBytes = 1 },
            _ => new Utf8ResourcePolicy { MaxResultBytes = 1 },
        };
        var limited = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = policy,
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes("😀😀😀!");
        var error = Assert.Throws<Pcre2MatchException>(() =>
        {
            ReadOnlyMemory<byte> result3Input = input;
            using var result3Cursor = limited.CreateMatchCursor(result3Input);
            result3Cursor.MoveNext(out var result3Result);
            result3Cursor.Dispose();
            _ = result3Result;
        });
        Assert.Equal(expected, error.ErrorKind);
        var funded = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 256,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        Assert.Equal("😀😀😀!", System.Text.Encoding.UTF8.GetString(funded.Match(input).GetValueBytes()));
    }

    [Fact]
    public void OriginalCancellationPrecedesUnicodeMatching()
    {
        var regex = new Utf8Pcre2Regex(@"\p{Xuc}+");
        using var source = new CancellationTokenSource();
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Count("😀😀"u8, 0, Pcre2MatchOptions.None, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(1, regex.Count("😀😀"u8));
    }

    [Fact]
    public void LookbehindCapturesUseTheWholePrecedingScalar()
    {
        var regex = new Utf8Pcre2Regex(@"(?<=(?<x>\p{Xuc}))(?<y>\P{Xuc})", options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 256,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var input = Encoding.UTF8.GetBytes("z😀a𐐀!");
        ReadOnlySpan<byte> first5Input = input;
        var first5Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var first = regex.Match(first5Input, captures: first5Captures);
        Assert.True(first.Success);
        Assert.Equal("😀", System.Text.Encoding.UTF8.GetString((first5Captures[1].Success ? first5Input.Slice(first5Captures[1].StartOffsetInBytes, first5Captures[1].EndOffsetInBytes - first5Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((first5Captures[2].Success ? first5Input.Slice(first5Captures[2].StartOffsetInBytes, first5Captures[2].EndOffsetInBytes - first5Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal((1, 5), (first5Captures[1].StartOffsetInBytes, first5Captures[1].EndOffsetInBytes));
        Assert.Equal((5, 6), (first.StartOffsetInBytes, first.EndOffsetInBytes));
        Assert.Equal(2, regex.Count(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OngoingCallsRetainOriginalCancellationAndDeadline(bool timeout)
    {
        using var source = new CancellationTokenSource();
        long time = 0;
        var reads = 0;
        var armed = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (armed && ++reads == 4)
            {
                if (timeout) time = 25;
                else source.Cancel();
            }
            return time;
        });
        var regex = new Utf8Pcre2Regex(@"(?<x>\p{Xuc}+)!", options: Pcre2CompileOptions.Ucp, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 256,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            },
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
        var input = Encoding.UTF8.GetBytes(new string('a', 9000) + "😀!");
        armed = true;
        if (timeout)
            Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() =>
                regex.Count(input, 0, Pcre2MatchOptions.None, source.Token)).ErrorKind);
        else
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, 0, Pcre2MatchOptions.None, source.Token)).CancellationToken);
        Assert.True(reads >= 4);
        armed = false;
        time = 0;
        Assert.Equal(1, regex.Count("😀!"u8));
    }
}
