using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2GlobalInvocationAuthorityTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var enumerate in new[] { false, true })
        {
            yield return ["abc", "abc", enumerate];
            yield return [".", "😀", enumerate];
            yield return ["(?<x>a)b", "ab", enumerate];
            yield return [@"(?<x>a)\k<x>", "aa", enumerate];
            yield return [@"\x41+", "AAA", enumerate];
            yield return ["[ab]+", "ab", enumerate];
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void GlobalExecutionKeepsOriginalToken(string pattern, string text, bool enumerate)
    {
        foreach (var atRead in new[] { 2, 5 })
        foreach (var hit in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
            {
                MatchTimeout = TimeSpan.FromMilliseconds(20)
            });
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            { if (++reads == atRead) source.Cancel(); return 0; });
            var input = Encoding.UTF8.GetBytes(hit ? text : "zzz");
            var failure = Assert.Throws<OperationCanceledException>(() =>
            { _ = Run(regex, input, enumerate, source.Token); });
            Assert.Equal(source.Token, failure.CancellationToken);
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void SearchStopsAtWorkDenial(string pattern, string text, bool enumerate)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 16385) + text);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() =>
        { _ = Run(Create(pattern, 3), input, enumerate); }).ErrorKind);
    }

    [Theory]
    [InlineData("", new[] { 0, 4 })]
    [InlineData("|.", new[] { 0, 0, 4 })]
    [InlineData("(?<x>.)", new[] { 0 })]
    public void EmptyRetryScalarAdvanceAndRetainedRangesStayValid(string pattern, int[] starts)
    {
        var regex = Create(pattern, 100000);
        var input = "😀"u8.ToArray();
        Assert.Equal(starts.Length, regex.Count(input));
        var cursor = regex.EnumerateMatches(input);
        var positions = new List<int>();
        while (cursor.MoveNext()) positions.Add(cursor.Current.StartOffsetInBytes);
        Assert.Equal(starts, positions);
        var retainedCursor = regex.EnumerateMatches(input);
        Assert.True(retainedCursor.MoveNext());
        var retained = retainedCursor.Current;
        while (retainedCursor.MoveNext()) { }
        Assert.Equal(starts[0], retained.StartOffsetInBytes);
        Assert.Equal(input.AsSpan(retained.StartOffsetInBytes, retained.EndOffsetInBytes - retained.StartOffsetInBytes).ToArray(), retained.GetValueBytes().ToArray());
    }

    [Theory]
    [InlineData("abc", "abc aa ab")]
    [InlineData("(?<x>a)b", "abc aa ab")]
    [InlineData(@"(?<x>a)\k<x>", "abc aa ab")]
    [InlineData(@"\x41+", "AAA AAA")]
    public void CursorCopiesCannotForkSpentWorkOrLoseOriginalCancellation(string pattern, string subject)
    {
        using var source = new CancellationTokenSource();
        var regex = Create(pattern, 100000);
        var input = Encoding.UTF8.GetBytes(subject);
        var original = regex.EnumerateMatches(input, 0, Pcre2MatchOptions.None, source.Token);
        Assert.True(original.MoveNext());
        var alias = original;
        var retained = original.Current;
        source.Cancel();
        Exception? failure = null;
        try { _ = alias.MoveNext(); } catch (Exception error) { failure = error; }
        Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(input.AsSpan(retained.StartOffsetInBytes, retained.EndOffsetInBytes - retained.StartOffsetInBytes).ToArray(), retained.GetValueBytes().ToArray());
    }

    private static Utf8Pcre2Regex Create(string pattern, uint work) => new(pattern, options:         Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
    {
        ResourcePolicy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = (work) == 0 ? null : (work),
            MaxDepth = 256,
            MaxWorkingBytes = 1 << 20,
            MaxResultBytes = 1
        },
        MatchTimeout = Timeout.InfiniteTimeSpan
    });

    private static int Run(Utf8Pcre2Regex regex, byte[] input, bool enumerate, CancellationToken token = default,
        Pcre2MatchOptions flags = Pcre2MatchOptions.None)
    {
        if (!enumerate) return regex.Count(input, 0, flags, token);
        var cursor = regex.EnumerateMatches(input, 0, flags, token);
        var count = 0;
        while (cursor.MoveNext()) count++;
        return count;
    }
}
