using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReDetailedTextAdmissionTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FullRangeCapturesAdmitOneOwnedTextExtent(bool oneUnder)
    {
        var text = new string('x', 1000);
        var input = Encoding.UTF8.GetBytes(text);
        var extent = Utf8ConstructionBudget.ArrayBytes<Utf8PythonGroupMatchData>(3)
            + Utf8ConstructionBudget.StringBytes(text.Length);
        var regex = new Utf8PythonRegex("((x+))", PythonReCompileOptions.None,
            Regex.InfiniteMatchTimeout, new()
            {
                MaxWorkSteps = 100000, MaxDepth = 8192, MaxWorkingBytes = 1048576,
                MaxResultBytes = extent - (oneUnder ? 1UL : 0UL),
            });
        using var cursor = regex.CreateMatchCursor(input);
        if (oneUnder)
        {
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => cursor.MoveNext());
            Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes, error.ResourceKind);
            Assert.Equal(extent, error.Requested);
            Assert.Equal(0UL, cursor.WorkingInvocation.Ledger.ReservedBytes);
            return;
        }

        Assert.True(cursor.MoveNext());
        var retained = cursor.Current;
        Assert.All(retained.Groups, group =>
        {
            Assert.True(group.Success);
            Assert.Equal(0, group.StartOffsetInBytes);
            Assert.Equal(input.Length, group.EndOffsetInBytes);
            Assert.Equal(text, group.ValueText);
        });
        Assert.Equal(extent, cursor.WorkingInvocation.Budget.ResultBytes);
        Assert.False(cursor.MoveNext());
        cursor.Dispose();
        input.AsSpan().Fill((byte)'z');
        Assert.All(retained.Groups, group => Assert.Equal(text, group.ValueText));
    }

    [Fact]
    public void SharedTextKeepsLookaroundEmptyUnmatchedAndSnapshotMetadata()
    {
        const string pattern = "(?=(😀é))(?P<whole>😀é)(?P<empty>)(x)?";
        var input = Encoding.UTF8.GetBytes("😀é 😀é");
        var regex = new Utf8PythonRegex(pattern, PythonReCompileOptions.None,
            Regex.InfiniteMatchTimeout, new()
            {
                MaxWorkSteps = 100000, MaxDepth = 8192,
                MaxWorkingBytes = 1048576, MaxResultBytes = 1048576,
            });
        var snapshots = regex.FindIterDetailed(input);
        Assert.Equal(2, snapshots.Length);
        for (var row = 0; row < snapshots.Length; row++)
        {
            var match = snapshots[row];
            var start = row * 7;
            Assert.Equal(3, match.LastIndex);
            Assert.Equal("empty", match.LastGroup);
            foreach (var group in match.Groups.Take(3))
            {
                Assert.True(group.Success);
                Assert.Equal(start, group.StartOffsetInBytes);
                Assert.Equal(start + 6, group.EndOffsetInBytes);
                Assert.Equal(row * 4, group.StartOffsetInUtf16);
                Assert.Equal(row * 4 + 3, group.EndOffsetInUtf16);
                Assert.Equal("😀é", group.ValueText);
            }
            Assert.True(match.Groups[3].Success);
            Assert.Equal(start + 6, match.Groups[3].StartOffsetInBytes);
            Assert.Equal(string.Empty, match.Groups[3].ValueText);
            Assert.False(match.Groups[4].Success);
            Assert.Equal(string.Empty, match.Groups[4].ValueText);
        }
        snapshots[0].Groups[1] = default;
        input.AsSpan().Clear();
        Assert.True(snapshots[1].Groups[1].Success);
        Assert.Equal("😀é", snapshots[1].Groups[1].ValueText);
    }
}
