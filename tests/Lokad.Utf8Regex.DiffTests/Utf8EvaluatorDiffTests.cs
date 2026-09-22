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

    public static IEnumerable<object?[]> StringEvaluatorCases()
    {
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant, "cat horse fish horse" };
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant, "no match here" };
        yield return new object?[] { "cat|horse", RegexOptions.CultureInvariant, "" };
        yield return new object?[] { "[a-z]+", RegexOptions.CultureInvariant, "11ab22cd" };
        yield return new object?[] { "ABC", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "xxabCxx" };
        yield return new object?[] { "y", RegexOptions.CultureInvariant, "x\U0001F600y\U0001F600" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "cat horse fish horse" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "horsecat" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "no match here" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "" };
        yield return new object?[] { "(?<w>cat)|(?<v>horse)", RegexOptions.CultureInvariant, "cat horse fish" };
        yield return new object?[] { "(a)|(ab)", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "(ab)|(a)", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "(cat|horse)", RegexOptions.CultureInvariant, "cat horse fish horse" };
        yield return new object?[] { "(cat|horse)", RegexOptions.CultureInvariant, "no match here" };
        yield return new object?[] { "(a)", RegexOptions.CultureInvariant, "baac" };
        yield return new object?[] { "(?<w>cat|horse)", RegexOptions.CultureInvariant, "cat horse" };
        yield return new object?[] { "(cat)|(horse)|(dog)", RegexOptions.CultureInvariant, "dog horse cat" };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", RegexOptions.None, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "CAT horse Cat" };
        yield return new object?[] { "(cat)|horse", RegexOptions.CultureInvariant, "horsecat" };
        yield return new object?[] { "((cat)|(horse))", RegexOptions.CultureInvariant, "cat horse" };
        yield return new object?[] { "(?:(a)|(b))", RegexOptions.CultureInvariant, "xbxa" };
        yield return new object?[] { "(k)|(q)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "K Q \u212A" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.RightToLeft | RegexOptions.CultureInvariant, "cat horse" };
        yield return new object?[] { "\\b(cat)|(horse)\\b", RegexOptions.CultureInvariant, "acat horsecatx" };
        yield return new object?[] { "(a)(b)", RegexOptions.CultureInvariant, "xxabxx" };
        yield return new object?[] { "(a)+", RegexOptions.CultureInvariant, "aaabaaa" };
    }

    [Theory]
    [MemberData(nameof(StringEvaluatorCases))]
    public void StringEvaluatorReplaceMatchesRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var runtime = new Regex(pattern, options);
        var expected = runtime.Replace(
            text,
            static match => "<" + match.Groups[1].Value + "|" + match.Value + ">");

        var utf8 = new Utf8Regex(pattern, options);
        var calls = new int[1];
        var actual = utf8.ReplaceToString(
            input,
            calls,
            static (in Utf8MatchContext context, ref int[] count) =>
            {
                count[0]++;
                Assert.True(context.Success);
                var group = context.GetGroup(1);
                return "<" + group.GetValueString() + "|" + context.GetValueString() + ">";
            });

        Assert.Equal(expected, actual);
        Assert.Equal(new Regex(pattern, options).Count(text), calls[0]);
    }

    [Theory]
    [MemberData(nameof(StringEvaluatorCases))]
    public void StringEvaluatorNullReplacementMatchesRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var runtime = new Regex(pattern, options);
        var expected = runtime.Replace(text, static match => (string)null!);

        var utf8 = new Utf8Regex(pattern, options);
        var actual = utf8.ReplaceToString(
            input,
            0,
            static (in Utf8MatchContext context, ref int state) => (string)null!);

        Assert.Equal(expected, actual);
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

    public static IEnumerable<object?[]> BranchedCases()
    {
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "cat horse fish horse" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "horsecat" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "cathorse" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "no match here" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.CultureInvariant, "cat" };
        yield return new object?[] { "(?<w>cat)|(?<v>horse)", RegexOptions.CultureInvariant, "cat horse fish" };
        yield return new object?[] { "(a)|(ab)", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "(ab)|(a)", RegexOptions.CultureInvariant, "ab" };
        yield return new object?[] { "(cat)|(horse)|(dog)", RegexOptions.CultureInvariant, "dog horse cat" };
        yield return new object?[] { "(caf\u00E9)|(ni\u00F1o)", RegexOptions.None, "\u03B1caf\u00E9\u03B2ni\u00F1o\u03B3" };
        yield return new object?[] { "(cat)|(horse)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "CAT horse Cat" };
        yield return new object?[] { "(CAT)|(DOG)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "cat dog" };
        yield return new object?[] { "(cat)|horse", RegexOptions.CultureInvariant, "horsecat" };
        yield return new object?[] { "horse|(cat)", RegexOptions.CultureInvariant, "horsecat" };
        yield return new object?[] { "((cat)|(horse))", RegexOptions.CultureInvariant, "cat horse" };
        yield return new object?[] { "(?:(a)|(b))", RegexOptions.CultureInvariant, "xbxa" };
        yield return new object?[] { "(k)|(q)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "K Q \u212A" };
        yield return new object?[] { "\\b(cat)|(horse)\\b", RegexOptions.CultureInvariant, "acat horsecatx" };
        yield return new object?[] { "(cat|horse)", RegexOptions.CultureInvariant, "cat horse fish horse" };
        yield return new object?[] { "(cat|horse)", RegexOptions.CultureInvariant, "no match here" };
        yield return new object?[] { "(a)", RegexOptions.CultureInvariant, "baac" };
        yield return new object?[] { "(?<w>cat|horse)", RegexOptions.CultureInvariant, "cat horse" };
        yield return new object?[] { "(cat|horse)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, "CAT horse" };
    }

    [Theory]
    [MemberData(nameof(BranchedCases))]
    public void BranchedEvaluatorReplaceMatchesRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var runtime = new Regex(pattern, options);
        var expected = Encoding.UTF8.GetBytes(runtime.Replace(
            text,
            static match => "<" + match.Groups[1].Value + "|" + match.Value + ">"));

        var utf8 = new Utf8Regex(pattern, options);
        var calls = new int[1];
        var actual = utf8.Replace(
            input,
            calls,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int[] count) =>
            {
                count[0]++;
                Assert.True(context.Success);
                var group = context.GetGroup(1);
                Assert.Equal(context.GetGroup(0).GetValueString(), context.GetValueString());
                writer.Append(Encoding.UTF8.GetBytes("<" + group.GetValueString() + "|" + context.GetValueString() + ">"));
            });

        Assert.Equal(expected, actual);
        Assert.Equal(new Regex(pattern, options).Count(text), calls[0]);
    }

    [Theory]
    [MemberData(nameof(BranchedCases))]
    public void BranchedEvaluatorGroupValuesMatchRuntime(string pattern, RegexOptions options, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var runtime = new Regex(pattern, options);
        var expectedValues = new List<string>();
        foreach (System.Text.RegularExpressions.Match match in runtime.Matches(text))
        {
            var groups = new List<string>();
            for (var i = 0; i < match.Groups.Count; i++)
            {
                groups.Add(match.Groups[i].Success.ToString() + ":" + match.Groups[i].Value);
            }

            expectedValues.Add(string.Join(";", groups));
        }

        var utf8 = new Utf8Regex(pattern, options);
        var actualValues = new List<string>();
        var actual = utf8.Replace(
            input,
            actualValues,
            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<string> values) =>
            {
                var groups = new List<string>();
                for (var i = 0; i < context.GroupCount; i++)
                {
                    var group = context.GetGroup(i);
                    groups.Add(group.Success.ToString() + ":" + group.GetValueString());
                }

                values.Add(string.Join(";", groups));
                writer.Append(Encoding.UTF8.GetBytes("[" + context.GetValueString() + "]"));
            });

        var expectedOutput = Encoding.UTF8.GetBytes(runtime.Replace(text, static match => "[" + match.Value + "]"));
        Assert.Equal(expectedOutput, actual);
        Assert.Equal(expectedValues, actualValues);
    }
}
