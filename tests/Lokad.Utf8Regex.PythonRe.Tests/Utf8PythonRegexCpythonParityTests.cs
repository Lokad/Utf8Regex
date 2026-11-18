using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class Utf8PythonRegexCpythonParityTests
{
    [Fact]
    public void TopLevelAsciiAndUnicodeFlagsAreIncompatibleLikeCpython()
    {
        var exception = Assert.Throws<PythonRePatternException>(() =>
            new Utf8PythonRegex("abc", options: PythonReCompileOptions.Ascii | PythonReCompileOptions.Unicode));

        Assert.Contains("ASCII and UNICODE flags are incompatible", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchAndMatchFollowCpythonStarPlusSemantics()
    {
        Assert.Equal((0, 0), GetSpan(new Utf8PythonRegex("x*").Match("axx"u8, mode: Utf8PythonMatchMode.Search)));
        Assert.Equal((1, 3), GetSpan(new Utf8PythonRegex("x+").Match("axx"u8, mode: Utf8PythonMatchMode.Search)));
        Assert.False(new Utf8PythonRegex("x").Match("aaa"u8, mode: Utf8PythonMatchMode.Search).Success);

        Assert.Equal((0, 0), GetSpan(new Utf8PythonRegex("a*").Match("xxx"u8)));
        Assert.Equal((0, 3), GetSpan(new Utf8PythonRegex("x*").Match("xxxa"u8)));
        Assert.False(new Utf8PythonRegex("a+").Match("xxx"u8).Success);
    }

    [Fact]
    public void BranchingAndAlternationFollowCpythonExamples()
    {
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex("(ab|ba)").Match("ab"u8)));
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex("(ab|ba)").Match("ba"u8)));
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex("(abc|bac|ca|cb)").Match("ca"u8)));
        Assert.Equal((0, 1), GetSpan(new Utf8PythonRegex("((a)|(b)|(c))").Match("c"u8)));
    }

    [Fact]
    public void FullMatchFollowsCpythonExamples()
    {
        Assert.Equal((0, 1), GetSpan(new Utf8PythonRegex("a").Match("a"u8, mode: Utf8PythonMatchMode.FullMatch)));
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex("a|ab").Match("ab"u8, mode: Utf8PythonMatchMode.FullMatch)));
        Assert.Equal((0, 3), GetSpan(new Utf8PythonRegex(".*?").Match("abc"u8, mode: Utf8PythonMatchMode.FullMatch)));
        Assert.Equal((0, 4), GetSpan(new Utf8PythonRegex("ab(?=c)cd").Match("abcd"u8, mode: Utf8PythonMatchMode.FullMatch)));
        Assert.Equal((0, 4), GetSpan(new Utf8PythonRegex("ab(?<=b)cd").Match("abcd"u8, mode: Utf8PythonMatchMode.FullMatch)));
        Assert.False(new Utf8PythonRegex("abc$").Match("abc\n"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.False(new Utf8PythonRegex("(?m)abc$").Match("abc\n"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void ConditionalGroupsFollowCpythonExamples()
    {
        var first = new Utf8PythonRegex(@"^(\()?([^()]+)(?(1)\))$");
        var second = new Utf8PythonRegex(@"^(?:(a)|c)((?(1)b|d))$");
        var third = new Utf8PythonRegex(@"^(?:(a)|c)((?(1)|d))$");
        var named = new Utf8PythonRegex(@"(?P<g1>a)(?P<g2>b)?((?(g2)c|d))");

        Assert.True(first.Match("(a)"u8).Success);
        Assert.True(first.Match("a"u8).Success);
        Assert.False(first.Match("a)"u8).Success);
        Assert.False(first.Match("(a"u8).Success);

        Assert.True(second.Match("ab"u8).Success);
        Assert.True(second.Match("cd"u8).Success);
        Assert.True(third.Match("cd"u8).Success);
        Assert.True(third.Match("a"u8).Success);
        Assert.True(named.Match("abc"u8).Success);
        Assert.True(named.Match("ad"u8).Success);
        Assert.False(named.Match("abd"u8).Success);
        Assert.False(named.Match("ac"u8).Success);
    }

    [Fact]
    public void VerboseModeAndCommentsFollowCpythonExamples()
    {
        Assert.True(new Utf8PythonRegex("(?x) a").Match("a"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(new Utf8PythonRegex(" a(?x: b) c").Match(" ab c"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(new Utf8PythonRegex(" a(?-x: b) c", options: PythonReCompileOptions.Verbose).Match("a bc"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(new Utf8PythonRegex("#x\na", options: PythonReCompileOptions.Verbose).Match("a"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(new Utf8PythonRegex("(?x)#x\na").Match("a"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(new Utf8PythonRegex("(?x)#x\na|#y\nb").Match("a"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(new Utf8PythonRegex("(?x)#x\na|#y\nb").Match("b"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void ReplacementTemplatesSupportCpythonGroupAndEscapeExamples()
    {
        Assert.Equal("x x", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(?i)b+", options: PythonReCompileOptions.IgnoreCase).Replace("bbbb BBBB"u8, "x"u8)));
        Assert.Equal("xxxx", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(?P<a>x)").Replace("xx"u8, @"\g<a>\g<a>"u8)));
        Assert.Equal("xxxx", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(?P<a>x)").Replace("xx"u8, @"\g<a>\g<1>"u8)));
        Assert.Equal("xxxx", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("()x").Replace("xx"u8, @"\g<0>\g<0>"u8)));
        Assert.Equal("<>b<>", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(a)?").Replace("b"u8, @"<\1>"u8)));
        Assert.Equal("<>b<>", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(a)?").Replace("b"u8, @"<\g<1>>"u8)));
        Assert.Equal("\t\n\v\r\f\a\b", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("a").Replace("a"u8, @"\t\n\v\r\f\a\b"u8)));
        Assert.Equal("xx\bxx\b", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(?P<unk>x)").Replace("xx"u8, @"\g<1>\g<1>\b"u8)));
        Assert.Equal("hello there", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"(\S)\s+(\S)").Replace("hello  there"u8, @"\1 \2"u8)));
    }

    [Fact]
    public void ReplacementNumericEscapesFollowCpythonExamples()
    {
        var regex = new Utf8PythonRegex("x");

        Assert.Equal("\0", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\0"u8)));
        Assert.Equal("\0", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\000"u8)));
        Assert.Equal("\0" + "8", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\008"u8)));
        Assert.Equal("\0" + "9", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\009"u8)));
        Assert.Equal("\x49", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\111"u8)));
        Assert.Equal("\x4F", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\117"u8)));
        Assert.Equal("\xFF", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\377"u8)));
        Assert.Equal("\x49" + "1", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\1111"u8)));
        Assert.Equal("\x07", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\07"u8)));
        Assert.Equal("\0" + "a", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\0a"u8)));
        Assert.Equal("x", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\g<00>"u8)));
        Assert.Equal("x", System.Text.Encoding.UTF8.GetString(regex.Replace("x"u8, @"\g<000>"u8)));

        Assert.Equal("x", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(((((((((((x)))))))))))").Replace("x"u8, @"\11"u8)));
        Assert.Equal("x", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(x)").Replace("x"u8, @"\g<01>"u8)));
        Assert.Equal("x", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(x)").Replace("x"u8, @"\g<001>"u8)));
        Assert.Equal("xz8", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("((((((((((y))))))))))(.)").Replace("xyz"u8, @"\118"u8)));
        Assert.Equal("xza", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("((((((((((y))))))))))(.)").Replace("xyz"u8, @"\11a"u8)));
    }

    [Fact]
    public void SubnFollowsCpythonExamples()
    {
        var first = PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("(?i)b+", options: PythonReCompileOptions.IgnoreCase).Subn("bbbb BBBB"u8, "x"u8));
        var second = PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("b+").Subn("bbbb BBBB"u8, "x"u8));
        var third = PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("b+").Subn("xyz"u8, "x"u8));
        var fourth = PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("b*").Subn("xyz"u8, "x"u8));
        var limited = PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("b*").Subn("xyz"u8, "x"u8, count: 2));
        var emptyTwo = PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("").Subn("ab"u8, "-"u8, count: 2));
        var emptyThree = PythonReSubstitutionAssert.Decode(new Utf8PythonRegex("").Subn("ab"u8, "-"u8, count: 3));

        Assert.Equal(("x x", 2), (first.ResultText, first.ReplacementCount));
        Assert.Equal(("x BBBB", 1), (second.ResultText, second.ReplacementCount));
        Assert.Equal(("xyz", 0), (third.ResultText, third.ReplacementCount));
        Assert.Equal(("xxxyxzx", 4), (fourth.ResultText, fourth.ReplacementCount));
        Assert.Equal(("xxxyz", 2), (limited.ResultText, limited.ReplacementCount));
        Assert.Equal(("-a-b", 2), (emptyTwo.ResultText, emptyTwo.ReplacementCount));
        Assert.Equal(("-a-b-", 3), (emptyThree.ResultText, emptyThree.ReplacementCount));
    }

    [Fact]
    public void SplitFollowsCpythonExamples()
    {
        AssertSplit(["", "a", "b", "", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex(":").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", "a", "b", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex(":+").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", ":", "a", ":", "b", "::", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex("(:+)").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", "a", "b", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex("(?::+)").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", ":", "a", ":", "b", ":", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex("(:)+").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", null, ":", "a", null, ":", "", "b", null, "", null, "::", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex("(b)|(:+)").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", "a", "", "", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex("(?:b)|(?::+)").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", "a", "", "", "bc", ""], PythonReSplitAssert.Decode("a::bc"u8, new Utf8PythonRegex(@"\b|:+").SplitRanges("a::bc"u8, 0, 0, default)));
        AssertSplit(["", ":a", ":b", ":", ":c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex(@"(?=:)").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit([":", "a:", "b:", ":", "c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex(@"(?<=:)").SplitRanges(":a:b::c"u8, 0, 0, default)));
        AssertSplit(["", "a", "b::c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex(":").SplitRanges(":a:b::c"u8, 2, 0, default)));
        AssertSplit([":a:b::c"], PythonReSplitAssert.Decode(":a:b::c"u8, new Utf8PythonRegex(":").SplitRanges(":a:b::c"u8, -1, 0, default)));
    }

    [Fact]
    public void EmptyOnlySplitPatternsFollowCpythonProgression()
    {
        AssertSplit(["", "a", "b", "c", ""], PythonReSplitAssert.Decode("abc"u8, new Utf8PythonRegex("").SplitRanges("abc"u8, 0, 0, default)));
        AssertSplit(["", "a", "bc"], PythonReSplitAssert.Decode("abc"u8, new Utf8PythonRegex("").SplitRanges("abc"u8, 2, 0, default)));
        AssertSplit(
            ["", "alpha", " ", "beta", ""],
            PythonReSplitAssert.Decode("alpha beta"u8, new Utf8PythonRegex(@"\b", options: PythonReCompileOptions.Ascii).SplitRanges("alpha beta"u8, 0, 0, default)));
        AssertSplit(["a", ":b", ":"], PythonReSplitAssert.Decode("a:b:"u8, new Utf8PythonRegex(@"(?=:)").SplitRanges("a:b:"u8, 0, 0, default)));
        AssertSplit(["abc"], PythonReSplitAssert.Decode("abc"u8, new Utf8PythonRegex(@"(?!)").SplitRanges("abc"u8, 0, 0, default)));
        AssertSplit(["", "𝒜", "b", ""], PythonReSplitAssert.Decode("𝒜b"u8, new Utf8PythonRegex("").SplitRanges("𝒜b"u8, 0, 0, default)));
    }

    [Fact]
    public void ReplacementTemplateErrorsFollowCpythonExamples()
    {
        AssertTemplateError("x", @"\400", "x", "octal escape value \\400 outside of range 0-0o377");
        AssertTemplateError("x", @"\777", "x", "octal escape value \\777 outside of range 0-0o377");
        AssertTemplateError("x", @"\1", "x", "invalid group reference 1");
        AssertTemplateError("x", @"\8", "x", "invalid group reference 8");
        AssertTemplateError("x", @"\9", "x", "invalid group reference 9");
        AssertTemplateError("x", @"\11", "x", "invalid group reference 11");
        AssertTemplateError("x", @"\18", "x", "invalid group reference 18");
        AssertTemplateError("x", @"\1a", "x", "invalid group reference 1");
        AssertTemplateError("x", @"\90", "x", "invalid group reference 90");
        AssertTemplateError("x", @"\99", "x", "invalid group reference 99");
        AssertTemplateError("x", @"\118", "x", "invalid group reference 11");
        AssertTemplateError("x", @"\11a", "x", "invalid group reference 11");
        AssertTemplateError("x", @"\181", "x", "invalid group reference 18");
        AssertTemplateError("x", @"\800", "x", "invalid group reference 80");
        AssertTemplateError("(?P<a>x)", @"\g<a", "xx", "missing >, unterminated name");
        AssertTemplateError("(?P<a>x)", @"\g<", "xx", "missing group name");
        AssertTemplateError("(?P<a>x)", @"\g", "xx", "missing <");
        AssertTemplateError("(?P<a>x)", @"\g<a a>", "xx", "bad character in group name 'a a'");
        AssertTemplateError("(?P<a>x)", @"\g<>", "xx", "missing group name");
        AssertTemplateError("(?P<a>x)", @"\g<1a1>", "xx", "bad character in group name '1a1'");
        AssertTemplateError("(?P<a>x)", @"\g<2>", "xx", "invalid group reference 2");
        AssertTemplateError("(?P<a>x)", @"\g<-1>", "xx", "bad character in group name '-1'");
        AssertTemplateError("(?P<a>x)", @"\g<+1>", "xx", "bad character in group name '+1'");
        AssertTemplateError("()()()()()()()()()()", @"\g<1_0>", "xx", "bad character in group name '1_0'");
        AssertTemplateError("(?P<a>x)", @"\g< 1 >", "xx", "bad character in group name ' 1 '");
        AssertTemplateError("(?P<a>x)", @"\g<©>", "xx", "bad character in group name '©'");
        AssertTemplateError("(?P<a>x)", @"\g<㊀>", "xx", "bad character in group name '㊀'");
        AssertTemplateError("(?P<a>x)", @"\g<¹>", "xx", "bad character in group name '¹'");
        AssertTemplateError("(?P<a>x)", @"\g<१>", "xx", "bad character in group name '१'");
        Assert.Throws<IndexOutOfRangeException>(() => System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("(?P<a>x)").Replace("xx"u8, @"\g<ab>"u8)));
    }

    [Fact]
    public void MalformedPatternsFollowCpythonMessagesForCoreCases()
    {
        AssertPatternError("(", "missing ), unterminated subpattern", 0);
        AssertPatternError("((a|b)", "missing ), unterminated subpattern", 0);
        AssertPatternError("(a|b))", "unbalanced parenthesis", 5);
        AssertPatternError(@"(?P=", "missing group name", 4);
        AssertPatternError(@"(?P=)", "missing group name", 4);
        AssertPatternError(@"(?P<)", "missing group name", 4);
        AssertPatternError(@"(?P<a", "missing >, unterminated name", 4);
        AssertPatternError(@"(?(", "missing group name", 3);
        AssertPatternError(@"(?())", "missing group name", 3);
        AssertPatternError(@"(?P=1)", "bad character in group name '1'", 4);
        AssertPatternError(@"(?P<a.>)", "bad character in group name 'a.'", 4);
        AssertPatternError(@"(?P=a)", "unknown group name 'a'");
        AssertPatternError(@"(?(a))", "unknown group name 'a'");
        AssertPatternError(@"(?(2)a)", "invalid group reference 2");
        AssertPatternError(@"(?<=a+)b", "look-behind requires fixed-width pattern");
    }

    [Fact]
    public void SymbolicGroupErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"(?P<a>)(?P<a>)", "redefinition of group name 'a'");
        AssertPatternError(@"(?P<a>(?P=a))", "cannot refer to an open group", 10);
        AssertPatternError(@"(?Pxy)", "unknown extension ?Px", 1);
        AssertPatternError(@"(?P<a>)(?P=a", "missing ), unterminated name", 11);
        AssertPatternError(@"(?P=a1)", "unknown group name 'a1'");
        AssertPatternError(@"(?P=a.)", "bad character in group name 'a.'", 4);
        AssertPatternError(@"(?P<1>)", "bad character in group name '1'", 4);
        AssertPatternError(@"(?(-1))", "bad character in group name '-1'", 3);
        AssertPatternError(@"(?(1a))", "bad character in group name '1a'", 3);
        AssertPatternError(@"(?(a.))", "bad character in group name 'a.'", 3);
        AssertPatternError(@"(?P<©>x)", "bad character in group name '©'", 4);
        AssertPatternError(@"(?P=©)", "bad character in group name '©'", 4);
        AssertPatternError(@"(?(©)y)", "bad character in group name '©'", 3);
    }

    [Fact]
    public void BalancingShapesFollowCpythonUnknownExtension()
    {
        // .NET balancing captures have no Python counterpart: CPython 3.13.2
        // rejects every (?< opener past (?<=/(?<!/(?<> as an unknown extension
        // naming the next character (ground-truth rejections probed locally).
        AssertPatternError(@"(?<o>a)+(?<-o>b)+", "unknown extension ?<o", 1);
        AssertPatternError(@"(?<a>x)(?<b-a>y)", "unknown extension ?<a", 1);
        AssertPatternError(@"(?<a>x)(?<b-a>y)(?<c-b>z)", "unknown extension ?<a", 1);
        AssertPatternError(@"(?<-o>b)", "unknown extension ?<-", 1);
        AssertPatternError(@"(?<1-a>x)", "unknown extension ?<1", 1);
        AssertPatternError(@"(?<a-1>x)", "unknown extension ?<a", 1);
    }

    [Fact]
    public void ConditionalReferenceErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"(?P<a>)(?(0)a|b)", "bad group number", 10);
        AssertPatternError(@"()(?(-1)a|b)", "bad character in group name '-1'", 5);
        AssertPatternError(@"()(?(+1)a|b)", "bad character in group name '+1'", 5);
        AssertPatternError(@"()(?(1_0)a|b)", "bad character in group name '1_0'", 5);
        AssertPatternError(@"()(?( 1 )a|b)", "bad character in group name ' 1 '", 5);
        AssertPatternError(@"()(?(1", "missing ), unterminated name", 5);
        AssertPatternError(@"()(?(1)a", "missing ), unterminated subpattern", 2);
        AssertPatternError(@"()(?(1)a|b", "missing ), unterminated subpattern", 2);
        AssertPatternError(@"()(?(1)a|b|c", "conditional backref with more than two branches", 10);
        AssertPatternError(@"()(?(1)a|b|c)", "conditional backref with more than two branches", 10);
    }

    [Fact]
    public void InlineFlagErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"(?-", "missing flag", 3);
        AssertPatternError(@"(?-+", "missing flag", 3);
        AssertPatternError(@"(?-z", "unknown flag", 3);
        AssertPatternError(@"(?-i", "missing :", 4);
        AssertPatternError(@"(?-i)", "missing :", 4);
        AssertPatternError(@"(?-i+", "missing :", 4);
        AssertPatternError(@"(?-iz", "unknown flag", 4);
        AssertPatternError(@"(?i:", "missing ), unterminated subpattern", 0);
        AssertPatternError(@"(?i", "missing -, : or )", 3);
        AssertPatternError(@"(?i+", "missing -, : or )", 3);
        AssertPatternError(@"(?iz", "unknown flag", 3);
    }

    [Fact]
    public void NumericBackreferenceErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"(abc\1)", "cannot refer to an open group", 4);
        AssertPatternError(@"\1", "invalid group reference 1", 1);
        AssertPatternError(@"\11", "invalid group reference 11", 1);
    }

    [Fact]
    public void BadEscapesFollowCpythonExamples()
    {
        AssertPatternError(@"\", "bad escape (end of pattern)", 0);

        foreach (var c in "ceghijklmopqyCEFGHIJKLMOPQRTVXY")
        {
            AssertPatternError(@"\" + c, $@"bad escape \{c}", 0);
        }

        // Contiguous matches have no Python counterpart (CPython 3.13.2 rejects them identically).
        AssertPatternError(@"\G", @"bad escape \G", 0);
    }

    [Fact]
    public void MiscErrorsFollowAdditionalCpythonExamples()
    {
        AssertPatternError(@"(?P", "unexpected end of pattern", 3);
        AssertPatternError(@"(?z)", "unknown extension ?z", 1);
        AssertPatternError(@"(?#abc", "missing ), unterminated comment", 0);
        AssertPatternError(@"(?<", "unexpected end of pattern", 3);
        AssertPatternError(@"(?<>)", "unknown extension ?<>", 1);
        AssertPatternError(@"(?", "unexpected end of pattern", 2);
    }

    [Fact]
    public void CharacterClassBadEscapesFollowCpythonExamples()
    {
        foreach (var c in "ceghijklmopqyzABCEFGHIJKLMOPQRTVXYZ")
        {
            AssertPatternError("[\\" + c + "]", $@"bad escape \{c}", 1);
        }
    }

    [Fact]
    public void NamedUnicodeEscapesAreIntentionallyUnsupported()
    {
        AssertPatternError(@"\N", "intentionally unsupported", 0);
        AssertPatternError(@"[\N]", "intentionally unsupported", 1);
        AssertPatternError(@"\N{", "intentionally unsupported", 0);
        AssertPatternError(@"[\N{", "intentionally unsupported", 1);
        AssertPatternError(@"\N{}", "intentionally unsupported", 0);
        AssertPatternError(@"[\N{}]", "intentionally unsupported", 1);
        AssertPatternError(@"\NSNAKE}", "intentionally unsupported", 0);
        AssertPatternError(@"[\NSNAKE}]", "intentionally unsupported", 1);
        AssertPatternError(@"\N{SNAKE", "intentionally unsupported", 0);
        AssertPatternError(@"[\N{SNAKE]", "intentionally unsupported", 1);
        AssertPatternError(@"[\N{SNAKE]}", "intentionally unsupported", 1);
        AssertPatternError(@"\N{SPAM}", "intentionally unsupported", 0);
        AssertPatternError(@"[\N{SPAM}]", "intentionally unsupported", 1);
        AssertPatternError(@"\N{KEYCAP NUMBER SIGN}", "intentionally unsupported", 0);
        AssertPatternError(@"[\N{KEYCAP NUMBER SIGN}]", "intentionally unsupported", 1);
    }

    [Fact]
    public void CharacterEscapesFollowCpythonExamples()
    {
        Assert.True(new Utf8PythonRegex(@"\141").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\1410").Match("a0"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\1418").Match("a8"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\x61").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\x610").Match("a0"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\u0061").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\u00610").Match("a0"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\U00000061").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\U0001D49C").Match("𝒜"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\0").Match("\0"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\08").Match("\08"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\01").Match("\u0001"u8).Success);
        Assert.True(new Utf8PythonRegex(@"\018").Match("\u00018"u8).Success);
    }

    [Fact]
    public void CharacterClassEscapesFollowCpythonExamples()
    {
        Assert.True(new Utf8PythonRegex(@"[\141]").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\1410]").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\1418]").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\x61]").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\x610]").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\u0061]").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\u00610]").Match("a"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\U00000061]").Match("a"u8).Success);
    }

    [Fact]
    public void AstralCharacterClassLiteralsAndRangesFollowPythonScalarSemantics()
    {
        Assert.True(new Utf8PythonRegex(@"[\U0001D49C]").Match("𝒜"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[𝒜]").Match("𝒜"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\U0001D49C-\U0001D49E]").Match("𝒜"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\U0001D49C-\U0001D49E]").Match("𝒝"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[\U0001D49C-\U0001D49E]").Match("𝒞"u8).Success);
        Assert.False(new Utf8PythonRegex(@"[\U0001D49C-\U0001D49E]").Match("𝒟"u8).Success);
        Assert.False(new Utf8PythonRegex(@"[\U0001D49C-\U0001D49E]").Match("A"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[A\U0001D49C-\U0001D49E]").Match("A"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[A\U0001D49C-\U0001D49E]").Match("𝒝"u8).Success);
        Assert.True(new Utf8PythonRegex(@"[^\U0001D49C-\U0001D49E]").Match("A"u8).Success);
        Assert.False(new Utf8PythonRegex(@"[^\U0001D49C-\U0001D49E]").Match("𝒝"u8).Success);
    }

    [Fact]
    public void CharacterEscapeErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"\567", @"octal escape value \567 outside of range 0-0o377", 0);
        AssertPatternError(@"\911", "invalid group reference 91", 1);
        AssertPatternError(@"\x1", @"incomplete escape \x1", 0);
        AssertPatternError(@"\x1z", @"incomplete escape \x1", 0);
        AssertPatternError(@"\u123", @"incomplete escape \u123", 0);
        AssertPatternError(@"\u123z", @"incomplete escape \u123", 0);
        AssertPatternError(@"\U0001234", @"incomplete escape \U0001234", 0);
        AssertPatternError(@"\U0001234z", @"incomplete escape \U0001234", 0);
        AssertPatternError(@"\U00110000", @"bad escape \U00110000", 0);
    }

    [Fact]
    public void CharacterClassEscapeErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"[\567]", @"octal escape value \567 outside of range 0-0o377", 1);
        AssertPatternError(@"[\911]", @"bad escape \9", 1);
        AssertPatternError(@"[\x1z]", @"incomplete escape \x1", 1);
        AssertPatternError(@"[\u123z]", @"incomplete escape \u123", 1);
        AssertPatternError(@"[\U0001234z]", @"incomplete escape \U0001234", 1);
        AssertPatternError(@"[\U00110000]", @"bad escape \U00110000", 1);
    }

    [Fact]
    public void CharacterClassRangeErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"[a-", "unterminated character set", 0);
        AssertPatternError(@"[\w-b]", @"bad character range \w-b", 1);
        AssertPatternError(@"[a-\w]", @"bad character range a-\w", 1);
        AssertPatternError(@"[b-a]", @"bad character range b-a", 1);
    }

    [Fact]
    public void RepeatErrorsFollowCpythonExamples()
    {
        AssertPatternError(@"*", "nothing to repeat", 0);
        AssertPatternError(@"+", "nothing to repeat", 0);
        AssertPatternError(@"?", "nothing to repeat", 0);
        AssertPatternError(@"{1,2}", "nothing to repeat", 0);
        AssertPatternError(@"(?:*)", "nothing to repeat", 3);
        AssertPatternError(@"x**", "multiple repeat", 2);
        AssertPatternError(@"x+*", "multiple repeat", 2);
        AssertPatternError(@"x??*", "multiple repeat", 3);
        AssertPatternError(@"x{1,2}*", "multiple repeat", 6);
    }

    [Fact]
    public void GlobalInlineFlagPlacementErrorsFollowCpythonExamples()
    {
        AssertPatternError("À(?i)", "global flags not at the start of the expression", 1);
        AssertPatternError("(?s).(?i)À", "global flags not at the start of the expression", 5);
        AssertPatternError("(?i) À (?x)", "global flags not at the start of the expression", 7);
        AssertPatternError("^(?i)À", "global flags not at the start of the expression", 1);
        AssertPatternError("$|(?i)À", "global flags not at the start of the expression", 2);
        AssertPatternError("(?:(?i)À)", "global flags not at the start of the expression", 3);
    }

    [Fact]
    public void UnicodeGroupNamesFollowCpythonExamples()
    {
        Assert.True(new Utf8PythonRegex(@"(?P<µ>x)").Match("x"u8).Success);
        Assert.True(new Utf8PythonRegex(@"(?P<𝔘𝔫𝔦𝔠𝔬𝔡𝔢>x)").Match("x"u8).Success);
        Assert.Equal("xx", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"(?P<µ>x)").Replace("xx"u8, @"\g<µ>"u8)));
        Assert.Equal("xx", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"(?P<𝔘𝔫𝔦𝔠𝔬𝔡𝔢>x)").Replace("xx"u8, @"\g<𝔘𝔫𝔦𝔠𝔬𝔡𝔢>"u8)));
    }

    [Fact]
    public void SpecialEscapesFollowCpythonExamples()
    {
        Utf8PythonRangeMatch snapshot1 = default;
        using (var captureCursor1 = new Utf8PythonRegex(@"\b(b.)\b").CreateMatchCursor(("abcd abc bcd bx"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor1.MoveNext(out snapshot1);
        }
        Assert.Equal("bx", GetGroup1(snapshot1));
        Utf8PythonRangeMatch snapshot2 = default;
        using (var captureCursor2 = new Utf8PythonRegex(@"\B(b.)\B").CreateMatchCursor(("abc bcd bc abxd"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor2.MoveNext(out snapshot2);
        }
        Assert.Equal("bx", GetGroup1(snapshot2));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"^abc$", options: PythonReCompileOptions.Multiline).Match("\nabc\n"u8, mode: Utf8PythonMatchMode.Search).GetValueBytes()));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"^\Aabc\Z$", options: PythonReCompileOptions.Multiline).Match("abc"u8, mode: Utf8PythonMatchMode.Search).GetValueBytes()));
        Assert.False(new Utf8PythonRegex(@"^\Aabc\Z$", options: PythonReCompileOptions.Multiline).Match("\nabc\n"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.Equal("1aa! a", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"\d\D\w\W\s\S").Match("1aa! a"u8, mode: Utf8PythonMatchMode.Search).GetValueBytes()));
    }

    [Fact]
    public void WordBoundarySemanticsFollowCpythonExamples()
    {
        Utf8PythonRangeMatch snapshot3 = default;
        using (var captureCursor3 = new Utf8PythonRegex(@"\b(abc)\b").CreateMatchCursor(("abc"u8).ToArray(), mode: Utf8PythonMatchMode.Search))
        {
            captureCursor3.MoveNext(out snapshot3);
        }
        Assert.Equal("abc", GetGroup1(snapshot3));
        Assert.True(new Utf8PythonRegex(@".\b").Match("a="u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b").Match("=a"u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b").Match("я="u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b").Match("=я"u8).Success);
    }

    [Fact]
    public void UnicodeInlineFlagIsAcceptedAsPythonNoOpForStrPatterns()
    {
        Assert.True(new Utf8PythonRegex(@"(?u)\w+").Match("café"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        Assert.True(new Utf8PythonRegex(@"(?u:\w+)").Match("café"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void AsciiModeChangesWordAndBoundarySemantics()
    {
        Assert.True(new Utf8PythonRegex(@"\b(abc)\b", options: PythonReCompileOptions.Ascii).Match("abc"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.False(new Utf8PythonRegex(@"\b(ьюя)\b", options: PythonReCompileOptions.Ascii).Match("ьюя"u8, mode: Utf8PythonMatchMode.Search).Success);
        Assert.True(new Utf8PythonRegex(@".\b", options: PythonReCompileOptions.Ascii).Match("a="u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b", options: PythonReCompileOptions.Ascii).Match("=a"u8).Success);
        Assert.False(new Utf8PythonRegex(@".\b", options: PythonReCompileOptions.Ascii).Match("я="u8).Success);
        Assert.False(new Utf8PythonRegex(@".\b", options: PythonReCompileOptions.Ascii).Match("=я"u8).Success);
    }

    [Fact]
    public void ScopedAsciiAndUnicodeFlagsFollowCpythonExamples()
    {
        Assert.True(new Utf8PythonRegex(@"\w(?a:\W)\w").Match("ààà"u8).Success);
        Assert.True(new Utf8PythonRegex(@"(?a:\W(?u:\w)\W)").Match("ààà"u8).Success);
        AssertPatternError(@"(?a)(?-a:\w)", "bad inline flags: cannot turn off flags 'a', 'u' and 'L'", 8);
        AssertPatternError(@"(?au:a)", "bad inline flags: flags 'a' and 'u' are incompatible", 4);
    }

    [Fact]
    public void RepeatMinMaxFollowsCpythonExamples()
    {
        Assert.False(new Utf8PythonRegex(@"^(\w){1}$").Match("abc"u8).Success);
        Assert.False(new Utf8PythonRegex(@"^(\w){1,2}$").Match("abc"u8).Success);
        Utf8PythonRangeMatch snapshot4 = default;
        using (var captureCursor4 = new Utf8PythonRegex(@"^(\w){3}$").CreateMatchCursor(("abc"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor4.MoveNext(out snapshot4);
        }
        Assert.Equal("c", GetGroup1(snapshot4));
        Utf8PythonRangeMatch snapshot5 = default;
        using (var captureCursor5 = new Utf8PythonRegex(@"^(\w){1,3}$").CreateMatchCursor(("abc"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor5.MoveNext(out snapshot5);
        }
        Assert.Equal("c", GetGroup1(snapshot5));
        Assert.True(new Utf8PythonRegex(@"^x{3}$").Match("xxx"u8).Success);
        Assert.True(new Utf8PythonRegex(@"^x{1,4}?$").Match("xxx"u8).Success);
        Assert.False(new Utf8PythonRegex(@"^x{1}$").Match("xxx"u8).Success);
    }

    [Fact]
    public void PossessiveQuantifiersFollowCpythonExamples()
    {
        Assert.False(new Utf8PythonRegex("e*+e").Match("eeee"u8).Success);
        Assert.Equal("eeea", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("e++a").Match("eeea"u8).GetValueBytes()));
        Assert.Equal("ea", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex("e?+a").Match("ea"u8).GetValueBytes()));
        Assert.False(new Utf8PythonRegex("(.)++.").Match("ee"u8).Success);
        Assert.Equal((0, 0), GetSpan(new Utf8PythonRegex("x*+").Match("axx"u8, mode: Utf8PythonMatchMode.Search)));
        Assert.Equal((1, 3), GetSpan(new Utf8PythonRegex("x++").Match("axx"u8, mode: Utf8PythonMatchMode.Search)));
        var snapshots1Regex = new Utf8PythonRegex("a?+");
        ReadOnlyMemory<byte> snapshots1Input = ("aab"u8).ToArray();
        var snapshots1 = new List<Utf8PythonRangeMatch>();
        using (var snapshots1Cursor = snapshots1Regex.CreateMatchCursor(snapshots1Input, 0, default))
        {
            while (snapshots1Cursor.MoveNext(out var snapshots1Match))
                snapshots1.Add(snapshots1Match);
        }
        Assert.Equal(4, snapshots1.Count);
    }

    [Fact]
    public void AtomicGroupingFollowsCpythonExamples()
    {
        Assert.False(new Utf8PythonRegex(@"a(?>bc|b)c").Match("abc"u8).Success);
        Assert.True(new Utf8PythonRegex(@"a(?>bc|b)c").Match("abcc"u8).Success);
        Assert.False(new Utf8PythonRegex(@"(?>.*).").Match("abc"u8).Success);
        Assert.True(new Utf8PythonRegex(@"(?>x)++").Match("xxx"u8).Success);
        Assert.True(new Utf8PythonRegex(@"(?>x++)").Match("xxx"u8).Success);
        Assert.False(new Utf8PythonRegex(@"(?>x)++x").Match("xxx"u8).Success);
        Assert.False(new Utf8PythonRegex(@"(?>x++)x").Match("xxx"u8).Success);
        Assert.True(new Utf8PythonRegex(@"(?>a+)b").Match("ab"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
        var snapshots2Regex = new Utf8PythonRegex(@"(?>(?:ab)?)");
        ReadOnlyMemory<byte> snapshots2Input = ("ababc"u8).ToArray();
        var snapshots2 = new List<Utf8PythonRangeMatch>();
        using (var snapshots2Cursor = snapshots2Regex.CreateMatchCursor(snapshots2Input, 0, default))
        {
            while (snapshots2Cursor.MoveNext(out var snapshots2Match))
                snapshots2.Add(snapshots2Match);
        }
        Assert.Equal(4, snapshots2.Count);
    }

    [Fact]
    public void PossessiveQuantifierCapturesFollowCpythonExamples()
    {
        Utf8PythonRangeMatch snapshot6 = default;
        using (var captureCursor6 = new Utf8PythonRegex(@"(ae)*+a").CreateMatchCursor(("aea"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor6.MoveNext(out snapshot6);
        }
        Assert.Equal("ae", GetGroup1(snapshot6));
        Utf8PythonRangeMatch snapshot7 = default;
        using (var captureCursor7 = new Utf8PythonRegex(@"([ae][ae])?+a").CreateMatchCursor(("aea"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor7.MoveNext(out snapshot7);
        }
        Assert.Equal("ae", GetGroup1(snapshot7));
        Utf8PythonRangeMatch snapshot8 = default;
        using (var captureCursor8 = new Utf8PythonRegex(@"(e?){2,4}+a").CreateMatchCursor(("eeea"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor8.MoveNext(out snapshot8);
        }
        Assert.Equal(string.Empty, GetGroup1(snapshot8));
        Utf8PythonRangeMatch snapshot9 = default;
        using (var captureCursor9 = new Utf8PythonRegex(@"()*+a").CreateMatchCursor(("a"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor9.MoveNext(out snapshot9);
        }
        Assert.Equal(string.Empty, GetGroup1(snapshot9));
    }

    [Fact]
    public void PossessiveAndAtomicRegressionExamplesFollowCpython()
    {
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex(@"(?>(?:.(?!D))+)").Match("ABCDE"u8)));
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex(@"(?:.(?!D))++").Match("ABCDE"u8)));
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex(@"(?>(?:ab?c)+)").Match("aca"u8)));
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex(@"(?:ab?c)++").Match("aca"u8)));
    }

    [Fact]
    public void PossessiveRepeatCapturesFollowCpythonRegression()
    {
        Utf8PythonRangeMatch match = default;
        using (var captureCursor10 = new Utf8PythonRegex(@"((x)|y|z)*+").CreateMatchCursor(("xyz"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor10.MoveNext(out match);
        }
        Assert.True(match.Success);
        Assert.Equal("z", GetGroup(match, 1));
        Assert.Equal("x", GetGroup(match, 2));
    }

    [Fact]
    public void NamedCaptureMetadataFollowsCpythonExamples()
    {
        Utf8PythonRangeMatch match = default;
        using var regex = new Utf8PythonRegex("(?P<first>first) (?P<second>second)");
        using (var captureCursor11 = regex.CreateMatchCursor(("first second"u8).ToArray(), mode: Utf8PythonMatchMode.Match))
        {
            captureCursor11.MoveNext(out match);
        }
        Assert.True(match.Success);
        Assert.True(regex.TryGetGroupNumber("first"u8, out var first));
        Assert.Equal(1, first);
        Assert.Equal("first", GetGroup(match, first));
        Assert.True(regex.TryGetGroupNumber("second"u8, out var second));
        Assert.Equal(2, second);
        Assert.Equal("second", GetGroup(match, second));
    }

    [Theory]
    [InlineData(@"a++", "a")]
    [InlineData(@"a*+", "a")]
    [InlineData(@"a?+", "a")]
    [InlineData(@"a{1,3}+", "a")]
    [InlineData(@"a++b", "ab")]
    [InlineData(@"a*+b", "ab")]
    [InlineData(@"a?+b", "ab")]
    [InlineData(@"a{1,3}+b", "ab")]
    [InlineData(@"(?:ab)++", "ab")]
    [InlineData(@"(?:ab)*+", "ab")]
    [InlineData(@"(?:ab)?+", "ab")]
    [InlineData(@"(?:ab){1,3}+", "ab")]
    [InlineData(@"(?:ab)++c", "abc")]
    [InlineData(@"(?:ab)*+c", "abc")]
    [InlineData(@"(?:ab)?+c", "abc")]
    [InlineData(@"(?:ab){1,3}+c", "abc")]
    public void FullMatchPossessiveQuantifiersFollowCpythonPositiveExamples(string pattern, string input)
    {
        Assert.True(new Utf8PythonRegex(pattern).Match(System.Text.Encoding.UTF8.GetBytes(input), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(@"a++", "ab")]
    [InlineData(@"a*+", "ab")]
    [InlineData(@"a?+", "ab")]
    [InlineData(@"a{1,3}+", "ab")]
    [InlineData(@"(?:ab)++", "abc")]
    [InlineData(@"(?:ab)*+", "abc")]
    [InlineData(@"(?:ab)?+", "abc")]
    [InlineData(@"(?:ab){1,3}+", "abc")]
    public void FullMatchPossessiveQuantifiersFollowCpythonNegativeExamples(string pattern, string input)
    {
        Assert.False(new Utf8PythonRegex(pattern).Match(System.Text.Encoding.UTF8.GetBytes(input), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(@"a++", "aab", "aa")]
    [InlineData(@"a*+", "aab", "aa||")]
    [InlineData(@"a?+", "aab", "a|a||")]
    [InlineData(@"a{1,3}+", "aab", "aa")]
    [InlineData(@"(?:ab)++", "ababc", "abab")]
    [InlineData(@"(?:ab)*+", "ababc", "abab||")]
    [InlineData(@"(?:ab)?+", "ababc", "ab|ab||")]
    [InlineData(@"(?:ab){1,3}+", "ababc", "abab")]
    public void FindAllPossessiveQuantifiersFollowCpythonExamples(string pattern, string input, string expectedPipeJoined)
    {
        var wholeMatches14 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor14 = new Utf8PythonRegex(pattern).CreateMatchCursor(System.Text.Encoding.UTF8.GetBytes(input), 0, default))
        {
            while (rangeCursor14.MoveNext(out var rangeMatch14))
            {
                wholeMatches14.Add(rangeMatch14);
            }
        }
        var actual = JoinValues(wholeMatches14.ToArray());
        Assert.Equal(expectedPipeJoined, actual);
    }

    [Theory]
    [InlineData(@"(?>a+)", "a")]
    [InlineData(@"(?>a*)", "a")]
    [InlineData(@"(?>a?)", "a")]
    [InlineData(@"(?>a{1,3})", "a")]
    [InlineData(@"(?>a+)b", "ab")]
    [InlineData(@"(?>a*)b", "ab")]
    [InlineData(@"(?>a?)b", "ab")]
    [InlineData(@"(?>a{1,3})b", "ab")]
    [InlineData(@"(?>(?:ab)+)", "ab")]
    [InlineData(@"(?>(?:ab)*)", "ab")]
    [InlineData(@"(?>(?:ab)?)", "ab")]
    [InlineData(@"(?>(?:ab){1,3})", "ab")]
    [InlineData(@"(?>(?:ab)+)c", "abc")]
    [InlineData(@"(?>(?:ab)*)c", "abc")]
    [InlineData(@"(?>(?:ab)?)c", "abc")]
    [InlineData(@"(?>(?:ab){1,3})c", "abc")]
    public void FullMatchAtomicGroupingFollowsCpythonPositiveExamples(string pattern, string input)
    {
        Assert.True(new Utf8PythonRegex(pattern).Match(System.Text.Encoding.UTF8.GetBytes(input), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(@"(?>a+)", "ab")]
    [InlineData(@"(?>a*)", "ab")]
    [InlineData(@"(?>a?)", "ab")]
    [InlineData(@"(?>a{1,3})", "ab")]
    [InlineData(@"(?>(?:ab)+)", "abc")]
    [InlineData(@"(?>(?:ab)*)", "abc")]
    [InlineData(@"(?>(?:ab)?)", "abc")]
    [InlineData(@"(?>(?:ab){1,3})", "abc")]
    public void FullMatchAtomicGroupingFollowsCpythonNegativeExamples(string pattern, string input)
    {
        Assert.False(new Utf8PythonRegex(pattern).Match(System.Text.Encoding.UTF8.GetBytes(input), mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Theory]
    [InlineData(@"(?>a+)", "aab", "aa")]
    [InlineData(@"(?>a*)", "aab", "aa||")]
    [InlineData(@"(?>a?)", "aab", "a|a||")]
    [InlineData(@"(?>a{1,3})", "aab", "aa")]
    [InlineData(@"(?>(?:ab)+)", "ababc", "abab")]
    [InlineData(@"(?>(?:ab)*)", "ababc", "abab||")]
    [InlineData(@"(?>(?:ab)?)", "ababc", "ab|ab||")]
    [InlineData(@"(?>(?:ab){1,3})", "ababc", "abab")]
    public void FindAllAtomicGroupingFollowsCpythonExamples(string pattern, string input, string expectedPipeJoined)
    {
        var wholeMatches15 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor15 = new Utf8PythonRegex(pattern).CreateMatchCursor(System.Text.Encoding.UTF8.GetBytes(input), 0, default))
        {
            while (rangeCursor15.MoveNext(out var rangeMatch15))
            {
                wholeMatches15.Add(rangeMatch15);
            }
        }
        var actual = JoinValues(wholeMatches15.ToArray());
        Assert.Equal(expectedPipeJoined, actual);
    }

    [Fact]
    public void ZeroWidthBoundaryReplaceFollowsCpythonExamples()
    {
        Assert.Equal("-a-::-bc-", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"\b").Replace("a::bc"u8, "-"u8)));
    }

    [Fact]
    public void ZeroWidthAlternationFindAllAndReplaceFollowCpythonExamples()
    {
        var wholeMatches16 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor16 = new Utf8PythonRegex(@"\b|:+").CreateMatchCursor(("a::bc"u8).ToArray(), 0, default))
        {
            while (rangeCursor16.MoveNext(out var rangeMatch16))
            {
                wholeMatches16.Add(rangeMatch16);
            }
        }
        Assert.Equal("||::||", JoinValues(wholeMatches16.ToArray()));
        var wholeMatches17 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor17 = new Utf8PythonRegex(@"\b|\w+").CreateMatchCursor(("a::bc"u8).ToArray(), 0, default))
        {
            while (rangeCursor17.MoveNext(out var rangeMatch17))
            {
                wholeMatches17.Add(rangeMatch17);
            }
        }
        Assert.Equal("|a|||bc|", JoinValues(wholeMatches17.ToArray()));
        Assert.Equal("-a---bc-", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"\b|:+").Replace("a::bc"u8, "-"u8)));
        Assert.Equal("[]a[][::][]bc[]", System.Text.Encoding.UTF8.GetString(new Utf8PythonRegex(@"(\b|:+)").Replace("a::bc"u8, @"[\1]"u8)));
    }

    private static (int Start, int End) GetSpan(Utf8PythonValueMatch match)
    {
        Assert.True(match.Success);
        return (match.StartOffsetInBytes, match.EndOffsetInBytes);
    }

    private static string GetGroup1(Utf8PythonRangeMatch match)
    {
        Assert.True(match.TryGetGroup(1, out var group));
        Assert.True(group.Success);
        return System.Text.Encoding.UTF8.GetString((group.Success ? match.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty));
    }

    private static string GetGroup(Utf8PythonRangeMatch match, int number)
    {
        Assert.True(match.TryGetGroup(number, out var group));
        Assert.True(group.Success);
        return System.Text.Encoding.UTF8.GetString((group.Success ? match.GetGroupMemory(group.Number).Span : ReadOnlySpan<byte>.Empty));
    }

    private static string JoinValues(Utf8PythonRangeMatch[] matches)
        => string.Join('|', matches.Select(x => System.Text.Encoding.UTF8.GetString(x.GetValueMemory().Span)));

    private static void AssertSplit(string?[] expected, string?[] actual)
        => Assert.Equal(expected, actual, StringComparer.Ordinal);

    private static void AssertPatternError(string pattern, string messageContains, int? position = null)
    {
        var ex = Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));
        Assert.Contains(messageContains, ex.Message, StringComparison.Ordinal);
        if (position.HasValue)
        {
            Assert.Equal(position.Value, ex.Position);
        }
    }

    private static void AssertTemplateError(string pattern, string replacement, string input, string messageContains)
    {
        var regex = new Utf8PythonRegex(pattern);
        var ex = Assert.Throws<PythonRePatternException>(() => System.Text.Encoding.UTF8.GetString(regex.Replace(System.Text.Encoding.UTF8.GetBytes(input), System.Text.Encoding.UTF8.GetBytes(replacement))));
        Assert.Contains(messageContains, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RangedDivergentRepeatFollowsOrderedSpans()
    {
        // R05: ranged divergent counts resolve ordered first-success spans,
        // unlike the longest-match defect class on the other engines; the
        // oracle values below match CPython backtracking order.
        Assert.Equal((1, 3), GetSpan(new Utf8PythonRegex("(?:a|ab){2,3}").Match("xaabab"u8, mode: Utf8PythonMatchMode.Search)));
        Assert.Equal((1, 3), GetSpan(new Utf8PythonRegex("(?:a|ab){2,3}").Match(System.Text.Encoding.UTF8.GetBytes("xaabab\U0001F600"), mode: Utf8PythonMatchMode.Search)));
        Assert.Equal((1, 7), GetSpan(new Utf8PythonRegex("(?:a|ab){2,3}c").Match("xaababc"u8, mode: Utf8PythonMatchMode.Search)));
        Assert.Equal((1, 3), GetSpan(new Utf8PythonRegex("(?:a|ab){2}").Match("xaab"u8, mode: Utf8PythonMatchMode.Search)));
        var wholeMatches18 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor18 = new Utf8PythonRegex("(?:a|ab){2,3}").CreateMatchCursor(System.Text.Encoding.UTF8.GetBytes("xaabab"), 0, default))
        {
            while (rangeCursor18.MoveNext(out var rangeMatch18))
            {
                wholeMatches18.Add(rangeMatch18);
            }
        }
        Assert.Equal(new[] { "aa" }, wholeMatches18.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        var wholeMatches19 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor19 = new Utf8PythonRegex("(?:a|ab){2,3}").CreateMatchCursor(System.Text.Encoding.UTF8.GetBytes("xaabab\U0001F600"), 0, default))
        {
            while (rangeCursor19.MoveNext(out var rangeMatch19))
            {
                wholeMatches19.Add(rangeMatch19);
            }
        }
        Assert.Equal(new[] { "aa" }, wholeMatches19.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        var wholeMatches20 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor20 = new Utf8PythonRegex("(?:a|ab){2,3}c").CreateMatchCursor(System.Text.Encoding.UTF8.GetBytes("xaababc"), 0, default))
        {
            while (rangeCursor20.MoveNext(out var rangeMatch20))
            {
                wholeMatches20.Add(rangeMatch20);
            }
        }
        Assert.Equal(new[] { "aababc" }, wholeMatches20.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
        var wholeMatches21 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor21 = new Utf8PythonRegex("(?:a|ab){2}").CreateMatchCursor(System.Text.Encoding.UTF8.GetBytes("xaab"), 0, default))
        {
            while (rangeCursor21.MoveNext(out var rangeMatch21))
            {
                wholeMatches21.Add(rangeMatch21);
            }
        }
        Assert.Equal(new[] { "aa" }, wholeMatches21.ToArray().Select(static match => System.Text.Encoding.UTF8.GetString(match.GetValueMemory().Span)).ToArray());
    }
}
