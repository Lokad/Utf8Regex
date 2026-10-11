using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceUnicodeAtomTests
{
    private static readonly string[] Texts = ["", "abc", "\u00e9\u00e9a\u00c9", "\u6f22\u6f22a",
        "\U0001f600", "a\U0001f600b", "\U0001f600\U0001f600b", "x\U0001f600ab",
        "\u00e9\U00010400a\u00e9\u00e9", "\u212aKk\u0130Ii\u0131", "\u03a3\u03c3\u03c2\u2126\u03c9",
        "\u00c5\u00e5\u212b\u0178\u00ff", "\u0000\u0080\u00ff\uffff\n", "a\n\U0001f600b",
        "bcdAEIOUxyz 123_\t\r\n", "\u0000\u0001\u0008\u001a\u001b\u001c\u001d\u001e\u001fA", "aa abab abbc bbcc xaa aabbaa abcabc cabcab abcaac"];

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
            @"(?<x>[a-z-[e]]+)\k<x>", @"(?'x'a+)\k'x'", @"\k<x>(?<x>a)", @"\1(a)",
            @"(?<x>a)\k<1>", @"(?<x>a)\k<01>", @"(?<x>a)(b)\k<x>\1",
            @"(?<x>a)(?<x>b)", @"(?<x>a|b)+\k<x>", @"(?<x>a)(?<x>b)\k<x>",
            @"(?:(?<x>a)|(?<x>b))+\k<x>", @"(?<x>(?<x>a)b)\k<x>",
            @"(?<x>a)(?<x>b)?\k<x>", @"(?<x>a)(?<x>b*)\k<x>",
            @"(?<x>a)(?<x>b)c|(?<x>a)\k<x>", @"(?>(?<x>a)(?<x>b))\k<x>",
            @"(?<x>a)(?<x>b)(?<-x>c)(?(x)\k<x>|d)",
            @"(?<x>a)(?<y>b)(?<y-x>c)\k<y>",
            @"(?=(?<x>a))(?<x>a)\k<x>", @"(?<x>a)(?<x>b)(?(x)c|d)",
            "(?<\u00e9>\u00e9+)\\k<\u00e9>", "(?<x>\u00e9)(?<x>\u6f22)\\k<x>",
            "(?i:(?<x>\u00e9+)\\k<x>)", "(?<x>\u212a)(?i:\\k<x>)",
            "(?<x>\ud83d)\\k<x>", "(?<x>\ude00+)\\k<x>", "(?<x>\U0001f600)\\k<x>",
            "(?<x>\ud83d)(?<x>\ude00)\\k<x>", "(?<x>\ud83d)|(?<x>\ude00)",
            @"(?<x>[^x-[a]]){2}\k<x>", @"(?<x>a)(?<x>b)\1", @"\k'x'(?<x>a)",
            @"(?<x>c(?<x>a)b)\k<x>", @"(?<x>a)(?<x>b\k<x>)", @"(?<x>a)(?<x>\k<x>b)",
            @"(?<x>a)(?<x>b(?(x)\k<x>|c))", @"(?<x>(?(x)\k<x>|a))+",
            @"(?<x>a)(?<x>b(?=(?<x>c)))\k<x>",
            @"(?<x>c(?<x>a)b)", @"(?<x>c(?<x>a)(?<x>b)d)",
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
    public void UnicodeAtomsKeepScalarRangesHistoriesAndTiling(int patternId, RegexOptions options)
    {
        var pattern = AtomPatterns[patternId];
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, options));
        var oracle = CreateFixtureOracle(patternId, options);
        var metadata = new Regex(pattern, options & ~RegexOptions.Compiled);
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var subject = new FixtureSubject(text);
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.IsMatch(subject.Projected), regex.IsMatch(input, source.Token));
            Assert.Equal(oracle.Count(subject.Projected), regex.Count(input, source.Token));
            AssertMatch(subject, oracle.Match(subject.Projected), regex.Match(input, source.Token));
            AssertDetailed(subject, oracle.Match(subject.Projected), regex.MatchDetailed(input, source.Token));
            AssertSequence(subject, oracle.EnumerateMatches(subject.Projected), regex.EnumerateMatches(input, source.Token));
            foreach (var count in new[] { 0, 2 })
                AssertSplits(subject, oracle.EnumerateSplits(subject.Projected, count), regex.EnumerateSplits(input, count, source.Token));
            for (var offset = 0; offset <= text.Length; offset++)
            {
                var scalarOffset = Array.BinarySearch(subject.Utf16Boundaries, offset);
                if (scalarOffset < 0)
                {
                    AssertInteriorStartRejected(regex, input, offset, source.Token);
                    continue;
                }
                Assert.Equal(oracle.IsMatch(subject.Projected.AsSpan(), scalarOffset), regex.IsMatchFromUtf16Offset(input, offset, source.Token));
                Assert.Equal(oracle.Count(subject.Projected.AsSpan(), scalarOffset), regex.CountFromUtf16Offset(input, offset, source.Token));
                AssertMatch(subject, oracle.Match(subject.Projected, scalarOffset), regex.MatchFromUtf16Offset(input, offset, source.Token));
                AssertDetailed(subject, oracle.Match(subject.Projected, scalarOffset), regex.MatchDetailedFromUtf16Offset(input, offset, source.Token));
                AssertSequence(subject, oracle.EnumerateMatches(subject.Projected.AsSpan(), scalarOffset), regex.EnumerateMatchesFromUtf16Offset(input, offset));
            }
            foreach (var template in new[] { "Q", "$&", "<${x}>|${x}${x}", "<$1>|$2|${\u00e9}", "\ud83d$&\ude00", "$`|$'|$_|$+" })
            {
                // Literal fragments encode independently: these two lone halves
                // become U+FFFD before substitution, including an empty match.
                var scalarTemplate = Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(template));
                var expected = subject.Restore(oracle.Replace(subject.Projected, scalarTemplate));
                Assert.Equal(expected, regex.ReplaceToString(input, template, source.Token));
                var expectedBytes = Encoding.UTF8.GetBytes(expected);
                Assert.Equal(expectedBytes, regex.Replace(input, template, source.Token));
                var destination = new byte[expectedBytes.Length + 1];
                Assert.Equal(OperationStatus.Done, regex.TryReplace(input, template, destination, out var written, source.Token));
                Assert.Equal(expectedBytes, destination.AsSpan(0, written).ToArray());
            }
            AssertOwned(regex);
        }
        Assert.Equal(metadata.GetGroupNames(), oracle.GetGroupNames());
        Assert.Equal(metadata.GetGroupNumbers(), oracle.GetGroupNumbers());
        Assert.Equal(metadata.GetGroupNames(), regex.GetGroupNames());
        Assert.Equal(metadata.GetGroupNumbers(), regex.GetGroupNumbers());
        AssertOwned(regex);
    }

    [Theory]
    [MemberData(nameof(Patterns))]
    public void UnicodeCallbacksKeepCaptureHistoriesAndByteAlignment(int patternId, RegexOptions options)
    {
        var oracle = CreateFixtureOracle(patternId, options);
        var regex = new Utf8Regex(AtomPatterns[patternId], options, TimeSpan.FromSeconds(10));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var subject = new FixtureSubject(text);
            var expectedCalls = new List<string>();
            var expected = subject.Restore(oracle.Replace(subject.Projected, match =>
            {
                expectedCalls.Add(Signature(subject, match));
                return match.Groups[^1].Value + "<" + match.Value + ">";
            }));
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
            var expectedByteString = subject.Restore(oracle.Replace(subject.Projected, match =>
            {
                expectedCalls.Add(Signature(subject, match));
                return "[" + match.Groups[^1].Value + "]";
            }));
            var actualBytes = regex.Replace(input, calls,
                static (in Utf8MatchContext context, ref Utf8ReplacementWriter writer, ref List<string> state) =>
                {
                    Assert.NotNull(Utf8SliceByteOutputScope.Current);
                    state.Add(Signature(in context));
                    writer.AppendAsciiByte((byte)'[');
                    writer.Append(context.GetGroup(context.GroupCount - 1).GetValueString().AsSpan());
                    writer.AppendAsciiByte((byte)']');
                }, source.Token);
            Assert.Equal(Encoding.UTF8.GetBytes(expectedByteString), actualBytes);
            Assert.Equal(expectedCalls, calls);
            AssertOwned(regex);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    private static string Signature(FixtureSubject subject, Match match)
    {
        var (start, length, byteStart, byteLength) = subject.Coordinates(match.Index, match.Length);
        var result = new StringBuilder($"{byteStart}:{byteLength}:{start}:{length}|");
        foreach (Group group in match.Groups)
        {
            (start, length, byteStart, byteLength) = subject.Coordinates(group.Index, group.Length);
            result.Append($"{group.Success}:{byteStart}:{byteLength}:{start}:{length}:{subject.Value(group.Index, group.Length)}:");
            foreach (Capture capture in group.Captures)
            {
                (start, length, byteStart, byteLength) = subject.Coordinates(capture.Index, capture.Length);
                result.Append($"{byteStart}:{byteLength}:{start}:{length}:{subject.Value(capture.Index, capture.Length)};");
            }
            result.Append('|');
        }
        return result.ToString();
    }

    [Theory]
    [InlineData("([\\uFFFF]+)", "\uffff")]
    [InlineData("([^\\uFFFF]+)", "a")]
    [InlineData("([\\uFFFE-\\uFFFF]+)", "\ufffe\uffff")]
    [InlineData("([^\\uFFFE-\\uFFFF]+)", "ab")]
    public void CompiledMaximumBmpLoopsUseInterpretedSemantics(string pattern, string text)
    {
        var options = RegexOptions.CultureInvariant | RegexOptions.Compiled;
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(Utf8SliceParser.TryParse(pattern, options));
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        var input = Encoding.UTF8.GetBytes(text);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(oracle.Count(text), regex.Count(input));
        AssertMatch(text, oracle.Match(text), regex.Match(input));
        AssertDetailed(new FixtureSubject(text, projectSupplementary: false), oracle.Match(text), regex.MatchDetailed(input));
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
    [InlineData("(\\ud83d\\ude00)+", "\U0001f600", 1)]
    [InlineData("([\\u00C0-\\u00FF])+", "\u00e9", 1)]
    [InlineData("((?i:\\u00e9)+)", "\u00e9", 1)]
    [InlineData("([\\ud800-\\udfff])+", "\U0001f600", 0)]
    [InlineData(@"([a-z-[aeiou]])+", "b", 0)]
    [InlineData(@"([\ud800-\udfff-[x]])+", "\U0001f600", 0)]
    public void UnicodeAtomsObserveCancellationInsideTheVmAndRecover(string pattern, string unit, int recoveryCount)
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
            Assert.Equal(recoveryCount, regex.Count("\U0001f600\u00e9"u8));
            AssertOwned(regex);
            Assert.Null(Utf8SliceByteOutputScope.Current);
        }
    }

    private static string Signature(in Utf8MatchContext context)
    {
        Assert.True(context.IsByteAligned);
        var result = new StringBuilder($"{context.IndexInBytes}:{context.LengthInBytes}:{context.IndexInUtf16}:{context.LengthInUtf16}|");
        for (var number = 0; number < context.GroupCount; number++)
        {
            var group = context.GetGroup(number);
            if (group.Success) Assert.True(group.IsByteAligned);
            result.Append($"{group.Success}:{(group.Success ? group.IndexInBytes : 0)}:{(group.Success ? group.LengthInBytes : 0)}:{group.IndexInUtf16}:{group.LengthInUtf16}:{group.GetValueString()}:");
            for (var index = 0; index < group.CaptureCount; index++)
            {
                var capture = group.GetCapture(index);
                Assert.True(capture.IsByteAligned);
                result.Append($"{capture.IndexInBytes}:{capture.LengthInBytes}:{capture.IndexInUtf16}:{capture.LengthInUtf16}:{capture.GetValueString()};");
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
        // Valid UTF-8 excludes surrogate code points. Impossible surrogate atoms
        // remain covered in the scalar-pattern matrix; compare every BMP scalar here.
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static IEnumerable<object[]> ScalarWitnesses()
    {
        const string alphabet = "a😀𐐀é漢";
        (int Pattern, string Text, int[] Forward, int[]? Reverse)[] rows =
        [
            (9, "😀😀b", [0, 8], null),
            (10, "😀😀b", [0, 8], null),
            (11, "😀😀b", [0, 9], null),
            (12, "a😀😀b", [0, 10], null),
            (25, alphabet, [9, 2], null),
            (26, alphabet, [0, 9, 11, 3], null),
            (27, alphabet, [9, 2], null),
            (28, alphabet, [], null),
            (29, alphabet, [0, 1, 11, 3], null),
            (30, alphabet, [0, 1, 5, 9], null),
            (31, alphabet, [0, 1, 5, 4, 11, 3], null),
            (34, "a😀😀b", [0, 10], null),
            (35, alphabet, [0, 14], null),
            (36, alphabet, [], null),
            (37, alphabet, [0, 14], null),
            (39, alphabet, [0, 14], null),
            (41, alphabet, [9, 2], null),
            (44, alphabet, [0, 1, 9, 5], null),
            (45, "a😀c", [], null),
            (45, "a𐐀c", [], null),
            (45, "aéc", [0, 4], null),
            (46, alphabet, [], null),
            (47, alphabet, [1, 13], null),
            (48, alphabet, [0, 1, 5, 9], null),
            (49, alphabet, [], null),
            (51, alphabet, [0, 1], null),
            (52, alphabet, [0, 1], null),
            (53, alphabet, [], null),
            (54, alphabet, [0, 1, 9, 5], null),
            (55, alphabet, [11, 3], null),
            (56, alphabet, [0, 1], null),
            (57, alphabet, [9, 2], null),
            (58, alphabet, [11, 3], null),
            (60, alphabet, [0, 14], null),
            (61, alphabet, [0, 1, 9, 5], null),
            (62, alphabet, [0, 1], null),
            (63, alphabet, [0, 0, 1, 0, 5, 0, 9, 0, 11, 0, 14, 0], null),
            (64, alphabet, [], null),
            (65, alphabet, [9, 2], null),
            (72, alphabet, [0, 0, 5, 0, 9, 0, 11, 0], null),
            (76, alphabet, [0, 0, 1, 0, 5, 0, 9, 0, 11, 0, 14, 0], null),
            (77, alphabet, [9, 5], null),
            (78, alphabet, [0, 1], null),
            (79, alphabet, [0, 1], null),
            (82, alphabet, [1, 13], null),
            (83, "😀😀", [0, 8, 8, 0], [0, 8, 0, 0]),
            (84, alphabet, [1, 13], null),
            (86, alphabet, [1, 8, 9, 5], null),
            (115, "😀😀", [0, 8], []),
            (118, "😀😀😀", [0, 12], []),
        ];
        foreach (var row in rows)
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
        {
            var coordinates = row.Forward;
            if (rtl) coordinates = row.Reverse ?? ReverseRanges(row.Forward);
            yield return [row.Pattern, row.Text, coordinates, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0)];
        }
        foreach (var pattern in new[] { 13, 14, 15, 16, 17, 18, 19, 32, 33, 59, 85, 113, 114, 116, 117 })
        foreach (var compiled in new[] { false, true })
        foreach (var rtl in new[] { false, true })
            yield return [pattern, alphabet, Array.Empty<int>(), RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : 0) | (rtl ? RegexOptions.RightToLeft : 0)];
    }

    private static int[] ReverseRanges(int[] coordinates) => Enumerable.Range(0, coordinates.Length / 2)
        .Reverse().SelectMany(index => new[] { coordinates[index * 2], coordinates[index * 2 + 1] }).ToArray();

    [Theory]
    [MemberData(nameof(ScalarWitnesses))]
    public void UnicodeScalarWitnessesIndependentlyQualifyMembershipAndWidths(int patternId, string text, int[] coordinates, RegexOptions options)
    {
        var expected = Enumerable.Range(0, coordinates.Length / 2)
            .Select(index => (coordinates[index * 2], coordinates[index * 2 + 1])).ToArray();
        var subject = new FixtureSubject(text);
        var oracle = CreateFixtureOracle(patternId, options);
        var projected = oracle.Matches(subject.Projected).Select(match =>
        {
            var range = subject.Coordinates(match.Index, match.Length);
            return (range.ByteStart, range.ByteLength);
        }).ToArray();
        Assert.Equal(expected, projected);
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(AtomPatterns[patternId], options);
        using var source = new CancellationTokenSource();
        Assert.Equal(expected.Length != 0, regex.IsMatch(input, source.Token));
        Assert.Equal(expected.Length, regex.Count(input, source.Token));
        var cursor = regex.EnumerateMatches(input, source.Token);
        foreach (var range in expected)
        {
            Assert.True(cursor.MoveNext());
            Assert.True(cursor.Current.IsByteAligned);
            Assert.Equal(range, (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
        }
        Assert.False(cursor.MoveNext());
        var detailed = regex.MatchDetailed(input, source.Token);
        AssertDetailed(subject, oracle.Match(subject.Projected), detailed);
        AssertOwned(regex);
    }

    // This comparator is qualified for the frozen alphabet above: emoji (So)
    // and Deseret capital U+10400 (Lu) are the only supplementary subjects.
    // The grammar tests L/Lu/Cs, word/digit/space and BasicLatin membership;
    // U+E000 (non-word) and Greek capital alpha (Lu) preserve those predicates.
    // Explicit BMP-only classes must exclude both markers. This finite mapping
    // is not a general Unicode regex implementation; extend its direct witnesses
    // before extending the pattern/subject alphabet.
    private static Regex CreateFixtureOracle(int patternId, RegexOptions options)
    {
        var pattern = AtomPatterns[patternId].Replace("\U0001f600", "\ue000", StringComparison.Ordinal)
            .Replace(@"[\u0000-\uD7FF\uE000-\uFFFF]", @"[\u0000-\uD7FF\uE000-\uFFFF-[\u0391\uE000]]", StringComparison.Ordinal)
            .Replace(@"[\u0000-\uFFFF-[\ud800-\udfff]]", @"[\u0000-\uFFFF-[\ud800-\udfff\u0391\uE000]]", StringComparison.Ordinal)
            .Replace(@"[\u0080-\uFFFF-[\p{Cs}]]", @"[\u0080-\uFFFF-[\p{Cs}\u0391\uE000]]", StringComparison.Ordinal);
        return new Regex(pattern, OracleOptions(patternId, options), TimeSpan.FromSeconds(10));
    }

    private sealed class FixtureSubject
    {
        internal readonly string Original;
        internal readonly string Projected;
        internal readonly int[] Utf16Boundaries;
        internal readonly int[] ByteBoundaries;

        internal FixtureSubject(string text, bool projectSupplementary = true)
        {
            Original = text;
            var projected = new StringBuilder();
            var utf16 = new List<int> { 0 };
            var bytes = new List<int> { 0 };
            foreach (var scalar in text.EnumerateRunes())
            {
                if (projectSupplementary)
                {
                    Assert.False(scalar.Value is 0x0391 or 0xe000);
                    if (scalar.IsBmp) projected.Append((char)scalar.Value);
                    else
                    {
                        Assert.True(scalar.Value is 0x1f600 or 0x10400);
                        projected.Append(scalar.Value == 0x1f600 ? '\ue000' : '\u0391');
                    }
                }
                else
                {
                    Assert.True(scalar.IsBmp);
                    projected.Append((char)scalar.Value);
                }
                utf16.Add(utf16[^1] + scalar.Utf16SequenceLength);
                bytes.Add(bytes[^1] + scalar.Utf8SequenceLength);
            }
            Projected = projected.ToString();
            Utf16Boundaries = utf16.ToArray();
            ByteBoundaries = bytes.ToArray();
        }

        internal (int Start, int Length, int ByteStart, int ByteLength) Coordinates(int index, int length) =>
            (Utf16Boundaries[index], Utf16Boundaries[index + length] - Utf16Boundaries[index],
             ByteBoundaries[index], ByteBoundaries[index + length] - ByteBoundaries[index]);

        internal string Value(int index, int length) =>
            Original.Substring(Utf16Boundaries[index], Utf16Boundaries[index + length] - Utf16Boundaries[index]);

        internal string Restore(string value) => value.Replace("\ue000", "\U0001f600", StringComparison.Ordinal)
            .Replace("\u0391", "\U00010400", StringComparison.Ordinal);
    }

    private static void AssertInteriorStartRejected(Utf8Regex regex, byte[] input, int offset, CancellationToken token)
    {
        Action[] operations =
        [
            () => { _ = regex.IsMatchFromUtf16Offset(input, offset, token); },
            () => { _ = regex.CountFromUtf16Offset(input, offset, token); },
            () => { _ = regex.MatchFromUtf16Offset(input, offset, token); },
            () => { _ = regex.MatchDetailedFromUtf16Offset(input, offset, token); },
            () => { _ = regex.EnumerateMatchesFromUtf16Offset(input, offset, token); },
        ];
        foreach (var operation in operations)
            Assert.Equal("utf16Offset", Assert.Throws<ArgumentOutOfRangeException>(operation).ParamName);
    }

    private static void AssertMatch(string text, Match expected, Utf8ValueMatch actual) =>
        AssertMatch(new FixtureSubject(text, projectSupplementary: false), expected, actual);

    private static void AssertMatch(FixtureSubject subject, Match expected, Utf8ValueMatch actual) =>
        AssertRange(subject, expected.Success, expected.Index, expected.Length, actual);

    private static void AssertRange(FixtureSubject subject, bool success, int index, int length, Utf8ValueMatch actual)
    {
        var expected = subject.Coordinates(index, length);
        Assert.Equal(success, actual.Success);
        Assert.Equal(expected.Start, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        if (!success) return;
        Assert.True(actual.IsByteAligned);
        Assert.True(actual.TryGetByteRange(out var byteStart, out var byteLength));
        Assert.Equal((expected.ByteStart, expected.ByteLength), (byteStart, byteLength));
    }

    private static void AssertDetailed(FixtureSubject subject, Match expected, Utf8MatchContext actual)
    {
        var range = subject.Coordinates(expected.Index, expected.Length);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal((range.Start, range.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
        Assert.Equal(subject.Value(expected.Index, expected.Length), actual.GetValueString());
        if (expected.Success)
        {
            Assert.True(actual.IsByteAligned);
            Assert.Equal((range.ByteStart, range.ByteLength), (actual.IndexInBytes, actual.LengthInBytes));
        }
        Assert.Equal(expected.Groups.Count, actual.GroupCount);
        for (var number = 0; number < expected.Groups.Count; number++)
        {
            var group = expected.Groups[number];
            var actualGroup = actual.GetGroup(number);
            range = subject.Coordinates(group.Index, group.Length);
            Assert.Equal(group.Success, actualGroup.Success);
            Assert.Equal((range.Start, range.Length), (actualGroup.IndexInUtf16, actualGroup.LengthInUtf16));
            Assert.Equal(subject.Value(group.Index, group.Length), actualGroup.GetValueString());
            if (group.Success)
            {
                Assert.True(actualGroup.IsByteAligned);
                Assert.Equal((range.ByteStart, range.ByteLength), (actualGroup.IndexInBytes, actualGroup.LengthInBytes));
            }
            Assert.Equal(group.Captures.Count, actualGroup.CaptureCount);
            for (var index = 0; index < group.Captures.Count; index++)
            {
                var capture = group.Captures[index];
                var actualCapture = actualGroup.GetCapture(index);
                range = subject.Coordinates(capture.Index, capture.Length);
                Assert.True(actualCapture.IsByteAligned);
                Assert.Equal((range.Start, range.Length), (actualCapture.IndexInUtf16, actualCapture.LengthInUtf16));
                Assert.Equal((range.ByteStart, range.ByteLength), (actualCapture.IndexInBytes, actualCapture.LengthInBytes));
                Assert.Equal(subject.Value(capture.Index, capture.Length), actualCapture.GetValueString());
            }
        }
    }

    private static void AssertSequence(string text, Regex.ValueMatchEnumerator expected, Utf8ValueMatchEnumerator actual) =>
        AssertSequence(new FixtureSubject(text, projectSupplementary: false), expected, actual);

    private static void AssertSequence(FixtureSubject subject, Regex.ValueMatchEnumerator expected, Utf8ValueMatchEnumerator actual)
    {
        var seen = 0;
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            AssertRange(subject, true, expected.Current.Index, expected.Current.Length, actual.Current);
            Assert.True(++seen <= subject.Projected.Length * 2 + 10);
        }
        Assert.False(actual.MoveNext());
    }

    private static void AssertSplits(FixtureSubject subject, Regex.ValueSplitEnumerator expected, Utf8ValueSplitEnumerator actual)
    {
        while (expected.MoveNext())
        {
            Assert.True(actual.MoveNext());
            var (start, length) = expected.Current.GetOffsetAndLength(subject.Projected.Length);
            var range = subject.Coordinates(start, length);
            Assert.Equal((range.Start, range.Length), (actual.Current.IndexInUtf16, actual.Current.LengthInUtf16));
            Assert.Equal((range.ByteStart, range.ByteLength), (actual.Current.IndexInBytes, actual.Current.LengthInBytes));
            Assert.Equal(subject.Value(start, length), actual.Current.GetValueString());
            Assert.True(actual.Current.IsByteAligned);
        }
        Assert.False(actual.MoveNext());
    }
}
