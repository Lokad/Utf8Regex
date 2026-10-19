using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeCapturePreparationTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CapturedUnicodeFamilyKeepsScalarOperationsWithoutNativeProjectionTables(bool compiled)
    {
        var regex = new Utf8Regex("((😀ab|中def))", Options(compiled), Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 1000000, MaxDepth = 512, MaxWorkingBytes = 2 << 20, MaxResultBytes = 1 << 20 });
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.NotNull(regex.Inspection.PreparedRegex.ExecutionTree);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram!.HasUnicodePatternAtoms);
        Assert.True(Field<bool>(regex, "_hasCaptures"));
        AssertNoMaps(regex);
        var input = Encoding.UTF8.GetBytes("z😀ab z中def");
        Assert.True(regex.IsMatch(input)); Assert.Equal(2, regex.Count(input));
        var first = regex.MatchDetailed(input);
        Assert.Equal((1, 6), (first.IndexInBytes, first.LengthInBytes));
        Assert.Equal("😀ab", first.GetGroup(1).GetValueString());
        Assert.Equal("😀ab", first.GetGroup(2).GetValueString());
        var next = regex.MatchFromUtf16Offset(input, 6);
        Assert.Equal((9, 6), (next.IndexInBytes, next.LengthInBytes));
        Assert.Equal("z<😀ab:😀ab> z<中def:中def>", regex.ReplaceToString(input, "<${2}:${1}>"));
        var parts = new List<string>();
        foreach (var part in regex.EnumerateSplits(input)) parts.Add(part.GetValueString());
        Assert.Equal(new[] { "z", " z", "" }, parts);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, canceled.Token)).CancellationToken);
        Assert.Throws<ArgumentException>(() => regex.IsMatch([.. input, 0xff]));
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("😀ab", first.GetGroup(1).GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CapturedUnicodeFamilyDoesNotRetainUnusedBranchExpansions(bool compiled)
    {
        var pattern = string.Join("|", Enumerable.Range(0, 32).Select(i => $"(?<g{i}>😀token{i:D3}xxxxxxxx)"));
        var regex = new Utf8Regex(pattern, Options(compiled));
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram!.HasUnicodePatternAtoms);
        AssertNoMaps(regex);
        var input = "😀token000xxxxxxxx 😀token031xxxxxxxx"u8.ToArray();
        Assert.Equal(2, regex.Count(input));
        var first = regex.MatchDetailed(input);
        Assert.Equal("😀token000xxxxxxxx", first.GetGroup("g0").GetValueString());
        Assert.False(first.GetGroup("g31").Success);
        Assert.Equal("<😀token000xxxxxxxx> <>", regex.ReplaceToString(input, "<${g0}>"));
        Assert.Equal("😀token000xxxxxxxx", first.GetGroup("g0").GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AbsentNativeTreeDoesNotStartProjectionPreparation(bool compiled)
    {
        var regex = new Utf8Regex(@"(?<a>a+)\k<a>", Options(compiled));
        Assert.Null(regex.Inspection.PreparedRegex.ExecutionTree);
        AssertNoMaps(regex);
        Assert.Equal("aa", regex.MatchDetailed("aaaa"u8).GetGroup("a").GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void AsciiAndAnchoredNativeConsumersRetainTheirPreparation(bool compiled)
    {
        var grouped = new Utf8Regex("(a)(b)", Options(compiled));
        Assert.NotNull(Field<object?>(grouped, "_groupedLiteralSpanTable"));
        Assert.Equal("<b:a> <b:a>", grouped.ReplaceToString("ab ab"u8, "<${2}:${1}>"));
        var family = new Utf8Regex("(?<a>cat)|(?<b>horse)", Options(compiled));
        Assert.NotNull(Field<object?>(family, "_literalFamilyCaptureMap"));
        Assert.NotNull(Field<object?>(family, "_alternationBranchSpanMap"));
        Assert.Equal("<cat|> <|horse>", family.ReplaceToString("cat horse"u8, "<${a}|${b}>"));
        var prefix = new Utf8Regex("^((ab)c)", Options(compiled));
        Assert.NotNull(Field<object?>(prefix, "_groupedLiteralSpanTable"));
        Assert.NotNull(Field<object?>(prefix, "_caretAnchoredGroupedLiteralUtf8"));
        Assert.Equal(1, prefix.Count("abc abc"u8));
        Assert.Equal("<ab> abc", prefix.ReplaceToString("abc abc"u8, "<${2}>"));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void ElidedCaptureDeclarationsKeepTheirExistingNativeProjection(bool compiled, bool unicode)
    {
        var regex = new Utf8Regex("(?<gone>x){0}(?<alsoGone>y){0}(?:" + (unicode ? "😀|中" : "cat|horse") + ")", Options(compiled));
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.Equal(2, regex.Inspection.PreparedRegex.ScalarProgram!.GroupCount);
        Assert.False(Field<bool>(regex, "_hasCaptures"));
        Assert.NotNull(Field<object?>(regex, "_literalFamilyCaptureMap"));
        var input = Encoding.UTF8.GetBytes(unicode ? "😀 中" : "cat horse");
        var hit = regex.MatchDetailed(input);
        Assert.True(hit.Success);
        Assert.False(hit.GetGroup("gone").Success);
        Assert.False(hit.GetGroup("alsoGone").Success);
        Assert.Equal(2, regex.Count(input));
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void FixedFinitePolicyOrDeadlineDoesNotPrepareUnusedAsciiCaptureMaps(bool compiled, bool deadline)
    {
        var policy = deadline ? Utf8ResourcePolicy.Unlimited : new Utf8ResourcePolicy
        { MaxWorkSteps = 1000000, MaxDepth = 64, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 };
        var regex = new Utf8Regex("(?<a>cat)|(?<b>horse)", Options(compiled),
            deadline ? TimeSpan.FromSeconds(30) : Regex.InfiniteMatchTimeout, policy);
        Assert.Equal(NativeExecutionKind.ExactUtf8Literals, regex.Inspection.ExecutionKind);
        Assert.False(regex.Inspection.PreparedRegex.ScalarProgram!.HasUnicodePatternAtoms);
        AssertNoMaps(regex);
        Assert.True(regex.IsMatch("cat horse"u8));
        Assert.Equal(2, regex.Count("cat horse"u8));
        var first = regex.MatchDetailed("cat horse"u8);
        Assert.Equal("cat", first.GetGroup("a").GetValueString());
        Assert.False(first.GetGroup("b").Success);
        Assert.Equal("<cat|> <|horse>", regex.ReplaceToString("cat horse"u8, "<${a}|${b}>"));
        Assert.Equal("cat", first.GetGroup("a").GetValueString());
    }

    private static RegexOptions Options(bool compiled) => RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
    private static T Field<T>(Utf8Regex regex, string name) => (T)typeof(Utf8Regex).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(regex)!;
    private static void AssertNoMaps(Utf8Regex regex)
    {
        foreach (var name in new[] { "_literalFamilyCaptureMap", "_groupedLiteralSpanTable", "_alternationBranchSpanMap" })
            Assert.Null(Field<object?>(regex, name));
    }
}
