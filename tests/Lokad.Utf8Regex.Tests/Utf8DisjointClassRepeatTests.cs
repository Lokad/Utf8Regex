using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DisjointClassRepeatTests
{
    [Theory]
    [InlineData("([a-z]+)-([0-9]+)", "abc-12 other-345")]
    [InlineData(@"([\x3f-\x40]{2,4})-", "???@- ??-?")]
    [InlineData(@"([\x00-\x7f]+)é", "é\0@\u007fé")]
    [InlineData(@"([\x3f\x41]+)@", "?A@ @@?A@")]
    [InlineData(@"([\x7e-\x7f]{0,3})é", "~\u007fé é")]
    [InlineData("([a-z]{2,3})-", "a- abc- abcde-")]
    [InlineData("([a-z]{0,3})-", "- abc- abcde-")]
    [InlineData("([a-z]{0})-", "abc--")]
    [InlineData("(?:([a-z]+)-x|([a-z]+)-y)", "abc-y abc-x")]
    [InlineData("(([a-z]+)-)*X", "abc-def-X")]
    [InlineData("(?=([a-z]+)-)([a-z]+)-", "abc-")]
    [InlineData("(?i:([a-z]+))-", "AbC- dEF-")]
    [InlineData(@"(\w+)-", "abc- def-")]
    [InlineData(@"(\p{L}+)-", "abc- def-")]
    [InlineData("([a-z]+)a", "baaa")]
    [InlineData("([a-z]+)(?i:A)", "baaa baA")]
    [InlineData("([^0-9]+)-", "abc-def-")]
    [InlineData("([a-z]+?)-", "abc-")]
    [InlineData("(([a-z]+))-", "abc-")]
    [InlineData("(?<outer>(?<inner>[a-z]+))-", "abc- def-")]
    [InlineData("(?<same>(?<same>[a-z]+))-", "abc-")]
    [InlineData("(([a-z]+))a", "baaa")]
    [InlineData("(([^']*))'", "abc😀' é'")]
    [InlineData("(?=(([a-z]+))-)", "abc-")]
    [InlineData("(?<=(([a-z]+)))b", "aab")]
    [InlineData("""^import\s+(?<shared>shared\s+)?"(?<path>(\.|[^\\"]*))".*$""", "import \"module😀.nvn\";")]
    [InlineData("([a-z])+-", "abc-")]
    [InlineData("([A-Z]{3})([0-9]{3})", "ABC12x ABC123 DEF456")]
    [InlineData("([a-z]{3})a", "abca abba")]
    [InlineData("([a-z]{3}?)a", "abca abba")]
    [InlineData("([a-z]){3}a", "abca abba")]
    [InlineData("(?:([a-z]{3})x|([a-z]{3})y)", "abc-y abcy abcx")]
    [InlineData("(?=([a-z]{3})a)([a-z]{3})a", "abca")]
    [InlineData("(?<=([a-z]{3}))a", "abca")]
    [InlineData("(?i:([a-z]{3}))a", "AbCa")]
    [InlineData(@"(\w{2})[a-z]", "ééa ABc")]
    [InlineData("(x*)-", "- xx- xxx-")]
    [InlineData("((x{1,2}))-", "x- xxx-")]
    [InlineData("(x*)x", "xxx")]
    [InlineData("(x{2})x", "xxxx")]
    [InlineData("(x)*-", "xxx-")]
    [InlineData("(?i:(x*))-", "XXX-")]
    [InlineData("(x*?)-", "xxx-")]
    [InlineData(@"(?=(x*))\1y", "xxxy")]
    [InlineData("(?<=(x{2}))y", "xxy")]
    [InlineData("(é*)-", "éé- é-")]
    [InlineData(@"(\d{2,4})\b", "123A 1234 12 1")]
    [InlineData(@"\b\d{1,2}/\d{1,2}/(\d{2,4})\b", "11/18/201A 11/19/2018")]
    [InlineData(@"(([a-z]{1,4}))\b", "abcd abcdef abc-")]
    [InlineData(@"(?=(([a-z]{1,4}))\b)\1", "abcd abcdef abc-")]
    [InlineData(@"(?<=(([a-z]{1,4}))\b)!", "abcd! abcdx!")]
    [InlineData(@"(a{1,4})\b", "aaaab aaaa a-")]
    [InlineData(@"(a{0,4})\b", "aaaab")]
    [InlineData(@"([a-z]{1,4})\B", "abcd!")]
    [InlineData(@"(.{1,4})\b", "abc !")]
    [InlineData(@"([^a]{1,4})\b", "x !a")]
    [InlineData(@"([a-z]{1,9})\b", "abcdefghi abcdefghix")]
    [InlineData(@"([a-z]{1,4}?)\b", "abcd abcdef")]
    [InlineData(@"((?i:[a-z]){1,4})\b", "AbCd AbCdX")]
    [InlineData(@"([a-z]{1,4})\bX|([a-z]{1,4})\b!", "abcd! abcdX")]
    [InlineData(@"([a-z]{1,8})\b", "alpha beta gamma")]
    [InlineData(@"([a-z ]{1,8})\b", "alpha beta gamma")]
    [InlineData(@"([^x]{1,8})\b", "alpha beta gamma")]
    [InlineData(@"(.{1,8})\b", "alpha beta gamma")]
    [InlineData(@"(.{1,8})\b", "abc\ndef")]
    [InlineData(@"(\d{1,4})\b", "12-345/6")]
    [InlineData(@"(?<run>[a-z]+)(?:-|:)", "abc- def: abc? a:-")]
    [InlineData(@"(?<run>a+)(?:b|a)c", "aaac aaabc")]
    [InlineData(@"(?<run>a+)(?:a|b)c", "aaac aaabc")]
    [InlineData(@"(?<run>a+)(?:b|(?i:a))c", "aaaAc aaabc")]
    [InlineData(@"(?<run>a+)(?:b|(?=a)a)c", "aaac aaabc")]
    [InlineData(@"(?<run>a+)(?:b|)", "aaa aaab")]
    [InlineData(@"(?<run>a+)(?:(?<tail>b)|(?<tail>c))", "aaac aaab")]
    [InlineData(@"(?=(?<run>a+)(?:b|c))\k<run>", "aaab aaac")]
    [InlineData(@"(?<run>a+)(?:b|c)\k<run>", "aabaa aacaa")]
    [InlineData(@"(?<pop>a+)(?<-pop>b|c)(?(pop)d|e)", "aaabe abe")]
    [InlineData(@"(?<run>a+)(?<end>b)?c", "aaabc aaac")]
    [InlineData("""^import\s+(?<shared>shared\s+)?"(?<path>(\.|[^\\"]*))".*$""", "import shared \"module.nvn\";")]
    public void CapturesAndFallbackKeepOverlappingBclSemantics(string pattern, string text)
    {
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var input = Encoding.UTF8.GetBytes(text);
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Value, actual.GetValueString());
        foreach (var number in oracle.GetGroupNumbers())
        {
            if (number == 0) continue;
            var wanted = expected.Groups[number];
            var group = actual.GetGroup(number);
            Assert.Equal(wanted.Success, group.Success);
            Assert.Equal(wanted.Value, group.GetValueString());
            Assert.Equal(wanted.Captures.Count, group.CaptureCount);
            for (var index = 0; index < wanted.Captures.Count; index++)
                Assert.Equal(wanted.Captures[index].Value, group.GetCapture(index).GetValueString());
        }
        Assert.Equal(oracle.Count(text), regex.Count(input));
    }

    [Theory]
    [InlineData("([é😀]{1,2})-", "é😀-", "é😀")]
    [InlineData("([é😀]{2,3})-", "é-", null)]
    [InlineData("([é😀]*)-", "-", "")]
    [InlineData("(([é😀]{1,2}))-", "é😀-", "é😀")]
    [InlineData("(([é😀]{2,3}))-", "é-", null)]
    [InlineData("([é😀]{2})[é😀]", "é😀é", "é😀")]
    [InlineData("([é😀]{2})[é😀]", "é😀", null)]
    [InlineData("([é😀]{0})[é😀]", "😀", "")]
    public void RepeatBoundsCountCompleteScalars(string pattern, string text, string? expected)
    {
        var match = new Utf8Regex(pattern).MatchDetailed(Encoding.UTF8.GetBytes(text));
        Assert.Equal(expected is not null, match.Success);
        if (expected is not null)
        {
            Assert.Equal(text, match.GetValueString());
            Assert.Equal(expected, match.GetGroup(1).GetValueString());
            Assert.Equal(Encoding.UTF8.GetByteCount(text), match.LengthInBytes);
        }
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4097)]
    public void AsciiCaptureBlocksRespectFiniteMaximaAndSupplementaryPrefixes(int length)
    {
        var input = Encoding.UTF8.GetBytes("😀" + new string('a', length) + "-");
        var policy = new Utf8ResourcePolicy { MaxWorkSteps = 100_000, MaxDepth = 256,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        using var token = new CancellationTokenSource();
        var match = new Utf8Regex("([a-z]{1,4096})-", RegexOptions.None,
            Timeout.InfiniteTimeSpan, policy).MatchDetailed(input, token.Token);
        Assert.True(match.Success);
        Assert.Equal(4 + Math.Max(0, length - 4096), match.IndexInBytes);
        Assert.Equal(Math.Min(length, 4096) + 1, match.LengthInBytes);
        Assert.Equal(new string('a', Math.Min(length, 4096)), match.GetGroup(1).GetValueString());
    }

    [Fact]
    public void RightToLeftKeepsItsExistingTraversalAndCapture()
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.RightToLeft;
        var match = new Utf8Regex("([a-z]+)-", options).MatchDetailed("abc- def-"u8);
        Assert.Equal("def-", match.GetValueString());
        Assert.Equal("def", match.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void ForkTailsKeepOtherExecutionOptions(RegexOptions option)
    {
        const string pattern = @"(?<run>a+)(?:(?<tail>b)|c)";
        const string text = "aaab aac";
        var options = RegexOptions.CultureInvariant | option;
        var oracle = new Regex(pattern, options);
        var regex = new Utf8Regex(pattern, options);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
        var expected = oracle.Match(text);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected.Value, actual.GetValueString());
        Assert.Equal(expected.Groups["run"].Value, actual.GetGroup(regex.GroupNumberFromName("run")).GetValueString());
        Assert.Equal(expected.Groups["tail"].Value, actual.GetGroup(regex.GroupNumberFromName("tail")).GetValueString());
    }

    [Theory]
    [InlineData("[é😀]+(?:x|y)", "é😀y", "é😀")]
    [InlineData("[é😀]{1,2}(?:x|y)", "é😀x", "é😀")]
    [InlineData("[é😀]+(?:x|😀)z", "é😀😀z", "é😀")]
    [InlineData("[é😀]{2,3}(?:x|y)", "éy", null)]
    public void ForkTailBoundsAndOverlapUseScalars(string body, string text, string? expected)
    {
        var split = body.IndexOf("(?:", StringComparison.Ordinal);
        var regex = new Utf8Regex("(?<run>" + body[..split] + ")" + body[split..]);
        var input = Encoding.UTF8.GetBytes(text);
        var actual = regex.MatchDetailed(input);
        Assert.Equal(expected is not null, actual.Success);
        Assert.Equal(expected is null ? 0 : 1, regex.Count(input));
        if (expected is not null)
        {
            Assert.Equal(expected, actual.GetGroup(regex.GroupNumberFromName("run")).GetValueString());
            Assert.Equal(input.Length, actual.LengthInBytes);
        }
    }

    [Fact]
    public void ForkTailCursorResultsSurviveLaterBranchesAndDisposal()
    {
        var regex = new Utf8Regex(@"(?<run>[a-z]+)(?:(?<tail>-)|:)");
        var input = "abc- def:"u8.ToArray();
        var cursor = regex.CreateMatchCursor(input);
        Assert.True(cursor.MoveNext(out var first));
        var alias = first;
        Assert.True(cursor.MoveNext(out var second));
        Assert.Equal("def:", Encoding.UTF8.GetString(second.GetValueMemory().Span));
        Assert.False(cursor.MoveNext(out _));
        cursor.Dispose();
        Assert.Equal("abc-", Encoding.UTF8.GetString(alias.GetValueMemory().Span));
        Assert.Equal("abc", Encoding.UTF8.GetString(alias.GetGroupMemory(regex.GroupNumberFromName("run")).Span));
        Assert.Equal("-", Encoding.UTF8.GetString(alias.GetGroupMemory(regex.GroupNumberFromName("tail")).Span));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(31)] [InlineData(32)] [InlineData(33)]
    public void NestedCaptureTailsKeepResultsAcrossBoundedInspection(int nesting)
    {
        var pattern = "(?<run>a+)(?:" + new string('(', nesting) + "b" + new string(')', nesting) + "|c)";
        const string text = "aaab aac";
        var oracle = new Regex(pattern);
        var regex = new Utf8Regex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(oracle.Count(text), regex.Count(input));
        var actual = regex.MatchDetailed(input);
        var expected = oracle.Match(text);
        foreach (var number in oracle.GetGroupNumbers())
            Assert.Equal(expected.Groups[number].Value, actual.GetGroup(number).GetValueString());
    }

    [Fact]
    public void ForkTailProofKeepsEveryWorkCutoffAndParentLoan()
    {
        var regex = new Utf8Regex(@"(?<run>a+)(?:(?<tail>b)|c)");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = "aaab aac"u8.ToArray();
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(2, result.Count);
        Assert.Equal(13UL, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit < baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
        byte[] malformed = [.. input, 0xff];
        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        var limited = new Utf8Regex(regex.Pattern, RegexOptions.None, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        Assert.Equal(2, limited.Count(input));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ForkTailMatchingObservesOriginalInterruption(bool timeout)
    {
        var regex = new Utf8Regex(@"(?<run>a+)(?:b|c)", RegexOptions.None, TimeSpan.FromMilliseconds(20));
        var input = Encoding.UTF8.GetBytes(new string('a', 200000) + "b");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (entered || !new System.Diagnostics.StackTrace().GetFrames().Any(frame =>
                frame.GetMethod()?.DeclaringType?.Name == nameof(Utf8SliceRunner))) return;
            entered = true;
            if (timeout) ticks = 25; else token.Cancel();
        });
        try
        {
            if (timeout) Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input, token.Token));
            else Assert.Equal(token.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, token.Token)).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(31)] [InlineData(32)] [InlineData(33)]
    public void OnlyProvedUselessAlternativesLoseTheirDepthRequirement(int nesting)
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 1, MaxWorkSteps = 100000,
            MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 };
        var text = Encoding.UTF8.GetBytes(new string('a', 2048) + "-");
        var disjoint = new Utf8Regex(new string('(', nesting) + "[a-z]+" + new string(')', nesting) + "-",
            RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, policy);
        if (nesting <= 32) Assert.Equal(1, disjoint.Count(text));
        else Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => disjoint.Count(text)).ResourceKind);
        var overlapping = new Utf8Regex("([a-z]+)a", RegexOptions.CultureInvariant,
            Timeout.InfiniteTimeSpan, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => overlapping.Count(text));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, error.ResourceKind);
    }

    [Theory]
    [InlineData(1, false, false)] [InlineData(2, false, false)]
    [InlineData(1, true, false)] [InlineData(2, true, false)]
    [InlineData(1, false, true)] [InlineData(2, false, true)]
    public void EveryWorkCutoffDeniesWithoutLeakingTheParentLoan(int nesting, bool exact, bool boundary)
    {
        var regex = new Utf8Regex(new string('(', nesting) + (boundary ? @"\d{2,4}" : exact ? @"\p{L}{2}" : @"\p{L}{2,3}") +
            new string(')', nesting) + (boundary ? @"\b" : exact ? "[A-Z]" : "-"));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var input = Encoding.UTF8.GetBytes(boundary ? "12 567" : exact ? "ééA ABB" : "éé- AB-");
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(13));
        var result = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref baseline);
        Assert.Equal(Utf8ExecutionLimitKind.None, result.Breach);
        Assert.Equal(2, result.Count);
        Assert.Equal(13ul, baseline.Ledger.ReservedBytes);
        for (ulong limit = 1; limit < baseline.Budget.Steps; limit++)
        {
            var invocation = new Utf8SliceInvocation(new(limit, 1, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(13));
            var denied = Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, 1, false, false)] [InlineData(true, 1, false, false)]
    [InlineData(false, 2, false, false)] [InlineData(true, 2, false, false)]
    [InlineData(false, 1, true, false)] [InlineData(true, 1, true, false)]
    [InlineData(false, 1, false, true)] [InlineData(true, 1, false, true)]
    public void OriginalInterruptionInsideScanReleasesScratch(bool timeout, int nesting, bool exact, bool ascii)
    {
        var regex = new Utf8Regex(new string('(', nesting) + (ascii ? "[a-z]+" : exact ? @"\p{L}{2048}" : @"\p{L}+") +
            new string(')', nesting) + (exact ? "." : "-"));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var input = Encoding.UTF8.GetBytes((ascii ? new string('a', 262144) : new string('é', 2048)) + "-");
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var session = Utf8SearchDiagnosticsSession.Start(_ => { if (timeout) ticks = 25; else token.Cancel(); });
        var invocation = new Utf8SliceInvocation(new(100000, 1, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        try
        {
            var starts = new int[program.GroupCount];
            var ends = new int[program.GroupCount];
            var failure = Record.Exception(() => Utf8SliceRunner.CountFromBytes(program, input, 0, starts, ends, ref invocation));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(13ul, invocation.Ledger.ReservedBytes);
            Assert.True(invocation.Budget.Steps >= 64);
        }
        finally { session.Complete(); }
    }
    [Theory]
    [InlineData(0, 1)] [InlineData(1, 31)] [InlineData(3, 32)]
    [InlineData(7, 63)] [InlineData(1, 64)] [InlineData(3, 2047)]
    [InlineData(7, 2048)] [InlineData(1, 2049)] [InlineData(4, 4097)]
    public void AsciiRangeRunsKeepOffsetsAndStopBeforeWiderScalars(int padding, int length)
    {
        var regex = new Utf8Regex("(?<run>[a-z]+)");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var prefix = Encoding.UTF8.GetBytes("é😀 " + new string('p', padding));
        byte[] input = [.. prefix, .. Encoding.ASCII.GetBytes(new string('a', length)), .. "😀#"u8];
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var invocation = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(invocation.Ledger.TryReserve(17));
        var actual = Utf8SliceRunner.TryMatchAt(program, input, prefix.Length, starts, ends, false, true, ref invocation);
        Assert.True(actual.IsMatch);
        Assert.Equal(Utf8ExecutionLimitKind.None, actual.Breach);
        Assert.Equal(prefix.Length + length, actual.EndIndex);
        Assert.Equal(prefix.Length, starts[0]);
        Assert.Equal(prefix.Length + length, ends[0]);
        Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void AsciiRangeRunKeepsEveryWorkCutoffAndTheBorrowedLoan()
    {
        var regex = new Utf8Regex("([a-z]+)");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.ASCII.GetBytes(new string('a', 4097));
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        var baseline = new Utf8SliceInvocation(new(100000, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
        Assert.True(baseline.Ledger.TryReserve(17));
        var result = Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, false, true, ref baseline);
        Assert.True(result.IsMatch);
        Assert.Equal(17UL, baseline.Ledger.ReservedBytes);
        for (ulong work = 1; work < baseline.Budget.Steps; work++)
        {
            var invocation = new Utf8SliceInvocation(new(work, 128, 1 << 20), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(17));
            var denied = Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, false, true, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, denied.Breach);
            Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AsciiRangeBlocksObserveOriginalInterruption(bool deadline)
    {
        var regex = new Utf8Regex("([a-z]+)");
        var program = regex.Inspection.AvailableOwnedSliceProgram!;
        var input = Encoding.ASCII.GetBytes(new string('a', 1 << 20));
        var starts = new int[program.GroupCount];
        var ends = new int[program.GroupCount];
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(10000000, 128, 1 << 20), TimeSpan.FromMilliseconds(20), token.Token);
        Assert.True(invocation.Ledger.TryReserve(17));
        var entered = false;
        invocation.CheckpointHook = _ =>
        {
            if (entered || !new System.Diagnostics.StackTrace().GetFrames().Any(frame =>
                frame.GetMethod()?.Name == "RunClassRepeat")) return;
            entered = true;
            if (deadline) ticks = 25; else token.Cancel();
        };
        if (deadline)
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
                Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, false, true, ref invocation));
        else
            Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                Utf8SliceRunner.TryMatchAt(program, input, 0, starts, ends, false, true, ref invocation)).CancellationToken);
        Assert.True(entered);
        Assert.Equal(17UL, invocation.Ledger.ReservedBytes);
    }

}
