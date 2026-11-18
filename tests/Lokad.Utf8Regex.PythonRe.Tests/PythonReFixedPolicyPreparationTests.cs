using System.Reflection;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFixedPolicyPreparationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public void FiniteCorePreparationKeepsSearchWithoutUnmeteredRuntime(int limit)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = limit is 0 or 4 ? 1_000_000ul : null,
            MaxDepth = limit is 1 or 4 ? 2048u : null,
            MaxWorkingBytes = limit is 2 or 4 ? 1_048_576ul : null,
            MaxResultBytes = limit is 3 or 4 ? 1_048_576ul : null
        };
        var regex = new Utf8PythonRegex("abc|xyz", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var core = Assert.IsType<Utf8Regex>(typeof(Utf8PythonRegex)
            .GetField("_utf8Regex", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex));
        Assert.Equal(policy, core.ResourcePolicy);
        Assert.Null(core.Inspection.CompiledRuntime);
        Assert.True(core.CanFindNativeLiteralForResult);
        var input = "é😀 abc xyz abc"u8.ToArray();
        Assert.True(regex.IsMatch(input));
        Assert.Equal(3, regex.Count(input));
        PythonReMatchAssert.Equal("abc", regex.Match(input, mode: Utf8PythonMatchMode.Search));
        ReadOnlyMemory<byte> snapshots1Input = input;
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(["abc", "xyz", "abc"], snapshots1.Select(captureSnapshot1 => System.Text.Encoding.UTF8.GetString(captureSnapshot1.GetGroupMemory(captureSnapshot1.Groups.Length == 2 ? 1 : 0).Span)).ToArray());
        Assert.Equal("é😀 ! ! !", System.Text.Encoding.UTF8.GetString(regex.Replace(input, "!")));
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(snapshot1.GetValueMemory().Span));
        Assert.True(regex.Match("xyz"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Count(input, 0, canceled.Token)).CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScalarOneShotValuesAndTextSupportFiniteWork(bool finite)
    {
        var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 1_000_000 } : default;
        var prefix = new Utf8PythonRegex("prefix[0-9]+", options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        var full = new Utf8PythonRegex("pre.*post", options: PythonReCompileOptions.DotAll, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        Assert.True(prefix.Match("prefix123"u8).Success);
        Assert.True(full.Match("pre😀post"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        PythonReMatchAssert.Equal("pre😀post", full.Match("pre😀post"u8, mode: Utf8PythonMatchMode.FullMatch));
    }
}
