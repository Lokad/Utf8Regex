using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2OutputInvocationAuthorityTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var helper in new[] { false, true })
        {
            yield return ["(?<x>a)b", "ab", helper];
            yield return [@"(?<x>a)\k<x>", "aa", helper];
            yield return [@"(?<x>\u0041+)", "AAA", helper];
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void EntryAndDiscoveryKeepOriginalToken(string pattern, string text, bool helper)
    {
        foreach (var atRead in new[] { 2, 5 })
        foreach (var hit in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            { if (++reads == atRead) source.Cancel(); return 0; });
            var input = Encoding.UTF8.GetBytes(hit ? text : "zzz");
            var destination = new Utf8Pcre2MatchData[4];
            var failure = Assert.Throws<OperationCanceledException>(() =>
            { _ = Run(regex, input, destination, helper, source.Token); });
            Assert.Equal(source.Token, failure.CancellationToken);
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void InitialValidationCannotReachMalformedTailAfterWorkDenial(string pattern, string text, bool helper)
    {
        var input = new byte[16385]; input.AsSpan().Fill((byte)'x'); input[^1] = 0xff;
        var destination = new Utf8Pcre2MatchData[4];
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Run(Create(pattern, 3), input, destination, helper); }).ErrorKind);
        Assert.All(destination, item => Assert.False(item.Success));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void WholeSubjectValidityAndFreshSufficientCallsPreserveNamesAndRanges(string pattern, string text, bool helper)
    {
        var regex = Create(pattern, 100000);
        var prefix = "😀";
        var input = Encoding.UTF8.GetBytes(prefix + text);
        var destination = new Utf8Pcre2MatchData[4];
        Assert.Equal(1, Run(regex, input, destination, helper));
        if (!helper)
            Assert.Equal((4, input.Length), (destination[0].StartOffsetInBytes, destination[0].EndOffsetInBytes));
        var match = regex.MatchDetailed(input);
        Assert.True(match.TryGetFirstSetGroup("x", out var retained));
        var captured = retained.GetValueString();
        var malformed = new byte[input.Length + 1]; input.CopyTo(malformed, 0); malformed[^1] = 0xff;
        var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(malformed));
        var actual = Assert.Throws<ArgumentException>(() => { _ = Run(regex, malformed, destination, helper); });
        Assert.Equal(expected.Message, actual.Message);
        Assert.Equal(1, Run(regex, input, destination, helper));
        Assert.Equal(captured, retained.GetValueString());
    }

    [Theory] [MemberData(nameof(Routes))]
    public void WorkingAndResultAdmissionPreserveCallerStorageAndFreshInvocations(string pattern, string text, bool helper)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var destination = new Utf8Pcre2MatchData[4];
        Utf8Pcre2Regex Policy(ulong heap, ulong result) => new(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = heap, ResultLimitInBytes = result },
            Timeout.InfiniteTimeSpan);
        // Native MatchMany publishes value ranges into caller storage without
        // acquiring owned scratch. A tiny heap allowance must not deny that path.
        if (!helper && pattern == "(?<x>a)b")
            Assert.Equal(1, Run(Policy(1, 1 << 20), input, destination, false));
        else
        {
            Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
            { _ = Run(Policy(1, 1 << 20), input, destination, helper); }).ErrorKind);
            Assert.All(destination, item => Assert.False(item.Success));
        }
        if (helper)
            Assert.Equal(Pcre2ErrorKind.ResultLimit, Assert.Throws<Pcre2MatchException>(() =>
            { _ = Run(Policy(1 << 20, 1), input, destination, true); }).ErrorKind);
        else
            Assert.Equal(1, Run(Policy(1 << 20, 1), input, destination, false));
        Assert.Equal(1, Run(Policy(1 << 20, 1 << 20), input, destination, helper));
    }

    [Theory]
    [InlineData("abc", "abc abc", 0, 0, true)]
    [InlineData("abc", "abc abc", 1, 1, true)]
    [InlineData("abc", "abc abc", 2, 2, false)]
    [InlineData("abc", "abc abc", 4, 2, false)]
    [InlineData("|.", "😀", 2, 2, true)]
    [InlineData("|.", "😀", 3, 3, false)]
    public void CallerCapacityAndEmptyScalarProgressKeepIsMore(string pattern, string text, int capacity, int expected, bool more)
    {
        var regex = Create(pattern, 100000);
        var destination = new Utf8Pcre2MatchData[capacity];
        Assert.Equal(expected, regex.MatchMany(Encoding.UTF8.GetBytes(text), destination, out var actualMore));
        Assert.Equal(more, actualMore);
        Assert.All(destination.Take(expected), item => Assert.True(item.Success));
    }

    [Fact]
    public void CancellationAfterPrefixPublicationKeepsEarlierDestinationAndSourceValid()
    {
        var input = "abc abc"u8.ToArray();
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default,
            default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
        var destination = new Utf8Pcre2MatchData[4];
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (destination[0].Success) source.Cancel(); return 0; });
        var failure = Assert.Throws<OperationCanceledException>(() =>
        { _ = regex.MatchMany(input, destination, out _, 0, Pcre2MatchOptions.None, source.Token); });
        Assert.Equal(source.Token, failure.CancellationToken);
        Assert.True(destination[0].Success);
        Assert.Equal((0, 3), (destination[0].StartOffsetInBytes, destination[0].EndOffsetInBytes));
        Assert.False(destination[1].Success);
        Assert.Equal("abc abc", Encoding.UTF8.GetString(input));
    }

    private static Utf8Pcre2Regex Create(string pattern, uint work) => new(pattern,
        Pcre2CompileOptions.None, default, new() { MatchLimit = work, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);

    private static int Run(Utf8Pcre2Regex regex, byte[] input, Utf8Pcre2MatchData[] destination,
        bool helper, CancellationToken token = default) => helper
        ? (regex.TryGetFirstSetGroup(input, "x", out _, 0, Pcre2MatchOptions.None, token) ? 1 : 0)
        : regex.MatchMany(input, destination, out _, 0, Pcre2MatchOptions.None, token);
}
