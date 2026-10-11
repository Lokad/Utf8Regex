using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PackedPreparationRetirementTests
{
    [Fact]
    public void ProductionSearchRetainsOnlyConsumedStateAndNoPackedExperiment()
    {
        var type = typeof(PreparedSubstringSearch);
        var fields = type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.Equal(7, fields.Length);
        Assert.DoesNotContain(fields, f => f.Name is "_ignoreCasePackedPair" or "<IgnoreCaseTier>k__BackingField");
        Assert.Null(type.Assembly.GetType("Lokad.Utf8Regex.Internal.Search.PreparedIgnoreCasePackedPair"));
        Assert.DoesNotContain("PackedPair", Enum.GetNames<PreparedIgnoreCaseSearchTier>());
        var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        Assert.DoesNotContain(methods, m => m.Name is "DetermineIgnoreCaseTier" or "ShouldUsePackedPairTier"
            or "TryCreateIgnoreCasePackedPair" or "IndexOfIgnoreCasePackedPair" or "IndexOfIgnoreCasePackedPairWithMetrics"
            or "MatchesFoldedTailSkippingIndexes");
    }

    [Fact]
    public void LongFoldedSearchFitsItsUsefulPreparationBudgetAndBorrowsCanonicalBytes()
    {
        foreach (var needle in new[] { "abcdefghijklmnop"u8.ToArray(), "ABCDEFGHIJKLMNOP"u8.ToArray() })
        {
            using var owner = new Utf8ConstructionScope(0, 1104, 640, 1);
            var search = new PreparedSubstringSearch(needle, true);
            Assert.Equal(PreparedIgnoreCaseSearchTier.VectorAnchored, search.IgnoreCaseTier);
            Assert.Equal(1, search.IndexOf("!AbCdEfGhIjKlMnOp?"u8));
            Assert.Equal(1, search.CountWithMetrics("!AbCdEfGhIjKlMnOp?"u8, out _, out _));
            Assert.InRange(owner.Budget.WorkSteps, 1ul, 640ul);
            var expected = Utf8ConstructionBudget.ArrayBytes<int>(256);
            if (needle[0] == (byte)'A') expected += Utf8ConstructionBudget.ArrayBytes<byte>(16);
            Assert.Equal(expected, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void DerivedTierKeepsAllAsciiBytesAndLengthTransitionsOnTheOwnedKernels()
    {
        foreach (var length in new[] { 1, 2, 8, 9, 10, 11, 16, 32 })
        foreach (var value in Enumerable.Range(0, 128))
        foreach (var ignore in new[] { false, true })
        {
            var needle = Enumerable.Repeat((byte)value, length).ToArray();
            var search = new PreparedSubstringSearch(needle, ignore);
            Assert.Equal(!ignore ? PreparedIgnoreCaseSearchTier.None :
                length <= 8 ? PreparedIgnoreCaseSearchTier.ShortLiteral : PreparedIgnoreCaseSearchTier.VectorAnchored,
                search.IgnoreCaseTier);
            var input = Enumerable.Repeat((byte)value, length * 3).ToArray();
            Assert.Equal(0, search.IndexOf(input)); Assert.Equal(2 * length, search.LastIndexOf(input));
            Assert.Equal(3, search.CountWithMetrics(input, out _, out _));
            Assert.Equal(3, search.CountIgnoreCaseWithTier(input, search.IgnoreCaseTier, out _, out _));
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
            var match = regex.MatchDetailed(input);
            Assert.Equal(options.HasFlag(RegexOptions.RightToLeft) ? 39 : 4, match.IndexInBytes);
            Assert.Equal(32, match.LengthInBytes); Assert.Equal(32, match.LengthInUtf16);
            Assert.Equal(1, new Utf8Regex(".", options).Count("😀"u8));
            Assert.Throws<ArgumentException>(() => regex.Count(input.Concat(new byte[] { 0xff }).ToArray()));
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancelled.Token));
        }
    }
}
