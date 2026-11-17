using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReScalarCursorOwnershipTests
{

    [Theory]
    [InlineData(false)] [InlineData(true)]
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
                var cursor = new PythonReScalarMatchCursor("\U0001f600"u8, program, 0, Regex.InfiniteMatchTimeout,
                    null, "(.)", new(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token));
                cursor.Dispose();
            });
            Assert.Equal(source.Token, failure.CancellationToken);
            return;
        }
        var mapped = new PythonReScalarMatchCursor("\U0001f600"u8, program, 0, Regex.InfiniteMatchTimeout,
            null, "(.)", new(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, source.Token));
        try
        {
            Assert.True(mapped.MoveNext());
            var positionedBytes = mapped.ReservedBytes;
            Assert.True(positionedBytes > 0); // Owned slots; unnecessary rollback scratch may be absent.
            source.Cancel();
            var observed = false;
            try { mapped.SnapshotGroups(); }
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
