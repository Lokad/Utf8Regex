using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixGreedyAsciiRunTests
{
    [Fact]
    public void NewlineAnchorsSelectEachCompleteDisjointRunPair()
    {
        var regex = new Utf8PosixRegex("^([a-z]+)([0-9]+)$", options: PosixCompileOptions.Newline, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = default
        });
        var captures = new Utf8CaptureRange[3];
        var whole = regex.Match("bad\nsku007\nx9\n"u8, captures: captures);
        Assert.Equal((4, 6), (whole.StartOffsetInBytes, (whole.EndOffsetInBytes - whole.StartOffsetInBytes)));
        Assert.Equal((4, 3), (captures[1].StartOffsetInBytes, (captures[1].EndOffsetInBytes - captures[1].StartOffsetInBytes)));
        Assert.Equal((7, 3), (captures[2].StartOffsetInBytes, (captures[2].EndOffsetInBytes - captures[2].StartOffsetInBytes)));
        Assert.Equal(2, regex.Count("bad\nsku007\nx9\n"u8));
    }

    [Fact]
    public void OnlyProvedFlatAsciiRunsAdmitGreedyBoundaries()
    {
        Assert.True(PosixProgram.Compile("^([[:alpha:]]+)([0-9]+)$", default).FirstAcceptanceIsLongest);
        Assert.True(PosixProgram.Compile("([a-z]{1,3})([0-9]{2,4})([!]{1,2})", default).FirstAcceptanceIsLongest);
        foreach (var pattern in new[] { "([a-z]+)[0-9]+", "[a-z]+([0-9]+)",
            "[a-z]+[0-9]+", "(([a-z]+))[0-9]{2,4}", "(a*)(a*)",
            "([a-z]*)([0-9]+)", "([a-z]+)([a-z0-9]*)", "a*([a-b]*)",
            "(a+)()", "Z(a+)()", "ZZ(a+)ZZ()", "[Z](a{1,3})[Z]()", "(a+)Z()" })
            Assert.True(PosixProgram.Compile(pattern, default).DirectGreedyRuns);
        // The older homogeneous-literal proof can still accept (a*)(a+),
        // but its required overlapping tail cannot use the direct run loop.
        Assert.False(PosixProgram.Compile("(a*)(a+)", default).DirectGreedyRuns);
        foreach (var pattern in new[] { "Z(a+)a()", "Z([a-b]*)a()", "Z(a)+()", "^Z(a*)$" })
            Assert.False(PosixProgram.Compile(pattern, default).DirectGreedyRuns);
        Assert.False(PosixProgram.Compile("Z(k+)K()", PosixCompileOptions.IgnoreCase).DirectGreedyRuns);
        foreach (var pattern in new[] { "([a-z]+)([a-z0-9]+)",
            "(a*)^(a*)", "(a*)^(b*)", "^([a-z]*)([0-9]+)$",
            "([a-zé]+)([0-9]+)", "([^0-9]+)([0-9]+)", "([a-z])+([0-9]+)", "([a-z]+)|([0-9]+)",
            "([a-z])+[0-9]+", "[a-z]+([0-9])+", "[a-z]+([0-9]+)+", "([a-z]+[0-9]+)+" })
            Assert.False(PosixProgram.Compile(pattern, default).FirstAcceptanceIsLongest);
    }

    [Fact]
    public void FoldedRunProofUsesScalarExecutionAndDeclinesUncertainSplits()
    {
        foreach (var pattern in new[] { "(k+)([0-9]+)", "([K-M]{1,3})([0-9]{2,4})", "([k]*)()([K]*)" })
        {
            var program = PosixProgram.Compile(pattern, PosixCompileOptions.IgnoreCase);
            Assert.True(program.FirstAcceptanceIsLongest);
            Assert.True(program.DirectGreedyRuns);
        }
        foreach (var pattern in new[] { "([k]+)([K]+)", "([k]*)^([0-9]+)", "([k])+([0-9]+)",
            "([K]+)([0-9]+)", "([^k]+)([0-9]+)", "(k|kk)(k*)" })
            Assert.False(PosixProgram.Compile(pattern, PosixCompileOptions.IgnoreCase).FirstAcceptanceIsLongest);
        var rewind = PosixProgram.Compile("^([a-z]+)([[:space:]]+)$", PosixCompileOptions.Newline);
        Assert.True(rewind.FirstAcceptanceIsLongest);
        Assert.False(rewind.DirectGreedyRuns);
    }

    [Theory]
    [InlineData("a \nb", 2, 1)]
    [InlineData("a \n\nb", 3, 2)]
    [InlineData("a \n", 3, 2)]
    public void LineEndRewindSelectsTheLatestValidEndpoint(string text, int length, int whitespace)
    {
        var regex = new Utf8PosixRegex("^([a-z]+)([[:space:]]+)$", options: PosixCompileOptions.Newline);
        var groups = new Utf8CaptureRange[3];
        var whole = regex.Match(Encoding.UTF8.GetBytes(text), captures: groups);
        Assert.True(whole.Success);
        Assert.Equal((0, length), (whole.StartOffsetInBytes, (whole.EndOffsetInBytes - whole.StartOffsetInBytes)));
        Assert.Equal((0, 1), (groups[1].StartOffsetInBytes, (groups[1].EndOffsetInBytes - groups[1].StartOffsetInBytes)));
        Assert.Equal((1, whitespace), (groups[2].StartOffsetInBytes, (groups[2].EndOffsetInBytes - groups[2].StartOffsetInBytes)));
    }

    [Theory]
    [InlineData("([a\n]+)^()", "a\na", PosixCompileOptions.Newline, 2)]
    [InlineData("([k\n]+)^()", "K\nK", PosixCompileOptions.Newline | PosixCompileOptions.IgnoreCase, 4)]
    public void LineBeginningAfterMixedRunCanRewind(string pattern, string text, PosixCompileOptions options, int end)
    {
        using var regex = new Utf8PosixRegex(pattern, options: options);
        var groups = new Utf8CaptureRange[3];
        var input = Encoding.UTF8.GetBytes(text);
        var whole = regex.Match(input, captures: groups);
        Assert.True(whole.Success);
        Assert.Equal((0, end), (whole.StartOffsetInBytes, whole.EndOffsetInBytes));
        Assert.Equal((0, end), (groups[1].StartOffsetInBytes, groups[1].EndOffsetInBytes));
        Assert.Equal((end, end), (groups[2].StartOffsetInBytes, groups[2].EndOffsetInBytes));
        Assert.Equal(1, regex.Count(input));
    }

    [Theory]
    [InlineData("^([a]+)([[:space:]]+)$", PosixCompileOptions.Newline)]
    [InlineData("([a]+)([[:space:]]+)$", PosixCompileOptions.Newline)]
    [InlineData("^(([a]{1,2}))([[:space:]]{1,3})$", PosixCompileOptions.Newline)]
    [InlineData("([a]+)$([\n]+)", PosixCompileOptions.Newline)]
    [InlineData("([a]+)$([\n]+)$", PosixCompileOptions.Newline)]
    [InlineData("([a]+)([\n]+)^", PosixCompileOptions.Newline)]
    [InlineData("([\n]+)^([a]+)$", PosixCompileOptions.Newline)]
    [InlineData("^([k]+)([[:space:]]+)$", PosixCompileOptions.Newline | PosixCompileOptions.IgnoreCase)]
    public void LineEndScalarRewindAgreesWithGeneralSelectionAtEveryFloorAndOutputWidth(string pattern, PosixCompileOptions options)
    {
        // In Newline mode $. cannot consume: $ requires EOF or LF, while dot
        // rejects LF. This impossible alternative forces the production general
        // selector without changing the language or any public capture number.
        var contrast = pattern + "|$.";
        var program = PosixProgram.Compile(pattern, options);
        Assert.True(program.FirstAcceptanceIsLongest);
        Assert.Equal(!pattern.Contains('$'), program.DirectGreedyRuns);
        Assert.False(PosixProgram.Compile(contrast, options).FirstAcceptanceIsLongest);
        var regex = new Utf8PosixRegex(pattern, options: options);
        var general = new Utf8PosixRegex(contrast, options: options);
        foreach (var text in Subjects().Concat(new[] { "a \n\nb", "😀a \n\nb", "KK \nb", "a\na\n", "\na\n\na \nb" }))
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var floor = 0; floor <= input.Length; floor++)
            {
                if (floor < input.Length && (input[floor] & 0xc0) == 0x80) continue;
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var expected = new Utf8CaptureRange[width];
                    var actual = new Utf8CaptureRange[width];
                    Assert.Equal(general.Match(input, captures: expected, startOffsetInBytes: floor), regex.Match(input, captures: actual, startOffsetInBytes: floor));
                    Assert.Equal(expected, actual);
                }
                Assert.Equal(general.Count(input, floor), regex.Count(input, floor));
            }
        }
        static IEnumerable<string> Subjects()
        {
            yield return "";
            var level = new[] { "" };
            for (var length = 1; length <= 4; length++)
            {
                level = level.SelectMany(prefix => new[] { prefix + "a", prefix + " ", prefix + "\n" }).ToArray();
                foreach (var text in level) yield return text;
            }
        }
    }

    [Theory]
    [InlineData("(k+)([0-9]+)")]
    [InlineData("([k]+)([0-9]+)")]
    [InlineData("(([k]{1,2}))([0-9]+)")]
    public void RequiredRunPredicateKeepsFoldedStartsAndCaptureFloors(string pattern)
    {
        var options = PosixCompileOptions.IgnoreCase;
        var program = PosixProgram.Compile(pattern, options);
        Assert.True(program.FirstRequiredRunAtom >= 0);
        // Consuming a cannot satisfy an immediately following beginning anchor,
        // even in Newline mode. This impossible branch keeps the same language
        // and capture numbers while forcing production general selection.
        var contrast = pattern + "|a^";
        Assert.False(PosixProgram.Compile(contrast, options).FirstAcceptanceIsLongest);
        var regex = new Utf8PosixRegex(pattern, options: options);
        var general = new Utf8PosixRegex(contrast, options: options);
        foreach (var text in new[] { "", "!", "123", "k", "k12", "KK12", "😀!KK12 k2", "KKK123", "\nK12\n", new string('!', 512) + "KK12" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var floor = 0; floor <= input.Length; floor++)
            {
                if (floor < input.Length && (input[floor] & 0xc0) == 0x80) continue;
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var expected = new Utf8CaptureRange[width]; var actual = new Utf8CaptureRange[width];
                    Assert.Equal(general.Match(input, captures: expected, startOffsetInBytes: floor), regex.Match(input, captures: actual, startOffsetInBytes: floor));
                    Assert.Equal(expected, actual);
                }
                Assert.Equal(general.Count(input, floor), regex.Count(input, floor));
            }
        }
    }

    [Fact]
    public void RequiredRunPredicateDeclinesNullableOrUnprovedPrefixes()
    {
        foreach (var pattern in new[] { "(k*)([0-9]+)", "(k*)([K]*)", "([k]+)([K]+)", "(k|kk)(k*)", "(k)+([0-9]+)" })
            Assert.Equal(-1, PosixProgram.Compile(pattern, PosixCompileOptions.IgnoreCase).FirstRequiredRunAtom);
        Assert.Equal(-1, PosixProgram.Compile("([a-z]+)([0-9]+)", default).FirstRequiredRunAtom);
    }

    [Theory]
    [InlineData("^([[:alpha:]]+)([0-9]+)$", "sku007", 0, 0, 6, 0, 3, 3, 3)]
    [InlineData("([a-z]+)([0-9]+)", "😀sku007!", 4, 4, 6, 4, 3, 7, 3)]
    [InlineData("([a-z]{1,3})([0-9]{2,4})", "abcd12345", 0, 1, 7, 1, 3, 4, 4)]
    [InlineData("(([a-z]+))([0-9]+)", "ab123", 0, 0, 5, 0, 2, 0, 2)]
    public void LongestCapturesKeepIndependentByteCoordinates(string pattern, string text, int floor,
        int wholeStart, int wholeLength, int firstStart, int firstLength, int secondStart, int secondLength)
    {
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        var regex = new Utf8PosixRegex(pattern);
        var captures = new Utf8CaptureRange[regex.GroupCount + 1];
        var whole = regex.Match(input, captures: captures, startOffsetInBytes: floor);
        Assert.True(whole.Success);
        Assert.Equal((wholeStart, wholeLength), (whole.StartOffsetInBytes, (whole.EndOffsetInBytes - whole.StartOffsetInBytes)));
        Assert.Equal((firstStart, firstLength), (captures[1].StartOffsetInBytes, (captures[1].EndOffsetInBytes - captures[1].StartOffsetInBytes)));
        Assert.Equal((secondStart, secondLength), (captures[2].StartOffsetInBytes, (captures[2].EndOffsetInBytes - captures[2].StartOffsetInBytes)));
        using var token = new CancellationTokenSource();
        Assert.Equal(whole, regex.Match(input, captures: captures, startOffsetInBytes: floor, cancellationToken: token.Token));
        Assert.Equal(1, regex.Count(input, floor, token.Token));
        Assert.True(regex.IsMatch(input, token.Token));
    }

    [Theory]
    [InlineData("([a-b]+)([0-1]+)")]
    [InlineData("([a-b]+)()([0-1]+)")]
    [InlineData("(([a-b]{1,3}))([0-1]{2,4})")]
    [InlineData("([a-b]{2,4})([0-1]{1,2})([!]{1,2})")]
    [InlineData("([a-b]+)[0-1]+")]
    [InlineData("[a-b]+([0-1]+)")]
    [InlineData("[a-b]+[0-1]+")]
    [InlineData("(([a-b]{1,3}))[0-1]{2,4}[!]{1,2}")]
    [InlineData("(a*)(a*)")]
    [InlineData("([a-b]*)([a-b]*)")]
    [InlineData("([a-b]+)([a-c]*)")]
    [InlineData("([a-b]*)([0-1]+)")]
    [InlineData("([a-b]{0,3})([a-c]{0,4})")]
    [InlineData("a*([a-b]*)")]
    [InlineData("([a-b]*)[a-c]*")]
    [InlineData("(([a-b]*))([a-c]*)")]
    [InlineData("([a-b]*)()([a-c]*)")]
    [InlineData("(a*)(a*)()(b*)")]
    [InlineData("(a+)()")]
    [InlineData("(a*)()")]
    [InlineData("Z(a+)()")]
    [InlineData("ZZ(a+)ZZ()")]
    [InlineData("Z([a-b]*)()")]
    [InlineData("[Z](a{1,3})[Z]()")]
    [InlineData("(a+)Z()")]
    [InlineData("Z([a-b]+)([a-c]*)()")]
    [InlineData("Z([a-b]{0,3})([a-c]{0,4})()")]
    [InlineData("Z()([0-1]+)")]
    [InlineData("Z(a+)a()")]
    [InlineData("Z([a-b]*)a()")]
    public void GreedyRunsKeepPublicSelectionAtEveryFloorAndOutputWidth(string pattern)
    {
        var regex = new Utf8PosixRegex(pattern);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var text in new[] { "", "aaaa", "aabaa", "é😀aaaa!", "ab01!", "😀ab011!", "aaab00111!!ab01!", "a0 b1 ab001", "aabb0011!", "ab", "0011", "ab01\nab11!",
            "Z", "ZZ", "Zaaa", "ZaaaZ", "ZZaaaZZ", "😀Zab001ZZ!", "ZaaabZ01", "Zab", "Z001", "ZaaabcZZ", "ZZZ" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var floor = 0; floor <= text.Length; floor++)
            {
                if (floor < text.Length && char.IsLowSurrogate(text[floor])) continue;
                var byteFloor = Encoding.UTF8.GetByteCount(text.AsSpan(0, floor));
                var expected = oracle.Match(text, floor);
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var output = new Utf8CaptureRange[width];
                    var actual = regex.Match(input, captures: output, startOffsetInBytes: byteFloor);
                    Assert.Equal(expected.Success, actual.Success);
                    if (!expected.Success) { Assert.All(output, value => Assert.False(value.Success)); continue; }
                    Check(expected, actual);
                    for (var slot = 0; slot < Math.Min(output.Length, regex.GroupCount + 1); slot++)
                        Check(expected.Groups[slot], output[slot]);
                    if (width > regex.GroupCount + 1) Assert.False(output[^1].Success);
                }
                Assert.Equal(ScalarMatches(floor).Count(), regex.Count(input, byteFloor));
            }
            using var cursor = regex.CreateMatchCursor(input);
            var groups = new Utf8CaptureRange[regex.GroupCount + 1];
            foreach (var expected in ScalarMatches(0))
            {
                Assert.True(cursor.MoveNext(captures: groups, match: out var actual));
                Check(expected, actual);
                for (var slot = 0; slot < groups.Length; slot++) Check(expected.Groups[slot], groups[slot]);
            }
            Assert.False(cursor.MoveNext(match: out _));

            // ASCII-only consuming atoms cannot match a surrogate. BCL empty
            // progression also visits the middle of a supplementary scalar;
            // exclude only those positions from this otherwise shared oracle.
            IEnumerable<Match> ScalarMatches(int floor) => oracle.Matches(text, floor).Cast<Match>()
                .Where(match => match.Index == text.Length || !char.IsLowSurrogate(text[match.Index]));

            void Check(Group expected, Utf8CaptureRange actual)
            {
                Assert.Equal(expected.Success, actual.Success);
                if (!expected.Success) return;
                Assert.Equal((Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), Encoding.UTF8.GetByteCount(expected.Value)),
                    (actual.StartOffsetInBytes, (actual.EndOffsetInBytes - actual.StartOffsetInBytes)));
            }
        }
    }
    [Theory]
    [InlineData("Z(k+)()", PosixCompileOptions.IgnoreCase)]
    [InlineData("(k+)Z()", PosixCompileOptions.IgnoreCase)]
    [InlineData("[Z](k{1,3})Z()", PosixCompileOptions.IgnoreCase)]
    [InlineData("^Z([a-z]+)()$", PosixCompileOptions.Newline)]
    [InlineData("Z([a]+)\n^([0-9]+)", PosixCompileOptions.Newline)]
    [InlineData("Z([\n]+)$()", PosixCompileOptions.Newline)]
    public void FixedPiecesKeepFoldedAndAnchorSelectionAgainstGeneralExecution(string pattern, PosixCompileOptions options)
    {
        var regex = new Utf8PosixRegex(pattern, options: options);
        // $. cannot match with either absolute EOF or Newline's LF boundary:
        // dot cannot consume after EOF, and rejects LF in Newline mode.
        var contrast = pattern + "|$.";
        Assert.False(PosixProgram.Compile(contrast, options).FirstAcceptanceIsLongest);
        var general = new Utf8PosixRegex(contrast, options: options);
        foreach (var text in new[] { "", "Z", "ZKK", "KKZ", "ZKKZ", "😀!ZKKZ", "Zaaaa\nZaa", "Za\n12", "Z\n\nb", "Z\n\n" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var floor = 0; floor <= input.Length; floor++)
            {
                if (floor < input.Length && (input[floor] & 0xc0) == 0x80) continue;
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var expected = new Utf8CaptureRange[width];
                    var actual = new Utf8CaptureRange[width];
                    Assert.Equal(general.Match(input, captures: expected, startOffsetInBytes: floor), regex.Match(input, captures: actual, startOffsetInBytes: floor));
                    Assert.Equal(expected, actual);
                }
                Assert.Equal(general.Count(input, floor), regex.Count(input, floor));
            }
        }
    }

    [Fact]
    public void SingleRunAndFixedPiecesKeepUsefulFiniteAdmission()
    {
        var regex = new Utf8PosixRegex("Z(a+)()", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 40_000,
                MaxDepth = 1,
                MaxWorkingBytes = 4096,
                MaxResultBytes = 1
            }
        });
        var input = Encoding.ASCII.GetBytes("Z" + new string('a', 8192));
        var output = new Utf8CaptureRange[3];
        var whole = regex.Match(input, captures: output);
        Assert.Equal((0, 8193), (whole.StartOffsetInBytes, (whole.EndOffsetInBytes - whole.StartOffsetInBytes)));
        Assert.Equal((1, 8192), (output[1].StartOffsetInBytes, (output[1].EndOffsetInBytes - output[1].StartOffsetInBytes)));
        Assert.Equal((8193, 0), (output[2].StartOffsetInBytes, (output[2].EndOffsetInBytes - output[2].StartOffsetInBytes)));
        var limited = new Utf8PosixRegex("Z(a+)()", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1
            }
        });
        Assert.Throws<Utf8ResourceLimitExceededException>(() => limited.Match(input));
    }
}
