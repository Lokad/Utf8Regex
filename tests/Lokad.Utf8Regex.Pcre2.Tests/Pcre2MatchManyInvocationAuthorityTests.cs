using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2MatchManyInvocationAuthorityTests
{
    public static IEnumerable<object[]> Routes()
    {
        yield return ["(?<x>a)b", "ab"];
        yield return [@"(?<x>a)\k<x>", "aa"];
        yield return [@"(?<x>\x41+)", "AAA"];
    }

    [Theory] [MemberData(nameof(Routes))]
    public void EntryAndDiscoveryKeepOriginalToken(string pattern, string text)
    {
        foreach (var atRead in new[] { 2, 5 })
        foreach (var hit in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromMilliseconds(20)
            });
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            { if (++reads == atRead) source.Cancel(); return 0; });
            var input = Encoding.UTF8.GetBytes(hit ? text : "zzz");
            var destination = new Utf8Pcre2MatchData[4];
            var failure = Assert.Throws<OperationCanceledException>(() =>
            { _ = regex.MatchMany(input, destination, out _, cancellationToken: source.Token); });
            Assert.Equal(source.Token, failure.CancellationToken);
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void SearchStopsAtWorkDenial(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 16385) + text);
        var destination = new Utf8Pcre2MatchData[4];
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, 3).MatchMany(input, destination, out _); }).ErrorKind);
        Assert.All(destination, item => Assert.False(item.Success));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void FreshSufficientCallsPreserveBorrowedNamesAndRanges(string pattern, string text)
    {
        var regex = Create(pattern, 100000);
        var prefix = "😀";
        var input = Encoding.UTF8.GetBytes(prefix + text);
        var destination = new Utf8Pcre2MatchData[4];
        Assert.Equal(1, regex.MatchMany(input, destination, out _));
        Assert.Equal((4, input.Length), (destination[0].StartOffsetInBytes, destination[0].EndOffsetInBytes));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var match));
        Assert.True(match.TryGetFirstSetGroup("x", out var retained));
        var captured = Encoding.UTF8.GetString(match.GetGroupMemory(retained.Number).Span);
        Assert.Equal(1, regex.MatchMany(input, destination, out _));
        Assert.Equal(captured, Encoding.UTF8.GetString(match.GetGroupMemory(retained.Number).Span));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void WorkingAndResultAdmissionPreserveCallerStorageAndFreshInvocations(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var destination = new Utf8Pcre2MatchData[4];
        Utf8Pcre2Regex Policy(ulong heap, ulong result) => new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 100000,
                MaxDepth = 256,
                MaxWorkingBytes = (heap) == 0 ? null : (heap),
                MaxResultBytes = (result) == 0 ? null : (result)
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        // Native MatchMany publishes value ranges into caller storage without
        // acquiring owned scratch. A tiny heap allowance must not deny that path.
        if (pattern == "(?<x>a)b")
            Assert.Equal(1, Policy(1, 1 << 20).MatchMany(input, destination, out _));
        else
        {
            Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
            { _ = Policy(1, 1 << 20).MatchMany(input, destination, out _); }).ErrorKind);
            Assert.All(destination, item => Assert.False(item.Success));
        }
        Assert.Equal(1, Policy(1 << 20, 1).MatchMany(input, destination, out _));
        Assert.Equal(1, Policy(1 << 20, 1 << 20).MatchMany(input, destination, out _));
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
        var regex = new Utf8Pcre2Regex("abc", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20)
        });
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

    private static Utf8Pcre2Regex Create(string pattern, uint work) => new(pattern, options:         Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
    {
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = (work) == 0 ? null : (work),
            MaxDepth = 256,
            MaxWorkingBytes = 1 << 20,
            MaxResultBytes = 1 << 20
        },
        MatchTimeout = Timeout.InfiniteTimeSpan
    });

}
