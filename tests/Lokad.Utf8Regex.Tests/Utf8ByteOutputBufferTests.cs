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
        var deniedControl = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8SliceByteOutputScope.Enter(new(0, 0, control - 1), Regex.InfiniteMatchTimeout, CancellationToken.None));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, deniedControl.ResourceKind);
        Assert.Equal(control - 1, deniedControl.Limit); Assert.Equal(control, deniedControl.Requested);
        Assert.Null(Utf8SliceByteOutputScope.Current);
        var scope = Utf8SliceByteOutputScope.Enter(new(0, 0, control), Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            Assert.Throws<InvalidOperationException>(() => scope.CreateBuffer());
            Assert.Equal(control, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);

        scope = Utf8SliceByteOutputScope.Enter(new(0, 0, control * 2 + 111), Regex.InfiniteMatchTimeout, CancellationToken.None);
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

        foreach (var textResult in new[] { false, true })
        foreach (var exact in new[] { false, true })
        {
            var resultBytes = textResult ? 80UL : 48UL;
            scope = Utf8SliceByteOutputScope.Enter(new(0, 0, control * 2 + 48 + resultBytes - (exact ? 0UL : 1UL)), Regex.InfiniteMatchTimeout, CancellationToken.None);
            try
            {
                var buffer = scope.CreateBuffer();
                buffer.Append("abcdefghijklmnop"u8);
                if (exact)
                {
                    if (textResult) Assert.Equal("abcdefghijklmnop", buffer.ToValidatedString());
                    else Assert.Equal("abcdefghijklmnop"u8.ToArray(), buffer.ToArray());
                }
                else Assert.Throws<InvalidOperationException>(() =>
                {
                    if (textResult) _ = buffer.ToValidatedString();
                    else _ = buffer.ToArray();
                });
                Assert.Equal(control * 2 + 48, scope.Invocation.Ledger.ReservedBytes);
            }
            finally { scope.Release(); }
            Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        }
        Assert.Null(Utf8SliceByteOutputScope.Current);
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
            var expected = Record.Exception(() => Utf8Validation.Validate(bytes));
            var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
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
                    Assert.Equal(Encoding.UTF8.GetString(bytes), buffer.ToValidatedString());
                }
            }
            finally { scope.Release(); }
            Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void Utf16AppendKeepsPairsWithinItsFragmentAndReplacesSeparateLoneFragments()
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            var writer = new Utf8ReplacementWriter(scope.CreateBuffer());
            var text = new string('x', 2047) + "\U0001f600" + new string('y', 2047);
            writer.Append(text.AsSpan());
            writer.Append("\ud83d".AsSpan());
            writer.Append("\ude00".AsSpan());
            Assert.Equal(Encoding.UTF8.GetBytes(text + "\ufffd\ufffd"), writer.GetOwnedOutput(scope).ToArray());
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("append-bytes")]
    [InlineData("append-chars")]
    [InlineData("growth-copy")]
    [InlineData("reverse")]
    [InlineData("validate")]
    [InlineData("materialize")]
    [InlineData("string-size")]
    [InlineData("decode")]
    public void ByteOutputStagesObserveCancellationAndReleaseTheirTemporaryStorage(string operation)
    {
        using var source = new CancellationTokenSource();
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token);
        var buffer = scope.CreateBuffer();
        var bytes = Enumerable.Repeat((byte)'x', 300000).ToArray();
        if (!operation.StartsWith("append", StringComparison.Ordinal)) buffer.Append(bytes);
        if (operation == "reverse") buffer.MarkPiece();
        var held = scope.Invocation.Ledger.ReservedBytes;
        var start = scope.Invocation.Budget.Steps;
        var gate = operation is "materialize" or "string-size" ? start + 74 : operation == "decode" ? start + 148 : start;
        var entered = false;
        scope.Invocation.CheckpointHook = steps => { if (steps > gate) { entered = true; source.Cancel(); } };
        try
        {
            var exception = Assert.Throws<OperationCanceledException>(() =>
            {
                switch (operation)
                {
                    case "append-bytes": buffer.Append(bytes); break;
                    case "append-chars": buffer.Append(new string('x', 300000).AsSpan()); break;
                    case "growth-copy": buffer.Append("Y"u8); break;
                    case "reverse": buffer.ReversePieces(); break;
                    case "validate": _ = buffer.GetValidatedBytes(); break;
                    case "materialize": _ = buffer.ToArray(); break;
                    case "string-size": _ = buffer.ToValidatedString(); break;
                    case "decode": _ = buffer.ToValidatedString(); break;
                }
            });
            Assert.True(entered);
            Assert.Equal(source.Token, exception.CancellationToken);
            if (operation != "append-bytes") Assert.Equal(held, scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Fact]
    public void ReturnedWriterIsTheOnlyLiveLeaseAfterItsCallbackBoundary()
    {
        var scope = Utf8SliceByteOutputScope.Enter(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
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
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void WorkLimitsInterruptOutputBeforeAdmissionOrDuringCopyAndReleaseItsOwner(ulong steps)
    {
        var scope = Utf8SliceByteOutputScope.Enter(new(steps, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        try
        {
            var buffer = scope.CreateBuffer();
            Assert.Throws<InvalidOperationException>(() => buffer.Append(new byte[20000]));
            Assert.True(scope.Invocation.Budget.Steps > steps);
            Assert.Equal(Utf8SliceByteOutputScope.ControlBytes * 2 + (steps == 2 ? 0UL : 20032UL), scope.Invocation.Ledger.ReservedBytes);
        }
        finally { scope.Release(); }
        Assert.Equal(0UL, scope.Invocation.Ledger.ReservedBytes);
        Assert.Null(Utf8SliceByteOutputScope.Current);
    }

}
