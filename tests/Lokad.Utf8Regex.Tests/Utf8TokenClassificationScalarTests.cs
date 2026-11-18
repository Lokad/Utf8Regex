using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TokenClassificationScalarTests
{
    private static void ScalarRange(string pattern, string text, int index, int length)
    {
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options); var input = Encoding.UTF8.GetBytes(text);
            Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
            var match = regex.Match(input); var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
            Assert.Equal((index, length), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            Assert.Equal((index, length), (detailed.Value.StartOffsetInBytes, (detailed.Value.EndOffsetInBytes - detailed.Value.StartOffsetInBytes)));
        }
    }

    [Fact]
    public void EscapedClassAtomsUseCanonicalScalarMembership()
    {
        ScalarRange(@"[\n]+:[\d]+[a-z]+", "\n:12a", 0, 5);
        ScalarRange("[a]+:[b]+[c]+", "a:bc", 0, 4);
        ScalarRange(@"[a]+:[b]+\.[c]+", "a:b.c", 0, 5);
    }

    [Fact]
    public void NulClassMembersRemainDistinctFromOptionalTailSeparation()
    {
        ScalarRange("[a]+:[b]+[c]+(?:\\?[\0x]*)?(?:#[y]*)?", "a:bc?\0x#y", 0, 9);
    }

    [Fact]
    public void AsciiIntersectionsDeclineUnicodeSubjectsWhileQualifiedWordTokensRemainNative()
    {
        ScalarRange("[a]+:[b]+[^x]+", "a:b😀", 0, 7);
        const string email = @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+";
        ScalarRange(email, "𐐀@𐐀.𐐀", 0, 14);
        const string plainWord = @"\w+@\w+\.\w+";
        ScalarRange(plainWord, "𐐀@𐐀.𐐀", 0, 14);
    }

    [Fact]
    public void UnsupportedSecondaryLiteralsKeepScalarGrammarWithoutSelectingAnIncompleteShortcut()
    {
        ScalarRange("[a]+:[b]+[c]+\\?[x]*", "a:bc?xx", 0, 7);
    }

    [Fact]
    public void DelimitedGreedyMiddleNeverAcceptsTailOnlyBytesBeforeItsSecondaryDelimiter()
    {
        const string pattern = "[a]+:[b]+\\.[c]+";
        var regex = new Utf8Regex(pattern);
        var oracle = new Regex(pattern);
        foreach (var text in Subjects(5, ""))
        {
            var expected = oracle.Match(text); var actual = regex.Match(Encoding.UTF8.GetBytes(text));
            Assert.Equal(expected.Success, actual.Success);
            if (actual.Success) Assert.Equal((expected.Index, expected.Length), (actual.StartOffsetInBytes, actual.EndOffsetInBytes - actual.StartOffsetInBytes));
        }
        Assert.False(regex.IsMatch("a:bc.c"u8));
    }private static IEnumerable<string> Subjects(int remaining, string prefix)
    {
        yield return prefix; if (remaining == 0) yield break;
        foreach (var value in "x.1") foreach (var text in Subjects(remaining - 1, prefix + value)) yield return text;
    }

    [Fact]
    public void OverlappingGreedyHeadsDeclineEarliestLiteralShortcuts()
    {
        const string pattern = "[a:]+:[a]+[a]+";
        ScalarRange(pattern, "a:aa:aa", 0, 7);
    }
}
