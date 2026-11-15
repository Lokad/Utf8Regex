using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixGreedyAsciiRunTests
{
    [Fact]
    public void NewlineAnchorsSelectEachCompleteDisjointRunPair()
    {
        var regex = new Utf8PosixRegex("^([a-z]+)([0-9]+)$", PosixCompileOptions.Newline,
            Timeout.InfiniteTimeSpan, default);
        var captures = new Utf8ValueMatch[3];
        var whole = regex.Match("bad\nsku007\nx9\n"u8, captures);
        Assert.Equal((4, 6), (whole.IndexInBytes, whole.LengthInBytes));
        Assert.Equal((4, 3), (captures[1].IndexInBytes, captures[1].LengthInBytes));
        Assert.Equal((7, 3), (captures[2].IndexInBytes, captures[2].LengthInBytes));
        Assert.Equal(2, regex.Count("bad\nsku007\nx9\n"u8));
    }

    [Fact]
    public void OnlyProvedFlatAsciiRunsAdmitGreedyBoundaries()
    {
        Assert.True(PosixProgram.Compile("^([[:alpha:]]+)([0-9]+)$", default).FirstAcceptanceIsLongest);
        Assert.True(PosixProgram.Compile("([a-z]{1,3})([0-9]{2,4})([!]{1,2})", default).FirstAcceptanceIsLongest);
        foreach (var pattern in new[] { "([a-z]+)[0-9]+", "[a-z]+([0-9]+)",
            "[a-z]+[0-9]+", "(([a-z]+))[0-9]{2,4}", "(a*)(a*)",
            "([a-z]*)([0-9]+)", "([a-z]+)([a-z0-9]*)", "a*([a-b]*)" })
            Assert.True(PosixProgram.Compile(pattern, default).GreedyAsciiRuns);
        // The older homogeneous-literal proof can still accept (a*)(a+),
        // but its required overlapping tail cannot use the direct run loop.
        Assert.False(PosixProgram.Compile("(a*)(a+)", default).GreedyAsciiRuns);
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
            Assert.False(program.GreedyAsciiRuns); // Kelvin is not an ASCII byte run.
        }
        foreach (var pattern in new[] { "([k]+)([K]+)", "([k]*)^([0-9]+)", "([k])+([0-9]+)",
            "([K]+)([0-9]+)", "([^k]+)([0-9]+)", "(k|kk)(k*)" })
            Assert.False(PosixProgram.Compile(pattern, PosixCompileOptions.IgnoreCase).FirstAcceptanceIsLongest);
        var rewind = PosixProgram.Compile("^([a-z]+)([[:space:]]+)$", PosixCompileOptions.Newline);
        Assert.True(rewind.FirstAcceptanceIsLongest);
        Assert.False(rewind.GreedyAsciiRuns);
    }

    [Theory]
    [InlineData("a \nb", 2, 1)]
    [InlineData("a \n\nb", 3, 2)]
    [InlineData("a \n", 3, 2)]
    public void LineEndRewindSelectsTheLatestValidEndpoint(string text, int length, int whitespace)
    {
        var regex = new Utf8PosixRegex("^([a-z]+)([[:space:]]+)$", PosixCompileOptions.Newline);
        var groups = new Utf8ValueMatch[3];
        var whole = regex.Match(Encoding.UTF8.GetBytes(text), groups);
        Assert.True(whole.Success);
        Assert.Equal((0, length), (whole.IndexInBytes, whole.LengthInBytes));
        Assert.Equal((0, 1), (groups[1].IndexInBytes, groups[1].LengthInBytes));
        Assert.Equal((1, whitespace), (groups[2].IndexInBytes, groups[2].LengthInBytes));
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
        Assert.Equal(!pattern.Contains('$') && (options & PosixCompileOptions.IgnoreCase) == 0, program.GreedyAsciiRuns);
        Assert.False(PosixProgram.Compile(contrast, options).FirstAcceptanceIsLongest);
        var regex = new Utf8PosixRegex(pattern, options);
        var general = new Utf8PosixRegex(contrast, options);
        foreach (var text in Subjects().Concat(new[] { "a \n\nb", "😀a \n\nb", "KK \nb", "a\na\n", "\na\n\na \nb" }))
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var floor = 0; floor <= input.Length; floor++)
            {
                if (floor < input.Length && (input[floor] & 0xc0) == 0x80) continue;
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var expected = new Utf8ValueMatch[width];
                    var actual = new Utf8ValueMatch[width];
                    Assert.Equal(general.Match(input, expected, floor), regex.Match(input, actual, floor));
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
        var regex = new Utf8PosixRegex(pattern, options);
        var general = new Utf8PosixRegex(contrast, options);
        foreach (var text in new[] { "", "!", "123", "k", "k12", "KK12", "😀!KK12 k2", "KKK123", "\nK12\n", new string('!', 512) + "KK12" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var floor = 0; floor <= input.Length; floor++)
            {
                if (floor < input.Length && (input[floor] & 0xc0) == 0x80) continue;
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var expected = new Utf8ValueMatch[width]; var actual = new Utf8ValueMatch[width];
                    Assert.Equal(general.Match(input, expected, floor), regex.Match(input, actual, floor));
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
        var captures = new Utf8ValueMatch[regex.GroupCount + 1];
        var whole = regex.Match(input, captures, floor);
        Assert.True(whole.Success);
        Assert.Equal((wholeStart, wholeLength), (whole.IndexInBytes, whole.LengthInBytes));
        Assert.Equal((firstStart, firstLength), (captures[1].IndexInBytes, captures[1].LengthInBytes));
        Assert.Equal((secondStart, secondLength), (captures[2].IndexInBytes, captures[2].LengthInBytes));
        using var token = new CancellationTokenSource();
        Assert.Equal(whole, regex.Match(input, captures, floor, token.Token));
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
    public void GreedyRunsKeepPublicSelectionAtEveryFloorAndOutputWidth(string pattern)
    {
        var regex = new Utf8PosixRegex(pattern);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant);
        foreach (var text in new[] { "", "aaaa", "aabaa", "é😀aaaa!", "ab01!", "😀ab011!", "aaab00111!!ab01!", "a0 b1 ab001", "aabb0011!", "ab", "0011", "ab01\nab11!" })
        {
            var input = Encoding.UTF8.GetBytes(text);
            for (var floor = 0; floor <= text.Length; floor++)
            {
                if (floor < text.Length && char.IsLowSurrogate(text[floor])) continue;
                var byteFloor = Encoding.UTF8.GetByteCount(text.AsSpan(0, floor));
                var expected = oracle.Match(text, floor);
                for (var width = 0; width <= regex.GroupCount + 2; width++)
                {
                    var output = new Utf8ValueMatch[width];
                    var actual = regex.Match(input, output, byteFloor);
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
            var groups = new Utf8ValueMatch[regex.GroupCount + 1];
            foreach (var expected in ScalarMatches(0))
            {
                Assert.True(cursor.MoveNext(groups, out var actual));
                Check(expected, actual);
                for (var slot = 0; slot < groups.Length; slot++) Check(expected.Groups[slot], groups[slot]);
            }
            Assert.False(cursor.MoveNext(out _));

            // ASCII-only consuming atoms cannot match a surrogate. BCL empty
            // progression also visits the middle of a supplementary scalar;
            // exclude only those positions from this otherwise shared oracle.
            IEnumerable<Match> ScalarMatches(int floor) => oracle.Matches(text, floor).Cast<Match>()
                .Where(match => match.Index == text.Length || !char.IsLowSurrogate(text[match.Index]));

            void Check(Group expected, Utf8ValueMatch actual)
            {
                Assert.Equal(expected.Success, actual.Success);
                if (!expected.Success) return;
                Assert.Equal((expected.Index, expected.Length), (actual.IndexInUtf16, actual.LengthInUtf16));
                Assert.Equal((Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), Encoding.UTF8.GetByteCount(expected.Value)),
                    (actual.IndexInBytes, actual.LengthInBytes));
            }
        }
    }
}
