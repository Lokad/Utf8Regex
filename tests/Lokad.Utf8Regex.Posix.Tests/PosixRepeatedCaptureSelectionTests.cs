using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixRepeatedCaptureSelectionTests
{
    [Fact]
    public void BoundedLiteralRunCapturesMatchAnIndependentOccurrenceOracle()
    {
        for (var outerMax = 0; outerMax <= 4; outerMax++)
        for (var outerMin = 0; outerMin <= outerMax; outerMin++)
        for (var innerMax = 0; innerMax <= 3; innerMax++)
        for (var innerMin = 0; innerMin <= innerMax; innerMin++)
        {
            var regex = new Utf8PosixRegex($"(a{{{innerMin},{innerMax}}}){{{outerMin},{outerMax}}}");
            for (var count = 0; count <= 10; count++)
                Check(regex, outerMin, outerMax, innerMin, innerMax, count);
        }
    }

    [Theory]
    [InlineData(0, 4, 0, int.MaxValue)]
    [InlineData(1, 4, 1, int.MaxValue)]
    [InlineData(2, 4, 0, int.MaxValue)]
    [InlineData(1, int.MaxValue, 0, 1)]
    [InlineData(2, int.MaxValue, 0, int.MaxValue)]
    [InlineData(0, int.MaxValue, 0, 1)]
    [InlineData(0, int.MaxValue, 0, int.MaxValue)]
    [InlineData(1, int.MaxValue, 3, 4)]
    public void UnboundedAndExcludedShapesKeepTheSameHistoryRules(int outerMin, int outerMax,
        int innerMin, int innerMax)
    {
        static string Bound(int value) => value == int.MaxValue ? "" : value.ToString();
        var regex = new Utf8PosixRegex($"(a{{{innerMin},{Bound(innerMax)}}}){{{outerMin},{Bound(outerMax)}}}");
        for (var count = 0; count <= 6; count++)
            Check(regex, outerMin, outerMax, innerMin, innerMax, count);
    }

    [Fact]
    public void FirstAcceptanceDoesNotReplaceGeneralRankingForCounterexamples()
    {
        Assert.False(PosixProgram.Compile("(a{3,4})+", default).FirstAcceptanceIsLongest);
        Assert.False(PosixProgram.Compile("(a?)*", default).FirstAcceptanceIsLongest);
        // The first greedy path consumes only four bytes; two length-three
        // occurrences consume six and win whole-match selection.
        var lengths = new Utf8PosixRegex("(a{3,4})+");
        var captures = new Utf8ValueMatch[2];
        Assert.Equal(6, lengths.Match("aaaaaa"u8, captures).LengthInBytes);
        Assert.Equal((3, 3), (captures[1].IndexInBytes, captures[1].LengthInBytes));
        // A saturated minimum-zero outer counter cannot distinguish its first
        // empty occurrence from a later redundant one. The shorter history wins.
        var nullable = new Utf8PosixRegex("(a?)*");
        Assert.Equal(1, nullable.Match("a"u8, captures).LengthInBytes);
        Assert.Equal((0, 1), (captures[1].IndexInBytes, captures[1].LengthInBytes));
    }

    [Fact]
    public void RepeatedLiteralProofDeclinesAdditionalHistoryAndConsumingTails()
    {
        foreach (var pattern in new[] { "(a*){2,4}", "(a{1,2}){2,4}", "(😀?)+" })
            Assert.True(PosixProgram.Compile(pattern, default).FirstAcceptanceIsLongest);
        foreach (var pattern in new[] { "((a)?){2,4}", "(a?){2,4}a", "(a|aa){2,4}", "([ab]?){2,4}" })
            Assert.False(PosixProgram.Compile(pattern, default).FirstAcceptanceIsLongest);
    }

    [Theory]
    [InlineData("é")] [InlineData("😀")] [InlineData("\0")]
    public void RepeatedCaptureKeepsScalarFloorsEmptyCoordinatesAndRetainedValues(string atom)
    {
        var regex = new Utf8PosixRegex($"({atom}?){{2,4}}");
        const string prefix = "é😀!";
        var text = prefix + atom + "!";
        var storage = Encoding.UTF8.GetBytes("pad" + text + "pad");
        var input = storage.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var width = Encoding.UTF8.GetByteCount(atom);
        var captures = new Utf8ValueMatch[2];
        var whole = regex.Match(input, captures, floor);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, atom.Length, floor, width), whole);
        var retained = captures[1];
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + atom.Length, 0, floor + width, 0), retained);
        Span<Utf8ValueMatch> shortOutput = stackalloc Utf8ValueMatch[1];
        Assert.Equal(whole, regex.Match(input, shortOutput, floor));
        Assert.Equal(whole, shortOutput[0]);
        regex.Match(input, captures, floor + width);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + atom.Length, 0, floor + width, 0), retained);
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match(storage.AsSpan(3, storage.Length - 6), captures, 1));
        Assert.Throws<ArgumentException>(() => regex.Match(new byte[] { (byte)'a', 255 }, captures));
    }

    private static void Check(Utf8PosixRegex regex, int outerMin, int outerMax, int innerMin, int innerMax, int count)
    {
        var expected = Occurrences(outerMin, outerMax, innerMin, innerMax, count);
        var captures = new Utf8ValueMatch[2];
        var actual = regex.Match(Encoding.ASCII.GetBytes(new string('a', count)), captures);
        var total = expected?.Sum() ?? -1;
        var whole = expected is null ? default : new Utf8ValueMatch(true, true, 0, total, 0, total);
        Assert.Equal(whole, actual);
        Assert.Equal(whole, captures[0]);
        var last = expected is not { Length: > 0 } ? default :
            new Utf8ValueMatch(true, true, total - expected[^1], expected[^1], total - expected[^1], expected[^1]);
        Assert.Equal(last, captures[1]);
    }

    // Enumerate scalar extents, independently of instructions, stacks, tags or
    // greedy search order. Whole length wins first; occurrence lengths then rank
    // left to right. Participation beats no occurrence. Otherwise a strict
    // equal-prefix history loses to its shorter counterpart.
    private static int[]? Occurrences(int outerMin, int outerMax, int innerMin, int innerMax, int count)
    {
        var maximum = Math.Min(outerMax, count + outerMin + 1);
        var current = new List<int>();
        int[]? best = null;
        var bestLength = -1;
        Visit(count);
        return best;

        void Visit(int remaining)
        {
            var length = count - remaining;
            if (current.Count >= outerMin && (length > bestLength || length == bestLength && Better()))
            {
                best = current.ToArray();
                bestLength = length;
            }
            if (current.Count == maximum) return;
            for (var extent = innerMin; extent <= Math.Min(innerMax, remaining); extent++)
            {
                current.Add(extent);
                Visit(remaining - extent);
                current.RemoveAt(current.Count - 1);
            }
        }
        bool Better()
        {
            if (best is null) return true;
            if (current.Count == 0 || best.Length == 0) return current.Count > best.Length;
            for (var index = 0; index < Math.Min(current.Count, best.Length); index++)
                if (current[index] != best[index]) return current[index] > best[index];
            return current.Count < best.Length;
        }
    }
}
