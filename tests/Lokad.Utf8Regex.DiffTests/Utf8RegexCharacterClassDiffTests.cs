using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexCharacterClassDiffTests
{
    [Theory]
    [InlineData(@"\p{Lu}", "A", RegexOptions.CultureInvariant)]
    [InlineData(@"\P{Nd}", "A", RegexOptions.CultureInvariant)]
    [InlineData(@"\w", "_", RegexOptions.CultureInvariant)]
    [InlineData(@"\d", "١", RegexOptions.CultureInvariant)]
    [InlineData(@"[^\d]", "A", RegexOptions.CultureInvariant)]
    [InlineData(@".", "\n", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForMirroredCharacterClassBatch(string pattern, string input, RegexOptions options)
    {
        // Mirrored and adapted from (research checkout external/runtime @ 9ffface2f3f):
        // external/runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/RegexCharacterSetTests.cs
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData(@"\w+", "abc١٢٣", RegexOptions.CultureInvariant)]
    [InlineData(@"\w+", "אבג123", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+", "١٢٣", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+", "१२३", RegexOptions.CultureInvariant)]
    public void MatchMatchesRuntimeForMirroredUnicodeCharacterBatch(string pattern, string input, RegexOptions options)
    {
        // Mirrored and adapted from (research checkout external/runtime @ 9ffface2f3f):
        // external/runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.UnicodeChar.Tests.cs
        RegexParityContext.Create(pattern, input, options).AssertMatchParity();
    }

    [Theory]
    [InlineData(@"[\da]+", "a1b2", RegexOptions.CultureInvariant)]
    [InlineData(@"[^\da]+", "a-b", RegexOptions.CultureInvariant)]
    [InlineData(@"[\w-]+", "a-b_c", RegexOptions.CultureInvariant)]
    [InlineData(@"[^\w\s]+", "a-b", RegexOptions.CultureInvariant)]
    [InlineData(@"[\d-e]+", "d-e5", RegexOptions.CultureInvariant)]
    [InlineData(@"[\Da]+", "ax5", RegexOptions.CultureInvariant)]
    [InlineData(@"[\d\s]+", "a 1", RegexOptions.CultureInvariant)]
    public void IsMatchMatchesRuntimeForOwnedHybridClassBatch(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData(@"[\da]+", "a1b2", RegexOptions.CultureInvariant)]
    [InlineData(@"[^\da]+", "a-b", RegexOptions.CultureInvariant)]
    [InlineData(@"[\w-]+", "a-b_c", RegexOptions.CultureInvariant)]
    [InlineData(@"[^\w\s]+", "a-b", RegexOptions.CultureInvariant)]
    [InlineData(@"[\d-e]+", "d-e5", RegexOptions.CultureInvariant)]
    [InlineData(@"[\Da]+", "ax5", RegexOptions.CultureInvariant)]
    [InlineData(@"[\d\s]+", "a 1", RegexOptions.CultureInvariant)]
    public void MatchMatchesRuntimeForOwnedHybridClassBatch(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertMatchParity();
    }

    [Theory]
    [InlineData(@"[\da]+", "a1b2", RegexOptions.CultureInvariant)]
    [InlineData(@"[^\da]+", "a-b", RegexOptions.CultureInvariant)]
    [InlineData(@"[\w-]+", "a-b_c", RegexOptions.CultureInvariant)]
    [InlineData(@"[^\w\s]+", "a-b", RegexOptions.CultureInvariant)]
    [InlineData(@"[\d-e]+", "d-e5", RegexOptions.CultureInvariant)]
    [InlineData(@"[\Da]+", "ax5", RegexOptions.CultureInvariant)]
    [InlineData(@"[\d\s]+", "a 1", RegexOptions.CultureInvariant)]
    public void CountMatchesRuntimeForOwnedHybridClassBatch(string pattern, string input, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertCountParity();
    }
}
