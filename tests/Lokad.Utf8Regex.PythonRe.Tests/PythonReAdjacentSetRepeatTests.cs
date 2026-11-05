using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReAdjacentSetRepeatTests
{
    private static Utf8ResourcePolicy Generous => new() { MaxWorkSteps = 1_000_000, MaxDepth = 1024,
        MaxWorkingBytes = 1 << 24, MaxResultBytes = 1 << 24 };

    [Theory]
    [InlineData(@"item \s+ [0-9]+", "item   12345", PythonReCompileOptions.Verbose)]
    [InlineData(@"item(\s+)[0-9]+", "item\u2003\u3000 12345", PythonReCompileOptions.None)]
    [InlineData(@"item[ 0-9]+[0-9]+", "item   12345", PythonReCompileOptions.None)]
    [InlineData(@"item[a-z]+[0-9]*[a-z]+", "itemabc", PythonReCompileOptions.None)]
    [InlineData(@"item[a-z]+?[0-9]+", "itemabc12", PythonReCompileOptions.None)]
    [InlineData(@"(?i:item[a-z]+)[K0-9]+", "itemabkK12", PythonReCompileOptions.None)]
    [InlineData(@"item[a-z]+[é0-9]+", "itemabcé12", PythonReCompileOptions.None)]
    [InlineData(@"item(😀{1,2})[0-9]+", "item😀😀123", PythonReCompileOptions.None)]
    public void PublicFullMatchesKeepScalarRangesAndDeclinedShapes(string pattern, string text, PythonReCompileOptions options)
    {
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var policy in new[] { Utf8ResourcePolicy.Unlimited, Generous })
        {
            var owner = new Utf8PythonRegex(pattern, options, Timeout.InfiniteTimeSpan, policy);
            var value = owner.FullMatch(input);
            Assert.True(value.Success);
            Assert.Equal((0, input.Length, 0, text.Length),
                (value.StartOffsetInBytes, value.EndOffsetInBytes, value.StartOffsetInUtf16, value.EndOffsetInUtf16));
            Assert.Equal(text, owner.FullMatchToString(input));
            var retained = owner.FullMatchDetailedData(input);
            Assert.Equal(text, retained.Value.ValueText);
            Assert.Equal(1, owner.Count(input));
            Assert.Equal(text, retained.Value.ValueText);
            Assert.Equal(text, value.GetValueString());
            Assert.Throws<ArgumentException>(() => owner.FullMatch(input.Concat(new byte[] { 0xFF }).ToArray()));
        }
    }

    [Fact]
    public void FundedCapturesOutliveProgramClosureAndReturnTemporaryLoans()
    {
        var allowance = new Allowance(16 << 20);
        var owner = Utf8PythonRegex.Compile(@"item(?P<gap>\s+)[0-9]+", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, default, default, Timeout.InfiniteTimeSpan, default, allowance);
        var held = allowance.Used;
        var input = "item\u2003 12345"u8.ToArray();
        var retained = owner.FullMatchDetailedData(input);
        Assert.True(retained.Success);
        Assert.Equal("\u2003 ", retained.Groups[1].ValueText);
        Assert.Equal("gap", retained.LastGroup);
        Assert.Equal(held, allowance.Used);
        Assert.Equal(1, owner.Count(input));
        Assert.Equal(held, allowance.Used);
        owner.Dispose();
        Assert.Equal(0UL, allowance.Used);
        Assert.Equal("\u2003 ", retained.Groups[1].ValueText);
    }

    [Fact]
    public void WorkWorkingAndResultAdmissionRemainUseful()
    {
        const string pattern = @"item\s+[0-9]+";
        foreach (var (policy, kind) in new[]
        {
            (new Utf8ResourcePolicy { MaxWorkingBytes = 1 }, Utf8ResourceLimitKind.ExecutionWorkingBytes),
            (new Utf8ResourcePolicy { MaxWorkSteps = 1 }, Utf8ResourceLimitKind.ExecutionWorkSteps),
        })
        {
            var denied = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan, policy);
            Assert.Equal(kind, Assert.Throws<Utf8ResourceLimitExceededException>(() => denied.Search("item   12345"u8)).ResourceKind);
        }
        var results = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, Timeout.InfiniteTimeSpan,
            new() { MaxResultBytes = 1 });
        Assert.Equal(Utf8ResourceLimitKind.ExecutionResultBytes,
            Assert.Throws<Utf8ResourceLimitExceededException>(() => results.FindAllToStrings("item   12345"u8)).ResourceKind);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void TheDisjointClassRunKeepsTheOriginalTokenAndDeadline(bool expire)
    {
        const string pattern = @"item\s+[0-9]+";
        var input = Encoding.UTF8.GetBytes("item" + new string(' ', 8192) + "12345");
        var validation = new Utf8SliceInvocation(new(1_000_000, 1024, 1 << 24, 1 << 24), Timeout.InfiniteTimeSpan, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var cutoff = validation.Budget.Steps + 128;
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var owner = new Utf8PythonRegex(pattern, PythonReCompileOptions.None, TimeSpan.FromMilliseconds(20), Generous);
        var interrupted = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps =>
        {
            if (steps <= cutoff) return;
            interrupted = true;
            if (expire) ticks = 50; else token.Cancel();
        });
        try
        {
            if (expire) Assert.Equal(pattern, Assert.Throws<RegexMatchTimeoutException>(() => owner.Search(input)).Pattern);
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => owner.Search(input, 0, token.Token)).CancellationToken);
        }
        finally { session.Complete(); }
        Assert.True(interrupted);
        ticks = 0;
        Assert.True(owner.Search(input).Success);
    }
}
