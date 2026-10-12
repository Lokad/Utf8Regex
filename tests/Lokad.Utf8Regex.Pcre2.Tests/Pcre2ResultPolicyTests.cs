using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ResultPolicyTests
{
    private static Utf8Pcre2Regex Create(string pattern, ulong resultBytes) => Utf8Pcre2Regex.Create(
        pattern, Pcre2CompileOptions.None, default, new() { MaxResultBytes = resultBytes }, Timeout.InfiniteTimeSpan);

    [Theory]
    [InlineData("A", "A")]
    [InlineData("[A-Z]", "A")]
    [InlineData("(é)(😀)", "é😀")]
    [InlineData("(*MARK:x)(é)(😀)", "é😀")]
    [InlineData(@"\u0041+", "AAA")]
    public void ResultOnlyPolicyBoundsDetailedPublicationWithoutBoundingScalarValues(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        var tiny = Create(pattern, 1);
        Assert.True(tiny.IsMatch(input));
        Assert.Equal(1, tiny.Count(input));
        Assert.True(tiny.Match(input).Success);
        var error = Assert.Throws<Pcre2MatchException>(() => { _ = tiny.MatchDetailed(input); });
        Assert.Equal("ResultLimit", error.ErrorKind.ToString());
        Assert.Equal(text, Create(pattern, 1 << 20).MatchDetailed(input).GetValueString());
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OwnedNoMatchOutputCannotEscapeResultAdmission(bool text)
    {
        var regex = Create("A", 1);
        var error = Assert.Throws<Pcre2MatchException>(() =>
        {
            if (text) _ = regex.ReplaceToString("zzz"u8, "!");
            else _ = regex.Replace("zzz"u8, "!");
        });
        Assert.Equal("ResultLimit", error.ErrorKind.ToString());
    }

    [Fact]
    public void ResultCapacityIsCumulativeWithoutInvalidatingPublishedSnapshots()
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(1);
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 0, 2 * bytes - 1), Timeout.InfiniteTimeSpan, default);
        var input = Utf8ValidatedInput.Create("😀"u8);
        var first = Pcre2Runner.ProjectSliceCaptures(1, 0, 4, [], [], ref input, ref invocation);
        Pcre2SliceDriver.DetachProjectedGroups(first, ref invocation);
        var alias = invocation;
        Exception? failure = null;
        try { _ = Pcre2Runner.ProjectSliceCaptures(1, 0, 4, [], [], ref input, ref alias); }
        catch (Exception error) { failure = error; }
        Assert.Equal("ResultLimit", Assert.IsType<Pcre2MatchException>(failure).ErrorKind.ToString());
        Assert.Equal(Utf8ExecutionLimitKind.ResultBytes, invocation.TryChargeStep());
        Assert.Equal((0, 4), (first[0].StartOffsetInBytes, first[0].EndOffsetInBytes));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Theory]
    [InlineData("A", "A", 1)]
    [InlineData("(é)(😀)", "é😀", 3)]
    [InlineData(@"(?<v>\u0041)(😀)", "A😀", 3)]
    public void DetailedArrayHasAnExactModeledBoundary(string pattern, string text, int slots)
    {
        var cap = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(slots);
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal(text, Create(pattern, cap).MatchDetailed(input).GetValueString());
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, cap - 1).MatchDetailed(input); }).ErrorKind.ToString());
        Assert.Equal(text, Create(pattern, cap).MatchDetailed(input).GetValueString());
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("(a)b+", "abbb")]
    public void FullProbeChargesOwnedCapturesWhileHardPartialRemainsInline(string pattern, string text)
    {
        var input = Encoding.UTF8.GetBytes(text);
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create(pattern, 1).Probe(input, Pcre2PartialMode.Soft); }).ErrorKind.ToString());
        if (pattern == "(a)b+") Assert.Equal(Utf8Pcre2ProbeKind.PartialMatch, Create(pattern, 1).Probe(input, Pcre2PartialMode.Hard).Kind);
        Assert.Equal(text, Create(pattern, 4096).Probe(input, Pcre2PartialMode.Soft).GetMatch().GetValueString());
    }

    [Theory]
    [InlineData("A", "AzA", "!", "!z!", Pcre2SubstitutionOptions.None)]
    [InlineData(@"(?<v>\u0041)😀", "A😀xA😀", "${v}!", "A!xA!", Pcre2SubstitutionOptions.None)]
    [InlineData("(*MARK:x)(é)(😀)", "é😀xé😀", "$2$1", "😀éx😀é", Pcre2SubstitutionOptions.None)]
    [InlineData("A", "AzA", @"\Ué\E$0", "ÉAzÉA", Pcre2SubstitutionOptions.Extended)]
    public void ActualReplacementProducersEnforceCumulativeResultCapacity(string pattern, string text,
        string replacement, string expected, Pcre2SubstitutionOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        string Invoke(ulong cap) => Create(pattern, cap).ReplaceToString(input, replacement, 0, options);
        Assert.Equal(expected, Invoke(1 << 20));
        ulong first = 1, last = 1 << 20;
        while (first < last)
        {
            var cap = first + (last - first) / 2;
            try { Assert.Equal(expected, Invoke(cap)); last = cap; }
            catch (Pcre2MatchException error) { Assert.Equal("ResultLimit", error.ErrorKind.ToString()); first = cap + 1; }
        }
        Assert.InRange(first, 2UL, (1UL << 20) - 1);
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() => Invoke(first - 1)).ErrorKind.ToString());
        Assert.Equal(expected, Invoke(first));
        Assert.Equal(expected, Encoding.UTF8.GetString(Create(pattern, 1 << 20).Replace(input, replacement, 0, options)));
    }

    [Fact]
    public void SpanReplacementDenialPreservesCallerStorageAndCombinedPolicyIsUsable()
    {
        var destination = new byte[32]; destination.AsSpan().Fill(0xA5);
        Assert.Equal("ResultLimit", Assert.Throws<Pcre2MatchException>(() =>
        { _ = Create("A", 1).TryReplace("AzA"u8, "!"u8, destination, out _); }).ErrorKind.ToString());
        Assert.All(destination, value => Assert.Equal((byte)0xA5, value));
        var regex = Utf8Pcre2Regex.Create("(é)(😀)", Pcre2CompileOptions.None, default,
            new() { MaxWorkSteps = 100000, MaxDepth = 128, MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 }, Timeout.InfiniteTimeSpan);
        Assert.Equal("😀é", regex.ReplaceToString("é😀"u8, "$2$1"));
        Assert.Equal("é😀", regex.MatchDetailed("é😀"u8).GetValueString());
    }

    [Fact]
    public void CallbackResultFailureIsStructuredAndFreshCallsReset()
    {
        var error = Assert.Throws<Pcre2MatchException>(() => Create(@"(?<v>\u0041)😀", 1).ReplaceToString("A😀"u8, 0,
            static (in Utf8Pcre2MatchContext match, ref int state) => "!"));
        Assert.Equal("ResultLimit", error.ErrorKind.ToString());
        var regex = Create(@"(?<v>\u0041)😀", 1 << 20);
        Assert.Equal("!", regex.ReplaceToString("A😀"u8, 0, static (in Utf8Pcre2MatchContext match, ref int state) => "!"));
        Assert.Equal("!", regex.ReplaceToString("A😀"u8, 0, static (in Utf8Pcre2MatchContext match, ref int state) => "!"));
    }
}
