using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8OwnedTreeUtf8Tests
{
    public static IEnumerable<object[]> Patterns()
    {
        foreach (var row in Utf8OwnedTreeParserTests.AcceptCases())
            yield return [(string)row[0]!, RegexOptions.CultureInvariant];
        yield return ["😀+", RegexOptions.CultureInvariant];
        yield return ["(?<=é)(?<nomé>😀+)(?<empty>)(?<absent>x)?", RegexOptions.CultureInvariant];
        yield return [@"(?<x>[é-ê😀-🙏]+)\k<x>", RegexOptions.CultureInvariant];
        yield return [@"[é-ê-[ê]]+", RegexOptions.CultureInvariant];
        yield return [@"[\uD83D\uDE00-🙏]", RegexOptions.CultureInvariant];
        yield return ["\\😀+|\\é", RegexOptions.CultureInvariant | RegexOptions.ECMAScript];
        yield return [@"\uD83D\uDE00+", RegexOptions.CultureInvariant];
        yield return ["a\0é\u0301😀", RegexOptions.CultureInvariant];
        yield return ["(?x:# 😀é comment\n é \v \u00a0 😀+)", RegexOptions.CultureInvariant];
        yield return [@"(?<5>é)(?<x>😀)?(?<x>a)?(?(x)b|c)", RegexOptions.CultureInvariant];
        yield return ["(?i:é|K|k)+", RegexOptions.CultureInvariant];
        yield return ["(?<x>😀|é)+", RegexOptions.CultureInvariant | RegexOptions.RightToLeft];
        yield return ["(?:😀|é)+", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking];
        yield return ["a{1}é{1}😀{1}b", RegexOptions.CultureInvariant];
        yield return ["(?i:\\É\\u0041😀)+", RegexOptions.CultureInvariant | RegexOptions.ECMAScript];
        yield return ["(?(xyz)é|😀)", RegexOptions.CultureInvariant];
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void NativeGrammarLowersToExistingScalarExecution(string pattern, RegexOptions options)
    {
        var bytes = Encoding.UTF8.GetBytes(pattern);
        var tree = Utf8OwnedTreeParser.TryParse(bytes, options, out var diagnostic);
        Assert.False(diagnostic.IsSyntaxError, diagnostic.ToString());
        Assert.NotNull(tree);
        var program = Utf8SliceParser.TryParse(options, tree);
        Assert.NotNull(program);
        var text = new Utf8Regex(pattern, options);
        Assert.Equal(text.GetGroupNumbers(), tree.CaptureCatalog!.GroupNumbers);
        Assert.Equal(text.GetGroupNames(), tree.CaptureNames);
        // Parsing borrows only for this call, including names and literal runs.
        Array.Fill(bytes, (byte)0xff);
        string[] subjects = ["", "a", "abc", "aaaa", "ababc", "é😀😀b", "é😀é😀", "éê😀🙏",
            "xé😀aabc", "a\0é\u0301😀", "é \v \u00a0 😀😀", "\nabc\n", "KkKéÉ", "🙏😀x"];
        foreach (var subject in subjects)
        {
            var input = Encoding.UTF8.GetBytes(subject);
            var expected = text.MatchDetailed(input);
            var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
            var actual = Utf8SliceRunner.TryFindFirst(program, input, program.RightToLeft ? input.Length : 0,
                starts, ends, false, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, actual.Breach);
            Assert.Equal(expected.Success, actual.Found);
            if (!actual.Found) continue;
            Assert.Equal((expected.IndexInBytes, expected.IndexInBytes + expected.LengthInBytes), (actual.StartByte, actual.EndByte));
            var numbers = tree.CaptureCatalog.GroupNumbers;
            for (var slot = 0; slot < numbers.Length - 1; slot++)
            {
                var group = expected.GetGroup(numbers[slot + 1]);
                Assert.Equal(group.Success, starts[slot] >= 0 && ends[slot] >= 0);
                if (group.Success)
                    Assert.Equal((group.IndexInBytes, group.IndexInBytes + group.LengthInBytes),
                        (Math.Min(starts[slot], ends[slot]), Math.Max(starts[slot], ends[slot])));
            }
        }
    }

    [Theory]
    [InlineData("😀)")]
    [InlineData("é[😀")]
    [InlineData("😀(?< a>x)")]
    [InlineData("é\\q")]
    [InlineData("😀[ê-é]")]
    [InlineData("😀(?<x>a)\\k<missing>")]
    public void NativeDiagnosticsNameByteCoordinatesWithoutChangingTextUnits(string pattern)
    {
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant, out var text));
        Assert.Null(Utf8OwnedTreeParser.TryParse(Encoding.UTF8.GetBytes(pattern), RegexOptions.CultureInvariant, out var native));
        Assert.Equal(text.Error, native.Error);
        Assert.Equal(Encoding.UTF8.GetByteCount(pattern.AsSpan(0, text.Offset)), native.Offset);
        Assert.False(text.IsUtf8); Assert.True(native.IsUtf8);
        Assert.Contains("UTF-8 byte pattern offset", native.ToException().Message);
        Assert.Contains("UTF-16 pattern offset", text.ToException().Message);
    }

    [Theory]
    [InlineData(new byte[] { 0xff })]
    [InlineData(new byte[] { (byte)'a', 0xc0, 0x80 })]
    [InlineData(new byte[] { 0xed, 0xa0, 0x80 })]
    [InlineData(new byte[] { 0xf4, 0x90, 0x80, 0x80 })]
    [InlineData(new byte[] { 0xf0, 0x9f, 0x98 })]
    public void MalformedPatternsRejectBeforeParserStorage(byte[] pattern)
    {
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Throws<ArgumentException>(() => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None, out _));
        Assert.Equal(0UL, owner.Budget.PeakBytes);
    }

    [Fact]
    public void InterruptedValidationPrecedesMalformedTailAndAllocation()
    {
        var pattern = Enumerable.Repeat((byte)'a', 10_000).Append((byte)0xff).ToArray();
        using var token = new CancellationTokenSource();
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.CancellationToken = token.Token;
        var calls = 0;
        owner.Budget.CheckpointHook = _ => { if (++calls == 3) token.Cancel(); };
        var error = Assert.Throws<OperationCanceledException>(() => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None, out _));
        Assert.Equal(token.Token, error.CancellationToken);
        Assert.Equal(0UL, owner.Budget.PeakBytes);
    }

    [Fact]
    public void NativeValidationHasAnEnforceableConstructionWorkLimit()
    {
        using var owner = new Utf8ConstructionScope(100_000, 100_000, 4, 100);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8OwnedTreeParser.TryParse("😀😀😀😀😀"u8, RegexOptions.None, out _));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(0UL, owner.Budget.PeakBytes);
    }

    [Fact]
    public void NativeValidationObservesOriginalDeadlineBeforeAllocation()
    {
        var pattern = Enumerable.Repeat((byte)'a', 10_000).Append((byte)0xff).ToArray();
        using var owner = Utf8ConstructionScope.Enter();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        var calls = 0;
        owner.Budget.CheckpointHook = _ => { if (++calls == 3) ticks = 25; };
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None, out _));
        Assert.Equal(0UL, owner.Budget.PeakBytes);
    }

    [Fact]
    public void NativeTreeAcquisitionStillRequiresConstructionAdmission()
    {
        using var owner = new Utf8ConstructionScope(100_000, 0);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8OwnedTreeParser.TryParse("(?<nomé>😀+)"u8, RegexOptions.None, out _));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.Equal(0UL, owner.Budget.PeakBytes);
    }

    [Fact]
    public void LargeNativeCommentsDoNotAcquireWholePatternText()
    {
        static ulong Peak(byte[] pattern)
        {
            using var owner = Utf8ConstructionScope.Enter();
            Assert.NotNull(Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.None, out _));
            return owner.Budget.PeakBytes;
        }
        var small = Peak("(?#comment)x"u8.ToArray());
        var large = Peak(Encoding.UTF8.GetBytes("(?#" + string.Concat(Enumerable.Repeat("😀é", 8192)) + ")x"));
        Assert.Equal(small, large);
    }

    [Theory]
    [InlineData("plainascii")]
    [InlineData("é😀e\u0301\0🙏")]
    [InlineData("é{1}😀{1}x")]
    public void NativeLiteralPayloadsOwnUtf8WithoutRetainedLiteralText(string pattern)
    {
        var input = Encoding.UTF8.GetBytes(pattern);
        var tree = Utf8OwnedTreeParser.TryParse(input, RegexOptions.CultureInvariant, out _);
        Assert.NotNull(tree);
        var literal = tree.Root.Child(0);
        Assert.Equal(Utf8TreeNodeKind.Multi, literal.Kind);
        Assert.Null(literal.Str);
        var expected = Encoding.UTF8.GetBytes(pattern.Replace("{1}", "", StringComparison.Ordinal));
        Assert.Equal(expected, literal.LiteralUtf8);
        Array.Fill(input, (byte)0xff);
        Assert.Equal(expected, literal.LiteralUtf8);
    }
}
