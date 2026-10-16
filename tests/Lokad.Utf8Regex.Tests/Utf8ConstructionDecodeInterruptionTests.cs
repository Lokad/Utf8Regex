using System.Text;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionDecodeInterruptionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EarlierInterruptionWinsOverMalformedTailWithoutAcquiringText(bool timeout)
    {
        var input = Enumerable.Repeat((byte)'a', 10000).Append((byte)0xff).ToArray();
        using var owner = Utf8ConstructionScope.Enter();
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        owner.Budget.CancellationToken = cancellation.Token;
        var checkpoints = 0;
        owner.Budget.CheckpointHook = _ =>
        {
            if (++checkpoints != 3) return;
            if (timeout) ticks = 25; else cancellation.Cancel();
        };
        var failure = Record.Exception(() => owner.Budget.DecodeUtf8(input, new UTF8Encoding(false, true)));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(0UL, owner.Budget.PeakBytes);
        Assert.Equal((ulong)input.Length, owner.Budget.WorkSteps);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(false, true)]
    [InlineData(true, false)] [InlineData(true, true)]
    public void BothPassesObserveOriginalAuthorityAndReleaseOnlyTheirLoan(bool decoding, bool timeout)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 4095) + "😀" + new string('中', 3000));
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var held = owner.Budget.ReservedBytes;
        var priorWork = owner.Budget.WorkSteps;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        var checkpoints = 0;
        owner.Budget.CheckpointHook = _ =>
        {
            // Four count blocks, then the original decode precharge, followed
            // by the first checkpoint inside the admitted string's fill.
            if (++checkpoints != (decoding ? 6 : 3)) return;
            if (timeout) ticks = 25; else cancellation.Cancel();
        };

        var failure = Record.Exception(() => owner.Budget.DecodeUtf8(input, new UTF8Encoding(false, true)));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(held, owner.Budget.ReservedBytes);
        Assert.Equal(priorWork + (decoding ? 2UL * (ulong)input.Length : (ulong)input.Length), owner.Budget.WorkSteps);
        if (decoding) Assert.True(owner.Budget.PeakBytes > held);
        else Assert.Equal(held, owner.Budget.PeakBytes);

        owner.Budget.CheckpointHook = null;
        owner.Budget.CancellationToken = default;
        owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
        Assert.Equal(Encoding.UTF8.GetString(input), owner.Budget.DecodeUtf8(input, Encoding.UTF8));
        owner.Budget.ReleaseBytes(Utf8ConstructionBudget.StringBytes(Encoding.UTF8.GetCharCount(input)));
        Assert.Equal(held, owner.Budget.ReservedBytes);
        GC.KeepAlive(parent);
    }

    [Theory]
    [InlineData(4093)] [InlineData(4094)] [InlineData(4095)] [InlineData(4096)]
    public void ScalarBoundariesKeepTextAndSuccessfulCharges(int prefix)
    {
        var text = new string('a', prefix) + "😀é中\0" + new string('b', 4100);
        var input = Encoding.UTF8.GetBytes(text);
        using var owner = Utf8ConstructionScope.Enter();
        Assert.Equal(text, owner.Budget.DecodeUtf8(input, new UTF8Encoding(false, true)));
        Assert.Equal(2UL * (ulong)input.Length, owner.Budget.WorkSteps);
        Assert.Equal(Utf8ConstructionBudget.StringBytes(text.Length), owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(4093)] [InlineData(4094)] [InlineData(4095)] [InlineData(4096)]
    [InlineData(8191)]
    public void MalformedBoundariesKeepStrictOffsetsAndRepairSemantics(int prefix)
    {
        byte[][] malformed = [[0xc2], [0xed, 0xa0, 0x80], [0xf0, 0x80, 0x80, 0x80],
            [0xf4, 0x90, 0x80, 0x80], [0x80, 0x80, 0x80, 0x80, 0x80], [0xe2, 0x82, 0x41]];
        var strict = new UTF8Encoding(false, true);
        foreach (var tail in malformed)
        {
            var input = Enumerable.Repeat((byte)'a', prefix).Concat(tail).Concat("中"u8.ToArray()).ToArray();
            var expected = Assert.Throws<DecoderFallbackException>(() => strict.GetString(input));
            using var owner = Utf8ConstructionScope.Enter();
            var failure = Assert.Throws<DecoderFallbackException>(() => owner.Budget.DecodeUtf8(input, strict));
            Assert.Equal(expected.Index, failure.Index);
            Assert.Equal(expected.BytesUnknown, failure.BytesUnknown);
            Assert.Equal(0UL, owner.Budget.ReservedBytes);
            Assert.Equal(Encoding.UTF8.GetString(input), owner.Budget.DecodeUtf8(input, Encoding.UTF8));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(17)] [InlineData(4096)]
    public void ShortInputsKeepTwoCheckpoints(int length)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var checkpoints = 0;
        owner.Budget.CheckpointHook = _ => checkpoints++;
        _ = owner.Budget.DecodeUtf8(new byte[length], Encoding.UTF8);
        Assert.Equal(2, checkpoints);
    }
}
