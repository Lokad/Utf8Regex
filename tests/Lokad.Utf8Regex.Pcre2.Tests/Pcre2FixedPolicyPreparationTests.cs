namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2FixedPolicyPreparationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void EachFiniteControlReachesRetainedOriginalAndDerivedCore(int limit)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = limit is 0 or 4 ? 1_000_000ul : null,
            MaxDepth = limit is 1 or 4 ? 2048u : null,
            MaxWorkingBytes = limit is 2 or 4 ? 1_048_576ul : null,
            MaxResultBytes = limit is 3 or 4 ? 1_048_576ul : null,
        };
        foreach (var pattern in new[] { "abc|xyz", "(?<left>a)(b)" })
        {
            var regex = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default, policy, Timeout.InfiniteTimeSpan);
            var core = Assert.IsType<Utf8Regex>(regex.DebugCompiledProgram.PrimaryUtf8);
            Assert.Equal(policy, core.ResourcePolicy);
            Assert.Null(core.Inspection.CompiledRuntime);
            Assert.True(core.CanFindNativeLiteralForResult);
            var input = pattern == "abc|xyz" ? "abc xyz"u8.ToArray() : "ab ab"u8.ToArray();
            Assert.True(regex.IsMatch(input));
            Assert.Equal(2, regex.Count(input));
            Assert.True(regex.MatchDetailed(input).Success);
            var matches = regex.EnumerateMatches(input);
            var count = 0;
            while (matches.MoveNext()) count++;
            Assert.Equal(2, count);
            Assert.Equal("! !", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "!")));
            if (pattern.Contains("?<left>"))
                Assert.Equal("a", regex.MatchDetailed(input).GetGroup(1).GetValueString());
            using var canceled = new CancellationTokenSource();
            canceled.Cancel();
            Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Count(input, 0, Pcre2MatchOptions.None, canceled.Token)).CancellationToken);
        }
    }

    [Theory]
    [InlineData("abc|xyz", "abc xyz")]
    [InlineData("(?<left>a)(b)", "ab ab")]
    [InlineData(@"([0-9]+)-\1", "12-12 7-7")]
    [InlineData(@"\b(?:class|record|struct)\s+[A-Z][A-Za-z0-9_]+", "class Bravo struct Alpha")]
    public void PolicyPropagationDoesNotChangeOperationSelection(string pattern, string text)
    {
        var unlimited = new Utf8Pcre2Regex(pattern);
        var finite = Utf8Pcre2Regex.Create(pattern, Pcre2CompileOptions.None, default,
            new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 }, Timeout.InfiniteTimeSpan);
        var before = unlimited.DebugCompiledProgram.Operations;
        var after = finite.DebugCompiledProgram.Operations;
        Assert.Equal(before.IsMatch.GetType(), after.IsMatch.GetType());
        Assert.Equal(before.Count.GetType(), after.Count.GetType());
        Assert.Equal(before.Match.GetType(), after.Match.GetType());
        Assert.Equal(before.Enumerate.GetType(), after.Enumerate.GetType());
        Assert.Equal(before.Replace.GetType(), after.Replace.GetType());
        var input = System.Text.Encoding.UTF8.GetBytes(text);
        Assert.Equal(unlimited.Count(input), finite.Count(input));
    }
}
