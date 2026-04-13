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
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/RegexCharacterSetTests.cs
        RegexParityContext.Create(pattern, input, options).AssertIsMatchParity();
    }

    [Theory]
    [InlineData(@"\w+", "abc١٢٣", RegexOptions.CultureInvariant)]
    [InlineData(@"\w+", "אבג123", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+", "١٢٣", RegexOptions.CultureInvariant)]
    [InlineData(@"\d+", "१२३", RegexOptions.CultureInvariant)]
    public void MatchMatchesRuntimeForMirroredUnicodeCharacterBatch(string pattern, string input, RegexOptions options)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.UnicodeChar.Tests.cs
        RegexParityContext.Create(pattern, input, options).AssertMatchParity();
    }
}
