using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2SpacePropertyTests
{
    // PCRE2 10.47 PT_SPACE/PT_PXSPACE share HSPACE_CASES/VSPACE_CASES.
    // In particular, U+0085 and U+180E are included, unlike a category-Z test.
    [Theory]
    [InlineData(0, false)]
    [InlineData(8, false)]
    [InlineData(9, true)]
    [InlineData(10, true)]
    [InlineData(11, true)]
    [InlineData(12, true)]
    [InlineData(13, true)]
    [InlineData(0x1C, false)]
    [InlineData(0x1F, false)]
    [InlineData(0x20, true)]
    [InlineData(0x85, true)]
    [InlineData(0x9F, false)]
    [InlineData(0xA0, true)]
    [InlineData(0x1680, true)]
    [InlineData(0x180E, true)]
    [InlineData(0x2000, true)]
    [InlineData(0x200A, true)]
    [InlineData(0x200B, false)]
    [InlineData(0x2028, true)]
    [InlineData(0x2029, true)]
    [InlineData(0x202F, true)]
    [InlineData(0x205F, true)]
    [InlineData(0x3000, true)]
    [InlineData(0xFEFF, false)]
    [InlineData(0x1F600, false)]
    public void UnicodeSpacePropertiesAndComplementsFollowNativeBoundaries(int scalar, bool expected)
    {
        var value = char.ConvertFromUtf32(scalar);
        var input = Encoding.UTF8.GetBytes("before:" + value + "tail");
        var subject = input.AsSpan(7, Encoding.UTF8.GetByteCount(value));
        using var source = new CancellationTokenSource();
        foreach (var ucp in new[] { false, true })
        foreach (var body in new[] { @"\p{Xps}", @"\p{Xsp}", @"\P{Xps}", @"\P{Xsp}", @"[\p{Xps}]", @"[^\p{Xsp}]", @"\s", @"[[:space:]]" })
        {
            var hit = body is @"\P{Xps}" or @"\P{Xsp}" or @"[^\p{Xsp}]" ? !expected : expected;
            if (body is @"\s" or @"[[:space:]]") hit &= ucp || scalar < 128;
            foreach (var finite in new[] { false, true })
            {
                var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1000000,
                    MaxDepth = 256, MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 } : default;
                var regex = Utf8Pcre2Regex.Create(@"\A(?:" + body + @")\z", ucp ? Pcre2CompileOptions.Ucp : Pcre2CompileOptions.None,
                    default, policy, Timeout.InfiniteTimeSpan);
                Assert.Equal(hit, regex.IsMatch(subject, 0, Pcre2MatchOptions.None, source.Token));
                Assert.Equal(hit ? 1 : 0, regex.Count(subject, 0, Pcre2MatchOptions.None, source.Token));
                var match = regex.MatchDetailed(subject, 0, Pcre2MatchOptions.None, source.Token);
                Assert.Equal(hit, match.Success);
                if (hit)
                {
                    Assert.Equal(value, match.GetValueString());
                    Assert.Equal(subject.Length, match.Value.EndOffsetInBytes);
                }
            }
        }
    }

    [Theory]
    [InlineData(Pcre2CompileOptions.None)]
    [InlineData(Pcre2CompileOptions.Ucp)]
    public void BytePatternsKeepComplementCapturesAndScalarCoordinates(Pcre2CompileOptions options)
    {
        var pattern = Encoding.UTF8.GetBytes(@"(?<space>\p{Xps}+)(?<other>\P{Xsp}+)");
        var regex = Utf8Pcre2Regex.Compile(pattern, options, default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 },
            new Utf8ConstructionPolicy { MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxInstructionCapacity = 65536, MaxBytes = 1048576 },
            Timeout.InfiniteTimeSpan, CancellationToken.None);
        var input = Encoding.UTF8.GetBytes("z\u0085\u180E😀! \u0085a");
        var retained = regex.MatchDetailed(input, 1);
        Assert.True(retained.Success);
        Assert.Equal("\u0085\u180E", retained.GetGroup(1).GetValueString());
        Assert.Equal("😀!", retained.GetGroup(2).GetValueString());
        Assert.Equal((1, 6), (retained.GetGroup(1).StartOffsetInBytes, retained.GetGroup(1).EndOffsetInBytes));
        Assert.Equal((1, 11), (retained.Value.StartOffsetInBytes, retained.Value.EndOffsetInBytes));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("\u0085\u180E", retained.GetGroup(1).GetValueString());
    }

    [Fact]
    public void PropertyLoopsRetainWorkAndResultAdmission()
    {
        const string pattern = @"(?<x>\p{Xsp}+)";
        var input = Encoding.UTF8.GetBytes(new string('\u0085', 4096));
        var work = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => work.Count(input)).ErrorKind);
        var result = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default,
            new Utf8ResourcePolicy { MaxResultBytes = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() => { _ = result.MatchDetailed(input); }).ErrorKind);
        // This route keeps greedy alternatives for the long run. Preserve the
        // denied smaller depth allowance as well as a funded successful call.
        var depth = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default,
            new Utf8ResourcePolicy { MaxDepth = 256 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.DepthLimit, Assert.Throws<Pcre2MatchException>(() => depth.Count(input)).ErrorKind);
        var funded = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxDepth = 8192,
                MaxWorkingBytes = 4 * 1048576, MaxResultBytes = 1048576 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(1, funded.Count(input));
        Assert.Equal("\u0085\u180E", funded.MatchDetailed("\u0085\u180E"u8).GetValueString());
    }
}
