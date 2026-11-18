using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2ValueInvocationAuthorityTests
{
    public static IEnumerable<object[]> Routes()
    {
        foreach (var value in new[] { false, true })
        {
            yield return ["abc", "abc", value];
            yield return [".", "😀", value];
            yield return ["(?<x>a)b", "ab", value];
            yield return [@"(?<x>a)\k<x>", "aa", value];
            yield return [@"\x41+", "AAA", value];
            yield return ["[ab]+", "ab", value];
        }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void OriginalTokenIsObservedOnCapturedDeadlineRead(string pattern, string text, bool value)
    {
        foreach (var atRead in new[] { 2, 5 })
        foreach (var hit in new[] { false, true })
        {
            using var source = new CancellationTokenSource();
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
                default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(20));
            var reads = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
            { if (++reads == atRead) source.Cancel(); return 0; });
            var input = Encoding.UTF8.GetBytes(hit ? text : "zzz");
                Exception? failure = null;
                try { _ = Run(regex, input, value, token: source.Token); }
                catch (Exception error) { failure = error; }
                if (atRead == 2) Assert.True(source.IsCancellationRequested);
                if (source.IsCancellationRequested)
                    Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
                else Assert.Null(failure); // Short operations need not read the clock five times.
            }
    }

    [Theory] [MemberData(nameof(Routes))]
    public void ScalarOffsetsAndRetainedValuesStayValid(string pattern, string text, bool value)
    {
        var regex = Create(pattern, 100000);
        foreach (var lead in new[] { 2046, 2047, 2048 })
        {
            var prefix = new string('x', lead) + "😀";
            var input = Encoding.UTF8.GetBytes(prefix + text);
            Assert.True(Run(regex, input, value, lead + 4));
            var retained = regex.Match(input, startOffsetInBytes: lead + 4);
            Assert.Equal((lead + 4, input.Length), (retained.StartOffsetInBytes, retained.EndOffsetInBytes));
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
            Assert.Throws<ArgumentOutOfRangeException>(() => { _ = Run(regex, input, value, lead + 1); });
            Assert.True(Run(regex, Encoding.UTF8.GetBytes(text), value));
            Assert.Equal(text, System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        }
    }

    [Theory]
    [InlineData("a+b", "aaab")]
    [InlineData(".*abc", "xxabc")]
    [InlineData("^[ab]+$", "ab")]
    [InlineData("(?:foo|bar)", "foo")]
    [InlineData("[ab]+", "ab")]
    public void CancellableInvocationCannotEnterUnlimitedNativeKernel(string pattern, string text)
    {
        using var source = new CancellationTokenSource();
        var regex = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        var compiled = regex.DebugCompiledProgram;
        var invocation = Pcre2SliceDriver.CreateInvocation(compiled.Request, source.Token);
        var subject = Pcre2Runner.PrepareInput(input, 0, ref invocation);
        source.Cancel();
        Exception? failure = null;
        try { _ = Pcre2Runner.TryIsMatch(compiled, ref subject, new(0), Pcre2MatchOptions.None, ref invocation, out _); }
        catch (Exception error) { failure = error; }
        Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }

    private static Utf8Pcre2Regex Create(string pattern, uint work) => new(pattern,
        Pcre2CompileOptions.None, default, new() { MatchLimit = work, DepthLimit = 256, HeapLimitInBytes = 1 << 20, ResultLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);

    private static bool Run(Utf8Pcre2Regex regex, byte[] input, bool value, int start = 0,
        CancellationToken token = default) => value
        ? regex.Match(input, startOffsetInBytes: start, matchOptions: Pcre2MatchOptions.None, cancellationToken: token).Success
        : regex.IsMatch(input, start, Pcre2MatchOptions.None, token);
}
