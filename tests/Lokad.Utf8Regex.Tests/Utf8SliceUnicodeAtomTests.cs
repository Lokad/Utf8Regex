using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceUnicodeAtomTests
{
    private static readonly string[] Texts = ["", "abc", "\u00e9\u00e9a\u00c9", "\u6f22\u6f22a",
        "\U0001f600", "a\U0001f600b", "\U0001f600\U0001f600b", "x\U0001f600ab",
        "\u00e9\U00010400a\u00e9\u00e9", "\u212aKk\u0130Ii\u0131", "\u03a3\u03c3\u03c2\u2126\u03c9",
        "\u00c5\u00e5\u212b\u0178\u00ff", "\u0000\u0080\u00ff\uffff\n", "a\n\U0001f600b",
        "bcdAEIOUxyz 123_\t\r\n", "\u0000\u0001\u0008\u001a\u001b\u001c\u001d\u001e\u001fA"];

    private static readonly string[] AtomPatterns = [
            "(\u00e9+)\\1", "(\u6f22|\u00e9)+", "(?i:\u00e9+)", "(?i:\u212a+)", "(?i:\u03a3+)", "(?i:\u0130+)",
            "\\xFF+", "(\\u6f22+)", "(\\uFFFF|\\u0080)", "(\U0001f600)+", "\U0001f600+", "\U0001f600*b",
            "a\U0001f600+b", "(\ud83d)", "(\ude00+)", "(\\ud83d)(\\ude00)", "(?<h>\ud83d)(?<l>\ude00)",
            "(\ud83d|\ude00)+", "(\ud83d)(?=\ude00)", "(?<=\ud83d)(\ude00)", "(?<=(\u00e9+))a",
            "(?=(\u6f22+))", "(?<\u00e9>\u00e9+)", "(?#\u6f22)\u00e9", "(\\\u2603)",
            "([\u00e9]+)", "([^\u00e9]+)", "([\u00c0-\u00ff]+)", "([\u007f-\u0081]+)", "([\u6f22a\\d]+)",
            "([\u00e9\\p{L}]+)", "([^\u00e9\\P{L}]+)", "([\ud800-\udbff]+)", "([\udc00-\udfff]+)",
            "([\U0001f600a]+)b", "([^\ud800-\udfff]+)", "([\uffff]+)", "([^\uffff]+)",
            "(?i:[\u212a]+)", "(?i:[^\u212a]+)", "(?i:[\u00c5\u03a9]+)", "(?i:[\u00c0-\u00de]+)",
            "([\\777]+)", "([\\400]+)", "([\\u0000-\\uD7FF\\uE000-\\uFFFF]+)",
            "a(?:[\\u0000-\\uD7FF\\uE000-\\uFFFF]|[\\uD800-\\uDBFF][\\uDC00-\\uDFFF])c",
            @"([a-z-[aeiou]]+)", @"([^a-z-[aeiou]]+)", @"([\w-[\d_]]+)", @"([^\w-[^\d]]+)",
            @"([\s-[\r\n]]+)", @"(?i:[A-Z-[k]]+)", @"([a-z-[d-w-[m-p]]]+)", @"([a-z-[^b-f]]+)",
            @"([\p{L}-[\p{Lu}]]+)", @"(?i:[\p{L}-[\p{Lu}]]+)", @"([\p{L}-[\P{IsBasicLatin}]]+)",
            "([\u00e9\u6f22-[\u6f22]]+)", "([^\u00e9-[^\u6f22]]+)",
            @"([\ud800-\udfff-[\ud800-\udbff]]+)", @"([^x-[\ud800-\udbff]]+)",
            @"([\u0000-\uFFFF-[\ud800-\udfff]]+)", @"([\u0000-\uFFFF-[\u0080-\uFFFF]]+)",
            @"([a-z-[a-z]]*)", @"([a-z-[a-z]]+)", "([\u00e9-[\u00e9-[\u00e9]]]+)",
            @"([\c@-\c_]+)", @"([\ca-\cz]+)", @"([\c]-\c_]+)", @"([\c@\c[\c\\c]\c^\c_]+)",
            @"([\cA-\cZ-[\cD-\cW-[\cM-\cP]]]+)",
            @"(?<=([a-z-[aeiou]]+))a", @"(?=([\w-[\d_]]+))", @"([a-z-[e]]+)\1",
            @"([a-z-[d-w-[m-p]]]+?)x", @"([a-z-[aeiou]]){2}", @"([a-z-[aeiou]])?",
            @"([\u0080-\uFFFF-[\p{Cs}]]+)", @"(?i:[\p{IsBasicLatin}-[k]])", @"(?i:[^\P{IsBasicLatin}-[k]])",
            @"[a-[b]]", @"[a-[\b]]", @"([^x-[a]]){2,}", @"([^x-[a]]){0,3}",
            @"[^x-[a]]{2,}", @"([\p{Cs}-[x]]){2,}", @"(?:[^x-[a]]){2,3}?",
            @"(?>([a-z-[e]]+))", @"(?<x>[a-z-[e]]+)(?<-x>[^a-z-[a]])",
    ];

    public static IEnumerable<object[]> Patterns()
    {
        // Theory string serialization replaces lone halves and merges case IDs.
        // Transport an ordinal; construct the exact UTF-16 pattern in the test.
        for (var pattern = 0; pattern < AtomPatterns.Length; pattern++)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var folded in pattern is 0 or 1 or 9 or 25 or 27 or 30 or 33 or 34 ? new[] { false, true } : new[] { false })
        {
            yield return [pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None) |
                (rtl ? RegexOptions.RightToLeft : RegexOptions.None) |
                (folded ? RegexOptions.IgnoreCase : RegexOptions.None)];
        }
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void UnicodeAtomsKeepOracleUnitsHistoriesAndTiling(int patternId, RegexOptions options)
    {
        var pattern = AtomPatterns[patternId];
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, options));
        var oracle = new Regex(pattern, OracleOptions(patternId, options), TimeSpan.FromSeconds(10));
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.True(oracle.IsMatch(text) == regex.IsMatch(input, source.Token),
                $"Pattern {patternId}, {options}, subject UTF-16 bytes {Convert.ToHexString(Encoding.Unicode.GetBytes(text))}; oracle Match {oracle.Match(text).Index}+{oracle.Match(text).Length}.");
            Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
            AssertMatch(text, oracle.Match(text), regex.Match(input, source.Token));
            AssertDetailed(oracle.Match(text), regex.MatchDetailed(input, source.Token));
            AssertSequence(text, oracle.EnumerateMatches(text), regex.EnumerateMatches(input, source.Token));
            foreach (var count in new[] { 0, 2 })
                AssertSplits(text, oracle.EnumerateSplits(text, count), regex.EnumerateSplits(input, count, source.Token));
            for (var offset = 0; offset <= text.Length; offset++)
            {
                Assert.Equal(oracle.IsMatch(text.AsSpan(), offset), regex.IsMatchFromUtf16Offset(input, offset, source.Token));
                Assert.Equal(oracle.Count(text.AsSpan(), offset), regex.CountFromUtf16Offset(input, offset, source.Token));
                AssertMatch(text, oracle.Match(text, offset), regex.MatchFromUtf16Offset(input, offset, source.Token));
                AssertDetailed(oracle.Match(text, offset), regex.MatchDetailedFromUtf16Offset(input, offset, source.Token));
                AssertSequence(text, oracle.EnumerateMatches(text.AsSpan(), offset), regex.EnumerateMatchesFromUtf16Offset(input, offset));
            }
            foreach (var template in new[] { "Q", "$&", "<$1>|$2|${\u00e9}", "\ud83d$&\ude00", "$`|$'|$_|$+" })
            {
                var expected = oracle.Replace(text, template);
                Assert.Equal(expected, regex.ReplaceToString(input, template, source.Token));
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
                var destination = new byte[expectedBytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
                Assert.Equal(expectedBytes, destination.AsSpan(0, written).ToArray());
            }
            AssertOwned(regex);
        }
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), regex.GetGroupNumbers());
        AssertOwned(regex);
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void UnicodeCallbacksKeepCaptureHistoriesAndByteAlignment(int patternId, RegexOptions options)
    {
        var pattern = AtomPatterns[patternId];
        var oracle = new Regex(pattern, OracleOptions(patternId, options), TimeSpan.FromSeconds(10));
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var expectedCalls = new List<string>();
            var expected = oracle.Replace(text, match =>
            {
                expectedCalls.Add(Signature(match));
                return match.Groups[^1].Value + "<" + match.Value + ">";
            });
            var calls = new List<string>();
            var input = Encoding.UTF8.GetBytes(text);
            var actual = regex.ReplaceToString(input, calls,
                static (in Utf8MatchContext context, ref List<string> state) =>
                {
                    state.Add(Signature(in context));
                    return context.GetGroup(context.GroupCount - 1).GetValueString() + "<" + context.GetValueString() + ">";
                }, source.Token);
            Assert.Equal(expected, actual);
            Assert.Equal(expectedCalls, calls);
            expectedCalls.Clear();
            calls.Clear();
            var unaligned = false;
            string? expectedByteString = null;
            try
            {
                expectedByteString = oracle.Replace(text, match =>
                {
                    if (!IsBoundary(text, match.Index) || !IsBoundary(text, match.Index + match.Length))
                        throw new InvalidOperationException();
                    expectedCalls.Add(Signature(match));
                    return "[" + Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(match.Groups[^1].Value)) + "]";
                });
            }
            catch (InvalidOperationException) { unaligned = true; }
            byte[]? actualBytes = null;
            var failure = Record.Exception(() => actualBytes = regex.Replace(input, calls,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<string> state) =>
                {
                    Assert.NotNull(Utf8SliceByteOutputScope.Current);
                    state.Add(Signature(in context));
                    writer.AppendAsciiByte((byte)'[');
                    writer.Append(context.GetGroup(context.GroupCount - 1).GetValueString().AsSpan());
                    writer.AppendAsciiByte((byte)']');
                }, source.Token));
            if (unaligned)
                Assert.Equal("The evaluator match is not aligned to UTF-8 scalar boundaries.", Assert.IsType<InvalidOperationException>(failure).Message);
            else
            {
                Assert.Null(failure);
                Assert.Equal(Encoding.UTF8.GetBytes(expectedByteString ?? ""), actualBytes);
            }
            Assert.Equal(expectedCalls, calls);
            AssertOwned(regex);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    private static string Signature(Match match)
    {
        var result = new StringBuilder($"{match.Index}:{match.Length}|");
        foreach (Group group in match.Groups)
        {
            result.Append($"{group.Success}:{group.Index}:{group.Length}:{group.Value}:");
            foreach (Capture capture in group.Captures) result.Append($"{capture.Index}:{capture.Length}:{capture.Value};");
            result.Append('|');
        }
        return result.ToString();
    }

    [Theory]
    [InlineData("([\\uFFFF]+)", "\uffff")]
    [InlineData("([^\\uFFFF]+)", "a")]
    [InlineData("([\\uFFFE-\\uFFFF]+)", "\ufffe\uffff")]
    [InlineData("([^\\uFFFE-\\uFFFF]+)", "ab")]
    public void CompiledMaximumUnitLoopsUseInterpretedSemantics(string pattern, string text)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, options));
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));
        AssertMatch(text, oracle.Match(text), regex.Match(input));
        AssertDetailed(oracle.Match(text), regex.MatchDetailed(input));
        AssertSequence(text, oracle.EnumerateMatches(text), regex.EnumerateMatches(input));
        Assert.Equal(oracle.Replace(text, "[$1]"), regex.ReplaceToString(input, "[$1]"));
        AssertOwned(regex);
    }

    private static RegexOptions OracleOptions(int patternId, RegexOptions options)
    {
        // .NET 10.0.12 compiled LTR [FFFF]+ and its complement use an
        // invalid emitted position. The approved contract follows interpreted
        // semantics for these loops instead of copying allocation-dependent
        // false misses or exceptions. Keep compiled oracles on other lanes.
        return patternId is 36 or 37 && (options & RegexOptions.RightToLeft) == 0
            ? options & ~RegexOptions.Compiled
            : options;
    }

    [Theory]
    [InlineData("(\\ud83d\\ude00)+", "\U0001f600")]
    [InlineData("([\\u00C0-\\u00FF])+", "\u00e9")]
    [InlineData("((?i:\\u00e9)+)", "\u00e9")]
    [InlineData("([\\ud800-\\udfff])+", "\U0001f600")]
    [InlineData(@"([a-z-[aeiou]])+", "b")]
    [InlineData(@"([\ud800-\udfff-[x]])+", "\U0001f600")]
    public void UnicodeAtomsObserveCancellationInsideTheVmAndRecover(string pattern, string unit)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(unit, 256)));
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        foreach (var operation in new[] { 0, 1, 2, 3, 4 })
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0);
            var regex = new Utf8Regex(pattern, options, Regex.InfiniteMatchTimeout);
            using var source = new CancellationTokenSource();
            var entered = false;
            var session = Utf8SearchDiagnosticsSession.Start(steps =>
            {
                entered = true;
                source.Cancel();
                if (steps >= 128) throw new InvalidOperationException("Unicode matching dropped its token.");
            });
            try
            {
                var failure = Assert.Throws<OperationCanceledException>(() =>
                {
                    switch (operation)
                    {
                        case 0: _ = regex.Count(input, source.Token); break;
                        case 1: _ = regex.MatchDetailed(input, source.Token); break;
                        case 2: _ = regex.EnumerateMatches(input, source.Token).MoveNext(); break;
                        case 3: _ = regex.ReplaceToString(input, "Q", source.Token); break;
                        case 4: _ = regex.Replace(input, 0,
                            static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref int state) => writer.AppendAsciiByte((byte)'Q'), source.Token); break;
                    }
                });
                Assert.True(entered);
                Assert.Equal(source.Token, failure.CancellationToken);
            }
            finally { session.Complete(); }
            Assert.Equal(new Regex(pattern, options).Count("\U0001f600\u00e9"), regex.Count("\U0001f600\u00e9"u8));
            AssertOwned(regex);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    private static string Signature(in Utf8MatchContext context)
    {
        var result = new StringBuilder($"{context.IndexInUtf16}:{context.LengthInUtf16}|");
        for (var number = 0; number < context.GroupCount; number++)
        {
            var group = context.GetGroup(number);
            result.Append($"{group.Success}:{group.IndexInUtf16}:{group.LengthInUtf16}:{group.GetValueString()}:");
            for (var index = 0; index < group.CaptureCount; index++)
            {
                var capture = group.GetCapture(index);
                result.Append($"{capture.IndexInUtf16}:{capture.LengthInUtf16}:{capture.GetValueString()};");
            }
            result.Append('|');
        }
        return result.ToString();
    }

    [Theory]
    [InlineData("(?i:\u00e9)")]
    [InlineData("(?i:\u212a)")]
    [InlineData("(?i:\u0130)")]
    [InlineData("(?i:\u03a3)")]
    [InlineData("(?i:[\u00c0-\u00de])")]
    [InlineData("(?i:[^\u212a])")]
    [InlineData("[\\000-\\777]")]
    [InlineData("[\\u007f-\\uFFFF]")]
    [InlineData("[\\uFFFF\\u0080-\\u0090\\u0088-\\u00ff]")]
    [InlineData(@"[a-z-[aeiou]]")]
    [InlineData(@"[^a-z-[aeiou]]")]
    [InlineData(@"[a-z-[d-w-[m-p]]]")]
    [InlineData(@"[\w-[\d_]]")]
    [InlineData(@"[^\w-[^\d]]")]
    [InlineData(@"[\s-[\r\n]]")]
    [InlineData(@"[\p{L}-[\p{Lu}]]")]
    [InlineData(@"(?i:[\p{L}-[\p{Lu}]])")]
    [InlineData(@"(?i:[\p{IsBasicLatin}-[k]])")]
    [InlineData(@"(?i:[^\P{IsBasicLatin}-[k]])")]
    [InlineData(@"[\u0000-\uFFFF-[\ud800-\udfff]]")]
    [InlineData(@"[\u0080-\uFFFF-[\p{Cs}]]")]
    [InlineData(@"[\c@-\c_]")]
    [InlineData(@"[\cA-\cZ-[\cD-\cW-[\cM-\cP]]]")]
    public void BmpMembershipAndFoldingAgreeAcrossTheEntireAlphabet(string pattern)
    {
        // Exclude lone surrogates from public input, retain them in the separate
        // unit/capture battery. One enumeration compares every other BMP unit.
        var text = string.Create(65536 - 2048, 0, static (chars, _) =>
        {
            var index = 0;
            for (var value = 0; value <= char.MaxValue; value++)
                if (value is < 0xD800 or > 0xDFFF) chars[index++] = (char)value;
        });
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.RightToLeft,
            RegexOptions.CultureInvariant | RegexOptions.Compiled, RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.RightToLeft })
        {
            var oracle = new Regex(pattern, options, TimeSpan.FromSeconds(20));
            var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(20));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            AssertSequence(text, oracle.EnumerateMatches(text), regex.EnumerateMatches(input));
            AssertOwned(regex);
        }
    }

    private static void AssertOwned(Utf8Regex regex)
    {
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
    }

    private static void AssertMatch(string text, Match expected, Utf8ValueMatch actual)
    {
        AssertRange(text, Utf8BoundaryMap.Create(Encoding.UTF8.GetBytes(text)), expected.Success, expected.Index, expected.Length, actual);
    }

    private static void AssertRange(string text, Utf8BoundaryMap map, bool success, int start, int length, Utf8ValueMatch actual)
    {
        Assert.Equal(success, actual.Success);
        Assert.Equal(start, actual.IndexInUtf16);
        Assert.Equal(length, actual.LengthInUtf16);
        if (!success) return;
        var aligned = IsBoundary(text, start) && IsBoundary(text, start + length);
        Assert.Equal(aligned, actual.IsByteAligned);
        Assert.Equal(aligned, actual.TryGetByteRange(out var byteStart, out var byteLength));
        if (aligned)
        {
            Assert.Equal(map.Resolve(start).ByteOffset, byteStart);
            Assert.Equal(map.Resolve(start + length).ByteOffset - byteStart, byteLength);
        }
    }

    private static bool IsBoundary(string text, int index) => index == 0 || index == text.Length ||
        !char.IsHighSurrogate(text[index - 1]) || !char.IsLowSurrogate(text[index]);

    private static void AssertDetailed(Match expected, Utf8MatchContext actual)
    {
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Assert.Equal(expected.Value, actual.GetValueString());
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        for (var number = 0; number < expected.Groups.Count; number++)
        {
            var group = expected.Groups[number];
            var actualGroup = actual.GetGroup(number);
            Assert.Equal(group.Success, actualGroup.Success);
            Assert.Equal(group.Index, actualGroup.IndexInUtf16);
            Assert.Equal(group.Length, actualGroup.LengthInUtf16);
            Assert.Equal(group.Value, actualGroup.GetValueString());
            Assert.Equal(group.Captures.Count, actualGroup.CaptureCount);
            for (var index = 0; index < group.Captures.Count; index++)
            {
                var capture = group.Captures[index];
                var actualCapture = actualGroup.GetCapture(index);
                Assert.Equal(capture.Index, actualCapture.IndexInUtf16);
                Assert.Equal(capture.Length, actualCapture.LengthInUtf16);
                Assert.Equal(capture.Value, actualCapture.GetValueString());
            }
        }
    }

    private static void AssertSequence(string text, Regex.ValueMatchEnumerator expected, Utf8ValueMatchEnumerator actual)
    {
        var seen = 0;
        var map = Utf8BoundaryMap.Create(Encoding.UTF8.GetBytes(text));
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            AssertRange(text, map, true, expected.Current.Index, expected.Current.Length, actual.Current);
            Assert.True(++seen <= text.Length * 2 + 10);
        }
        Assert.False(actual.MoveNext());
    }

    private static void AssertSplits(string text, Regex.ValueSplitEnumerator expected, Utf8ValueSplitEnumerator actual)
    {
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            var (start, length) = expected.Current.GetOffsetAndLength(text.Length);
            Assert.Equal(start, actual.Current.IndexInUtf16);
            Assert.Equal(length, actual.Current.LengthInUtf16);
            Assert.Equal(text.Substring(start, length), actual.Current.GetValueString());
            Assert.Equal(IsBoundary(text, start) && IsBoundary(text, start + length), actual.Current.IsByteAligned);
        }
        Assert.False(actual.MoveNext());
    }
}
