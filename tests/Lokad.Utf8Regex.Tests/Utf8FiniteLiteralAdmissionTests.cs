using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8FiniteLiteralAdmissionTests
{
    [Theory]
    [InlineData("[ab]{2}", RegexOptions.None)]
    [InlineData("ab[cd]ef", RegexOptions.None)]
    [InlineData("(?:ab|cd){1,2}[xy]", RegexOptions.None)]
    [InlineData("[Aa]x", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    [InlineData("[AaBbCcDd]{2}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public void ReturnedLiteralBuffersRemainAdmitted(string pattern, RegexOptions options)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var semantic = Utf8FrontEnd.Analyze(pattern, options).SemanticRegex;
        using var owner = Utf8ConstructionScope.Enter();
        Assert.True(Utf8FiniteLiteralLowerer.TryCreateLiteralBranches(semantic, options, out var literals));
        Assert.InRange(literals.Length, 1, 64);
        var bytes = Utf8ConstructionBudget.ArrayBytes<byte[]>(literals.Length);
        foreach (var literal in literals)
        {
            Assert.NotEmpty(literal);
            bytes += Utf8ConstructionBudget.ArrayBytes<byte>(literal.Length);
        }
        Assert.True(owner.Budget.ReservedBytes >= bytes);
        Assert.True(owner.Budget.PeakBytes >= owner.Budget.ReservedBytes);
        Assert.True(owner.Budget.DiagnosticCheckpoints > 0);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData(0)] [InlineData(2)]
    public void DeniedConstructionPreservesTheParentBuffer(int dimension)
    {
        var semantic = Utf8FrontEnd.Analyze("(?:ab|cd){1,3}[xy]", RegexOptions.None).SemanticRegex;
        using var owner = new Utf8ConstructionScope(0, dimension == 0 ? 48ul : ulong.MaxValue,
            dimension == 2 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateArray<byte>(1, false);
        parent[0] = 0xa5;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
            Utf8FiniteLiteralLowerer.TryCreateLiteralBranches(semantic, RegexOptions.None, out _));
        Assert.Equal(dimension switch { 0 => Utf8ResourceLimitKind.ConstructionBytes, _ => Utf8ResourceLimitKind.ConstructionDepth }, failure.ResourceKind);
        Assert.Equal(48ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0xa5, parent[0]);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ConstructionKeepsOriginalInterruptionAuthority(bool expire)
    {
        var semantic = Utf8FrontEnd.Analyze("(?:ab|cd){1,3}[xy]", RegexOptions.None).SemanticRegex;
        using var clock = Utf8DeadlineTestClock.Start();
        using var cancellation = new CancellationTokenSource();
        using var owner = Utf8ConstructionScope.Enter();
        owner.Budget.ReserveBytes(7);
        owner.Budget.CancellationToken = cancellation.Token;
        owner.Budget.Deadline = Utf8ExecutionDeadline.Start(expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        owner.Budget.CheckpointHook = () => { if (expire) clock.Advance(250); else cancellation.Cancel(); };
        var failure = Record.Exception(() =>
            Utf8FiniteLiteralLowerer.TryCreateLiteralBranches(semantic, RegexOptions.None, out _));
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
        else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
        Assert.Equal(7ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData("[ab]{96}")] [InlineData("a.{1,2}b")] [InlineData("[0-9]{2}")]
    [InlineData("(?=abc)ab[cd]")] [InlineData("\\Aab[cd]\\Z")]
    public void NonliteralOrExcessiveExpansionsDeclineWithoutRetainingScratch(string pattern)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        var semantic = Utf8FrontEnd.Analyze(pattern, RegexOptions.None).SemanticRegex;
        using var owner = Utf8ConstructionScope.Enter();
        Assert.False(Utf8FiniteLiteralLowerer.TryCreateLiteralBranches(semantic, RegexOptions.None, out _));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.True(owner.Budget.DiagnosticCheckpoints > 0);
    }

    [Theory]
    [InlineData(RegexOptions.CultureInvariant)]
    [InlineData(RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    public void WideClassRepeatDeclinesBeforeExpandingRepeatLengths(RegexOptions options)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        Assert.Equal(Measure("[A-Za-z]{0,2}ing"), Measure("[A-Za-z]{0,12}ing"));

        (ulong Work, ulong PeakBytes) Measure(string pattern)
        {
            var semantic = Utf8FrontEnd.Analyze(pattern, options).SemanticRegex;
            using var owner = Utf8ConstructionScope.Enter();
            Assert.False(Utf8FiniteLiteralLowerer.TryCreateLiteralBranches(semantic, options, out _));
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
            return (owner.Budget.DiagnosticCheckpoints, owner.Budget.PeakBytes);
        }
    }
}
