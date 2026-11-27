using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReResourcePolicyTests
{
    [Fact]
    public void ZeroDepthPolicyIsInvalidBeforePatternValidation()
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 0 };
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex("[invalid", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Utf8PythonRegex(new byte[] { 255 }, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        }));
    }

    [Fact]
    public void UnlimitedPolicyConstructsBothFlavorsAndRoundTrips()
    {
        var fromText = new Utf8PythonRegex("cat", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited
        });
        var fromBytes = new Utf8PythonRegex("cat"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited
        });
        Assert.Equal(Utf8ResourcePolicy.Unlimited, fromText.ResourcePolicy);
        Assert.Equal(Utf8ResourcePolicy.Unlimited, fromBytes.ResourcePolicy);
        Assert.Equal(1, fromText.Count(Encoding.UTF8.GetBytes("cat")));
        Assert.Equal(1, fromBytes.Count(Encoding.UTF8.GetBytes("cat")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FiniteDepthAndMemoryBudgetsConstructAndExecute(int kind)
    {
        var policy = kind switch
        {
            0 => new Utf8ResourcePolicy { MaxDepth = 100 },
            1 => new Utf8ResourcePolicy { MaxWorkingBytes = 100 },
            _ => new Utf8ResourcePolicy { MaxDepth = 100, MaxResultBytes = 100 },
        };
        var text = new Utf8PythonRegex("cat", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = policy
        });
        var bytes = new Utf8PythonRegex("cat"u8, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = policy
        });
        Assert.Equal(policy, text.ResourcePolicy);
        Assert.True(text.IsMatch("cat"u8));
        Assert.True(bytes.IsMatch("cat"u8));
    }

    [Fact]
    public void ZeroBudgetIsRejectedBeforePatternWork()
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 0u };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8PythonRegex("cat", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = policy
        }));
    }
    [Fact]
    public void ValidFinitePolicyPreservesPatternSyntaxFailure()
    {
        var policy = new Utf8ResourcePolicy { MaxWorkingBytes = 1024ul };
        Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex("[invalid", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = policy
        }));
    }

    [Fact]
    public void ZeroPolicyIsRejectedBeforePatternCompilation()
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = 0u };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8PythonRegex("[invalid", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = policy
        }));
    }

    [Fact]
    public void ZeroPolicyPrecedesBytePatternDecoding()
    {
        var zero = new Utf8ResourcePolicy { MaxDepth = 0u };
        var invalidBytes = new byte[] { 0x63, 0x61, 0x74, 0xFF };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => new Utf8PythonRegex(invalidBytes, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = zero
        }));
        Assert.Throws<System.ArgumentException>(() => new Utf8PythonRegex(invalidBytes, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited
        }));
    }

    [Theory]
    [InlineData("cat", "xxcatxx")]
    [InlineData(@"(\w+)@(\w+)", "mail me a@b.cd ok")]
    public void DefaultMatchesExplicitUnlimited(string pattern, string inputText)
    {
        // R06 compatibility evidence: default construction behaves exactly like
        // an explicit Unlimited policy across value and global operations.
        var input = Encoding.UTF8.GetBytes(inputText);
        var baseline = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None);
        var explicitUnlimited = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Utf8PythonRegex.DefaultMatchTimeout,
            ResourcePolicy = Utf8ResourcePolicy.Unlimited
        });

        Assert.Equal(baseline.Count(input), explicitUnlimited.Count(input));
        var expectedSearch = baseline.Match(input, mode: Utf8PythonMatchMode.Search);
        var actualSearch = explicitUnlimited.Match(input, mode: Utf8PythonMatchMode.Search);
        Assert.Equal(expectedSearch.Success, actualSearch.Success);
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = baseline.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        ReadOnlyMemory<byte> snapshots2Input = input;
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = explicitUnlimited.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        Assert.Equal(snapshots1.Count, snapshots2.Count);
        Assert.Equal(PythonReSplitAssert.Decode(input, baseline.SplitRanges(input, 0, 0, default)).Length, PythonReSplitAssert.Decode(input, explicitUnlimited.SplitRanges(input, 0, 0, default)).Length);
    }
}
