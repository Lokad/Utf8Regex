using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ScalarReplacementOutputTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void TemplatesKeepCompleteScalarCapturesAcrossOutputForms(bool compiled, bool rtl)
    {
        var regex = new Utf8Regex("(?<outer>(?<x>.))", Options(compiled, rtl));
        using var source = new CancellationTokenSource();
        foreach (var text in new[] { "\U0001f600", "a\U0001f600b", "\u00e9\U0001f600" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            const string template = "\ud83d${x}\ude00|$&";
            var expected = string.Concat(text.EnumerateRunes().Select(scalar => $"\ufffd{scalar}\ufffd|{scalar}"));
            var expectedBytes = Encoding.UTF8.GetBytes(expected);
            Assert.Equal(expected, regex.ReplaceToString(input, template, source.Token));
            Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
            Assert.Equal(expectedBytes, regex.Replace(input, Encoding.UTF8.GetBytes(template), source.Token));
            var destination = new byte[expectedBytes.Length];
            Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
            Assert.Equal(expectedBytes.Length, written);
            Assert.Equal(expectedBytes, destination);
            destination = Enumerable.Repeat((byte)0xa5, expectedBytes.Length - 1).ToArray();
            Assert.Equal(OperationStatus.DestinationTooSmall, regex.TryReplace(input, template, destination, out written, source.Token));
            Assert.Equal(0, written);
            Assert.All(destination, value => Assert.Equal((byte)0xa5, value));
        }
        AssertOwned(regex);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void StringEvaluatorFragmentsCannotPairAcrossMatches(bool compiled, bool rtl)
    {
        var regex = new Utf8Regex("(.)", Options(compiled, rtl));
        var calls = new List<int>();
        Assert.Equal("\ufffd\ufffd", regex.ReplaceToString("ab"u8, calls,
            static (in Utf8MatchContext context, ref List<int> state) =>
            {
                state.Add(context.IndexInUtf16);
                return context.IndexInUtf16 == 0 ? "\ud83d" : "\ude00";
            }));
        Assert.Equal(rtl ? new[] { 1, 0 } : new[] { 0, 1 }, calls);
        Assert.Equal("\U0001f600\U0001f600", regex.ReplaceToString("ab"u8, 0,
            static (in Utf8MatchContext context, ref int state) => "\ud83d\ude00"));
        AssertOwned(regex);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void EmptySubstitutionsCannotCompleteLiteralSurrogates(bool compiled, bool rtl)
    {
        var regex = new Utf8Regex("()", Options(compiled, rtl));
        const string template = "\ud83d$1\ude00";
        const string expected = "\ufffd\ufffd\U0001f600\ufffd\ufffd";
        Assert.Equal(expected, regex.ReplaceToString("\U0001f600"u8, template));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace("\U0001f600"u8, template));
        AssertOwned(regex);
    }

    [Theory]
    [InlineData(false, false, "string")]
    [InlineData(false, true, "string")]
    [InlineData(true, false, "string")]
    [InlineData(true, true, "string")]
    [InlineData(false, false, "bytes")]
    [InlineData(false, true, "bytes")]
    [InlineData(true, false, "bytes")]
    [InlineData(true, true, "bytes")]
    [InlineData(false, false, "destination")]
    [InlineData(false, true, "destination")]
    [InlineData(true, false, "destination")]
    [InlineData(true, true, "destination")]
    public void TemplateMissesAvoidSubjectMapsAndDecodedStaging(bool compiled, bool rtl, string operation)
    {
        var regex = new Utf8Regex("(?<x>z)", Options(compiled, rtl));
        var text = new string('\u00e9', 50000);
        var input = Encoding.UTF8.GetBytes(text);
        var destination = new byte[input.Length];
        // Warm cached template analysis, VM and pools before observing allocations.
        for (var index = 0; index < 5; index++) Run();
        var before = GC.GetAllocatedBytesForCurrentThread();
        Run();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var requiredPayloads = operation == "destination" ? input.Length : input.Length * 2;
        Assert.True(allocated <= requiredPayloads + 32768, $"{operation} allocated {allocated} bytes for {input.Length} input bytes.");
        AssertOwned(regex);

        void Run()
        {
            switch (operation)
            {
                case "string": Assert.Equal(text, regex.ReplaceToString(input, "$1")); break;
                case "bytes": Assert.Equal(input, regex.Replace(input, "$1")); break;
                case "destination":
                    Assert.Equal(OperationStatus.Done, regex.TryReplace(input, "$1", destination, out var written));
                    Assert.Equal(input.Length, written);
                    Assert.Equal(input, destination);
                    break;
            }
        }
    }

    private static RegexOptions Options(bool compiled, bool rtl) => RegexOptions.CultureInvariant |
        (compiled ? RegexOptions.Compiled : RegexOptions.None) | (rtl ? RegexOptions.RightToLeft : RegexOptions.None);

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }
}
