using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReLookaroundCaptureRangeTests
{

    [Fact]
    public void LookaroundEmptyAndUnmatchedRangesKeepCaptureMetadata()
    {
        const string pattern = "(?=(😀é))(?P<whole>😀é)(?P<empty>)(x)?";
        var input = Encoding.UTF8.GetBytes("😀é 😀é");
        var regex = new Utf8PythonRegex(pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxDepth = 8192,
                MaxWorkingBytes = 1048576,
                MaxResultBytes = 1048576
            }
        });
        var collectedRanges1 = new List<Utf8PythonRangeMatch>();
        using (var rangeCursor1 = regex.CreateMatchCursor(input, 0, default))
        {
            while (rangeCursor1.MoveNext(out var rangeMatch1))
                collectedRanges1.Add(rangeMatch1);
        }
        var snapshots = collectedRanges1.ToArray();
        Assert.Equal(2, snapshots.Length);
        for (var row = 0; row < snapshots.Length; row++)
        {
            var match = snapshots[row];
            var start = row * 7;
            Assert.Equal(3, match.LastIndex);
            foreach (var group in match.Groups.ToArray().Take(3))
            {
                Assert.True(group.Success);
                Assert.Equal(start, group.StartOffsetInBytes);
                Assert.Equal(start + 6, group.EndOffsetInBytes);
                Assert.Equal("😀é", Encoding.UTF8.GetString(match.GetGroupMemory(group.Number).Span));
            }
            Assert.True(match.Groups[3].Success);
            Assert.Equal(start + 6, match.Groups[3].StartOffsetInBytes);
            Assert.Equal(string.Empty, System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(3).Span));
            Assert.False(match.Groups[4].Success);
            Assert.Equal(string.Empty, System.Text.Encoding.UTF8.GetString(match.GetGroupMemory(4).Span));
        }
        Assert.True(snapshots[1].Groups[1].Success);
        Assert.Equal("😀é", System.Text.Encoding.UTF8.GetString(snapshots[1].GetGroupMemory(1).Span));
    }
}
