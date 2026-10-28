using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLeadingAsciiRangeTests
{
    private const string Pattern = "(?P<word>[a-z]+)";
    private static readonly Utf8ExecutionLimits Limits = new(ulong.MaxValue, 32, 1 << 20, 1 << 20);

    private static Utf8SliceProgram Compile(string pattern)
    {
        var regex = new Utf8PythonRegex(pattern);
        return (Utf8SliceProgram)typeof(Utf8PythonRegex).GetField("_scalarProgram",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(regex)!;
    }

    [Theory]
    [InlineData("[a-z]+", 97, 122)]
    [InlineData("(?P<word>[a-z]+)", 97, 122)]
    [InlineData("([a-z]{2,4})!", 97, 122)]
    [InlineData(@"[\x00-\x7f]+", 0, 127)]
    [InlineData(@"[\x00-\x3f]+", 0, 63)]
    [InlineData(@"[\x40-\x7f]+", 64, 127)]
    [InlineData(@"[\x3f-\x40]+", 63, 64)]
    [InlineData(@"[\x7f]+", 127, 127)]
    [InlineData("[a-cd-f]+", 97, 102)]
    [InlineData("[A-Za-z_][A-Za-z0-9_]*", 65, 122)]
    [InlineData("(?P<word>[a-cx-z]+)", 97, 122)]
    [InlineData("[a-cx-z]+", 97, 122)]
    [InlineData("[ACE]+", 65, 69)]
    public void MandatoryMaskIsContainedInTheDerivedAsciiInterval(string pattern, int minimum, int maximum)
    {
        var program = Compile(pattern);
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(program, false, out var low, out var high));
        Assert.Equal(minimum, low); Assert.Equal(maximum, high);
        var mask = program.SetMasks[program.Instructions.First(instruction => instruction.Kind == Utf8SliceInstructionKind.Set).A];
        for (var scalar = 0; scalar < 128; scalar++)
        {
            if ((mask[scalar >> 3] & (1 << (scalar & 7))) != 0)
                Assert.InRange(scalar, low, high);
        }
        Assert.NotEqual(0, mask[low >> 3] & (1 << (low & 7)));
        Assert.NotEqual(0, mask[high >> 3] & (1 << (high & 7)));
    }

    [Theory]
    [InlineData("[^a-z]+")]
    [InlineData("[a-z]*Z")]
    [InlineData("([a-z]+)?Z")]
    [InlineData("[a-z]+?Z")]
    [InlineData("[é-ö]+")]
    public void UnprovedPrefixesKeepTheExistingCandidateLoop(string pattern)
    {
        Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), false, out _, out _));
    }

    [Theory]
    [InlineData("(?=[a-z])([a-z]+)")]
    [InlineData("(([a-z]+))")]
    [InlineData(@"\A([a-z]+)")]
    [InlineData("(?:^|(?=x))[a-z]+")]
    [InlineData("(?![0-9])[a-z]+")]
    public void ZeroWidthPrefixesConvergeOnTheSameMandatoryMask(string pattern)
    {
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), false, out var low, out var high));
        Assert.Equal(97, low); Assert.Equal(122, high);
    }

    [Theory]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", 48, 57)]
    [InlineData(@"\B\d+", 48, 57)]
    [InlineData(@"(?=[A-Z])\d+", 48, 57)]
    [InlineData(@"(?P<x>\d+)-(?P=x)", 48, 57)]
    [InlineData(@"(?<=:)\w+", 48, 122)]
    [InlineData(@"(?<!:)\s+", 9, 32)]
    public void ProfileCategoryProjectionRequiresAuthoritativeAsciiInput(string pattern, int minimum, int maximum)
    {
        var program = Compile(pattern);
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(program, true, out var low, out var high));
        Assert.Equal(minimum, low); Assert.Equal(maximum, high);
        Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(program, false, out _, out _));
    }

    [Theory]
    [InlineData(@"(?:\d+|[a-z]+)")]
    [InlineData(@"(?i:[a-z]+)")]
    [InlineData(@"(?P<x>[a-z]+)?\d+")]
    [InlineData(@"[\d\s]+")]
    public void NonConvergentOrUnprovedCategoryDefinitionsDecline(string pattern)
        => Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), true, out _, out _));

    [Fact]
    public void OptionalProofStopsAtItsFixedWorkBound()
    {
        var pattern = new string('(', 70) + "[a-z]+" + new string(')', 70);
        Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), true, out _, out _));
        Assert.True(new Utf8PythonRegex(pattern).IsMatch("123 alpha"u8));
    }

    [Theory]
    [InlineData(0)] [InlineData(2047)] [InlineData(2048)] [InlineData(2049)] [InlineData(4097)]
    public void CapturesAndCoordinatesSurviveSearchBlockTailsAndStartOffsets(int padding)
    {
        var prefix = new string('9', padding) + "😀";
        var text = prefix + "alpha𐐀beta!gamma";
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8PythonRegex(Pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 32, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        using var token = new CancellationTokenSource();
        Assert.Equal(new[] { "alpha", "beta", "gamma" }, regex.FindAllToStrings(input, 0, token.Token).ScalarValues);
        var matches = regex.FindIterDetailed(input, 0, token.Token);
        var first = matches[0];
        Assert.Equal(Encoding.UTF8.GetByteCount(prefix), first.Value.StartOffsetInBytes);
        Assert.Equal(prefix.Length, first.Value.StartOffsetInUtf16);
        Assert.Equal("alpha", first.Groups[1].ValueText);
        Assert.Equal(1, first.LastIndex); Assert.Equal("word", first.LastGroup);
        Assert.Equal(new[] { "beta", "gamma" }, regex.FindAllToStrings(input, matches[1].Value.StartOffsetInBytes, token.Token).ScalarValues);
        Assert.False(regex.Match(input).Success);
        Assert.False(regex.FullMatch(input).Success);
        Assert.True(regex.Match(input, first.Value.StartOffsetInBytes).Success);
        Assert.ThrowsAny<ArgumentException>(() => regex.FindAllToStrings(input, padding + 1));
        Assert.Equal("alpha", first.Groups[1].ValueText);
    }

    [Theory]
    [InlineData("([a-z]{2,3})!", "a9bb!ccc!dddd!", new[] { "bb", "ccc", "ddd" })]
    [InlineData("(?i)([a-z]+)", "9KſİıX", new[] { "KſİıX" })]
    [InlineData("(?ai)([a-z]+)", "9KſİıX", new[] { "X" })]
    [InlineData("([^a-z]+)", "abc😀12", new[] { "😀12" })]
    [InlineData(@"([\x00-\x7f]+)", "😀\0\u007fX𐐀", new[] { "\0\u007fX" })]
    [InlineData(@"([\x00-\x3f]+)", "😀\0?𐐀@", new[] { "\0?" })]
    [InlineData(@"([\x3f-\x40]+)", "9?@AX", new[] { "?@" })]
    [InlineData(@"([a-z]+)-\1", "99abc-abc xx", new[] { "abc" })]
    [InlineData("([a-z]+)(?=pha)", "9alpha", new[] { "al" })]
    [InlineData("([A-Za-z_][A-Za-z0-9_]*)", "99[\\]^`😀_id2 A3 z", new[] { "_id2", "A3", "z" })]
    [InlineData("([ACE]+)", "BDACEBDE", new[] { "ACE", "E" })]
    [InlineData(@"([a-cx-z]+)-\1", "mno ab-ab xyz-xyz", new[] { "ab", "xyz" })]
    public void VerificationRetainsMinimumTailNegationAndFolding(string pattern, string text, string[] expected)
        => Assert.Equal(expected, new Utf8PythonRegex(pattern).FindAllToStrings(Encoding.UTF8.GetBytes(text)).ScalarValues);

    [Fact]
    public void ImpossibleStartsStillRequireStrictWholeInputValidationAndOutputAdmission()
    {
        var invalid = Encoding.UTF8.GetBytes(new string('9', 8192)).Concat(new byte[] { 0x80 }).ToArray();
        Assert.ThrowsAny<ArgumentException>(() => new Utf8PythonRegex(Pattern).FindAllToStrings(invalid));
        foreach (var resultLimit in new[] { true, false })
        {
            var regex = new Utf8PythonRegex(Pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
                resultLimit ? new() { MaxResultBytes = 1 } : new() { MaxWorkingBytes = 1 });
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.FindAllToStrings("999 alpha"u8));
            Assert.Equal(resultLimit ? Utf8ResourceLimitKind.ExecutionResultBytes : Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        }
    }

    [Fact]
    public void DurableAliasesSharePositionAndRetainedCapturesSurviveEofAndDisposal()
    {
        var regex = new Utf8PythonRegex(Pattern);
        using var cursor = regex.CreateMatchCursor(Encoding.UTF8.GetBytes("999😀alpha 123 beta"));
        var alias = cursor;
        Assert.True(cursor.MoveNext());
        var first = alias.Current;
        Assert.True(alias.MoveNext());
        Assert.Equal("beta", cursor.Current.Groups[1].ValueText);
        Assert.False(cursor.MoveNext());
        alias.Dispose();
        Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext());
        Assert.Equal("alpha", first.Groups[1].ValueText);
        Assert.Equal(7, first.Value.StartOffsetInBytes);
        Assert.Equal("word", first.LastGroup);
    }

    [Fact]
    public void LaterCapturesKeepTheirValuesAndCompletionOrder()
    {
        var regex = new Utf8PythonRegex("([a-z]+)-([0-9]+)", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxWorkSteps = 100000, MaxDepth = 32, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 });
        var input = "999😀one-12 z-3 bad!"u8;
        var strings = regex.FindAllToStrings(input).TupleValues;
        var bytes = regex.FindAllToUtf8(input).TupleValues;
        Assert.Equal(2, strings.Length); Assert.Equal(2, bytes.Length);
        Assert.Equal(new[] { "one", "12" }, strings[0]); Assert.Equal(new[] { "z", "3" }, strings[1]);
        for (var index = 0; index < strings.Length; index++)
            Assert.Equal(strings[index], bytes[index].Select(Encoding.UTF8.GetString));
        Assert.All(regex.FindIterDetailed(input), match => Assert.Equal(2, match.LastIndex));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void WorkDenialInCandidateSearchKeepsItsAuthorityAndReleasesStorage(bool category)
    {
        var pattern = category ? @"\b(?P<digits>\d+)\b" : Pattern;
        var program = Compile(pattern);
        var input = Encoding.UTF8.GetBytes(new string(category ? 'x' : '9', 8192) + (category ? " 123" : "alpha"));
        var probe = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan,
            null, pattern, new(Limits, Timeout.InfiniteTimeSpan, default));
        var constructionWork = probe.WorkingInvocation.Budget.Steps;
        probe.Dispose();
        var cursor = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan,
            null, pattern, new(new(constructionWork + 1, 32, 1 << 20, 1 << 20), Timeout.InfiniteTimeSpan, default));
        Exception? error = null;
        try { cursor.MoveNext(); } catch (Exception failure) { error = failure; }
        finally { cursor.Dispose(); }
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, 1)] [InlineData(true, 1)]
    [InlineData(false, 2)] [InlineData(true, 2)]
    public void CandidateBlocksObserveOriginalTokenAndDeadlineAndKeepParentLoans(bool expire, int kind)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var pattern = kind switch { 1 => "(?P<word>[A-Za-z_]+)", 2 => @"\b(?P<digits>\d+)\b", _ => Pattern };
        var cursor = new PythonReScalarMatchCursor(Encoding.UTF8.GetBytes(new string(kind == 2 ? 'x' : '9', 200000) + (kind == 2 ? " 123" : "alpha")),
            Compile(pattern), 0, timeout,
            null, pattern, new(Limits, timeout, cancellation.Token));
        Assert.True(cursor.WorkingInvocation.Ledger.TryReserve(13));
        cursor.WorkingInvocation.ShareStorageForCursor();
        var observer = cursor.WorkingInvocation;
        var initialBytes = cursor.ReservedBytes;
        while ((cursor.WorkingInvocation.Budget.Steps & 63) != 62)
            Assert.Equal(Utf8ExecutionLimitKind.None, cursor.WorkingInvocation.Budget.TryChargeStep());
        var entered = false;
        cursor.WorkingInvocation.CheckpointHook = _ =>
        {
            entered = true;
            Assert.Equal(initialBytes, observer.Ledger.ReservedBytes); // No candidate entered the VM.
            if (expire) ticks = 50; else cancellation.Cancel();
        };
        Exception? error = null;
        try { cursor.MoveNext(); } catch (Exception failure) { error = failure; }
        finally { cursor.Dispose(); }
        Assert.True(entered);
        if (expire)
        {
            var failure = Assert.IsType<RegexMatchTimeoutException>(error);
            Assert.Equal(pattern, failure.Pattern); Assert.Equal(timeout, failure.MatchTimeout);
        }
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(error).CancellationToken);
        Assert.Equal(13UL, cursor.ReservedBytes);
    }
}
