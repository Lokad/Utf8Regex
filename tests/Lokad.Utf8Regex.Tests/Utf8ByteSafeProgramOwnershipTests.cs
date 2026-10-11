using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ByteSafeProgramOwnershipTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8ExecutionTree Tree(string pattern)
    {
        var tree = Utf8ExecutionTreeLowerer.Lower(Utf8FrontEnd.Analyze(pattern, RegexOptions.CultureInvariant).SemanticRegex);
        Assert.NotNull(tree); return tree;
    }
    private static ulong ObjectBytes(Utf8ConstructionBudget.ObjectAllocation? current, Utf8ConstructionBudget.ObjectAllocation? before)
    {
        ulong total = 0;
        for (; !ReferenceEquals(current, before); current = current.Previous)
        { Assert.NotNull(current); total += current.Bytes; }
        return total;
    }
    private static ulong GraphBytes(Utf8ByteSafeLinearVerifierStep[] root)
    {
        var seen = new HashSet<Array>(ReferenceEqualityComparer.Instance); var pending = new Stack<Array>(); pending.Push(root);
        ulong total = 0;
        while (pending.TryPop(out var array))
        {
            if (array.Length == 0 || !seen.Add(array)) continue;
            switch (array)
            {
                case Utf8ByteSafeLinearVerifierStep[] steps:
                    total += Utf8ConstructionBudget.ArrayBytes<Utf8ByteSafeLinearVerifierStep>(steps.Length);
                    foreach (var step in steps) { pending.Push(step.Program); pending.Push(step.Text); pending.Push(step.Alternatives); }
                    break;
                case byte[][] alternatives:
                    total += Utf8ConstructionBudget.ArrayBytes<byte[]>(alternatives.Length);
                    foreach (var value in alternatives) pending.Push(value);
                    break;
                case byte[] bytes: total += Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length); break;
                default: throw new InvalidOperationException();
            }
        }
        return total;
    }

    [Fact]
    public void SuccessfulProgramsTransferExactReachableArrayLoans()
    {
        foreach (var pattern in new[] { "a", "abc", "^(?:cat|dog)z$", "^(?:ab[0-9]){2}$", "^a*(?:b)?c$" })
        {
            var tree = Tree(pattern);
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var result = Utf8ByteSafeLinearVerifierProgram.Compile(tree); Assert.True(result.Succeeded);
            Assert.Equal(112 + GraphBytes(result.Program.Steps) + ObjectBytes(owner.Budget.ObjectCheckpoint, checkpoint), owner.Budget.ReservedBytes);
            Assert.True(owner.Budget.ReservedBytes > 112); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved);
            Assert.Equal(17, parent.Value);
        }
        // Raw execution trees must retain lexical folding even when the normal
        // lowerer would have projected a folded literal to a scalar class.
        var folded = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
        foreach (var node in new Utf8ExecutionNode[] {
            new(Utf8ExecutionNodeKind.One, folded, [], "", 'a', 0, 1, 1),
            new(Utf8ExecutionNodeKind.Multi, folded, [], "ab", '\0', 0, 1, 1),
            new(Utf8ExecutionNodeKind.Loop, folded, [], "", 'k', 0, 1, 2),
            new(Utf8ExecutionNodeKind.Loop, RegexOptions.None,
                [new(Utf8ExecutionNodeKind.Multi, folded, [], "ab", '\0', 0, 1, 1)], "", '\0', 0, 1, 2) })
            Assert.False(Utf8ByteSafeLinearVerifierProgram.Compile(new(node)).Succeeded);
        var exact = new Utf8ExecutionTree(new(Utf8ExecutionNodeKind.Multi, folded, [], "!_", '\0', 0, 1, 1));
        using var exactOwner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var exactCheckpoint = exactOwner.Budget.ObjectCheckpoint;
        var exactResult = Utf8ByteSafeLinearVerifierProgram.Compile(exact); Assert.True(exactResult.Succeeded);
        Assert.Equal(GraphBytes(exactResult.Program.Steps) + ObjectBytes(exactOwner.Budget.ObjectCheckpoint, exactCheckpoint), exactOwner.Budget.ReservedBytes);
        Assert.True(exactResult.Program.TryMatch("!_"u8, 0, out var exactLength, out var exactFallback));
        Assert.Equal(2, exactLength); Assert.False(exactFallback);
    }

    [Fact]
    public void EveryByteAndWorkCutoffEitherFundsTheWholeProgramOrRestoresTheParent()
    {
        var root = new Utf8ExecutionTree(new(Utf8ExecutionNodeKind.Concatenate, RegexOptions.None,
            [new(Utf8ExecutionNodeKind.One, RegexOptions.None, [], "", 'a', 0, 1, 1), new(Utf8ExecutionNodeKind.One, RegexOptions.None, [], "", 'b', 0, 1, 1)], "", '\0', 0, 1, 1));
        ulong peak, work;
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Assert.True(Utf8ByteSafeLinearVerifierProgram.Compile(root).Succeeded); peak = funded.Budget.PeakBytes; work = funded.Budget.WorkSteps; }
        Assert.InRange(peak, 1ul, 20000ul); Assert.InRange(work, 1ul, 10000ul);
        foreach (var bytes in new[] { false, true })
        {
            var limit = bytes ? peak : work; var successes = 0;
            for (var cutoff = 0ul; cutoff <= limit; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, bytes ? 112 + cutoff : ulong.MaxValue, bytes ? ulong.MaxValue : 2 + cutoff, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
                var outcome = default(Utf8ByteSafeLinearCompileOutcome);
                var failure = Record.Exception(() => outcome = Utf8ByteSafeLinearVerifierProgram.Compile(root));
                if (failure is null)
                {
                    successes++; Assert.True(outcome.Succeeded);
                    Assert.Equal(112 + GraphBytes(outcome.Program.Steps) + ObjectBytes(owner.Budget.ObjectCheckpoint, checkpoint), owner.Budget.ReservedBytes);
                    Assert.True(outcome.Program.TryMatch("ab"u8, 0, out var length, out var fallback)); Assert.Equal(2, length); Assert.False(fallback);
                }
                else
                {
                    Assert.Equal(bytes ? Utf8ResourceLimitKind.ConstructionBytes : Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                    Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
                }
                Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(0ul, owner.Budget.Reserved); Assert.Equal(17, parent.Value);
                Assert.True(bytes ? owner.Budget.PeakBytes <= 112 + cutoff : owner.Budget.WorkSteps <= 2 + cutoff);
            }
            Assert.True(successes > 0); if (!bytes) Assert.Equal(1, successes);
        }
    }

    [Fact]
    public void CaptureGroupAndAtomicWrappersConsumeActualCompilationDepth()
    {
        foreach (var kind in new[] { Utf8ExecutionNodeKind.Capture, Utf8ExecutionNodeKind.Group, Utf8ExecutionNodeKind.Atomic })
        {
            var node = new Utf8ExecutionNode(Utf8ExecutionNodeKind.One, RegexOptions.None, [], "", 'a', 0, 1, 1);
            for (var index = 0; index < 65; index++) node = new(kind, RegexOptions.None, [node], "", '\0', 0, 1, 1);
            var tree = new Utf8ExecutionTree(node);
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ByteSafeLinearVerifierProgram.Compile(tree));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind); Assert.Equal(9ul, failure.Requested);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(0ul, owner.Budget.Depth); Assert.Equal(17, parent.Value);
        }
    }

    [Fact]
    public void CollapsedSubprogramsAndUnusedAlternativeProbesLeaveNoArrayLoan()
    {
        var inner = new Utf8ExecutionNode(Utf8ExecutionNodeKind.Loop, RegexOptions.None, [], "", 'a', 0, 1, 2);
        var outer = new Utf8ExecutionTree(new(Utf8ExecutionNodeKind.Loop, RegexOptions.None, [inner], "", '\0', 0, 2, 2));
        foreach (var tree in new[] { outer, Tree("^(?:cat|dog)z$") })
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
            var result = Utf8ByteSafeLinearVerifierProgram.Compile(tree); Assert.True(result.Succeeded);
            Assert.Equal(112 + GraphBytes(result.Program.Steps) + ObjectBytes(owner.Budget.ObjectCheckpoint, checkpoint), owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
        }
    }

    [Fact]
    public void OptionalPrefixSharingRequiresBacktrackingAndPublicScalarResultsRemainExact()
    {
        foreach (var pattern in new[] { "^(?:a|ab)?b$", "^(?:ab|a)?b$" })
        {
            Assert.False(Utf8ByteSafeLinearVerifierProgram.Compile(Tree(pattern)).Succeeded);
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
                foreach (var subject in new[] { "b", "ab", "abb" })
                {
                    var input = Encoding.UTF8.GetBytes(subject); Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
                    var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
                    Assert.Equal((0, input.Length), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((0, input.Length), (detailed.IndexInBytes, detailed.LengthInBytes));
                }
            }
        }
        foreach (var pattern in new[] { "^(?:a|ab)$", "^(?:ab|a)$", "^(?:a|ab)?$", "^(?:ab|a)?$" })
        {
            Assert.False(Utf8ByteSafeLinearVerifierProgram.Compile(Tree(pattern)).Succeeded);
            foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
            {
                var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant);
                foreach (var subject in new[] { "a", "ab" })
                {
                    var input = Encoding.UTF8.GetBytes(subject); Assert.True(regex.IsMatch(input)); Assert.Equal(1, regex.Count(input));
                    var match = regex.Match(input); var detailed = regex.MatchDetailed(input);
                    Assert.Equal((0, input.Length), (match.IndexInBytes, match.LengthInBytes)); Assert.Equal((0, input.Length), (detailed.IndexInBytes, detailed.LengthInBytes));
                }
            }
        }
        Assert.True(Utf8ByteSafeLinearVerifierProgram.Compile(Tree("^(?:cat|dog)?z$")).Succeeded);
    }

    [Fact]
    public void AlternativeMinimumScansAreMeteredBeforeTheyRun()
    {
        var alternatives = Enumerable.Range(0, 1024).Select(static _ => "ab"u8.ToArray()).ToArray();
        var steps = new[] { Utf8ByteSafeLinearVerifierStep.MatchAnyText(alternatives) };
        var type = typeof(Utf8ByteSafeLinearVerifierProgram).GetNestedType("Compiler", BindingFlags.NonPublic); Assert.NotNull(type);
        var method = type.GetMethod("GetMinimumConsumedLength", BindingFlags.NonPublic | BindingFlags.Instance); Assert.NotNull(method);
        int Minimum()
        {
            try { return Assert.IsType<int>(method.Invoke(Activator.CreateInstance(type), [steps])); }
            catch (TargetInvocationException error) when (error.InnerException is { } inner) { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        }
        using (var funded = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { Assert.Equal(2, Minimum()); Assert.True(funded.Budget.WorkSteps >= 1024); Assert.Equal(0ul, funded.Budget.ReservedBytes); }
        using var denied = new Utf8ConstructionScope(0, ulong.MaxValue, 1023, ulong.MaxValue);
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Minimum()); Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.Equal(0ul, denied.Budget.ReservedBytes); Assert.Equal(0ul, denied.Budget.Depth); Assert.True(denied.Budget.WorkSteps <= 1023);
    }
}
