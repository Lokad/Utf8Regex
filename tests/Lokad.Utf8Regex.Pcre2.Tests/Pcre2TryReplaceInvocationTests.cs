using System.Buffers;
using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2TryReplaceInvocationTests
{
    public static IEnumerable<object[]> Routes()
    {
        yield return ["abc", "xabc", "Z", "xZ"];
        yield return ["(?<x>a)b", "xab", "${x}", "xa"];
        yield return [@"(?<x>a)\k<x>", "xaa", "${x}", "xa"];
        yield return [@"(?<x>\u0041+)", "xAAA", "${x}", "xAAA"];
        yield return ["abc", "xabc", "$0", "xabc"];
    }

    [Theory] [MemberData(nameof(Routes))]
    public void EntryDiscoveryAndPublicationObserveOriginalToken(string pattern, string text, string template, string expected)
    {
        foreach (var cancelAt in new[] { 2, 8 })
        foreach (var hit in new[] { true, false })
        {
            using var source = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
            var input = Encoding.UTF8.GetBytes(hit ? text : "zzz");
            var replacement = Encoding.UTF8.GetBytes(template);
            var destination = new byte[64]; destination.AsSpan().Fill(0xcc);
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            { if (++reads == cancelAt) source.Cancel(); return 0; });
            var error = Assert.Throws<OperationCanceledException>(() =>
            { _ = regex.TryReplace(input, replacement, destination, out _, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, source.Token); });
            Assert.Equal(source.Token, error.CancellationToken);
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void ValidationDenialPrecedesMalformedSubjectAndTemplateTails(string pattern, string text, string template, string expected)
    {
        var malformed = new byte[16385]; malformed.AsSpan().Fill((byte)'x'); malformed[^1] = 0xff;
        var destination = new byte[64]; destination.AsSpan().Fill(0xcc);
        foreach (var templateTail in new[] { true, false })
        {
            var input = templateTail ? Encoding.UTF8.GetBytes(text) : malformed;
            var replacement = templateTail ? malformed : Encoding.UTF8.GetBytes(template);
            var error = Assert.Throws<Pcre2MatchException>(() =>
            { _ = Policy(pattern, 3).TryReplace(input, replacement, destination, out _); });
            Assert.Equal(Pcre2ErrorKind.MatchLimit, error.ErrorKind);
            Assert.All(destination, value => Assert.Equal((byte)0xcc, value));
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void ScalarOffsetsCapacityAndFreshCallsKeepOutput(string pattern, string text, string template, string expected)
    {
        var regex = Policy(pattern, 100000);
        var input = Encoding.UTF8.GetBytes("😀" + text);
        var replacement = Encoding.UTF8.GetBytes(template);
        var expectedBytes = Encoding.UTF8.GetBytes("😀" + expected);
        var small = new byte[expectedBytes.Length - 1]; small.AsSpan().Fill(0xcc);
        Assert.Equal(OperationStatus.DestinationTooSmall, regex.TryReplace(input, replacement, small, out var required,
            4, Pcre2SubstitutionOptions.SubstituteOverflowLength, Pcre2MatchOptions.None));
        Assert.Equal(expectedBytes.Length, required);
        Assert.All(small, value => Assert.Equal((byte)0xcc, value));
        var destination = new byte[expectedBytes.Length];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, replacement, destination, out var written,
            4, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None));
        Assert.Equal(expectedBytes.Length, written);
        Assert.Equal(expectedBytes, destination);
        var malformed = new byte[input.Length + 1]; input.CopyTo(malformed, 0); malformed[^1] = 0xff;
        var error = Assert.Throws<ArgumentException>(() => regex.TryReplace(malformed, replacement, destination, out _));
        Assert.Equal(Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(malformed)).Message, error.Message);
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, replacement, destination, out _, 4, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None));
    }

    [Fact]
    public void NoMatchOverlappingCopyKeepsMemmoveSemantics()
    {
        var storage = new byte[16385]; storage.AsSpan(0, 16384).Fill((byte)'x'); storage[^1] = 0xcc;
        var regex = Policy("abc", 100000);
        Assert.Equal(OperationStatus.Done, regex.TryReplace(storage.AsSpan(0, 16384), "Z"u8,
            storage.AsSpan(1), out var written));
        Assert.Equal(16384, written);
        Assert.All(storage, value => Assert.Equal((byte)'x', value));
    }

    [Theory] [MemberData(nameof(Routes))]
    public void WorkingAndResultDenialLeaveCallerStorageAndFreshSuccess(string pattern, string text, string template, string expected)
    {
        var input = Encoding.UTF8.GetBytes(text); var replacement = Encoding.UTF8.GetBytes(template);
        var destination = new byte[64]; destination.AsSpan().Fill(0xcc);
        foreach (var heap in new[] { true, false })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = heap ? 1UL : 1UL << 20,
                    ResultLimitInBytes = heap ? 1UL << 20 : 1UL }, Timeout.InfiniteTimeSpan);
            Assert.Equal(heap ? Pcre2ErrorKind.HeapLimit : Pcre2ErrorKind.ResultLimit,
                Assert.Throws<Pcre2MatchException>(() => regex.TryReplace(input, replacement, destination, out _)).ErrorKind);
            Assert.All(destination, value => Assert.Equal((byte)0xcc, value));
        }
        Assert.Equal(OperationStatus.Done, Policy(pattern, 100000).TryReplace(input, replacement, destination, out var written));
        Assert.Equal(expected, Encoding.UTF8.GetString(destination.AsSpan(0, written)));
    }

    [Fact]
    public void NoMatchCopyObservesCancellationAfterFirstBlockAndKeepsSubjectValid()
    {
        var input = new byte[16384]; input.AsSpan().Fill((byte)'x');
        var destination = new byte[input.Length]; destination.AsSpan().Fill(0xcc);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default,
            default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (destination[0] == (byte)'x') source.Cancel(); return 0; });
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() =>
        { _ = regex.TryReplace(input, "Z"u8, destination, out _, 0, Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, source.Token); }).CancellationToken);
        Assert.Equal((byte)'x', destination[0]);
        Assert.Equal((byte)0xcc, destination[^1]);
        Assert.All(input, value => Assert.Equal((byte)'x', value));
    }

    private static Utf8Pcre2Regex Policy(string pattern, uint work) => new(pattern,
        Pcre2CompileOptions.None, default, new() { MatchLimit = work, DepthLimit = 256,
            HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
}
