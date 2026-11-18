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
        foreach (var operation in PythonReFiniteWorkPolicyTests.OperationIds())
            foreach (var bytes in new[] { false, true }) yield return [operation, bytes];
    }
    [Theory]
    [MemberData(nameof(Operations))]
    public void EveryPublicOperationHasFiniteDepthAndExactCutoff(int operation, bool bytes)
    {
        const string pattern = "(?P<x>😀+)(?P<y>😀ab)"; var input = "😀😀😀ab"u8.ToArray();
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
        foreach (var operation in PythonReFiniteWorkPolicyTests.OperationIds())
            Assert.Equal(PythonReFiniteWorkPolicyTests.Run(new(pattern), input, operation),
                PythonReFiniteWorkPolicyTests.Run(Create(pattern, 4096, false), input, operation));
    }
    [Fact]
    public void ExplicitCursorOwnerPreservesSnapshotsOnDepthFailureAndDisposal()
    {
        Utf8PythonRangeMatch cursorRange = default;
        // The overlapping emoji tail requires rewinding the greedy run.
        var regex = Create("(?P<x>ab)|😀+😀ab", 1, false); using var cursor = regex.CreateMatchCursor("ab 😀😀ab"u8.ToArray());
        Assert.True(cursor.MoveNext(out cursorRange)); var retained = cursorRange;
        Denied(Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext(out _)), 1);
        cursor.Dispose(); cursor.Dispose(); Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
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
            MaxDepth = kind == Utf8ResourceLimitKind.ExecutionDepth ? 1 : uint.MaxValue
        };
        var regex = new Utf8PythonRegex("(?P<x>😀+)(?P<y>😀ab)", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            Utf8PythonRangeMatch snapshot1 = default;
            using (var captureCursor1 = regex.CreateMatchCursor(("😀😀😀ab"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
            {
                captureCursor1.MoveNext(out snapshot1);
            }

            _ = snapshot1;
        });
        Assert.Equal(kind, error.ResourceKind);
        Assert.Equal(1UL, error.Limit);
        Assert.True(error.Requested > 1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OriginalInterruptionWinsOnTheNextOwnedCursorMove(bool deadline)
    {
        Utf8PythonRangeMatch cursorRange = default;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8PythonRegex("(?P<x>ab)|😀+ab", PythonReCompileOptions.None,
            deadline ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy { MaxDepth = 1 });
        using var cursor = regex.CreateMatchCursor("ab 😀😀ab"u8.ToArray(), 0, source.Token);
        Assert.True(cursor.MoveNext(out cursorRange));
        var retained = cursorRange;
        if (deadline)
        {
            ticks = 250;
            Assert.Throws<RegexMatchTimeoutException>(() => cursor.MoveNext(out _));
        }
        else
        {
            source.Cancel();
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => cursor.MoveNext(out _)).CancellationToken);
        }
        Assert.Throws<InvalidOperationException>(() => cursor.MoveNext(out _));
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(retained.GetValueMemory().Span));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DepthPolicyStillValidatesTheEntireSubjectAndScalarOffsets(bool bytes)
    {
        var regex = Create("ab", 1, bytes);

        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("😀ab"u8,1,mode: Utf8PythonMatchMode.Search));
        Assert.Equal(4, regex.Match("😀ab"u8,4,mode: Utf8PythonMatchMode.Search).StartOffsetInBytes);
        Assert.Equal(6, regex.Match("😀ab"u8,4,mode: Utf8PythonMatchMode.Search).EndOffsetInBytes);
    }
}
