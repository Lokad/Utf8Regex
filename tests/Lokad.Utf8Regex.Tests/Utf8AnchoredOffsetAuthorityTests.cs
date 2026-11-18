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
        Utf8SliceBufferWork.Checkpoint(ref invocation);
        var warm = new Utf8OperationMatchCursor(literal, literal, Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix, invocation);
        Assert.True(warm.MoveNext());
        var before = GC.GetAllocatedBytesForCurrentThread();
        var cursor = new Utf8OperationMatchCursor(literal, literal, Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix, invocation);
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
            var regex = new Utf8Regex("(?i:^I)", options: RegexOptions.None, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromSeconds(20)
            });
            using var source = new CancellationTokenSource();
            Assert.False(regex.Match("i"u8, 0, [], source.Token).Success);
            var dotless = regex.Match("ı"u8, 0, [], source.Token);
            Assert.True(dotless.Success);
            Assert.Equal((0, 2), (dotless.StartOffsetInBytes, (dotless.EndOffsetInBytes - dotless.StartOffsetInBytes)));
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
    [InlineData("(?i:^ABC)", "abc😀", 0, 0, 3)]
    [InlineData("(?i:^ABC)", "😀abc", 0, -1, 0)]
    [InlineData("(?i:^ABC)", "abc", 1, -1, 0)]
    [InlineData("(?i:ABC$)", "abc\n", 0, 0, 3)]
    [InlineData(@"(?i:ABC\z)", "abc\n", 0, -1, 0)]
    [InlineData(@"(?i:ABC\z)", "😀abc", 2, 4, 3)]
    [InlineData("(?i:ABC$)", "😀abc\n", 2, 4, 3)]
    [InlineData("^", "😀", 0, 0, 0)]
    [InlineData("^(abc)", "abc", 0, 0, 3)]
    public void ControlledOffsetCallsPreserveScalarRangesFloorsAndAnchorContext(string pattern, string text, int floor, int start, int length)
    {
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(20)
        });
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var expected = start >= 0;
        Assert.Equal(expected, regex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, floor)), [], source.Token).Success);
        Assert.Equal(expected ? 1 : 0, Utf8MatchSequence.Count(regex.EnumerateMatches(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, floor)), source.Token)));
        var value = regex.Match(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, floor)), [], source.Token);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input, Encoding.UTF8.GetByteCount(text.AsSpan(0, floor)), source.Token));
        Assert.Equal(expected, value.Success);
        Assert.Equal(expected, detailed.Success);
        if (expected)
        {
            Assert.Equal((start, length), (value.StartOffsetInBytes, (value.EndOffsetInBytes - value.StartOffsetInBytes)));
            Assert.Equal((start, start + length), (detailed.Value.StartOffsetInBytes, detailed.Value.EndOffsetInBytes));
            if (pattern == "^(abc)") Assert.Equal("abc"u8.ToArray(), detailed.GetGroupMemory(1).ToArray());
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

    private static bool Run(Utf8Regex regex, byte[] input, int operation, CancellationToken token)
        => operation switch
        {
            0 => regex.Match(input, 0, [], token).Success,
            1 => regex.Match(input, 0, [], token).Success,
            2 => Utf8MatchSequence.First(regex.CreateMatchCursor(input, 0, token)).Success,
            _ => Utf8MatchSequence.Count(regex.EnumerateMatches(input, 0, token)) == 1,
        };
}
