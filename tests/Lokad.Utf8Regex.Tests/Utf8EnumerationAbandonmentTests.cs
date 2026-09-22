using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R06/R07 lazy-access cleanup: abandoning a lazy enumeration early must leave
// the instance fully reusable with results identical to uninterrupted use.
public sealed class Utf8EnumerationAbandonmentTests
{
    [Theory]
    [InlineData("abc", "abc abc abc")]
    [InlineData("[a-z]+", "ab cd ef")]
    public void AbandonedEnumerationLeavesInstanceReusable(string pattern, string inputText)
    {
        var options = RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var expectedCount = new Utf8Regex(pattern, options).Count(input);
        Assert.True(expectedCount > 1);

        var reused = new Utf8Regex(pattern, options);
        var enumerator = reused.EnumerateMatches(input);
        Assert.True(enumerator.MoveNext());
        // Abandoned without exhausting: the stack-only enumerator carries no
        // Dispose, so pooled rents are GC-covered, but the instance itself
        // must stay exact on every later operation.
        Assert.Equal(expectedCount, reused.Count(input));

        var seen = 0;
        foreach (var item in reused.EnumerateMatches(input))
        {
            seen++;
        }

        Assert.Equal(expectedCount, seen);
    }
}
