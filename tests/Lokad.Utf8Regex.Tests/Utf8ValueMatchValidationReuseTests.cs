using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValueMatchValidationReuseTests
{
    [Theory]
    [InlineData(@"\A(a+)(b*)\z", "aaaabbb", 0, 7)]
    [InlineData(@"\A(?s:(.+))\z", "π😀中", 0, 4)]
    [InlineData(@"a+(b*)", "π😀XYZ中aabb!", 7, 4)]
    [InlineData(@"(a+)", "xxaaa yy aa", 2, 3)]
    [InlineData(@"(?:a?){0,3}", "", 0, 0)]
    public void PublicMatchesKeepScalarRangesAndDerivedCoordinates(
        string pattern, string text, int index, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        var storage = Encoding.UTF8.GetBytes("padding" + text + "padding");
        using var source = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        foreach (var finite in new[] { false, true })
        {
            var regex = Create(pattern, mode, finite ? 1000000UL : null);
            foreach (var token in new[] { CancellationToken.None, source.Token })
            {
                AssertRange(regex.Match(storage.AsSpan(7, bytes.Length), token), text, index, length);
            }
        }
    }

    [Theory]
    [InlineData("zz aaaabbb!", 3, 3, 7)]
    [InlineData("π😀zz aaaabbb!", 6, 6, 7)]
    public void OffsetCallsKeepWholeSubjectCoordinateOrigin(string text, int from, int index, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        foreach (var finite in new[] { false, true })
        {
            var regex = Create("a+(b*)", mode, finite ? 1000000UL : null);
            AssertRange(regex.MatchFromUtf16Offset(bytes, from, source.Token), text, index, length);
        }
    }

    [Theory]
    [InlineData("xxaaa yy aa", 9, 2)]
    [InlineData("π😀aaa yy aa", 10, 2)]
    public void RightToLeftKeepsNormalizedScalarCoordinates(string text, int index, int length)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        foreach (var mode in new[] { RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        foreach (var finite in new[] { false, true })
            AssertRange(Create("(a+)", mode, finite ? 1000000UL : null).Match(bytes, source.Token), text, index, length);
    }

    [Theory]
    [InlineData(@"\A(?s:(.+))\z", "π😀中", 0, 4)]
    [InlineData("(a+)(b*)", "zz aaaabbb", 3, 7)]
    public void EveryWorkCutoffDeniesOrPublishesTheCompleteValue(
        string pattern, string text, int index, int length)
    {
        var input = Encoding.UTF8.GetBytes(text);
        ulong low = 1, high = 100000;
        while (low < high)
        {
            var cap = low + (high - low) / 2;
            try { AssertRange(Create(pattern, RegexOptions.Compiled, cap).Match(input), text, index, length); high = cap; }
            catch (Utf8ResourceLimitExceededException error)
            {
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
                Assert.Equal(cap + 1, error.Requested);
                low = cap + 1;
            }
        }
        for (ulong cap = 1; cap < low; cap++)
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(pattern, RegexOptions.Compiled, cap).Match(input));
            Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
            Assert.Equal(cap + 1, error.Requested);
        }
        AssertRange(Create(pattern, RegexOptions.Compiled, low).Match(input), text, index, length);
    }

    [Fact]
    public void CompleteValidationStillRejectsMalformedTailAndPreCancellationWins()
    {
        byte[] input = [.. "aaaabbb"u8, 0xff];
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        foreach (var finite in new[] { false, true })
        {
            var regex = Create("(a+)(b*)", mode, finite ? 1000000UL : null);
            Assert.Throws<ArgumentException>(() => regex.Match(input));
            using var source = new CancellationTokenSource();
            source.Cancel();
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, source.Token)).CancellationToken);
        }
    }

    private static Utf8Regex Create(string pattern, RegexOptions mode, ulong? work) =>
        new(pattern, mode | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            work is null ? Utf8ResourcePolicy.Unlimited : new()
            { MaxWorkSteps = work, MaxDepth = 1024, MaxWorkingBytes = 1_048_576, MaxResultBytes = 1_048_576 });

    private static void AssertRange(Utf8ValueMatch match, string text, int index, int length)
    {
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(index, match.IndexInUtf16);
        Assert.Equal(length, match.LengthInUtf16);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, index)), match.IndexInBytes);
        Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(index, length)), match.LengthInBytes);
    }
}
