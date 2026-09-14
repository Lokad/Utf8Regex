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
}
