using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ReplacementLiteralInterruptionTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    public void EachLiteralPassObservesOriginalInterruptionAndRestoresParent(int pass, bool timeout)
    {
        var text = new string('a', 2047) + "😀" + new string('é', 5000);
        var count = Encoding.UTF8.GetByteCount(text);
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        var held = owner.Budget.ReservedBytes;
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        ulong phase = 0, peakBefore = 0;
        var armed = false;
        var visits = 0;
        owner.Budget.CheckpointHook = steps =>
        {
            // Interrupt within a prepaid pass, not at the next allocation/stage.
            if (!armed || steps != phase || ++visits != 2) return;
            if (timeout) ticks = 25; else cancellation.Cancel();
        };
        var failure = Record.Exception(() =>
        {
            using var child = Utf8ConstructionScope.Enter();
            using var storage = Utf8ConstructionSession.Create(owner.Budget);
            var spent = owner.Budget.WorkSteps;
            phase = spent + (pass == 0 ? (ulong)text.Length :
                (pass == 1 ? 2UL : 3UL) * (ulong)text.Length + 2UL * (ulong)count);
            peakBefore = owner.Budget.PeakBytes;
            armed = true;
            _ = Utf8ReplacementInstruction.Literal(storage, text);
            storage.Complete(); child.Complete();
        });
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(held, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        if (pass == 0) Assert.Equal(peakBefore, owner.Budget.PeakBytes);
        else Assert.True(owner.Budget.PeakBytes > peakBefore);

        owner.Budget.CheckpointHook = null;
        owner.Budget.CancellationToken = default;
        owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
        var spentAfterFailure = owner.Budget.WorkSteps;
        using (var child = Utf8ConstructionScope.Enter())
        {
            using var storage = Utf8ConstructionSession.Create(owner.Budget);
            var result = Utf8ReplacementInstruction.Literal(storage, text);
            Assert.Equal(Encoding.UTF8.GetBytes(text), result.LiteralUtf8);
        }
        Assert.True(owner.Budget.WorkSteps > spentAfterFailure);
        Assert.Equal(held, owner.Budget.ReservedBytes);
        GC.KeepAlive(parent);
    }

    [Theory]
    [InlineData(2046, false)] [InlineData(2047, false)] [InlineData(2048, false)]
    [InlineData(4095, false)] [InlineData(4096, false)]
    [InlineData(2046, true)] [InlineData(2047, true)] [InlineData(2048, true)]
    [InlineData(4095, true)] [InlineData(4096, true)]
    public void BoundaryPairsAndRepairsKeepBytesAndAdmitOnlyTheirPayload(int prefix, bool invalid)
    {
        var tail = invalid ? "😀\ud83dX\ude00\0" : "😀é\0";
        var text = new string('a', prefix) + tail + new string('é', 4200);
        var bytes = Encoding.UTF8.GetBytes(text);
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        var spent = owner.Budget.WorkSteps;
        var held = owner.Budget.ReservedBytes;
        var result = Utf8ReplacementInstruction.Literal(storage, text);
        Assert.Equal(bytes, result.LiteralUtf8);
        Assert.Equal(2UL * (ulong)text.Length + 2UL * (ulong)bytes.Length,
            owner.Budget.WorkSteps - spent);
        Assert.Equal(Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length),
            owner.Budget.ReservedBytes - held);
    }

    [Fact]
    public void CaptureInsertionCannotJoinBrokenSurrogatesAcrossLiteralFragments()
    {
        var prefix = new string('x', 2047);
        var suffix = new string('é', 3000);
        var template = prefix + "\ud83d$1\ude00" + suffix;
        var expected = prefix + "\ufffda\ufffd" + suffix;
        var regex = new Utf8Regex("(a)");
        using var token = new CancellationTokenSource();
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace("a"u8, template, token.Token));
        Assert.Equal(expected, regex.ReplaceToString("a"u8, template, token.Token));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public void MalformedEdgesKeepWholeFragmentRepair(int shape)
    {
        // Attribute strings pass through UTF-8 metadata. Construct malformed
        // UTF-16 in the body so test discovery cannot repair or merge the cases.
        var edge = shape switch
        {
            0 => "\ud83d\ud83d\ude00",
            1 => "\ude00\ud83d\ude00",
            2 => "\ud83d\ude00\ude00",
            3 => "\ud83dX",
            _ => "X\ude00",
        };
        Assert.NotEqual(edge, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(edge)));
        foreach (var prefix in new[] { 2046, 2047, 2048 })
        {
            var text = new string('a', prefix) + edge + new string('b', 2050);
            using var owner = Utf8ConstructionScope.Enter();
            using var storage = Utf8ConstructionSession.Create(owner.Budget);
            var result = Utf8ReplacementInstruction.Literal(storage, text);
            var expected = Encoding.UTF8.GetBytes(text);
            Assert.Equal(expected, result.LiteralUtf8);
        }
    }
}
