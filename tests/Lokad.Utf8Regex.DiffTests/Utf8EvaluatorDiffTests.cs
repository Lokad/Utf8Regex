using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

/// <summary>
/// R03 evaluator ownership: groupless patterns discover matches and shape every
/// callback argument from owned spans, with byte-identical replacement output to the
/// BCL evaluator oracle. Grouped patterns keep BCL discovery and shaping; the timeout
/// contract matches on both routes.
/// </summary>
public sealed class Utf8EvaluatorDiffTests
{
    public static IEnumerable<object?[]> GrouplessCases()
    {
        yield return new object?[] { "abc", RegexOptions.CultureInvariant, "xxabcxxabc" };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, "11ab22cd" };
        yield return new object?[] { "^abc", RegexOptions.CultureInvariant, "abcabc" };
        yield return new object?[] { "abc$", RegexOptions.CultureInvariant, "xxabc" };
        yield return new object?[] { "\\bword\\b", RegexOptions.CultureInvariant, "a word or wordy words" };
        yield return new object?[] { "a|b", RegexOptions.CultureInvariant, "xxaxxb" };
        yield return new object?[] { "\\d+", RegexOptions.CultureInvariant, "ab12cd34" };
        yield return new object?[] { "a*", RegexOptions.CultureInvariant, "bba" };
        yield return new object?[] { string.Empty, RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "Шерлок", RegexOptions.None, "аШерлокбШерлок" };
        yield return new object?[] { "y", RegexOptions.CultureInvariant, "x\U0001F600y\U0001F600" };
        yield return new object?[] { "ABC", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxabCxx" };
        yield return new object?[] { "zzz", RegexOptions.CultureInvariant, "abc" };
        yield return new object?[] { "abc", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "xxabcxx" };
    }

    public static IEnumerable<object?[]> GroupedCases()
    {
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "xxabxx" };
        yield return new object?[] { "(?<w>\\w+)@(?<d>\\w+)", RegexOptions.CultureInvariant, "mail a@b.cd ok" };
        yield return new object?[] { "(a)+", RegexOptions.CultureInvariant, "aaabaaa" };
    }

    [Theory]
    [MemberData(nameof(GrouplessCases))]
    public void GrouplessEvaluatorReplaceMatchesRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var runtime = new Regex(pattern, options);
        var expected = Encoding.UTF8.GetBytes(runtime.Replace(
            text,
            static match => "<" + match.Value + ">"));

        var utf8 = new Utf8Regex(pattern, options);
        var calls = new int[1];
        var actual = utf8.Replace(
            input,
            calls,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int[] count) =>
            {
                count[0]++;
                Assert.True(context.Success);
                Assert.Equal(1, context.GroupCount);
                var value = context.GetValueString();
                var group = context.GetGroup(0);
                Assert.True(group.Success);
                Assert.Equal(value, group.GetValueString());
                Assert.Equal(1, group.CaptureCount);
                Assert.Equal(value, group.GetCapture(0).GetValueString());
                Assert.True(context.TryGetGroup(0, out var tryGroup));
                Assert.Equal(value, tryGroup.GetValueString());
                Assert.False(context.TryGetGroup(1, out _));
                Assert.False(context.TryGetGroup("missing", out _));
                writer.Append(Encoding.UTF8.GetBytes("<" + value + ">"));
            });

        Assert.Equal(expected, actual);
        Assert.Equal(new Regex(pattern, options).Count(text), calls[0]);
    }

    [Theory]
    [MemberData(nameof(GroupedCases))]
    public void GroupedEvaluatorReplaceMatchesRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var runtime = new Regex(pattern, options);
        var expected = Encoding.UTF8.GetBytes(runtime.Replace(
            text,
            static match => "<" + match.Groups[1].Value + ">"));

        var utf8 = new Utf8Regex(pattern, options);
        var calls = new int[1];
        var actual = utf8.Replace(
            input,
            calls,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int[] count) =>
            {
                count[0]++;
                writer.Append(Encoding.UTF8.GetBytes("<" + context.GetGroup(1).GetValueString() + ">"));
            });

        Assert.Equal(expected, actual);
        Assert.Equal(new Regex(pattern, options).Count(text), calls[0]);
    }

    [Fact]
    public void EvaluatorTimeoutMatchesRuntime()
    {
        var input = new byte[50_000];
        Array.Fill(input, (byte)'a');
        input[^1] = (byte)'!';
        var text = Encoding.UTF8.GetString(input);
        var utf8 = new Utf8Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));
        var runtime = new Regex(@"(a+)+$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(1));

        var runtimeException = Assert.Throws<RegexMatchTimeoutException>(() =>
            runtime.Replace(text, static match => match.Value));
        var utf8Exception = Assert.Throws<RegexMatchTimeoutException>(() =>
            utf8.Replace(input, 0, static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => { }));
        Assert.Equal(runtimeException.Pattern, utf8Exception.Pattern);
    }
}
