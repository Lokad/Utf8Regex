using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class Utf8PythonRegexCpythonParityTests
{
    [Fact]
    public void SearchAndMatchFollowCpythonStarPlusSemantics()
    {
        Assert.Equal((0, 0), GetSpan(new Utf8PythonRegex("x*").Search("axx"u8)));
        Assert.Equal((1, 3), GetSpan(new Utf8PythonRegex("x+").Search("axx"u8)));
        Assert.False(new Utf8PythonRegex("x").Search("aaa"u8).Success);

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
        Assert.Equal((0, 1), GetSpan(new Utf8PythonRegex("a").FullMatch("a"u8)));
        Assert.Equal((0, 2), GetSpan(new Utf8PythonRegex("a|ab").FullMatch("ab"u8)));
        Assert.Equal((0, 3), GetSpan(new Utf8PythonRegex(".*?").FullMatch("abc"u8)));
        Assert.Equal((0, 4), GetSpan(new Utf8PythonRegex("ab(?=c)cd").FullMatch("abcd"u8)));
        Assert.Equal((0, 4), GetSpan(new Utf8PythonRegex("ab(?<=b)cd").FullMatch("abcd"u8)));
        Assert.False(new Utf8PythonRegex("abc$").FullMatch("abc\n"u8).Success);
        Assert.False(new Utf8PythonRegex("(?m)abc$").FullMatch("abc\n"u8).Success);
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
        Assert.True(new Utf8PythonRegex("(?x) a").FullMatch("a"u8).Success);
        Assert.True(new Utf8PythonRegex(" a(?x: b) c").FullMatch(" ab c"u8).Success);
        Assert.True(new Utf8PythonRegex(" a(?-x: b) c", PythonReCompileOptions.Verbose).FullMatch("a bc"u8).Success);
        Assert.True(new Utf8PythonRegex("#x\na", PythonReCompileOptions.Verbose).FullMatch("a"u8).Success);
        Assert.True(new Utf8PythonRegex("(?x)#x\na").FullMatch("a"u8).Success);
        Assert.True(new Utf8PythonRegex("(?x)#x\na|#y\nb").FullMatch("a"u8).Success);
        Assert.True(new Utf8PythonRegex("(?x)#x\na|#y\nb").FullMatch("b"u8).Success);
    }

    [Fact]
    public void ReplacementTemplatesSupportCpythonGroupAndEscapeExamples()
    {
        Assert.Equal("x x", new Utf8PythonRegex("(?i)b+", PythonReCompileOptions.IgnoreCase).ReplaceToString("bbbb BBBB"u8, "x"));
        Assert.Equal("xxxx", new Utf8PythonRegex("(?P<a>x)").ReplaceToString("xx"u8, @"\g<a>\g<a>"));
        Assert.Equal("xxxx", new Utf8PythonRegex("(?P<a>x)").ReplaceToString("xx"u8, @"\g<a>\g<1>"));
        Assert.Equal("xxxx", new Utf8PythonRegex("()x").ReplaceToString("xx"u8, @"\g<0>\g<0>"));
        Assert.Equal("<>b<>", new Utf8PythonRegex("(a)?").ReplaceToString("b"u8, @"<\1>"));
        Assert.Equal("<>b<>", new Utf8PythonRegex("(a)?").ReplaceToString("b"u8, @"<\g<1>>"));
        Assert.Equal("\t\n\v\r\f\a\b", new Utf8PythonRegex("a").ReplaceToString("a"u8, @"\t\n\v\r\f\a\b"));
        Assert.Equal("xx\bxx\b", new Utf8PythonRegex("(?P<unk>x)").ReplaceToString("xx"u8, @"\g<1>\g<1>\b"));
        Assert.Equal("hello there", new Utf8PythonRegex(@"(\S)\s+(\S)").ReplaceToString("hello  there"u8, @"\1 \2"));
    }

    [Fact]
    public void ReplacementNumericEscapesFollowCpythonExamples()
    {
        var regex = new Utf8PythonRegex("x");

        Assert.Equal("\0", regex.ReplaceToString("x"u8, @"\0"));
        Assert.Equal("\0", regex.ReplaceToString("x"u8, @"\000"));
        Assert.Equal("\0" + "8", regex.ReplaceToString("x"u8, @"\008"));
        Assert.Equal("\0" + "9", regex.ReplaceToString("x"u8, @"\009"));
        Assert.Equal("\x49", regex.ReplaceToString("x"u8, @"\111"));
        Assert.Equal("\x4F", regex.ReplaceToString("x"u8, @"\117"));
        Assert.Equal("\xFF", regex.ReplaceToString("x"u8, @"\377"));
        Assert.Equal("\x49" + "1", regex.ReplaceToString("x"u8, @"\1111"));
        Assert.Equal("\x07", regex.ReplaceToString("x"u8, @"\07"));
        Assert.Equal("\0" + "a", regex.ReplaceToString("x"u8, @"\0a"));
        Assert.Equal("x", regex.ReplaceToString("x"u8, @"\g<00>"));
        Assert.Equal("x", regex.ReplaceToString("x"u8, @"\g<000>"));

        Assert.Equal("x", new Utf8PythonRegex("(((((((((((x)))))))))))").ReplaceToString("x"u8, @"\11"));
        Assert.Equal("x", new Utf8PythonRegex("(x)").ReplaceToString("x"u8, @"\g<01>"));
        Assert.Equal("x", new Utf8PythonRegex("(x)").ReplaceToString("x"u8, @"\g<001>"));
        Assert.Equal("xz8", new Utf8PythonRegex("((((((((((y))))))))))(.)").ReplaceToString("xyz"u8, @"\118"));
        Assert.Equal("xza", new Utf8PythonRegex("((((((((((y))))))))))(.)").ReplaceToString("xyz"u8, @"\11a"));
    }

    [Fact]
    public void SubnFollowsCpythonExamples()
    {
        var first = new Utf8PythonRegex("(?i)b+", PythonReCompileOptions.IgnoreCase).SubnToString("bbbb BBBB"u8, "x");
        var second = new Utf8PythonRegex("b+").SubnToString("bbbb BBBB"u8, "x");
        var third = new Utf8PythonRegex("b+").SubnToString("xyz"u8, "x");
        var fourth = new Utf8PythonRegex("b*").SubnToString("xyz"u8, "x");
        var limited = new Utf8PythonRegex("b*").SubnToString("xyz"u8, "x", count: 2);
        var emptyTwo = new Utf8PythonRegex("").SubnToString("ab"u8, "-", count: 2);
        var emptyThree = new Utf8PythonRegex("").SubnToString("ab"u8, "-", count: 3);

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
        AssertSplit(["", "a", "b", "", "c"], new Utf8PythonRegex(":").SplitToStrings(":a:b::c"u8));
        AssertSplit(["", "a", "b", "c"], new Utf8PythonRegex(":+").SplitToStrings(":a:b::c"u8));
        AssertSplit(["", ":", "a", ":", "b", "::", "c"], new Utf8PythonRegex("(:+)").SplitToStrings(":a:b::c"u8));
        AssertSplit(["", "a", "b", "c"], new Utf8PythonRegex("(?::+)").SplitToStrings(":a:b::c"u8));
        AssertSplit(["", ":", "a", ":", "b", ":", "c"], new Utf8PythonRegex("(:)+").SplitToStrings(":a:b::c"u8));
        AssertSplit(["", null, ":", "a", null, ":", "", "b", null, "", null, "::", "c"], new Utf8PythonRegex("(b)|(:+)").SplitToStrings(":a:b::c"u8));
        AssertSplit(["", "a", "", "", "c"], new Utf8PythonRegex("(?:b)|(?::+)").SplitToStrings(":a:b::c"u8));
        AssertSplit(["", "a", "", "", "bc", ""], new Utf8PythonRegex(@"\b|:+").SplitToStrings("a::bc"u8));
        AssertSplit(["", ":a", ":b", ":", ":c"], new Utf8PythonRegex(@"(?=:)").SplitToStrings(":a:b::c"u8));
        AssertSplit([":", "a:", "b:", ":", "c"], new Utf8PythonRegex(@"(?<=:)").SplitToStrings(":a:b::c"u8));
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
        Assert.Throws<IndexOutOfRangeException>(() => new Utf8PythonRegex("(?P<a>x)").ReplaceToString("xx"u8, @"\g<ab>"));
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
        AssertPatternError(@"(?-","missing flag", 3);
        AssertPatternError(@"(?-+","missing flag", 3);
        AssertPatternError(@"(?-z","unknown flag", 3);
        AssertPatternError(@"(?-i","missing :", 4);
        AssertPatternError(@"(?-i)","missing :", 4);
        AssertPatternError(@"(?-i+","missing :", 4);
        AssertPatternError(@"(?-iz","unknown flag", 4);
        AssertPatternError(@"(?i:","missing ), unterminated subpattern", 0);
        AssertPatternError(@"(?i","missing -, : or )", 3);
        AssertPatternError(@"(?i+","missing -, : or )", 3);
        AssertPatternError(@"(?iz","unknown flag", 3);
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
        Assert.Equal("xx", new Utf8PythonRegex(@"(?P<µ>x)").ReplaceToString("xx"u8, @"\g<µ>"));
        Assert.Equal("xx", new Utf8PythonRegex(@"(?P<𝔘𝔫𝔦𝔠𝔬𝔡𝔢>x)").ReplaceToString("xx"u8, @"\g<𝔘𝔫𝔦𝔠𝔬𝔡𝔢>"));
    }

    [Fact]
    public void SpecialEscapesFollowCpythonExamples()
    {
        Assert.Equal("bx", GetGroup1(new Utf8PythonRegex(@"\b(b.)\b").SearchDetailed("abcd abc bcd bx"u8)));
        Assert.Equal("bx", GetGroup1(new Utf8PythonRegex(@"\B(b.)\B").SearchDetailed("abc bcd bc abxd"u8)));
        Assert.Equal("abc", new Utf8PythonRegex(@"^abc$", PythonReCompileOptions.Multiline).Search("\nabc\n"u8).GetValueString());
        Assert.Equal("abc", new Utf8PythonRegex(@"^\Aabc\z$", PythonReCompileOptions.Multiline).Search("abc"u8).GetValueString());
        Assert.False(new Utf8PythonRegex(@"^\Aabc\z$", PythonReCompileOptions.Multiline).Search("\nabc\n"u8).Success);
        Assert.Equal("1aa! a", new Utf8PythonRegex(@"\d\D\w\W\s\S").Search("1aa! a"u8).GetValueString());
    }

    [Fact]
    public void WordBoundarySemanticsFollowCpythonExamples()
    {
        Assert.Equal("abc", GetGroup1(new Utf8PythonRegex(@"\b(abc)\b").SearchDetailed("abc"u8)));
        Assert.True(new Utf8PythonRegex(@".\b").Match("a="u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b").Match("=a"u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b").Match("я="u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b").Match("=я"u8).Success);
    }

    [Fact]
    public void UnicodeInlineFlagIsAcceptedAsPythonNoOpForStrPatterns()
    {
        Assert.True(new Utf8PythonRegex(@"(?u)\w+").FullMatch("café"u8).Success);
        Assert.True(new Utf8PythonRegex(@"(?u:\w+)").FullMatch("café"u8).Success);
    }

    [Fact]
    public void AsciiModeChangesWordAndBoundarySemantics()
    {
        Assert.True(new Utf8PythonRegex(@"\b(abc)\b", PythonReCompileOptions.Ascii).Search("abc"u8).Success);
        Assert.False(new Utf8PythonRegex(@"\b(ьюя)\b", PythonReCompileOptions.Ascii).Search("ьюя"u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b", PythonReCompileOptions.Ascii).Match("a="u8).Success);
        Assert.True(new Utf8PythonRegex(@".\b", PythonReCompileOptions.Ascii).Match("=a"u8).Success);
        Assert.False(new Utf8PythonRegex(@".\b", PythonReCompileOptions.Ascii).Match("я="u8).Success);
        Assert.False(new Utf8PythonRegex(@".\b", PythonReCompileOptions.Ascii).Match("=я"u8).Success);
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
        Assert.Equal("c", GetGroup1(new Utf8PythonRegex(@"^(\w){3}$").MatchDetailed("abc"u8)));
        Assert.Equal("c", GetGroup1(new Utf8PythonRegex(@"^(\w){1,3}$").MatchDetailed("abc"u8)));
        Assert.True(new Utf8PythonRegex(@"^x{3}$").Match("xxx"u8).Success);
        Assert.True(new Utf8PythonRegex(@"^x{1,4}?$").Match("xxx"u8).Success);
        Assert.False(new Utf8PythonRegex(@"^x{1}$").Match("xxx"u8).Success);
    }

    [Fact]
    public void PossessiveQuantifiersFollowCpythonExamples()
    {
        Assert.False(new Utf8PythonRegex("e*+e").Match("eeee"u8).Success);
        Assert.Equal("eeea", new Utf8PythonRegex("e++a").Match("eeea"u8).GetValueString());
        Assert.Equal("ea", new Utf8PythonRegex("e?+a").Match("ea"u8).GetValueString());
        Assert.False(new Utf8PythonRegex("(.)++.").Match("ee"u8).Success);
        Assert.Equal((0, 0), GetSpan(new Utf8PythonRegex("x*+").Search("axx"u8)));
        Assert.Equal((1, 3), GetSpan(new Utf8PythonRegex("x++").Search("axx"u8)));
        Assert.Equal(4, new Utf8PythonRegex("a?+").FindAll("aab"u8).Length);
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
        Assert.True(new Utf8PythonRegex(@"(?>a+)b").FullMatch("ab"u8).Success);
        Assert.Equal(4, new Utf8PythonRegex(@"(?>(?:ab)?)").FindAll("ababc"u8).Length);
    }

    [Fact]
    public void PossessiveQuantifierCapturesFollowCpythonExamples()
    {
        Assert.Equal("ae", GetGroup1(new Utf8PythonRegex(@"(ae)*+a").MatchDetailed("aea"u8)));
        Assert.Equal("ae", GetGroup1(new Utf8PythonRegex(@"([ae][ae])?+a").MatchDetailed("aea"u8)));
        Assert.Equal(string.Empty, GetGroup1(new Utf8PythonRegex(@"(e?){2,4}+a").MatchDetailed("eeea"u8)));
        Assert.Equal(string.Empty, GetGroup1(new Utf8PythonRegex(@"()*+a").MatchDetailed("a"u8)));
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
        var match = new Utf8PythonRegex(@"((x)|y|z)*+").MatchDetailed("xyz"u8);
        Assert.True(match.Success);
        Assert.Equal("z", GetGroup(match, 1));
        Assert.Equal("x", GetGroup(match, 2));
    }

    [Fact]
    public void NamedCaptureMetadataFollowsCpythonExamples()
    {
        var match = new Utf8PythonRegex("(?P<first>first) (?P<second>second)").MatchDetailed("first second"u8);
        Assert.True(match.Success);
        Assert.Equal("first", GetNamedGroup(match, "first"));
        Assert.Equal("second", GetNamedGroup(match, "second"));
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
        Assert.True(new Utf8PythonRegex(pattern).FullMatch(System.Text.Encoding.UTF8.GetBytes(input)).Success);
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
        Assert.False(new Utf8PythonRegex(pattern).FullMatch(System.Text.Encoding.UTF8.GetBytes(input)).Success);
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
        var actual = JoinValues(new Utf8PythonRegex(pattern).FindAll(System.Text.Encoding.UTF8.GetBytes(input)));
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
        Assert.True(new Utf8PythonRegex(pattern).FullMatch(System.Text.Encoding.UTF8.GetBytes(input)).Success);
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
        Assert.False(new Utf8PythonRegex(pattern).FullMatch(System.Text.Encoding.UTF8.GetBytes(input)).Success);
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
        var actual = JoinValues(new Utf8PythonRegex(pattern).FindAll(System.Text.Encoding.UTF8.GetBytes(input)));
        Assert.Equal(expectedPipeJoined, actual);
    }

    [Fact]
    public void ZeroWidthBoundaryReplaceFollowsCpythonExamples()
    {
        Assert.Equal("-a-::-bc-", new Utf8PythonRegex(@"\b").ReplaceToString("a::bc"u8, "-"));
    }

    [Fact]
    public void ZeroWidthAlternationFindAllAndReplaceFollowCpythonExamples()
    {
        Assert.Equal("||::||", JoinValues(new Utf8PythonRegex(@"\b|:+").FindAll("a::bc"u8)));
        Assert.Equal("|a|||bc|", JoinValues(new Utf8PythonRegex(@"\b|\w+").FindAll("a::bc"u8)));
        Assert.Equal("-a---bc-", new Utf8PythonRegex(@"\b|:+").ReplaceToString("a::bc"u8, "-"));
        Assert.Equal("[]a[][::][]bc[]", new Utf8PythonRegex(@"(\b|:+)").ReplaceToString("a::bc"u8, @"[\1]"));
    }

    private static (int Start, int End) GetSpan(Utf8PythonValueMatch match)
    {
        Assert.True(match.Success);
        return (match.StartOffsetInUtf16, match.EndOffsetInUtf16);
    }

    private static string GetGroup1(Utf8PythonMatchContext match)
    {
        Assert.True(match.TryGetGroup(1, out var group));
        Assert.True(group.Success);
        return group.Value.GetValueString();
    }

    private static string GetGroup(Utf8PythonMatchContext match, int number)
    {
        Assert.True(match.TryGetGroup(number, out var group));
        Assert.True(group.Success);
        return group.Value.GetValueString();
    }

    private static string GetNamedGroup(Utf8PythonMatchContext match, string name)
    {
        Assert.True(match.TryGetFirstSetGroup(name, out var group));
        Assert.True(group.Success);
        return group.Value.GetValueString();
    }

    private static string JoinValues(Utf8PythonMatchData[] matches)
        => string.Join('|', matches.Select(x => x.ValueText));

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
        var ex = Assert.Throws<PythonRePatternException>(() => regex.ReplaceToString(System.Text.Encoding.UTF8.GetBytes(input), replacement));
        Assert.Contains(messageContains, ex.Message, StringComparison.Ordinal);
    }
}
