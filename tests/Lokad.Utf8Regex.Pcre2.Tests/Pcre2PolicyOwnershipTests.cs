using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2PolicyOwnershipTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConstructorsAndFactoriesOwnFundingAndKeepAdmittedCursorAliases(int creation)
    {
        const string pattern = "(?<x>a+)(?<x>b)";
        var source = Encoding.UTF8.GetBytes(pattern); var account = new Account();
        var settings = new Utf8Pcre2CompileSettings { AllowDuplicateNames = true };
        var policy = new Utf8RegexPolicy
        {
            ProgramMemoryAllowance = account,
            ResourcePolicy = new() { MaxWorkSteps = 100000 }
        };
        using var regex = creation switch
        {
            0 => new Utf8Pcre2Regex(pattern, compileSettings: settings, policy: policy),
            1 => new Utf8Pcre2Regex(source, compileSettings: settings, policy: policy),
            2 => Utf8Pcre2Regex.Compile(pattern, compileSettings: settings, policy: policy),
            _ => Utf8Pcre2Regex.Compile(source, compileSettings: settings, policy: policy)
        };
        source.AsSpan().Clear(); Assert.True(account.Used >= 4UL << 20);
        Assert.Equal(settings, regex.CompileSettings); Assert.Equal(2, regex.CaptureGroupCount);
        var numbers = new int[2]; Assert.Equal(2, regex.CopyNumbersForUtf8Name("x"u8, numbers, out var more));
        Assert.False(more); Assert.Equal(new[] { 1, 2 }, numbers);
        using var cursor = regex.CreateMatchCursor("aaab aab"u8.ToArray()); var alias = cursor;
        regex.Dispose(); Assert.True(account.Used > 0);
        Assert.Throws<ObjectDisposedException>(() => regex.Count("ab"u8));
        Assert.True(alias.MoveNext(out var first)); Assert.Equal("aaa"u8.ToArray(), first.GetGroupMemory(1).ToArray());
        Assert.True(cursor.MoveNext(out var second)); Assert.Equal(5, second.Value.StartOffsetInBytes);
        Assert.False(alias.MoveNext(out _)); Assert.Equal(0UL, account.Used);
        Assert.Equal("aaab"u8.ToArray(), first.GetValueMemory().ToArray());
        Assert.Equal(pattern, regex.Pattern); Assert.Equal(policy.ResourcePolicy, regex.ResourcePolicy);
    }

    [Fact]
    public void ClosedFundedExpressionRejectsEveryProgramEntryWithoutTouchingCallerOutput()
    {
        var account = new Account(); using var regex = new Utf8Pcre2Regex("(?<x>a)",
            policy: new() { ProgramMemoryAllowance = account });
        regex.Dispose(); Assert.Equal(0UL, account.Used);
        var captures = new[] { new Utf8Pcre2CaptureRange(91, true, 5, 6, true) }; var original = captures[0];
        Assert.Throws<ObjectDisposedException>(() => regex.IsMatch("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.Match("a"u8, captures: captures));
        Assert.Equal(original, captures[0]);

        Assert.Throws<ObjectDisposedException>(() => regex.Count("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.EnumerateMatches("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.CreateMatchCursor("a"u8.ToArray()));
        Assert.Throws<ObjectDisposedException>(() => regex.Probe("a"u8, Pcre2PartialMode.None));
        Assert.Throws<ObjectDisposedException>(() => regex.Replace("a"u8, "x"));
        Assert.Throws<ObjectDisposedException>(() => regex.Replace("a"u8, "x"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.TryReplace("a"u8, "x"u8, new byte[1], out _));
        Assert.Throws<ObjectDisposedException>(() => regex.CaptureGroupCount);
        Assert.Throws<ObjectDisposedException>(() => regex.NameEntryCount);
        Assert.Throws<ObjectDisposedException>(() => regex.TryCopyNameEntryUtf8(0, new byte[1], out _, out _));
        Assert.Throws<ObjectDisposedException>(() => regex.CopyNumbersForUtf8Name("x"u8, new int[1], out _));
        Assert.Throws<ObjectDisposedException>(() => regex.TryCopyPatternUtf8(new byte[20], out _));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.Match("a"u8, captures: captures, cancellationToken: canceled.Token)).CancellationToken);
        Assert.Equal(original, captures[0]);
    }

    [Theory]
    [InlineData("a", "a a", false)]
    [InlineData("(?:ab|cd)+", "ab cd", true)]
    public void FundedSpanCopiesBorrowOpenProducerWithoutHostWorkingLoans(string pattern, string text, bool slice)
    {
        var account = new Account(); using var regex = new Utf8Pcre2Regex(pattern, policy: new()
        { ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkSteps = 100000 } });
        Assert.Equal(slice, regex.DebugCompiledProgram.Operations.Enumerate is Pcre2SliceDirectProgram);
        var input = Encoding.UTF8.GetBytes(text); var attempts = account.Attempts;
        var cursor = regex.EnumerateMatches(input); var alias = cursor;
        Assert.True(cursor.MoveNext()); var retained = cursor.Current;
        Assert.True(cursor.MoveNext()); Assert.Equal(input.AsSpan().IndexOf((byte)' ') + 1, cursor.Current.StartOffsetInBytes);
        Assert.True(alias.MoveNext()); Assert.Equal(0, alias.Current.StartOffsetInBytes);
        Assert.False(cursor.MoveNext()); Assert.Equal(attempts, account.Attempts);
        regex.Dispose(); Assert.Equal(0UL, account.Used);
        try { alias.MoveNext(); Assert.Fail("Closed producer must reject every later advance."); } catch (ObjectDisposedException) { }
        try { cursor.MoveNext(); Assert.Fail("Ended copies still borrow the open producer."); } catch (ObjectDisposedException) { }
        Assert.True(retained.IsUtf8SliceWellFormed); Assert.Equal(input.AsSpan(0, input.AsSpan().IndexOf((byte)' ')).ToArray(), retained.GetValueBytes().ToArray());
    }

    [Theory]
    [InlineData("a")]
    [InlineData("(?:ab|cd)+")]
    public void OriginalSpanCancellationPrecedesClosedProducer(string pattern)
    {
        using var source = new CancellationTokenSource(); var account = new Account();
        using var regex = new Utf8Pcre2Regex(pattern, policy: new() { ProgramMemoryAllowance = account });
        var cursor = regex.EnumerateMatches("ab cd"u8, cancellationToken: source.Token);
        source.Cancel(); regex.Dispose();
        try { cursor.MoveNext(); Assert.Fail("Expected original cancellation."); }
        catch (OperationCanceledException error) { Assert.Equal(source.Token, error.CancellationToken); }
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void NumericSpanWorkingLimitDoesNotAcquireHostWorkingFunding()
    {
        var account = new Account(); using var regex = new Utf8Pcre2Regex("(?:ab|cd)+", policy: new()
        { ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkingBytes = 1 } });
        var attempts = account.Attempts;
        try { var cursor = regex.EnumerateMatches("abcd"u8); cursor.MoveNext(); Assert.Fail("Expected numeric working refusal."); }
        catch (Pcre2MatchException error) { Assert.Equal(Pcre2ErrorKind.HeapLimit, error.ErrorKind); }
        Assert.Equal(attempts, account.Attempts);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void AdmittedNumericOperationsFinishAfterCloseWithoutPublicGateReentry(int operation)
    {
        var account = new Account(); using var regex = new Utf8Pcre2Regex("(?<x>a+)", policy: new()
        { ProgramMemoryAllowance = account, MatchTimeout = TimeSpan.FromSeconds(10) });
        var held = account.Used; var closed = false;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (!closed) { regex.Dispose(); closed = true; Assert.Equal(held, account.Used); } return 0; });
        switch (operation)
        {
            case 1: Assert.True(regex.Probe("aaa"u8, Pcre2PartialMode.None).GetMatch().Success); break;
            case 2: Assert.Equal("<aaa>"u8.ToArray(), regex.Replace("aaa"u8, "<$1>"u8)); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
        Assert.True(closed); Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void AdmittedSpanAdvanceCanCloseProducerAndRetainCurrent()
    {
        var account = new Account(); Utf8Pcre2Regex? expression = null; var armed = false; var closed = false; ulong held = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() =>
        { if (armed && !closed) { expression!.Dispose(); closed = true; Assert.Equal(held, account.Used); } return 0; });
        using var regex = expression = new Utf8Pcre2Regex("(?:ab|cd)+", policy: new()
        { ProgramMemoryAllowance = account, MatchTimeout = TimeSpan.FromSeconds(10) });
        var cursor = regex.EnumerateMatches("abcd"u8); held = account.Used; armed = true;
        Assert.True(cursor.MoveNext()); Assert.True(closed); Assert.Equal("abcd"u8.ToArray(), cursor.Current.GetValueBytes().ToArray());
        Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConstructorAdmitsConstructionPolicyBeforePublishing(int kind)
    {
        var limit = kind switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 }
        };
        Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Pcre2Regex("((a|aa)*)b", policy: new() { ConstructionPolicy = limit }));
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructionDeadlineIsIndependentForConstructorAndCompile(bool compile)
    {
        long ticks = 0; using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var policy = new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromSeconds(30), ConstructionTimeout = TimeSpan.FromMilliseconds(1) };
        var error = Assert.Throws<RegexMatchTimeoutException>(() =>
        { if (compile) _ = Utf8Pcre2Regex.Compile("(a|aa)+b", policy: policy); else _ = new Utf8Pcre2Regex("(a|aa)+b", policy: policy); });
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout); Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void ExplicitConstructionCannotReplaceInheritedAuthorityAndCompileTokenEndsAtPublication()
    {
        using (var owner = Utf8ConstructionScope.Enter())
        {
            using var expression = new Utf8Pcre2Regex("a"); var work = owner.Budget.WorkSteps; Assert.True(work > 0);
            Assert.Throws<InvalidOperationException>(() => Utf8Pcre2Regex.Compile("a"));
            Assert.Throws<InvalidOperationException>(() => new Utf8Pcre2Regex("a", policy: new() { ConstructionPolicy = new() { MaxWorkSteps = 100000 } }));
            Assert.Equal(work, owner.Budget.WorkSteps);
        }
        using var source = new CancellationTokenSource(); using var regex = Utf8Pcre2Regex.Compile("a", cancellationToken: source.Token);
        source.Cancel(); Assert.True(regex.IsMatch("a"u8));
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => Utf8Pcre2Regex.Compile((string)null!,
            policy: new() { MatchTimeout = TimeSpan.Zero }, cancellationToken: source.Token)).CancellationToken);
    }

    [Fact]
    public void UnfundedDisposeLeavesExistingSpanCopiesUsable()
    {
        var regex = new Utf8Pcre2Regex("a"); var cursor = regex.EnumerateMatches("a a"u8); var alias = cursor;
        regex.Dispose(); Assert.True(cursor.MoveNext()); Assert.True(cursor.MoveNext());
        Assert.True(alias.MoveNext()); Assert.Equal(0, alias.Current.StartOffsetInBytes); Assert.Equal(2, regex.Count("a a"u8));
    }

    [Fact]
    public void NameEntryCopyRetainsCallerBytesAndOriginalTokenAfterFundedClose()
    {
        var account = new Account();
        using var regex = new Utf8Pcre2Regex("(?<名>a)", policy: new()
        {
            ProgramMemoryAllowance = account,
            ResourcePolicy = new() { MaxWorkSteps = 1, MaxDepth = 1, MaxWorkingBytes = 1, MaxResultBytes = 1 }
        });
        var destination = new byte[3]; var attempts = account.Attempts;
        Assert.Equal(System.Buffers.OperationStatus.Done,
            regex.TryCopyNameEntryUtf8(0, destination, out var required, out var number));
        Assert.Equal(3, required); Assert.Equal(1, number); Assert.Equal(attempts, account.Attempts);
        regex.Dispose(); Assert.Equal(0UL, account.Used);
        Assert.Equal("名"u8.ToArray(), destination);
        Assert.Throws<ObjectDisposedException>(() => regex.TryCopyNameEntryUtf8(0, destination, out _, out _));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.Equal(canceled.Token, Assert.Throws<OperationCanceledException>(() =>
            regex.TryCopyNameEntryUtf8(-1, destination, out _, out _, canceled.Token)).CancellationToken);
        Assert.Equal("名"u8.ToArray(), destination);
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used; internal int Attempts;
        public bool TryReserve(ulong bytes) { Attempts++; if (bytes > (64UL << 20) - Used) return false; Used += bytes; return true; }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}
