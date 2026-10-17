using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8StaticPatternArgumentTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    [InlineData(3)] [InlineData(4)]
    public void NullPatternIsAnArgumentFailureBeforeConstructionAdmission(int operation)
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            using var owner = new Utf8ConstructionScope(0, 0, 0, 0);
            var error = Assert.Throws<ArgumentNullException>(() => Run(operation));
            Assert.Equal("pattern", error.ParamName);
            Assert.Equal(0ul, owner.Budget.WorkSteps);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    private static void Run(int operation)
    {
        switch (operation)
        {
            case 0: _ = Utf8Regex.IsMatch("x"u8, null!); break;
            case 1: _ = Utf8Regex.Count("x"u8, null!); break;
            case 2: _ = Utf8Regex.Match("x"u8, null!); break;
            case 3: _ = Utf8Regex.MatchDetailed("x"u8, null!); break;
            default: _ = Utf8Regex.EnumerateMatches("x"u8, null!); break;
        }
    }
}
