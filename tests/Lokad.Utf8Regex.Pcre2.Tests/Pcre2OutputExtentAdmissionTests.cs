using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2OutputExtentAdmissionTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_invocation")]
    private static extern ref Utf8SliceInvocation Invocation(ref Pcre2ReplacementTemplateWriter writer);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void FinalTemplateCopyPreservesProfileCancellationAndDeadlineIdentity(bool expire)
    {
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var writer = new Pcre2ReplacementTemplateWriter(new byte[8192], [],
            new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 1UL << 20),
                expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, token.Token));
        writer.AppendGap(0, 8192);
        ref var invocation = ref Invocation(ref writer);
        while ((invocation.Budget.Steps & 63) != 60) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else token.Cancel(); };
        Exception? failure = null;
        try { _ = writer.Complete(); }
        catch (Exception error) { failure = error; }
        try
        {
            Assert.True(entered);
            if (expire) Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(8192, writer.Length);
            Assert.Equal(8224UL, writer.ReservedWorkingBytes);
        }
        finally { writer.Release(); }
        Assert.Equal(0UL, writer.ReservedWorkingBytes);
    }

    [Theory]
    [InlineData(0, 48UL)] [InlineData(1, 272UL)] [InlineData(17, 2160UL)]
    public void LiteralDriverAdmitsFinalOutputAlongsideLiveRanges(int count, ulong peak)
    {
        var input = count == 0 ? "z"u8.ToArray() : Enumerable.Repeat((byte)'a', count).ToArray();
        var replacement = new string('b', count == 17 ? 100 : 1);
        foreach (var allow in new[] { false, true })
        {
            var regex = new Utf8Pcre2Regex("a", Pcre2CompileOptions.None, default,
                new() { HeapLimitInBytes = peak - (allow ? 0UL : 1UL) }, Regex.InfiniteMatchTimeout);
            Pcre2PreparationMemoryPolicyTests.Warm(regex, replacement);
            if (allow)
            {
                var result = regex.Replace(input, replacement);
                if (count == 0) Assert.Equal(input, result);
                else { Assert.Equal(count * replacement.Length, result.Length); Assert.All(result, static value => Assert.Equal((byte)'b', value)); }
            }
            else
            {
                var failure = Assert.Throws<Pcre2MatchException>(() => regex.Replace(input, replacement));
                Assert.Equal(Pcre2ErrorKind.HeapLimit, failure.ErrorKind);
            }
        }
    }
}
