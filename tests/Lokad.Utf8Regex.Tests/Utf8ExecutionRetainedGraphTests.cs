using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8ExecutionRetainedGraphTests
{
    [Theory]
    [InlineData("(?<n>ab)[a-c]", false)] [InlineData("(?<n>ab)[a-c]", true)]
    [InlineData("(?<n>😀)\\k<n>", false)] [InlineData("(?<n>😀)\\k<n>", true)]
    [InlineData("[\\p{L}-[AE]]+", false)] [InlineData("[\\p{L}-[AE]]+", true)]
    public void GlobalManifestIncludesEveryExecutionReferent(string pattern, bool compiled)
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var regex = Utf8RegexCache.GetOrAdd(pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var expected = InspectExecutionGraph(Prepared(regex));
            Assert.NotEmpty(expected);
            var cacheField = typeof(Utf8RegexCache).GetField("s_cache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(cacheField);
            var cache = cacheField.GetValue(null); Assert.NotNull(cache);
            var entry = Field(cache, "_first"); Assert.NotNull(entry);
            var manifest = Assert.IsType<Utf8RetainedPayloadExtent[]>(Field(entry, "Payloads"));
            foreach (var pair in expected)
                Assert.Contains(manifest, extent => ReferenceEquals(extent.Value, pair.Key) && extent.Bytes == pair.Value);
            Utf8RegexCache.ResetForTests();
            Assert.Equal(0, Utf8RegexCache.EntryCount);
            Assert.True(regex.IsMatch("abc😀😀𐐨"u8));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Theory]
    [InlineData("literal", false)] [InlineData("literal", true)]
    [InlineData("class", false)] [InlineData("class", true)]
    [InlineData("captures", false)] [InlineData("captures", true)]
    [InlineData("assertions", false)] [InlineData("assertions", true)]
    [InlineData("rtl", false)] [InlineData("nfa", true)]
    public void TypedCollectorMatchesEveryPhysicalExecutionReferent(string kind, bool compiled)
    {
        var pattern = kind switch
        {
            "literal" => "😀𐐨",
            "class" => string.Concat(Enumerable.Repeat("[\\p{L}-", 4098)) + "[AE]" + new string(']', 4098),
            "captures" => "(?<n>ab)(?<n>c)(?<900>[a-c])",
            "assertions" => "(?<=\\p{L})(?=(?<n>.))(?<n>.)+",
            _ => "(?<n>.)+",
        };
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (kind == "rtl" ? RegexOptions.RightToLeft : kind == "nfa" ? RegexOptions.NonBacktracking : RegexOptions.None);
        var prepared = Prepared(new Utf8Regex(pattern, options));
        AssertManifest(InspectExecutionGraph(prepared), Collect(prepared.ExecutionTree, prepared.ExecutionProgram));
        if (kind == "captures")
        {
            Assert.NotNull(prepared.ExecutionProgram);
            Assert.True(prepared.ExecutionProgram.DeterministicCaptureLayout.HasValue);
            var layout = prepared.ExecutionProgram.DeterministicCaptureLayout.Value;
            Assert.Equal(4, layout.MatchLength);
            Assert.Contains(layout.Captures, static pair => pair.Key == 900 && pair.Value.Offset == 3 && pair.Value.Length == 1);
        }
    }

    [Theory]
    [InlineData(65)] [InlineData(4098)]
    public void DeepStoredNodesDoNotRequireClrRecursion(int depth)
    {
        var node = Node(Utf8ExecutionNodeKind.One, [], string.Empty);
        for (var index = 0; index < depth; index++) node = Node(Utf8ExecutionNodeKind.Group, [node], string.Empty);
        var tree = new Utf8ExecutionTree(node);
        var program = Utf8ExecutionProgramLowerer.Lower(tree); Assert.NotNull(program);
        Assert.Equal(2 * (depth + 1), program.Instructions.Count);
        var expected = InspectExecutionGraph(tree, program);
        Assert.Equal(depth + 1, expected.Keys.OfType<Utf8ExecutionNode>().Count());
        AssertManifest(expected, Collect(tree, program));
    }

    [Fact]
    public void SharedNodesArraysClassesAndEqualDistinctTextHaveTheirActualIdentityLoans()
    {
        var prepared = Utf8FrontEnd.Compile("[a-c]", RegexOptions.CultureInvariant);
        var scalarClass = Assert.Single(InspectExecutionGraph(prepared).Keys.OfType<Utf8ScalarClass>());
        var first = new string('q', 3); var second = new string('q', 3);
        Assert.NotSame(first, second);
        var leaf = Node(Utf8ExecutionNodeKind.Set, [], scalarClass);
        var children = new[] { leaf, Node(Utf8ExecutionNodeKind.Multi, [], first), Node(Utf8ExecutionNodeKind.Multi, [], second) };
        var firstGroup = Node(Utf8ExecutionNodeKind.Group, children, string.Empty);
        var secondGroup = Node(Utf8ExecutionNodeKind.Group, children, string.Empty);
        var tree = new Utf8ExecutionTree(Node(Utf8ExecutionNodeKind.Concatenate, [firstGroup, secondGroup], string.Empty));
        var program = Utf8ExecutionProgramLowerer.Lower(tree); Assert.NotNull(program);
        var manifest = Collect(tree, program);
        AssertManifest(InspectExecutionGraph(tree, program), manifest);
        Assert.Single(manifest, extent => ReferenceEquals(extent.Value, leaf));
        Assert.Single(manifest, extent => ReferenceEquals(extent.Value, children));
        Assert.Single(manifest, extent => ReferenceEquals(extent.Value, scalarClass));
        Assert.Equal(2, manifest.Count(static extent => extent.Value is string text && text == "qqq"));
    }

    [Fact]
    public void TheCompleteExecutionGraphCrossesTheExactRetainedByteBoundary()
    {
        var prepared = Utf8FrontEnd.Compile("(?<n>ab)[a-c]", RegexOptions.CultureInvariant);
        var pair = new ExecutionPair(prepared.ExecutionTree, prepared.ExecutionProgram);
        var probe = Cache(1024 * 1024);
        Assert.Equal(pair, Add(probe, "key", pair));
        var maximum = probe.PayloadBytes;
        Assert.True(maximum > 0);
        foreach (var bytes in new[] { maximum - 1, maximum })
        {
            var cache = Cache(bytes);
            Assert.Equal(pair, Add(cache, "key", pair));
            Assert.Equal(bytes == maximum ? 1 : 0, cache.Count);
            Assert.Equal(bytes == maximum ? maximum : 0, cache.PayloadBytes);
            cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes);
            AssertManifest(InspectExecutionGraph(prepared), Collect(pair.Tree, pair.Program));
        }
    }

    [Fact]
    public void TwoEntriesShareExecutionLeavesAndKeepBothPhysicalManifests()
    {
        var prepared = Utf8FrontEnd.Compile("(?<n>ab)[a-c]", RegexOptions.CultureInvariant);
        var pair = new ExecutionPair(prepared.ExecutionTree, prepared.ExecutionProgram);
        var graph = InspectExecutionGraph(prepared);
        var cache = Cache(1024 * 1024);
        Assert.Equal(pair, Add(cache, "first", pair));
        Assert.Equal(pair, Add(cache, "second", pair));
        Assert.Equal(2, cache.Count);
        var ledger = Field(cache, "_payloadLedger"); Assert.NotNull(ledger);
        var registry = Assert.IsAssignableFrom<Array>(Field(ledger, "_entries"));
        var expected = graph.Values.Aggregate(ArrayBytes(registry), static (sum, bytes) => sum + bytes);
        expected += Utf8ConstructionBudget.StringBytes(5) + Utf8ConstructionBudget.StringBytes(6);
        expected += 2 * Utf8ConstructionBudget.ArrayBytes<Utf8RetainedPayloadExtent>(graph.Count + 1);
        Assert.Equal(expected, cache.PayloadBytes);
        foreach (var item in registry)
        {
            Assert.NotNull(item);
            var reference = item.GetType().GetProperty("Value")?.GetValue(item); Assert.NotNull(reference);
            var count = Assert.IsType<int>(reference.GetType().GetProperty("Count")?.GetValue(reference));
            if (count == 0) continue;
            var identity = item.GetType().GetProperty("Key")?.GetValue(item); Assert.NotNull(identity);
            var value = Field(identity, "Value"); Assert.NotNull(value);
            Assert.Equal(graph.ContainsKey(value) ? 2 : 1, count);
        }
        cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes);
        AssertManifest(graph, Collect(pair.Tree, pair.Program));
    }

    [Fact]
    public void EveryWorkCutoffRestoresParentLoansWithoutRefundingSpentWork()
    {
        var prepared = Utf8FrontEnd.Compile("(?<n>ab)[a-c]", RegexOptions.CultureInvariant);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter())
        { _ = Collect(prepared.ExecutionTree, prepared.ExecutionProgram); work = measured.Budget.WorkSteps; }
        for (var available = 0ul; available <= work; available++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 2 + available, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Collect(prepared.ExecutionTree, prepared.ExecutionProgram));
                if (available < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, 2 + available);
        }
    }

    [Fact]
    public void ByteDenialAndPartialCancellationPreserveBorrowedGraphsAndParentLoans()
    {
        var prepared = Utf8FrontEnd.Compile("(?<n>ab)[a-c]", RegexOptions.CultureInvariant);
        var expected = InspectExecutionGraph(prepared);
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, 112, ulong.MaxValue, ulong.MaxValue))
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.Throws<Utf8ResourceLimitExceededException>(() => Collect(prepared.ExecutionTree, prepared.ExecutionProgram)).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        using var cancellation = new CancellationTokenSource();
        using (var owner = Utf8ConstructionScope.Enter())
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            owner.Budget.CancellationToken = cancellation.Token;
            owner.Budget.CheckpointHook = steps => { if (steps >= 30) cancellation.Cancel(); };
            using (var child = Utf8ConstructionScope.Enter()) Assert.Throws<OperationCanceledException>(() => Collect(prepared.ExecutionTree, prepared.ExecutionProgram));
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.True(owner.Budget.WorkSteps >= 30);
        }
        AssertManifest(expected, Collect(prepared.ExecutionTree, prepared.ExecutionProgram));
    }

    [Fact]
    public void UnknownPayloadsAndCollectionImplementationsCannotReceiveZeroByteCertificates()
    {
        var opaqueTree = new Utf8ExecutionTree(Node(Utf8ExecutionNodeKind.Empty, [], new object()));
        Assert.Throws<InvalidOperationException>(() => Collect(opaqueTree, null));
        var unknownChildren = new Utf8ExecutionTree(new Utf8ExecutionNode(Utf8ExecutionNodeKind.Group,
            RegexOptions.CultureInvariant, new List<Utf8ExecutionNode>(), string.Empty, 'a', 0, 1, 1));
        Assert.Throws<InvalidOperationException>(() => Collect(unknownChildren, null));
        var unknownInstructions = new Utf8ExecutionProgram(new List<Utf8ExecutionInstruction>());
        Assert.Throws<InvalidOperationException>(() => Collect(null, unknownInstructions));
    }

    private static Utf8ExecutionNode Node(Utf8ExecutionNodeKind kind, Utf8ExecutionNode[] children, object payload)
        => new(kind, RegexOptions.CultureInvariant, children, payload, 'a', 0, 1, 1);

    private static Utf8RetainedPayloadExtent[] Collect(Utf8ExecutionTree? tree, Utf8ExecutionProgram? program)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8RetainedPayloadBuilder<Utf8ConstructionTextNoControl>.Create(64 * 1024 * 1024, default);
        Utf8ExecutionRetainedStorage.Collect(tree, program, storage);
        Assert.False(storage.Exceeded);
        return storage.Finish();
    }

    private static void AssertManifest(Dictionary<object, ulong> expected, Utf8RetainedPayloadExtent[] manifest)
    {
        Assert.Equal(expected.Count, manifest.Length);
        foreach (var extent in manifest)
        { Assert.True(expected.TryGetValue(extent.Value, out var bytes)); Assert.Equal(bytes, extent.Bytes); }
        Assert.Equal(manifest.Length, manifest.Select(static extent => extent.Value).Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    private readonly record struct ExecutionPair(Utf8ExecutionTree? Tree, Utf8ExecutionProgram? Program);
    private static Utf8BoundedPreparationCache<string, ExecutionPair> Cache(ulong maximum)
        => Utf8BoundedPreparationCache<string, ExecutionPair>.CreateRetained(8, StringComparer.Ordinal, 64 * 1024, maximum);
    private static ExecutionPair Add(Utf8BoundedPreparationCache<string, ExecutionPair> cache, string key, ExecutionPair pair)
        => cache.GetOrAdd(key, pair, static (_, value) => value, default(Utf8ConstructionTextNoControl), default(ExecutionStorage));
    private readonly struct ExecutionStorage : IUtf8RetainedStorage<string, ExecutionPair>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public void CollectKey<TControl>(string key, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Text(key);
        public void CollectValue<TControl>(ExecutionPair value, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => Utf8ExecutionRetainedStorage.Collect(value.Tree, value.Program, storage);
    }

    internal static Utf8PreparedRegex Prepared(Utf8Regex regex)
        => Assert.IsType<Utf8RegexProgram>(Field(regex, "_program")).PreparedRegex;

    internal static Dictionary<object, ulong> InspectExecutionGraph(Utf8PreparedRegex prepared)
        => InspectExecutionGraph(prepared.ExecutionTree, prepared.ExecutionProgram);

    private static Dictionary<object, ulong> InspectExecutionGraph(Utf8ExecutionTree? tree, Utf8ExecutionProgram? program)
    {
        var seen = new Dictionary<object, ulong>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<object>();
        if (tree is not null) pending.Enqueue(tree);
        if (program is not null) pending.Enqueue(program);
        while (pending.TryDequeue(out var value))
        {
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) continue;
            if (value is string text)
            {
                var bytes = Utf8ConstructionBudget.StringBytes(text.Length);
                if (bytes != 0) seen.TryAdd(value, bytes);
                continue;
            }
            if (value is Array array)
            {
                if (array.Length == 0 || !seen.TryAdd(array, ArrayBytes(array))) continue;
                foreach (var child in array) if (child is not null) pending.Enqueue(child);
                continue;
            }
            var fields = type.GetFields(Instance);
            if (!type.IsValueType)
            {
                var (count, bytes) = value switch
                {
                    Utf8ExecutionTree => (1, 48ul),
                    Utf8ExecutionNode => (8, 80ul),
                    Utf8ExecutionProgram => (2, 64ul),
                    Utf8ScalarClass => (1, 48ul),
                    _ => throw new InvalidOperationException("Unqualified execution graph: " + type),
                };
                Assert.Equal(count, fields.Length);
                Assert.All(fields, static field => Assert.True(field.IsInitOnly));
                if (!seen.TryAdd(value, bytes)) continue;
            }
            foreach (var field in fields)
                if (field.GetValue(value) is { } child) pending.Enqueue(child);
        }
        return seen;
    }

    private static ulong ArrayBytes(Array array)
    {
        var method = typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ArrayBytes), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ulong>(method.MakeGenericMethod(array.GetType().GetElementType()).Invoke(null, [array.Length]));
    }

    private static object? Field(object value, string name)
    {
        var field = value.GetType().GetField(name, Instance); Assert.NotNull(field);
        return field.GetValue(value);
    }

    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
}
