using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Posix.Tests;

public sealed class PosixTextInputContractTests
{
    [Theory]
    [InlineData("(a|aa)(a*)", "é😀 aaa aa", 2)]
    [InlineData("(é|é😀)(.*)", "😀é😀x", 1)]
    [InlineData("()", "é😀x", 4)]
    public void SelectionCapturesAndCursorsUseExistingText(string pattern, string text, int count)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var regex = Utf8PosixRegex.Compile(pattern, default, Timeout.InfiniteTimeSpan, default,
            default, Timeout.InfiniteTimeSpan, default);
        var groups = new Utf8ValueMatch[regex.GroupCount + 1];
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.True(regex.IsMatch(input));
            Assert.Equal(count, regex.Count(input));
            var first = regex.Match(input, groups);
            Assert.True(first.Success);
            Assert.Equal(first, groups[0]);
            using var cursor = regex.CreateMatchCursor(input);
            var observed = 0;
            while (cursor.MoveNext(groups, out _)) observed++;
            Assert.Equal(count, observed);
            Assert.Equal(0, session.SubjectValidations);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void EarlyMatchDoesNotScanTheValidSuffix()
    {
        var input = Encoding.UTF8.GetBytes("needle " + new string('é', 65536));
        var regex = new Utf8PosixRegex("needle", resourcePolicy: new()
        { MaxWorkSteps = 64, MaxWorkingBytes = 1 << 20 });
        Assert.True(regex.IsMatch(input));
        Assert.Equal(6, regex.Match(input, new Utf8ValueMatch[1]).LengthInBytes);
    }
}
