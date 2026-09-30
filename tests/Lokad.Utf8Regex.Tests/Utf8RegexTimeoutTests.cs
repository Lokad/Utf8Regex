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
    public void FiniteTimeoutNeverBreaksFastPaths()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
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
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
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
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
