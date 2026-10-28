using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8QuotedOperandCountTests
{
    private const string Template = "(('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\").*?(\\n^.*?){0,4}(('|\")[a-zA-Z0-9+/]{40}('|\"))+|('|\")[a-zA-Z0-9+/]{40}('|\").*?(\\n^.*?){0,3}('|\")((?:ASIA|AKIA|AROA|AIDA)([A-Z0-7]{16}))('|\"))+";
    private static readonly string Pattern = Template.Replace("ASIA|AKIA|AROA|AIDA", "AB|CD")
        .Replace("[A-Z0-7]{16}", "[0-9]{2}").Replace("[a-zA-Z0-9+/]{40}", "[a-z]{3}");

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    public void NecessaryOperandsRetainBothOrdersLazyRepeatsAndCaptures(RegexOptions extra)
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.Multiline | extra);
        var oracle = new Regex(Pattern, RegexOptions.Multiline | extra);
        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.Inspection.ExecutionKind);
        Assert.Equal(PreparedSearcherKind.MultiLiteral, regex.Inspection.SearchPlan.RequiredPrefilterSearcher.Kind);
        Assert.False(regex.Inspection.SearchPlan.CanGuideFallbackStarts);
        foreach (var text in new[] { "", "EASTASIA AB12 CD34", "\"AB12\"", "\"ABxx\" \"abc\"",
            "\"AB12\"😀\"abc\"", "'abc' λ 'CD34'", "\"AB12\"\nctx\n\"abc\"",
            "\"AB12\"\"abc\"\"CD34\"\"abc\"", "é\"AB12\" \"abc\" 😀 \"abc\" \"CD34\"" })
        {
            var padded = Encoding.UTF8.GetBytes("pad" + text + "pad");
            var input = padded.AsSpan(3, Encoding.UTF8.GetByteCount(text));
            Assert.Equal(oracle.Count(text), regex.Count(input));
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input));
            var expected = oracle.Match(text);
            var whole = regex.Match(input);
            var actual = regex.MatchDetailed(input);
            Assert.Equal(expected.Success, whole.Success);
            Assert.Equal(expected.Success, actual.Success);
            if (expected.Success)
            {
                Assert.Equal(expected.Value, Encoding.UTF8.GetString(input.Slice(whole.IndexInBytes, whole.LengthInBytes)));
                for (var group = 0; group < expected.Groups.Count; group++)
                    Assert.Equal(expected.Groups[group].Value, actual.GetGroup(group).GetValueString());
                Assert.Equal(oracle.Count(text), regex.Count(input));
                Assert.Equal(expected.Value, actual.GetGroup(0).GetValueString());
            }
        }
    }

    [Theory]
    [InlineData("+?")]
    [InlineData("{2,}")]
    public void OtherOuterRepeatsKeepScalarSelection(string quantifier)
    {
        var pattern = Pattern[..^1] + quantifier;
        var regex = new Utf8Regex(pattern, RegexOptions.Multiline);
        Assert.NotEqual(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.Inspection.ExecutionKind);
        foreach (var text in new[] { "\"AB12\"\"abc\"", "\"AB12\"\"abc\"\"CD34\"\"abc\"" })
            Assert.Equal(new Regex(pattern, RegexOptions.Multiline).Count(text), regex.Count(Encoding.UTF8.GetBytes(text)));
    }

    [Fact]
    public void NativeAsciiCountHonorsItsExistingPrefilterSkipPolicy()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.Multiline);
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            Assert.Equal(0, regex.Count("unrelated ASCII text"u8));
            Assert.Equal(Utf8ExecutionRoute.NativeStructuralLinearAutomaton, session.ExecutionRoute);
        }
        finally { session.Complete(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingQuotedPrefixNeedsNoVmCapacityButPresenceStillNeedsAdmission(bool count)
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.Multiline, Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 4096, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var absent = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("EASTASIA AB12 CD34 😀 ", 2048)));
        if (count) Assert.Equal(0, regex.Count(absent));
        else Assert.False(regex.IsMatch(absent));
        Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (count) regex.Count("\"AB12\" missing operand"u8);
            else regex.IsMatch("\"AB12\" missing operand"u8);
        });
    }

    [Fact]
    public void UnicodeTailPredicatesRetainScalarSemantics()
    {
        var regex = new Utf8Regex(Pattern.Replace("[0-9]", "[^q]"), RegexOptions.Multiline);
        Assert.Equal(NativeExecutionKind.AsciiStructuralQuotedRelation, regex.Inspection.ExecutionKind);
        Assert.Equal(1, regex.Count("\"AB😀é\" \"abc\""u8));
        Assert.True(regex.IsMatch("\"abc\" \"CD😀é\""u8));
    }

    [Fact]
    public void OffsetsAndMalformedTailsKeepTheirExistingMeaning()
    {
        var regex = new Utf8Regex(Pattern, RegexOptions.Multiline);
        const string text = "é\"AB12\" \"abc\" 😀 'abc' 'CD34'";
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(new Regex(Pattern, RegexOptions.Multiline).Matches(text, 14).Count,
            regex.CountFromUtf16Offset(bytes, 14));
        foreach (var prefix in new[] { "EASTASIA", "\"AB12\" \"abc\"", "😀" })
        {
            var malformed = Encoding.UTF8.GetBytes(prefix).Append((byte)255).ToArray();
            Assert.Throws<ArgumentException>(() => regex.Count(malformed));
            Assert.Throws<ArgumentException>(() => regex.IsMatch(malformed));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FamilySearchPollsTheOriginalAuthorityAfterValidation(bool expire)
    {
        var input = Encoding.UTF8.GetBytes(new string('x', 200000));
        var calibration = new Utf8SliceInvocation(new(4096, 1, 1, 1), Regex.InfiniteMatchTimeout, default);
        Utf8Validation.ThrowIfInvalidOnly(input, ref calibration);
        var threshold = ((calibration.Budget.Steps / 64) + 1) * 64;
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex(Pattern, RegexOptions.Multiline,
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout,
            new() { MaxWorkSteps = 4096, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 });
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps < threshold) return;
            entered = true;
            if (expire) ticks = 50; else source.Cancel();
        });
        Exception? failure = null;
        try { regex.Count(input, source.Token); }
        catch (Exception error) { failure = error; }
        finally { session.Complete(); }
        Assert.True(entered);
        if (expire) Assert.IsType<RegexMatchTimeoutException>(failure);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
    }
}
