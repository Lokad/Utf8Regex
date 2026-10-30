using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeCompilationTests
{
    public static IEnumerable<object[]> Patterns() => Utf8OwnedTreeUtf8Tests.Patterns();

    [Theory]
    [MemberData(nameof(Patterns))]
    public void PublicNativeProgramsPreserveSelectionAndCapturesAfterSourceReuse(string pattern, RegexOptions options)
    {
        var source = Encoding.UTF8.GetBytes(pattern);
        var native = Utf8Regex.Compile(source, options);
        var text = new Utf8Regex(pattern, options);
        Array.Fill(source, (byte)0xff);
        Assert.Equal(pattern, native.Pattern);
        Assert.Equal(text.GetGroupNumbers(), native.GetGroupNumbers());
        Assert.Equal(text.GetGroupNames(), native.GetGroupNames());
        string[] subjects = ["", "a", "abc", "aaaa", "ababc", "é😀😀b", "é😀é😀", "éê😀🙏",
            "xé😀aabc", "a\0é\u0301😀", "é \v \u00a0 😀😀", "\nabc\n", "KkKéÉ", "🙏😀x"];
        foreach (var subject in subjects)
        {
            var input = Encoding.UTF8.GetBytes(subject);
            Assert.Equal(text.IsMatch(input), native.IsMatch(input));
            Assert.Equal(text.Count(input), native.Count(input));
            Assert.Equal(text.Match(input), native.Match(input));
            var expectedDetailed = text.MatchDetailed(input);
            var actualDetailed = native.MatchDetailed(input);
            Assert.Equal(expectedDetailed.Success, actualDetailed.Success);
            if (expectedDetailed.Success)
                foreach (var number in text.GetGroupNumbers())
                {
                    var group = expectedDetailed.GetGroup(number);
                    var nativeGroup = actualDetailed.GetGroup(number);
                    Assert.Equal((group.Success, group.IndexInBytes, group.LengthInBytes),
                        (nativeGroup.Success, nativeGroup.IndexInBytes, nativeGroup.LengthInBytes));
                }
            using var expected = text.CreateMatchCursor(input);
            using var actual = native.CreateMatchCursor(input);
            while (expected.MoveNext(out var match))
            {
                Assert.True(actual.MoveNext(out var found));
                Assert.Equal(match.Value, found.Value);
                foreach (var number in match.GroupNumbers)
                {
                    Assert.True(match.TryGetGroup(number, out var group));
                    Assert.True(found.TryGetGroup(number, out var nativeGroup));
                    Assert.Equal(group, nativeGroup);
                    Assert.Equal(match.GetCaptures(number).ToArray(), found.GetCaptures(number).ToArray());
                }
            }
            Assert.False(actual.MoveNext(out _));
        }
    }

    [Theory]
    [InlineData("😀+", "a😀😀b", 1, 1, 9)]
    [InlineData("b", "a😀b", 5, 5, 6)]
    [InlineData("(?<=a)😀+", "a😀😀b", 1, 1, 9)]
    [InlineData("\\Aa", "a😀b", 0, 0, 1)]
    [InlineData("\\z", "a😀b", 6, 6, 6)]
    [InlineData("(?<present>😀)(?<empty>)(?<absent>x)?", "a😀b", 1, 1, 5)]
    public void ByteStartsRetainFullSubjectContext(string pattern, string subject, int start, int expectedStart, int expectedEnd)
    {
        var regex = Utf8Regex.Compile(Encoding.UTF8.GetBytes(pattern));
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes(subject), start);
        Assert.True(cursor.MoveNext(out var match));
        Assert.Equal(expectedStart, match.Value.StartOffsetInBytes);
        Assert.Equal(expectedEnd, match.Value.EndOffsetInBytes);
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void ContinuationByteStartsReject(int start)
        => Assert.Throws<ArgumentOutOfRangeException>(() => Utf8Regex.Compile("."u8).CreateMatchCursor("a😀b"u8.ToArray(), start));

    [Theory]
    [InlineData(new byte[] { 0xff })]
    [InlineData(new byte[] { 0xed, 0xa0, 0x80 })]
    [InlineData(new byte[] { 0xf0, 0x9f, 0x98 })]
    public void MalformedPatternsReject(byte[] source)
        => Assert.Throws<ArgumentException>(() => Utf8Regex.Compile(source));

    [Fact]
    public void SyntaxErrorsReportByteCoordinates()
    {
        var failure = Assert.ThrowsAny<ArgumentException>(() => Utf8Regex.Compile("😀)"u8));
        Assert.Contains("UTF-8 byte pattern offset", failure.Message);
        Assert.Equal("patternUtf8", failure.ParamName);
    }

    [Theory]
    [InlineData("Шерлок Холмс")]
    [InlineData("Шерлок Холмс|Джон Уотсон|Ирен Адлер")]
    public void NativeSourceKeepsTheMeasuredCyrillicCountStrategy(string pattern)
    {
        const RegexOptions options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var native = Utf8Regex.Compile(Encoding.UTF8.GetBytes(pattern), options);
        Assert.True(native.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        var input = "шерлок холмс; ДЖОН УОТСОН; ирен адлер"u8;
        Assert.Equal(new Utf8Regex(pattern, options).Count(input), native.Count(input));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public void NativeCompilationEnforcesEachConstructionLimit(int limit)
    {
        var policy = limit switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 },
        };
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8Regex.Compile("(?<x>😀){2,4}"u8,
            RegexOptions.None, Timeout.InfiniteTimeSpan, default, policy, Timeout.InfiniteTimeSpan, default));
        Assert.Equal(limit switch
        {
            0 => Utf8ResourceLimitKind.ConstructionWorkSteps,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            2 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            _ => Utf8ResourceLimitKind.ConstructionBytes,
        }, failure.ResourceKind);
        Assert.True(Utf8Regex.Compile("😀+"u8).IsMatch("😀😀"u8));
    }

    [Fact]
    public void NativeConstructionAndExecutionHaveIndependentOriginalTokensAndDeadlines()
    {
        using var construction = new CancellationTokenSource();
        var programs = new Account();
        programs.AfterGrant = construction.Cancel;
        var error = Assert.Throws<OperationCanceledException>(() => Utf8Regex.Compile("(?<x>😀)+"u8,
            RegexOptions.None, Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, construction.Token, programs));
        Assert.Equal(construction.Token, error.CancellationToken); Assert.Equal(0UL, programs.Used);

        using var successful = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var native = Utf8Regex.Compile("(?<x>😀)+"u8, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxWorkingBytes = 1_000_000, MaxResultBytes = 1_000_000 },
            default, TimeSpan.FromMilliseconds(20), successful.Token);
        successful.Cancel(); ticks = 1000;
        Assert.Equal(1, native.Count("😀😀"u8));
        using var invocation = new CancellationTokenSource(); invocation.Cancel();
        Assert.Equal(invocation.Token, Assert.Throws<OperationCanceledException>(() => native.Count("😀"u8, invocation.Token)).CancellationToken);
        Assert.Throws<ArgumentException>(() => native.IsMatch(new byte[] { 0xf0, 0x9f, 0x98, 0x80, 0xff }));
    }

    [Fact]
    public void NativeConstructionTimeoutDoesNotDecodeThePatternAfterExpiry()
    {
        var calls = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ++calls < 3 ? 0 : 100);
        var failure = Assert.Throws<RegexMatchTimeoutException>(() => Utf8Regex.Compile("(?<x>😀)+"u8,
            RegexOptions.None, Timeout.InfiniteTimeSpan, default, default, TimeSpan.FromMilliseconds(20), default));
        Assert.Equal(string.Empty, failure.Pattern);
    }

    [Fact]
    public async Task FundedNativeProgramSurvivesSourceReuseOwnerCloseAndCursorAliases()
    {
        var programs = new Account(); var working = new Account(); var results = new Account();
        var source = "(?<x>😀)+"u8.ToArray();
        var owner = Utf8Regex.Compile(source, RegexOptions.None, Timeout.InfiniteTimeSpan, default,
            default, Timeout.InfiniteTimeSpan, default, programs);
        Assert.True(programs.Used > 0);
        Array.Fill(source, (byte)0xff);
        var cursor = owner.CreateMatchCursor("😀😀 😀"u8.ToArray(), workingMemoryAllowance: working);
        var alias = cursor;
        Assert.True(cursor.MoveNextRetained(out var retained, resultMemoryAllowance: results));
        var copy = retained.Match;
        Assert.Equal(2, copy.GetCaptures(1).Length);
        owner.Dispose(); Assert.True(programs.Used > 0);
        await Task.Yield();
        Assert.True(alias.MoveNext(out _)); Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0UL, programs.Used); Assert.Equal(0UL, working.Used);
        cursor.Dispose(); Assert.True(results.Used > 0);
        retained.Dispose(); Assert.Equal(0UL, results.Used);
        Assert.Equal("😀😀"u8.ToArray(), copy.GetValueMemory().ToArray());
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        public ulong Used;
        public Action? AfterGrant;
        public bool TryReserve(ulong bytes) { Used = checked(Used + bytes); AfterGrant?.Invoke(); return true; }
        public void Release(ulong bytes) { Assert.True(bytes <= Used); Used -= bytes; }
    }
}
