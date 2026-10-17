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
        using var construction = global::Lokad.Utf8Regex.Internal.Utf8ConstructionScope.Enter();
        var parse = PythonReParser.Create(pattern).Parse(PythonReCompileOptions.None);
        var program = PythonReScalarCompiler.Compile(parse, PythonReTranslator.Translate(parse));
        construction.Complete();
        return program;
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
        Assert.True(PythonReScalarMatchState.TryGetLeadingAsciiRange(program, out var low, out var high));
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
    [InlineData("(?=[a-z])([a-z]+)")]
    [InlineData("(([a-z]+))")]
    [InlineData(@"\A([a-z]+)")]
    [InlineData("[é-ö]+")]
    public void UnprovedPrefixesKeepTheExistingCandidateLoop(string pattern)
    {
        Assert.False(PythonReScalarMatchState.TryGetLeadingAsciiRange(Compile(pattern), out _, out _));
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

    [Fact]
    public void WorkDenialInCandidateSearchKeepsItsAuthorityAndReleasesStorage()
    {
        var program = Compile(Pattern);
        var input = Encoding.UTF8.GetBytes(new string('9', 8192) + "alpha");
        var probe = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan, Pattern, default, Limits);
        var constructionWork = probe.WorkingInvocation.Budget.Steps;
        probe.Dispose();
        var cursor = new PythonReScalarMatchCursor(input, program, 0, Timeout.InfiniteTimeSpan, Pattern, default,
            new(constructionWork + 1, 32, 1 << 20, 1 << 20));
        Exception? error = null;
        try { cursor.MoveNext(); } catch (Exception failure) { error = failure; }
        finally { cursor.Dispose(); }
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
        Assert.Equal(0UL, cursor.ReservedBytes);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CandidateBlocksObserveOriginalTokenAndDeadlineAndKeepParentLoans(bool expire, bool disjoint)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var cancellation = new CancellationTokenSource();
        var timeout = expire ? TimeSpan.FromMilliseconds(20) : Timeout.InfiniteTimeSpan;
        var pattern = disjoint ? "(?P<word>[A-Za-z_]+)" : Pattern;
        var cursor = new PythonReScalarMatchCursor(Encoding.UTF8.GetBytes(new string('9', 200000) + "alpha"),
            Compile(pattern), 0, timeout, pattern, cancellation.Token, Limits);
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
