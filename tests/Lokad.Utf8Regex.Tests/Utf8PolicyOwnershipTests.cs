using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8PolicyOwnershipTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void OneExpressionOwnsFundedConstructionAndPersistentCursorAliases(int creation)
    {
        var account = new Account();
        var source = "(?<x>a+)"u8.ToArray();
        var policy = new Utf8RegexPolicy { ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkSteps = 100000 } };
        using var regex = creation switch
        {
            0 => new Utf8Regex("(?<x>a+)", policy: policy),
            1 => Utf8Regex.Compile("(?<x>a+)", policy: policy),
            _ => Utf8Regex.Compile(source, policy: policy)
        };
        source.AsSpan().Clear();
        Assert.True(account.Used > 4UL << 20); // Includes the bounded replacement-cache capacity.
        using var cursor = regex.CreateMatchCursor("aa aaa"u8.ToArray());
        var alias = cursor;
        regex.Dispose();
        Assert.True(account.Used > 0);
        Assert.Throws<ObjectDisposedException>(() => regex.IsMatch("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.Match("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.Count("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.CreateMatchCursor("a"u8.ToArray()));
        Assert.Throws<ObjectDisposedException>(() => regex.EnumerateMatches("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.EnumerateSplits("a"u8));
        Assert.Throws<ObjectDisposedException>(() => regex.CaptureGroupCount);
        Assert.True(alias.MoveNext(out var retained));
        Assert.Equal("aa"u8.ToArray(), retained.GetGroupMemory(1).ToArray());
        Assert.True(cursor.MoveNext(out var second));
        Assert.Equal(3, second.Value.StartOffsetInBytes);
        Assert.False(alias.MoveNext(out _));
        Assert.Equal(0UL, account.Used);
        Assert.Equal("aa"u8.ToArray(), retained.GetValueMemory().ToArray());
        Assert.Equal("(?<x>a+)", regex.Pattern);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopiedFundedSpanPositionsAreIndependentAndBorrowAnOpenRegex(bool split)
    {
        var account = new Account();
        using var regex = new Utf8Regex(split ? "," : "a+", policy: new()
        {
            ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkSteps = 100000 }
        });
        var attempts = account.Attempts;
        if (split)
        {
            var cursor = regex.EnumerateSplits("aa,bbb,c"u8); var alias = cursor;
            Assert.True(cursor.MoveNext()); var retained = cursor.Current;
            Assert.True(cursor.MoveNext()); Assert.Equal(3, cursor.Current.IndexInBytes);
            Assert.True(alias.MoveNext()); Assert.Equal(0, alias.Current.IndexInBytes);
            Assert.Equal(attempts, account.Attempts);
            regex.Dispose(); Assert.Equal(0UL, account.Used);
            try { alias.MoveNext(); Assert.Fail("Closed producer must reject another advance."); }
            catch (ObjectDisposedException) { }
            Assert.Equal("aa"u8.ToArray(), retained.GetValueBytes().ToArray());
            Assert.Equal(3, cursor.Current.IndexInBytes);
        }
        else
        {
            var cursor = regex.EnumerateMatches("aa aaa"u8); var alias = cursor;
            Assert.True(cursor.MoveNext()); var retained = cursor.Current;
            Assert.True(cursor.MoveNext()); Assert.Equal(3, cursor.Current.StartOffsetInBytes);
            Assert.True(alias.MoveNext()); Assert.Equal(0, alias.Current.StartOffsetInBytes);
            Assert.Equal(attempts, account.Attempts);
            regex.Dispose(); Assert.Equal(0UL, account.Used);
            try { alias.MoveNext(); Assert.Fail("Closed producer must reject another advance."); }
            catch (ObjectDisposedException) { }
            Assert.Equal(new Utf8CaptureRange(0, 2), retained);
            Assert.Equal(3, cursor.Current.StartOffsetInBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FundedSpansKeepOriginalPreCancellationBeforeTheClosedGate(bool split)
    {
        using var source = new CancellationTokenSource();
        var account = new Account();
        using var regex = new Utf8Regex(",", policy: new() { ProgramMemoryAllowance = account });
        if (split)
        {
            var cursor = regex.EnumerateSplits("a,b"u8, cancellationToken: source.Token);
            source.Cancel(); regex.Dispose();
            try { cursor.MoveNext(); Assert.Fail("Original cancellation must win."); }
            catch (OperationCanceledException error) { Assert.Equal(source.Token, error.CancellationToken); }
        }
        else
        {
            var cursor = regex.EnumerateMatches("a,b"u8, cancellationToken: source.Token);
            source.Cancel(); regex.Dispose();
            try { cursor.MoveNext(); Assert.Fail("Original cancellation must win."); }
            catch (OperationCanceledException error) { Assert.Equal(source.Token, error.CancellationToken); }
        }
        Assert.Equal(0UL, account.Used);
    }

    [Fact]
    public void FundedSpanWorkingLimitsStayNumericWithoutAHostWorkingLoan()
    {
        var account = new Account();
        using var regex = new Utf8Regex(@"(a)\1", policy: new()
        {
            ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkingBytes = 1 }
        });
        var attempts = account.Attempts;
        try { var cursor = regex.EnumerateMatches("aaa"u8); cursor.MoveNext(); Assert.Fail("Expected numeric working refusal."); }
        catch (Utf8ResourceLimitExceededException error) { Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, error.ResourceKind); }
        Assert.Equal(attempts, account.Attempts);
    }

    [Fact]
    public void AdmittedNumericOwnedOutputFinishesAfterCloseWithoutReenteringItsGate()
    {
        var account = new Account();
        using var regex = new Utf8Regex("(a+)", policy: new()
        {
            ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkSteps = 100000 }
        });
        var held = account.Used; var closed = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (closed) return;
            regex.Dispose(); closed = true;
            Assert.Equal(held, account.Used);
        });
        try
        {
            var input = Encoding.UTF8.GetBytes(new string('a', 256));
            var result = regex.Replace(input, "<$1>"u8);
            Assert.True(closed); Assert.Equal(input.Length + 2, result.Length);
            Assert.Equal((byte)'<', result[0]); Assert.Equal((byte)'>', result[^1]);
        }
        finally { diagnostics.Complete(); }
        Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void ConstructorPolicyBoundsConstruction(int kind)
    {
        var limit = kind switch
        {
            0 => new Utf8ConstructionPolicy { MaxWorkSteps = 1 },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 }
        };
        Assert.Throws<Utf8ResourceLimitExceededException>(() => new Utf8Regex("((a|aa)*)b", policy: new() { ConstructionPolicy = limit }));
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void CacheCapacityIsAdmittedBeforePublishingAFundedExpression()
    {
        var account = new Account { Maximum = 1 << 20 };
        Assert.Throws<Utf8MemoryAdmissionException>(() => new Utf8Regex("a", policy: new() { ProgramMemoryAllowance = account }));
        Assert.Equal(0UL, account.Used); Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void CompileTokenEndsAtPublicationAndDoesNotReplaceMatchingAuthority()
    {
        using var source = new CancellationTokenSource();
        using var regex = Utf8Regex.Compile("a", cancellationToken: source.Token);
        source.Cancel();
        Assert.True(regex.IsMatch("a"u8));
        var error = Assert.Throws<OperationCanceledException>(() => Utf8Regex.Compile((string)null!,
            policy: new() { MatchTimeout = TimeSpan.Zero }, cancellationToken: source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ConstructionDeadlineIsSeparateOnBothCreationForms(bool factory)
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var policy = new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromSeconds(30), ConstructionTimeout = TimeSpan.FromMilliseconds(1) };
        var error = Assert.Throws<RegexMatchTimeoutException>(() =>
        {
            if (factory) _ = Utf8Regex.Compile("(a|aa)+b", policy: policy);
            else _ = new Utf8Regex("(a|aa)+b", policy: policy);
        });
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void AdmittedSpanAdvanceFinishesAfterCloseAndRetainsItsCurrentValue()
    {
        var account = new Account();
        using var regex = new Utf8Regex("(a|aa)+b", policy: new()
        {
            ProgramMemoryAllowance = account, ResourcePolicy = new() { MaxWorkSteps = 100000 }
        });
        var held = account.Used; var closed = false;
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (closed) return;
            regex.Dispose(); closed = true; Assert.Equal(held, account.Used);
        });
        try
        {
            var input = Encoding.UTF8.GetBytes(new string('a', 128) + "b");
            var cursor = regex.EnumerateMatches(input);
            Assert.True(cursor.MoveNext()); Assert.True(closed);
            Assert.Equal(new Utf8CaptureRange(0, input.Length), cursor.Current);
            Assert.Equal(0UL, account.Used);
        }
        finally { diagnostics.Complete(); }
    }

    [Fact]
    public void DefaultConstructorInheritsButExplicitConstructionCannotReplaceAnOwner()
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var regex = new Utf8Regex("a");
        var work = owner.Budget.WorkSteps; Assert.True(work > 0);
        Assert.Throws<InvalidOperationException>(() => Utf8Regex.Compile("a"));
        Assert.Throws<InvalidOperationException>(() => new Utf8Regex("a", policy: new() { ConstructionPolicy = new() { MaxWorkSteps = 100000 } }));
        Assert.Equal(work, owner.Budget.WorkSteps);
    }

    [Fact]
    public void UnfundedDisposeKeepsExistingSpanAliasesUsable()
    {
        var regex = new Utf8Regex(",");
        var cursor = regex.EnumerateMatches("a,b,c"u8); var alias = cursor;
        regex.Dispose();
        Assert.True(cursor.MoveNext()); Assert.Equal(1, cursor.Current.StartOffsetInBytes);
        Assert.True(cursor.MoveNext()); Assert.Equal(3, cursor.Current.StartOffsetInBytes);
        Assert.True(alias.MoveNext()); Assert.Equal(1, alias.Current.StartOffsetInBytes);
        Assert.Equal(2, regex.Count("a,b,c"u8));
    }

    [Theory]
    [InlineData("(?:a?b|c)*[d-f][xy]")]
    [InlineData(@"(?:a?b|c)*[d-f][xy]\z")]
    public void PackedBooleanAdmitsItsCacheWithinTheWorkingLimit(string pattern)
    {
        var input = Encoding.UTF8.GetBytes(new string('a', 4096) + "dy");
        var account = new Account { Maximum = 0 };
        using var cached = new Utf8Regex(pattern, RegexOptions.NonBacktracking);
        Assert.Throws<Utf8MemoryAdmissionException>(() => cached.IsMatch(input, workingMemoryAllowance: account));
        Assert.Equal(1, account.Attempts);
        Assert.Equal(0UL, account.Used);

        using var tight = new Utf8Regex(pattern, RegexOptions.NonBacktracking, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 }
        });
        Assert.True(tight.IsMatch(input, workingMemoryAllowance: account));
        Assert.Equal(1, account.Attempts);
        Assert.Equal(0UL, account.Used);

        account.Maximum = 2048;
        Assert.True(cached.IsMatch(input, workingMemoryAllowance: account));
        Assert.Equal(2, account.Attempts);
        Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData("(?<x>😀a|b)+", "😀a")]
    [InlineData("(?i:(?<x>😀|a)){1,2}", "😀A")]
    [InlineData("(?i:(?<x>k)){1,2}", "Kk")]
    public void PackedBooleanSkipsImpossibleStartsAndKeepsActivePaths(string pattern, string tail)
    {
        const int gap = 4097;
        var subject = Encoding.UTF8.GetBytes(new string('z', gap) + tail);
        using var regex = new Utf8Regex(pattern, RegexOptions.NonBacktracking | RegexOptions.CultureInvariant, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 64 }
        });
        using var cancellation = new CancellationTokenSource();
        var account = new Account();
        Assert.True(regex.IsMatch(subject, cancellation.Token, account));
        Assert.False(regex.IsMatch(subject.AsSpan(0, gap), cancellation.Token, account));
        Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(1, false)] [InlineData(2, false)]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, true)]
    public void PackedBooleanChargesAndRefundsOnInterruption(int interruption, bool skipStarts)
    {
        var input = Encoding.UTF8.GetBytes(skipStarts ? new string('z', 270000) : new string('a', 4096));
        using var cancellation = new CancellationTokenSource();
        using var clock = Utf8DeadlineTestClock.Start();
        using var regex = new Utf8Regex(skipStarts ? "(?i:(?<x>😀|a)){1,2}" : "(?:a?b|c)*[d-f][xy]", RegexOptions.NonBacktracking, new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(20),
            ResourcePolicy = new() { MaxWorkSteps = interruption == 0 ? 128UL : null }
        });
        var account = new Account();
        var diagnostics = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (interruption == 1) cancellation.Cancel();
            if (interruption == 2) clock.Advance(20);
        });
        try
        {
            if (interruption == 0)
            {
                var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.IsMatch(input, cancellation.Token, account));
                Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkSteps, error.ResourceKind);
                Assert.Equal(129UL, error.Requested);
            }
            else if (interruption == 1)
                Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, cancellation.Token, account)).CancellationToken);
            else
                Assert.Throws<RegexMatchTimeoutException>(() => regex.IsMatch(input, cancellation.Token, account));
            Assert.Equal(1, account.Attempts);
            Assert.Equal(0UL, account.Used);
        }
        finally { diagnostics.Complete(); }
    }

    [Fact]
    public void NonBacktrackingWholeRangesFitWhileSubcapturesStillRequireWorkingStorage()
    {
        using var regex = new Utf8Regex("(a?b|c)*(dé|😀)[xy]", RegexOptions.NonBacktracking, new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1024 }
        });
        var input = "éxx😀x!"u8.ToArray();
        var account = new Account();
        var whole = new Utf8CaptureRange[1];
        var from = "é"u8.Length;
        var expected = new Utf8CaptureRange("éxx"u8.Length, input.Length - 1);
        Assert.Equal(expected, regex.Match(input));
        Assert.Equal(expected, regex.Match(input, from, whole, workingMemoryAllowance: account, endOffsetInBytes: input.Length - 1));
        Assert.Equal(expected, whole[0]);
        Assert.Equal(1, regex.Count(input, workingMemoryAllowance: account));
        var groups = new Utf8CaptureRange[3];
        var denied = Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Match(input, from, groups, workingMemoryAllowance: account));
        Assert.Equal(Utf8ResourceLimitKind.ExecutionWorkingBytes, denied.ResourceKind);
        Assert.Equal(0UL, account.Used);
    }

    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public void CompactOrderedRangesRefundOnInterruption(bool expire, bool skipExactStarts)
    {
        var input = Encoding.UTF8.GetBytes((skipExactStarts ? new string('z', 140000) : new string('a', 4096)) + "😀x");
        using var cancellation = new CancellationTokenSource();
        using var clock = Utf8DeadlineTestClock.Start();
        using var regex = new Utf8Regex(skipExactStarts ? "(?<x>😀|a){1,2}" : "(a?b|c)*(dé|😀)[xy]", RegexOptions.NonBacktracking,
            new Utf8RegexPolicy { MatchTimeout = TimeSpan.FromMilliseconds(20) });
        var account = new Account();
        var session = Utf8SearchDiagnosticsSession.Start(_ =>
        {
            if (expire) clock.Advance(20);
            else cancellation.Cancel();
        });
        try
        {
            if (expire)
                Assert.Throws<RegexMatchTimeoutException>(() => regex.Match(input, cancellationToken: cancellation.Token, workingMemoryAllowance: account));
            else
                Assert.Equal(cancellation.Token, Assert.Throws<OperationCanceledException>(() =>
                    regex.Match(input, cancellationToken: cancellation.Token, workingMemoryAllowance: account)).CancellationToken);
            Assert.Equal(1, account.Attempts);
            Assert.Equal(0UL, account.Used);
        }
        finally { session.Complete(); }
    }

    private sealed class Account : IUtf8MemoryAllowance
    {
        internal ulong Used, Maximum = 64 << 20;
        internal int Attempts;
        public bool TryReserve(ulong bytes)
        {
            Attempts++; if (bytes > Maximum - Math.Min(Maximum, Used)) return false;
            Used += bytes; return true;
        }
        public void Release(ulong bytes) { Assert.InRange(bytes, 0UL, Used); Used -= bytes; }
    }
}
