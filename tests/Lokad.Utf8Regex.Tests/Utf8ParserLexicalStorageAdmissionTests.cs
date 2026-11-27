using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ParserLexicalStorageAdmissionTests
{
    private const RegexOptions Options = RegexOptions.CultureInvariant;
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    public static IEnumerable<object[]> Shapes()
    {
        yield return ["", Options];
        yield return ["café😀", Options];
        yield return ["ab😀{2}z", Options];
        yield return ["(?:a|bc|😀){1,3}z", Options];
        yield return ["(?<x>ab)(?<90000>😀)\\k<x>", Options];
        yield return ["(?<x>a)(?<-x>b)(?(x)c|d)", Options];
        yield return ["(?(?=😀)😀|b)(?<=b)", Options];
        yield return ["x\\1012{2}y\\08", Options];
        yield return ["(?i:a(?-i:b))|(?<x>😀)", Options];
        yield return ["(?<x>a)(?(x)b|)(?(x)|c)", Options];
        yield return ["[a-z-[bc]]+\\p{Lu}?", Options];
        yield return ["(a)\\123x", Options | RegexOptions.ECMAScript];
    }

    private static Utf8OwnedRegexTree Create(string pattern, RegexOptions options)
    {
        var tree = Utf8OwnedTreeParser.TryParse(pattern, options);
        Assert.NotNull(tree);
        return tree;
    }

    [Fact]
    public void RetainedParserLiteralOwnsItsTextAndFitsNumericAdmission()
    {
        var pattern = new string('a', 256);
        using var owner = Utf8ConstructionScope.Enter();
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var literal = tree.Root.Child(0).Str;
        Assert.Equal(pattern, literal);
        Assert.NotSame(pattern, literal);
        Assert.True(owner.Budget.ReservedBytes >= Utf8ConstructionBudget.StringBytes(literal.Length) + 128ul);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ReturnedMajorArraysFitNumericAdmissionAlongsideConservativeHelpers(string pattern, RegexOptions options)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var tree = Create(pattern, options);
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var bytes = 0ul;
        Visit(tree);
        Assert.True(owner.Budget.ReservedBytes >= bytes + 128ul);
        Assert.True(owner.Budget.PeakBytes > owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);

        void Visit(object? value)
        {
            if (value is null || !visited.Add(value)) return;
            Assert.False(value is Utf8ConstructionBuffer<Utf8TreeNode>, "Published trees cannot retain parser scratch vectors.");
            var type = value.GetType();
            if (value is Array array)
            {
                var measure = typeof(Utf8ConstructionBudget).GetMethod("ArrayBytes", BindingFlags.Static | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException();
                bytes += (ulong)(measure.MakeGenericMethod(type.GetElementType() ?? throw new InvalidOperationException())
                    .Invoke(null, [array.Length]) ?? throw new InvalidOperationException());
                foreach (var element in array) Visit(element);
                return;
            }
            if (type.Namespace?.StartsWith("Lokad.Utf8Regex.Internal", StringComparison.Ordinal) != true || type.IsEnum) return;
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                Visit(field.GetValue(value));
        }
    }


    [Theory]
    [MemberData(nameof(Shapes))]
    public void CapacityAndDepthCutoffsRecoverWithoutLosingParentLoans(string pattern, RegexOptions options)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        ulong peak, depth, recoveryPeak;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Create(pattern, options);
            peak = measured.Budget.PeakBytes;
            depth = measured.Budget.PeakDepth;
        }
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.AllocateObject(17, static value => new ParentValue(value));
            Create("a", Options);
            recoveryPeak = measured.Budget.PeakBytes;
        }
        foreach (var cap in new[] { 128ul, 224ul, 512ul, 1024ul, 2048ul, peak / 2, peak - 1, peak }.Distinct().Order())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Create(pattern, options));
                if (failure is not null)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                    Assert.Equal(cap, breach.Limit);
                    Assert.True(breach.Requested > cap);
                }
                if (cap == peak) Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.True(owner.Budget.PeakBytes <= cap);
            if (cap >= recoveryPeak)
            {
                var spent = owner.Budget.DiagnosticCheckpoints;
                using (var discarded = Utf8ConstructionScope.Enter()) Create("a", Options);
                Assert.True(owner.Budget.DiagnosticCheckpoints > spent);
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
        for (var cap = 0ul; cap < depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            var breach = Assert.Throws<Utf8ResourceLimitExceededException>(() => Create(pattern, options));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, breach.ResourceKind);
            Assert.Equal(cap, breach.Limit);
            Assert.True(breach.Requested > cap);
            AssertParent(owner.Budget, checkpoint, parent);
        }
    }

    public static IEnumerable<object[]> Interruptions()
    {
        foreach (var shape in Shapes())
        {
            yield return [shape[0], shape[1], false];
            yield return [shape[0], shape[1], true];
        }
    }

    [Theory]
    [MemberData(nameof(Interruptions))]
    public void EveryCheckpointKeepsInheritedTokenAndDeadlineAuthority(string pattern, RegexOptions options, bool timeout)
    {
        var count = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = () => count++;
            Create(pattern, options);
        }
        Assert.True(count > 0);
        for (var target = 1; target <= count; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
            owner.Budget.Deadline = deadline;
            owner.Budget.CancellationToken = cancellation.Token;
            var current = 0;
            owner.Budget.CheckpointHook = () =>
            {
                if (++current != target) return;
                if (timeout) ticks = 30000;
                else cancellation.Cancel();
            };
            var failure = Record.Exception(() => Create(pattern, options));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Same(deadline, owner.Budget.Deadline);
            AssertParent(owner.Budget, checkpoint, parent);
            owner.Budget.CheckpointHook = null;
            owner.Budget.CancellationToken = default;
            owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
            using (var discarded = Utf8ConstructionScope.Enter()) Create(pattern, options);
            AssertParent(owner.Budget, checkpoint, parent);
        }
    }

    private static void AssertParent(Utf8ConstructionBudget budget,
        ulong checkpoint, ParentValue parent)
    {
        Assert.Equal(128ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.Reserved);
        Assert.Equal(0ul, budget.Depth);
        Assert.Equal(checkpoint, budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }


    [Theory]
    [InlineData("(")]
    [InlineData("(?<x>a")]
    [InlineData("(?(?=a)b|c")]
    [InlineData("(?<x-y>a)")]
    [InlineData("a[bc")]
    public void SyntaxDeclinesReleaseAllLexicalStorageAndTheSameOwnerRecovers(string pattern)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, Options, out var diagnostic));
        Assert.True(diagnostic.IsSyntaxError);
        AssertParent(owner.Budget, checkpoint, parent);
        var spent = owner.Budget.DiagnosticCheckpoints;
        using (var discarded = Utf8ConstructionScope.Enter()) Create("a", Options);
        Assert.True(owner.Budget.DiagnosticCheckpoints > spent);
        AssertParent(owner.Budget, checkpoint, parent);
    }

    [Fact]
    public void LeadingOptionsReturnASourceOffsetWithNoByteHeadroom()
    {
        const string pattern = "(?im)(?-m)😀z";
        using var owner = new Utf8ConstructionScope(0, 0, ulong.MaxValue);
        var options = Options;
        var offset = Utf8OwnedOptionPrelude.ConsumeLeadingGlobalOptions(new(pattern), ref options);
        Assert.True(pattern.AsSpan(offset).SequenceEqual("😀z"));
        Assert.Equal(Options | RegexOptions.IgnoreCase, options);
        Assert.Equal(0, Utf8OwnedOptionPrelude.ConsumeLeadingGlobalOptions(new("abc"), ref options));
        Assert.Equal(0ul, owner.Budget.PeakBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryOptionWorkCutoffAndInterruptionPreservesParentAndRefOptions(bool timeout)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        const string pattern = "(?im)(?-m)😀z";
        ulong work;
        var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = () => checkpoints++;
            var options = Options;
            _ = Utf8OwnedOptionPrelude.ConsumeLeadingGlobalOptions(new(pattern), ref options);
            work = measured.Budget.DiagnosticCheckpoints;
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using var cancellation = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            owner.Budget.Deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
            owner.Budget.CancellationToken = cancellation.Token;
            var current = 0;
            owner.Budget.CheckpointHook = () =>
            {
                if (++current != target) return;
                if (timeout) ticks = 30000; else cancellation.Cancel();
            };
            var options = Options;
            var failure = Record.Exception(() => Utf8OwnedOptionPrelude.ConsumeLeadingGlobalOptions(new(pattern), ref options));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(Options, options);
            AssertParent(owner.Budget, checkpoint, parent);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrontEndEntriesBorrowSourceTextAndRespectSharedWorkCapsForFreshGraphs(bool compile)
    {
        using var checkpointObservation = global::Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Start();
        const string pattern = "(?i)ab😀";
        // These entries create fresh nodes and reference-keyed admitted maps.
        // Probe work can vary with node identities; bound actual work rather
        // than requiring the reference run's exact count on every fresh graph.
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            var executionPattern = compile ? Utf8FrontEnd.Compile(pattern, Options).SemanticRegex.ExecutionPattern : Utf8FrontEnd.Analyze(pattern, Options).SemanticRegex.ExecutionPattern;
            Assert.True(executionPattern == pattern.AsSpan(4));
            work = measured.Budget.DiagnosticCheckpoints;
        }
    }

}
