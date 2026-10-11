using System.Reflection;
using System.Runtime.CompilerServices;
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
    public void RetainedParserLiteralHasItsOwnStringAndDescriptorLoan()
    {
        var pattern = new string('a', 256);
        using var owner = Utf8ConstructionScope.Enter();
        var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(tree);
        var literal = tree.Root.Child(0).Str;
        Assert.Equal(pattern, literal);
        Assert.NotSame(pattern, literal);
        var tracked = false;
        for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
            tracked |= ReferenceEquals(literal, loan.Value);
        Assert.True(tracked, "Retained parser literal text requires its actual string capacity and retaining descriptor.");
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void CompleteReturnedGraphKeepsExactlyItsArrayAndDescriptorLoans(string pattern, RegexOptions options)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var tree = Create(pattern, options);
        var descriptors = 0ul;
        for (var loan = owner.Budget.ObjectCheckpoint; loan is not null; loan = loan.Previous)
        {
            descriptors += loan.Bytes;
            if (loan.Value is string text)
            {
                Assert.Equal(Utf8ConstructionBudget.StringBytes(text.Length) + 64, loan.Bytes);
                continue;
            }
            var fields = loan.Value.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var extent = fields.Aggregate(0ul, (size, field) => size + Size(field.FieldType));
            Assert.True(loan.Bytes >= 32 + ((extent + 15) & ~15ul) + 64,
                $"Complete instance extent for {loan.Value.GetType()} must remain admitted.");
        }
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var bytes = 0ul;
        Visit(tree);
        Assert.Equal(descriptors + bytes, owner.Budget.ReservedBytes);
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

    private static ulong Size(Type type) => type.IsValueType
        ? (ulong)(typeof(Utf8ParserLexicalStorageAdmissionTests).GetMethod(nameof(ValueSize), BindingFlags.Static | BindingFlags.NonPublic)
            ?.MakeGenericMethod(type).Invoke(null, null) ?? throw new InvalidOperationException()) : 8ul;
    private static ulong ValueSize<T>() => (ulong)Unsafe.SizeOf<T>();

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryWorkCutoffRestoresParentAndNeverRefundsSpentWork(string pattern, RegexOptions options)
    {
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            Create(pattern, options);
            work = measured.Budget.WorkSteps;
        }
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Create(pattern, options));
                if (cutoff < work)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                    Assert.Equal(cutoff + 2, breach.Limit);
                    Assert.True(breach.Requested > breach.Limit);
                }
                else Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void CapacityAndDepthCutoffsRecoverWithoutLosingParentLoans(string pattern, RegexOptions options)
    {
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
        foreach (var cap in new[] { 112ul, 224ul, 512ul, 1024ul, 2048ul, peak / 2, peak - 1, peak }.Distinct().Order())
        {
            using var owner = new Utf8ConstructionScope(0, cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
                var spent = owner.Budget.WorkSteps;
                using (var discarded = Utf8ConstructionScope.Enter()) Create("a", Options);
                Assert.True(owner.Budget.WorkSteps > spent);
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
        for (var cap = 0ul; cap < depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
            measured.Budget.CheckpointHook = _ => count++;
            Create(pattern, options);
        }
        Assert.True(count > 0);
        for (var target = 1; target <= count; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using var cancellation = new CancellationTokenSource();
            long ticks = 0;
            using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromSeconds(30));
            owner.Budget.Deadline = deadline;
            owner.Budget.CancellationToken = cancellation.Token;
            var current = 0;
            owner.Budget.CheckpointHook = _ =>
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
        Utf8ConstructionBudget.ObjectAllocation? checkpoint, ParentValue parent)
    {
        Assert.Equal(112ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.Reserved);
        Assert.Equal(0ul, budget.Depth);
        Assert.Same(checkpoint, budget.ObjectCheckpoint);
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
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ObjectCheckpoint;
        Assert.Null(Utf8OwnedTreeParser.TryParse(pattern, Options, out var diagnostic));
        Assert.True(diagnostic.IsSyntaxError);
        AssertParent(owner.Budget, checkpoint, parent);
        var spent = owner.Budget.WorkSteps;
        using (var discarded = Utf8ConstructionScope.Enter()) Create("a", Options);
        Assert.True(owner.Budget.WorkSteps > spent);
        AssertParent(owner.Budget, checkpoint, parent);
    }

    [Fact]
    public void LeadingOptionTextSharesCapacityAndDenialDoesNotPublishStagedOptions()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var options = Options;
        var text = Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions("(?im)(?-m)😀z", ref options);
        Assert.Equal("😀z", text);
        Assert.Equal(Options | RegexOptions.IgnoreCase, options);
        Assert.Same(text, owner.Budget.ObjectCheckpoint!.Value);
        Assert.Equal(Utf8ConstructionBudget.StringBytes(text.Length) + 64, owner.Budget.ReservedBytes);
        var unchanged = "abc";
        Assert.Same(unchanged, Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(unchanged, ref options));
        Assert.Equal(Utf8ConstructionBudget.StringBytes(text.Length) + 64, owner.Budget.ReservedBytes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EveryOptionWorkCutoffAndInterruptionPreservesParentAndRefOptions(bool timeout)
    {
        const string pattern = "(?im)(?-m)😀z";
        ulong work;
        var checkpoints = 0;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            measured.Budget.CheckpointHook = _ => checkpoints++;
            var options = Options;
            _ = Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(pattern, ref options);
            work = measured.Budget.WorkSteps;
        }
        for (var cap = 0ul; cap < work; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cap + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var options = Options;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() =>
                Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(pattern, ref options));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
            Assert.Equal(Options, options);
            AssertParent(owner.Budget, checkpoint, parent);
        }
        for (var target = 1; target <= checkpoints; target++)
        {
            using var owner = Utf8ConstructionScope.Enter();
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
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
            var options = Options;
            var failure = Record.Exception(() => Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions(pattern, ref options));
            if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(cancellation.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Equal(Options, options);
            AssertParent(owner.Budget, checkpoint, parent);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FrontEndEntriesKeepNormalizedTextAndRespectSharedWorkCapsForFreshGraphs(bool compile)
    {
        const string pattern = "(?i)ab😀";
        // These entries create fresh nodes and reference-keyed admitted maps.
        // Probe work can vary with node identities; bound actual work rather
        // than requiring the reference run's exact count on every fresh graph.
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        {
            var executionPattern = compile ? Utf8FrontEnd.Compile(pattern, Options).SemanticRegex.ExecutionPattern : Utf8FrontEnd.Analyze(pattern, Options).SemanticRegex.ExecutionPattern;
            Assert.Equal("ab😀", executionPattern);
            Assert.Contains(Loans(measured.Budget), loan => ReferenceEquals(loan.Value, executionPattern));
            work = measured.Budget.WorkSteps;
        }
        foreach (var cap in new[] { 0ul, work / 3, work - 1 })
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, cap + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            Exception? failure;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                failure = Record.Exception(() =>
                {
                    if (compile)
                    {
                        var prepared = Utf8FrontEnd.Compile(pattern, Options);
                        Assert.Equal("ab😀", prepared.SemanticRegex.ExecutionPattern);
                        Assert.NotNull(prepared.SemanticRegex.CaptureCatalog);
                    }
                    else
                    {
                        var semantic = Utf8FrontEnd.Analyze(pattern, Options).SemanticRegex;
                        Assert.Equal("ab😀", semantic.ExecutionPattern);
                        Assert.NotNull(semantic.RuntimeTree);
                    }
                });
                if (failure is not null)
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                if (cap == 0) Assert.NotNull(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cap + 2);
        }
    }

    [Fact]
    public void LeadingOptionByteBoundaryPublishesOptionsOnlyAfterTheRetainedTextIsAdmitted()
    {
        var loan = Utf8ConstructionBudget.StringBytes("😀z".Length) + 64;
        foreach (var cap in new[] { 0ul, 1ul, loan - 1, loan })
        {
            using var owner = new Utf8ConstructionScope(0, 112 + cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ObjectCheckpoint;
            var options = Options;
            using (var discarded = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Utf8OwnedOptionPrelude.NormalizeLeadingGlobalOptions("(?im)(?-m)😀z", ref options));
                if (cap < loan)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                    Assert.Equal(112 + loan, breach.Requested);
                    Assert.Equal(Options, options);
                }
                else { Assert.Null(failure); Assert.Equal(Options | RegexOptions.IgnoreCase, options); }
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.True(owner.Budget.PeakBytes <= 112 + cap);
        }
    }

    private static IEnumerable<Utf8ConstructionBudget.ObjectAllocation> Loans(Utf8ConstructionBudget budget)
    {
        for (var loan = budget.ObjectCheckpoint; loan is not null; loan = loan.Previous) yield return loan;
    }
}
