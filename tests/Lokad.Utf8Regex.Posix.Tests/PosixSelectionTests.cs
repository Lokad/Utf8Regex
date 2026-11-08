using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixSelectionTests
{
    private static JsonDocument Fixtures() => JsonDocument.Parse(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "Corpus", "selection.json")));

    [Fact]
    public void IndependentWinnerAndCaptureFixturesUsePosixSelection()
    {
        using var fixtures = Fixtures();
        foreach (var row in fixtures.RootElement.GetProperty("cases").EnumerateArray())
        foreach (var existenceOnly in new[] { false, true })
        {
            var pattern = row.GetProperty("pattern").GetString()!;
            var text = row.GetProperty("input").GetString()!;
            var input = Encoding.UTF8.GetBytes(text);
            var options = row.TryGetProperty("options", out var flag) ? Enum.Parse<PosixCompileOptions>(flag.GetString()!) : default;
            var start = row.TryGetProperty("startOffsetInBytes", out var offset) ? offset.GetInt32() : 0;
            var expected = row.GetProperty("groups");
            var program = PosixProgram.Compile(pattern, options);
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
            Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
            var workspace = new PosixWorkspace(program, ref invocation);
            try
            {
                var found = workspace.Find(input, start, existenceOnly, ref invocation, out var actualStart, out var actualEnd);
                Assert.True(found == (expected.ValueKind != JsonValueKind.Null), row.GetProperty("id").GetString());
                if (!found || existenceOnly) continue;
                Assert.Equal(expected[0][0].GetInt32(), actualStart);
                Assert.Equal(expected[0][1].GetInt32(), actualEnd - actualStart);
                Assert.Equal(expected.GetArrayLength(), program.CaptureTags.Length);
                for (var slot = 0; slot < expected.GetArrayLength(); slot++)
                {
                    var capture = expected[slot];
                    var participates = workspace.GetCapture(slot, ref invocation, out var from, out var to);
                    Assert.True((capture.ValueKind != JsonValueKind.Null) == participates,
                        $"{row.GetProperty("id").GetString()}, group {slot}: unexpected participation {participates}");
                    if (!participates) continue;
                    Assert.Equal(capture[0].GetInt32(), from);
                    Assert.Equal(capture[1].GetInt32(), to - from);
                    Assert.Equal(capture[2].GetInt32(), Encoding.UTF8.GetCharCount(input.AsSpan(0, from)));
                    Assert.Equal(capture[3].GetInt32(), Encoding.UTF8.GetCharCount(input.AsSpan(from, to - from)));
                }
            }
            finally { workspace.Release(ref invocation); }
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Fact]
    public void IndependentIterationFixturesRetainOneInvocationAndScalarProgress()
    {
        using var fixtures = Fixtures();
        foreach (var row in fixtures.RootElement.GetProperty("iterationCases").EnumerateArray())
        {
            var input = Encoding.UTF8.GetBytes(row.GetProperty("input").GetString()!);
            var program = PosixProgram.Compile(row.GetProperty("pattern").GetString()!, default);
            var position = row.TryGetProperty("startOffsetInBytes", out var offset) ? offset.GetInt32() : 0;
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default);
            var workspace = new PosixWorkspace(program, ref invocation);
            var expected = row.GetProperty("matches"); var count = 0;
            try
            {
                while (workspace.Find(input, position, false, ref invocation, out var start, out var end))
                {
                    Assert.True(count < expected.GetArrayLength(), row.GetProperty("id").GetString());
                    var value = expected[count++];
                    Assert.Equal(value[0].GetInt32(), start); Assert.Equal(value[1].GetInt32(), end - start);
                    Assert.Equal(value[2].GetInt32(), Encoding.UTF8.GetCharCount(input.AsSpan(0, start)));
                    Assert.Equal(value[3].GetInt32(), Encoding.UTF8.GetCharCount(input.AsSpan(start, end - start)));
                    if (start == end && end == input.Length) break;
                    position = start == end ? Utf8SliceRunner.AdvancePastScalar(input, end, false) : end;
                }
                Assert.Equal(expected.GetArrayLength(), count);
            }
            finally { workspace.Release(ref invocation); }
            Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkSteps)]
    [InlineData(Utf8ResourceLimitKind.ExecutionDepth)]
    [InlineData(Utf8ResourceLimitKind.ExecutionWorkingBytes)]
    public void ExecutionRefusalReleasesCoveredStorage(Utf8ResourceLimitKind kind)
    {
        var program = PosixProgram.Compile("(a|aa)*", default);
        var limits = new Utf8ExecutionLimits(kind == Utf8ResourceLimitKind.ExecutionWorkSteps ? 30UL : 1_000_000,
            kind == Utf8ResourceLimitKind.ExecutionDepth ? 1U : 100,
            kind == Utf8ResourceLimitKind.ExecutionWorkingBytes ? 1UL : 1 << 20, 1);
        var account = new Account(1 << 20);
        var invocation = new Utf8SliceInvocation(limits, Timeout.InfiniteTimeSpan, default, account);
        PosixWorkspace? workspace = null;
        try
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            {
                workspace = new PosixWorkspace(program, ref invocation);
                workspace.Find("aaaa"u8, 0, false, ref invocation, out _, out _);
            });
            Assert.Equal(kind, error.ResourceKind);
        }
        finally { workspace?.Release(ref invocation); }
        Assert.Equal(0UL, account.Used); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void SharedRefusalAndCancellationAtAcquisitionRefundExactly()
    {
        var program = PosixProgram.Compile("(a|aa)*", default);
        var refused = new Account(0);
        var denied = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, default, refused);
        Assert.Throws<Utf8MemoryAdmissionException>(() => new PosixWorkspace(program, ref denied));
        Assert.Equal(0UL, refused.Used);
        using var canceled = new CancellationTokenSource();
        var account = new Account(1 << 20) { AfterGrant = canceled.Cancel };
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, canceled.Token, account);
        var error = Assert.Throws<OperationCanceledException>(() => new PosixWorkspace(program, ref invocation));
        Assert.Equal(canceled.Token, error.CancellationToken);
        Assert.Equal(0UL, account.Used); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void CancellationDuringAmbiguousSelectionUsesTheOriginalTokenAndRefunds()
    {
        var program = PosixProgram.Compile("(a|aa)*", default);
        using var canceled = new CancellationTokenSource();
        var account = new Account(1 << 20);
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Timeout.InfiniteTimeSpan, canceled.Token, account);
        invocation.CheckpointHook = _ => canceled.Cancel();
        var workspace = new PosixWorkspace(program, ref invocation);
        try
        {
            var error = Assert.Throws<OperationCanceledException>(() =>
                workspace.Find("aaaaaaaaaaaa"u8, 0, false, ref invocation, out _, out _));
            Assert.Equal(canceled.Token, error.CancellationToken);
        }
        finally { workspace.Release(ref invocation); }
        Assert.Equal(0UL, account.Used); Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    private sealed class Account(ulong limit) : IUtf8MemoryAllowance
    {
        internal ulong Used;
        internal Action? AfterGrant;
        public bool TryReserve(ulong bytes)
        {
            if (bytes > limit - Used) return false;
            Used += bytes; AfterGrant?.Invoke(); return true;
        }
        public void Release(ulong bytes)
        { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}
