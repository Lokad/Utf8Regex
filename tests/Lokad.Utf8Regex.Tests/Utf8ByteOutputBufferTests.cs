using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ByteOutputBufferTests
{
    [Fact]
    public void OutputAdmissionCoversControlsGrowthOverlapAndFinalResults()
    {
        const ulong control = Utf8SliceByteOutputScope.ControlBytes;
        var deniedControl = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceByteOutputScope.Enter(new Utf8SliceInvocation(new(0, control - 1), Regex.InfiniteMatchTimeout, CancellationToken.None)));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, deniedControl.ResourceKind);
        Assert.Equal(control - 1, deniedControl.Limit); Assert.Equal(control, deniedControl.Requested);
        var scope = Utf8SliceByteOutputScope.Enter(new Utf8SliceInvocation(new(0, control), Regex.InfiniteMatchTimeout, CancellationToken.None));
        try
        {
            Assert.Throws<InvalidOperationException>(() => scope.CreateBuffer());
            Assert.Equal(control, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);

        scope = Utf8SliceByteOutputScope.Enter(new Utf8SliceInvocation(new(0, control * 2 + 111), Regex.InfiniteMatchTimeout, CancellationToken.None));
        try
        {
            var buffer = scope.CreateBuffer();
            buffer.Append("abcdefghijklmnop"u8);
            Assert.Throws<InvalidOperationException>(() => buffer.Append("q"u8));
            Assert.Equal(control * 2 + 48, scope.Invocation.Ledger.ReservedBytes);
            Assert.Throws<InvalidOperationException>(() => buffer.ToArray());
            var retainedBytes = (byte[])typeof(Utf8SliceByteOutputBuffer)
                .GetField("_buffer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(buffer)!;
            Assert.Equal("abcdefghijklmnop"u8.ToArray(), retainedBytes);
            Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, scope.Invocation.Budget.LimitBreach);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);

        foreach (var exact in new[] { false, true })
        {
            var resultBytes = 48UL;
            scope = Utf8SliceByteOutputScope.Enter(new Utf8SliceInvocation(new(0, control * 2 + 48 + resultBytes - (exact ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, CancellationToken.None));
            try
            {
                var buffer = scope.CreateBuffer();
                buffer.Append("abcdefghijklmnop"u8);
                if (exact)
                {
                    Assert.Equal("abcdefghijklmnop"u8.ToArray(), buffer.ToArray());
                }
                else Assert.Throws<InvalidOperationException>(() =>
                {
                    _ = buffer.ToArray();
                });
                Assert.Equal(control * 2 + 48, scope.Invocation.Ledger.ReservedBytes);
            }
            finally { scope.Release(); }
            Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void CompleteOutputValidationPreservesAbsoluteInvalidOffsetsAcrossBlockEdges()
    {
        byte[][] suffixes = [[0xc0, 0x80], [0xed, 0xa0, 0x80], [0xf0, 0x80, 0x80, 0x80],
            [0xf4, 0x90, 0x80, 0x80], [0xf0, 0x9f, 0x98, 0x80, 0x80, 0x80, 0x80, 0x80],
            [0x80], [0xe2, 0x28, 0xa1], [0xf0, 0x9f, 0x98, 0x80], [0xc2, 0x80], [0xe2, 0x80, 0xa8]];
        for (var prefix = 4088; prefix <= 4100; prefix++)
        foreach (var suffix in suffixes)
        {
            var bytes = Enumerable.Repeat((byte)'x', prefix).Concat(suffix).ToArray();
            var expected = Record.Exception(() => Utf8Validation.ThrowIfInvalidOnly(bytes));
            var scope = Utf8SliceByteOutputScope.Enter(new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None));
            try
            {
                var buffer = scope.CreateBuffer();
                buffer.Append(bytes);
                if (expected is ArgumentException invalid)
                {
                    var actual = Assert.Throws<ArgumentException>(() => buffer.ToArray());
                    Assert.Equal(invalid.Message, actual.Message);
                    Assert.Equal(invalid.ParamName, actual.ParamName);
                }
                else
                {
                    Assert.Null(expected);
                    Assert.Equal(bytes, buffer.ToArray());
                }
            }
            finally { scope.Release(); }
            Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData("append-bytes")]
    [InlineData("growth-copy")]
    [InlineData("reverse")]
    [InlineData("validate")]
    [InlineData("materialize")]
    public void ByteOutputStagesObserveCancellationAndReleaseTheirTemporaryStorage(string operation)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        using var source = new CancellationTokenSource();
        var scope = Utf8SliceByteOutputScope.Enter(new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token));
        var buffer = scope.CreateBuffer();
        var bytes = Enumerable.Repeat((byte)'x', 300000).ToArray();
        if (!operation.StartsWith("append", StringComparison.Ordinal)) buffer.Append(bytes);
        if (operation == "reverse") buffer.MarkPiece();
        var held = scope.Invocation.Ledger.ReservedBytes;
        var start = scope.Invocation.Budget.DiagnosticCheckpoints;
        var gate = operation == "materialize" ? start + 74 : start;
        var entered = false;
        scope.Invocation.CheckpointHook = steps => { if (steps > gate) { entered = true; source.Cancel(); } };
        try
        {
            var exception = Assert.Throws<OperationCanceledException>(() =>
            {
                switch (operation)
                {
                    case "append-bytes": buffer.Append(bytes); break;
                    case "growth-copy": buffer.Append("Y"u8); break;
                    case "reverse": buffer.ReversePieces(); break;
                    case "validate": _ = buffer.GetValidatedBytes(); break;
                    case "materialize": _ = buffer.ToArray(); break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, exception.CancellationToken);
            if (operation != "append-bytes") Assert.Equal(held, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ReturnedWriterIsTheOnlyLiveLeaseAfterItsCallbackBoundary()
    {
        var scope = Utf8SliceByteOutputScope.Enter(new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None));
        var discarded = scope.CreateBuffer();
        var returned = scope.CreateBuffer();
        try
        {
            discarded.Append("old"u8);
            returned.Append("new"u8);
            scope.Retain(returned);
            Assert.Throws<InvalidOperationException>(() => discarded.Append("no longer live"u8));
            Assert.Equal("new"u8.ToArray(), returned.ToArray());
        }
        finally { scope.Release(); }
        scope.Release();
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => returned.ToArray());
    }

}
