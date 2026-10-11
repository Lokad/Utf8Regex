using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RequiredLiteralAdmissionTests
{
    [Fact]
    public void WholeFamilyCertificatesRecoverEveryWorkCutoffAndDeclineWithoutRetainedLoans()
    {
        foreach (var pattern in new[] { "[ab](foo|bar)|[cd](foo|bar)", "[ab](foo|bar)|[cd](foo|baz)", "(?i:😀)x", "😀|🙏" })
        {
            var tree = Utf8OwnedTreeParser.TryParse(pattern, RegexOptions.CultureInvariant);
            Assert.NotNull(tree);
            string[]? expected;
            ulong work;
            using (var measured = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
            {
                expected = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(tree.Root);
                work = measured.Budget.WorkSteps;
            }
            Assert.InRange(work, 1ul, 10_000ul);
            for (ulong cutoff = 0; cutoff <= work; cutoff++)
            {
                using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                var checkpoint = owner.Budget.ReservedBytes;
                Exception? failure;
                using (var child = Utf8ConstructionScope.Enter())
                    failure = Record.Exception(() => Assert.Equal(expected, Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(tree.Root)));
                if (cutoff == work) Assert.Null(failure);
                else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Depth);
                Assert.Equal(0ul, owner.Budget.Reserved);
                Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.Equal(17, parent.Value);
                Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            }
        }
    }

    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    private static Utf8TreeNode Node(Utf8TreeNodeKind kind, string text, params Utf8TreeNode[] children)
    {
        var node = Utf8TreeNode.Create(kind, RegexOptions.None, text);
        foreach (var child in children) node.WithChild(child);
        return node;
    }

    private static Utf8TreeNode Make(int route, int depth)
    {
        var first = Node(Utf8TreeNodeKind.Multi, "shared");
        var second = Node(Utf8TreeNodeKind.Multi, route == 2 ? "shared" : "other");
        for (var index = 0; index < depth; index++)
        {
            first = Node(Utf8TreeNodeKind.Group, "", first);
            second = Node(Utf8TreeNodeKind.Group, "", second);
        }
        return route switch
        {
            0 => first,
            1 => Node(Utf8TreeNodeKind.Alternate, "", first, second),
            2 => Node(Utf8TreeNodeKind.Alternate, "",
                Node(Utf8TreeNodeKind.Concatenate, "", first, Node(Utf8TreeNodeKind.Set, "opaque")),
                Node(Utf8TreeNodeKind.Concatenate, "", second, Node(Utf8TreeNodeKind.Set, "opaque"))),
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
    }

    private static string[] Expected(int route) => route == 1 ? ["other", "shared"] : ["shared"];

    [Theory]
    [InlineData(0, 0)] [InlineData(0, 1)] [InlineData(0, 2)]
    [InlineData(1, 0)] [InlineData(1, 1)] [InlineData(1, 2)]
    [InlineData(2, 0)] [InlineData(2, 1)] [InlineData(2, 2)]
    public void RootAdmissionPrecedesCollectorsAndPreservesParent(int route, int denial)
    {
        var input = Make(route, 0);
        using var owner = new Utf8ConstructionScope(0, denial == 0 ? 128ul : ulong.MaxValue,
            denial == 2 ? 2ul : ulong.MaxValue, denial == 1 ? 0ul : ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input));
        Assert.Equal(denial switch
        {
            0 => Utf8ResourceLimitKind.ConstructionBytes,
            1 => Utf8ResourceLimitKind.ConstructionDepth,
            _ => Utf8ResourceLimitKind.ConstructionWorkSteps,
        }, failure.ResourceKind);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(128ul, owner.Budget.PeakBytes);
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(2ul, owner.Budget.WorkSteps);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ChildDepthDenialAndRecoveryRestoreAllLoans(int route)
    {
        var input = Make(route, 32);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, 8);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, failure.ResourceKind);
        Assert.Equal(9ul, failure.Requested);
        Assert.Equal(8ul, owner.Budget.PeakDepth);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        var work = owner.Budget.WorkSteps;
        using (var child = Utf8ConstructionScope.Enter())
            Assert.Equal(Expected(route), Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(Make(route, 0)));
        Assert.True(owner.Budget.WorkSteps > work);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ArrayGrowthDenialReleasesScratchAndPayloadAcquisitions(int route)
    {
        var input = Make(route, 128);
        using var owner = new Utf8ConstructionScope(0, 2048, ulong.MaxValue, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
        Assert.True(failure.Requested > 2048);
        Assert.InRange(owner.Budget.PeakBytes, 113ul, 2048ul);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryWorkCutoffRestoresParentWithoutRefundingWork(int route)
    {
        var input = Make(route, 3);
        ulong work;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Equal(Expected(route), Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input));
            work = measure.Budget.WorkSteps;
        }
        Assert.InRange(work, 1ul, 10000ul);
        for (var cutoff = 0ul; cutoff <= work; cutoff++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cutoff + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter())
                failure = Record.Exception(() => Assert.Equal(Expected(route), Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input)));
            if (cutoff == work) Assert.Null(failure);
            else Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, cutoff + 2);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void EveryByteCutoffAccountsForTemporaryAndReturnedStorage(int route)
    {
        var input = Make(route, 3);
        ulong peak;
        using (var measure = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            Assert.Equal(Expected(route), Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input));
            peak = measure.Budget.PeakBytes;
        }
        Assert.InRange(peak, 1ul, 20000ul);
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(0, 128 + allowance, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            Exception? failure;
            using (var child = Utf8ConstructionScope.Enter())
                failure = Record.Exception(() => Assert.Equal(Expected(route), Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input)));
            if (allowance == peak) Assert.Null(failure);
            if (failure is not null) Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
            Assert.True(owner.Budget.PeakBytes <= 128 + allowance);
            Assert.Equal(128ul, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(17, parent.Value);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OutputTransfersArrayAndAcquiredTextWhileBorrowedTextStaysBorrowed(bool one)
    {
        var text = new string('x', 1000);
        var input = one ? Utf8TreeNode.Create(Utf8TreeNodeKind.One, RegexOptions.None, 'x') : Node(Utf8TreeNodeKind.Multi, text);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var result = Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input);
        Assert.NotNull(result);
        Assert.Equal(one ? "x" : text, Assert.Single(result));
        if (!one) Assert.Same(text, result[0]);
        Assert.True(owner.Budget.ReservedBytes >= Utf8ConstructionBudget.ArrayBytes<string>(1) +
            (one ? Utf8ConstructionBudget.StringBytes(result[0].Length) : 0ul));
        Assert.True(owner.Budget.ReservedBytes >= Utf8ConstructionBudget.ArrayBytes<string>(1));
        Assert.Equal(0ul, owner.Budget.Reserved);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Fact]
    public void DeclinedFamiliesRestoreScratchWhileKeepingWorkSpent()
    {
        var input = Node(Utf8TreeNodeKind.Alternate, "", Node(Utf8TreeNodeKind.One, ""), Node(Utf8TreeNodeKind.Set, "opaque"));
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        Assert.Null(Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input));
        Assert.Equal(0ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);

        Assert.True(owner.Budget.WorkSteps > 0);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void LongComparisonAndHashingAdmitTextWorkBeforeScanning(bool dirty)
    {
        var prefix = new string('x', 1_000_000);
        var left = Node(Utf8TreeNodeKind.Multi, prefix + 'a');
        var right = Node(Utf8TreeNodeKind.Multi, prefix + 'b');
        if (dirty)
        {
            left = Node(Utf8TreeNodeKind.Concatenate, "", left, Node(Utf8TreeNodeKind.Set, "opaque"));
            right = Node(Utf8TreeNodeKind.Concatenate, "", right, Node(Utf8TreeNodeKind.Set, "opaque"));
        }
        var input = Node(Utf8TreeNodeKind.Alternate, "", left, right);
        using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 100_000, ulong.MaxValue);
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => Utf8OwnedRequiredLiterals.FindBestRequiredLiteralFamily(input));
        Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, failure.ResourceKind);
        Assert.True(failure.Requested >= 1_000_000);
        Assert.Equal(128ul, owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }
}
