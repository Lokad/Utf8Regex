using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ByteSafePrefixScalarTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static Utf8ExecutionTree Tree(string pattern)
    {
        var tree = Utf8ExecutionTreeLowerer.Lower(Utf8FrontEnd.Compile(pattern, RegexOptions.CultureInvariant).SemanticRegex);
        Assert.NotNull(tree); return tree;
    }
    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, string text = "", params Utf8ExecutionNode[] children)
        => new(kind, RegexOptions.CultureInvariant, children, text, '\0', 0, 1, 1);
    private static object? Call(string name, params object?[] args)
    {
        var type = typeof(Utf8ByteSafeLinearVerifierProgram).GetNestedType("Compiler", BindingFlags.NonPublic);
        Assert.NotNull(type);
        var method = type.GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance); Assert.NotNull(method);
        try { return method.Invoke(Activator.CreateInstance(type, nonPublic: true), args); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
    }

    [Fact]
    public void NullableSuffixCannotCertifyDisjointGreedyLoop()
    {
        var unsafeProgram = Utf8ByteSafeLinearVerifierProgram.Compile(Tree("^a*(?:b)?a$"));
        Assert.False(unsafeProgram.Succeeded);
        Assert.True(Utf8ByteSafeLinearVerifierProgram.Compile(Tree("^a*(?:b)?c$")).Succeeded);
        Assert.True(Utf8ByteSafeLinearVerifierProgram.Compile(Tree("^a*b?$")).Succeeded);
        foreach (var options in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft, RegexOptions.RightToLeft | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex("^a*(?:b)?a$", options | RegexOptions.CultureInvariant);
            Assert.True(regex.IsMatch("aaa"u8)); Assert.Equal(1, regex.Count("aaa"u8));
            var match = regex.Match("aaa"u8); Assert.True(match.Success); Assert.Equal(0, match.IndexInBytes); Assert.Equal(3, match.LengthInBytes);
            Assert.False(regex.IsMatch("aaac"u8));
        }
    }

    [Fact]
    public void FixedTextRequiresExactLexicalAsciiAndAdmitsItsPayload()
    {
        foreach (var root in new[] {
            new Utf8ExecutionNode(Utf8ExecutionNodeKind.Multi, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, [], "cat", '\0', 0, 1, 1),
            Node(Utf8ExecutionNodeKind.Multi, "ab😀"), Node(Utf8ExecutionNodeKind.Multi, "ab\ud800") })
        {
            object?[] args = [root, null]; Assert.False(Assert.IsType<bool>(Call("TryExtractFixedAsciiText", args)));
            Assert.Empty(Assert.IsType<byte[]>(args[1]));
        }
        var exact = Node(Utf8ExecutionNodeKind.Group, "", Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Multi, "ab"), Node(Utf8ExecutionNodeKind.Multi, "cd")));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        object?[] complete = [exact, null]; Assert.True(Assert.IsType<bool>(Call("TryExtractFixedAsciiText", complete)));
        var payload = Assert.IsType<byte[]>(complete[1]); Assert.Equal("abcd"u8.ToArray(), payload);
        var reserved = owner.Budget.ReservedBytes;
        Assert.True(reserved >= Utf8ConstructionBudget.ArrayBytes<byte>(payload.Length));
        owner.Budget.ReleaseArray(payload, false);
        Assert.Equal(reserved - Utf8ConstructionBudget.ArrayBytes<byte>(payload.Length), owner.Budget.ReservedBytes);
    }

    [Fact]
    public void FirstMasksCoverOptionalChildrenAndDeclineFailedOrWideFibers()
    {
        object?[] optional = [Tree("(?:b)?c").Root, 0ul, 0ul];
        Assert.True(Assert.IsType<bool>(Call("TryGetLeadingAsciiByteMask", optional)));
        Assert.Equal(0ul, optional[1]); Assert.Equal((1ul << ('b' - 64)) | (1ul << ('c' - 64)), optional[2]);
        foreach (var root in new[] { Node(Utf8ExecutionNodeKind.Alternate, "", Node(Utf8ExecutionNodeKind.Multi, "cat"), Node(Utf8ExecutionNodeKind.Unsupported)), Tree("(?i:k)c").Root, Tree("[aé]c").Root })
        {
            object?[] args = [root, 0ul, 0ul]; Assert.False(Assert.IsType<bool>(Call("TryGetLeadingAsciiByteMask", args)));
            Assert.Equal(0ul, args[1]); Assert.Equal(0ul, args[2]);
        }
        var wide = Tree("\\s+c").Root;
        Assert.False(Utf8ExecutionPrefixFacts.FirstMask(wide, out _));
        object?[] asciiReplay = [wide, 0ul, 0ul];
        Assert.True(Assert.IsType<bool>(Call("TryGetLeadingAsciiByteMask", asciiReplay)));
        Assert.Equal(0x100003e00ul, asciiReplay[1]); Assert.Equal(0ul, asciiReplay[2]);
    }

    [Fact]
    public void ActualWrappersAndEveryWorkCutoffPreserveParentLoans()
    {
        var deep = Node(Utf8ExecutionNodeKind.Multi, "cat");
        for (var index = 0; index < 65; index++) deep = Node(Utf8ExecutionNodeKind.Group, "", deep);
        var compiledRoot = new Utf8ExecutionTree(Node(Utf8ExecutionNodeKind.Loop, "", deep));
        using (var denied = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Call("TryExtractFixedAsciiText", deep, null));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(0ul, denied.Budget.ReservedBytes); Assert.Equal(0ul, denied.Budget.Depth);
        }
        using (var denied = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8))
        {
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8ByteSafeLinearVerifierProgram.Compile(compiledRoot));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
            Assert.Equal(0ul, denied.Budget.ReservedBytes); Assert.Equal(0ul, denied.Budget.Depth);
        }
        var text = Node(Utf8ExecutionNodeKind.Concatenate, "", Node(Utf8ExecutionNodeKind.Group, "", Node(Utf8ExecutionNodeKind.Multi, "ab")), Node(Utf8ExecutionNodeKind.Multi, "cd"));
        var alternatives = Node(Utf8ExecutionNodeKind.Alternate, "", text, Node(Utf8ExecutionNodeKind.Multi, "xy"));
        var following = new[] { Node(Utf8ExecutionNodeKind.Empty), text };
        foreach (var route in new[] { 0, 1, 2 })
        {
            void Run()
            {
                using var discarded = Utf8ConstructionScope.Enter();
                Assert.True(Assert.IsType<bool>(route switch {
                    0 => Call("TryExtractFixedAsciiText", text, null),
                    1 => Call("TryExtractAlternationTexts", alternatives, null, false),
                    _ => Call("TryGetFollowingAsciiByteMask", following, 0, 0ul, 0ul) }));
            }
            ulong work;
            using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            { Run(); work = measure.Budget.WorkSteps; Assert.Equal(0ul, measure.Budget.ReservedBytes); }
            Assert.InRange(work, 1ul, 10000ul);
            for (var cutoff = 0ul; cutoff <= work; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value)); var checkpoint = owner.Budget.ObjectCheckpoint;
                var failure = Record.Exception(Run);
                if (cutoff < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
                Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint); Assert.Equal(17, parent.Value);
                Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            }
        }
    }

    [Fact]
    public void AcceptedLinearProgramsAgreeWithIndependentScalarVmAtEveryBoundary()
    {
        var patterns = new[] { "^a*(?:b)?a$", "^a*(?:b)?c$", "^a*b?$", "^ab(?:cd|ef)?gh$", "^ab(?:cd|ef){2}$", "a*(?:b|c)d", "a*bc", "[ab]+c", "^a*$", "ab[0-9]{2}", "\\bab\\b", "(?:cat|dog)z", "ab(?:c|de)?f", "(?i:k)ab", "a[é😀]b", "a[aé]b" };
        var subjects = new[] { "", "aaa", "aaaba", "aaac", "aac", "abc", "abefgh", "abgh", "abcdcd", "aaabcd", "bbbc", "ab12", " ab ", "catz", "dogz", "abdef", "abf", "😀ab12", "Kab", "a😀b" }.Select(Encoding.UTF8.GetBytes).ToArray();
        var accepted = 0; var matches = 0; var fallbacks = 0;
        foreach (var pattern in patterns)
        {
            var linear = Utf8ByteSafeLinearVerifierProgram.Compile(Tree(pattern)); if (!linear.Succeeded) continue; accepted++;
            var owned = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant); Assert.NotNull(owned);
            var program = Utf8OwnedTreeSliceLowerer.TryLower(owned, RegexOptions.CultureInvariant); Assert.NotNull(program);
            var starts = new int[program.GroupCount]; var ends = new int[program.GroupCount];
            foreach (var subject in subjects)
            for (var boundary = 0; boundary <= subject.Length; boundary++)
            {
                if (boundary < subject.Length && (subject[boundary] & 0xc0) == 0x80) continue;
                var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(100000, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
                var expected = Utf8SliceRunner.TryMatchAt(program, subject, boundary, starts, ends, true, ref invocation);
                Assert.Equal(Utf8ExecutionLimitKind.None, expected.Breach);
                var actual = linear.Program.TryMatch(subject, boundary, out var length, out var fallback);
                if (fallback) { fallbacks++; Assert.True(subject.Skip(boundary).Any(static value => value > 127)); continue; }
                Assert.True(expected.IsMatch == actual, $"{pattern} / {Encoding.UTF8.GetString(subject)} / {boundary}");
                if (actual) { matches++; Assert.Equal(expected.EndIndex - boundary, length); }
            }
        }
        Assert.True(accepted >= 8); Assert.True(matches >= 20); Assert.True(fallbacks >= 1);
    }
}
