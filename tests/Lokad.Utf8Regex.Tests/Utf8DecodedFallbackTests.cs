using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DecodedFallbackTests
{
    [Theory]
    [InlineData("Шерлок Холмс", "xx шерлок холмс yy ШЕРЛОК ХОЛМС", 2)]
    [InlineData("Σ", "σςΣ", 2)]
    [InlineData("K", "kKK", 3)]
    [InlineData("𐐀", "𐐨𐐀", 1)]
    public void UnicodeIgnoreCaseFallbackMatchesDotNet(
        string pattern,
        string subject,
        int expectedCount)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options);
        var bytes = Encoding.UTF8.GetBytes(subject);

        Assert.Equal(expectedCount, regex.Count(bytes));
        Assert.Equal(new Regex(pattern, options).Count(subject), regex.Count(bytes));
        Assert.Equal(new Regex(pattern, options).IsMatch(subject), regex.IsMatch(bytes));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodeFallbackStrictDecodeRejectsMalformedPrefixAndSuffix(bool compiled)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("Шерлок", options);
        var malformedPrefix = new byte[] { 0xC3, 0x28, 0xD0, 0xA8 };
        var malformedSuffix = new byte[] { 0xD0, 0xA8, 0xC3, 0x28 };

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformedPrefix));
        Assert.Throws<ArgumentException>(() => regex.IsMatch(malformedSuffix));
        Assert.Throws<ArgumentException>(() => regex.Count(malformedPrefix));
        Assert.Throws<ArgumentException>(() => regex.Count(malformedSuffix));
    }

    [Fact]
    public void UnicodeLiteralFallbackHasBoundedValueOperationFacts()
    {
        var regex = new Utf8Regex(
            "Шерлок Холмс",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var prepared = regex.Inspection.PreparedRegex;

        Assert.Equal(NativeExecutionKind.FallbackRegex, prepared.ExecutionKind);
        Assert.Equal(Utf8SearchKind.AsciiFoldedByteLiteral, prepared.SearchPlan.Kind);
        Assert.NotEqual(int.MaxValue, prepared.SearchPlan.MaxPossibleLength);
        Assert.Equal(1, prepared.Features.CaptureCount);
        Assert.False(prepared.Features.HasBackreferences);
        Assert.False(prepared.Features.HasLookarounds);
        Assert.False(prepared.Features.HasAtomicGroups);
        Assert.False(prepared.Features.HasConditionals);
        Assert.False(prepared.Features.HasLoops);
    }
}
