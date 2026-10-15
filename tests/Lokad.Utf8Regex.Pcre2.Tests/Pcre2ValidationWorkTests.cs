using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ValidationWorkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PatternChecksDenyBeforeScanningWithNoWork(int entry)
    {
        using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue);
        var pattern = new string('a', 4096) + "(?C)";
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Validate(entry, pattern));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal((ulong)pattern.Length, failure.Requested);
        Assert.Equal(0ul, scope.Budget.WorkSteps);
        Assert.Equal(0ul, scope.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void UnterminatedTokenRescansCannotEscapeCumulativeWork(bool verb)
    {
        var pattern = verb ? string.Concat(Enumerable.Repeat("(*X", 256))
            : "[" + string.Concat(Enumerable.Repeat("[:", 256));
        var maximum = (ulong)pattern.Length * 3;
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, maximum + 1, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Pcre2CaptureNameValidator.Validate(pattern, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.InRange(owner.Budget.WorkSteps, 1ul, maximum + 1);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
    }

    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public void ValidationInheritsOriginalInterruptionAuthority(int entry, bool timeout)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var cancellation = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
        owner.Budget.Deadline = deadline;
        owner.Budget.CancellationToken = cancellation.Token;
        owner.Budget.CheckpointHook = _ => { if (timeout) ticks = 30000; else cancellation.Cancel(); };
        var failure = Record.Exception(() => Validate(entry, "plain😀"));
        if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Same(deadline, owner.Budget.Deadline);
        Assert.Equal(0ul, owner.Budget.WorkSteps);
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData(2046, false)] [InlineData(2047, false)] [InlineData(2048, false)]
    [InlineData(4095, false)] [InlineData(4096, false)]
    [InlineData(2047, true)] [InlineData(2048, true)] [InlineData(4096, true)]
    public void FundedTokenBoundariesKeepLiteralNamesAndRejectFollowingDefinitions(int length, bool verb)
    {
        // Keep token positions around the old scan boundaries while using a
        // valid 255-byte label. Larger verb names are syntax errors, not literals
        // that the complete admission validator may accept.
        var literal = verb ? new string('x', length) + "(*MARK:" + new string('a', 247) + "(?<100>a)"
            : "[[:" + new string('a', length) + ":]]";
        using var owner = Utf8ConstructionScope.Enter();
        // Qualify lexical admission alone; these bodies need not be served by
        // every compiler. A MARK argument is text, not a numeric definition.
        Pcre2CaptureNameValidator.Validate(literal, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf);
        Assert.True(owner.Budget.WorkSteps >= (ulong)literal.Length);
        var failure = Assert.Throws<Pcre2CompileException>(() =>
            Pcre2CaptureNameValidator.Validate(literal + "(?<100>b)", Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, failure.ErrorKind);
    }

    [Theory]
    [InlineData(2047)] [InlineData(2048)] [InlineData(4096)]
    public void FundedOversizedVerbLabelsFailAtTheirUtf8Limit(int length)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var failure = Assert.Throws<Pcre2CompileException>(() =>
            Pcre2CaptureNameValidator.Validate("(*MARK:" + new string('a', length) + "(?<100>a)",
                Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf));
        Assert.Equal(Pcre2ErrorKind.InvalidAfterParensQuery, failure.ErrorKind);
        Assert.Contains("255 UTF-8 bytes", failure.Message);
        Assert.Equal(0UL, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void NewNameChecksKeepTheExistingParentWorkAllowance(bool verb)
    {
        var pattern = verb ? "(*MARK:" + new string('é', 128) + ")a"
            : "(?<" + new string('é', 65) + ">a)";
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, 48,
            (ulong)pattern.Length + 1, ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 37;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Pcre2CaptureNameValidator.Validate(pattern, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(48UL, owner.Budget.ReservedBytes);
        Assert.Equal(0UL, owner.Budget.Depth);
        Assert.Equal(37, parent[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryValidationPassAndRescanRetainsParentInterruption(bool timeout)
    {
        foreach (var pattern in new[] { "(a)(?#ignored)[b]", "[" + string.Concat(Enumerable.Repeat("[:", 8)),
            string.Concat(Enumerable.Repeat("(*X", 8)) })
        {
            var checkpoints = 0;
            using (var probe = Utf8ConstructionScope.Enter())
            {
                probe.Budget.CheckpointHook = _ => checkpoints++;
                if (pattern[0] == '(' && pattern[1] != '*') Validate(2, pattern);
                else Validate(1, pattern);
            }
            Assert.True(checkpoints > 1);
            for (var target = 1; target <= checkpoints; target++)
            {
                using var owner = Utf8ConstructionScope.Enter();
                var parent = owner.Budget.AllocateArray<byte>(1, false);
                parent[0] = 37;
                using var cancellation = new CancellationTokenSource();
                long ticks = 0;
                using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
                owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
                owner.Budget.CancellationToken = cancellation.Token;
                var current = 0;
                owner.Budget.CheckpointHook = _ =>
                {
                    if (++current != target) return;
                    if (timeout) ticks = 30000; else cancellation.Cancel();
                };
                var failure = Record.Exception(() => Validate(pattern[0] == '(' && pattern[1] != '*' ? 2 : 1, pattern));
                if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
                else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
                Assert.Equal(48ul, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Equal(37, parent[0]);
            }
        }
    }

    private static void Validate(int entry, string pattern)
    {
        switch (entry)
        {
            case 0: _ = Pcre2PatternCodeScan.ContainsCodeText(pattern, "(?C"); break;
            case 1: Pcre2CaptureNameValidator.Validate(pattern, Pcre2CompileOptions.None, Pcre2NewlineConvention.Lf); break;
            default: Pcre2CompileValidator.ValidatePattern(pattern, Pcre2CompileOptions.None, default); break;
        }
    }
}
