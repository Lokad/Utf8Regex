using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureSlotRetirementTests
{
    [Fact]
    public void ShippedCoreRetainsNoSecondCaptureNumberingTranslator()
    {
        Assert.Null(typeof(Utf8Regex).Assembly.GetType("Lokad.Utf8Regex.Internal.Execution.Utf8BclSlotTranslator"));
    }

    public static IEnumerable<object[]> PublicCases()
    {
        (string Pattern, string Text)[] cases =
        [
            ("(?<outer>a(?<inner>😀)b)", "xa😀by"),
            ("(a)(?<outer>(?<inner>😀))", "xa😀y"),
            ("(?<outer>a(?<inner>😀)?b)", "ab a😀b"),
            ("(?<outer>a(?<inner>é|😀)b)", "aéb a😀b"),
            ("(?<outer>(?<inner>a|b)(?:é|😀))", "aé b😀"),
            ("(?<outer>(?<left>a)?(?<right>😀)??b)", "b ab 😀b a😀b"),
            ("(?<piece>é|😀){2}", "é😀 😀é"),
            ("(?<2>é)(?<5>😀)", "xé😀y"),
            ("(?<x>é)(?<x>😀)", "xé😀y"),
            ("(?<x>a)|(?<x>😀)", "a😀a"),
            ("(?<x>é)(?<y-x>😀)", "xé😀y"),
        ];
        foreach (var (pattern, text) in cases)
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
                yield return [pattern, text, options | RegexOptions.CultureInvariant];
    }

    [Theory]
    [MemberData(nameof(PublicCases))]
    public void PublicScalarCapturesRetainNamesNumbersRangesAndCompleteHistories(string pattern, string text, RegexOptions options)
    {
        // These patterns contain whole scalar literals only, so this comparator
        // overlaps the UTF-8 contract even with supplementary characters.
        var oracle = new Regex(pattern, options & ~RegexOptions.Compiled);
        var regex = new Utf8Regex(pattern, options: options);
        Assert.Equal(oracle.GetGroupNumbers(), Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
        var copiedNames2 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot2 = 0; captureSlot2 < copiedNames2.Length; captureSlot2++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot2, [], out var nameLength2));
            var nameBytes2 = new byte[nameLength2];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot2, nameBytes2, out var written2));
            Assert.Equal(nameLength2, written2);
            copiedNames2[captureSlot2] = System.Text.Encoding.UTF8.GetString(nameBytes2);
        }
        Assert.Equal(oracle.GetGroupNames(), copiedNames2);
        foreach (var name in oracle.GetGroupNames()) Assert.Equal(oracle.GroupNumberFromName(name), regex.GroupNumberFromUtf8Name(System.Text.Encoding.UTF8.GetBytes(name)));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, [text]);
        var input = Encoding.UTF8.GetBytes(text);
        var hit = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Utf8MatchSequence.AssertCaptures(oracle.Match(text), hit, input, text);
    }
}
