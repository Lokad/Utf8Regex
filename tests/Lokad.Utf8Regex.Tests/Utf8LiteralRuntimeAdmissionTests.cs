using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using Xunit;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8LiteralRuntimeAdmissionTests
{
    private static Utf8PreparedRegex Plan(string pattern) => (Utf8PreparedRegex)typeof(Utf8Regex)
        .GetProperty("PreparedRegex", BindingFlags.Instance | BindingFlags.NonPublic)!
        .GetValue(new Utf8Regex(pattern, options: RegexOptions.CultureInvariant))!;
    private static object? Field(object instance, string name) => instance.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(instance);
    private static Utf8LiteralCompiledEngineRuntime Create(Utf8PreparedRegex plan, bool reverse) =>
        Utf8LiteralCompiledEngineRuntime.Create(Utf8CompiledEngineSelector.Select(plan), plan, reverse);
    private static readonly string[] Patterns = ["abc", "café", "", "foo|bar", "zoo|apple|cat", "a|z", "ab|ac",
        "aaaaa|bbbbb|ccccc", "abc|def|ghi|jkl", "中文词|日本人|韩国人", "中文|日本", "中|日", "😀|😃", "éé|öü",
        "foo(?=bar)", "中(?=文)", @"\b(?:foo|bar)\b", "foo|foobar", "a0x|a1y|b0x|b1y"];
    public static IEnumerable<object[]> Shapes() => from pattern in Patterns
                                                    from reverse in new[] { false, true }
                                                    select new object[] { pattern, reverse };
    public static IEnumerable<object[]> Cutoffs() => from pattern in new[] { "abc", "foo|bar", "中|日", "foo(?=bar)" } select new object[] { pattern };
    public static IEnumerable<object[]> Depths() => from pattern in new[] { "abc", "foo|bar", "中文|日本", "foo(?=bar)" }
                                                    from depth in Enumerable.Range(0, 9)
                                                    select new object[] { pattern, depth };

    [Fact]
    public void TransferStateCoversRuntimeAndCompositeFields()
    {
        var root = typeof(Utf8LiteralCompiledEngineRuntime);
        void AssertState(Type runtime, string stateName)
        {
            var state = root.GetNestedType(stateName, BindingFlags.NonPublic)!;
            var fields = runtime.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            var stateFields = state.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            Assert.Equal(fields.Select(f => f.FieldType.FullName).Order(), stateFields.Select(f => f.FieldType.FullName).Order());
        }
        AssertState(root, "ConstructionState");
        AssertState(root.GetNestedType("CompositeTrailingLiteralSearch", BindingFlags.NonPublic)!, "CompositeConstructionState");
        Assert.Empty(root.GetConstructors(BindingFlags.Instance | BindingFlags.Public));
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void SortedFirstByteStateBorrowsOnlyEligiblePayloadsAndDeclinesBeforeAcquisition(string pattern, bool reverse)
    {
        var plan = Plan(pattern);
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue);
        var runtime = Create(plan, reverse);
        var candidates = plan.SearchPlan.AlternateLiteralsUtf8;
        var eligible = !reverse && plan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            !plan.SearchPlan.HasBoundaryRequirements && !plan.SearchPlan.HasTrailingLiteralRequirement &&
            candidates is { Length: >= 2 and <= 8 } && candidates.All(v => v.Length > 0 && v.All(b => b < 128)) &&
            candidates.Select(v => v[0]).Distinct().Count() == candidates.Length;
        var literals = Field(runtime, "_smallAsciiLiteralFamily") as byte[][];
        var search = Assert.IsType<PreparedByteSearch>(Field(runtime, "_smallAsciiLiteralFamilyFirstBytes"));
        Assert.Equal(eligible, literals is not null); Assert.Equal(eligible, search.Count > 0);
        if (eligible)
        {
            var expected = candidates!.OrderBy(v => v[0]).ToArray();
            Assert.Equal(expected.Length, literals!.Length);
            for (var index = 0; index < expected.Length; index++) Assert.Same(expected[index], literals[index]);
            Assert.Equal(expected.Select(v => v[0]).ToArray(), search.Values.ToArray());
            Assert.Equal(candidates.SequenceEqual(expected), ReferenceEquals(candidates, literals));
        }
        // Scalar/native payloads stay borrowed; an eligible emitted counter now
        // retains its independently admitted runtime code capacity.
        Assert.Equal(Field(runtime, "_emittedLiteralFamilyCounter") is not null, owner.Budget.Reserved > 0);
        Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [MemberData(nameof(Cutoffs))]
    public void ByteCutoffsPreserveFundedParents(string pattern)
    {
        var plan = Plan(pattern);         ulong peak, loan, slots;
        // Emitted IL now shares the owner. Leave that axis funded while sweeping
        // every byte cutoff; separate emission tests sweep instruction slots.
        using (var funded = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        {
            _ = funded.Budget.AllocateObject(17, static value => new Box(value)); _ = Create(plan, false);
            peak = funded.Budget.PeakBytes - 128; loan = funded.Budget.ReservedBytes;
            slots = funded.Budget.Reserved;
        }
        var successes = 0;
        for (var allowance = 0ul; allowance <= peak; allowance++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, 128 + allowance,
                ulong.MaxValue);
            var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
            var error = Record.Exception(() => _ = Create(plan, false));
            if (error is null) { successes++; Assert.Equal(loan, owner.Budget.ReservedBytes); }
            else
            {
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes,
                    Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
                Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
                Assert.Equal(0ul, owner.Budget.Reserved);
            }
            Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
            if (error is null) Assert.Equal(slots, owner.Budget.Reserved);
        }
        // Tighter byte capacity may select smaller growth extents while still
        // retaining exactly the same output.
        Assert.True(successes >= 1);
    }

    [Theory]
    [MemberData(nameof(Depths))]
    public void RootDepthDenialAndNestedFailureRestoreAllChildLoans(string pattern, int depth)
    {
        var plan = Plan(pattern); ulong required;
        using (var funded = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, ulong.MaxValue))
        { _ = Create(plan, false); required = funded.Budget.PeakDepth; }
        using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, (ulong)depth);
        var parent = owner.Budget.AllocateObject(17, static value => new Box(value)); var checkpoint = owner.Budget.ReservedBytes;
        var error = Record.Exception(() => _ = Create(plan, false));
        if ((ulong)depth < required)
        {
            Assert.Equal(Utf8ResourceLimitKind.ConstructionDepth, Assert.IsType<Utf8ResourceLimitExceededException>(error).ResourceKind);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        else Assert.Null(error);
        if (depth == 0) { Assert.Equal(128ul, owner.Budget.PeakBytes); }
        Assert.Equal(17, parent.Value); Assert.Equal(0ul, owner.Budget.Depth);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinaryAndCompiledFamiliesKeepScalarByteRangesAcrossPublicOperations(bool compiled)
    {
        foreach (var pattern in new[] { "foo|bar", "zoo|apple|cat", "中文词|日本人|韩国人", "😀|😃", "foo(?=bar)", "中(?=文)" })
        {
            var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var input = Encoding.UTF8.GetBytes("😀|foobar|zoo|apple|cat|中文词|日本人|韩国人|😃|中文");
            var ranges = new List<(int, int)>(); foreach (var match in regex.EnumerateMatches(input)) ranges.Add((match.StartOffsetInBytes, (match.EndOffsetInBytes - match.StartOffsetInBytes)));
            Assert.Equal(ranges.Count, regex.Count(input)); Assert.Equal(ranges.Count > 0, regex.IsMatch(input));
            var first = regex.Match(input); Assert.Equal(ranges[0], (first.StartOffsetInBytes, (first.EndOffsetInBytes - first.StartOffsetInBytes)));
            Assert.All(ranges, range => { Assert.NotEqual(0x80, input[range.Item1] & 0xc0); Assert.True(range.Item1 + range.Item2 == input.Length || (input[range.Item1 + range.Item2] & 0xc0) != 0x80); });
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }
    private sealed class Box(int value) { internal readonly int Value = value; }
}
