using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixRepeatedCaptureSelectionTests
{
    [Theory]
    [InlineData("a", false)] [InlineData("a", true)]
    [InlineData("😀", false)] [InlineData("😀", true)]
    public void ParentBoundsKeepIndependentOccurrencePriority(string scalar, bool nullable)
    {
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var width = Encoding.UTF8.GetByteCount(scalar);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        foreach (var (tailPattern, tail) in new[] { ("[bc]", "b"), (".", "é"), ("[^a]", "中"), ("[é中]", "é"),
            ("[bc]?", "b"), ("[bc]*", "bcc"), ("[bc]{1,3}", "bc"), ("[é中]*", "中é") })
        foreach (var capturedTail in new[] { false, true })
        foreach (var longerFirst in new[] { false, true })
        foreach (var longer in new[] { scalar + scalar, scalar + "[^bcé中]", scalar + "[^bcé中#]", scalar + "[" + scalar + "☃]" })
        {
            // Each longer alternative consumes exactly two of these input
            // scalars; the independent extent oracle does not inspect bytecode.
            var alternatives = (longerFirst ? longer + "|" + scalar : scalar + "|" + longer) + (nullable ? "|" : "");
            var regex = new Utf8PosixRegex($"(({alternatives})*)" + (capturedTail ? "(" + tailPattern + ")" : tailPattern),
                inputValidation: validation);
            for (var count = 0; count <= 8; count++)
            foreach (var rest in new[] { "", "!", "#" })
            {
                var extents = Occurrences(0, int.MaxValue, nullable ? 0 : 1, 2, count)!;
                var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(scalar, count)) + tail + rest);
                var whole = new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length + tail.Length,
                    floor, count * width + Encoding.UTF8.GetByteCount(tail));
                var groups = new Utf8ValueMatch[capturedTail ? 4 : 3];
                Assert.Equal(whole, regex.Match(input, groups, floor));
                Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length, floor, count * width), groups[1]);
                Assert.Equal(extents.Length == 0 ? default : new Utf8ValueMatch(true, true,
                    prefix.Length + (count - extents[^1]) * scalar.Length, extents[^1] * scalar.Length,
                    floor + (count - extents[^1]) * width, extents[^1] * width), groups[2]);
                if (capturedTail) Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + count * scalar.Length,
                    tail.Length, floor + count * width, Encoding.UTF8.GetByteCount(tail)), groups[3]);
                Assert.Equal(whole, regex.Match(input, groups.AsSpan(0, 1), floor));
                Assert.Equal(whole, regex.Match(input, floor));
            }
        }
    }

    [Theory]
    [InlineData("((a|a[^!#])*)!*", "a", "!", PosixCompileOptions.None)]
    [InlineData("((a|a[ab中])*)!*", "a", "!", PosixCompileOptions.None)]
    [InlineData("((a|a[^!#])*)(!|!!)", "a", "!!", PosixCompileOptions.None)]
    [InlineData("((😀|😀[^!#])*)!*", "😀", "!", PosixCompileOptions.None)]
    [InlineData("((a|a[^!#])*)([^a#]*)", "a", "!!", PosixCompileOptions.None)]
    [InlineData("((a|a.)*)!", "a", "!", PosixCompileOptions.Newline)]
    public void ExcludedWholeBoundaryKeepsCapturesAndCursorsWithinFiniteWork(
        string pattern, string scalar, string tail, PosixCompileOptions options)
    {
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var body = string.Concat(Enumerable.Repeat(scalar, 32));
        var text = body + tail;
        var parent = body;
        var last = scalar + scalar;
        var sentinel = options == PosixCompileOptions.Newline ? "\n" : "#";
        var input = Encoding.UTF8.GetBytes(prefix + text + sentinel);
        var whole = new Utf8ValueMatch(true, true, prefix.Length, text.Length, floor, Encoding.UTF8.GetByteCount(text));
        var parentMatch = new Utf8ValueMatch(true, true, prefix.Length, parent.Length, floor, Encoding.UTF8.GetByteCount(parent));
        var lastMatch = new Utf8ValueMatch(true, true, prefix.Length + parent.Length - last.Length, last.Length,
            floor + Encoding.UTF8.GetByteCount(parent) - Encoding.UTF8.GetByteCount(last), Encoding.UTF8.GetByteCount(last));
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8PosixRegex(pattern, options, resourcePolicy: new()
            { MaxWorkSteps = 100_000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }, inputValidation: validation);
            var groups = new Utf8ValueMatch[regex.GroupCount + 1];
            Assert.Equal(whole, regex.Match(input, groups, floor));
            Assert.Equal(parentMatch, groups[1]); Assert.Equal(lastMatch, groups[2]);
            if (groups.Length > 3) Assert.Equal(new Utf8ValueMatch(true, true,
                prefix.Length + body.Length, tail.Length, floor + Encoding.UTF8.GetByteCount(body), Encoding.UTF8.GetByteCount(tail)), groups[3]);
            Assert.Equal(whole, regex.Match(input, groups.AsSpan(0, 1), floor));
            Assert.Equal(whole, regex.Match(input, floor));
            var count = regex.Count(input, floor);
            Assert.Equal(pattern.Contains("(!|!!)", StringComparison.Ordinal) || options == PosixCompileOptions.Newline ? 1 : 3, count);
            using var cursor = regex.CreateMatchCursor(input, floor);
            var alias = cursor;
            Assert.True(cursor.MoveNext(groups, out var current)); Assert.Equal(whole, current);
            var retained = groups[2];
            var observed = 1;
            while (alias.MoveNext(out _)) observed++;
            Assert.Equal(count, observed);
            alias.Dispose();
            Assert.Equal(lastMatch, retained);
            Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        }
    }

    [Theory]
    [InlineData("((a|a[^!])*)!*", "a", "!", PosixCompileOptions.None)]
    [InlineData("((a|a[ab中])*)!*", "a", "!", PosixCompileOptions.None)]
    [InlineData("((a|a[^!])*)(!|!!)", "a", "!!", PosixCompileOptions.None)]
    [InlineData("((a|a[^!])*)([^a]*)", "a", "!", PosixCompileOptions.None)]
    [InlineData("((😀|😀[^!])*)!*", "😀", "!", PosixCompileOptions.None)]
    [InlineData("((a|a.)*)\n*", "a", "\n", PosixCompileOptions.Newline)]
    [InlineData("((😀|😀.)*)\n*", "😀", "\n", PosixCompileOptions.Newline)]
    public void BoundaryExclusionAllowsFiniteParentHistory(string pattern, string scalar, string tail, PosixCompileOptions options)
    {
        const int count = 32;
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var width = Encoding.UTF8.GetByteCount(scalar);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8PosixRegex(pattern, options, resourcePolicy: new()
            { MaxWorkSteps = 100_000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }, inputValidation: validation);
            var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(scalar, count)) + tail);
            var expected = new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length + tail.Length,
                floor, count * width + Encoding.UTF8.GetByteCount(tail));
            var groups = new Utf8ValueMatch[regex.GroupCount + 1];
            Assert.Equal(expected, regex.Match(input, groups, floor));
            Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length, floor, count * width), groups[1]);
            var pair = new Utf8ValueMatch(true, true, prefix.Length + (count - 2) * scalar.Length,
                2 * scalar.Length, floor + (count - 2) * width, 2 * width);
            Assert.Equal(pair, groups[2]);
            if (groups.Length > 3) Assert.Equal(new Utf8ValueMatch(true, true,
                prefix.Length + count * scalar.Length, tail.Length, floor + count * width, Encoding.UTF8.GetByteCount(tail)), groups[3]);
            Assert.Equal(expected, regex.Match(input, groups.AsSpan(0, 1), floor));
            Assert.Equal(expected, regex.Match(input, floor));
            using var cursor = regex.CreateMatchCursor(input, floor);
            var alias = cursor;
            Assert.True(cursor.MoveNext(groups, out var whole)); Assert.Equal(expected, whole);
            var retained = groups[2]; cursor.Dispose();
            Assert.Equal(pair, retained);
            Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(out _));
        }
    }

    [Theory]
    [InlineData("a", "é", "😀")] [InlineData("中", "é", "😀")]
    [InlineData("a", ".", "[😀]")] [InlineData("中", "[^a]", ".")]
    public void FixedSuffixCanDetermineDifferentEndsForNestedParents(string scalar, string innerTail, string outerTail)
    {
        const int count = 32;
        var width = Encoding.UTF8.GetByteCount(scalar);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8PosixRegex($"(({scalar}|{scalar}{scalar})*{innerTail}){outerTail}",
                resourcePolicy: new() { MaxWorkSteps = 100_000 }, inputValidation: validation);
            var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(scalar, count)) + "é😀");
            var groups = new Utf8ValueMatch[3];
            Assert.Equal(new Utf8ValueMatch(true, true, 0, count * scalar.Length + 3, 0, count * width + 6), regex.Match(input, groups));
            Assert.Equal(new Utf8ValueMatch(true, true, 0, count * scalar.Length + 1, 0, count * width + 2), groups[1]);
            Assert.Equal(new Utf8ValueMatch(true, true, (count - 2) * scalar.Length, 2 * scalar.Length,
                (count - 2) * width, 2 * width), groups[2]);
        }
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate, "(a|aa)*b", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "(a|aa)*b", "a")]
    [InlineData(Utf8InputValidation.Validate, "((a|aa)*)b", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "((a|aa)*)b", "a")]
    [InlineData(Utf8InputValidation.Validate, "(((a|aa)*))b", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "(((a|aa)*))b", "a")]
    [InlineData(Utf8InputValidation.Validate, "(a|a[ab])*b", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "(a|a[ab])*b", "a")]
    [InlineData(Utf8InputValidation.Validate, "(😀|😀😀)*b", "😀")]
    [InlineData(Utf8InputValidation.AssumeValid, "(😀|😀😀)*b", "😀")]
    [InlineData(Utf8InputValidation.Validate, "((a|aa)*)[bc]", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "((a|aa)*)[bc]", "a")]
    [InlineData(Utf8InputValidation.Validate, "((a|aa)*).", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "((a|aa)*).", "a")]
    [InlineData(Utf8InputValidation.Validate, "((a|aa)*)[^a]", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "((a|aa)*)[^a]", "a")]
    [InlineData(Utf8InputValidation.Validate, "((😀|😀😀)*)[ab]", "😀")]
    [InlineData(Utf8InputValidation.AssumeValid, "((😀|😀😀)*)[ab]", "😀")]
    [InlineData(Utf8InputValidation.Validate, "(a|aa)*[bc]?", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "(a|aa)*[bc]?", "a")]
    [InlineData(Utf8InputValidation.Validate, "((a|aa)*)[bc]?", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "((a|aa)*)[bc]?", "a")]
    [InlineData(Utf8InputValidation.Validate, "((a|aa)*)[bc]*", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "((a|aa)*)[bc]*", "a")]
    [InlineData(Utf8InputValidation.Validate, "((😀|😀😀)*)[ab]?", "😀")]
    [InlineData(Utf8InputValidation.AssumeValid, "((😀|😀😀)*)[ab]?", "😀")]
    public void BoundedParentsAllowFiniteDescendantHistoryCompetition(
        Utf8InputValidation validation, string pattern, string scalar)
    {
        const int count = 32;
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var width = Encoding.UTF8.GetByteCount(scalar);
        var regex = new Utf8PosixRegex(pattern, resourcePolicy: new()
        { MaxWorkSteps = 100_000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }, inputValidation: validation);
        using var token = new CancellationTokenSource();
        foreach (var nonEof in new[] { false, true })
        {
            var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(scalar, count)) + "b" + (nonEof ? "!" : ""));
            var groups = new Utf8ValueMatch[regex.GroupCount + 1];
            var expected = new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length + 1, floor, count * width + 1);
            Assert.Equal(expected, regex.Match(input, groups, floor, token.Token));
            for (var group = 1; group < groups.Length - 1; group++)
                Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length, floor, count * width), groups[group]);
            var retained = groups[^1];
            Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + (count - 2) * scalar.Length,
                2 * scalar.Length, floor + (count - 2) * width, 2 * width), retained);
            Assert.Equal(expected, regex.Match(input, groups.AsSpan(0, 1), floor));
            var nullable = pattern is "(a|aa)*[bc]?" or "((a|aa)*)[bc]?" or "((a|aa)*)[bc]*" or "((😀|😀😀)*)[ab]?";
            var emptyMatches = nullable ? nonEof ? 2 : 1 : 0;
            var trailingMatch = nonEof && pattern is "((a|aa)*)." or "((a|aa)*)[^a]";
            Assert.Equal(1 + emptyMatches + (trailingMatch ? 1 : 0), regex.Count(input, floor));
            using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
            var alias = cursor;
            Assert.True(cursor.MoveNext(groups, out var whole)); Assert.Equal(expected, whole);
            for (var empty = 0; empty < emptyMatches; empty++)
            {
                Assert.True(alias.MoveNext(groups, out var next));
                Assert.Equal(new Utf8ValueMatch(true, true, expected.IndexInUtf16 + expected.LengthInUtf16 + empty, 0,
                    expected.IndexInBytes + expected.LengthInBytes + empty, 0), next);
            }
            if (trailingMatch)
            {
                Assert.True(alias.MoveNext(groups, out var next));
                Assert.Equal(new Utf8ValueMatch(true, true, expected.IndexInUtf16 + expected.LengthInUtf16, 1,
                    expected.IndexInBytes + expected.LengthInBytes, 1), next);
                Assert.True(groups[1].Success); Assert.Equal(0, groups[1].LengthInBytes);
                Assert.False(groups[2].Success);
            }
            Assert.False(alias.MoveNext(groups, out _));
            Assert.All(groups, group => Assert.False(group.Success));
            Assert.Equal(2 * width, retained.LengthInBytes);
        }
    }

    [Theory]
    [InlineData("a", "b", false)] [InlineData("a", "b", true)]
    [InlineData("é", "😀", false)] [InlineData("é", "😀", true)]
    [InlineData("😀", "é", false)] [InlineData("😀", "é", true)]
    public void ReachableWholeBoundariesPreserveNestedCapturesAndCursorAliases(string scalar, string tail, bool nullable)
    {
        const int count = 32;
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var body = string.Concat(Enumerable.Repeat(scalar, count));
        var input = Encoding.UTF8.GetBytes(prefix + body + tail + "中!");
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8PosixRegex($"((({scalar}|{scalar}{scalar}{(nullable ? "|" : "")})*).)",
                resourcePolicy: new() { MaxWorkSteps = 100_000, MaxWorkingBytes = 1 << 20, MaxDepth = 4096 },
                inputValidation: validation);
            var groups = new Utf8ValueMatch[4];
            var whole = new Utf8ValueMatch(true, true, prefix.Length, body.Length + tail.Length,
                floor, Encoding.UTF8.GetByteCount(body + tail));
            Assert.Equal(whole, regex.Match(input, groups, floor));
            Assert.Equal(whole, groups[1]);
            Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, body.Length,
                floor, Encoding.UTF8.GetByteCount(body)), groups[2]);
            var last = new Utf8ValueMatch(true, true, prefix.Length + body.Length - scalar.Length * 2, scalar.Length * 2,
                floor + Encoding.UTF8.GetByteCount(body) - Encoding.UTF8.GetByteCount(scalar) * 2,
                Encoding.UTF8.GetByteCount(scalar) * 2);
            Assert.Equal(last, groups[3]);
            Assert.Equal(whole, regex.Match(input, groups.AsSpan(0, 1), floor));
            Assert.Equal(3, regex.Count(input, floor));
            using var cursor = regex.CreateMatchCursor(input, floor);
            var alias = cursor;
            Assert.True(cursor.MoveNext(groups, out var current)); Assert.Equal(whole, current);
            var retained = groups[3];
            Assert.True(alias.MoveNext(groups, out _));
            Assert.True(cursor.MoveNext(groups, out _));
            Assert.False(alias.MoveNext(groups, out _));
            Assert.All(groups, group => Assert.False(group.Success));
            cursor.Dispose(); Assert.Equal(last, retained);
            Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(out _));
        }
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void SeparateParentCeilingsAllowVariableCapturedTails(Utf8InputValidation validation)
    {
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var regex = new Utf8PosixRegex("(((a|aa)*)(b|cc))d*", resourcePolicy: new()
        { MaxWorkSteps = 100_000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }, inputValidation: validation);
        var input = Encoding.UTF8.GetBytes(prefix + new string('a', 32) + "bdd!");
        var groups = new Utf8ValueMatch[5];
        var whole = new Utf8ValueMatch(true, true, prefix.Length, 35, floor, 35);
        Assert.Equal(whole, regex.Match(input, groups, floor));
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, 33, floor, 33), groups[1]);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, 32, floor, 32), groups[2]);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + 30, 2, floor + 30, 2), groups[3]);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + 32, 1, floor + 32, 1), groups[4]);
        Assert.Equal(whole, regex.Match(input, groups.AsSpan(0, 1), floor));
        Assert.Equal(1, regex.Count(input, floor));
        using var cursor = regex.CreateMatchCursor(input, floor);
        var alias = cursor;
        Assert.True(cursor.MoveNext(groups, out var current)); Assert.Equal(whole, current);
        var retained = groups[3];
        Assert.False(alias.MoveNext(groups, out _));
        Assert.All(groups, group => Assert.False(group.Success));
        Assert.Equal(2, retained.LengthInBytes);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate, "a", false)]
    [InlineData(Utf8InputValidation.AssumeValid, "a", false)]
    [InlineData(Utf8InputValidation.Validate, "a", true)]
    [InlineData(Utf8InputValidation.AssumeValid, "a", true)]
    [InlineData(Utf8InputValidation.Validate, "😀", false)]
    [InlineData(Utf8InputValidation.AssumeValid, "😀", false)]
    [InlineData(Utf8InputValidation.Validate, "😀", true)]
    [InlineData(Utf8InputValidation.AssumeValid, "😀", true)]
    public void LongAmbiguousCaptureFitsFiniteWorkWithoutRescanningEqualOccurrences(
        Utf8InputValidation validation, string scalar, bool nonEof)
    {
        const int count = 1024;
        const string prefix = "é😀!";
        var width = Encoding.UTF8.GetByteCount(scalar);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(scalar, count)) + (nonEof ? "!" : ""));
        var regex = new Utf8PosixRegex($"({scalar}|{scalar}{scalar})*", resourcePolicy: new()
        { MaxWorkSteps = 100_000, MaxDepth = 4096, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }, inputValidation: validation);
        using var token = new CancellationTokenSource();
        var groups = new Utf8ValueMatch[2];
        var expected = new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length, floor, count * width);
        Assert.Equal(expected, regex.Match(input, groups, floor, token.Token));
        var retained = groups[1];
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + (count - 2) * scalar.Length,
            2 * scalar.Length, floor + (count - 2) * width, 2 * width), retained);
        Assert.Equal(expected, regex.Match(input, groups.AsSpan(0, 1), floor));
        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
        var alias = cursor;
        Assert.True(cursor.MoveNext(groups, out var whole)); Assert.Equal(expected, whole);
        Assert.True(alias.MoveNext(out var empty)); Assert.Equal(floor + count * width, empty.IndexInBytes);
        if (nonEof) { Assert.True(cursor.MoveNext(out empty)); Assert.Equal(input.Length, empty.IndexInBytes); }
        Assert.False(alias.MoveNext(groups, out _));
        Assert.All(groups, group => Assert.False(group.Success));
        Assert.Equal(2 * width, retained.LengthInBytes);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate, "a", "aa")]
    [InlineData(Utf8InputValidation.AssumeValid, "a", "aa")]
    [InlineData(Utf8InputValidation.Validate, "😀", "😀😀")]
    [InlineData(Utf8InputValidation.AssumeValid, "😀", "😀😀")]
    [InlineData(Utf8InputValidation.Validate, "a", "a[^!]")]
    [InlineData(Utf8InputValidation.AssumeValid, "a", "a[^!]")]
    [InlineData(Utf8InputValidation.Validate, "😀", "😀[^!]")]
    [InlineData(Utf8InputValidation.AssumeValid, "😀", "😀[^!]")]
    [InlineData(Utf8InputValidation.Validate, "a", ".a")]
    [InlineData(Utf8InputValidation.AssumeValid, "a", ".a")]
    [InlineData(Utf8InputValidation.Validate, "😀", ".😀")]
    [InlineData(Utf8InputValidation.AssumeValid, "😀", ".😀")]
    public void AmbiguousCaptureFitsFiniteWorkWithoutKeepingEveryPartition(Utf8InputValidation validation, string scalar, string longer)
    {
        var pair = scalar + scalar;
        var regex = new Utf8PosixRegex($"({scalar}|{longer})*", resourcePolicy: new()
        { MaxWorkSteps = 100_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }, inputValidation: validation);
        const string prefix = "é😀!";
        var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(scalar, 32)));
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var width = Encoding.UTF8.GetByteCount(scalar);
        var groups = new Utf8ValueMatch[2];
        using var token = new CancellationTokenSource();
        var whole = regex.Match(input, groups, floor, token.Token);
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, 32 * scalar.Length, floor, 32 * width), whole);
        var retained = groups[1];
        Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + 30 * scalar.Length, pair.Length,
            floor + 30 * width, 2 * width), retained);
        Assert.Equal(whole, regex.Match(input, floor, token.Token));
        Assert.Equal(2, regex.Count(input, floor, token.Token));
        using (var wholeCursor = regex.CreateMatchCursor(input, floor, token.Token))
        {
            var alias = wholeCursor;
            Assert.True(wholeCursor.MoveNext(out var first)); Assert.Equal(whole, first);
            Assert.True(alias.MoveNext(groups, out var terminal));
            Assert.Equal(input.Length, terminal.IndexInBytes); Assert.Equal(0, terminal.LengthInBytes);
            Assert.False(wholeCursor.MoveNext(out _));
            Assert.Equal(2 * width, retained.LengthInBytes);
        }
        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
        Assert.True(cursor.MoveNext(groups, out var current));
        Assert.Equal(whole, current); Assert.Equal(retained, groups[1]);
        Assert.True(cursor.MoveNext(groups, out var empty));
        Assert.Equal(input.Length, empty.IndexInBytes); Assert.Equal(0, empty.LengthInBytes);
        Assert.False(cursor.MoveNext(groups, out _));
        Assert.All(groups, group => Assert.False(group.Success));
        Assert.Equal(2 * width, retained.LengthInBytes);
    }

    [Theory]
    [InlineData(Utf8InputValidation.Validate, "(a|aa)*", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "(a|aa)*", "a")]
    [InlineData(Utf8InputValidation.Validate, "(a|a[ab])*", "a")]
    [InlineData(Utf8InputValidation.AssumeValid, "(a|a[ab])*", "a")]
    [InlineData(Utf8InputValidation.Validate, "(é|éé)*", "é")]
    [InlineData(Utf8InputValidation.AssumeValid, "(é|éé)*", "é")]
    [InlineData(Utf8InputValidation.Validate, "(😀|😀😀)*", "😀")]
    [InlineData(Utf8InputValidation.AssumeValid, "(😀|😀😀)*", "😀")]
    public void ExcludedBoundariesKeepNonEofHistoryAndWholeOnlyCountsWithinFiniteWork(Utf8InputValidation validation, string pattern, string scalar)
    {
        var regex = new Utf8PosixRegex(pattern, resourcePolicy: new()
        { MaxWorkSteps = 100_000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }, inputValidation: validation);
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var width = Encoding.UTF8.GetByteCount(scalar);
        foreach (var suffix in new[] { "!", "中", "\0" })
        {
            var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(scalar, 32)) + suffix);
            var groups = new Utf8ValueMatch[2];
            var whole = regex.Match(input, groups, floor);
            Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length, 32 * scalar.Length, floor, 32 * width), whole);
            Assert.Equal(new Utf8ValueMatch(true, true, prefix.Length + 30 * scalar.Length, 2 * scalar.Length, floor + 30 * width, 2 * width), groups[1]);
            var retained = groups[1];
            Assert.Equal(whole, regex.Match(input, floor));
            Assert.Equal(3, regex.Count(input, floor));
            using var cursor = regex.CreateMatchCursor(input, floor);
            var alias = cursor;
            Assert.True(cursor.MoveNext(groups, out var current)); Assert.Equal(whole, current);
            Assert.True(alias.MoveNext(out var empty)); Assert.Equal(floor + 32 * width, empty.IndexInBytes);
            Assert.True(cursor.MoveNext(groups, out empty)); Assert.Equal(input.Length, empty.IndexInBytes);
            Assert.False(alias.MoveNext(out _));
            Assert.Equal(2 * width, retained.LengthInBytes);
        }
        if (validation == Utf8InputValidation.Validate)
            Assert.Throws<ArgumentException>(() => regex.Count([.. Encoding.UTF8.GetBytes(prefix + new string('a', 32)), 0xff], floor));
    }

    [Theory]
    [InlineData(2048, "a")] [InlineData(2049, "a")] [InlineData(2050, "a")]
    [InlineData(683, "中")] [InlineData(684, "中")] [InlineData(685, "中")]
    [InlineData(512, "😀")] [InlineData(513, "😀")]
    public void AShortFirstAcceptanceCannotDiscardLongerEndpointsAcrossLongScalarBranches(int length, string scalar)
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat(scalar, length)) + "é");
        var bytes = length * Encoding.UTF8.GetByteCount(scalar);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            // ERE interval numbers stay <=255, including this long fixed branch.
            var regex = new Utf8PosixRegex($"({scalar}|({scalar}{{128}}){{{length / 128}}}{scalar}{{{length % 128}}})", inputValidation: validation);
            var captures = new Utf8ValueMatch[2];
            Assert.Equal(new Utf8ValueMatch(true, true, 0, length * scalar.Length, 0, bytes), regex.Match(input, captures));
            Assert.Equal(bytes, captures[1].LengthInBytes);
            Assert.Equal(bytes, regex.Match(input).LengthInBytes);
            Assert.Equal(1, regex.Count(input));
        }
    }

    [Theory]
    [InlineData("a", false, false)] [InlineData("a", true, false)]
    [InlineData("😀", false, false)] [InlineData("😀", true, false)]
    [InlineData("a", false, true)] [InlineData("a", true, true)]
    [InlineData("😀", false, true)] [InlineData("😀", true, true)]
    public void AmbiguousAlternativesKeepOccurrencePriorityAcrossNestingAndOutputSizes(string scalar, bool nullable, bool fixedSuffix)
    {
        var empty = nullable ? "|" : "";
        var tail = fixedSuffix ? "b" : "";
        var regexes = new List<Utf8PosixRegex>();
        foreach (var pair in new[] { scalar + scalar, scalar + "[^!]", "." + scalar })
        {
            var alternatives = scalar + "|" + pair + empty;
            regexes.AddRange([
                new Utf8PosixRegex($"({alternatives})*{tail}"),
                new Utf8PosixRegex($"({pair}|{scalar}{empty})*{tail}"),
                new Utf8PosixRegex($"(({alternatives})*){tail}"),
                new Utf8PosixRegex($"({scalar}|({pair}){empty})*{tail}"),
            ]);
        }
        const string prefix = "é😀!";
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var width = Encoding.UTF8.GetByteCount(scalar);
        for (var count = 0; count <= 8; count++)
        foreach (var suffix in new[] { "", "!" })
        {
            // Reuse the independent scalar-extent oracle. These alternatives
            // have exactly the 0/1/2 extents on this uniform scalar input; their
            // branch order and instruction/tag layout do not select the winner.
            var extents = Occurrences(0, int.MaxValue, nullable ? 0 : 1, 2, count)!;
            var input = Encoding.UTF8.GetBytes(prefix + string.Concat(Enumerable.Repeat(scalar, count)) + tail + suffix);
            var body = new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length, floor, count * width);
            var whole = new Utf8ValueMatch(true, true, prefix.Length, count * scalar.Length + tail.Length, floor, count * width + tail.Length);
            var last = extents.Length == 0 ? default : new Utf8ValueMatch(true, true,
                prefix.Length + (count - extents[^1]) * scalar.Length, extents[^1] * scalar.Length,
                floor + (count - extents[^1]) * width, extents[^1] * width);
            for (var index = 0; index < regexes.Count; index++)
            {
                var shape = index % 4;
                var regex = regexes[index];
                var captures = new Utf8ValueMatch[shape < 2 ? 2 : 3];
                Assert.Equal(whole, regex.Match(input, captures, floor));
                Assert.Equal(whole, captures[0]);
                Assert.Equal(shape == 2 ? body : last, captures[1]);
                if (shape >= 2)
                    Assert.Equal(shape == 2 || extents.Length > 0 && extents[^1] == 2 ? last : default, captures[2]);
                var shortOutput = new Utf8ValueMatch[1];
                Assert.Equal(whole, regex.Match(input, shortOutput, floor));
                Assert.Equal(whole, shortOutput[0]);
                Assert.Equal(whole, regex.Match(input, floor));
            }
        }
    }

    [Theory]
    [InlineData("(aa|a)(aab)*", "aaab", "a", "aab", null)]
    [InlineData("((aab|aa|ba)*)([ab]*)", "aaba", "aaba", "ba", "")]
    [InlineData("((a|aa)*)(a|aa)", "aaaaa", "aaaa", "aa", "a")]
    [InlineData("(a|aa)*()", "aaaa", "aa", "", null)]
    [InlineData("(a|aa|)*()", "aaaa", "aa", "", null)]
    [InlineData("((a|aa)*)()", "aaaa", "aaaa", "aa", "")]
    [InlineData("((a|ab)*)(bc|b*)", "abc", "a", "a", "bc")]
    [InlineData("((a|a[ab中])*)(中*)", "aaaaaaaaaaaa中", "aaaaaaaaaaaa中", "a中", "")]
    [InlineData("((a|a[^!])*)(é*)", "aaaaaaaaaaaaé", "aaaaaaaaaaaaé", "aé", "")]
    public void LongerWholeOrParentCanOutrankAnEarlierClosedChild(string pattern, string text,
        string first, string second, string? third)
    {
        var expected = third is null ? new[] { first, second } : new[] { first, second, third };
        var regex = new Utf8PosixRegex(pattern);
        var input = Encoding.UTF8.GetBytes(text);
        var captures = new Utf8ValueMatch[expected.Length + 1];
        var whole = regex.Match(input, captures);
        Assert.Equal(new Utf8ValueMatch(true, true, 0, text.Length, 0, input.Length), whole);
        for (var group = 1; group < captures.Length; group++)
        {
            Assert.True(captures[group].Success);
            Assert.Equal(expected[group - 1], Encoding.UTF8.GetString(
                input.AsSpan(captures[group].IndexInBytes, captures[group].LengthInBytes)));
        }
        Assert.Equal(whole, regex.Match(input));
    }

    [Theory]
    [InlineData(1)] [InlineData(16)] [InlineData(80)]
    public void FixedLiteralSuffixKeepsSelectionForShortAndLargePrograms(int suffixLength)
    {
        var tail = new string('b', suffixLength);
        foreach (var validation in Enum.GetValues<Utf8InputValidation>())
        {
            var regex = new Utf8PosixRegex("(a|aa)*" + tail, inputValidation: validation);
            var input = Encoding.ASCII.GetBytes("aaaaaaaa" + tail);
            var groups = new Utf8ValueMatch[2];
            var whole = regex.Match(input, groups);
            Assert.Equal(new Utf8ValueMatch(true, true, 0, input.Length, 0, input.Length), whole);
            Assert.Equal(new Utf8ValueMatch(true, true, 6, 2, 6, 2), groups[1]);
        }
    }

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
