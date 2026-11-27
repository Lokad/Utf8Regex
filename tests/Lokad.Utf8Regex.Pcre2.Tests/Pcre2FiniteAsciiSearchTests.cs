using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2FiniteAsciiSearchTests
{
    private const string CreditCardPattern = @"(\d{4}[- ]){3}\d{3,4}";

    [Fact]
    public void OptionalBooleanEntryKeepsOffsetsOptionsAndInterruptionAuthoritative()
    {
        const string pattern = "(?|(abc)|(xyz))";
        var regex = new Utf8Pcre2Regex(pattern);
        var input = "😀abc tail"u8.ToArray();
        Assert.True(regex.IsMatch(input));
        Assert.True(regex.IsMatch(input, cancellationToken: default));
        Assert.False(regex.IsMatch(input, matchOptions: Pcre2MatchOptions.Anchored));
        Assert.True(regex.IsMatch(input, startOffsetInBytes: 4, matchOptions: Pcre2MatchOptions.Anchored));
        Assert.False(regex.IsMatch(input, startOffsetInBytes: 5));
        using var source = new CancellationTokenSource();
        Assert.True(regex.IsMatch(input, cancellationToken: source.Token));
        source.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input,
            startOffsetInBytes: -1, matchOptions: (Pcre2MatchOptions)int.MaxValue, cancellationToken: source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
    }

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
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(expected, CreateMetered(pattern).IsMatch(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.IsMatch(input, 0, default, token.Token));
    }

    [Fact]
    public void FiniteBoundariesKeepSubjectContextCapturesAndAuthority()
    {
        const string pattern = @"\b(\d{1,2})/(\d{1,2})/(\d{2,4})\b";
        var regex = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern);
        var input = Encoding.UTF8.GetBytes("😀11/18/2019 1/2/34é");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), regex.IsMatch(input, start));
            Assert.Equal(metered.IsMatch(input, start, Pcre2MatchOptions.Anchored),
                regex.IsMatch(input, start, Pcre2MatchOptions.Anchored));
        }
        ReadOnlySpan<byte> retained1Input = input;
        var retained1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained1Input, captures: retained1Captures);
        Assert.Equal("11", System.Text.Encoding.UTF8.GetString((retained1Captures[1].Success ? retained1Input.Slice(retained1Captures[1].StartOffsetInBytes, retained1Captures[1].EndOffsetInBytes - retained1Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("18", System.Text.Encoding.UTF8.GetString((retained1Captures[2].Success ? retained1Input.Slice(retained1Captures[2].StartOffsetInBytes, retained1Captures[2].EndOffsetInBytes - retained1Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("2019", System.Text.Encoding.UTF8.GetString((retained1Captures[3].Success ? retained1Input.Slice(retained1Captures[3].StartOffsetInBytes, retained1Captures[3].EndOffsetInBytes - retained1Captures[3].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(2, regex.Count(input));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.IsMatch(input, 0, default, canceled.Token)).CancellationToken);
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
    public void SingleTokenAssertionConditionalsPreserveBooleanSemantics(string pattern, string text, bool expected)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, direct.IsMatch(input));
        Assert.Equal(expected, CreateMetered(pattern).IsMatch(input));
    }

    [Fact]
    public void ConditionalBooleanProofKeepsScalarFloorsCapturesAndRefusal()
    {
        const string pattern = "(?(?=(a))(a)b|(c)d)";
        var direct = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern);
        var input = Encoding.UTF8.GetBytes("😀ab écd");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), direct.IsMatch(input, start));
            Assert.Equal(metered.IsMatch(input, start, Pcre2MatchOptions.Anchored), direct.IsMatch(input, start, Pcre2MatchOptions.Anchored));
        }
        Assert.Equal(2, direct.Count(input));
        ReadOnlySpan<byte> retained2Input = input;
        var retained2Captures = new Utf8Pcre2CaptureRange[direct.CaptureGroupCount + 1];
        var retained = direct.Match(retained2Input, captures: retained2Captures);
        Assert.Equal("ab", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained2Captures[2].Success ? retained2Input.Slice(retained2Captures[2].StartOffsetInBytes, retained2Captures[2].EndOffsetInBytes - retained2Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.False(retained2Captures[3].Success);
        Assert.True(direct.IsMatch("cd"u8));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained2Captures[1].Success ? retained2Input.Slice(retained2Captures[1].StartOffsetInBytes, retained2Captures[1].EndOffsetInBytes - retained2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => direct.IsMatch("ab"u8, 0, default, canceled.Token)).CancellationToken);
    }

    [Theory]
    [InlineData("(?(?!(a)(*ACCEPT))def|abc)")]
    [InlineData("(?(?=(a)(*ACCEPT))abc|def)")]
    [InlineData("(?(?!(a(*ACCEPT)))def|abc)")]
    public void AcceptGuardBooleanProofPreservesCallerCapturesAndAuthority(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern);
        var input = Encoding.UTF8.GetBytes("😀abc édef");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), regex.IsMatch(input, start));
        }
        ReadOnlySpan<byte> retained3Input = input;
        var retained3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained3Input, captures: retained3Captures);
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained3Captures[1].Success ? retained3Input.Slice(retained3Captures[1].StartOffsetInBytes, retained3Captures[1].EndOffsetInBytes - retained3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(4, retained3Captures[1].StartOffsetInBytes);
        Assert.True(regex.IsMatch("def"u8));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained3Captures[1].Success ? retained3Input.Slice(retained3Captures[1].StartOffsetInBytes, retained3Captures[1].EndOffsetInBytes - retained3Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("abc"u8, 0, default, canceled.Token)).CancellationToken);
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
    public void SingleAsciiTokenCallsKeepProfileSemantics(string pattern, string text, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, regex.IsMatch(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.IsMatch(input, 0, default, token.Token));
        Assert.Equal(expected, CreateMetered(pattern).IsMatch(input));
    }

    [Fact]
    public void SingleTokenCallsKeepScalarOriginsOptionsAndCallerCaptures()
    {
        const string pattern = @"(?1)\d{3}(a)";
        var regex = new Utf8Pcre2Regex(pattern);
        var metered = CreateMetered(pattern);
        var input = Encoding.UTF8.GetBytes("😀a123a éa345a");
        for (var start = 0; start <= input.Length; start++)
        {
            if (start < input.Length && (input[start] & 0xc0) == 0x80) continue;
            Assert.Equal(metered.IsMatch(input, start), regex.IsMatch(input, start));
            Assert.Equal(metered.IsMatch(input, start, Pcre2MatchOptions.Anchored),
                regex.IsMatch(input, start, Pcre2MatchOptions.Anchored));
        }
        Assert.Equal(2, regex.Count(input));
        ReadOnlySpan<byte> retained4Input = input;
        var retained4Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var retained = regex.Match(retained4Input, captures: retained4Captures);
        Assert.Equal("a123a", System.Text.Encoding.UTF8.GetString(retained.GetValueBytes()));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained4Captures[1].Success ? retained4Input.Slice(retained4Captures[1].StartOffsetInBytes, retained4Captures[1].EndOffsetInBytes - retained4Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(8, retained4Captures[1].StartOffsetInBytes);
        Assert.True(regex.IsMatch("a456a"u8));
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((retained4Captures[1].Success ? retained4Input.Slice(retained4Captures[1].StartOffsetInBytes, retained4Captures[1].EndOffsetInBytes - retained4Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void SingleTokenCallsKeepOriginalRefusal()
    {
        const string pattern = @"^(?1)\d{3}(a)";
        var regex = new Utf8Pcre2Regex(pattern);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token,
            Assert.Throws<OperationCanceledException>(() => regex.IsMatch("a123a"u8, 0, default, canceled.Token)).CancellationToken);
    }

    [Fact]
    public void BoundedAsciiPreservesBooleanAndCaptureResults()
    {
        var regex = new Utf8Pcre2Regex(CreditCardPattern);
        Assert.True(regex.IsMatch("4111-1111-1111-111"u8));
        Assert.True(regex.IsMatch("4111 1111 1111 1111"u8));
        Assert.False(regex.IsMatch("4111-1111-1111-11"u8));
        Assert.False(regex.IsMatch("٤١١١-1111-1111-1111"u8));
        ReadOnlySpan<byte> detailed5Input = "card 4111-1111-1111-1111 done"u8;
        var detailed5Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var detailed = regex.Match(detailed5Input, captures: detailed5Captures);
        Assert.True(detailed.Success);
        Assert.Equal("4111-1111-1111-1111", System.Text.Encoding.UTF8.GetString(detailed.GetValueBytes()));
        Assert.Equal("1111-", System.Text.Encoding.UTF8.GetString((detailed5Captures[1].Success ? detailed5Input.Slice(detailed5Captures[1].StartOffsetInBytes, detailed5Captures[1].EndOffsetInBytes - detailed5Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void BoundedAsciiLanguageMatchesTheMeteredVmAtEveryValidStart()
    {
        var direct = new Utf8Pcre2Regex(CreditCardPattern);
        var vm = CreateMetered(CreditCardPattern);

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
        var vm = CreateMetered(pattern);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.Equal(expected, direct.IsMatch(input));
        Assert.Equal(vm.IsMatch(input), direct.IsMatch(input));
    }

    private static Utf8Pcre2Regex CreateMetered(string pattern) =>
        new(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 4096
            },
            MatchTimeout = Regex.InfiniteMatchTimeout
        });
}
