using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

// Timeout enforcement across result-producing operations: catastrophic input
// aborts Search, substitution, and split with RegexMatchTimeoutException, and
// the timed-out instance stays usable afterwards.
[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReTimeoutTests
{
    private static byte[] CreateCatastrophicInput()
    {
        // Keep a possible final scalar so the engine must verify the run.
        return Encoding.UTF8.GetBytes(new string('a', 100000) + "!a");
    }

    [Fact]
    public void CatastrophicSearchHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Match(CreateCatastrophicInput(), mode: Utf8PythonMatchMode.Search));
    }

    [Fact]
    public void CatastrophicSubstitutionHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => PythonReSubstitutionAssert.Decode(regex.Subn(CreateCatastrophicInput(), "-"u8)));
    }

    [Fact]
    public void CatastrophicSplitHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => PythonReSplitAssert.Decode(CreateCatastrophicInput(), regex.SplitRanges(CreateCatastrophicInput(), 0, 0, default)));
    }

    [Fact]
    public void TimedOutInstanceRemainsUsable()
    {
        // Recovery includes the first substitution call. Give its cold path
        // scheduling headroom while still forcing a real catastrophic timeout.
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(100)
        });
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Match(CreateCatastrophicInput(), mode: Utf8PythonMatchMode.Search));

        var input = Encoding.UTF8.GetBytes("aaabaaa");
        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.Search).Success);
        Assert.Equal(1, regex.Count(input));
        Assert.Equal("aaab-", PythonReSubstitutionAssert.Decode(regex.Subn(input, "-"u8)).ResultText);
    }
    [Fact]
    public void FiniteTimeoutSupportsFastPaths()
    {
        // This is a finite-policy success smoke, including cold core discovery
        // and projection. Their deadline now includes startup; a 1 ms ceiling
        // can correctly expire there. Explicit expiry tests keep tight budgets.
        var regex = new Utf8PythonRegex("abc", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromSeconds(1)
        });
        var input = Encoding.UTF8.GetBytes("xxabcxx");

        Assert.True(regex.Match(input, mode: Utf8PythonMatchMode.Search).Success);
        Assert.Equal(1, regex.Count(input));
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(1, snapshots1.Count);
        Assert.Equal("xxZxx", PythonReSubstitutionAssert.Decode(regex.Subn(input, "Z"u8)).ResultText);
        Assert.Equal(2, PythonReSplitAssert.Decode(input, regex.SplitRanges(input, 0, 0, default)).Length);
    }
    [Fact]
    public void CatastrophicFindIterDetailedHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() =>
        {
            var collectedRanges1 = new List<Utf8PythonRangeMatch>();
            using (var rangeCursor1 = regex.CreateMatchCursor(CreateCatastrophicInput(), 0, default))
            {
                while (rangeCursor1.MoveNext(out var rangeMatch1))
                    collectedRanges1.Add(rangeMatch1);
            }
        });
    }

    [Fact]
    public void CatastrophicSubnHonorsTimeout()
    {
        var regex = new Utf8PythonRegex("(a+)+$", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(1)
        });
        Assert.Throws<System.Text.RegularExpressions.RegexMatchTimeoutException>(() => regex.Subn(CreateCatastrophicInput(), "-"u8));
    }
}
