using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2NumericCaptureAdmissionTests
{
    public static IEnumerable<object[]> InvalidDefinitions()
    {
        foreach (var name in new[] { "1", "100", "١", "𝟙" })
        foreach (var form in new[] { "(?<{0}>a)", "(?'{0}'a)", "(?P<{0}>a)" })
            yield return [string.Format(form, name)];
        foreach (var pattern in new[] { "(a)(?<100>b)(?<x>c)", @"(?<x>a)(?<100>b)\k<x>",
            "(?x:(?-x:#(?<100>a)))", "(?x)(?-x:#)(?<100>a)", "(?x:# (?<100>a)\na)(?<100>b)" })
            yield return [pattern];
    }

    [Theory]
    [MemberData(nameof(InvalidDefinitions))]
    public void NumericDefinitionsFailBeforeAnyBackendAcrossConstructorSurfaces(string pattern)
    {
        var settings = new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Lf, AllowDuplicateNames = true };
        var bytes = Encoding.UTF8.GetBytes(pattern);
        foreach (var options in new[] { Pcre2CompileOptions.None, Pcre2CompileOptions.NoAutoCapture | Pcre2CompileOptions.Ucp })
        {
            Action[] constructors = [
                () => _ = new Utf8Pcre2Regex(pattern, options: options, compileSettings: settings, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = TimeSpan.FromSeconds(5)
                }),
                () => _ = new Utf8Pcre2Regex(bytes, options: options, compileSettings: settings, policy: new Utf8RegexPolicy
                {
                    MatchTimeout = TimeSpan.FromSeconds(5)
                }),
                () => _ = new Utf8Pcre2Regex(pattern, options: options, compileSettings: settings, policy: new Utf8RegexPolicy
                {
                    ResourcePolicy = default,
                    MatchTimeout = TimeSpan.FromSeconds(5)
                }),
                () => _ = new Utf8Pcre2Regex(bytes, options: options, compileSettings: settings, policy: new Utf8RegexPolicy
                {
                    ResourcePolicy = default,
                    MatchTimeout = TimeSpan.FromSeconds(5)
                }) ];
            foreach (var construct in constructors)
            {
                var exception = Assert.Throws<Pcre2CompileException>(construct);
                Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, exception.ErrorKind);
                Assert.Contains("non-digit", exception.Message);
            }
        }
    }

    [Theory]
    [InlineData(@"\Q(?<100>a)\E", "(?<100>a)", Pcre2CompileOptions.None)]
    [InlineData(@"\Q(?'100'a)\E", "(?'100'a)", Pcre2CompileOptions.None)]
    [InlineData(@"\Q(?P<100>a)\E", "(?P<100>a)", Pcre2CompileOptions.None)]
    [InlineData(@"\(\?<100>a\)", "(?<100>a)", Pcre2CompileOptions.None)]
    [InlineData("[(?<100>a)]", "1", Pcre2CompileOptions.None)]
    [InlineData("[](?<100>a)]", "]", Pcre2CompileOptions.None)]
    [InlineData("[^](?<100>a)]", "b", Pcre2CompileOptions.None)]
    [InlineData("[[:alpha:](?<100>)]", "a", Pcre2CompileOptions.None)]
    [InlineData("(?#(?<100>a)a", "a", Pcre2CompileOptions.None)]
    [InlineData("# (?<100>a)\na", "a", Pcre2CompileOptions.Extended)]
    [InlineData("(?x)# (?<100>a)\na", "a", Pcre2CompileOptions.None)]
    [InlineData("(?x:# (?<100>a)\na)b", "ab", Pcre2CompileOptions.None)]
    public void NumericLookingLiteralAndCommentTextKeepsItsMeaning(string pattern, string text, Pcre2CompileOptions options)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: options);
        var match = regex.MatchDetailed(Encoding.UTF8.GetBytes(text));
        Assert.True(match.Success);
        Assert.Equal(text, System.Text.Encoding.UTF8.GetString(match.Value.GetValueBytes()));
        Assert.Equal(1, match.CaptureSlotCount);
        Assert.Equal(0, regex.NameEntryCount);
    }

    [Theory]
    [InlineData(@"[\Q(?<100>a)\E]")]
    [InlineData("(*MARK:(?<100>a)a")]
    public void LiteralBodiesMatchWithoutDeclaringCapturesOrHidingFollowingDefinitions(string pattern)
    {
        // Both formerly unserved bodies now execute in the owned profile.
        Pcre2CaptureNameValidator.Validate(pattern, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf);
        var match = new Utf8Pcre2Regex(pattern).MatchDetailed("a"u8);
        Assert.True(match.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString(match.Value.GetValueBytes()));
        Assert.Equal(1, match.CaptureSlotCount);
        var exception = Assert.Throws<Pcre2CompileException>(() =>
            new Utf8Pcre2Regex(pattern + "(?<100>b)"));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, exception.ErrorKind);
    }

    [Theory]
    [InlineData(Pcre2NewlineConvention.Lf, '\n')]
    [InlineData(Pcre2NewlineConvention.Cr, '\r')]
    [InlineData(Pcre2NewlineConvention.Crlf, '\r')]
    [InlineData(Pcre2NewlineConvention.Any, '\u2028')]
    [InlineData(Pcre2NewlineConvention.AnyCrlf, '\r')]
    [InlineData(Pcre2NewlineConvention.Nul, '\0')]
    public void NameAdmissionObservesTheConfiguredExtendedCommentNewline(Pcre2NewlineConvention newline, char separator)
    {
        var breakText = newline == Pcre2NewlineConvention.Crlf ? "\r\n" : separator.ToString();
        Pcre2CaptureNameValidator.Validate("# (?<100>a)" + breakText + "a", Pcre2CompileOptions.Extended, newline);
        var exception = Assert.Throws<Pcre2CompileException>(() =>
            Pcre2CaptureNameValidator.Validate("# comment" + breakText + "(?<100>a)", Pcre2CompileOptions.Extended, newline));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, exception.ErrorKind);
    }

    [Theory]
    [InlineData("(?<x>a)(b)", "ab", "a", "b", 1)]
    [InlineData("(a)(?<x>b)", "ab", "a", "b", 2)]
    [InlineData("(?<x>a)(b)?", "a", "a", null, 1)]
    [InlineData(@"(?<x>a)(b)\1\2", "abab", "a", "b", 1)]
    [InlineData("(é)(?<x>😀)", "é😀", "é", "😀", 2)]
    [InlineData("(?<x>😀)(é)?", "😀", "😀", null, 1)]
    public void ValidNumericSlotsNamesAndRangeViewsKeepPcre2DeclarationOrder(
        string pattern, string text, string left, string? right, int nameSlot)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        var input = Encoding.UTF8.GetBytes("z" + text + "z");
        var match = regex.MatchDetailed(input);
        Assert.True(match.Success);
        Assert.Equal(3, match.CaptureSlotCount);
        Assert.False(match.TryGetGroup(100, out _));
        AssertGroup(match.GetGroup(0), text, text);
        AssertGroup(match.GetGroup(1), left, text);
        AssertGroup(match.GetGroup(2), right, text);
        var names = new Pcre2NameEntry[1];
        Assert.Equal(1, regex.CopyNameEntries(names, out var more));
        Assert.False(more);
        Assert.Equal("x", names[0].Name);
        Assert.Equal(nameSlot, names[0].Number);
        Assert.True(match.TryGetFirstSetGroup("x", out var named));
        Assert.Equal(nameSlot == 1 ? left : right, System.Text.Encoding.UTF8.GetString(named.GetValueBytes()));
        {
            using var cursor = regex.CreateMatchCursor(input);
            Assert.True(cursor.MoveNext(out var range));
            Assert.Equal(3, range.Groups.Length);
            Assert.Equal(left, Encoding.UTF8.GetString(range.GetGroupMemory(1).Span));
            Assert.Equal(right ?? "", Encoding.UTF8.GetString(range.GetGroupMemory(2).Span));
            Assert.Equal(right is not null, range.Groups[2].Success);
            Assert.Equal(nameSlot, range.NameEntries[0].Number);
        }    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void CoreProfileRetainsDotNetExplicitNumericDefinitions(int number)
    {
        var pattern = $"(?<{number}>😀)";
        var oracle = new Regex(pattern).Match("z😀z").Groups[number];
        var regex = new Utf8Regex(pattern);
        var input = Encoding.UTF8.GetBytes("z😀z");
        Span<Utf8CaptureRange> slots = stackalloc Utf8CaptureRange[2];
        Assert.True(regex.Match(input, 0, slots).Success);
        Assert.Equal(number, regex.GroupNumberFromCaptureSlot(1));
        var group = slots[1];
        Assert.True(group.Success);
        Assert.Equal(oracle.Value, Encoding.UTF8.GetString(input.AsSpan(
            group.StartOffsetInBytes, group.EndOffsetInBytes - group.StartOffsetInBytes)));
    }

    private static void AssertGroup(Utf8Pcre2GroupContext group, string? value, string text)
    {
        Assert.Equal(value is not null, group.Success);
        if (value is null) return;
        Assert.Equal(value, System.Text.Encoding.UTF8.GetString(group.GetValueBytes()));
        var start = text.IndexOf(value, StringComparison.Ordinal);
        Assert.Equal(1 + Encoding.UTF8.GetByteCount(text[..start]), group.StartOffsetInBytes);
        Assert.Equal(1 + Encoding.UTF8.GetByteCount(text[..(start + value.Length)]), group.EndOffsetInBytes);
    }
}
