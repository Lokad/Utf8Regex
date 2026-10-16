using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReReplacementPreparationAdmissionTests
{
    private sealed class ParentValue(int value) { internal readonly int Value = value; }
    private static readonly IReadOnlyDictionary<string, int> Names =
        new Dictionary<string, int>(StringComparer.Ordinal) { ["word"] = 5, ["𝒜"] = 3, ["_é"] = 7 };

    public static IEnumerable<object[]> Shapes()
    {
        foreach (var template in new[] { "", "café😀", @"\101z\041", @"x\0\07\377",
            @"\g<0>", @"\1\2\12", @"\g<0002>\g<word>", @"\g<𝒜>\g<_é>",
            "café\\g<word>😀", "surrogate-fragments", @"\\\n\r\t\v\f\a\b\&", "a$\\101\\g<2>é" })
            yield return [template];
    }

    private static PythonReReplacementPlan Parse(string template) => PythonReReplacementParser.Parse(
        template == "surrogate-fragments" ? new string((char)0xd83d, 1) + @"\g<1>" + new string((char)0xde00, 1) : template, 12, Names);

    [Fact]
    public void PublishedTemplateStorageKeepsItsConstructionLoan()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var plan = PythonReReplacementParser.Parse("café😀\\g<1>z", 1,
            new Dictionary<string, int>(StringComparer.Ordinal));
        Assert.NotEmpty(plan.Tokens);
        Assert.True(owner.Budget.ReservedBytes > 0,
            "Published replacement tokens and literal payloads require live construction capacity.");
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
        foreach (var token in plan.Tokens)
            if (token.Kind == PythonReReplacementTokenKind.Literal)
                Assert.Equal(token.RequiredUtf8, new UTF8Encoding(false, true).GetBytes(Encoding.UTF8.GetString(token.RequiredUtf8)));
        var literal = plan.Tokens.All(t => t.Kind == PythonReReplacementTokenKind.Literal);
        Assert.Equal(literal, plan.TryGetLiteralUtf8(out var bytes));
        if (literal) Assert.Equal(plan.Tokens.SelectMany(t => t.RequiredUtf8), bytes);
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
    [InlineData("\\", "bad escape (end of pattern)", 0)]
    [InlineData(@"\q", @"bad escape \q", -1)]
    [InlineData(@"\g", "missing <", -1)]
    [InlineData(@"\g<", "missing group name", 3)]
    [InlineData(@"\g<word", "missing >, unterminated name", 3)]
    [InlineData(@"\g<>", "missing group name", 3)]
    [InlineData(@"\g<a b>", "bad character in group name 'a b'", 3)]
    [InlineData(@"\13", "invalid group reference 13", -1)]
    [InlineData(@"\g<2147483648>", "invalid group reference 2147483648", -1)]
    [InlineData(@"\g<0002147483648>", "invalid group reference 2147483648", -1)]
    [InlineData(@"\g<999999999999999999>", "invalid group reference 999999999999999999", -1)]
    [InlineData(@"\777", @"octal escape value \777 outside of range 0-0o377", -1)]
    public void GrammarErrorsKeepTheirMessagePositionAndParentState(string template, string message, int position)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var parent = owner.Budget.AllocateObject(17, static value => new ParentValue(value));
        var checkpoint = owner.Budget.ReservedBytes;
        var error = Assert.Throws<PythonRePatternException>(() => Parse(template));
        Assert.Equal(message, error.Message);
        Assert.Equal(position, error.Position);
        AssertParent(owner.Budget, checkpoint, parent);
        Assert.True(owner.Budget.WorkSteps > 2);
        using (var recovery = Utf8ConstructionScope.Enter()) { _ = Parse(""); }
        AssertParent(owner.Budget, checkpoint, parent);
    }

    [Fact]
    public void UnknownNamesKeepTheirExceptionType()
    {
        Assert.Equal("unknown group name 'absent'", Assert.Throws<IndexOutOfRangeException>(() => Parse(@"\g<absent>")).Message);
    }

    [Theory]
    [InlineData(@"\g<2147483648>")]
    [InlineData(@"\g<0002147483648>")]
    [InlineData(@"\g<999999999999999999>")]
    public void OversizedPublicReferencesAreSyntaxErrorsAndNeverRetainFailedPlans(string template)
    {
        foreach (var finite in new[] { false, true })
        {
            var policy = finite ? new Utf8ResourcePolicy { MaxWorkSteps = 100000, MaxDepth = 128,
                MaxWorkingBytes = 1048576, MaxResultBytes = 1048576 } : default;
            var regex = new Utf8PythonRegex("(😀)a", PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
            var input = "😀a"u8.ToArray();
            var retained = regex.SearchDetailedData(input);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                Assert.Throws<PythonRePatternException>(() => regex.Replace(input, template));
                Assert.Throws<PythonRePatternException>(() => regex.ReplaceToString(input, template));
                Assert.Throws<PythonRePatternException>(() => regex.SubnToString(input, template));
                Assert.Equal(0, regex.DebugReplacementPlanCacheEntryCount);
            }
            Assert.Equal("😀a", retained.Value.ValueText);
            Assert.Equal("😀", retained.Groups[1].ValueText);
            Assert.Equal("😀", regex.ReplaceToString(input, @"\g<00000000000000000000001>"));
            Assert.Equal("😀a", regex.ReplaceToString(input, @"\g<00000000000000000000000>"));
            Assert.Equal(2, regex.DebugReplacementPlanCacheEntryCount);
        }
    }

    [Theory]
    [InlineData("cat", "cat cat", "$😀", "$😀 $😀", 2)]
    [InlineData("(?P<word>😀)", "x😀y😀", @"<\g<word>\040>", "x<😀 >y<😀 >", 2)]
    [InlineData("(a)?b", "b ab", @"\g<1>é", "é aé", 2)]
    [InlineData("x*", "😀a", "", "😀a", 3)]
    public void PublicReplacementUsesCompleteUtf8FragmentsAndPythonReferences(string pattern, string subject,
        string template, string expected, int count)
        => AssertReplacement(pattern, subject, template, expected, count);

    [Fact]
    public void MalformedTemplateFragmentsAreRepairedBeforeCapturesCanJoinThem()
    {
        // Build invalid UTF-16 interop text after discovery: xUnit serializes
        // InlineData strings through UTF-8 and repairs isolated surrogate input.
        var high = new string((char)0xd83d, 1);
        var low = new string((char)0xde00, 1);
        AssertReplacement("cat", "cat cat", high, "� �", 2);
        AssertReplacement(".", "😀é", high + @"\g<0>" + low, "�😀��é�", 2);
    }

    private static void AssertReplacement(string pattern, string subject, string template, string expected, int count)
    {
        var regex = new Utf8PythonRegex(pattern);
        var input = Encoding.UTF8.GetBytes(subject);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), regex.Replace(input, template));
        Assert.Equal(expected, regex.ReplaceToString(input, template));
        var bytes = regex.Subn(input, template);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), bytes.ResultBytes);
        Assert.Equal(count, bytes.ReplacementCount);
        var text = regex.SubnToString(input, template);
        Assert.Equal(expected, text.ResultText);
        Assert.Equal(count, text.ReplacementCount);
        Assert.Equal(1, regex.DebugReplacementPlanCacheEntryCount);
    }

    [Fact]
    public void SingleLiteralPayloadIsBorrowedByTheCachedLiteralFact()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var plan = Parse("café😀");
        Assert.Single(plan.Tokens);
        Assert.True(plan.TryGetLiteralUtf8(out var first));
        Assert.True(plan.TryGetLiteralUtf8(out var second));
        Assert.Same(plan.Tokens[0].RequiredUtf8, first);
        Assert.Same(first, second);
        Assert.Equal("café😀", Encoding.UTF8.GetString(first));
    }

    private static ulong GraphBytes(PythonReReplacementPlan plan)
    {
        var arrays = new HashSet<byte[]>(ReferenceEqualityComparer.Instance);
        foreach (var token in plan.Tokens) if (token.Utf8 is not null) arrays.Add(token.Utf8);
        if (plan.LiteralUtf8 is not null) arrays.Add(plan.LiteralUtf8);
        return Utf8ConstructionBudget.ArrayBytes<PythonReReplacementToken>(plan.Tokens.Length)
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
