using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralWordBoundaryTests
{
    [Theory]
    [InlineData("class|struct", "class", "struct")]
    [InlineData("record|struct|class", "record", "class")]
    [InlineData("class|struct|union|enum", "class", "enum")]
    [InlineData("record|struct|class|union|enum|module|trait|interface", "record", "interface")]
    [InlineData("record|struct|class|union|enum|module|trait|interface|namespace", "record", "namespace")]
    [InlineData("a|bbbb", "a", "bbbb")]
    public void SkippingWordInteriorsPreservesEveryCandidateOriginAndPublicResult(
        string alternatives, string first, string last)
    {
        var pattern = @"\b(?:" + alternatives + @")\s+[A-Z][A-Za-z0-9_]+";
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options: options);
            foreach (var padding in new[] { 0, 1, 15, 16, 31, 32, 63, 64, 2047, 2048 })
            foreach (var body in new[]
            {
                "", first, first + " ", first + " A", first + " Ab",
                "x" + first + " Ab " + last + " Cd_1",
                first + "! Ab " + last + "\tCd " + first + "\r\nEf",
                first + "x Ab " + last + "\vGh_ " + first + " Ij",
                first[..^1] + last[^1] + " Ab " + last[..^1] + first[^1] + " Cd " + last + " Ef",
                first + " Ab" + last + " Cd " + last + " Ef",
                "o.s" + first + " Ab !" + last + " Cd",
                "x." + first + " Ab x.." + last + " Cd",
                "x_0" + first + " Ab 9" + last + " Cd !" + first + " Ef",
                new string('o', 65) + "!" + first + " Ab",
                first + " Ab " + new string('o', 65),
            })
            {
                var text = new string('o', padding) + (padding == 0 ? "" : " ") + body;
                var input = Encoding.UTF8.GetBytes(text);
                byte[] storage = [0xff, .. input, 0xff];
                var subject = storage.AsSpan(1, input.Length);
                var expected = oracle.Matches(text).Cast<Match>().ToArray();
                Assert.Equal(expected.Length, regex.Count(subject));
                Assert.Equal(expected.Length != 0, regex.IsMatch(subject));
                var match = regex.Match(subject);
                Assert.Equal(expected.Length != 0, match.Success);
                if (match.Success)
                        Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[0].Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected[0].Index, expected[0].Length))), (match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
                    var cursor = regex.EnumerateMatches(subject);
                var index = 0;
                while (cursor.MoveNext())
                {
                        Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected[index].Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected[index].Index, expected[index].Length))),
                            (cursor.Current.StartOffsetInBytes, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes)));
                        index++;
                }
                Assert.Equal(expected.Length, index);
            }
        }
    }

    [Fact]
    public void StrictFullSubjectValidationAndOriginalTokenPrecedeNativeSearch()
    {
        const string pattern = @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+";
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.NonBacktracking })
        {
            var regex = new Utf8Regex(pattern, options: options);
            const string text = "éclass Nope 😀 class Alpha\u2003record Beta struct Gamma";
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(new Regex(pattern, options).Count(text), regex.Count(input));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Assert.Equal(cancellation.Token,
                Assert.Throws<OperationCanceledException>(() => regex.Count(input, cancellation.Token)).CancellationToken);
        }
    }
}
