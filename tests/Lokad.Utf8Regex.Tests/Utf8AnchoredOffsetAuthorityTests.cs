using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8AnchoredOffsetAuthorityTests
{
    [Fact]
    public void BorrowedAnchoredCursorReusesItsFiniteInvocationWithoutAllocatingAnotherOwner()
    {
        var literal = "abc"u8.ToArray();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, TimeSpan.FromSeconds(20), default);
        var validation = Utf8Validation.Validate(literal, ref invocation);
        var warm = new Utf8OperationMatchCursor(literal, literal,
            Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, validation, invocation);
        Assert.True(warm.MoveNext());
        var before = GC.GetAllocatedBytesForCurrentThread();
        var cursor = new Utf8OperationMatchCursor(literal, literal,
            Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix, validation, invocation);
        var found = cursor.MoveNext();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(found);
        Assert.Equal(0, allocated);
        Assert.Equal(cursor.DebugInvocationSteps, invocation.Budget.Steps);
    }

    [Fact]
    public void ControlledTurkicFoldRetainsItsScalarRoute()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var regex = new Utf8Regex("(?i:^I)", RegexOptions.None, TimeSpan.FromSeconds(20));
            using var source = new CancellationTokenSource();
            Assert.False(regex.MatchFromUtf16Offset("i"u8, 0, source.Token).Success);
            var dotless = regex.MatchFromUtf16Offset("ı"u8, 0, source.Token);
            Assert.True(dotless.Success);
            Assert.Equal((0, 2), (dotless.IndexInBytes, dotless.LengthInBytes));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Theory]
    [InlineData("^abc", "abc", 0, 0, 3)]
    [InlineData("^abc", "abc", 1, -1, 0)]
    [InlineData("abc$", "😀abc\n", 2, 4, 3)]
    [InlineData("abc$", "😀abc\n", 3, -1, 0)]
    [InlineData(@"abc\z", "😀abc\n", 2, -1, 0)]
    [InlineData(@"abc\z", "😀abc", 2, 4, 3)]
    [InlineData("(?i:^K)", "K", 0, 0, 3)]
    [InlineData(@"(?i:K\z)", "😀K", 2, 4, 3)]
    [InlineData("(?i:^ABC)", "abc", 0, 0, 3)]
    [InlineData("(?i:^ABC)", "abc", 1, -1, 0)]
    [InlineData("(?i:ABC$)", "abc\n", 0, 0, 3)]
    [InlineData(@"(?i:ABC\z)", "abc\n", 0, -1, 0)]
    [InlineData("(?i:ABC$)", "😀abc\n", 2, 4, 3)]
    [InlineData("^", "😀", 0, 0, 0)]
    [InlineData("^(abc)", "abc", 0, 0, 3)]
    public void ControlledOffsetCallsPreserveScalarRangesFloorsAndAnchorContext(string pattern, string text, int floor, int start, int length)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(20));
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var expected = start >= 0;
        Assert.Equal(expected, regex.IsMatchFromUtf16Offset(input, floor, source.Token));
        Assert.Equal(expected ? 1 : 0, regex.CountFromUtf16Offset(input, floor, source.Token));
        var value = regex.MatchFromUtf16Offset(input, floor, source.Token);
        var detailed = regex.MatchDetailedFromUtf16Offset(input, floor, source.Token);
        Assert.Equal(expected, value.Success);
        Assert.Equal(expected, detailed.Success);
        if (expected)
        {
            Assert.Equal((start, length), (value.IndexInBytes, value.LengthInBytes));
            Assert.Equal((start, length), (detailed.IndexInBytes, detailed.LengthInBytes));
            if (pattern == "^(abc)") Assert.Equal("abc", detailed.GetGroup(1).GetValueString());
        }
    }

    public static IEnumerable<object[]> Cases()
    {
        // Count has only the suffix shortcut; folded shortcuts belong to Match.
        (string Pattern, int Operation)[] routes =
        [ ("^aaa", 0), ("^aaa", 1), ("^aaa", 2),
          ("aaa$", 0), ("aaa$", 1), ("aaa$", 2), ("aaa$", 3),
          (@"aaa\z", 1), ("(?i:^AAA)", 1), (@"(?i:AAA\z)", 1) ];
        foreach (var route in routes)
        foreach (var compiled in new[] { false, true })
        foreach (var timeout in new[] { false, true })
            yield return [route.Pattern, route.Operation, compiled, timeout];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void AnchoredOffsetMatchingObservesAuthorityAfterPreparation(string pattern, int operation, bool compiled, bool timeout)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0);
        var duration = timeout ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout;
        var regex = new Utf8Regex(pattern, options, duration);
        var input = Encoding.ASCII.GetBytes(new string('a', 60 * 2048));
        using var source = new CancellationTokenSource();
        var token = timeout ? CancellationToken.None : source.Token;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);

        // Calibrate only preparation, using the same existing primitives. The
        // fixture puts the next diagnostic checkpoint in matching, never validation.
        var preparation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, duration, token);
        var validation = Utf8Validation.Validate(input, ref preparation);
        _ = Utf8BoundaryMap.Create(input, validation, ref preparation, out var reservation);
        preparation.Ledger.Release(reservation);
        Assert.Equal(63UL, preparation.Budget.Steps);

        Assert.True(Run(regex, input, operation, CancellationToken.None));
        var entered = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            Assert.True(steps > preparation.Budget.Steps);
            entered = true;
            if (timeout) ticks = 25; else source.Cancel();
        });
        Exception? failure;
        try { failure = Record.Exception(() => Run(regex, input, operation, token)); }
        finally { diagnostics.Complete(); }

        Assert.True(entered, "Anchored matching returned without observing the operation's execution checkpoint.");
        if (timeout)
        {
            var expired = Assert.IsType<RegexMatchTimeoutException>(failure);
            Assert.Equal(duration, expired.MatchTimeout);
        }
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        ticks = 0;
        Assert.True(Run(regex, input, operation, CancellationToken.None));
    }

    private static bool Run(Utf8Regex regex, byte[] input, int operation, CancellationToken token)
        => operation switch
        {
            0 => regex.IsMatchFromUtf16Offset(input, 0, token),
            1 => regex.MatchFromUtf16Offset(input, 0, token).Success,
            2 => regex.MatchDetailedFromUtf16Offset(input, 0, token).Success,
            _ => regex.CountFromUtf16Offset(input, 0, token) == 1,
        };
}
