using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexCountDiffTests
{
    [Theory]
    [InlineData(@"\b\w+\b", "abc def ghi jkl", RegexOptions.None, 0)]
    [InlineData(@"\b\w+\b", "abc def ghi jkl", RegexOptions.None, 7)]
    [InlineData(@"A", "aAaA", RegexOptions.IgnoreCase, 0)]
    [InlineData(@".", "\n\n\n", RegexOptions.Singleline, 0)]
    public void CountMatchesRuntimeForCuratedSurfaceCases(string pattern, string input, RegexOptions options, int startAt)
    {
        RegexParityContext.Create(pattern, input, options).AssertCountParity(startAt);
    }

    [Theory]
    [InlineData(@"", "ab", 0)]
    [InlineData(@"\w", "ab", 1)]
    [InlineData(@"(?<=abc)\w", "abcxabcy", 7)]
    public void CountMatchesRuntimeForMirroredDotNetBatch(string pattern, string input, int startAt)
    {
        // Mirrored and adapted from:
        // external/dotnet-runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Count.Tests.cs
        RegexParityContext.Create(pattern, input, RegexOptions.None).AssertCountParity(startAt);
    }
}
