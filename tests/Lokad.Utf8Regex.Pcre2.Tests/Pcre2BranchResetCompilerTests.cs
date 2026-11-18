using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BranchResetCompilerTests
{
    [Theory]
    [InlineData("(?|(a)|(b))", "b", true, 2)]
    [InlineData("(?|(a)(b)|(c))", "c", true, 3)]
    [InlineData("(?|(?<x>a)|(?<x>b))\\k<x>", "bb", true, 2)]
    [InlineData("(?|a|b)", "a", true, 1)]
    [InlineData("(?|)", "", true, 1)]
    [InlineData("(?=(?|(a)|(b)))a", "a", true, 2)]
    [InlineData("(?|(?|(a)|(b))|(c))\\1", "cc", true, 2)]
    [InlineData("(a)|(b)", "b", false, 3)]
    [InlineData(@"\(\?\|a\)", "(?|a)", false, 1)]
    [InlineData(@"\Q(?|a)\E", "(?|a)", false, 1)]
    [InlineData("[?|()]", "|", false, 1)]
    [InlineData("(?# (?| )a", "a", false, 1)]
    [InlineData("(?x)a # (?| ignored\n", "a", false, 1)]
    public void AnalysisUsesParsedBranchResetSyntax(
        string pattern, string input, bool usesBranchReset, int captureSlotCount)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        foreach (var regex in new[] { new Utf8Pcre2Regex(pattern), new Utf8Pcre2Regex(Encoding.UTF8.GetBytes(pattern)) })
        {
            Assert.Equal(usesBranchReset, regex.Analyze().UsesBranchReset);
            ReadOnlySpan<byte> match1Input = bytes;
            var match1Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
            var match = regex.Match(match1Input, captures: match1Captures);
            Assert.True(match.Success);
            Assert.Equal(captureSlotCount, (match.Success ? match1Captures.Length : 0));
        }
    }

    [Theory]
    [InlineData("(?|(abc)|(xyz))\\1", "abcabc", "abc")]
    [InlineData("(?|(abc)|(xyz))\\1", "xyzxyz", "xyz")]
    [InlineData("(?|(?|(a)|(b))|(c))\\1", "cc", "c")]
    public void BranchResetAlternativesShareNumericSlots(
        string pattern,
        string input,
        string capture)
    {
        var regex = new Utf8Pcre2Regex(pattern);
        ReadOnlySpan<byte> match2Input = Encoding.UTF8.GetBytes(input);
        var match2Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var match = regex.Match(match2Input, captures: match2Captures);

        Assert.True(match.Success);
        Assert.Equal(capture, System.Text.Encoding.UTF8.GetString((match2Captures[1].Success ? match2Input.Slice(match2Captures[1].StartOffsetInBytes, match2Captures[1].EndOffsetInBytes - match2Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void FollowupNumberingUsesTheWidestBranch()
    {
        var regex = new Utf8Pcre2Regex("(x)(?|(a)(b)|(c))(d)");
        ReadOnlySpan<byte> shortBranch3Input = "xcd"u8;
        var shortBranch3Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var shortBranch = regex.Match(shortBranch3Input, captures: shortBranch3Captures);
        ReadOnlySpan<byte> wideBranch4Input = "xabd"u8;
        var wideBranch4Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var wideBranch = regex.Match(wideBranch4Input, captures: wideBranch4Captures);

        Assert.True(shortBranch.Success);
        Assert.Equal(5, (shortBranch.Success ? shortBranch3Captures.Length : 0));
        Assert.Equal("c", System.Text.Encoding.UTF8.GetString((shortBranch3Captures[2].Success ? shortBranch3Input.Slice(shortBranch3Captures[2].StartOffsetInBytes, shortBranch3Captures[2].EndOffsetInBytes - shortBranch3Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.False(shortBranch3Captures[3].Success);
        Assert.Equal("d", System.Text.Encoding.UTF8.GetString((shortBranch3Captures[4].Success ? shortBranch3Input.Slice(shortBranch3Captures[4].StartOffsetInBytes, shortBranch3Captures[4].EndOffsetInBytes - shortBranch3Captures[4].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));

        Assert.True(wideBranch.Success);
        Assert.Equal("a", System.Text.Encoding.UTF8.GetString((wideBranch4Captures[2].Success ? wideBranch4Input.Slice(wideBranch4Captures[2].StartOffsetInBytes, wideBranch4Captures[2].EndOffsetInBytes - wideBranch4Captures[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((wideBranch4Captures[3].Success ? wideBranch4Input.Slice(wideBranch4Captures[3].StartOffsetInBytes, wideBranch4Captures[3].EndOffsetInBytes - wideBranch4Captures[3].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal("d", System.Text.Encoding.UTF8.GetString((wideBranch4Captures[4].Success ? wideBranch4Input.Slice(wideBranch4Captures[4].StartOffsetInBytes, wideBranch4Captures[4].EndOffsetInBytes - wideBranch4Captures[4].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void SameNameAndNumberProducesOneNameTableEntryWithoutDuplicateNameMode()
    {
        var regex = new Utf8Pcre2Regex("(?|(?<x>a)|(?<x>b))\\k<x>");
        ReadOnlySpan<byte> match5Input = "bb"u8;
        var match5Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        var match = regex.Match(match5Input, captures: match5Captures);
        var nameBytes = new byte[1];

        Assert.True(match.Success);
        Assert.Equal(System.Buffers.OperationStatus.Done,
            regex.TryCopyNameEntryUtf8(0, nameBytes, out var bytesRequired, out var groupNumber));
        Assert.Equal("x"u8.ToArray(), nameBytes);
        Assert.Equal(1, bytesRequired);
        Assert.Equal(1, groupNumber);
        Assert.Equal("b", System.Text.Encoding.UTF8.GetString((match5Captures[1].Success ? match5Input.Slice(match5Captures[1].StartOffsetInBytes, match5Captures[1].EndOffsetInBytes - match5Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
    }

    [Fact]
    public void DuplicateNamedBackreferenceSelectsTheFirstSetSlot()
    {
        var regex = CreateDuplicateNameRegex();
        ReadOnlySpan<byte> foo6Input = "foofoo"u8;
        var foo6Captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];

        var foo = regex.Match(foo6Input, captures: foo6Captures);
        ReadOnlyMemory<byte> bar7Input = ("barbar"u8).ToArray();
        using var bar7Cursor = regex.CreateMatchCursor(bar7Input);
        bar7Cursor.MoveNext(out var bar7Result);
        bar7Cursor.Dispose();
        var bar = bar7Result;
        Span<int> numbers = stackalloc int[2];

        Assert.True(foo.Success);
        Assert.Equal("foo", System.Text.Encoding.UTF8.GetString((foo6Captures[1].Success ? foo6Input.Slice(foo6Captures[1].StartOffsetInBytes, foo6Captures[1].EndOffsetInBytes - foo6Captures[1].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.False(foo6Captures[2].Success);
        Assert.True(bar.Success);
        Assert.False(bar.Groups[1].Success);
        Assert.Equal("bar", System.Text.Encoding.UTF8.GetString((bar.Groups[2].Success ? bar7Input.Span.Slice(bar.Groups[2].StartOffsetInBytes, bar.Groups[2].EndOffsetInBytes - bar.Groups[2].StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.True(bar.TryGetGroup(2, out var selected)); Assert.True(selected.Success);
        Assert.Equal("bar", System.Text.Encoding.UTF8.GetString((selected.Success ? bar7Input.Span.Slice(selected.StartOffsetInBytes, selected.EndOffsetInBytes - selected.StartOffsetInBytes) : ReadOnlySpan<byte>.Empty)));
        Assert.Equal(2, regex.CopyNumbersForUtf8Name("n"u8, numbers, out var isMore));
        Assert.False(isMore);
        Assert.Equal([1, 2], numbers.ToArray());
    }

    [Fact]
    public void DuplicateNamesWorkAcrossGlobalAndReplacementOperations()
    {
        var regex = CreateDuplicateNameRegex();
        var input = "foofoo barbar"u8;

        Assert.Equal(2, regex.Count(input));
        Assert.Equal("<foo> <bar>", Encoding.UTF8.GetString(regex.Replace(input, "<$n>")));
    }

    [Fact]
    public void InlineDuplicateNameOptionEnablesTheSameGenericSlotSet()
    {
        var regex = new Utf8Pcre2Regex("(?J)(?:(?<n>foo)|(?<n>bar))\\k<n>");

        Assert.True(regex.IsMatch("barbar"u8));
        Assert.Equal(2, regex.NameEntryCount);
        Assert.IsType<Pcre2BacktrackingDirectProgram>(regex.DebugCompiledProgram.Operations.Match);
    }

    [Fact]
    public void DifferentNamesForOneBranchResetNumberAreRejected()
    {
        var error = Assert.Throws<Pcre2CompileException>(
            () => new Utf8Pcre2Regex("(?|(?<x>a)|(?<y>b))"));

        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, error.ErrorKind);
    }

    private static Utf8Pcre2Regex CreateDuplicateNameRegex() => new("(?:(?<n>foo)|(?<n>bar))\\k<n>", options: Pcre2CompileOptions.None, compileSettings: new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }, policy: new Utf8RegexPolicy
    {
        MatchTimeout = System.Text.RegularExpressions.Regex.InfiniteMatchTimeout
    });
}
