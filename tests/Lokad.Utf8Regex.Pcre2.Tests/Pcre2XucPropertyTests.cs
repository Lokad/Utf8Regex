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
                    MatchLimit = 1000000, DepthLimit = 256,
                    HeapLimitInBytes = 1048576, ResultLimitInBytes = 1048576,
                } : default;
                var options = ucp ? Pcre2CompileOptions.Ucp : Pcre2CompileOptions.None;
                var fromText = new Utf8Pcre2Regex(pattern, options, default, limits, Timeout.InfiniteTimeSpan);
                var fromBytes = new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern), options, default, limits, Timeout.InfiniteTimeSpan);
                Assert.Equal(hit, fromText.IsMatch(subject, 0, Pcre2MatchOptions.None, token.Token));
                Assert.Equal(hit ? 1 : 0, fromBytes.Count(subject, 0, Pcre2MatchOptions.None, token.Token));
                var match = fromText.MatchDetailed(subject, 0, Pcre2MatchOptions.None, token.Token);
                Assert.Equal(hit, match.Success);
                if (hit)
                {
                    Assert.Equal(value, match.GetValueString());
                    Assert.Equal((0, subject.Length), (match.Value.StartOffsetInBytes, match.Value.EndOffsetInBytes));
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
        var regex = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.Ucp, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 }, Timeout.InfiniteTimeSpan);
        var retained = regex.MatchDetailed(input, 1, Pcre2MatchOptions.None, token.Token);
        Assert.True(retained.Success);
        Assert.Equal("😀$@`\u00A0é", retained.GetGroup(1).GetValueString());
        Assert.Equal("--", retained.GetGroup(2).GetValueString());
        Assert.Equal((1, 14), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal(2, regex.Count(input, 1, Pcre2MatchOptions.None, token.Token));
        var cursor = regex.EnumerateMatches(input, 1, Pcre2MatchOptions.None, token.Token);
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        Assert.True(alias.MoveNext());
        Assert.Equal(cursor.Current.StartOffsetInBytes, alias.Current.StartOffsetInBytes);
        Assert.True(cursor.MoveNext());
        Assert.Equal((14, 19), (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
        Assert.False(cursor.MoveNext());
        Assert.Equal("😀$@`\u00A0é", retained.GetGroup(1).GetValueString());
        Assert.Equal("--", retained.GetGroup(2).GetValueString());
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
        var limited = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.Ucp, default, policy, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("😀😀😀!");
        var error = Assert.Throws<Pcre2MatchException>(() => { _ = limited.MatchDetailed(input); });
        Assert.Equal(expected, error.ErrorKind);
        var funded = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.Ucp, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 }, Timeout.InfiniteTimeSpan);
        Assert.Equal("😀😀😀!", funded.MatchDetailed(input).GetValueString());
    }

    [Fact]
    public void MalformedSubjectsStillRejectAndOriginalCancellationPrecedesValidation()
    {
        var regex = new Utf8Pcre2Regex(@"\p{Xuc}+");
        byte[][] malformedSubjects =
        [
            [0xF0, 0x9F, 0x98, 0x80, 0xFF],
            [0xED, 0xA0, 0x80], // Surrogate, not a scalar.
            [0xED, 0xBF, 0xBF],
            [0xF4, 0x90, 0x80, 0x80], // Above U+10FFFF.
            [0xF0, 0x9F, 0x98],
        ];
        foreach (var malformed in malformedSubjects)
            Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        using var source = new CancellationTokenSource();
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.Count(malformedSubjects[0], 0, Pcre2MatchOptions.None, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(1, regex.Count("😀😀"u8));
    }

    [Fact]
    public void LookbehindCapturesUseTheWholePrecedingScalar()
    {
        var regex = Utf8Pcre2Regex.Create(@"(?<=(?<x>\p{Xuc}))(?<y>\P{Xuc})", Pcre2CompileOptions.Ucp,
            default, new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("z😀a𐐀!");
        var first = regex.MatchDetailed(input);
        Assert.True(first.Success);
        Assert.Equal("😀", first.GetGroup(1).GetValueString());
        Assert.Equal("a", first.GetGroup(2).GetValueString());
        Assert.Equal((1, 5), (first.GetGroup(1).StartOffsetInBytes, first.GetGroup(1).EndOffsetInBytes));
        Assert.Equal((5, 6), (first.Value.StartOffsetInBytes, first.Value.EndOffsetInBytes));
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
        var regex = Utf8Pcre2Regex.Create(@"(?<x>\p{Xuc}+)!", Pcre2CompileOptions.Ucp, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 }, TimeSpan.FromMilliseconds(20));
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
