using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NoMatchReplacementTests
{
    private static ulong Controls => 2 * Utf8SliceByteOutputScope.ControlBytes;
    private static Utf8Regex Create(RegexOptions options, ulong result, string pattern = "(cat)") =>
        new(pattern, options | RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000, MaxDepth = 256,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = result,
            });

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.Compiled | RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void MissNeedsOnlyFinalPayloadAndExistingControls(RegexOptions options)
    {
        foreach (var text in new[] { "", "dog é😀", string.Concat(Enumerable.Repeat("dog fox bird ", 512)) })
        {
            var input = Encoding.UTF8.GetBytes(text);
            var strings = Create(options, Controls + Utf8ConstructionBudget.StringBytes(text.Length));
            Assert.Equal(text, strings.ReplaceToString(input, "tiger"));
            var bytes = Create(options, Controls + Utf8ConstructionBudget.ArrayBytes<byte>(input.Length));
            var first = bytes.Replace(input, "tiger"u8);
            var retained = bytes.Replace(input, "tiger"u8);
            Assert.Equal(input, first);
            Assert.Equal(input, retained);
            if (input.Length != 0)
            {
                Assert.NotSame(input, first);
                Assert.NotSame(first, retained);
                first[0] ^= 1;
                Assert.Equal(Encoding.UTF8.GetBytes(text), input);
                Assert.Equal(input, retained);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OneByteBelowFinalExtentStillDenies(bool bytes)
    {
        var input = "😀é"u8.ToArray();
        var extent = bytes ? Utf8ConstructionBudget.ArrayBytes<byte>(input.Length)
            : Utf8ConstructionBudget.StringBytes("😀é".Length);
        var regex = Create(RegexOptions.None, Controls + extent - 1);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (bytes) _ = regex.Replace(input, "tiger"u8);
            else _ = regex.ReplaceToString(input, "tiger");
        });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, failure.ResourceKind);
    }

    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0, 3, false)]
    [InlineData(3, 0, false)]
    [InlineData(0, 0, true)]
    [InlineData(0, 3, true)]
    [InlineData(3, 0, true)]
    public void BorrowedDestinationKeepsOverlapAndInsufficientCapacityRules(int inputStart, int destinationStart, bool tooSmall)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.RightToLeft })
        {
            var input = Encoding.UTF8.GetBytes("dog é😀");
            var storage = Enumerable.Repeat((byte)0xa5, input.Length + 8).ToArray();
            input.CopyTo(storage, inputStart);
            var original = storage.ToArray();
            var regex = Create(options, Controls);
            var status = regex.TryReplace(storage.AsSpan(inputStart, input.Length), "tiger",
                storage.AsSpan(destinationStart, input.Length - (tooSmall ? 1 : 0)), out var written);
            Assert.Equal(tooSmall ? OperationStatus.DestinationTooSmall : OperationStatus.Done, status);
            Assert.Equal(tooSmall ? 0 : input.Length, written);
            if (tooSmall) Assert.Equal(original, storage);
            else Assert.Equal(input, storage.AsSpan(destinationStart, written).ToArray());
        }
    }

    [Theory]
    [InlineData("x*", "😀", "q", "q😀q")]
    [InlineData(".", "😀", "", "")]
    public void EmptyMatchesAndEmptyExpansionStillUseMatchedSemantics(string pattern, string text, string template, string expected)
    {
        var regex = Create(RegexOptions.None, 1 << 20, pattern);
        Assert.Equal(expected, regex.ReplaceToString(Encoding.UTF8.GetBytes(text), template));
    }

    [Fact]
    public void MissStillValidatesSubjectTemplateAndOriginalToken()
    {
        var regex = Create(RegexOptions.None, 1 << 20);
        Assert.Throws<ArgumentException>(() => regex.ReplaceToString([0xff], "tiger"));
        Assert.Throws<ArgumentException>(() => regex.Replace("dog"u8, [0xff]));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = Assert.Throws<OperationCanceledException>(() =>
            regex.ReplaceToString("dog"u8, "tiger", cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
    }
}
