using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8UnobservedValueCaptureTests
{
    public static IEnumerable<object[]> Cases()
    {
        foreach (var pattern in new[]
        {
            "abcdef([0-9]+)", "((ab)+)-", "(?i:abcdef)([a-z]+)", "é(😀+)X",
            "(?<value>[a-z]+)-([0-9]+)", @"abcdef(?<value>[0-9]+)\k<value>",
            "abcdef(?<value>[0-9]+)(?(value)X|Y)", "abcdef(?=(?<value>[0-9]+))[0-9]+",
            "abcdef(?<open>a)+(?<-open>b)+(?(open)(?!))", "abcdef(?<same>[0-9]+)(?<same>[a-z]+)",
            "abcdef(?<12>[0-9]+)", "(?:abcdef([0-9]+)|ghijkl([a-z]+))"
        })
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled,
            RegexOptions.RightToLeft, RegexOptions.Compiled | RegexOptions.RightToLeft })
            yield return [pattern, options | RegexOptions.CultureInvariant];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ValueBooleanCountAndDetailedOperationsPreserveMatchingDependencies(string pattern, RegexOptions options)
        => Utf8SliceSparseGroupTests.AssertOwnedOperations(pattern, options,
            ["", "abcdef123", "xabcdef123X", "abcdef123123", "abcdef123abc", "abcdefaaabbb",
             "abab- ab-", "é😀😀X", "abc-12 éghijklxyz", "abcdefABC", "ghijklabc"], options);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ValuesFitSmallWorkingBudgetWhileDetailedAndCursorCapturesKeepAdmission(bool rtl)
    {
        var regex = new Utf8Regex("abcdef([0-9]+)", rtl ? RegexOptions.RightToLeft : RegexOptions.None,
            // Mirrored repeats retain their ordinary frames and repeat journal.
            Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy
            {
                MaxWorkingBytes = rtl ? (ulong)Utf8SliceRunner.TrailReserveBytes - 1 : 256UL,
                MaxWorkSteps = 100000
            });
        var input = "xxabcdef123"u8.ToArray();
        using var token = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, token.Token));
        Assert.Equal(1, regex.Count(input, token.Token));
        var value = regex.Match(input, token.Token);
        Assert.True(value.Success);
        Assert.Equal(2, value.IndexInBytes);
        Assert.Equal(9, value.LengthInBytes);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.MatchDetailed(input)).ResourceKind);
        using var cursor = regex.CreateMatchCursor(input);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext(out _)).ResourceKind);
    }

    [Theory]
    [InlineData(@"abcdef(?<v>[0-9]+)\k<v>", "abcdef123123")]
    [InlineData("abcdef(?<v>[0-9]+)(?(v)X|Y)", "abcdef123X")]
    [InlineData("abcdef(?=(?<v>[0-9]+))[0-9]+", "abcdef123")]
    [InlineData("abcdef(?<v>a)+(?<-v>b)+(?(v)(?!))", "abcdefaaabbb")]
    [InlineData("abcdef(?<v>[0-9]+)(?<v>[a-z]+)", "abcdef123abc")]
    [InlineData("abcdef(?<12>[0-9]+)", "abcdef123")]
    public void OriginalConservativeExclusionsRetainTheirCaptureWorkingStorage(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 256, MaxWorkSteps = 100000 });
        Assert.True(new Utf8Regex(pattern).IsMatch(input));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input)).ResourceKind);
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryPublicWorkCutoffIsStructuredAndNeverChangesSuccessfulRange(bool boolean)
    {
        var input = Encoding.UTF8.GetBytes("é😀 abcdef123");
        var baseline = new Utf8Regex("abcdef([0-9]+)").Match(input);
        var admitted = false;
        for (ulong limit = 1; limit <= 256; limit++)
        {
            var regex = new Utf8Regex("abcdef([0-9]+)", RegexOptions.None, Timeout.InfiniteTimeSpan,
                new Utf8ResourcePolicy { MaxWorkSteps = limit, MaxWorkingBytes = 256 });
            try
            {
                if (boolean) Assert.True(regex.IsMatch(input));
                else Assert.Equal(baseline, regex.Match(input));
                admitted = true;
                break;
            }
            catch (Utf8ResourceLimitExceededException error)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind); }
        }
        Assert.True(admitted);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void OriginalInterruptionAndMalformedTailsRemainAuthoritative(bool boolean, bool expire)
    {
        var regex = new Utf8Regex("abcdef([0-9]+)", RegexOptions.None, TimeSpan.FromMilliseconds(20),
            new Utf8ResourcePolicy { MaxWorkSteps = 1000000, MaxWorkingBytes = 256 });
        var input = Encoding.ASCII.GetBytes(new string('x', 262144) + "abcdef123");
        using var token = new CancellationTokenSource(); long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (expire) ticks = 50; else token.Cancel(); });
        try
        {
            var error = Record.Exception(() => { if (boolean) regex.IsMatch(input, token.Token); else regex.Match(input, token.Token); });
            if (expire) Assert.IsType<RegexMatchTimeoutException>(error);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        }
        finally { session.Complete(); }
        var malformed = "abcdef123"u8.ToArray().Concat(new byte[] { 255 }).ToArray();
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        Assert.Throws<ArgumentException>(() => regex.Match(malformed));
    }

    [Fact]
    public void NonBacktrackingStillRetainsItsCaptureSemantics()
        => Utf8SliceSparseGroupTests.AssertOwnedOperations("abcdef([0-9]+)", RegexOptions.NonBacktracking,
            ["xxabcdef123", "abcdef12 abcdef345"], RegexOptions.NonBacktracking);
}
