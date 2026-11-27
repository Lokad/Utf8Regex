using System.Text;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixTextInputContractTests
{
    [Fact]
    public void EarlyMatchDoesNotScanTheValidSuffix()
    {
        var input = Encoding.UTF8.GetBytes("needle " + new string('é', 65536));
        var regex = new Utf8PosixRegex("needle", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxWorkingBytes = 1 << 20
            }
        });
        Assert.True(regex.IsMatch(input));
        Assert.Equal(6, regex.Match(input, captures: new Utf8CaptureRange[1]).EndOffsetInBytes);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("é")]
    [InlineData("😀")]
    public void FarByteStartPublishesCapturesWithoutScanningThePrefix(string scalar)
    {
        var prefix = string.Concat(Enumerable.Repeat(scalar, 8192));
        var input = Encoding.UTF8.GetBytes(prefix + "é😀");
        var floor = Encoding.UTF8.GetByteCount(prefix);
        var regex = new Utf8PosixRegex("(é😀)()", policy: new Utf8RegexPolicy()
        {
            ResourcePolicy = new()
            {
                MaxDepth = 64,
                MaxWorkingBytes = 1 << 16,
                MaxResultBytes = 1
            }
        });
        using var token = new CancellationTokenSource();
        var groups = new Utf8CaptureRange[3];
        var whole = regex.Match(input, captures: groups, startOffsetInBytes: floor, cancellationToken: token.Token);
        Assert.Equal((floor, input.Length), (whole.StartOffsetInBytes, whole.EndOffsetInBytes));
        Assert.Equal(whole, groups[1]);
        Assert.Equal((input.Length, input.Length), (groups[2].StartOffsetInBytes, groups[2].EndOffsetInBytes));
        Assert.True(groups[2].Success);
        var retained = groups.ToArray();
        using var cursor = regex.CreateMatchCursor(input, floor, token.Token);
        Assert.True(cursor.MoveNext(captures: groups, match: out var selected));
        Assert.Equal(whole, selected);
        Assert.Equal(retained, groups);
        Assert.False(cursor.MoveNext(captures: groups, match: out _));
        Assert.All(groups, range => Assert.False(range.Success));
        cursor.Dispose();
        Assert.Equal(whole, retained[1]);
    }
}
