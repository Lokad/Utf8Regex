using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReNativeSubstitutionTests
{
    [Theory]
    [InlineData("(?P<x>é|😀)(a)?", "!é😀a!", @"[\g<x>\2\g<0>]")]
    [InlineData("(?P<𝒜>é)", "é😀é", "😀\\g<𝒜>\0\\377")]
    [InlineData("x*", "abxd", @"[\g<0>]")]
    [InlineData("(?<=é)a", "éaéa", "😀")]
    [InlineData("^a", "éaaa", "!")]
    [InlineData("a", "aaaa", "é\\&\\101")]
    public void PublicByteTemplatesRetainTextSubstitutionResultsAndProgression(string pattern, string subject, string template)
    {
        var regex = new Utf8PythonRegex(Encoding.UTF8.GetBytes(pattern));
        var input = Encoding.UTF8.GetBytes(subject); var replacement = Encoding.UTF8.GetBytes(template);
        var starts = Enumerable.Range(0, input.Length + 1).Where(offset => offset == input.Length || (input[offset] & 0xc0) != 0x80);
        foreach (var count in new[] { -1, 0, 1, 2 })
        foreach (var start in starts)
        {
            var expected = regex.Subn(input, template, count, start);
            var actual = regex.Subn(input, replacement, count, start);
            Assert.Equal(expected.ReplacementCount, actual.ReplacementCount);
            Assert.Equal(expected.ResultBytes, actual.ResultBytes);
            Assert.Equal(expected.ResultBytes, regex.Replace(input, replacement, count, start));
        }
    }

    [Fact]
    public void NativeKeysAreImmutableAndWarmCallsBorrowTheirQueries()
    {
        var regex = new Utf8PythonRegex("(?P<x>a)"); var key = "é\\g<x>😀"u8.ToArray();
        Assert.Equal("éa😀", Encoding.UTF8.GetString(regex.Replace("a"u8, key)));
        var held = regex.DebugReplacementPlanCachePayloadBytes;
        key[0] = 0xff;
        Assert.Throws<ArgumentException>(() => regex.Replace(""u8, key));
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
        Assert.Equal(held, regex.DebugReplacementPlanCachePayloadBytes);
        using var construction = Utf8ConstructionScope.Enter();
        Assert.Equal("éa😀", Encoding.UTF8.GetString(regex.Replace("a"u8, "é\\g<x>😀"u8)));
        Assert.Equal(0UL, construction.Budget.ReservedBytes);
        Assert.Equal(0UL, construction.Budget.WorkSteps);
        // Both source forms share the bounded owner, with explicit independent
        // keys. Legacy invalid-UTF16 text never aliases strict native content.
        Assert.Equal("éa😀", Encoding.UTF8.GetString(regex.Replace("a"u8, "é\\g<x>😀")));
        Assert.Equal(2, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativeGrammarAndUtf8ErrorsAreValidatedOnMissEvenWithNegativeCount(bool warm)
    {
        var regex = new Utf8PythonRegex("a");
        if (warm) regex.Replace("a"u8, "é\\g<0>"u8);
        var retained = regex.DebugReplacementPlanCachePayloadBytes;
        foreach (var count in new[] { -1, 0, 1 })
        {
            var grammar = Assert.Throws<PythonRePatternException>(() => regex.Subn("none"u8, "é😀\\q"u8, count));
            Assert.Equal(3, grammar.Position); Assert.Equal(PythonReErrorSource.Replacement, grammar.ErrorSource);
            byte[] malformed = [.. "é😀\\q"u8, 0x80];
            Assert.Throws<ArgumentException>(() => regex.Replace("none"u8, malformed, count));
            Assert.Equal(retained, regex.DebugReplacementPlanCachePayloadBytes);
        }
        Assert.Throws<ArgumentException>(() => regex.Subn([97, 0x80], "!"u8, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Subn("éa"u8, "!"u8, startOffsetInBytes: 1));
        using var token = new CancellationTokenSource(); token.Cancel();
        var error = Assert.Throws<OperationCanceledException>(() =>
            regex.Subn([0x80], [0x80], startOffsetInBytes: -1, cancellationToken: token.Token));
        Assert.Equal(token.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NativePreparationRefusalAndInterruptionRefundEveryTemporaryLoan(bool timeout)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var allowance = new Allowance(4 << 20);
        using var regex = Utf8PythonRegex.Compile("(?P<x>a)"u8, default,
            timeout ? TimeSpan.FromMilliseconds(100) : Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, allowance);
        regex.Replace("a"u8, "warm\\g<x>"u8);
        var held = allowance.Used; var entries = regex.DebugReplacementPlanCacheEntryCount;
        using var token = new CancellationTokenSource();
        allowance.AfterGrant = () => { if (timeout) ticks = 100; else token.Cancel(); };
        try
        {
            if (timeout)
            {
                var failure = Assert.Throws<RegexMatchTimeoutException>(() => regex.Subn("a"u8, "new\\g<x>"u8));
                Assert.Empty(failure.Input);
            }
            else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() =>
                regex.Subn("a"u8, "new\\g<x>"u8, cancellationToken: token.Token)).CancellationToken);
        }
        finally { allowance.AfterGrant = null; ticks = 0; }
        Assert.Equal(held, allowance.Used); Assert.Equal(entries, regex.DebugReplacementPlanCacheEntryCount);
        allowance.Maximum = held + 32;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.Replace("a"u8, "new\\g<x>"u8));
        Assert.Equal(held, allowance.Used);
        allowance.Maximum = 4 << 20;
        Assert.Equal("warma", Encoding.UTF8.GetString(regex.Replace("a"u8, "warm\\g<x>"u8)));
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public void NativeKeysAndRetainedOutputKeepSeparateDeterministicLifetimes()
    {
        var allowance = new Allowance(4 << 20);
        using var regex = Utf8PythonRegex.Compile("(?P<x>a)"u8, default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, allowance);
        using var result = regex.SubnRetained("éaa😀"u8, "[\\g<x>]"u8);
        var bytes = result.Result.ResultBytes;
        Assert.Equal("é[a][a]😀", Encoding.UTF8.GetString(bytes)); Assert.Equal(2, result.Result.ReplacementCount);
        regex.Dispose(); Assert.Equal(result.ChargedBytes, allowance.Used);
        Assert.Throws<ObjectDisposedException>(() => regex.Subn("a"u8, "[\\g<x>]"u8));
        result.Dispose(); Assert.Equal(0UL, allowance.Used);
        Assert.Equal("é[a][a]😀", Encoding.UTF8.GetString(bytes));
        Assert.Throws<InvalidOperationException>(() => new Utf8PythonRegex("a").SubnRetained("a"u8, "!"u8));
    }

    [Theory]
    [InlineData(1, null, null)]
    [InlineData(null, 32, null)]
    [InlineData(null, null, 1)]
    public void NativeSubstitutionEnforcesUsefulFinitePolicies(int? work, int? working, int? result)
    {
        var regex = new Utf8PythonRegex("(?P<x>a)", default, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy { MaxWorkSteps = (ulong?)work, MaxWorkingBytes = (ulong?)working, MaxResultBytes = (ulong?)result });
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Subn("aaa"u8, "[\\g<x>]"u8));
        Assert.Equal(work is not null ? Utf8ResourceLimitKind.ExecutionWorkSteps :
            working is not null ? Utf8ResourceLimitKind.ExecutionWorkingBytes : Utf8ResourceLimitKind.ExecutionResultBytes,
            failure.ResourceKind);
    }

    [Fact]
    public void LongNativeTemplatesSpendWorkBeforeColdKeysAndOnWarmQueries()
    {
        var regex = new Utf8PythonRegex("a", default, Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy { MaxWorkSteps = 64 });
        var template = new byte[1 << 20]; template.AsSpan().Fill((byte)'z');
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Replace([], template));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, failure.ResourceKind);
        Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
        var allowance = new Allowance(4 << 20);
        using var funded = Utf8PythonRegex.Compile("a"u8, default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, allowance);
        ulong retained = 0;
        for (var i = 0; i < 96; i++)
        {
            funded.Replace("a"u8, Encoding.UTF8.GetBytes(i.ToString("D4") + @"\g<0>"));
            if (i == 31) retained = allowance.Used;
            if (i > 31) Assert.Equal(retained, allowance.Used);
        }
        funded.Dispose(); Assert.Equal(0UL, allowance.Used);
    }

    [Fact]
    public async Task ConcurrentNativeReuseKeepsOnlyItsBoundedRetainedFunding()
    {
        var allowance = new Allowance(4 << 20);
        using var regex = Utf8PythonRegex.Compile("(?P<x>a)"u8, default, Timeout.InfiniteTimeSpan,
            default, default, Timeout.InfiniteTimeSpan, default, allowance);
        byte[][] templates = ["0\\g<x>é"u8.ToArray(), "1\\g<x>é"u8.ToArray(), "2\\g<x>é"u8.ToArray(), "3\\g<x>é"u8.ToArray()];
        foreach (var template in templates) regex.Replace("a"u8, template);
        var held = allowance.Used;
        await Task.WhenAll(Enumerable.Range(0, 8).Select(worker => Task.Run(() =>
        {
            using var token = new CancellationTokenSource();
            for (var index = 0; index < 32; index++)
                Assert.Equal((index % 4) + "aé", Encoding.UTF8.GetString(
                    regex.Replace("a"u8, templates[index % 4], cancellationToken: token.Token)));
        })));
        Assert.Equal(held, allowance.Used);
        regex.Dispose(); Assert.Equal(0UL, allowance.Used);
    }
}
