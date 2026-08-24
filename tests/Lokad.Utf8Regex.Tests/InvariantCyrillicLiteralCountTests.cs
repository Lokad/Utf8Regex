using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class InvariantCyrillicLiteralCountTests
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    [Theory]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void SherlockLiteralUsesNativeCountStrategy(RegexOptions options)
    {
        var regex = new Utf8Regex("Шерлок Холмс", options);
        var oracle = new Regex("Шерлок Холмс", options, Regex.InfiniteMatchTimeout);
        const string input = "шерлок холмс; ШЕРЛОК ХОЛМС; Шерлок ХоЛмС; Шерлок Уолмс";

        Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.False(regex.Inspection.DebugInvariantCyrillicCountUsesCorrelatedPrefilter);
        Assert.True(regex.Inspection.DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled)]
    public void SherlockLiteralAlternationUsesNativeCountStrategy(RegexOptions options)
    {
        const string pattern = "Шерлок Холмс|Джон Уотсон|Ирен Адлер|инспектор Лестрейд|профессор Мориарти";
        const string input = "шерлок холмс; ДЖОН УОТСОН; ирен адлер; ИнСпЕкТоР ЛеСтРеЙд; ПРОФЕССОР МОРИАРТИ; Шерлок Уолмс";
        var regex = new Utf8Regex(pattern, options);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);

        Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.Equal(
            Sse2.IsSupported && Ssse3.IsSupported,
            regex.Inspection.DebugInvariantCyrillicCountUsesCorrelatedPrefilter);
        Assert.False(regex.Inspection.DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("А|АА", "АА")]
    [InlineData("АА|А", "АА")]
    [InlineData("АБ|Б", "АББ")]
    public void LiteralAlternationPreservesLeftmostBranchOrderAndNonOverlap(string pattern, string input)
    {
        var regex = new Utf8Regex(pattern, Options);
        var oracle = new Regex(pattern, Options, Regex.InfiniteMatchTimeout);

        Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
        Assert.Equal(oracle.Count(input), regex.Count(Encoding.UTF8.GetBytes(input)));
    }

    [Fact]
    public void BasicCyrillicPairsMatchTheInvariantRegexOracle()
    {
        var patterns = Enumerable.Range('\u0410', 0x40)
            .Select(static value => (char)value)
            .Append('\u0401')
            .Append('\u0451')
            .Distinct();

        foreach (var pattern in patterns)
        {
            var regex = new Utf8Regex(pattern.ToString(), Options);
            var oracle = new Regex(pattern.ToString(), Options, Regex.InfiniteMatchTimeout);
            Assert.True(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);

            for (var candidateValue = 0x0400; candidateValue <= 0x052F; candidateValue++)
            {
                var candidate = ((char)candidateValue).ToString();
                Assert.Equal(
                    oracle.Count(candidate),
                    regex.Count(Encoding.UTF8.GetBytes(candidate)));
            }
        }
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("Ш+")]
    [InlineData("Україна")]
    [InlineData("Шерлок|")]
    [InlineData("Шерлок|abc")]
    public void StrategyRejectsPatternsOutsideItsExactSemanticSubset(string pattern)
    {
        var regex = new Utf8Regex(pattern, Options);

        Assert.False(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
    }

    [Fact]
    public void StrategyRejectsFiniteTimeouts()
    {
        var regex = new Utf8Regex("Шерлок Холмс", Options, TimeSpan.FromSeconds(1));

        Assert.False(regex.Inspection.DebugHasInvariantCyrillicLiteralCountStrategy);
    }

    [Fact]
    public void StrategyStillRejectsMalformedUtf8()
    {
        var regex = new Utf8Regex("Шерлок Холмс", Options);
        var malformed = new byte[] { 0xD0, 0x20 };

        Assert.Throws<ArgumentException>(() => regex.Count(malformed));
    }
}
