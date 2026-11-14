using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixCoordinateProjectionTests
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
        var whole = Value(start, input.Length - start, prefix.Length, 2 + suffix.Length);
        var expected = new[] { whole, Value(start, 2, prefix.Length, 2),
            Value(start + 2, input.Length - start - 2, prefix.Length + 2, suffix.Length),
            Value(input.Length, 0, prefix.Length + 2 + suffix.Length, 0) };
        foreach (var finite in new[] { false, true })
        {
            var regex = new Utf8PosixRegex("(ab)([é😀]*)()", resourcePolicy: finite
                ? new() { MaxWorkSteps = 100000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20 }
                : default);
            foreach (var count in new[] { 1, 2, 4, 6 })
            {
                var output = new Utf8ValueMatch[count];
                Assert.Equal(whole, regex.Match(input, output, start));
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
        foreach (var prefix in new[] { new string('p',8192), new string('é',4097), string.Concat(Enumerable.Repeat("é😀",2049)) })
        {
            var text = empty ? "é😀" : "é😀xx";
            var input = Encoding.UTF8.GetBytes(prefix+text);
            var start = Encoding.UTF8.GetByteCount(prefix);
            var regex = new Utf8PosixRegex(empty ? "((é😀)())|(absent)" : "((é😀)(x*))|(absent)");
            var whole = Value(start,Encoding.UTF8.GetByteCount(text),prefix.Length,text.Length);
            var retained = Value(0,1,0,1);
            var expected = new[] {whole,whole,Value(start,6,prefix.Length,3),
                Value(start+6,empty?0:2,prefix.Length+3,empty?0:2),default(Utf8ValueMatch)};
            foreach (var count in new[]{0,1,3,5,7})
            {
                var output = Enumerable.Repeat(retained,count).ToArray();
                Assert.Equal(whole,regex.Match(input,output,start));
                Assert.Equal(expected.Take(Math.Min(count,5)),output.Take(5));
                Assert.All(output.Skip(5),value => Assert.Equal(retained,value));
            }
        }
    }
    [Fact]

    public void CursorAliasesKeepCaptureCoordinatesAcrossOutputSizes()
    {
        var prefix=string.Concat(Enumerable.Repeat("é😀",2049));
        var input=Encoding.UTF8.GetBytes(prefix+"é😀xx!é😀!");
        var start=Encoding.UTF8.GetByteCount(prefix);
        var regex=new Utf8PosixRegex("((é😀)(x*))");
        using var cursor=regex.CreateMatchCursor(input,start);
        var alias=cursor; var groups=new Utf8ValueMatch[4];
        Assert.True(cursor.MoveNext(groups,out var first));
        Assert.Equal(Value(start,8,prefix.Length,5),first);
        Assert.Equal(first,groups[1]);
        var retained=groups[2];
        Assert.Equal(Value(start,6,prefix.Length,3),retained);
        var shortOutput=new Utf8ValueMatch[2];
        Assert.True(alias.MoveNext(shortOutput,out var second));
        Assert.Equal(Value(start+9,6,prefix.Length+6,3),second);
        Assert.Equal(second,shortOutput[1]);
        Assert.False(cursor.MoveNext(groups,out _));
        Assert.All(groups,value => Assert.False(value.Success));
        Assert.Equal(Value(start,6,prefix.Length,3),retained);
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
        var expected = new List<Utf8ValueMatch[]>();
        var bytePosition = floor;
        var utf16Position = prefix.Length;
        foreach (var rune in suffix.EnumerateRunes())
        {
            var consumes = rune.Value is 'é' or 0x1f600;
            if (consumes || nullable) AddExpected(consumes ? rune.Utf8SequenceLength : 0,
                consumes ? rune.Utf16SequenceLength : 0, consumes);
            bytePosition += rune.Utf8SequenceLength;
            utf16Position += rune.Utf16SequenceLength;
        }
        if (nullable) AddExpected(0, 0, false);

        var regex = new Utf8PosixRegex(nullable ? "((é|😀)?)()" : "((é|😀))()", resourcePolicy: finite
            ? new() { MaxWorkSteps = 1000000, MaxDepth = 1024, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 }
            : default);
        using var cursor = regex.CreateMatchCursor(storage.AsMemory(5, bytes.Length), floor);
        var alias = cursor;
        var sentinel = Value(1, 2, 3, 4);
        Utf8ValueMatch retained = default;
        for (var i = 0; i < expected.Count; i++)
        {
            var output = Enumerable.Repeat(sentinel, i % 3 == 0 ? 0 : i % 3 == 1 ? 2 : 5).ToArray();
            Assert.True((i % 2 == 0 ? cursor : alias).MoveNext(output, out var whole));
            Assert.Equal(expected[i][0], whole);
            Assert.Equal(expected[i].Take(output.Length), output.Take(4));
            Assert.All(output.Skip(4), value => Assert.Equal(sentinel, value));
            if (i == 0) retained = whole;
        }
        var finalOutput = Enumerable.Repeat(sentinel, 5).ToArray();
        Assert.False(alias.MoveNext(finalOutput, out _));
        Assert.All(finalOutput.Take(4), value => Assert.False(value.Success));
        Assert.Equal(sentinel, finalOutput[4]);
        cursor.Dispose(); alias.Dispose();
        Assert.Equal(expected[0][0], retained);
        Assert.Throws<ObjectDisposedException>(() => alias.MoveNext(out _));

        void AddExpected(int byteLength, int utf16Length, bool participates)
        {
            var whole = Value(bytePosition, byteLength, utf16Position, utf16Length);
            expected.Add([whole, whole, participates ? whole : default,
                Value(bytePosition + byteLength, 0, utf16Position + utf16Length, 0)]);
        }
    }

    private static Utf8ValueMatch Value(int bytes,int byteLength,int utf16,int utf16Length)
        => new(true,true,utf16,utf16Length,bytes,byteLength);
}
