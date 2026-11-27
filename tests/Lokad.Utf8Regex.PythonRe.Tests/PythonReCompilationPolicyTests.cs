using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Allowance = Lokad.Utf8Regex.PythonRe.Tests.PythonReSharedWorkingAllowanceTests.Allowance;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Collection(PythonReGlobalSettingsCollection.Name)]
public sealed class PythonReCompilationPolicyTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void FundedPolicyPublishesCompleteProgramsAndKeepsCursorAliases(bool utf8, bool factory)
    {
        var account = new Allowance(16 << 20);
        var policy = new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkingBytes = 1 << 20, MaxResultBytes = 1 << 20 },
            MatchTimeout = TimeSpan.FromSeconds(2),
            ProgramMemoryAllowance = account
        };
        var source = "(?P<x>😀)+"u8.ToArray();
        using var regex = utf8
            ? factory ? Utf8PythonRegex.Compile(source, policy: policy) : new Utf8PythonRegex(source, policy: policy)
            : factory ? Utf8PythonRegex.Compile("(?P<x>😀)+", policy: policy) : new Utf8PythonRegex("(?P<x>😀)+", policy: policy);
        Assert.Equal(policy.ResourcePolicy, regex.ResourcePolicy);
        Assert.Equal(TimeSpan.FromSeconds(2), regex.MatchTimeout);
        Array.Fill(source, (byte)'x');
        Assert.Equal("(?P<x>😀)+", regex.Pattern);
        var input = "😀😀"u8.ToArray();
        using var cursor = regex.CreateMatchCursor(input, mode: Utf8PythonMatchMode.FullMatch);
        var alias = cursor;
        regex.Dispose();
        Assert.True(account.Used > 0);
        Assert.Throws<ObjectDisposedException>(() => regex.Count(input));
        Assert.True(alias.MoveNext(out var retained));
        Assert.Equal(1, retained.LastIndex);
        Assert.False(cursor.MoveNext(out _));
        Assert.Equal(0UL, account.Used);
        alias.Dispose();
        Assert.Equal("😀", Encoding.UTF8.GetString(retained.GetGroupMemory(1).Span));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(3, false)]
    [InlineData(1, true)]
    [InlineData(2, true)]
    [InlineData(3, true)]
    public void ConstructorsEnforceExplicitConstructionLimits(int kind, bool utf8)
    {
        var construction = kind switch
        {
            0 => new Utf8ConstructionPolicy {  },
            1 => new Utf8ConstructionPolicy { MaxDepth = 1 },
            2 => new Utf8ConstructionPolicy { MaxInstructionCapacity = 1 },
            _ => new Utf8ConstructionPolicy { MaxBytes = 1 }
        };
        var policy = new Utf8RegexPolicy { ConstructionPolicy = construction };
        var error = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
        {
            if (utf8) _ = new Utf8PythonRegex("(?P<x>😀){2,4}"u8, policy: policy);
            else _ = new Utf8PythonRegex("(?P<x>😀){2,4}", policy: policy);
        });
        Assert.Equal(kind switch
        {
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            2 => Utf8ResourceLimitKind.ConstructionInstructionCapacity,
            _ => Utf8ResourceLimitKind.ConstructionBytes
        }, error.ResourceKind);
        Assert.Equal(1UL, error.Limit); Assert.False(Utf8ConstructionScope.IsActive);
        using var fresh = new Utf8PythonRegex("(?P<x>😀){2,4}");
        Assert.True(fresh.Match("😀😀"u8, mode: Utf8PythonMatchMode.FullMatch).Success);
    }

    [Fact]
    public void NullTimeoutsUseProfileDefaultAndIndependentInfiniteConstruction()
    {
        var previous = Utf8PythonRegex.DefaultMatchTimeout;
        try
        {
            Utf8PythonRegex.DefaultMatchTimeout = TimeSpan.FromSeconds(7);
            var policy = new Utf8RegexPolicy { MatchTimeout = null, ConstructionTimeout = null };
            using var constructed = new Utf8PythonRegex("(?P<x>a)", policy: policy);
            using var compiled = Utf8PythonRegex.Compile("(?P<x>a)"u8, policy: policy);
            Assert.Equal(TimeSpan.FromSeconds(7), constructed.MatchTimeout);
            Assert.Equal(constructed.MatchTimeout, compiled.MatchTimeout);
            Assert.Equal(Utf8ResourcePolicy.Unlimited, compiled.ResourcePolicy);
        }
        finally { Utf8PythonRegex.DefaultMatchTimeout = previous; }
    }

    [Fact]
    public void ImplicitConstructionBorrowsOwnerAndExplicitConstructionCannotReplaceIt()
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        using var scope = Utf8ConstructionScope.Enter();
        using var regex = new Utf8PythonRegex("(?P<x>a)", policy: new() { ResourcePolicy = new() { MaxDepth = 4096 } });
        var spent = scope.Budget.DiagnosticCheckpoints; Assert.True(spent > 0);
        Assert.Throws<InvalidOperationException>(() => new Utf8PythonRegex("a", policy: new()
        {
            ConstructionPolicy = new() { MaxDepth = 128 }
        }));
        Assert.Throws<InvalidOperationException>(() => Utf8PythonRegex.Compile("a"));
        Assert.Same(scope.Budget, Utf8ConstructionScope.CurrentBudget);
        Assert.Equal(spent, scope.Budget.DiagnosticCheckpoints);
    }

    [Fact]
    public void ConstructorDeadlineUsesConstructionAuthorityAndRestoresOwner()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks++ * 1000);
        var error = Assert.Throws<RegexMatchTimeoutException>(() => new Utf8PythonRegex("😀+"u8, policy: new()
        {
            MatchTimeout = TimeSpan.FromSeconds(30),
            ConstructionTimeout = TimeSpan.FromMilliseconds(1)
        }));
        Assert.Equal(TimeSpan.FromMilliseconds(1), error.MatchTimeout);
        Assert.Equal(string.Empty, error.Pattern);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Fact]
    public void PreCancellationWinsBeforeInvalidPolicyOptionsBytesAndAllowance()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        var account = new Allowance(0);
        var error = Assert.Throws<OperationCanceledException>(() => Utf8PythonRegex.Compile(new byte[] { 0xff },
            PythonReCompileOptions.Locale, new()
            {
                MatchTimeout = TimeSpan.Zero,
                ConstructionTimeout = TimeSpan.Zero,
                ResourcePolicy = new() { MaxDepth = 4096 },
                ConstructionPolicy = new() { MaxBytes = 0 },
                ProgramMemoryAllowance = account
            }, source.Token));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(0UL, account.Used); Assert.False(Utf8ConstructionScope.IsActive);
    }
}
