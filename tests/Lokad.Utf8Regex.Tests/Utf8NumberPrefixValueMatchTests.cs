using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8NumberPrefixValueMatchTests
{
    private const string Pattern = @"\G-?[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000,
        MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static Utf8ValueMatch Scalar(Utf8Regex regex, byte[] input, int start = 0)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
        invocation.AnchorOriginByte = start;
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var result = Utf8SliceRunner.TryFindFirst(program, input, start, slots.Starts, slots.Ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            return result.Found ? new Utf8ValueMatch(true, true,
                Encoding.UTF8.GetCharCount(input.AsSpan(0, result.StartByte)),
                Encoding.UTF8.GetCharCount(input.AsSpan(result.StartByte, result.EndByte - result.StartByte)),
                result.StartByte, result.EndByte - result.StartByte) : Utf8ValueMatch.NoMatch;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Fact]
    public void OptionalTailsAndSignsSelectTheScalarRangeWithoutCaptureWorkspace()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, mode | RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan,
                Funded with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal("AnchoredNumberPrefix", regex.Inspection.DebugFallbackDirectFamilyKind);
            var oracle = new Regex(Pattern, mode | RegexOptions.CultureInvariant);
            foreach (var text in new[] { "", "-", "--1", "+1", ".5", "x1", " 1", "1", "-0", "0123",
                "-12.34e-56!", "1.", "1.e3", "1e", "1e+", "1e-", "1.2e+", "1.2e-3x", "1E3", "1\n2" })
            {
                var input = Encoding.ASCII.GetBytes(text);
                byte[] storage = [0xff, .. input, 0xff];
                var expected = Scalar(regex, input); var bcl = oracle.Match(text);
                Assert.Equal((bcl.Success, bcl.Length), (expected.Success, expected.LengthInUtf16));
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    Assert.Equal(expected, regex.Match(storage.AsSpan(1, input.Length), token.Token));
                    Assert.Equal(Utf8ExecutionRoute.NativeAsciiAnchoredValidator, session.ExecutionRoute);
                    Assert.Equal(expected, regex.Match(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void WholeSubjectBoundControlsAdmission(int length)
    {
        var input = Encoding.ASCII.GetBytes(new string('1', length));
        Assert.Equal(length, new Utf8Regex(Pattern).Match(input).LengthInBytes);
        var tight = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        if (length <= 2048) Assert.Equal(length, tight.Match(input).LengthInBytes);
        else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.Match(input)).ResourceKind);
    }

    [Fact]
    public void WiderSubjectsModesAndNonzeroOriginsKeepScalarResults()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(Pattern, mode | RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, Funded);
            foreach (var text in new[] { "-1.25e+3!", "12😀", "١٢", "-1.é", "12E3", "😀12", "12.3!" + new string(' ', 2049) })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var start = mode == RegexOptions.RightToLeft ? input.Length : 0;
                Assert.Equal(Scalar(regex, input, start), regex.Match(input));
                Assert.Equal(Scalar(regex, input, start), regex.Match(input, token.Token));
            }
        }
        foreach (var options in new[] { RegexOptions.None, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(Pattern.Replace(@"\G", @"\A"), options, Timeout.InfiniteTimeSpan, Funded);
            Assert.Equal(Scalar(regex, "-12.34e-5!"u8.ToArray()), regex.Match("-12.34e-5!"u8, token.Token));
        }
        var offsetRegex = new Utf8Regex(Pattern);
        var offsetInput = "😀x-12.34e-5!"u8.ToArray();
        Assert.Equal(Scalar(offsetRegex, offsetInput, 5), offsetRegex.MatchFromUtf16Offset(offsetInput, 3));
        Assert.False(offsetRegex.MatchFromUtf16Offset(offsetInput, 2).Success);
    }

    [Fact]
    public void LongReverseSearchStillObservesTheWorkLimit()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.RightToLeft, Timeout.InfiniteTimeSpan,
            Funded with { MaxWorkSteps = 1000 });
        var input = Encoding.ASCII.GetBytes(new string('1', 2049));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input)).ResourceKind);
    }

    [Fact]
    public void DetailedCapturesAndRetainedValuesRemainOwnedAndMalformedTailsAreRejected()
    {
        var regex = new Utf8Regex(Pattern);
        var input = "-12.34e-5!"u8.ToArray(); var value = regex.Match(input);
        var detailed = regex.MatchDetailed(input);
        Assert.Equal(".34", detailed.GetGroup(1).GetValueString());
        Assert.Equal("e-5", detailed.GetGroup(2).GetValueString());
        Assert.Equal(value, regex.MatchFromUtf16Offset(input, 0));
        Assert.Equal(1, regex.Count(input));
        var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal(".34", detailed.GetGroup(1).GetValueString());
        Assert.Equal(9, value.LengthInBytes);
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(malformed, token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData("12.3e-4!", true)] [InlineData("wrong", false)]
    public void WorkDenialCannotPublishSuccessOrNoMatch(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text); var admitted = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                Funded with { MaxWorkSteps = work, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            try { Assert.Equal(success, regex.Match(input).Success); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException error) { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
        }
        Assert.True(admitted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationObservesTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var token = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        {
            if (++reads == trigger) { if (expire) ticks = 100; else token.Cancel(); }
            return ticks;
        });
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50), Funded);
        Assert.True(regex.Match("12.3e-4!"u8, token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("12.3e-4!"u8, token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("12.3e-4!"u8, token.Token)).CancellationToken);
    }
}
