using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixCoordinateProjectionTests
{
    [Theory]
    [InlineData(false, Utf8InputValidation.Validate)]
    [InlineData(true, Utf8InputValidation.Validate)]
    [InlineData(false, Utf8InputValidation.AssumeValid)]
    [InlineData(true, Utf8InputValidation.AssumeValid)]
    public void CaptureOriginsPreserveOverlappingEmptyAbsentAndPartialOutput(bool empty, Utf8InputValidation policy)
    {
        foreach (var prefix in new[] { new string('p',8192), new string('é',4097), string.Concat(Enumerable.Repeat("é😀",2049)) })
        {
            var text = empty ? "é😀" : "é😀xx";
            var input = Encoding.UTF8.GetBytes(prefix+text);
            var start = Encoding.UTF8.GetByteCount(prefix);
            var regex = new Utf8PosixRegex(empty ? "((é😀)())|(absent)" : "((é😀)(x*))|(absent)", inputValidation:policy);
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

    [Theory]
    [InlineData(Utf8InputValidation.Validate)]
    [InlineData(Utf8InputValidation.AssumeValid)]
    public void CursorAliasesKeepCaptureCoordinatesAcrossOutputSizes(Utf8InputValidation policy)
    {
        var prefix=string.Concat(Enumerable.Repeat("é😀",2049));
        var input=Encoding.UTF8.GetBytes(prefix+"é😀xx!é😀!");
        var start=Encoding.UTF8.GetByteCount(prefix);
        var regex=new Utf8PosixRegex("((é😀)(x*))",inputValidation:policy);
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
