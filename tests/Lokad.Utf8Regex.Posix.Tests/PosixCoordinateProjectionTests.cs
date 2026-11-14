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

    private static Utf8ValueMatch Value(int bytes,int byteLength,int utf16,int utf16Length)
        => new(true,true,utf16,utf16Length,bytes,byteLength);
}
