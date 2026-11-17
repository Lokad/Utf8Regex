using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PackedPreparationRetirementTests
{
    [Fact]
    public void LongFoldedSearchFitsItsUsefulPreparationBudgetAndBorrowsCanonicalBytes()
    {
        foreach (var needle in new[] { "abcdefghijklmnop"u8.ToArray(), "ABCDEFGHIJKLMNOP"u8.ToArray() })
        {
            using var owner = new Utf8ConstructionScope(0, 1104, 640, 1);
            var search = new PreparedSubstringSearch(needle, true);
            Assert.Equal(1, search.IndexOf("!AbCdEfGhIjKlMnOp?"u8));
            Assert.Equal(1, search.Count("!AbCdEfGhIjKlMnOp?"u8));
            Assert.InRange(owner.Budget.WorkSteps, 1ul, 640ul);
            var expected = Utf8ConstructionBudget.ArrayBytes<int>(256);
            if (needle[0] == (byte)'A') expected += Utf8ConstructionBudget.ArrayBytes<byte>(16);
            Assert.Equal(expected, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void OwnedSearchKeepsAllAsciiBytesAndLengthTransitions()
    {
        foreach (var length in new[] { 1, 2, 8, 9, 10, 11, 16, 32 })
        foreach (var value in Enumerable.Range(0, 128))
        foreach (var ignore in new[] { false, true })
        {
            var needle = Enumerable.Repeat((byte)value, length).ToArray();
            var search = new PreparedSubstringSearch(needle, ignore);
            var input = Enumerable.Repeat((byte)value, length * 3).ToArray();
            Assert.Equal(0, search.IndexOf(input)); Assert.Equal(2 * length, search.LastIndexOf(input));
            Assert.Equal(3, search.Count(input));
        }
    }

    [Fact]
    public void RepeatedLongNeedleKeepsScalarCoordinatesValidationAndCancellation()
    {
        var pattern = string.Concat(Enumerable.Repeat("Az!9B_cD", 4));
        var input = Encoding.UTF8.GetBytes("😀" + pattern.ToLowerInvariant() + "中" + pattern.ToUpperInvariant() + "é");
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft,
            RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
            var match = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal(options.HasFlag(RegexOptions.RightToLeft) ? 39 : 4, match.Value.StartOffsetInBytes);
            Assert.Equal(32, (match.Value.EndOffsetInBytes - match.Value.StartOffsetInBytes));             Assert.Equal(1, new Utf8Regex(".", options).Count("😀"u8));
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token));
        }
    }
}
