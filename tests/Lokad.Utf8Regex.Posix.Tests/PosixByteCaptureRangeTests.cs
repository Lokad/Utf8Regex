using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixByteCaptureRangeTests
{
    [Theory]
    [InlineData("")]
    [InlineData("é")]
    [InlineData("😀é")]
    public void SuffixAndEmptyEndCapturesKeepUnicodeCoordinates(string suffix)
    {
        const string prefix = "中😀";
        var input = Encoding.UTF8.GetBytes(prefix + "ab" + suffix);
        var start = Encoding.UTF8.GetByteCount(prefix);
        var whole = Value(start, input.Length - start);
        var expected = new[] { whole, Value(start, 2),
            Value(start + 2, input.Length - start - 2),
            Value(input.Length, 0) };
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8PosixRegex("(ab)([é😀]*)()", policy: new Utf8RegexPolicy()
            {
                ResourcePolicy = finite ? new()
                {
                    MaxWorkSteps = 100000,
                    MaxDepth = 1024,
                    MaxWorkingBytes = 1 << 20
                }

    : default
            });
            foreach (var count in new[] { 1, 2, 4, 6 })
            {
                var output = new Utf8CaptureRange[count];
                Assert.Equal(whole, regex.Match(input, captures: output, startOffsetInBytes: start));
                Assert.Equal(expected.Take(count), output.Take(expected.Length));
                Assert.All(output.Skip(expected.Length), value => Assert.False(value.Success));
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CaptureOriginsPreserveOverlappingEmptyAbsentAndPartialOutput(bool empty)
    {
        foreach (var prefix in new[] { new string('p', 8192), new string('é', 4097), string.Concat(Enumerable.Repeat("é😀", 2049)) })
        {
            var text = empty ? "é😀" : "é😀xx";
            var input = Encoding.UTF8.GetBytes(prefix + text);
            var start = Encoding.UTF8.GetByteCount(prefix);
            var regex = new Utf8PosixRegex(empty ? "((é😀)())|(absent)" : "((é😀)(x*))|(absent)");
            var whole = Value(start, Encoding.UTF8.GetByteCount(text));
            var retained = Value(0, 1);
            var expected = new[] {whole,whole,Value(start, 6),
                Value(start+6, empty?0:2),default(Utf8CaptureRange)};
            foreach (var count in new[] { 0, 1, 3, 5, 7 })
            {
                var output = Enumerable.Repeat(retained, count).ToArray();
                Assert.Equal(whole, regex.Match(input, captures: output, startOffsetInBytes: start));
                Assert.Equal(expected.Take(Math.Min(count, 5)), output.Take(5));
                Assert.All(output.Skip(5), value => Assert.Equal(retained, value));
            }
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AdjacentCaptureOriginsKeepGapsNestedRangesAndAbsentSlots(bool nested)
    {
        const string prefix = "中😀";
        const string text = "é😀!中#😀éx";
        var input = Encoding.UTF8.GetBytes(prefix + text);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var whole = At(0, text.Length);
        var expected = nested
            ? new[] { whole, At(0, 5), At(0, 3), At(4, 1), At(6, 3), At(9, 0), default }
            : new[] { whole, At(0, 3), At(4, 1), At(6, 3), At(9, 0), default };
        var sentinel = Value(1, 2);
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8PosixRegex(nested
                ? "((é😀)!(中))#(😀é)()(absent)?x"
                : "(é😀)!(中)#(😀é)()(absent)?x", policy: new Utf8RegexPolicy()
                {
                    ResourcePolicy = finite ? new()
                    {
                        MaxWorkSteps = 1000000,
                        MaxDepth = 1024,
                        MaxWorkingBytes = 1 << 20,
                        MaxResultBytes = 1
                    }

    : default
                });
            foreach (var count in new[] { 0, 1, 3, expected.Length, expected.Length + 2 })
            {
                var output = Enumerable.Repeat(sentinel, count).ToArray();
                Assert.Equal(whole, regex.Match(input, captures: output, startOffsetInBytes: floor));
                Assert.Equal(expected.Take(count), output.Take(expected.Length));
                Assert.All(output.Skip(expected.Length), value => Assert.Equal(sentinel, value));
            }
            using var cursor = regex.CreateMatchCursor(input, floor);
            var groups = new Utf8CaptureRange[expected.Length];
            Assert.True(cursor.MoveNext(captures: groups, match: out var selected));
            Assert.Equal(whole, selected);
            Assert.Equal(expected, groups);
            var retained = groups.ToArray();
            Assert.False(cursor.MoveNext(captures: groups, match: out _));
            Assert.All(groups, value => Assert.False(value.Success));
            Assert.Equal(expected, retained);
        }

        Utf8CaptureRange At(int offset, int length) => Value(floor + Encoding.UTF8.GetByteCount(text.AsSpan(0, offset)), Encoding.UTF8.GetByteCount(text.AsSpan(offset, length)));
    }

    [Fact]
    public void CursorAliasesKeepCaptureCoordinatesAcrossOutputSizes()
    {
        var prefix = string.Concat(Enumerable.Repeat("é😀", 2049));
        var input = Encoding.UTF8.GetBytes(prefix + "é😀xx!é😀!");
        var start = Encoding.UTF8.GetByteCount(prefix);
        var regex = new Utf8PosixRegex("((é😀)(x*))");
        using var cursor = regex.CreateMatchCursor(input, start);
        var alias = cursor; var groups = new Utf8CaptureRange[4];
        Assert.True(cursor.MoveNext(captures: groups, match: out var first));
        Assert.Equal(Value(start, 8), first);
        Assert.Equal(first, groups[1]);
        var retained = groups[2];
        Assert.Equal(Value(start, 6), retained);
        var shortOutput = new Utf8CaptureRange[2];
        Assert.True(alias.MoveNext(captures: shortOutput, match: out var second));
        Assert.Equal(Value(start + 9, 6), second);
        Assert.Equal(second, shortOutput[1]);
        Assert.False(cursor.MoveNext(captures: groups, match: out _));
        Assert.All(groups, value => Assert.False(value.Success));
        Assert.Equal(Value(start, 6), retained);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ForwardCursorKeepsCoordinatesAcrossUnicodeGapsAndEmptyProgression(bool nullable, bool finite)
    {
        var prefix = string.Concat(Enumerable.Repeat("é😀", 2049));
        const string suffix = "é中😀\n!é?";
        var bytes = Encoding.UTF8.GetBytes(prefix + suffix);
        var storage = Enumerable.Repeat((byte)255, bytes.Length + 12).ToArray();
        bytes.CopyTo(storage, 5);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var expected = new List<Utf8CaptureRange[]>();
        var bytePosition = floor;
        foreach (var rune in suffix.EnumerateRunes())
        {
            var consumes = rune.Value is 'é' or 0x1f600;
            if (consumes || nullable) AddExpected(consumes ? rune.Utf8SequenceLength : 0, consumes);
            bytePosition += rune.Utf8SequenceLength;
        }
        if (nullable) AddExpected(0, false);

        var regex = new Utf8PosixRegex(nullable ? "((é|😀)?)()" : "((é|😀))()", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = finite ? new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 1024,
                MaxWorkingBytes = 1 << 20,
                MaxResultBytes = 1
            }

    : default
        });
        using var cursor = regex.CreateMatchCursor(storage.AsMemory(5, bytes.Length), floor);
        var alias = cursor;
        var sentinel = Value(1, 2);
        Utf8CaptureRange retained = default;
        for (var i = 0; i < expected.Count; i++)
        {
            var output = Enumerable.Repeat(sentinel, i % 3 == 0 ? 0 : i % 3 == 1 ? 2 : 5).ToArray();
            Assert.True((i % 2 == 0 ? cursor : alias).MoveNext(captures: output, match: out var whole));
            Assert.Equal(expected[i][0], whole);
            Assert.Equal(expected[i].Take(output.Length), output.Take(4));
            Assert.All(output.Skip(4), value => Assert.Equal(sentinel, value));
            if (i == 0) retained = whole;
        }
        var finalOutput = Enumerable.Repeat(sentinel, 5).ToArray();
        Assert.False(alias.MoveNext(captures: finalOutput, match: out _));
        Assert.All(finalOutput.Take(4), value => Assert.False(value.Success));
        Assert.Equal(sentinel, finalOutput[4]);
        cursor.Dispose(); alias.Dispose();
        Assert.Equal(expected[0][0], retained);
        Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(match: out _));

        void AddExpected(int byteLength, bool participates)
        {
            var whole = Value(bytePosition, byteLength);
            expected.Add([whole, whole, participates ? whole : default,
                Value(bytePosition + byteLength, 0)]);
        }
    }

    [Theory]
    [InlineData("a")]
    [InlineData("é")]
    [InlineData("中")]
    [InlineData("😀")]
    public void LongSuffixCaptureKeepsCoordinatesForBorrowedScalarRanges(string scalar)
    {
        var prefix = new string('p', 4091) + "中😀";
        var content = string.Concat(Enumerable.Repeat(scalar, 4097));
        var text = prefix + "Z" + content;
        var bytes = Encoding.UTF8.GetBytes(text);
        var storage = Enumerable.Repeat((byte)255, bytes.Length + 12).ToArray();
        bytes.CopyTo(storage, 5);
        var input = storage.AsMemory(5, bytes.Length);
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var whole = Value(floor, bytes.Length - floor);
        var expected = new[] { whole,
            Value(floor + 1, bytes.Length - floor - 1),
            Value(bytes.Length, 0) };
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8PosixRegex("Z(" + scalar + "+)()", policy: new Utf8RegexPolicy()
            {
                ResourcePolicy = finite ? new()
                {
                    MaxWorkSteps = 1_000_000,
                    MaxDepth = 32768,
                    MaxWorkingBytes = 8 << 20,
                    MaxResultBytes = 1
                }

    : default
            });
            var output = new Utf8CaptureRange[3];
            Assert.Equal(whole, regex.Match(input.Span, captures: output, startOffsetInBytes: floor));
            Assert.Equal(expected, output);
            using var cursor = regex.CreateMatchCursor(input, floor);
            var alias = cursor;
            Assert.True(cursor.MoveNext(captures: output, match: out var selected));
            Assert.Equal(whole, selected);
            Assert.Equal(expected, output);
            var retained = output.ToArray();
            Assert.False(alias.MoveNext(captures: output, match: out _));
            Assert.All(output, value => Assert.False(value.Success));
            Assert.Equal(expected, retained);
        }
    }

    private static Utf8CaptureRange Value(int bytes, int byteLength)
        => new(bytes, bytes + byteLength);
}
