using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureSlotMatchTests
{
    public static IEnumerable<object[]> Cases()
    {
        string[] regular = ["", "ab", "é", "ab|a|bc|b", "ab|a|bc|b|é|d|ef|f", "(a)+(b)?", @"(?<10>a)(?<2>b)?(?<word>c)*",
            @"(?<x>a)(?<x>b)", @"(?<word>\w+)", @"(a|ab)+", @"(?:(?<x>a)|b)+"];
        string[] backtracking = [@"(?<x>a+)\k<x>", @"(?<=é)(?<x>a+)", @"(?=(?<ahead>ab))a",
            @"\G(?<x>ab)", @"(?<x>a)+(?<-x>b)+(?(x)(?!))", @"(?<x>a)+(?<y-x>b)(?(x)c|d)"];
        foreach (var pattern in regular.Concat(backtracking))
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            if (mode == RegexOptions.NonBacktracking && !regular.Contains(pattern)) continue;
            foreach (var controlled in new[] { false, true }) yield return [pattern, mode, controlled];
        }
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ByteStartsAndEndsPreserveLastCaptures(string pattern, RegexOptions options, bool controlled)
    {
        options |= RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options, resourcePolicy: controlled
            ? new() { MaxWorkSteps = 1_000_000, MaxDepth = 512, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }
            : default, inputValidation: Utf8InputValidation.AssumeValid);
        using var source = new CancellationTokenSource();
        var numbers = oracle.GetGroupNumbers();
        Assert.Equal(numbers, regex.GetGroupNumbers());
        var sentinel = new Utf8CaptureRange(123, 124);
        foreach (var text in new[] { "", "abc", "éaababc d", "aaabbbd", "aaaac", "éé abc éa" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            foreach (var end in new[] { 0, text.Length / 2, text.Length }.Distinct())
            for (var start = 0; start <= end; start++)
            {
                // BMP subjects make every UTF16 position a scalar boundary.
                // Prefix-only truncation retains lookbehind and absolute origin.
                var expected = oracle.Match(text[..end], start);
                var byteStart = Encoding.UTF8.GetByteCount(text.AsSpan(0, start));
                var byteEnd = Encoding.UTF8.GetByteCount(text.AsSpan(0, end));
                foreach (var length in new[] { 0, 1, numbers.Length, numbers.Length + 2 }.Distinct())
                {
                    var slots = Enumerable.Repeat(sentinel, length).ToArray();
                    var actual = regex.Match(input, byteStart, slots, controlled ? source.Token : default,
                        endOffsetInBytes: byteEnd);
                    EqualRange(text, expected, actual);
                    for (var ordinal = 0; ordinal < Math.Min(length, numbers.Length); ordinal++)
                        EqualRange(text, expected.Success ? expected.Groups[numbers[ordinal]] : null, slots[ordinal]);
                    foreach (var excess in slots.Skip(numbers.Length)) Assert.Equal(sentinel, excess);
                }
            }
        }
    }

    private static void EqualRange(string text, Group? expected, Utf8CaptureRange actual)
    {
        Assert.Equal(expected?.Success == true, actual.Success);
        Assert.Equal(expected?.Success == true ? Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)) : 0,
            actual.StartOffsetInBytes);
        Assert.Equal(expected?.Success == true ? Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index + expected.Length)) : 0,
            actual.EndOffsetInBytes);
    }

    [Theory]
    [InlineData(RegexOptions.None)] [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)] [InlineData(RegexOptions.NonBacktracking)]
    public void SupplementaryCapturesAndHistoryRemainScalarRanges(RegexOptions options)
    {
        var regex = new Utf8Regex("(?<scalar>.)+", options | RegexOptions.CultureInvariant);
        var input = "é😀a"u8.ToArray();
        var slots = new Utf8CaptureRange[2];
        var actual = regex.Match(input, options == RegexOptions.RightToLeft ? input.Length : 0, slots);
        Assert.Equal((0, input.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
        Assert.Equal(actual, slots[0]);
        Assert.Equal(options == RegexOptions.RightToLeft ? (0, 2) : (6, 7),
            (slots[1].StartOffsetInBytes, slots[1].EndOffsetInBytes));
        using var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var history));
        Assert.True(history.TryGetGroup(1, out var last));
        Assert.Equal(last, slots[1]);
        Assert.Equal(options == RegexOptions.NonBacktracking ? 1 : 3, history.GetCaptures(1).Length);
    }

    [Fact]
    public void ByteBoundsAndCurrentInputPolicyAreEnforcedWithoutStaleOutput()
    {
        var regex = new Utf8Regex("(.)");
        var sentinel = regex.Match("a"u8, 0, []);
        foreach (var start in new[] { -1, 1, 3, 4, 5, 8, int.MaxValue })
        {
            var output = new[] { sentinel, sentinel, sentinel };
            var error = Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("é😀a"u8, start, output));
            Assert.Equal("startOffsetInBytes", error.ParamName);
            Assert.All(output[..2], range => Assert.False(range.Success));
            Assert.Equal(sentinel, output[2]);
        }
        foreach (var end in new[] { -1, 1, 3, 4, 5, 8, int.MaxValue })
            Assert.Equal("endOffsetInBytes", Assert.Throws<ArgumentOutOfRangeException>(() =>
                regex.Match("é😀a"u8, 0, [], endOffsetInBytes: end)).ParamName);
        Assert.Throws<ArgumentException>(() => regex.Match(new byte[] { (byte)'a', 0xff }, 0, [], endOffsetInBytes: 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("abc"u8, 2, [], endOffsetInBytes: 1));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void DenialAndCancellationClearWrittenSlotsAndRefundWorkingLoans(int failure)
    {
        var policy = failure switch
        {
            0 => new Utf8ResourcePolicy { MaxWorkSteps = 1 },
            1 => new Utf8ResourcePolicy { MaxWorkingBytes = 1 },
            _ => new Utf8ResourcePolicy { MaxDepth = 1 },
        };
        var regex = new Utf8Regex(@"(?=(?=(a)))a", resourcePolicy: policy);
        var sentinel = new Utf8Regex("a").Match("a"u8, 0, []);
        var output = new[] { sentinel, sentinel, sentinel };
        var account = new Account();
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            regex.Match("a"u8, 0, output, workingMemoryAllowance: account));
        Assert.Equal(failure switch { 0 => Utf8ResourceLimitKind.ExecutionWorkSteps,
            1 => Utf8ResourceLimitKind.ExecutionWorkingBytes, _ => Utf8ResourceLimitKind.ExecutionDepth }, error.ResourceKind);
        Assert.All(output[..2], range => Assert.False(range.Success));
        Assert.Equal(sentinel, output[2]);
        Assert.Equal(0UL, account.Held);
        using var source = new CancellationTokenSource();
        source.Cancel();
        output.AsSpan().Fill(sentinel);
        var canceled = Assert.Throws<OperationCanceledException>(() => regex.Match("a"u8, 0, output, source.Token));
        Assert.Equal(source.Token, canceled.CancellationToken);
        Assert.All(output, range => Assert.Equal(sentinel, range));
    }

    [Fact]
    public void MatchingUsesOriginalTokenAndAbsoluteDeadline()
    {
        var regex = new Utf8Regex("(a+)", matchTimeout: TimeSpan.FromMilliseconds(100));
        var input = Encoding.UTF8.GetBytes(new string('a', 256));
        var output = new Utf8CaptureRange[2];
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(_ => source.Cancel());
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() => regex.Match(input, 0, output, source.Token));
            Assert.Equal(source.Token, error.CancellationToken);
        }
        finally { session.Complete(); }
        using var clock = Utf8DeadlineTestClock.Start();
        session = Utf8SearchDiagnosticsSession.Start(_ => clock.Advance(101));
        try
        {
            var error = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, 0, output));
            Assert.Equal(regex.Pattern, error.Pattern);
            Assert.Equal(regex.MatchTimeout, error.MatchTimeout);
            Assert.All(output, range => Assert.False(range.Success));
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void WorkingAdmissionRefundsOnSuccessAndRefusal()
    {
        var regex = new Utf8Regex(@"(?<word>\w+)");
        var output = new Utf8CaptureRange[2];
        var account = new Account();
        Assert.True(regex.Match("éabc"u8, 0, output, workingMemoryAllowance: account).Success);
        Assert.True(account.Peak > 0);
        Assert.Equal(0UL, account.Held);
        account.Maximum = account.Peak - 1;
        Assert.Throws<Utf8MemoryAdmissionException>(() =>
            regex.Match("éabc"u8, 0, output, workingMemoryAllowance: account));
        Assert.Equal(0UL, account.Held);
        Assert.All(output, range => Assert.False(range.Success));
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Held;
        internal ulong Peak;
        internal ulong Maximum = ulong.MaxValue;
        public bool TryReserve(ulong bytes)
        {
            if (bytes > Maximum - Held) return false;
            Held = checked(Held + bytes);
            Peak = Math.Max(Peak, Held);
            return true;
        }
        public void Release(ulong bytes) { Assert.True(bytes <= Held); Held -= bytes; }
    }
}
