using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8IdentifierValueMatchTests
{
    private const string Pattern = "[a-zA-Z][a-zA-Z0-9]*";
    private static readonly Utf8ResourcePolicy Policy = new()
    { MaxWorkSteps = 1_000_000, MaxDepth = 256, MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000 };

    [Fact]
    public void BoundedAsciiValuesPreserveTheSelectedIndexWithoutVmStorage()
    {
        using var live = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(Pattern, mode, Timeout.InfiniteTimeSpan,
                Policy with { MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            Assert.Equal("AsciiIdentifierToken", regex.Inspection.DebugFallbackDirectFamilyKind);
            var oracle = new Regex(Pattern, mode);
            foreach (var text in new[] { "", "123_", "a", "ABC123", "!!abc12.def", "\0\r\n_19a0", "9_A_B", "!a!b" })
            {
                var input = Encoding.ASCII.GetBytes(text); byte[] backing = [0xff, .. input, 0xff];
                var session = Utf8SearchDiagnosticsSession.Start();
                try
                {
                    var value = regex.Match(backing.AsSpan(1, input.Length), live.Token);
                    var expected = oracle.Match(text);
                    Assert.Equal(expected.Success, value.Success);
                    if (value.Success) Assert.Equal((expected.Index, expected.Length, expected.Index, expected.Length),
                        (value.IndexInBytes, value.LengthInBytes, value.IndexInUtf16, value.LengthInUtf16));
                    Assert.Equal(Utf8ExecutionRoute.FallbackDirectAsciiIdentifierToken, session.ExecutionRoute);
                    Assert.Equal(value, regex.Match(input));
                }
                finally { session.Complete(); }
            }
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)]
    public void WholeSubjectBoundsRetainAdmissionAndNonzeroProjection(int length)
    {
        var input = Encoding.ASCII.GetBytes(new string('0', length - 3) + "abc");
        var unlimited = new Utf8Regex(Pattern).Match(input);
        Assert.Equal((length - 3, 3), (unlimited.IndexInBytes, unlimited.LengthInBytes));
        var tight = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            Policy with { MaxWorkingBytes = 1, MaxResultBytes = 1 });
        if (length <= 2048) Assert.Equal(unlimited, tight.Match(input));
        else Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => tight.Match(input)).ResourceKind);
    }

    [Fact]
    public void UnicodeDeclinedShapesCapturesAndAliasesRetainTheirContracts()
    {
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.IgnoreCase,
            RegexOptions.RightToLeft, RegexOptions.NonBacktracking })
            Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(Pattern, mode), new Regex(Pattern, mode),
                ["!!abc12.def", "é😀abc9!de", "Ka1!x", "123_", "123é", "😀"]);
        foreach (var pattern in new[] { "(?i:" + Pattern + ")", "[a-zA-Z][a-zA-Z0-9]*?", Pattern + "x" })
            Utf8SliceSparseGroupTests.AssertOwnedOperations(new Utf8Regex(pattern), new Regex(pattern), ["!!Abc12x.def", "Ka1!x"]);
        var regex = new Utf8Regex("(?<id>" + Pattern + ")"); var bytes = "!!abc12!de"u8.ToArray();
        var retained = regex.MatchDetailed(bytes); var cursor = regex.EnumerateMatches(bytes); var alias = cursor;
        Assert.True(cursor.MoveNext()); Assert.True(alias.MoveNext()); var first = alias.Current;
        Assert.Equal(first, cursor.Current); Assert.True(cursor.MoveNext()); Assert.Equal(first, alias.Current);
        _ = regex.Match("other"u8); Assert.Equal("abc12", retained.GetGroup("id").GetValueString());
        var offset = new Utf8Regex(Pattern).MatchFromUtf16Offset("😀!abc!de"u8, 2);
        Assert.Equal((5, 3, 3, 3), (offset.IndexInBytes, offset.LengthInBytes, offset.IndexInUtf16, offset.LengthInUtf16));
    }

    [Fact]
    public void FullValidationAndOriginalEntryCancellationPrecedeMatching()
    {
        var regex = new Utf8Regex(Pattern);
        foreach (var text in new[] { "!!abc", "123_" })
        {
            byte[] invalid = [.. Encoding.ASCII.GetBytes(text), 0xff];
            Assert.Throws<ArgumentException>(() => regex.Match(invalid));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(invalid, canceled.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData("!!abc12.tail", true)] [InlineData("123_", false)]
    public void EveryWorkCutoffRefusesPartialPublication(string text, bool success)
    {
        var input = Encoding.ASCII.GetBytes(text); var denied = false; var admitted = false;
        for (ulong work = 1; work < 100; work++)
        {
            var regex = new Utf8Regex(Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
                Policy with { MaxWorkSteps = work, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
            try { Assert.Equal(success, regex.Match(input).Success); admitted = true; break; }
            catch (Utf8ResourceLimitExceededException failure)
            { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind); denied = true; }
        }
        Assert.True(denied); Assert.True(admitted);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void PublicationObservesTheOriginalTokenAndDeadline(bool expire)
    {
        var reads = 0; var trigger = int.MaxValue; long ticks = 0;
        using var live = new CancellationTokenSource();
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (++reads == trigger) { if (expire) ticks = 100; else live.Cancel(); } return ticks; });
        var regex = new Utf8Regex(Pattern, RegexOptions.None, TimeSpan.FromMilliseconds(50), Policy);
        Assert.True(regex.Match("!!abc12.tail"u8, live.Token).Success);
        trigger = reads; reads = 0;
        if (expire) Assert.Equal(regex.MatchTimeout, Assert.Throws<RegexMatchTimeoutException>(() => regex.Match("!!abc12.tail"u8, live.Token)).MatchTimeout);
        else Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Match("!!abc12.tail"u8, live.Token)).CancellationToken);
    }
}
