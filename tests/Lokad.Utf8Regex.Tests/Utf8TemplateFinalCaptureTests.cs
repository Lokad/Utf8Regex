using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8TemplateFinalCaptureTests
{
    public static IEnumerable<object[]> Cases()
    {
        yield return ["(a|(b))+", "éababa中", "<$1|$2|$+|$&>", RegexOptions.None];
        yield return ["((a)+)a", "éaaaa中", "<$1|$2|$+>", RegexOptions.None];
        yield return ["(?=(a+))a", "éaaa中", "<$1|$&>", RegexOptions.None];
        yield return ["(?<x>a)+(?<x>b)?", "éaaab中aaa", "<${x}|$1|$+>", RegexOptions.None];
        yield return ["(?<7>a)+(?<word>b)?", "éaaab中aaa", "<${7}|${word}|$+>", RegexOptions.None];
        yield return ["()((a)?)", "éa中", "<$1|$2|$3|$+>", RegexOptions.None];
        yield return [@"(a+)\1", "éaaaa中", "<$1|$&>", RegexOptions.None];
        yield return ["(?<x>a)+(?<-x>b)+(?(x)(?!))", "éaaabbb中", "<${x}|$+|$&>", RegexOptions.None];
        yield return ["(?<x>a)+(?<-x>b)", "éaaab中", "<${x}|$+|$&>", RegexOptions.None];
        yield return ["(😀|é)+", "中😀é😀/é中", "<$1|$+|$&>", RegexOptions.RightToLeft];
        yield return ["(?<g>[a-z])+", "éabc中de", "<${g}|$+|$&>", RegexOptions.NonBacktracking];
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ControlledTemplatesKeepFinalRestoredCaptureValues(string pattern, string text, string template, RegexOptions options)
    {
        options |= RegexOptions.CultureInvariant;
        var expected = new Regex(pattern, options).Replace(text, template);
        var input = Encoding.UTF8.GetBytes(text);
        var bytes = Encoding.UTF8.GetBytes(expected);
        var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 8 << 20, MaxResultBytes = 8 << 20 });
        Assert.Equal(expected, regex.ReplaceToString(input, template));
        Assert.Equal(bytes, regex.Replace(input, template));
        Assert.Equal(bytes, regex.Replace(input, Encoding.UTF8.GetBytes(template)));
        var output = new byte[bytes.Length];
        Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, output, out var written));
        Assert.Equal(bytes.Length, written); Assert.Equal(bytes, output);
        Assert.Throws<ArgumentException>(() => regex.Replace([.. input, 0xff], template));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EvaluatorsStillReceiveTheirCompleteProfileHistory(bool nonBacktracking)
    {
        var regex = new Utf8Regex("(a)+", nonBacktracking ? RegexOptions.NonBacktracking : RegexOptions.None);
        var observed = new List<int>();
        var output = regex.ReplaceToString("éaaaa中"u8, observed,
            static (in Utf8MatchContext match, ref List<int> counts) => {
                var group = match.GetGroup(1);
                counts.Add(group.CaptureCount);
                for (var index = 0; index < group.CaptureCount; index++)
                    Assert.Equal("a", group.GetCapture(index).GetValueString());
                return group.GetValueString();
            });
        Assert.Equal("éa中", output);
        Assert.Equal(new[] { nonBacktracking ? 1 : 4 }, observed);
        Assert.Equal(output, regex.ReplaceToString("éaaaa中"u8, "$1"));
    }
}
