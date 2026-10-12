using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2LiteralTemplateTextAuthorityTests
{
    [Theory]
    [InlineData(0, 48UL)] [InlineData(1, 272UL)] [InlineData(17, 2160UL)]
    public void StringPublicationAdmitsTextAlongsideByteOutputAndRanges(int count, ulong bytePeak)
    {
        var input = count == 0 ? "z"u8.ToArray() : Enumerable.Repeat((byte)'a', count).ToArray();
        var replacement = new string('b', count == 17 ? 100 : 1);
        var expected = count == 0 ? "z" : new string('b', count * replacement.Length);
        var textPeak = bytePeak + Utf8ConstructionBudget.StringBytes(expected.Length);
        var admitted = Create(new() { HeapLimitInBytes = textPeak });
        Assert.Equal(expected, admitted.ReplaceToString(input, replacement));
        var denied = Create(new() { HeapLimitInBytes = textPeak - 1 });
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(
            () => denied.ReplaceToString(input, replacement)).ErrorKind);
        // The byte producer still needs only its existing byte/range extent.
        Assert.Equal(Encoding.UTF8.GetBytes(expected), Create(new() { HeapLimitInBytes = bytePeak }).Replace(input, replacement));
        Assert.Equal(expected, admitted.ReplaceToString(input, replacement));
    }

    [Theory]
    [InlineData("a", "aé😀a", "é😀", Pcre2SubstitutionOptions.None, "é😀é😀é😀")]
    [InlineData("a", "é😀", "b", Pcre2SubstitutionOptions.None, "é😀")]
    [InlineData("a", "é😀", "b", Pcre2SubstitutionOptions.SubstituteReplacementOnly, "")]
    [InlineData("", "é😀", "_", Pcre2SubstitutionOptions.None, "_é_😀_")]
    [InlineData("a", "aé😀a", "<$0>", Pcre2SubstitutionOptions.None, "<a>é😀<a>")]
    [InlineData("a", "aé😀a", "$0", Pcre2SubstitutionOptions.SubstituteLiteral, "$0é😀$0")]
    public void FiniteTextPreservesScalarAndSubstitutionResults(string pattern, string subject,
        string replacement, Pcre2SubstitutionOptions options, string expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 10000, HeapLimitInBytes = 1UL << 20 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(expected, regex.ReplaceToString(input, replacement, options));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, replacement, 0, options));
    }

    [Fact]
    public void DecoderCannotResetSpentDiscoveryWork()
    {
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 100, HeapLimitInBytes = 10000 };
        var budget = new Pcre2ResourceBudget(limits, Timeout.InfiniteTimeSpan, false);
        budget.ReserveWorking(13);
        while (budget.WorkingInvocation.Budget.Steps < 100) budget.ChargeWorkspaceStep();
        var control = new Pcre2CaptureProjectionBudget(ref budget);
        Pcre2MatchException? failure = null;
        try { _ = control.DecodeReplacementText("😀"u8); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(101UL, budget.WorkingInvocation.Budget.Steps);
        Assert.Equal(13UL, budget.ReservedWorkingBytes);
        budget.ReleaseWorking(13);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DecoderKeepsOriginalDeadlineAndTokenAcrossBlocks(bool cancel)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var source = new CancellationTokenSource();
        var limits = new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1UL << 20 };
        var invocation = new Utf8SliceInvocation(new(0, 0, limits.HeapLimitInBytes),
            TimeSpan.FromMilliseconds(20), source.Token);
        var budget = new Pcre2ResourceBudget(limits, invocation, false);
        budget.ReserveWorking(13);
        ticks = 15;
        while ((budget.WorkingInvocation.Budget.Steps & 63) != 60)
            Assert.Equal(Utf8ExecutionLimitKind.None, budget.WorkingInvocation.TryChargeInputStep());
        var entered = false;
        budget.WorkingInvocation.CheckpointHook = _ => { entered = true; if (cancel) source.Cancel(); else ticks = 25; };
        var control = new Pcre2CaptureProjectionBudget(ref budget);
        Exception? failure = null;
        try { _ = control.DecodeReplacementText(new byte[8192]); }
        catch (Exception error) { failure = error; }
        Assert.True(entered);
        if (cancel) Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        else Assert.Equal(Pcre2ErrorKind.Timeout, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        budget.ReleaseWorking(13);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    private static Utf8Pcre2Regex Create(Utf8Pcre2ExecutionLimits limits)
        => new("a", Pcre2CompileOptions.None, default, limits, Timeout.InfiniteTimeSpan);
}
