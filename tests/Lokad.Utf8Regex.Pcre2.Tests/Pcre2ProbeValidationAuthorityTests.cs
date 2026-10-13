using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ProbeValidationAuthorityTests
{
    [Theory]
    [InlineData("abc")]
    [InlineData("(a)b+")]
    public void InitialValidationCannotReachMalformedTailAfterWorkDenial(string pattern)
    {
        var regex = Create(pattern, 3);
        var input = new byte[16385];
        input.AsSpan().Fill((byte)'a');
        input[^1] = 0xff;
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = regex.Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
    }

    [Theory]
    [InlineData("abc", false)] [InlineData("abc", true)]
    [InlineData("(a)b+", false)] [InlineData("(a)b+", true)]
    public void InitialValidationObservesOriginalInterruption(string pattern, bool cancel)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
        var reads = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads < 4) return 0;
            if (cancel) { source.Cancel(); return 0; }
            return 25;
        });
        var input = new byte[16385];
        input.AsSpan().Fill((byte)'a');
        input[^1] = 0xff;
        Exception? failure = null;
        try { _ = regex.Probe(input, Pcre2PartialMode.Soft, 0, Pcre2MatchOptions.None, source.Token); }
        catch (Exception error) { failure = error; }
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
    }

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData("(a)b+", "abbb")]
    public void ValidationAndMatchingDoNotReceiveSeparateWorkAllowances(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        // Three input steps admit this short validation, leaving no discovery
        // or projection allowance. A fresh sufficient invocation still succeeds.
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, 3).Probe(input, Pcre2PartialMode.Soft); }).ErrorKind);
        Assert.Equal(Utf8Pcre2ProbeKind.FullMatch, Create(pattern, 10000).Probe(input, Pcre2PartialMode.Soft).Kind);
    }

    [Theory]
    [InlineData("abc")] [InlineData("(a)b+")]
    public void ValidationPreservesScalarBoundariesCoordinatesAndRetainedResults(string pattern)
    {
        var regex = Create(pattern, 100000);
        foreach (var scalar in new[] { "é", "€", "😀" })
        foreach (var lead in new[] { 2045, 2046, 2047, 2048, 2049 })
        {
            var prefix = new string('x', lead) + scalar;
            var body = pattern == "abc" ? "abc" : "abbb";
            var input = Encoding.UTF8.GetBytes(prefix + body);
            var start = Encoding.UTF8.GetByteCount(prefix);
            var retained = regex.Probe(input, Pcre2PartialMode.Soft, start).GetMatch();
            var group = retained.GetGroup(0);
            Assert.Equal((start, input.Length), (group.StartOffsetInBytes, group.EndOffsetInBytes));
            Assert.Equal((prefix.Length, prefix.Length + body.Length), (group.StartOffsetInUtf16, group.EndOffsetInUtf16));
            Assert.Equal(body, retained.GetValueString());
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            { _ = regex.Probe(input, Pcre2PartialMode.Soft, lead + 1); });
            Assert.Equal(Utf8Pcre2ProbeKind.NoMatch, regex.Probe("zzz"u8, Pcre2PartialMode.Soft).Kind);
            Assert.Equal(body, retained.GetValueString());
        }
    }

    [Theory]
    [InlineData("abc")] [InlineData("(a)b+")]
    public void MalformedBoundaryOffsetsAndWholeSubjectValidationStayPrecise(string pattern)
    {
        var regex = Create(pattern, 100000);
        foreach (var offset in new[] { 2046, 2047, 2048, 2049, 4095 })
        foreach (var malformed in new byte[][] { [0xff], [0xf0, 0x9f, 0x98], [0xe0, 0x80, 0x80], [0xc2, 0x61] })
        {
            var input = new byte[offset + malformed.Length];
            input.AsSpan().Fill((byte)'x');
            (pattern == "abc" ? "abc"u8 : "abbb"u8).CopyTo(input);
            malformed.CopyTo(input, offset);
            var expected = Assert.Throws<ArgumentException>(() => Utf8Validation.Validate(input));
            var actual = Assert.Throws<ArgumentException>(() =>
            { _ = regex.Probe(input, Pcre2PartialMode.Soft); });
            Assert.Equal(expected.Message, actual.Message);
        }
    }

    private static Utf8Pcre2Regex Create(string pattern, uint work) => new(pattern,
        Pcre2CompileOptions.None, default, new() { MatchLimit = work,
            DepthLimit = 256, HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 << 20 },
        Timeout.InfiniteTimeSpan);
}
