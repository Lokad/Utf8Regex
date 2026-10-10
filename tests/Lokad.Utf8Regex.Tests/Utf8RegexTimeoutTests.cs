using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexTimeoutTests
{
    [Fact]
    public void NativeSimpleExecutionRespectsMatchTimeout()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';

        var exception = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.Equal(input.Length, exception.Input.Length);
    }

    [Fact]
    public void NativeCategoryExecutionRespectsMatchTimeout()
    {
        // Category arms run the same per-visit budget steps as every other
        // arm: a catastrophic category shape times out with the same identity.
        var regex = new Utf8Regex(@"(\p{L}+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';

        var exception = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.Equal(input.Length, exception.Input.Length);
    }

    [Fact]
    public void NativeControlExecutionRespectsMatchTimeout()
    {
        // Control-literal arms run the same per-visit budget steps as every other
        // arm: a catastrophic control shape times out with the same identity.
        var regex = new Utf8Regex(@"(\t+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)9);
        input[^1] = (byte)33;

        var exception = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.Equal(input.Length, exception.Input.Length);
    }

    [Fact]
    public void NativeMatchEnumerationRespectsMatchTimeout()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';

        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateMatches(input);
            _ = enumerator.MoveNext();
        });
    }

    [Fact]
    public void NativeReplaceRespectsMatchTimeout()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';

        Assert.Throws<RegexMatchTimeoutException>(() => regex.Replace(input, "z"));
    }

    [Fact]
    public void NativeSplitEnumerationMapsDeadlineExpirationAtTheAdapter()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = CreateCatastrophicInput();

        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateSplits(input);
            _ = enumerator.MoveNext();
        });
    }

    [Fact]
    public void NativeCountAndMatchMapDeadlineExpirationAtTheFacade()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));

        Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(CreateCatastrophicInput()));
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(CreateCatastrophicInput()));
    }


    [Fact]
    public void TimedOutInstanceRemainsUsableForSubsequentOperations()
    {
        // PLAN.md M1: a deadline unwinds through pooled search, capture, and
        // replacement buffers; the same instance must keep answering with
        // oracle parity afterwards instead of retaining per-operation state.
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var oracle = new Regex(@"(a+)+$", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);

        Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(CreateCatastrophicInput()));

        byte[] input = Encoding.UTF8.GetBytes("aaabaaa");
        const string text = "aaabaaa";
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));

        var expectedMatch = oracle.Match(text);
        var actualMatch = regex.Match(input);
        Assert.Equal(expectedMatch.Success, actualMatch.Success);
        Assert.Equal(expectedMatch.Index, actualMatch.IndexInUtf16);
        Assert.Equal(expectedMatch.Length, actualMatch.LengthInUtf16);

        Assert.Equal(oracle.Replace(text, "z"), regex.ReplaceToString(input, "z"));

        var expectedIndexes = new List<int>();
        foreach (Match match in oracle.Matches(text))
        {
            expectedIndexes.Add(match.Index);
        }

        var actualIndexes = new List<int>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            actualIndexes.Add(match.IndexInUtf16);
        }

        Assert.Equal(expectedIndexes, actualIndexes);
    }
    private static byte[] CreateCatastrophicInput()
    {
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';
        return input;
    }
    [Fact]
    public void FromOffsetOperationsRespectMatchTimeout()
    {
        var regex = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var input = CreateCatastrophicInput();

        Assert.Throws<RegexMatchTimeoutException>(() => regex.MatchFromUtf16Offset(input, 0));
        Assert.Throws<RegexMatchTimeoutException>(() => regex.MatchDetailedFromUtf16Offset(input, 0));
        Assert.Throws<RegexMatchTimeoutException>(() => regex.CountFromUtf16Offset(input, 0));
    }
    [Fact]
    public void FiniteTimeoutPreservesFastPathResultsWithinBudget()
    {
        // This checks finite-policy route compatibility. Cold JIT and scheduler
        // time can exceed1ms, and projection now checks that original deadline.
        // Explicit expiration is covered by the timeout and projection witnesses.
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var input = Encoding.UTF8.GetBytes("xxabcxx");

        Assert.True(regex.IsMatch(input));
        Assert.True(regex.Match(input).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.Equal(Encoding.UTF8.GetBytes("xxZxx"), regex.Replace(input, "Z"));
        var matches = 0;
        foreach (var item in regex.EnumerateMatches(input))
        {
            matches++;
        }

        Assert.Equal(1, matches);
        var splits = 0;
        foreach (var item in regex.EnumerateSplits(input))
        {
            splits++;
        }

        Assert.Equal(2, splits);
        Assert.True(regex.MatchDetailed(input).Success);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SliceCatastrophicTimeoutAbortsAllLanes(bool astralTail)
    {
        // R05: slice catastrophic backtracking checks the invocation deadline
        // on every lane with the same timeout identity as the oracle, on ASCII
        // and astral-tailed inputs alike, without constructing fallback engines.
        var timeout = TimeSpan.FromMilliseconds(500);
        var text = new string('a', 20000) + (astralTail ? "\U0001F600!" : "!");
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex("(a|ab)+$", RegexOptions.CultureInvariant, timeout);
        Assert.Throws<RegexMatchTimeoutException>(() => oracle.IsMatch(text));
        var regex = new Utf8Regex("(a|ab)+$", RegexOptions.CultureInvariant, timeout);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var exception = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.Equal(text, exception.Input);
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input));
        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateMatches(input);
            _ = enumerator.MoveNext();
        });
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Replace(input, "z"));
        var destination = new byte[65536];
        Assert.Throws<RegexMatchTimeoutException>(() => regex.TryReplace(input, "z", destination, out var bytesWritten));
        Assert.Throws<RegexMatchTimeoutException>(() => regex.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) =>
        {
            writer.Append("["u8);
            writer.Append(context.GetGroup(0).GetValueBytes());
            writer.Append("]"u8);
        }));
        Assert.Throws<RegexMatchTimeoutException>(() => regex.ReplaceToString(input, 0, static (in Utf8MatchContext context, ref int state) => "[" + context.GetGroup(0).GetValueString() + "]"));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SliceFastMissNeverTimesOut(bool astralTail)
    {
        // Fast misses complete well inside generous budgets without throwing.
        var timeout = TimeSpan.FromSeconds(30);
        var text = new string('a', 50) + (astralTail ? "\U0001F600!" : "!");
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex("(a|ab)+$", RegexOptions.CultureInvariant, timeout);
        var expectedMiss = oracle.IsMatch(text);
        Assert.False(expectedMiss);
        var regex = new Utf8Regex("(a|ab)+$", RegexOptions.CultureInvariant, timeout);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(expectedMiss, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SliceCatastrophicTimeoutRemainingLanes(bool astralTail)
    {
        // R05: catastrophic slice backtracking maps the invocation deadline to
        // the oracle timeout identity on the remaining lanes (light and detailed
        // matches, splits, and FromOffset operations), on ASCII and astral-tailed
        // inputs alike, without constructing fallback engines.
        var timeout = TimeSpan.FromMilliseconds(500);
        var text = new string('a', 20000) + (astralTail ? "\U0001F600!" : "!");
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex("(a|ab)+$", RegexOptions.CultureInvariant, timeout);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var detailed = Assert.Throws<RegexMatchTimeoutException>(() => regex.MatchDetailed(input));
        Assert.Equal(text, detailed.Input);
        var light = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input));
        Assert.Equal(text, light.Input);
        Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            foreach (var item in regex.EnumerateSplits(input, int.MaxValue))
            {
            }
        });
        Assert.Throws<RegexMatchTimeoutException>(() => regex.MatchFromUtf16Offset(input, 0));
        Assert.Throws<RegexMatchTimeoutException>(() => regex.CountFromUtf16Offset(input, 0));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CatastrophicTimeoutAbortsStrictLanesWithIdentity(bool compiled)
    {
        // R05: timeout-path branch strictness: every catastrophic lane aborts with
        // the full oracle timeout identity (decoded input content, pattern, and
        // match timeout) on both lanes, without constructing fallback engines.
        // This closes the strictness tail over the native interpreter lanes the
        // earlier pins left at length-only identity (IsMatch), op-only coverage
        // (Count/Match without Detailed), first-MoveNext-only enumeration (without
        // FromOffset enumeration), and ordinary-only lanes (without compiled).
        var options = (compiled ? RegexOptions.Compiled : RegexOptions.None) | RegexOptions.CultureInvariant;
        var timeout = TimeSpan.FromMilliseconds(1);
        var text = new string('a', 49999) + "!";
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex("(a+)+$", options, timeout);
        Assert.Throws<RegexMatchTimeoutException>(() => oracle.IsMatch(text));
        var regex = new Utf8Regex("(a+)+$", options, timeout);
        var isMatchException = Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input));
        Assert.Equal(text, isMatchException.Input);
        Assert.Equal("(a+)+$", isMatchException.Pattern);
        Assert.Equal(timeout, isMatchException.MatchTimeout);
        var countException = Assert.Throws<RegexMatchTimeoutException>(() => regex.Count(input));
        Assert.Equal(text, countException.Input);
        Assert.Equal("(a+)+$", countException.Pattern);
        Assert.Equal(timeout, countException.MatchTimeout);
        var matchException = Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input));
        Assert.Equal(text, matchException.Input);
        Assert.Equal("(a+)+$", matchException.Pattern);
        Assert.Equal(timeout, matchException.MatchTimeout);
        var detailedException = Assert.Throws<RegexMatchTimeoutException>(() => regex.MatchDetailed(input));
        Assert.Equal(text, detailedException.Input);
        Assert.Equal("(a+)+$", detailedException.Pattern);
        Assert.Equal(timeout, detailedException.MatchTimeout);
        var enumerateException = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateMatches(input);
            _ = enumerator.MoveNext();
        });
        Assert.Equal(text, enumerateException.Input);
        Assert.Equal("(a+)+$", enumerateException.Pattern);
        Assert.Equal(timeout, enumerateException.MatchTimeout);
        var fromOffsetException = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateMatchesFromUtf16Offset(input, 0);
            _ = enumerator.MoveNext();
        });
        Assert.Equal(text, fromOffsetException.Input);
        Assert.Equal("(a+)+$", fromOffsetException.Pattern);
        Assert.Equal(timeout, fromOffsetException.MatchTimeout);
        var splitException = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            var enumerator = regex.EnumerateSplits(input, int.MaxValue);
            _ = enumerator.MoveNext();
        });
        Assert.Equal(text, splitException.Input);
        Assert.Equal("(a+)+$", splitException.Pattern);
        Assert.Equal(timeout, splitException.MatchTimeout);
        var replaceException = Assert.Throws<RegexMatchTimeoutException>(() => regex.Replace(input, "z"));
        Assert.Equal(text, replaceException.Input);
        Assert.Equal("(a+)+$", replaceException.Pattern);
        Assert.Equal(timeout, replaceException.MatchTimeout);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
