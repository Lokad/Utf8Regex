using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8NumberPrefixValueMatchTests
{
    private const string Pattern = @"\G-?[0-9]+(\.[0-9]+)?(e[+-]?[0-9]+)?";
    private static Utf8ResourcePolicy Funded => new() { MaxWorkSteps = 1000000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };

    private static Utf8CaptureRange Scalar(Utf8Regex regex, byte[] input, int start = 0)
    {
        var program = regex.Inspection.AvailableOwnedSliceProgram;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
        invocation.AnchorOriginByte = start;
        var slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
        try
        {
            var result = Utf8SliceRunner.TryFindFirst(program, input, start, slots.Starts, slots.Ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
            return result.Found ? new Utf8CaptureRange(result.StartByte, (result.StartByte) + (result.EndByte - result.StartByte)) : default(Utf8CaptureRange);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    [Fact]
    public void OptionalTailsAndSignsSelectTheScalarRange()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, options: mode | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxDepth = 8192,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
            var oracle = new Regex(Pattern, mode | RegexOptions.CultureInvariant);
            foreach (var text in new[] { "", "-", "--1", "+1", ".5", "x1", " 1", "1", "-0", "0123",
                "-12.34e-56!", "1.", "1.e3", "1e", "1e+", "1e-", "1.2e+", "1.2e-3x", "1E3", "1\n2" })
            {
                var input = Encoding.ASCII.GetBytes(text);
                byte[] storage = [0xff, .. input, 0xff];
                var expected = Scalar(regex, input); var bcl = oracle.Match(text);
                Assert.Equal((bcl.Success, System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(bcl.Index, bcl.Length))), (expected.Success, (expected.EndOffsetInBytes - expected.StartOffsetInBytes)));
                Assert.Equal(expected, regex.Match(storage.AsSpan(1, input.Length), cancellationToken: token.Token));
                Assert.Equal(expected, regex.Match(input));
            }
        }
    }

    [Fact]
    public void WiderSubjectsModesAndNonzeroOriginsKeepScalarResults()
    {
        using var token = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase, RegexOptions.RightToLeft })
        {
            var regex = new Utf8Regex(Pattern, options: mode | RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            foreach (var text in new[] { "-1.25e+3!", "12😀", "١٢", "-1.é", "12E3", "😀12", "12.3!" + new string(' ', 2049) })
            {
                var input = Encoding.UTF8.GetBytes(text);
                var start = mode == RegexOptions.RightToLeft ? input.Length : 0;
                Assert.Equal(Scalar(regex, input, start), regex.Match(input));
                Assert.Equal(Scalar(regex, input, start), regex.Match(input, cancellationToken: token.Token));
            }
        }
        foreach (var options in new[] { RegexOptions.None, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(Pattern.Replace(@"\G", @"\A"), options: options, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded
            });
            Assert.Equal(Scalar(regex, "-12.34e-5!"u8.ToArray()), regex.Match("-12.34e-5!"u8, cancellationToken: token.Token));
        }
        var offsetRegex = new Utf8Regex(Pattern);
        var offsetInput = "😀x-12.34e-5!"u8.ToArray();
        Utf8MatchSequence.EqualValue(Scalar(offsetRegex, offsetInput, 5), offsetRegex.Match(offsetInput, 5, []));
        Assert.False(offsetRegex.Match(offsetInput, 4, []).Success);
    }

    [Fact]
    public void LongReverseSearchStillObservesTheWorkLimit()
    {
        var regex = new Utf8Regex(Pattern, options: RegexOptions.RightToLeft, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = Funded with
            {
                MaxWorkSteps = 1000
            }
        });
        var input = Encoding.ASCII.GetBytes(new string('1', 2049));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input)).ResourceKind);
    }

    [Fact]
    public void RetainedCapturesAndCursorAliasesKeepTheirRanges()
    {
        var regex = new Utf8Regex(Pattern);
        var input = "-12.34e-5!"u8.ToArray(); var value = regex.Match(input);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal(".34", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        Assert.Equal("e-5", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(2).Span));
        Utf8MatchSequence.EqualValue(value, regex.Match(input, 0, []));
        Assert.Equal(1, regex.Count(input));
        var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext());
        Assert.Equal(value, cursor.Current); Assert.Equal(value, alias.Current);
        Assert.False(cursor.MoveNext()); Assert.False(alias.MoveNext());
        Assert.Equal(".34", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        Assert.Equal(9, (value.EndOffsetInBytes - value.StartOffsetInBytes));
        using var token = new CancellationTokenSource(); token.Cancel();
        Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, cancellationToken: token.Token)).CancellationToken);
    }

    [Theory]
    [InlineData("123!", 0, 3)]
    [InlineData("😀x12e-", 5, 2)]
    [InlineData("😀x-12.34e-5!", 5, 9)]
    public void ShortContiguousRangeFitsTheBoundedProof(string text, int origin, int length)
    {
        using var token = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile(Pattern, RegexOptions.Compiled, new()
        {
            ResourcePolicy = new() { MaxWorkSteps = 4, MaxDepth = 1, MaxWorkingBytes = 1 }
        });
        var actual = regex.Match(Encoding.UTF8.GetBytes(text), origin, [], cancellationToken: token.Token);
        Assert.True(actual.Success);
        Assert.Equal((origin, origin + length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }

    [Theory]
    [InlineData("12.3e-4!", true)]
    [InlineData("wrong", false)]
    public void WorkDenialCannotPublishSuccessOrNoMatch(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text); var admitted = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = Funded with
                {
                    MaxWorkSteps = work,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }
            });
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
        var regex = new Utf8Regex(Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(50),
            ResourcePolicy = Funded
        });
        Assert.True(regex.Match("12.3e-4!"u8, cancellationToken: token.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("12.3e-4!"u8, cancellationToken: token.Token)).MatchTimeout);
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("12.3e-4!"u8, cancellationToken: token.Token)).CancellationToken);
    }
}
