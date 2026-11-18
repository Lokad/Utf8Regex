using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeCapturePreparationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CapturedUnicodeFamilyPreservesScalarOperations(bool compiled)
    {
        var regex = new Utf8Regex("((😀ab|中def))", options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = new()
            {
                MaxWorkSteps = 1000000,
                MaxDepth = 512,
                MaxWorkingBytes = 2 << 20,
                MaxResultBytes = 1 << 20
            }
        });
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram!.HasUnicodePatternAtoms);
        Assert.True(Field<bool>(regex, "_hasCaptures"));
        var input = Encoding.UTF8.GetBytes("z😀ab z中def");
        Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
        var first = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal((1, 6), (first.Value.StartOffsetInBytes, (first.Value.EndOffsetInBytes - first.Value.StartOffsetInBytes)));
        Assert.Equal("😀ab", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
        Assert.Equal("😀ab", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(2).Span));
        var next = regex.Match(input, Encoding.UTF8.GetByteCount(Encoding.UTF8.GetString(input).AsSpan(0, 6)), []);
        Assert.Equal((9, 6), (next.StartOffsetInBytes, (next.EndOffsetInBytes - next.StartOffsetInBytes)));
        Assert.Equal("z<😀ab:😀ab> z<中def:中def>", Encoding.UTF8.GetString(regex.Replace(input, "<${2}:${1}>"u8)));
        var parts = new List<string>();
        foreach (var part in regex.EnumerateSplits(input)) parts.Add(Encoding.UTF8.GetString(part.GetValueBytes()));
        Assert.Equal(new[] { "z", " z", "" }, parts);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token)).CancellationToken);
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("😀ab", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CapturedUnicodeFamilyDoesNotRetainUnusedBranchExpansions(bool compiled)
    {
        var pattern = string.Join("|", Enumerable.Range(0, 32).Select(i => $"(?<g{i}>😀token{i:D3}xxxxxxxx)"));
        var regex = new Utf8Regex(pattern, options: Options(compiled));
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram!.HasUnicodePatternAtoms);
        var input = "😀token000xxxxxxxx 😀token031xxxxxxxx"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var first = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.Equal("😀token000xxxxxxxx", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("g0"u8)).Span));
        Assert.True(first.TryGetGroup(32, out var range1));
        Assert.False(range1.Success);
        Assert.Equal("<😀token000xxxxxxxx> <>", Encoding.UTF8.GetString(regex.Replace(input, "<${g0}>"u8)));
        Assert.Equal("😀token000xxxxxxxx", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("g0"u8)).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GeneralProgramPreservesBackreferenceCaptures(bool compiled)
    {
        var regex = new Utf8Regex(@"(?<a>a+)\k<a>", options: Options(compiled));
        Assert.Equal("aa", System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor("aaaa"u8.ToArray())).GetGroupMemory(regex.GroupNumberFromUtf8Name("a"u8)).Span));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AsciiAndAnchoredConsumersPreserveCaptures(bool compiled)
    {
        var grouped = new Utf8Regex("(a)(b)", options: Options(compiled));
        Assert.Equal("<b:a> <b:a>", Encoding.UTF8.GetString(grouped.Replace("ab ab"u8, "<${2}:${1}>"u8)));
        var family = new Utf8Regex("(?<a>cat)|(?<b>horse)", options: Options(compiled));
        Assert.Equal("<cat|> <|horse>", Encoding.UTF8.GetString(family.Replace("cat horse"u8, "<${a}|${b}>"u8)));
        var prefix = new Utf8Regex("^((ab)c)", options: Options(compiled));
        Assert.Equal(1, prefix.Count("abc abc"u8));
        Assert.Equal("<ab> abc", Encoding.UTF8.GetString(prefix.Replace("abc abc"u8, "<${2}>"u8)));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ElidedCaptureDeclarationsKeepAbsentSlots(bool compiled, bool unicode)
    {
        var regex = new Utf8Regex("(?<gone>x){0}(?<alsoGone>y){0}(?:" + (unicode ? "😀|中" : "cat|horse") + ")", options: Options(compiled));
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(2, regex.Inspection.PreparedRegex.ScalarProgram!.GroupCount);
        Assert.False(Field<bool>(regex, "_hasCaptures"));
        var input = Encoding.UTF8.GetBytes(unicode ? "😀 中" : "cat horse");
        var hit = Utf8MatchSequence.First(regex.CreateMatchCursor(input));
        Assert.True(hit.Success);
        Assert.True(hit.TryGetGroup(1, out var range2));
        Assert.False(range2.Success);
        Assert.True(hit.TryGetGroup(2, out var range3));
        Assert.False(range3.Success);
        Assert.Equal(2, regex.Count(input));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void FixedFinitePolicyOrDeadlinePreservesAsciiCaptures(bool compiled, bool deadline)
    {
        var policy = deadline ? Utf8ResourcePolicy.Unlimited : new Utf8ResourcePolicy
        { MaxWorkSteps = 1000000, MaxDepth = 64, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var regex = new Utf8Regex("(?<a>cat)|(?<b>horse)", options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = deadline ? TimeSpan.FromSeconds(30) : Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.False(regex.Inspection.PreparedRegex.ScalarProgram!.HasUnicodePatternAtoms);
        Assert.True(regex.IsMatch("cat horse"u8));
        Assert.Equal(2, regex.Count("cat horse"u8));
        var first = Utf8MatchSequence.First(regex.CreateMatchCursor("cat horse"u8.ToArray()));
        Assert.Equal("cat", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("a"u8)).Span));
        Assert.True(first.TryGetGroup(2, out var range4));
        Assert.False(range4.Success);
        Assert.Equal("<cat|> <|horse>", Encoding.UTF8.GetString(regex.Replace("cat horse"u8, "<${a}|${b}>"u8)));
        Assert.Equal("cat", System.Text.Encoding.UTF8.GetString(first.GetGroupMemory(regex.GroupNumberFromUtf8Name("a"u8)).Span));
    }

    private static RegexOptions Options(bool compiled) => RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
    private static T Field<T>(Utf8Regex regex, string name) => (T)typeof(Utf8Regex).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
}
