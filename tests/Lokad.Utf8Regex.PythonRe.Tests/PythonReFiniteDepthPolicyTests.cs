using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFiniteDepthPolicyTests
{
    private static Utf8PythonRegex Create(string pattern, uint cap, bool bytes)
    {
        var policy = new Utf8ResourcePolicy { MaxDepth = cap };
        return bytes ? new(Encoding.UTF8.GetBytes(pattern), PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy)
            : new(pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
    }
    private static void Denied(Utf8ResourceLimitExceededException error, uint cap)
    {
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, error.ResourceKind);
        Assert.Equal((ulong)cap, error.Limit); Assert.Equal((ulong)cap + 1, error.Requested);
    }
    public static IEnumerable<object[]> Operations()
    {
        for (var operation = 0; operation < 25; operation++) foreach (var bytes in new[] { false, true }) yield return [operation, bytes];
    }
    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryPublicOperationHasFiniteDepthAndExactCutoff(int operation, bool bytes)
    {
        const string pattern = "(?P<x>😀+)(?P<y>ab)"; var input = "😀😀ab"u8.ToArray();
        var expected = PythonReFiniteWorkPolicyTests.Run(new(pattern), input, operation);
        Assert.Equal(expected, PythonReFiniteWorkPolicyTests.Run(Create(pattern, uint.MaxValue, bytes), input, operation));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() => PythonReFiniteWorkPolicyTests.Run(Create(pattern, 1, bytes), input, operation)), 1);
        uint low = 1, high = 256;
        while (high - low > 1)
        {
            var middle = (low + high) / 2;
            try { Assert.Equal(expected, PythonReFiniteWorkPolicyTests.Run(Create(pattern, middle, bytes), input, operation)); high = middle; }
            catch (Utf8ResourceLimitExceededException error) { Denied(error, middle); low = middle; }
        }
        Assert.Equal(expected, PythonReFiniteWorkPolicyTests.Run(Create(pattern, high, bytes), input, operation));
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() => PythonReFiniteWorkPolicyTests.Run(Create(pattern, high - 1, bytes), input, operation)), high - 1);
    }
    [Theory]
    [InlineData("cat", "cat")]
    [InlineData("(?<=😀)(?P<n>é+)\\1", "😀éé")]
    [InlineData(".*?", "😀a")]
    [InlineData("(a|aa)+", "aaaa")]
    public void ScalarLookbehindCapturesReferencesAndEmptyProgressRemainExact(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        for (var operation = 0; operation < 25; operation++)
            Assert.Equal(PythonReFiniteWorkPolicyTests.Run(new(pattern), input, operation),
                PythonReFiniteWorkPolicyTests.Run(Create(pattern, 4096, false), input, operation));
    }
    [Fact]
    public void ExplicitCursorOwnerPreservesSnapshotsOnDepthFailureAndDisposal()
    {
        var regex = Create("(?P<x>ab)|😀+ab", 1, false); using var cursor = regex.CreateMatchCursor("ab 😀😀ab"u8.ToArray());
        Assert.True(cursor.MoveNext()); var retained = cursor.Current;
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext()), 1);
        cursor.Dispose(); cursor.Dispose(); Assert.Equal("ab", retained.Value.ValueText);
        Assert.True(regex.IsMatch("ab"u8));
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ExecutionResultBytes)]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth)]
    public void CombinedPoliciesKeepEachPublicFailureKind(Utf8ResourceLimitKind kind)
    {
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 1 : ulong.MaxValue,
            MaxResultBytes = kind == Utf8ResourceLimitKind.ExecutionResultBytes ? 1 : ulong.MaxValue,
            MaxDepth = kind == Utf8ResourceLimitKind.ExecutionDepth ? 1 : uint.MaxValue,
        };
        var regex = new Utf8PythonRegex("(?P<x>😀+)(?P<y>ab)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.SearchDetailedData("😀😀ab"u8));
        Assert.Equal(kind, error.ResourceKind);
        Assert.Equal(1UL, error.Limit);
        Assert.True(error.Requested > 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionWinsOnTheNextOwnedCursorMove(bool deadline)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("(?P<x>ab)|😀+ab", PythonReCompileOptions.None,
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxDepth = 1 });
        using var cursor = regex.CreateMatchCursor("ab 😀😀ab"u8.ToArray(), 0, source.Token);
        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        if (deadline)
        {
            ticks = 250;
            Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext());
        }
        else
        {
            source.Cancel();
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext()).CancellationToken);
        }
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext());
        Assert.Equal("ab", retained.Value.ValueText);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepthPolicyStillValidatesTheEntireSubjectAndScalarOffsets(bool bytes)
    {
        var regex = Create("ab", 1, bytes);
        Assert.Throws<ArgumentException>(() => regex.IsMatch([97, 98, 255]));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Search("😀ab"u8, 1));
        Assert.Equal(4, regex.Search("😀ab"u8, 4).StartOffsetInBytes);
        Assert.Equal(6, regex.Search("😀ab"u8, 4).EndOffsetInBytes);
    }
}
