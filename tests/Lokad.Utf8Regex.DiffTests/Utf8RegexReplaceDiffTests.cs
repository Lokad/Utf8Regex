using System.Text;
using System.Buffers;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexReplaceDiffTests
{
    [Theory]
    [InlineData("", "   ", "123", RegexOptions.None)]
    [InlineData("icrosoft", "MiCrOsOfT", "icrosoft", RegexOptions.IgnoreCase)]
    [InlineData("D\\.(.+)", "D.Bau", "David $1", RegexOptions.None)]
    [InlineData("(?<cat>cat)\\s*(?<dog>dog)", "cat dog", "${cat}est ${dog}est", RegexOptions.None)]
    public void MirroredDotNetCasesAgreeAcrossModes(string pattern, string input, string replacement, RegexOptions options)
    {
        // Mirrored and adapted from (research checkout external/runtime @ 9ffface2f3f):
        // external/runtime/src/libraries/System.Text.RegularExpressions/tests/FunctionalTests/Regex.Replace.Tests.cs
        RegexParityContext.Create(pattern, input, options).AssertReplaceParity(replacement);
    }

    [Theory]
    [InlineData("a|ab|abc", "abcababa", "Q", RegexOptions.CultureInvariant)]
    [InlineData("a|ab|abc", "abcababa", "<$&>", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "CAT horse Cat HORSE cat", "Q", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "CAT horse Cat HORSE cat", "$&!", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cathorsecat", "X", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "cat horse fish", "$1-$2", RegexOptions.CultureInvariant)]
    [InlineData("(cat)|(horse)", "CAT horse", "$1!", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cat horse", "a$`b", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cat horse", "a$" + "'" + "b", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cat horse", "$$", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "", "Q", RegexOptions.CultureInvariant)]
    public void LiteralFamilyReplacementAgreesAcrossModes(string pattern, string input, string replacement, RegexOptions options)
    {
        RegexParityContext.Create(pattern, input, options).AssertReplaceParity(replacement);
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "ZZ", RegexOptions.CultureInvariant)]
    [InlineData("abc", "nomatch", "ZZ", RegexOptions.CultureInvariant)]
    [InlineData("needle", "needle in haystack", "pin", RegexOptions.CultureInvariant)]
    [InlineData("café", "αcaféβcaféγ", "tea", RegexOptions.CultureInvariant)]
    [InlineData("😀", "😀x😀y", "!", RegexOptions.CultureInvariant)]
    [InlineData("café|niño", "αcaféβniñoγ", "tea", RegexOptions.CultureInvariant)]
    [InlineData("café|niño|résumé", "résumé café niño", "x", RegexOptions.CultureInvariant)]
    [InlineData(@"\b(?:café|niño)\b", "café niño caféx xniño niño", "tea", RegexOptions.CultureInvariant)]
    [InlineData(@"\b(?:café|niño|résumé)\b", "résumé café niño", "x", RegexOptions.CultureInvariant)]
    [InlineData("café(?= noir)", "café noir café gris café noir", "bistro", RegexOptions.CultureInvariant)]
    [InlineData("niño(?= listo)", "niño listo niño triste", "child", RegexOptions.CultureInvariant)]
    [InlineData("(café|niño)", "αcaféβniñoγ", "$1-x", RegexOptions.CultureInvariant)]
    [InlineData("(café|niño|résumé)", "résumé café niño", "$1!", RegexOptions.CultureInvariant)]
    [InlineData(@"\b(café|niño)\b", "café niño caféx xniño niño", "$1-x", RegexOptions.CultureInvariant)]
    [InlineData(@"\b(café|niño|résumé)\b", "résumé café niño", "$1!", RegexOptions.CultureInvariant)]
    [InlineData("(café)", "αcaféβcaféγ", "$1-bistro", RegexOptions.CultureInvariant)]
    [InlineData("(😀)", "😀x😀y", "$1!", RegexOptions.CultureInvariant)]
    [InlineData("(?<left>ab)(?<digit>[0-9])d", "ab1d xx ab2d", "${digit}${left}", RegexOptions.CultureInvariant)]
    [InlineData("([A-Z])([0-9])", "A1 B2", "$2$1", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$$", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$&", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$`|$'", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "x$$y", RegexOptions.CultureInvariant)]
    [InlineData("ab.cd", "abXcd zz abYcd", "x$$y", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$1", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$10", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "${1}", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "${name}", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "x$1y", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxABCxxabc", "ZZ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("abc", "nomatch", "ZZ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("ab.cd", "abXcd zz abYcd", "Q", RegexOptions.CultureInvariant)]
    [InlineData("..a", "12a xx 34a", "Z", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d", "ab1d xx ab2d", "Q", RegexOptions.CultureInvariant)]
    [InlineData("a[^x]d", "ayd axd azd", "Z", RegexOptions.CultureInvariant)]
    [InlineData("^abc", "abc abc", "Q", RegexOptions.CultureInvariant)]
    [InlineData("abc$", "abc abc", "Q", RegexOptions.CultureInvariant)]
    [InlineData("abc$", "abc\n", "Q", RegexOptions.CultureInvariant)]
    [InlineData("^ab.cd$", "abXcd", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"\d\d", "12 34", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"a\sb", "a b axb", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"\.", "a.b.c", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"a{3}", "aaa x aaa", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"\d{2}", "12 345", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"[ab]{2}", "ab ba cc", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"[\d][A-Z]", "3Z 9X zz", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"[\w-][\s]", "_  -\t", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"\n", "a\nb\nc", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"[\r\n]", "\r\n", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"\x41", "A BA", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"[\x30A]", "0 A B", "Q", RegexOptions.CultureInvariant)]
    [InlineData("cat|dog", "cat dog fish", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"ab.cd|xy\d\d", "abXcd xy12 zz", "Q", RegexOptions.CultureInvariant)]
    [InlineData("cat|horse", "cat horse fish", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"ab?c", "ac abc abbc", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"\d{2,4}", "1 12 123 1234", "Q", RegexOptions.CultureInvariant)]
    [InlineData("(?:cat|horse)", "cat horse fish", "Q", RegexOptions.CultureInvariant)]
    [InlineData("(ab.cd)", "abXcd zz", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"[A-Za-z]{10}\s+[\s\S]{0,100}Result[\s\S]{0,100}\s+[A-Za-z]{10}", "AlphaBravo  xx Result yy ZetaTheta", "Q", RegexOptions.CultureInvariant)]
    [InlineData(@"(?:[A-Z][a-z]+\s*){10,100}", "John Paul George Ringo Alice Bob Carol Dave Erin Frank", "Q", RegexOptions.CultureInvariant)]
    [InlineData("ab.cd", "xxABxCDyy", "ZZ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("a[bc]d", "zzaBdyy", "Q", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData(@"a\wb", "A_B a-b", "Q", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("ab.cd", "abXcd zz abYcd", "$&!", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d", "ab1d xx ab2d", "$$", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$`|$'|$_", RegexOptions.CultureInvariant)]
    [InlineData("ab.cd", "abXcd zz abYcd", "$0!", RegexOptions.CultureInvariant)]
    [InlineData("(ab)(cd)", "abcd xx abcd", "$2-$1", RegexOptions.CultureInvariant)]
    [InlineData("(a)(b)?", "xab ay", "$2-$1", RegexOptions.CultureInvariant)]
    [InlineData("(?<left>ab)(?<right>cd)", "abcd xx abcd", "${right}-${left}", RegexOptions.CultureInvariant)]
    [InlineData("^abc", "abc\nzzz\nabc", "Q", RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    [InlineData("a.b", "a\nb xx a\nb", "Q", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", "Q", RegexOptions.CultureInvariant)]
    [InlineData("café", "café café", "tea", RegexOptions.CultureInvariant)]
    [InlineData("(a)(b)?", "xab ay", "$`|$'|$+|$_", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "piñata", RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", "茶", RegexOptions.CultureInvariant)]
    [InlineData("ab[0-9]d", "ab1d xx ab2d", "ZZ", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$&!", RegexOptions.CultureInvariant)]
    [InlineData("café", "αcaféβcaféγ", "$&!", RegexOptions.CultureInvariant)]
    [InlineData(@"\bé\b", "é a éb é é", "x", RegexOptions.CultureInvariant)]
    [InlineData("(ab)+", "xxababyy", "Q", RegexOptions.CultureInvariant)]
    [InlineData("", "abc", "-", RegexOptions.CultureInvariant)]
    public void ByteReplacementMatchesDotNet(string pattern, string input, string replacement, RegexOptions options)
    {
        using var utf8Regex = new Utf8Regex(pattern, options: options);
        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var actual = utf8Regex.Replace(Encoding.UTF8.GetBytes(input), Encoding.UTF8.GetBytes(replacement));

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("abc", "xxabcxxabc", "piñata", RegexOptions.CultureInvariant)]
    [InlineData("ab+", "ab abb abbb", "茶", RegexOptions.CultureInvariant)]
    [InlineData("café", "αcaféβcaféγ", "茶", RegexOptions.CultureInvariant)]
    [InlineData("(ab)(cd)", "abcd xx abcd", "$2-$1", RegexOptions.CultureInvariant)]
    [InlineData("abc", "xxabcxxabc", "$`|$'|$_", RegexOptions.CultureInvariant)]
    [InlineData("(a)(b)?", "xab ay", "$2-$1", RegexOptions.CultureInvariant)]
    [InlineData("café", "αcaféβcaféγ", "$&!", RegexOptions.CultureInvariant)]
    public void TryReplaceWritesExpectedBytes(string pattern, string input, string replacement, RegexOptions options)
    {
        using var utf8Regex = new Utf8Regex(pattern, options: options);
        var expected = Encoding.UTF8.GetBytes(Regex.Replace(input, pattern, replacement, options));
        var destination = new byte[expected.Length];

        var status = utf8Regex.TryReplace(
            Encoding.UTF8.GetBytes(input),
            Encoding.UTF8.GetBytes(replacement),
            destination,
            out var bytesWritten);

        Assert.Equal(OperationStatus.Done, status);
        Assert.Equal(expected.Length, bytesWritten);
        Assert.Equal(expected, destination);
    }

    [Theory]
    [InlineData("abc", "xxabcxx", "toolong", "xyz")]
    [InlineData("(ab)(cd)", "abcd xx abcd", "$2-$1", "12345")]
    public void TryReplaceLeavesInsufficientDestinationUntouched(string pattern, string input, string replacement, string initial)
    {
        using var utf8Regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        var expected = Encoding.UTF8.GetBytes(initial);
        var destination = (byte[])expected.Clone();

        var status = utf8Regex.TryReplace(
            Encoding.UTF8.GetBytes(input),
            Encoding.UTF8.GetBytes(replacement),
            destination,
            out var bytesWritten);

        Assert.Equal(OperationStatus.DestinationTooSmall, status);
        Assert.Equal(0, bytesWritten);
        Assert.Equal(expected, destination);
    }
}
