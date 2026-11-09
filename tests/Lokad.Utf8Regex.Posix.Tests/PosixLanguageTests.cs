using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixLanguageTests
{
    // Exercise the adapter's own class/anchor dispatch, not Core's ordered runner.
    [Theory]
    [InlineData("[a-z]+", "abc", false, true)]
    [InlineData("[a-z]+", "ABC", false, false)]
    [InlineData("[é-ê]", "é", false, true)]
    [InlineData("[é-ê]", "😀", false, false)]
    [InlineData("[😀-🙏]", "😀", false, true)]
    [InlineData("[^a]", "😀", false, true)]
    [InlineData("[^a]", "a", false, false)]
    [InlineData("[^a]", "\n", false, true)]
    [InlineData("[^a]", "\n", true, false)]
    [InlineData(".", "\n", false, true)]
    [InlineData(".", "\n", true, false)]
    [InlineData("^a$", "a\n", false, false)]
    [InlineData("^a$", "a\n", true, true)]
    [InlineData("^a$", "ba", true, false)]
    [InlineData("\\(\\)", "()", false, true)]
    [InlineData("[]-]", "]", false, true)]
    [InlineData("[]-]", "-", false, true)]
    [InlineData("[-a]", "-", false, true)]
    [InlineData("[\\]", "\\", false, true)]
    [InlineData("a{0}", "", false, true)]
    [InlineData("a{2,3}", "aa", false, true)]
    [InlineData("a{2,3}", "a", false, false)]
    public void ScalarAtomsAndEreOperatorsRetainTheirAdmittedLanguage(string pattern, string input, bool newline, bool expected)
    {
        var regex = new Utf8PosixRegex(pattern, newline ? PosixCompileOptions.Newline : default,
            Timeout.InfiniteTimeSpan, default);
        Assert.Equal(expected, regex.IsMatch(System.Text.Encoding.UTF8.GetBytes(input)));
    }

    [Theory]
    [InlineData("alnum", "Az09", "_é😀")]
    [InlineData("alpha", "Az", "09_é😀")]
    [InlineData("blank", "\t ", "\n\ré")]
    [InlineData("cntrl", "\0\t\n\u001f\u007f", " Azé")]
    [InlineData("digit", "09", "az²")]
    [InlineData("graph", "!Az09~", "\t \u007fé")]
    [InlineData("lower", "az", "AZé")]
    [InlineData("print", " !~", "\t\u007fé")]
    [InlineData("punct", "!_~", "Az09 é")]
    [InlineData("space", "\t\n\v\f\r ", "\0A\u0085")]
    [InlineData("upper", "AZ", "azÉ")]
    [InlineData("xdigit", "09aAfF", "gGé")]
    public void NamedClassesHaveFixedAsciiMembership(string name, string accepted, string rejected)
    {
        foreach (var scalar in accepted.EnumerateRunes()) Check(scalar, true);
        foreach (var scalar in rejected.EnumerateRunes()) Check(scalar, false);
        void Check(Rune scalar, bool expected) => ScalarAtomsAndEreOperatorsRetainTheirAdmittedLanguage(
            $"[[:{name}:]]", scalar.ToString(), false, expected);
    }

}
