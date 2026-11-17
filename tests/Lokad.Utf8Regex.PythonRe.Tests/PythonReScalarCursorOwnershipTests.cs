using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarCursorOwnershipTests
{

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreparationAndMapUseTheOriginalOperationToken(bool snapshot)
    {
        var core = new global::Lokad.Utf8Regex.Utf8Regex("(.)");
        var program = core.Inspection.AvailableOwnedSliceProgram;
        Assert.NotNull(program);
        using var source = new CancellationTokenSource();
        if (!snapshot) source.Cancel();
        if (!snapshot)
        {
            var failure = Assert.Throws<OperationCanceledException>(() =>
            {
                var cursor = new PythonReScalarMatchState("\U0001f600"u8.ToArray(), program, 0, Regex.InfiniteMatchTimeout,
                    "(.)", new(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token), null);
                cursor.Dispose();
            });
            Assert.Equal(source.Token, failure.CancellationToken);
            return;
        }
        var mapped = new PythonReScalarMatchState("\U0001f600"u8.ToArray(), program, 0, Regex.InfiniteMatchTimeout,
            "(.)", new(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token), null);
        try
        {
            Assert.True(mapped.MoveNext("\U0001f600"u8));
            var positionedBytes = mapped.ReservedBytes;
            Assert.True(positionedBytes > 0); // Owned slots; unnecessary rollback scratch may be absent.
            source.Cancel();
            var observed = false;
            try { mapped.SnapshotRanges("\U0001f600"u8.ToArray(), []); }
            catch (OperationCanceledException failure)
            {
                observed = true;
                Assert.Equal(source.Token, failure.CancellationToken);
            }
            Assert.True(observed);
            // Cancellation before projection acquires nothing else. Scratch
            // remains owned until the explicit close below.
            Assert.Equal(positionedBytes, mapped.ReservedBytes);
        }
        finally { mapped.Dispose(); }
        Assert.Equal(0UL, mapped.ReservedBytes);
    }
}
