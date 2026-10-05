using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceUnitSubjectTests
{
    public static IEnumerable<object[]> Patterns()
    {
        string[] patterns = [
            "([^x]b)+", "(.+?)", "((.)+)", "(.{2,3})", "(.{2,}?)", "((.){2})",
            "((.){2})\\1", "(\\D{2,}?)", "(?=(.))", "(?<=(.))a", "(?<=.+)a",
            "([a\\p{IsHighSurrogates}]{2,})", "([a\\p{IsLowSurrogates}]{2,})",
            "([^\\p{Cs}]{2,})", "([\\p{Cs}]{2,})", "(\\W+?)", "(\\D*)",
            "(?i)([^x]+?)", "(a|.)+", "(?<x>.)(?<-x>.)", "(?<=.)\\b",
            "\\G(.)", "(?>.{1,2}).", "(?<x>.)(?(x).|a)",
        ];
        foreach (var pattern in patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None)];
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void PublicMatchingKeepsUnitsCaptureHistoriesAndOriginalBytes(string pattern, RegexOptions options)
    {
        string[] texts = ["\U0001f600", "a\U0001f600b", "\U0001f600b\U0001f600b",
            "a\U0001f600a\U0001f600a", "\U0001f600\U0001f600", "\u00e9\U00010400a", "a\n\U0001f600b", ""];
        var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(10));
        using var source = new CancellationTokenSource();
        foreach (var text in texts)
        {
            var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
            Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
            AssertRange(text, oracle.Match(text), regex.Match(input, source.Token));
            AssertDetailed(oracle.Match(text), regex.MatchDetailed(input, source.Token));
            AssertSequence(text, oracle.EnumerateMatches(text), regex.EnumerateMatches(input, source.Token));
            foreach (var count in new[] { 0, 2, 4 })
                AssertSplits(text, oracle.EnumerateSplits(text, count), regex.EnumerateSplits(input, count, source.Token));
            for (var offset = 0; offset <= text.Length; offset++)
            {
                Assert.Equal(oracle.IsMatch(text.AsSpan(), offset), regex.IsMatchFromUtf16Offset(input, offset, source.Token));
                Assert.Equal(oracle.Count(text.AsSpan(), offset), regex.CountFromUtf16Offset(input, offset, source.Token));
                AssertRange(text, oracle.Match(text, offset), regex.MatchFromUtf16Offset(input, offset, source.Token));
                AssertDetailed(oracle.Match(text, offset), regex.MatchDetailedFromUtf16Offset(input, offset, source.Token));
                AssertSequence(text, oracle.EnumerateMatches(text.AsSpan(), offset), regex.EnumerateMatchesFromUtf16Offset(input, offset));
            }
            if (text.Length != 0)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, pattern + " " + options);
                Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated, pattern + " " + options);
            }
        }
    }

    private static void AssertRange(string text, Match expected, Utf8ValueMatch actual)
    {
        AssertRange(text, expected.Success, expected.Index, expected.Length, actual);
    }

    private static void AssertRange(string text, bool success, int index, int length, Utf8ValueMatch actual)
    {
        Assert.Equal(success, actual.Success);
        Assert.Equal(index, actual.IndexInUtf16);
        Assert.Equal(length, actual.LengthInUtf16);
        if (!success) return;
        var map = Utf8BoundaryMap.Create(Encoding.UTF8.GetBytes(text));
        var start = map.Resolve(index);
        var end = map.Resolve(index + length);
        Assert.Equal(start.IsScalarBoundary && end.IsScalarBoundary, actual.IsByteAligned);
        Assert.Equal(actual.IsByteAligned, actual.TryGetByteRange(out var byteStart, out var byteLength));
        if (actual.IsByteAligned)
        {
            Assert.Equal(start.ByteOffset, byteStart);
            Assert.Equal(end.ByteOffset - start.ByteOffset, byteLength);
        }
    }

    private static void AssertDetailed(Match expected, Utf8MatchContext actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        for (var group = 0; group < expected.Groups.Count; group++)
        {
            var expectedGroup = expected.Groups[group];
            var actualGroup = actual.GetGroup(group);
            Assert.Equal(expectedGroup.Success, actualGroup.Success);
            Assert.Equal(expectedGroup.Index, actualGroup.IndexInUtf16);
            Assert.Equal(expectedGroup.Length, actualGroup.LengthInUtf16);
            Assert.Equal(expectedGroup.Value, actualGroup.GetValueString());
            Assert.Equal(expectedGroup.Captures.Count, actualGroup.CaptureCount);
            for (var capture = 0; capture < expectedGroup.Captures.Count; capture++)
            {
                var expectedCapture = expectedGroup.Captures[capture];
                var actualCapture = actualGroup.GetCapture(capture);
                Assert.Equal(expectedCapture.Index, actualCapture.IndexInUtf16);
                Assert.Equal(expectedCapture.Length, actualCapture.LengthInUtf16);
                Assert.Equal(expectedCapture.Value, actualCapture.GetValueString());
            }
        }
    }

    private static void AssertSequence(string text, Regex.ValueMatchEnumerator expected, Utf8ValueMatchEnumerator actual)
    {
        var seen = 0;
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            Assert.Equal(expected.Current.Index, actual.Current.IndexInUtf16);
            Assert.Equal(expected.Current.Length, actual.Current.LengthInUtf16);
            AssertRange(text, true, expected.Current.Index, expected.Current.Length, actual.Current);
            Assert.True(++seen <= text.Length * 2 + 10, "Owned enumeration failed to advance.");
        }
        Assert.False(actual.MoveNext());
        Assert.False(actual.MoveNext());
    }

    private static void AssertSplits(string text, Regex.ValueSplitEnumerator expected, Utf8ValueSplitEnumerator actual)
    {
        var map = Utf8BoundaryMap.Create(Encoding.UTF8.GetBytes(text));
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            var (index, length) = expected.Current.GetOffsetAndLength(text.Length);
            Assert.Equal(index, actual.Current.IndexInUtf16);
            Assert.Equal(length, actual.Current.LengthInUtf16);
            Assert.Equal(text.Substring(index, length), actual.Current.GetValueString());
            var start = map.Resolve(index);
            var end = map.Resolve(index + length);
            Assert.Equal(start.IsScalarBoundary && end.IsScalarBoundary, actual.Current.IsByteAligned);
            if (actual.Current.IsByteAligned)
            {
                Assert.Equal(start.ByteOffset, actual.Current.IndexInBytes);
                Assert.Equal(end.ByteOffset - start.ByteOffset, actual.Current.LengthInBytes);
            }
        }
        Assert.False(actual.MoveNext());
    }

    [Theory]
    [InlineData(@"a.{0,4}b", "\u00e9 a\U0001f600b a\u6f22b")]
    [InlineData(@"a\s{2,}b", "\u00e9 a \u2028b a\u00a0\u00a0b")]
    [InlineData(@"Holmes.{0,25}Watson|Watson.{0,25}Holmes", "Holmes\U0001f600Watson\nWatson\u6f22Holmes")]
    public void NativeWindowsKeepOwnedMatchingOnNonAsciiSubjects(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options);
            var oracle = new Regex(pattern, options);
            Assert.Equal("AsciiOrderedLiteralWindow", regex.Inspection.ExecutionKind.ToString());
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            AssertRange(text, oracle.Match(text), regex.Match(input));
            AssertDetailed(oracle.Match(text), regex.MatchDetailed(input));
            AssertSequence(text, oracle.EnumerateMatches(text), regex.EnumerateMatches(input));
            AssertSplits(text, oracle.EnumerateSplits(text), regex.EnumerateSplits(input));
            for (var offset = 0; offset <= text.Length; offset++)
                AssertSequence(text, oracle.EnumerateMatches(text.AsSpan(), offset), regex.EnumerateMatchesFromUtf16Offset(input, offset));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
    }

    [Fact]
    public void StorageAdmissionPrecedesConversionAndBalancesItsOwner()
    {
        var input = Encoding.UTF8.GetBytes("a\U0001f600b");
        var map = Utf8BoundaryMap.Create(input);
        const ulong payload = 8 + 5 * 4;
        var denied = new Utf8SliceInvocation(new(0, 0, payload - 1), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, Utf8SliceUnitSubject.TryCreate(input, map, ref denied, out _));
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);
        var admitted = new Utf8SliceInvocation(new(0, 0, payload), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SliceUnitSubject.TryCreate(input, map, ref admitted, out var subject));
        Assert.Equal(payload, admitted.Ledger.ReservedBytes);
        Assert.Equal(new byte[] { 0x61, 0xed, 0xa0, 0xbd, 0xed, 0xb8, 0x80, 0x62 }, subject.Input.ToArray());
        Assert.Equal(4, subject.Map.Utf16Length);
        Assert.True(subject.Map.Resolve(2).IsScalarBoundary);
        Assert.False(map.Resolve(2).IsScalarBoundary);
        subject.Release(ref admitted);
        subject.Release(ref admitted);
        Assert.Equal(0UL, admitted.Ledger.ReservedBytes);
        Assert.Throws<ArgumentException>(() => Utf8Validation.Validate([0xed, 0xa0, 0xbd]));
        var workLimited = new Utf8SliceInvocation(new(2, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8SliceUnitSubject.TryCreate(input, map, ref workLimited, out _));
        Assert.Equal(0UL, workLimited.Ledger.ReservedBytes);
    }

    [Fact]
    public void ConversionObservesCancellationAndReleasesAdmittedStorage()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 40000)));
        var map = Utf8BoundaryMap.Create(input);
        using var source = new CancellationTokenSource();
        // Detection and the first pass charge 41 steps. Step 64 occurs after storage admission
        // during the conversion pass, so cancellation exercises cleanup there.
        var session = Utf8SearchDiagnosticsSession.Start(steps => { if (steps == 64) source.Cancel(); });
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        OperationCanceledException? canceled = null;
        try { Utf8SliceUnitSubject.TryCreate(input, map, ref invocation, out _); }
        catch (OperationCanceledException exception) { canceled = exception; }
        finally { session.Complete(); }
        Assert.NotNull(canceled);
        Assert.Equal(source.Token, canceled.CancellationToken);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void BufferDeadlineDoesNotMultiplyTheVmPollingInterval()
    {
        var text = string.Concat(Enumerable.Repeat("\U0001f600", 40000));
        var input = Encoding.UTF8.GetBytes(text);
        var timeout = TimeSpan.FromMilliseconds(500);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps != 64) return;
            entered = true;
            Thread.Sleep(700);
        });
        try
        {
            var regex = new Utf8Regex("(.+?)", RegexOptions.CultureInvariant, timeout);
            var exception = Assert.Throws<RegexMatchTimeoutException>(() => regex.EnumerateMatches(input));
            Assert.True(entered);
            Assert.Equal(text, exception.Input);
            Assert.Equal("(.+?)", exception.Pattern);
            Assert.Equal(timeout, exception.MatchTimeout);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void AstralFreeDetectionStillObservesLiveCancellation()
    {
        var input = new byte[320000];
        Array.Fill(input, (byte)'a');
        var map = Utf8BoundaryMap.Create(input);
        using var source = new CancellationTokenSource();
        var session = Utf8SearchDiagnosticsSession.Start(steps => { if (steps == 64) source.Cancel(); });
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        OperationCanceledException? canceled = null;
        try { Utf8SliceUnitSubject.TryCreate(input, map, ref invocation, out _); }
        catch (OperationCanceledException exception) { canceled = exception; }
        finally { session.Complete(); }
        Assert.NotNull(canceled);
        Assert.Equal(source.Token, canceled.CancellationToken);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("bool")]
    [InlineData("count")]
    [InlineData("value")]
    [InlineData("detailed")]
    [InlineData("enumerate")]
    [InlineData("split")]
    [InlineData("offset-bool")]
    [InlineData("offset-count")]
    [InlineData("offset-value")]
    [InlineData("offset-detailed")]
    [InlineData("offset-enumerate")]
    [InlineData("split-count")]
    public void CancellationAfterConversionReachesEveryOwnedOperation(string operation)
    {
        // Three buffer checkpoints finish conversion. Step 64 occurs inside the
        // healthy VM match, so this also qualifies the newly routed operations.
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("\U0001f600", 256)));
        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex("((.)+)", options, Regex.InfiniteMatchTimeout);
            using var source = new CancellationTokenSource();
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(steps =>
            {
                entered = true;
                source.Cancel();
                if (steps >= 128) throw new InvalidOperationException("The VM lost the caller's token.");
            });
            try
            {
                var exception = Assert.Throws<OperationCanceledException>(() => Invoke(regex, input, operation, source.Token));
                Assert.True(entered);
                Assert.Equal(source.Token, exception.CancellationToken);
            }
            finally { session.Complete(); }
            Assert.Equal(1, regex.Count("ab"u8));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
            Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
        }
    }

    private static void Invoke(Utf8Regex regex, byte[] input, string operation, CancellationToken token)
    {
        switch (operation)
        {
            case "bool": _ = regex.IsMatch(input, token); break;
            case "count": _ = regex.Count(input, token); break;
            case "value": _ = regex.Match(input, token); break;
            case "detailed": _ = regex.MatchDetailed(input, token); break;
            case "enumerate": _ = regex.EnumerateMatches(input, token).MoveNext(); break;
            case "split": _ = regex.EnumerateSplits(input, token).MoveNext(); break;
            case "offset-bool": _ = regex.IsMatchFromUtf16Offset(input, 1, token); break;
            case "offset-count": _ = regex.CountFromUtf16Offset(input, 1, token); break;
            case "offset-value": _ = regex.MatchFromUtf16Offset(input, 1, token); break;
            case "offset-detailed": _ = regex.MatchDetailedFromUtf16Offset(input, 1, token); break;
            case "offset-enumerate": _ = regex.EnumerateMatchesFromUtf16Offset(input, 1, token).MoveNext(); break;
            case "split-count": _ = regex.EnumerateSplits(input, 4, token).MoveNext(); break;
            default: throw new InvalidOperationException(operation);
        }
    }
}
