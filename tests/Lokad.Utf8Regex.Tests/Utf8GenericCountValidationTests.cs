using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8GenericCountValidationTests
{
    private static Utf8Regex Create(string pattern, RegexOptions options = RegexOptions.None)
        => new(pattern, options | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000000, MaxDepth = 128,
                MaxWorkingBytes = 1 << 20, MaxResultBytes = 1,
            });

    [Theory]
    [InlineData("(?=.)", RegexOptions.None, "aé😀中", 4)]
    [InlineData(@"(?<x>é)\k<x>", RegexOptions.None, "éé😀éé", 2)]
    [InlineData(@"\k<x>(?<x>é)", RegexOptions.RightToLeft, "éé😀éé", 2)]
    [InlineData("(?<x>.)", RegexOptions.NonBacktracking, "aé😀中", 4)]
    public void ScalarCountsNeedNoDerivedCoordinatesOrResultCapacity(
        string pattern, RegexOptions options, string text, int expected)
    {
        var regex = Create(pattern, options);
        var bytes = Encoding.UTF8.GetBytes("pad" + text + "tail");
        var input = bytes.AsSpan(3, Encoding.UTF8.GetByteCount(text));
        Assert.Equal(expected, regex.Count(input));
        using var token = new CancellationTokenSource();
        Assert.Equal(expected, regex.Count(input, token.Token));
    }

    [Theory]
    [InlineData(2045)]
    [InlineData(2046)]
    [InlineData(2047)]
    [InlineData(2048)]
    public void GenericMissStillValidatesWholeSubjectAndReportsOriginalByteOffset(int prefixLength)
    {
        var regex = Create("a(?=b)");
        var valid = Encoding.UTF8.GetBytes(new string('x', prefixLength) + "😀é");
        var malformed = valid.Append((byte)255).ToArray();
        var error = Assert.Throws<ArgumentException>(() => regex.Count(malformed));
        Assert.Contains($"byte offset {valid.Length}.", error.Message);
        Assert.Equal(0, regex.Count(valid));
    }
}
