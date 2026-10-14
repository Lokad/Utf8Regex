using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReCaptureBodyArrayTests
{
    [Theory]
    [InlineData("((a)(b))", 3)]
    [InlineData("(?P<outer>(a)(?P<inner>b))", 3)]
    [InlineData("(?<=(a)(b))c", 2)]
    [InlineData("(?P<x>a){0}(?(x)b|c)", 1)]
    [InlineData("(?P<x>)(?P=x)", 1)]
    [InlineData("a", 0)]
    public void ClosedBodiesKeepOpeningOrderAfterParserScratchCloses(string pattern, int count)
    {
        var parse = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
        Assert.Equal(count, parse.GroupBodies.Length);
        Assert.All(parse.GroupBodies, Assert.NotNull);
        var pending = new Stack<PythonReNode>(); pending.Push(parse.Root);
        var found = 0;
        while (pending.TryPop(out var node))
        {
            switch (node)
            {
                case PythonReGroupNode group:
                    if (group.GroupNumber > 0) { Assert.Same(group.Inner, parse.GroupBodies[group.GroupNumber - 1]); found++; }
                    pending.Push(group.Inner); break;
                case PythonReSequenceNode sequence:
                    foreach (var child in sequence.Elements) pending.Push(child); break;
                case PythonReAlternationNode alternate:
                    foreach (var child in alternate.Branches) pending.Push(child); break;
                case PythonReQuantifierNode repeat: pending.Push(repeat.Inner); break;
                case PythonReConditionalNode conditional:
                    pending.Push(conditional.YesBranch);
                    if (conditional.NoBranch is { } no) pending.Push(no); break;
            }
        }
        Assert.Equal(count, found);
        Assert.False(parse.TryGetGroupBody(0, out _));
        Assert.False(parse.TryGetGroupBody(count + 1, out _));
        _ = PythonReTranslator.Translate(parse);
        Assert.False(Utf8ConstructionScope.IsActive);
    }

    [Theory]
    [InlineData(@"(\1)")]
    [InlineData(@"(?P<x>(?P=x))")]
    [InlineData(@"\1(a)")]
    [InlineData(@"(a)(?<=\2)(b)")]
    public void InvalidOrOpenReferencesStillFail(string pattern)
        => Assert.Throws<PythonRePatternException>(() => new Utf8PythonRegex(pattern));

    [Fact]
    public void BodyLookupUsesCurrentWorkOwnerWithoutRetainingParserAuthority()
    {
        var parse = new PythonReParser("(😀)").Parse(PythonReCompileOptions.None);
        Assert.True(parse.TryGetGroupBody(1, out var body));
        using (var denied = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 0, ulong.MaxValue))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => parse.TryGetGroupBody(1, out _));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
            Assert.Equal(0UL, denied.Budget.ReservedBytes);
        }
        Assert.True(parse.TryGetGroupBody(1, out var retained));
        Assert.Same(body, retained);
    }

    [Fact]
    public void PublicConstructionCapsRemainEnforceableForDenseCaptures()
    {
        var pattern = string.Concat(Enumerable.Repeat("(a)", 65));
        var expected = new Utf8PythonRegex(pattern);
        foreach (var work in new[] { false, true })
        {
            Utf8PythonRegex Compile(ulong cap) => Utf8PythonRegex.Compile(pattern, PythonReCompileOptions.None,
                Regex.InfiniteMatchTimeout, Utf8ResourcePolicy.Unlimited,
                work ? new() { MaxWorkSteps = cap } : new() { MaxBytes = cap }, Regex.InfiniteMatchTimeout, default);
            Assert.Equal(expected.GetGroupNames(), Compile(4000000).GetGroupNames());
            foreach (var cap in new[] { 1UL, 128UL, 1024UL })
            {
                var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => Compile(cap));
                Assert.Equal(work ? Utf8ResourceLimitKind.ConstructionWorkSteps : Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind);
                Assert.Equal(cap, error.Limit);
                Assert.False(Utf8ConstructionScope.IsActive);
            }
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(17)] [InlineData(65)]
    public void DenseBodyGrowthAndPublicationDenialsRestoreParentLoans(int groups)
    {
        var pattern = new string('(', groups) + "a" + new string(')', groups);
        ulong work, bytes;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            _ = new PythonReParser(pattern).Parse(PythonReCompileOptions.None);
            work = measured.Budget.WorkSteps; bytes = measured.Budget.PeakBytes;
        }
        foreach (var cap in new[] { 1UL, work / 2, work - 1 })
        {
            using var scope = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cap, ulong.MaxValue);
            scope.Budget.ReserveBytes(13);
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, error.ResourceKind);
            Assert.Equal(13UL, scope.Budget.ReservedBytes); Assert.Equal(0UL, scope.Budget.Depth);
        }
        using (var scope = new Utf8ConstructionScope(ulong.MaxValue, bytes + 12, ulong.MaxValue, ulong.MaxValue))
        {
            scope.Budget.ReserveBytes(13);
            var error = Assert.Throws<Utf8ResourceLimitExceededException>(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, error.ResourceKind);
            Assert.Equal(13UL, scope.Budget.ReservedBytes); Assert.Equal(0UL, scope.Budget.Depth);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void DenseBodyPreparationPreservesParentInterruptionAndRetainedParse(bool expire)
    {
        var retained = new PythonReParser("((😀))").Parse(PythonReCompileOptions.None);
        long ticks = 0;
        using var clock = Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        using var token = new CancellationTokenSource();
        using var scope = Utf8ConstructionScope.Enter();
        scope.Budget.ReserveBytes(13);
        scope.Budget.CancellationToken = token.Token;
        scope.Budget.Deadline = expire ? Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(20))
            : Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadline.Infinite;
        var entered = false;
        scope.Budget.CheckpointHook = work => { if (work > 300) { entered = true; if (expire) ticks = 50; else token.Cancel(); } };
        var pattern = new string('(', 65) + "a" + new string(')', 65);
        if (expire) Assert.Throws<Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadlineExpiredException>(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None));
        else Assert.Equal(token.Token, Assert.Throws<OperationCanceledException>(() => new PythonReParser(pattern).Parse(PythonReCompileOptions.None)).CancellationToken);
        Assert.True(entered); Assert.Equal(13UL, scope.Budget.ReservedBytes); Assert.Equal(0UL, scope.Budget.Depth);
        scope.Budget.CheckpointHook = null;
        scope.Budget.CancellationToken = default;
        scope.Budget.Deadline = Lokad.Utf8Regex.Internal.Execution.Utf8ExecutionDeadline.Infinite;
        Assert.True(retained.TryGetGroupBody(1, out var body));
        Assert.IsType<PythonReGroupNode>(body);
        Assert.Equal(1, new Utf8PythonRegex("(a)").Count("a"u8));
    }
}
