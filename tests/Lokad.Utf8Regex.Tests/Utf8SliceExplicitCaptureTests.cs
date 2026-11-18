using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 ExplicitCapture on the slice: unnamed groups lower transparently (no
// slot, no number), so numbering, group APIs, and backreference digits follow
// the named groups only. Unbound digits decline to legacy-exact construction
// failures. Bare shapes keep native precedence.
public sealed class Utf8SliceExplicitCaptureTests
{
    public static IEnumerable<object?[]> SliceExplicitShapes()
    {
        yield return new object?[] { "(a)+(?<x>b)+" };
        yield return new object?[] { "(?:a)+(?<x>b)+" };
        yield return new object?[] { "(a)(?<x>b)\\1" };
        yield return new object?[] { "(\\p{L}+)(?<x>y)" };
        yield return new object?[] { "([\\p{L}a]+)(?<x>y)" };
    }

    [Theory]
    [MemberData(nameof(SliceExplicitShapes))]
    public void ExplicitShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(a)(b)", "ab", true };
        yield return new object?[] { "(a)(b)", "ac", false };
        yield return new object?[] { "(a)(?<x>b)", "ab", true };
        yield return new object?[] { "(a)+(?<x>b)+", "aabb", true };
        yield return new object?[] { "(a)(?<x>b)\\1", "abb", true };
        yield return new object?[] { "(a)(?<x>b)\\1", "aba", false };
        yield return new object?[] { "(\\p{L}+)(?<x>y)", "aby", true };
        yield return new object?[] { "(\\p{L}+)(?<x>y)", "ab", false };
        yield return new object?[] { "([\\p{L}a]+)(?<x>y)", "aBy", true };
        yield return new object?[] { "(\\p{L}+)(x)", "abx", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options: options);
            Assert.Equal(expected, regex.IsMatch(input));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
            }
        }
    }

    [Fact]
    public void OwnedGroupApisSkipUnnamedGroups()
    {
        var regex = new Utf8Regex("(a)+(?<x>b)+", options: RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var oracle = new Regex("(a)+(?<x>b)+", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var copiedNames19 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot19 = 0; captureSlot19 < copiedNames19.Length; captureSlot19++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot19, [], out var nameLength19));
            var nameBytes19 = new byte[nameLength19];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot19, nameBytes19, out var written19));
            Assert.Equal(nameLength19, written19);
            copiedNames19[captureSlot19] = System.Text.Encoding.UTF8.GetString(nameBytes19);
        }
        Assert.Equal(oracle.GetGroupNames(), copiedNames19);
        Assert.Equal(oracle.GetGroupNumbers(), Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
        var copiedNames20 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot20 = 0; captureSlot20 < copiedNames20.Length; captureSlot20++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot20, [], out var nameLength20));
            var nameBytes20 = new byte[nameLength20];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot20, nameBytes20, out var written20));
            Assert.Equal(nameLength20, written20);
            copiedNames20[captureSlot20] = System.Text.Encoding.UTF8.GetString(nameBytes20);
        }
        Assert.Equal(new[] { "0", "x" }, copiedNames20);
        Assert.Equal(new[] { 0, 1 }, Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes("ab")));
        Assert.True(detailed.Success);
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Fact]
    public void OwnedCategoryGroupApisSkipUnnamedGroups()
    {
        var regex = new Utf8Regex("(\\p{L}+)(?<x>y)", options: RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var oracle = new Regex("(\\p{L}+)(?<x>y)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var copiedNames21 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot21 = 0; captureSlot21 < copiedNames21.Length; captureSlot21++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot21, [], out var nameLength21));
            var nameBytes21 = new byte[nameLength21];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot21, nameBytes21, out var written21));
            Assert.Equal(nameLength21, written21);
            copiedNames21[captureSlot21] = System.Text.Encoding.UTF8.GetString(nameBytes21);
        }
        Assert.Equal(oracle.GetGroupNames(), copiedNames21);
        Assert.Equal(oracle.GetGroupNumbers(), Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
        var copiedNames22 = new string[regex.CaptureGroupCount + 1];
        for (var captureSlot22 = 0; captureSlot22 < copiedNames22.Length; captureSlot22++)
        {
            Assert.Equal(System.Buffers.OperationStatus.DestinationTooSmall,
                regex.TryCopyGroupNameUtf8(captureSlot22, [], out var nameLength22));
            var nameBytes22 = new byte[nameLength22];
            Assert.Equal(System.Buffers.OperationStatus.Done,
                regex.TryCopyGroupNameUtf8(captureSlot22, nameBytes22, out var written22));
            Assert.Equal(nameLength22, written22);
            copiedNames22[captureSlot22] = System.Text.Encoding.UTF8.GetString(nameBytes22);
        }
        Assert.Equal(new[] { "0", "x" }, copiedNames22);
        Assert.Equal(new[] { 0, 1 }, Enumerable.Range(0, regex.CaptureGroupCount + 1).Select(regex.GroupNumberFromCaptureSlot).ToArray());
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes("aby")));
        Assert.True(detailed.Success);
        Assert.Equal("y", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData("(a)\\1")]
    [InlineData("(a)(b)\\2")]
    [InlineData("(a)(?<x>b)\\2")]
    public void UnboundDigitsFailConstruction(string pattern)
    {
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex(pattern, options: RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant));
    }
}
