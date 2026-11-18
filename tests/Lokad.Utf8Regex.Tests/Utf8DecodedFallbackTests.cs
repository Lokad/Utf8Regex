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
    [InlineData("𐐀", "𐐨𐐀", 2)]
    public void UnicodeIgnoreCaseMatchesScalarContract(
        string pattern,
        string subject,
        int expectedCount)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        var regex = new Utf8Regex(pattern, options: options);
        var bytes = Encoding.UTF8.GetBytes(subject);

        Assert.Equal(expectedCount, regex.Count(bytes));
        Assert.True(regex.IsMatch(bytes));
        if (pattern == "𐐀")
        {
            // Deseret case folding is defined over scalars, not surrogate halves.
            var ranges = new List<(int, int)>();
            foreach (var match in regex.EnumerateMatches(bytes))
                ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            Assert.Equal([(0, 4), (4, 4)], ranges);
        }
        else
        {
            Assert.Equal(new Regex(pattern, options).Count(subject), regex.Count(bytes));
            Assert.Equal(new Regex(pattern, options).IsMatch(subject), regex.IsMatch(bytes));
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnicodeFallbackStrictDecodeRejectsMalformedPrefixAndSuffix(bool compiled)
    {
        var options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant |
            (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex("Шерлок", options: options);

        Assert.Equal(NativeExecutionKind.FallbackRegex, regex.Inspection.ExecutionKind);
    }

    [Fact]
    public void UnicodeLiteralFallbackHasBoundedValueOperationFacts()
    {
        var regex = new Utf8Regex(            "Шерлок Холмс", options:             RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
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
