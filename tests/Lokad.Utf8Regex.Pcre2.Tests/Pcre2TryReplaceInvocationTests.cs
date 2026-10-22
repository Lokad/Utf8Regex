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
        yield return [@"(?<x>\x41+)", "xAAA", "${x}", "xAAA"];
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

    public static IEnumerable<object[]> AliasingRoutes()
    {
        yield return ["a", "a😀z", "XYZ", "XYZ😀z"];
        yield return ["[a]", "a😀z", "$0$0", "aa😀z"];
        yield return ["a+", "aaa😀z", "Q", "Q😀z"];
        yield return ["(?<x>a)", "a😀z", "${x}${x}", "aa😀z"];
        yield return ["(?<x>a)b", "ab😀z", "${x}${x}", "aa😀z"];
        yield return ["a", "a😀z", "", "😀z"];
        yield return ["a", "a😀za😀z", "$0$0", "aa😀zaa😀z"];
    }

    [Theory] [MemberData(nameof(AliasingRoutes))]
    public void MatchedOverlapsPreserveUnreadScalarBytes(string pattern, string text, string template, string expected)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var replacement = Encoding.UTF8.GetBytes(template);
        var output = Encoding.UTF8.GetBytes(expected);
        foreach (var finite in new[] { false, true })
        foreach (var liveToken in new[] { false, true })
        foreach (var shift in new[] { -1, 0, 1 })
        {
            using var source = new CancellationTokenSource();
            var regex = finite ? Policy(pattern, 1000000) : new Utf8Pcre2Regex(pattern);
            var storage = new byte[128];
            input.CopyTo(storage.AsSpan(16));
            Assert.Equal(OperationStatus.Done, regex.TryReplace(storage.AsSpan(16, input.Length), replacement,
                storage.AsSpan(16 + shift, output.Length), out var written, 0,
                Pcre2SubstitutionOptions.None, Pcre2MatchOptions.None, liveToken ? source.Token : default));
            Assert.Equal(output.Length, written);
            Assert.Equal(output, storage.AsSpan(16 + shift, written).ToArray());
            Utf8Validation.Validate(storage.AsSpan(16 + shift, written));
        }
    }

    [Theory] [InlineData(false)] [InlineData(true)]
    public void OverlappingShortDestinationIsUnchanged(bool reportLength)
    {
        foreach (var size in new[] { 0, 7 })
        {
            var storage = new byte[64]; "a😀z"u8.CopyTo(storage);
            var before = storage.ToArray();
            Assert.Equal(OperationStatus.DestinationTooSmall, Policy("a", 100000).TryReplace(
                storage.AsSpan(0, 6), "XYZ"u8, storage.AsSpan(0, size), out var written, 0,
                reportLength ? Pcre2SubstitutionOptions.SubstituteOverflowLength : Pcre2SubstitutionOptions.None,
                Pcre2MatchOptions.None));
            Assert.Equal(reportLength ? 8 : 0, written);
            Assert.Equal(before, storage);
        }
    }

    [Fact]
    public void InputTemplateAndDestinationCanShareStorage()
    {
        var storage = new byte[64]; "a😀z"u8.CopyTo(storage.AsSpan(16));
        Assert.Equal(OperationStatus.Done, Policy("a", 100000).TryReplace(storage.AsSpan(16, 6),
            storage.AsSpan(17, 4), storage.AsSpan(15, 9), out var written));
        Assert.Equal(9, written);
        Assert.Equal("😀😀z"u8.ToArray(), storage.AsSpan(15, written).ToArray());
    }

    [Fact]
    public void OverlappingReplacementOnlyOmitsOriginalGaps()
    {
        var storage = new byte[64]; "za😀a"u8.CopyTo(storage);
        Assert.Equal(OperationStatus.Done, Policy("a", 100000).TryReplace(storage.AsSpan(0, 7), "$0$0"u8,
            storage.AsSpan(1, 4), out var written, Pcre2SubstitutionOptions.SubstituteReplacementOnly));
        Assert.Equal(4, written);
        Assert.Equal("aaaa"u8.ToArray(), storage.AsSpan(1, written).ToArray());
    }

    [Fact]
    public void OverlappingStagingRequiresAdmissionBeforeWriting()
    {
        var regex = new Utf8Pcre2Regex("a", Pcre2CompileOptions.None, default,
            new() { MatchLimit = 100000, DepthLimit = 256, HeapLimitInBytes = 4096,
                ResultLimitInBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes("a" + new string('x', 8191));
        var independent = new byte[input.Length];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "Q"u8, independent, out _));
        var before = input.ToArray();
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() =>
            regex.TryReplace(input, "Q"u8, input, out _)).ErrorKind);
        Assert.Equal(before, input);
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "Q"u8, independent, out _));
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
