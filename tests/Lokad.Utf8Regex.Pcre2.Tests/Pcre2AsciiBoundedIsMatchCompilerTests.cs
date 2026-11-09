using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2AsciiBoundedIsMatchCompilerTests
{
    private const string CreditCardPattern = @"(\d{4}[- ]){3}\d{3,4}";

    [Theory]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "11/18/2019", true)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "11/18/201A", false)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "x11/18/20", false)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "11/18/20_", false)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "111/18/20", false)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "11/18/20199", false)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "é11/18/20é", true)]
    [InlineData(@"\b\d{1,2}/\d{1,2}/\d{2,4}\b", "😀1/2/34😀", true)]
    [InlineData(@"^\b\d{1,2}/\d{1,2}/\d{2,4}\b$", "1/2/34\n", true)]
    [InlineData(@"^\b\d{1,2}/\d{1,2}/\d{2,4}\b$", " 1/2/34", false)]
    public void FiniteWordBoundaryLanguagesPreserveIndependentBooleanResults(string pattern, string text, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected, CreateMetered(pattern, 1_000_000).IsMatch(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.IsMatch(input, 0, default, token.Token));
    }

    [Theory]
    [InlineData(@"\ba!\b")]
    [InlineData(@"\b!a\b")]
    [InlineData(@"\b(a|!)\b")]
    [InlineData(@"\ba?\b")]
    [InlineData(@"\ba\bb\b")]
    [InlineData(@"\Ba\b")]
    public void UnprovedBoundaryEndpointsKeepTheirExistingExecutors(string pattern)
        => Assert.IsNotType<Pcre2AsciiBoundedIsMatchDirectProgram>(
            new Utf8Pcre2Regex(pattern).DebugCompiledProgram.Operations.IsMatch);

    [Fact]
    public void FiniteBoundariesKeepSubjectContextCapturesValidationAndAuthority()
    {
        const string pattern = @"\b(\d{1,2})/(\d{1,2})/(\d{2,4})\b";
        var regex = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern, 1_000_000);
        var input = Encoding.UTF8.GetBytes("😀11/18/2019 1/2/34é");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), regex.IsMatch(input, start));
            Assert.Equal(metered.IsMatch(input, start, Pcre2MatchOptions.Anchored),
                regex.IsMatch(input, start, Pcre2MatchOptions.Anchored));
        }
        var retained = regex.MatchDetailed(input);
        Assert.Equal("11", retained.GetGroup(1).GetValueString());
        Assert.Equal("18", retained.GetGroup(2).GetValueString());
        Assert.Equal("2019", retained.GetGroup(3).GetValueString());
        Assert.Equal(2, regex.Count(input));
        Assert.Throws<ArgumentException>(() => regex.IsMatch([.. "1/2/34"u8, 0xff]));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(input, 0, default, canceled.Token)).CancellationToken);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => CreateMetered(pattern, 1).IsMatch(input)).ErrorKind);
        Assert.IsNotType<Pcre2AsciiBoundedIsMatchDirectProgram>(
            new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Ucp).DebugCompiledProgram.Operations.IsMatch);
    }

    [Theory]
    [InlineData("^(?(?=(a))abc|def)", "abc", true)]
    [InlineData("^(?(?=(a))abc|def)", "def", true)]
    [InlineData("^(?(?=(a))abc|def)", "abd", false)]
    [InlineData("^(?(?!(a))def|abc)", "def", true)]
    [InlineData("^(?(?!(a))def|abc)", "abc", true)]
    [InlineData("^(?(?!(a))def|abc)", "dabc", false)]
    [InlineData("^(?(?!(a)(*ACCEPT))def|abc)", "def", true)]
    [InlineData("^(?(?!(a)(*ACCEPT))def|abc)", "abc", true)]
    [InlineData("^(?(?!(a)(*ACCEPT))def|abc)", "a", false)]
    [InlineData("^(?(?=(a)(*ACCEPT))abc|def)", "abc", true)]
    [InlineData("^(?(?=(a)(*ACCEPT))abc|def)", "def", true)]
    [InlineData("^(?(?=(a)(*ACCEPT))abc|def)", "a", false)]
    [InlineData("^(?(?!(a(*ACCEPT)))def|abc)", "abc", true)]
    [InlineData("^(?(?=[ab](*ACCEPT))[b-d]|[ab])$", "a", false)]
    [InlineData("^(?(?=[ab](*ACCEPT))[b-d]|[ab])$", "b", true)]
    [InlineData("^(?(?=[ab])[b-d]|[ab])$", "a", false)]
    [InlineData("^(?(?=[ab])[b-d]|[ab])$", "b", true)]
    [InlineData("^(?(?=[ab])[b-d]|[ab])$", "c", false)]
    [InlineData("^(?(?!a)[ab]|[ab])$", "a", true)]
    [InlineData("^(?(?!a)[ab]|[ab])$", "b", true)]
    [InlineData("^(?(?!a)[ab]|[ab])$", "c", false)]
    [InlineData("^x(?(?=([ab]))[a-c]|[a-c])y$", "xcy", true)]
    [InlineData("^(?(?=a)(?(?!b)a|b)|c)$", "a", true)]
    public void SingleTokenAssertionConditionalsFilterTheExistingFiniteLanguage(string pattern, string text, bool expected)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, direct.IsMatch(input));
        Assert.Equal(expected, CreateMetered(pattern, 1_000_000).IsMatch(input));
    }

    [Fact]
    public void ConditionalBooleanProofKeepsScalarFloorsCapturesValidationAndRefusal()
    {
        const string pattern = "(?(?=(a))(a)b|(c)d)";
        var direct = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern, 1_000_000);
        var input = Encoding.UTF8.GetBytes("😀ab écd");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), direct.IsMatch(input, start));
            Assert.Equal(metered.IsMatch(input, start, Pcre2MatchOptions.Anchored), direct.IsMatch(input, start, Pcre2MatchOptions.Anchored));
        }
        Assert.Equal(2, direct.Count(input));
        var retained = direct.MatchDetailed(input);
        Assert.Equal("ab", retained.GetValueString());
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Equal("a", retained.GetGroup(2).GetValueString());
        Assert.False(retained.GetGroup(3).Success);
        Assert.True(direct.IsMatch("cd"u8));
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Throws<ArgumentException>(() => direct.IsMatch([.. "ab"u8, 0xff]));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => direct.IsMatch("ab"u8, 0, default, canceled.Token)).CancellationToken);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => CreateMetered(pattern, 1).IsMatch("ab"u8)).ErrorKind);
    }

    [Theory]
    [InlineData("^(?(?!(a)(*ACCEPT:named))def|abc)")]
    [InlineData("^(?(?!(a)(*COMMIT))def|abc)")]
    [InlineData("^(?(?!(a)(*ACCEPT)b)def|abc)")]
    [InlineData("^(?(?=ab)abc|def)")]
    [InlineData("(?(?<=a)b|c)")]
    [InlineData("^(?(?=a)a|)$")]
    [InlineData("^(?(?=a)a?|b)$")]
    [InlineData("^(?(?=(é))é|a)$")]
    [InlineData(@"^(?(?=(a))a\1|b)$")]
    public void UnprovenConditionalLanguagesKeepTheirOwnedVm(string pattern)
        => Assert.IsNotType<Pcre2AsciiBoundedIsMatchDirectProgram>(
            new Utf8Pcre2Regex(pattern).DebugCompiledProgram.Operations.IsMatch);

    [Theory]
    [InlineData("(?(?!(a)(*ACCEPT))def|abc)")]
    [InlineData("(?(?=(a)(*ACCEPT))abc|def)")]
    [InlineData("(?(?!(a(*ACCEPT)))def|abc)")]
    public void AcceptGuardBooleanProofPreservesCallerCapturesAndAuthority(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern, 1_000_000);
        var input = Encoding.UTF8.GetBytes("😀abc édef");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), regex.IsMatch(input, start));
        }
        var retained = regex.MatchDetailed(input);
        Assert.Equal("abc", retained.GetValueString());
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Equal(4, retained.GetGroup(1).StartOffsetInBytes);
        Assert.True(regex.IsMatch("def"u8));
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Throws<ArgumentException>(() => regex.IsMatch([.. "abc"u8, 0xff]));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("abc"u8, 0, default, canceled.Token)).CancellationToken);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => CreateMetered(pattern, 1).IsMatch("abc"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(@"^(?1)\d{3}(a)", "a123a", true)]
    [InlineData(@"^(?1)\d{3}(a)", "b123a", false)]
    [InlineData(@"^(?1)\d{3}([a-c])$", "b123c", true)]
    [InlineData(@"^(?&x)\d{3}(?<x>a)$", "a123a", true)]
    [InlineData(@"^(?+1)\d{3}(a)$", "a123a", true)]
    [InlineData(@"^(a)(?-1)$", "aa", true)]
    [InlineData(@"^(?<x>a)(?&x)$", "aa", true)]
    [InlineData(@"^(?1)-(\d)$", "1-2", true)]
    [InlineData(@"^(?1)-(\d)$", "١-2", false)]
    public void SingleAsciiTokenCallsUseTheExistingBooleanPlan(string pattern, string text, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(regex.DebugCompiledProgram.Operations.IsMatch);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.IsMatch(input, 0, default, token.Token));
        Assert.Equal(expected, CreateMetered(pattern, 1_000_000).IsMatch(input));
    }

    [Fact]
    public void SingleTokenCallsKeepScalarOriginsOptionsAndCallerCaptures()
    {
        const string pattern = @"(?1)\d{3}(a)";
        var regex = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern, 1_000_000);
        var input = Encoding.UTF8.GetBytes("😀a123a éa345a");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), regex.IsMatch(input, start));
            Assert.Equal(metered.IsMatch(input, start, Pcre2MatchOptions.Anchored),
                regex.IsMatch(input, start, Pcre2MatchOptions.Anchored));
        }
        Assert.Equal(2, regex.Count(input));
        var retained = regex.MatchDetailed(input);
        Assert.Equal("a123a", retained.GetValueString());
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
        Assert.Equal(8, retained.GetGroup(1).StartOffsetInBytes);
        Assert.True(regex.IsMatch("a456a"u8));
        Assert.Equal("a", retained.GetGroup(1).GetValueString());
    }

    [Fact]
    public void SingleTokenCallsKeepWholeInputValidationAndOriginalRefusal()
    {
        const string pattern = @"^(?1)\d{3}(a)";
        var regex = new Utf8Pcre2Regex(pattern);
        Assert.Throws<ArgumentException>(() => regex.IsMatch([.. "a123a"u8, 0xff]));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("a123a"u8, 0, default, canceled.Token)).CancellationToken);
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => CreateMetered(pattern, 1).IsMatch("a123a"u8)).ErrorKind);
    }

    [Theory]
    [InlineData(@"^(a|ab)(?1)$")]
    [InlineData(@"^()(?1)$")]
    [InlineData(@"^(😀)(?1)$")]
    [InlineData(@"^([^a])(?1)$")]
    [InlineData(@"^(?i:(a))(?1)$")]
    [InlineData(@"^(a)(?1)\1$")]
    [InlineData(@"^(a)(?1)(?(1)b|c)$")]
    [InlineData(@"^(a(?1)?)$")]
    public void NonTokenOrCaptureDependentCallsKeepTheirOwnedVm(string pattern)
        => Assert.IsNotType<Pcre2AsciiBoundedIsMatchDirectProgram>(
            new Utf8Pcre2Regex(pattern).DebugCompiledProgram.Operations.IsMatch);

    [Fact]
    public void BoundedAsciiLanguageUsesAnIsMatchOnlyDirectPlan()
    {
        var regex = new Utf8Pcre2Regex(CreditCardPattern);

        Assert.Equal(
            "IsMatch=Pcre2AsciiBoundedIsMatch, Count=Pcre2Backtracking, Enumerate=Pcre2Backtracking, Match=Pcre2Backtracking, Replace=Pcre2Backtracking",
            regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch("4111-1111-1111-111"u8));
        Assert.True(regex.IsMatch("4111 1111 1111 1111"u8));
        Assert.False(regex.IsMatch("4111-1111-1111-11"u8));
        Assert.False(regex.IsMatch("٤١١١-1111-1111-1111"u8));

        var detailed = regex.MatchDetailed("card 4111-1111-1111-1111 done"u8);
        Assert.True(detailed.Success);
        Assert.Equal("4111-1111-1111-1111", detailed.GetValueString());
        Assert.Equal("1111-", detailed.GetGroup(1).GetValueString());
    }

    [Fact]
    public void BoundedAsciiLanguageMatchesTheMeteredVmAtEveryValidStart()
    {
        var direct = new Utf8Pcre2Regex(CreditCardPattern);
        var vm = CreateMetered(CreditCardPattern, 1_000_000);
        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(vm.DebugCompiledProgram.Operations.IsMatch);

        foreach (var inputText in new[]
                 {
                     "",
                     "4111-1111-1111-111",
                     "4111-1111-1111-1111",
                     "x4111 1111-1111 1111y",
                     "4111-1111-1111-11",
                     "٤١١١-1111-1111-1111",
                     "é4111-1111-1111-1111🙂",
                     "4111-1111-1111-11 then 5555-5555-5555-5555",
                 })
        {
            var input = Encoding.UTF8.GetBytes(inputText);
            Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));

            for (var start = 0; start <= input.Length; start++)
            {
                if (start < input.Length && (input[start] & 0xC0) == 0x80)
                {
                    continue;
                }

                Assert.Equal(vm.IsMatch(input, start), direct.IsMatch(input, start));
            }
        }
    }

    [Theory]
    [InlineData(@"\d{4}-\d{2}-\d{2}", "on 2026-08-25", true)]
    [InlineData(@"\d{4}-\d{2}-\d{2}", "on ٢٠٢٦-08-25", false)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "25/8/2026", true)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "on 25/8/2026", false)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "25/8/2026\n", true)]
    [InlineData(@"^\d{1,2}/\d{1,2}/\d{4}$", "٢٥/8/2026", false)]
    [InlineData(@"(?:\d{2}|[A-Z]{2})-[a-z]{2}", "code AB-xy", true)]
    [InlineData(@"(?:\d{2}|[A-Z]{2})-[a-z]{2}", "code A1-xy", false)]
    public void BoundedAsciiLanguageGeneralizesAcrossFiniteShapes(
        string pattern,
        string inputText,
        bool expected)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var vm = CreateMetered(pattern, 1_000_000);
        var input = Encoding.UTF8.GetBytes(inputText);

        Assert.IsType<Pcre2AsciiBoundedIsMatchDirectProgram>(direct.DebugCompiledProgram.Operations.IsMatch);
        Assert.Equal(expected, direct.IsMatch(input));
        Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));
    }

    [Fact]
    public void BoundedAsciiLanguageFallsBackForMatchOptionsAndLimits()
    {
        var direct = new Utf8Pcre2Regex(CreditCardPattern);
        var vm = CreateMetered(CreditCardPattern, 1_000_000);
        var input = "card 4111-1111-1111-1111"u8.ToArray();

        foreach (var options in new[]
                 {
                     Pcre2MatchOptions.Anchored,
                     Pcre2MatchOptions.EndAnchored,
                     Pcre2MatchOptions.NotBol,
                     Pcre2MatchOptions.NotEol,
                     Pcre2MatchOptions.NotEmpty,
                     Pcre2MatchOptions.NotEmptyAtStart,
                 })
        {
            Assert.Equal(vm.IsMatch(input, 0, options), direct.IsMatch(input, 0, options));
        }

        var tightlyMetered = CreateMetered(CreditCardPattern, 1);
        Assert.Equal(
            Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => tightlyMetered.IsMatch(input)).ErrorKind);
        Assert.Throws<ArgumentException>(() => direct.IsMatch([0xFF]));
    }

    [Fact]
    public void BoundedAsciiLanguageAnalyzerRejectsUnprovenLanguages()
    {
        foreach (var regex in new[]
                 {
                     new Utf8Pcre2Regex(@"\d+"),
                     new Utf8Pcre2Regex(@"[^0]{2}"),
                     new Utf8Pcre2Regex(@".{2}"),
                     new Utf8Pcre2Regex(@"é{2}"),
                     new Utf8Pcre2Regex(@"(?i:a){2}"),
                     new Utf8Pcre2Regex(@"(?:a|b|c|d|e){4}[0-9]"),
                     new Utf8Pcre2Regex(@"[a]{129}[b]"),
                     new Utf8Pcre2Regex(@"(a)\1"),
                     new Utf8Pcre2Regex(@"\A\d{4}-\d{2}-\d{2}\z"),
                     new Utf8Pcre2Regex(CreditCardPattern, Pcre2CompileOptions.Ucp),
                     new Utf8Pcre2Regex(CreditCardPattern, Pcre2CompileOptions.Caseless),
                     new Utf8Pcre2Regex(
                         CreditCardPattern,
                         Pcre2CompileOptions.None,
                         new Utf8Pcre2CompileSettings { Bsr = Pcre2BsrConvention.AnyCrlf },
                         default(Utf8Pcre2ExecutionLimits),
                         Regex.InfiniteMatchTimeout),
                 })
        {
            Assert.IsNotType<Pcre2AsciiBoundedIsMatchDirectProgram>(
                regex.DebugCompiledProgram.Operations.IsMatch);
        }
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern, uint matchLimit) =>
        new(
            pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = matchLimit },
            Regex.InfiniteMatchTimeout);
}
