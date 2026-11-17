using System.Reflection;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFixedPolicyPreparationTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void FiniteCorePreparationKeepsSearchWithoutUnmeteredRuntime(int limit)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = limit is 0 or 4 ? 1_000_000ul : null,
            MaxDepth = limit is 1 or 4 ? 2048u : null,
            MaxWorkingBytes = limit is 2 or 4 ? 1_048_576ul : null,
            MaxResultBytes = limit is 3 or 4 ? 1_048_576ul : null
        };
        var regex = new Utf8PythonRegex("abc|xyz", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
        var core = Assert.IsType<Utf8Regex>(typeof(Utf8PythonRegex)
            .GetField("_utf8Regex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
        Assert.Equal(policy, core.ResourcePolicy);
        Assert.Null(core.Inspection.CompiledRuntime);
        Assert.True(core.CanFindNativeLiteralForResult);
        var input = "é😀 abc xyz abc"u8.ToArray();
        Assert.True(regex.IsMatch(input));
        Assert.Equal(3, regex.Count(input));
        PythonReMatchAssert.Equal("abc", regex.Search(input));
        Assert.Equal(["abc", "xyz", "abc"], PythonReFindAllAssert.Decode(regex.FindAllToUtf8(input)).ScalarValues);
        Assert.Equal("é😀 ! ! !", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "!")));
        Assert.Equal("abc", regex.SearchDetailedData(input).Value.ValueText);
        Assert.True(regex.FullMatch("xyz"u8).Success);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, canceled.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarOneShotValuesAndTextSupportFiniteWork(bool finite)
    {
        var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 } : default;
        var prefix = new Utf8PythonRegex("prefix[0-9]+", PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
        var full = new Utf8PythonRegex("pre.*post", PythonReCompileOptions.DotAll, Timeout.InfiniteTimeSpan, policy);
        Assert.True(prefix.Match("prefix123"u8).Success);
        Assert.True(full.FullMatch("pre😀post"u8).Success);
        PythonReMatchAssert.Equal("pre😀post", full.FullMatch("pre😀post"u8));
    }
}
