using System.Buffers;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

public sealed class ReplacementInfrastructureTests
{
    [Fact]
    public void TryReplaceIsTransactionalForSmallExactAndOversizedDestinations()
    {
        var regex = new Utf8Regex("foo");
        var tooSmall = Enumerable.Repeat((byte)0xCC, 7).ToArray();

        var status = regex.TryReplace("foofoo"u8, "long", tooSmall, out var bytesWritten);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(0, bytesWritten);
        Assert.All(tooSmall, static value => Assert.Equal(0xCC, value));

        Span<byte> exact = stackalloc byte[8];
        status = regex.TryReplace("foofoo"u8, "long", exact, out bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(8, bytesWritten);
        Assert.True(exact.SequenceEqual("longlong"u8));

        Span<byte> oversized = stackalloc byte[12];
        oversized.Fill(0xCC);
        status = regex.TryReplace("foofoo"u8, "x", oversized, out bytesWritten);
        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(2, bytesWritten);
        Assert.True(oversized[..2].SequenceEqual("xx"u8));
        Assert.All(oversized[2..].ToArray(), static value => Assert.Equal(0xCC, value));
    }

    [Fact]
    public void DestinationReplacementHasBoundedWarmedAllocationIndependentOfMatchCount()
    {
        var regex = new Utf8Regex("foo");
        var input = System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("foo-", 128)));
        var destination = new byte[input.Length];
        _ = regex.TryReplace(input, "bar", destination, out _);

        var allSucceeded = true;
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 100; i++)
        {
            var status = regex.TryReplace(input, "bar", destination, out var written);
            allSucceeded &= status == OperationStatus.Done && written == input.Length;
        }

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allSucceeded);
        Assert.InRange(allocated, 0, 128 * 100);
    }

    [Fact]
    public void VariableRangeSizingDiscoversEachMatchOnlyOnce()
    {
        var calls = 0;
        var result = Utf8LiteralReplaceEngine.Replace(
            "a-bb-ccc"u8,
            "x"u8.ToArray(),
            (ReadOnlySpan<byte> input, int start, out int matchIndex, out int matchLength) =>
            {
                calls++;
                while (start < input.Length && input[start] == (byte)'-')
                {
                    start++;
                }

                if (start >= input.Length)
                {
                    matchIndex = 0;
                    matchLength = 0;
                    return false;
                }

                var separator = input[start..].IndexOf((byte)'-');
                matchIndex = start;
                matchLength = separator < 0 ? input.Length - start : separator;
                return true;
            },
            Utf8ExecutionDeadline.Infinite);

        Assert.True(result.AsSpan().SequenceEqual("x-x-x"u8));
        Assert.Equal(4, calls);
    }

    [Fact]
    public void RangeLedgerReturnsItsBufferWhenSizingThrowsAfterGrowth()
    {
        var calls = 0;
        Assert.Throws<InvalidOperationException>(() => Utf8LiteralReplaceEngine.Replace(
            new byte[64],
            "x"u8.ToArray(),
            (ReadOnlySpan<byte> _, int start, out int matchIndex, out int matchLength) =>
            {
                calls++;
                if (calls == 24)
                {
                    throw new InvalidOperationException("injected");
                }

                matchIndex = start;
                matchLength = 1;
                return start < 64;
            },
            Utf8ExecutionDeadline.Infinite));

        Assert.Equal(24, calls);
        var followup = new Utf8Regex("a").Replace("aaaa"u8, "b");
        Assert.True(followup.AsSpan().SequenceEqual("bbbb"u8));
    }

    [Fact]
    public void CheckedOutputLengthRejectsOverflow()
    {
        var length = new Utf8ReplacementOutputLength(int.MaxValue);
        Assert.Throws<OverflowException>(() => length.ReplaceRange(0, 1));
    }

    [Fact]
    public void ReplacementPlanCacheIsBoundedUnderCallerControlledChurn()
    {
        var regex = new Utf8Regex("foo");
        for (var i = 0; i < 100; i++)
        {
            _ = regex.Replace("foo"u8, $"replacement-{i}");
        }

        Assert.InRange(regex.Inspection.DebugReplacementCacheEntryCount, 1, Utf8ReplacementPlanCache.Capacity);
    }

    [Fact]
    public void ReplacementImplementationHasOneOutputAndLedgerOwner()
    {
        var root = FindRepositoryDirectory();
        var executionDirectory = Path.Combine(root, "src", "Lokad.Utf8Regex", "Internal", "Execution");
        Assert.Empty(Directory.GetFiles(executionDirectory, "*Replacement*.cs"));

        var replacementDirectory = Path.Combine(root, "src", "Lokad.Utf8Regex", "Internal", "Replacement");
        var source = string.Join('\n', Directory.GetFiles(replacementDirectory, "*.cs").Select(File.ReadAllText));
        Assert.DoesNotContain("TryCopyToDestination", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CopySlice", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ArrayPool<int>", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "Lokad.Utf8Regex")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
