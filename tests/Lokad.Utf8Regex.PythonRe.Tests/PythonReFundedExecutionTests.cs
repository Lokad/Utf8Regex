using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReFundedExecutionTests
{
    private static Utf8PythonRegex Compile(string pattern, Allowance account, Utf8ResourcePolicy policy = default,
        TimeSpan? timeout = null) => Utf8PythonRegex.Compile(pattern, default, timeout ?? Timeout.InfiniteTimeSpan,
            policy, default, Timeout.InfiniteTimeSpan, default, account);

    [Theory]
    [InlineData("(?P<x>[a-z]+)", "aa bb")]
    [InlineData("(?P<x>é+|a+)", "éé 😀 aa")]
    [InlineData("cat", "cat 😀 cat")]
    [InlineData("cat|horse", "cat 😀 horse")]
    [InlineData("a.b", "a😀b aéb")]
    public void PublicOperationFamiliesRefundTheirTemporaryStorage(string pattern, string text)
    {
        var account = new Allowance(16 << 20); using var regex = Compile(pattern, account);
        var input = Encoding.UTF8.GetBytes(text);
        regex.SubnToString(input, @"x\g<0>y"); // Retain the one template before observing temporary balances.
        var held = account.Used;
        foreach (var (name, operation) in Operations(regex, input))
        {
            operation(); Assert.True(account.Used == held, name + " left temporary funding outstanding.");
        }
        var retained = regex.SearchDetailedData(input); Assert.True(retained.Success);
        regex.Dispose(); Assert.Equal(0UL, account.Used); Assert.NotEmpty(retained.Groups[0].ValueText);
    }

    [Fact]
    public void ScalarFamiliesCannotBypassARefusedWorkingAccount()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("(?P<x>a+)", account);
        var input = "aaa"u8.ToArray(); regex.SubnToString(input, @"x\g<0>y");
        var held = account.Used; account.Maximum = held;
        foreach (var (name, operation) in Operations(regex, input))
        {
            Assert.IsType<Utf8MemoryAdmissionException>(Record.Exception(operation));
            Assert.True(account.Used == held, name + " did not restore its refused account.");
        }
    }

    [Theory]
    [InlineData("cat")]
    [InlineData("cat|horse")]
    [InlineData("a.b")]
    public void NativeCollectionRestoresPartialAndSuccessfulLoans(string pattern)
    {
        var account = new Allowance(16 << 20); using var regex = Compile(pattern, account);
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("cat 😀 horse a😀b aéb ", 64)));
        var held = account.Used; var refusals = 0; var successes = 0;
        for (ulong remaining = 128; remaining <= 131072; remaining *= 2)
        {
            account.Maximum = held + remaining;
            Action[] operations = [() => regex.FindAll(input), () => regex.FindAllToStrings(input), () => regex.FindAllToUtf8(input)];
            foreach (var operation in operations)
            {
                try { operation(); successes++; }
                catch (Utf8MemoryAdmissionException) { refusals++; }
                Assert.Equal(held, account.Used);
            }
        }
        Assert.True(refusals > 0); Assert.True(successes > 0);
    }

    [Fact]
    public void FundedLiteralAndScalarOperationsStillValidateTheWholeSubject()
    {
        var invalid = new byte[] { (byte)'c', (byte)'a', (byte)'t', 0xff };
        foreach (var pattern in new[] { "cat", "(?P<x>a+)" })
        {
            var account = new Allowance(16 << 20); using var regex = Compile(pattern, account);
            var held = account.Used; account.Maximum = held;
            Assert.Throws<ArgumentException>(() => regex.Count(invalid));
            Assert.Throws<ArgumentException>(() => { _ = regex.Search(invalid); });
            Assert.Equal(held, account.Used);
        }
    }

    [Fact]
    public void ExactLiteralBooleanAndCountKeepTheirStorageFreeRoute()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("cat", account);
        var held = account.Used; account.Maximum = held; var reservations = account.Reservations;
        Assert.True(regex.IsMatch("😀 cat"u8)); Assert.Equal(2, regex.Count("cat 😀 cat"u8));
        Assert.Equal(reservations, account.Reservations); Assert.Equal(held, account.Used);
    }

    [Fact]
    public void CallbackOutputIsAdmittedBeforeEngineCopying()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("a+", account); var held = account.Used;
        var called = false;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.SubnToString("aaa"u8, account, (budget, _) =>
        {
            called = true; Assert.True(budget.Used > held); budget.Maximum = budget.Used;
            return new string('x', 65536); // Caller-owned callback return; the engine must admit its copy.
        }));
        Assert.True(called); Assert.Equal(held, account.Used);
    }

    [Theory] [InlineData("cat|horse")] [InlineData("a.b")]
    public void BoundNativeOrScalarTimeoutDoesNotDecodeDiagnosticInput(string pattern)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var account = new Allowance(16 << 20); using var regex = Compile(pattern, account, timeout: TimeSpan.FromMilliseconds(100));
        var held = account.Used;
        var subject = Encoding.UTF8.GetBytes("cat a😀b " + new string('a', 262144));
        account.AfterGrant = () => ticks = 100;
        var failure = Assert.Throws<RegexMatchTimeoutException>(() => regex.FindAll(subject));
        account.AfterGrant = null;
        Assert.Empty(failure.Input); Assert.Equal(regex.Pattern, failure.Pattern); Assert.Equal(held, account.Used);
    }

    [Fact]
    public void DefaultCursorUsesProgramAccountAndStreamsWithoutTrafficQuota()
    {
        var account = new Allowance(16 << 20); using var regex = Compile(".", account);
        var held = account.Used; account.Maximum = held + 16384;
        var input = new byte[500000]; input.AsSpan().Fill((byte)'x');
        using var cursor = regex.CreateMatchCursor(input);
        var count = 0;
        while (cursor.MoveNext(out var match)) { Assert.Equal(count, match.Value.StartOffsetInBytes); count++; }
        Assert.Equal(500000, count); Assert.Equal(held, account.Used);
        regex.Dispose(); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void ExplicitCursorWorkingAccountRemainsAnIndependentChoice()
    {
        var program = new Allowance(16 << 20); var working = new Allowance(16384);
        using var regex = Compile("(?P<x>.)", program); var held = program.Used; program.Maximum = held;
        Assert.Throws<Utf8MemoryAdmissionException>(() => regex.CreateMatchCursor("é😀"u8.ToArray()));
        using var cursor = regex.CreateMatchCursor("é😀"u8.ToArray(), 0, default, working);
        Assert.True(cursor.MoveNext(out var first)); Assert.True(working.Used > 0); Assert.Equal(held, program.Used);
        cursor.Dispose(); Assert.Equal(0UL, working.Used);
        Assert.Equal("é", Encoding.UTF8.GetString(first.GetValueMemory().Span));
    }

    [Fact]
    public void NumericWorkingDenialPrecedesSharedAdmission()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("(?P<x>a+)", account,
            new Utf8ResourcePolicy { MaxWorkingBytes = 1 });
        var held = account.Used; var reservations = account.Reservations;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Count("aaa"u8));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, failure.ResourceKind);
        Assert.Equal(reservations, account.Reservations); Assert.Equal(held, account.Used);
    }

    [Fact]
    public async Task ConcurrentExecutionsRefundTheirOwnLoansAndCancellation()
    {
        var account = new Allowance(16 << 20); using var regex = Compile("(?P<x>a+)", account); var held = account.Used;
        using var canceled = new CancellationTokenSource(); account.AfterGrant = canceled.Cancel;
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() => regex.Count("aaa"u8, 0, canceled.Token)).CancellationToken);
        account.AfterGrant = null; Assert.Equal(held, account.Used);
        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 32; i++) Assert.Equal(2, regex.Count("aaa aaa"u8));
        })));
        Assert.Equal(held, account.Used);
    }

    private static (string Name, Action Operation)[] Operations(Utf8PythonRegex regex, byte[] input) => [
        ("IsMatch", () => regex.IsMatch(input)), ("Search", () => { _ = regex.Search(input); }),
        ("Match", () => { _ = regex.Match(input); }), ("FullMatch", () => { _ = regex.FullMatch(input); }),
        ("Count", () => regex.Count(input)), ("SearchDetailed", () => { _ = regex.SearchDetailed(input); }),
        ("MatchDetailed", () => { _ = regex.MatchDetailed(input); }), ("FullMatchDetailed", () => { _ = regex.FullMatchDetailed(input); }),
        ("SearchDetailedData", () => regex.SearchDetailedData(input)), ("MatchDetailedData", () => regex.MatchDetailedData(input)),
        ("FullMatchDetailedData", () => regex.FullMatchDetailedData(input)), ("SearchToString", () => regex.SearchToString(input)),
        ("MatchToString", () => regex.MatchToString(input)), ("FullMatchToString", () => regex.FullMatchToString(input)),
        ("FindAll", () => regex.FindAll(input)), ("FindAllToStrings", () => regex.FindAllToStrings(input)),
        ("FindAllToUtf8", () => regex.FindAllToUtf8(input)), ("FindIterDetailed", () => regex.FindIterDetailed(input)),
        ("SplitRanges", () => regex.SplitRanges(input)), ("SplitToStrings", () => regex.SplitToStrings(input)),
        ("SplitDetailed", () => regex.SplitDetailed(input)), ("Subn", () => regex.Subn(input, @"x\g<0>y")),
        ("SubnToString", () => regex.SubnToString(input, @"x\g<0>y")),
        ("String evaluator", () => regex.SubnToString(input, 0, static (_, _) => "x")),
        ("UTF8 result string evaluator", () => regex.Subn(input, 0, static (_, _) => "x")),
        ("UTF8 evaluator", () => regex.Subn(input, 0, static (_, _) => "x"u8.ToArray())),
        ("Cursor", () => { using var cursor = regex.CreateMatchCursor(input); while (cursor.MoveNext(out _)) { } }),
    ];
}
