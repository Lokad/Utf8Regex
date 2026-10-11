using System.Text;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2ReplacementPreparationAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }

    public static IEnumerable<object[]> Shapes()
    {
        foreach (var template in new[] { "", "café😀", "$$", "$0", "$&", "${0}",
            "a$$b$0c${0}😀", "surrogate-fragments", "tail$", "𝒜$0́" })
            yield return [template];
    }

    private static string Resolve(string template) => template == "surrogate-fragments"
        ? new string((char)0xd83d, 1) + "$0" + new string((char)0xde00, 1) : template;

    private static Pcre2SimpleReplacementPlan Parse(string template)
    {
        if (!Pcre2SimpleReplacementParser.TryParse(Resolve(template), Pcre2SubstitutionOptions.None, out var plan))
            throw new InvalidOperationException("Expected a qualified simple template.");
        return plan;
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ReturnedScalarFragmentsFitMajorStorageAndConservativeHelperAdmission(string template)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var plan = Parse(template);
        Assert.True(owner.Budget.ReservedBytes >= GraphBytes(plan) + 128ul);
        Assert.True(owner.Budget.PeakBytes > owner.Budget.ReservedBytes);
        Assert.Equal(0ul, owner.Budget.Depth);
        Assert.Equal(0ul, owner.Budget.Reserved);
        foreach (var segment in plan.Segments)
            Assert.Equal(segment.LiteralUtf8, new UTF8Encoding(false, true).GetBytes(Encoding.UTF8.GetString(segment.LiteralUtf8)));
        Assert.All(plan.Segments.Where(s => s.IsWholeMatch), s => Assert.Empty(s.LiteralUtf8));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void EveryWorkCutoffPreservesParentCapacityAndSpentWork(string template)
    {
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter()) { _ = Parse(template); work = measured.Budget.WorkSteps; }
        for (var cap = 0ul; cap <= work; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, cap + 2, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Parse(template));
                if (cap < work)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, breach.ResourceKind);
                    Assert.Equal(cap + 2, breach.Limit);
                }
                else Assert.Null(failure);
                Assert.True(owner.Budget.WorkSteps <= cap + 2);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            var spent = owner.Budget.WorkSteps;
            using (var child = Utf8ConstructionScope.Enter()) { }
            Assert.Equal(spent, owner.Budget.WorkSteps);
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void ByteAndDepthDenialsPreserveParentLoansAndFundedRecovery(string template)
    {
        ulong peak, depth, recovery;
        using (var measured = Utf8ConstructionScope.Enter()) { _ = Parse(template); peak = measured.Budget.PeakBytes; depth = measured.Budget.PeakDepth; }
        using (var measured = Utf8ConstructionScope.Enter()) { _ = Parse(""); recovery = measured.Budget.PeakBytes; }
        foreach (var cap in new[] { 0ul, 1ul, 256ul, 1024ul, peak / 2, peak - 1, peak, peak + 128 }.Distinct())
        {
            using var owner = new Utf8ConstructionScope(0, 128 + cap, ulong.MaxValue, ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Parse(template));
                if (failure is not null)
                {
                    var breach = Assert.IsType<Utf8ResourceLimitExceededException>(failure);
                    Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, breach.ResourceKind);
                    Assert.Equal(128 + cap, breach.Limit);
                }
                Assert.True(owner.Budget.PeakBytes <= 128 + cap);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            if (cap >= recovery)
            {
                using (var child = Utf8ConstructionScope.Enter()) { _ = Parse(""); }
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
        for (var cap = 0ul; cap <= depth; cap++)
        {
            using var owner = new Utf8ConstructionScope(0, ulong.MaxValue, ulong.MaxValue, cap);
            var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Parse(template));
                if (cap < depth) Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            AssertParent(owner.Budget, checkpoint, parent);
            Assert.True(owner.Budget.PeakDepth <= cap);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryTokenAndDeadlineCheckpointKeepsTheSharedOwner(bool timeout)
    {
        foreach (var shape in Shapes())
        {
            var template = (string)shape[0];
            var checkpoints = 0;
            using (var measured = Utf8ConstructionScope.Enter())
            { measured.Budget.CheckpointHook = _ => checkpoints++; _ = Parse(template); }
            for (var target = 1; target <= checkpoints; target++)
            {
                using var owner = Utf8ConstructionScope.Enter();
                var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
                var checkpoint = owner.Budget.ReservedBytes;
                using var token = new CancellationTokenSource();
                long clock = 30000;
                using var clockScope = Utf8ExecutionDeadline.UseClockForDiagnostics(() => clock);
                owner.Budget.Deadline = timeout ? Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1)) : Utf8ExecutionDeadline.Infinite;
                owner.Budget.CancellationToken = token.Token;
                var calls = 0;
                owner.Budget.CheckpointHook = _ => { if (++calls == target) { if (timeout) clock++; else token.Cancel(); } };
                var failure = Record.Exception(() => Parse(template));
                if (timeout) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
                else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
                AssertParent(owner.Budget, checkpoint, parent);
                Assert.True(owner.Budget.WorkSteps >= 2);
                owner.Budget.CheckpointHook = null;
                owner.Budget.Deadline = Utf8ExecutionDeadline.Infinite;
                owner.Budget.CancellationToken = default;
                using (var recovery = Utf8ConstructionScope.Enter()) { _ = Parse(""); }
                AssertParent(owner.Budget, checkpoint, parent);
            }
        }
    }


    [Theory]
    [InlineData("", "")]
    [InlineData("café😀", "café😀")]
    [InlineData("$0", "$0")]
    [InlineData("${1}", "${1}")]
    [InlineData(@"\U𐐨", @"\U𐐨")]
    public void LiteralBypassUsesTheSameCanonicalByteAndCapacityOwner(string template, string expected)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var plan = Pcre2SimpleReplacementParser.Literal(template);
        Assert.True(owner.Budget.ReservedBytes >= GraphBytes(plan) + 128ul);
        Assert.Single(plan.Segments);
        Assert.False(plan.Segments[0].IsWholeMatch);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), plan.Segments[0].LiteralUtf8);
    }

    [Theory]
    [InlineData("café$01")]
    [InlineData("a$$b$1")]
    [InlineData("a$0b${word}")]
    [InlineData("a${0")]
    [InlineData("a${000}")]
    public void DeclinedOptimizerRestoresCapacityAndKeepsSpentWork(string template)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        Assert.False(Pcre2SimpleReplacementParser.TryParse(template, Pcre2SubstitutionOptions.None, out _));
        AssertParent(owner.Budget, checkpoint, parent);
        Assert.True(owner.Budget.WorkSteps > 2);
        using (var recovery = Utf8ConstructionScope.Enter()) { _ = Parse(""); }
        AssertParent(owner.Budget, checkpoint, parent);
    }

    [Fact]
    public void FailedCachePreparationIsEvictedAndTheSameTemplateRecovers()
    {
        var regex = new Utf8Pcre2Regex("cat");
        using (var owner = new Utf8ConstructionScope(0, ulong.MaxValue, 0, ulong.MaxValue))
        {
            Assert.Throws<Utf8ResourceLimitExceededException>(() => regex.Replace("cat"u8, "café😀$0"));
            Assert.Equal(0, regex.DebugReplacementCacheEntryCount);
            Assert.Equal(0ul, owner.Budget.ReservedBytes);
        }
        Assert.Equal("café😀cat", regex.ReplaceToString("cat"u8, "café😀$0"));
        Assert.Equal(1, regex.DebugReplacementCacheEntryCount);
    }

    [Fact]
    public void InvalidSurrogateFragmentsCannotCombineAcrossAnEmptyReference()
    {
        var template = new string((char)0xd83d, 1) + "$0" + new string((char)0xde00, 1);
        Assert.Equal("��😀��", new Utf8Pcre2Regex("").ReplaceToString("😀"u8, template));
        var literal = Pcre2SimpleReplacementParser.Literal(new string((char)0xd83d, 1));
        Assert.Equal("�"u8.ToArray(), literal.Segments[0].LiteralUtf8);
    }

    private static ulong GraphBytes(Pcre2SimpleReplacementPlan plan)
    {
        var arrays = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
        foreach (var segment in plan.Segments) arrays.Add(segment.LiteralUtf8);
        return Utf8ConstructionBudget.ArrayBytes<Pcre2SimpleReplacementSegment>(plan.Segments.Length)
            + arrays.Aggregate(0ul, (bytes, array) => bytes + Utf8ConstructionBudget.ArrayBytes<byte>(array.Length));
    }

    private static void AssertParent(Utf8ConstructionBudget budget, ulong checkpoint, ParentValue parent)
    {
        Assert.Equal(128ul, budget.ReservedBytes);
        Assert.Equal(0ul, budget.Reserved);
        Assert.Equal(0ul, budget.Depth);
        Assert.Equal(checkpoint, budget.ReservedBytes);
        Assert.Equal(17, parent.Value);
    }
}
