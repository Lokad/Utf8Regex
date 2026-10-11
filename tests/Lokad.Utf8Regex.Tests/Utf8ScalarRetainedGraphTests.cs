using System.Reflection;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8ScalarRetainedGraphTests
{
    [Theory]
    [InlineData("(?<n>😀)\\k<n>", false)]
    [InlineData("(?<900>.)", true)]
    [InlineData("(?<=\\p{L})[\\p{L}-[AE]]", false)]
    [InlineData("(?=(?=(?<n>.))).", true)]
    public void GlobalChargeCoversEveryOwnedScalarReferent(string pattern, bool compiled)
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var regex = Utf8RegexCache.GetOrAdd(pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var expected = InspectScalarGraph(ScalarProgram(regex));
            var cache = GlobalCache();
            var entry = Field(cache, "_first");
            Assert.NotNull(entry);
            var manifest = Utf8GlobalCacheDescendantTests.InspectEntryPayloads(entry);
            var actual = manifest.ToDictionary(static e => e.Value, static e => e.Bytes, ReferenceEqualityComparer.Instance);
            foreach (var pair in expected)
            {
                Assert.True(actual.TryGetValue(pair.Key, out var extent), "Missing scalar referent: " + pair.Key.GetType());
                Assert.Equal(pair.Value, extent);
            }
            Assert.True(regex.IsMatch("😀😀a𐐨"u8));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Theory]
    [InlineData("literal", false)] [InlineData("literal", true)]
    [InlineData("assertions", false)] [InlineData("assertions", true)]
    [InlineData("class", false)] [InlineData("class", true)]
    [InlineData("captures", false)] [InlineData("captures", true)]
    [InlineData("rtl", false)] [InlineData("nfa", true)]
    public void TypedCollectorExactlyMatchesTheIndependentPhysicalGraph(string kind, bool compiled)
    {
        var pattern = kind switch
        {
            "literal" => "😀𐐨",
            "assertions" => string.Concat(Enumerable.Repeat("(?=", 65)) + "." + new string(')', 65),
            "class" => string.Concat(Enumerable.Repeat("[\\p{L}-", 4098)) + "[AE]" + new string(']', 4098),
            "captures" => "(?<n>😀)(?<n>.)(?<900>.)\\k<n>",
            _ => "(?<n>.)+",
        };
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (kind is "rtl" or "assertions" ? RegexOptions.RightToLeft : kind == "nfa" ? RegexOptions.NonBacktracking : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        var program = ScalarProgram(regex);
        var expected = InspectScalarGraph(program);
        if (kind == "assertions") Assert.Equal(2, expected.Keys.OfType<Utf8SliceProgram>().Count());
        AssertManifest(expected, Collect(program));
    }

    [Theory]
    [InlineData(65)] [InlineData(4098)]
    public void NestedStoredAssertionsWalkWithoutClrRecursion(int depth)
    {
        var program = Utf8SliceParser.TryParse(".", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        for (var index = 0; index < depth; index++)
        {
            var assertion = Utf8SliceAssertion.Create(program, 1, 4, true);
            program = Copy(program, [assertion], program.GroupNames, program.BackreferenceLowerings);
        }
        var expected = InspectScalarGraph(program);
        Assert.Equal(depth + 1, expected.Keys.OfType<Utf8SliceProgram>().Count());
        Assert.Equal(depth, expected.Keys.OfType<Utf8SliceAssertion>().Count());
        AssertManifest(expected, Collect(program));
    }

    [Fact]
    public void SharedAssertionBodiesAndCatalogsHaveOnePhysicalLoan()
    {
        var program = Utf8SliceParser.TryParse("a", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var assertion = Utf8SliceAssertion.Create(program, 1, 1, false);
        var repeated = Copy(program, Enumerable.Repeat(assertion, 256).ToArray(), program.GroupNames, program.BackreferenceLowerings);
        var manifest = Collect(repeated);
        AssertManifest(InspectScalarGraph(repeated), manifest);
        Assert.Single(manifest, extent => ReferenceEquals(extent.Value, assertion));
        Assert.Single(manifest, extent => ReferenceEquals(extent.Value, assertion.Body));
    }

    [Fact]
    public void EqualTextInDistinctObjectsRetainsDistinctLoans()
    {
        var program = Utf8SliceParser.TryParse("(.)", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var names = new[] { new string('q', 3), new string('q', 3) };
        Assert.NotSame(names[0], names[1]);
        var copied = Copy(program, program.Assertions, names, program.BackreferenceLowerings);
        var manifest = Collect(copied);
        AssertManifest(InspectScalarGraph(copied), manifest);
        Assert.Equal(2, manifest.Count(static extent => extent.Value is string text && text == "qqq"));
    }

    [Fact]
    public void ACompleteScalarGraphCrossesTheExactCacheByteBoundary()
    {
        var program = Utf8SliceParser.TryParse("(?<n>😀)[\\p{L}-[AE]]", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var probe = Cache(1024 * 1024);
        Assert.Same(program, Add(probe, "key", program));
        var maximum = probe.PayloadBytes;
        Assert.True(maximum > 0);
        foreach (var bytes in new[] { maximum - 1, maximum })
        {
            var cache = Cache(bytes);
            Assert.Same(program, Add(cache, "key", program));
            Assert.Equal(bytes == maximum ? 1 : 0, cache.Count);
            Assert.Equal(bytes == maximum ? maximum : 0, cache.PayloadBytes);
            cache.Clear();
            Assert.Equal(0ul, cache.PayloadBytes);
            AssertManifest(InspectScalarGraph(program), Collect(program));
        }
    }

    [Fact]
    public void TwoEntriesConservativelyChargeTheirSharedGraphTwice()
    {
        var program = Utf8SliceParser.TryParse("(?<n>.)", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var graph = InspectScalarGraph(program);
        var cache = Cache(1024 * 1024);
        Assert.Same(program, Add(cache, "first", program));
        Assert.Same(program, Add(cache, "second", program));
        Assert.Equal(2, cache.Count);
        var expected = 2 * graph.Values.Aggregate(0UL, static (sum, bytes) => sum + bytes);
        expected += Utf8ConstructionBudget.StringBytes(5) + Utf8ConstructionBudget.StringBytes(6);
        Assert.Equal(expected, cache.PayloadBytes);
        cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes);
        AssertManifest(graph, Collect(program));
    }

    [Fact]
    public void EveryCollectorWorkCutoffRestoresParentLoansWithoutRefundingWork()
    {
        var program = Utf8SliceParser.TryParse("(?<n>😀)[\\p{L}-[AE]]", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter()) { _ = Collect(program); work = measured.Budget.WorkSteps; }
        for (var available = 0ul; available <= work; available++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 2 + available, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Collect(program));
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
    public void ByteDenialAndCanceledWalkPreserveTheBorrowedGraphAndParentLoan()
    {
        var program = Utf8SliceParser.TryParse("(?<n>.)", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var expected = InspectScalarGraph(program);
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, 112, ulong.MaxValue, ulong.MaxValue))
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.Throws<Utf8ResourceLimitExceededException>(() => Collect(program)).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        using var cancellation = new CancellationTokenSource();
        using (var owner = Utf8ConstructionScope.Enter())
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ObjectCheckpoint;
            owner.Budget.CancellationToken = cancellation.Token;
            owner.Budget.CheckpointHook = steps => { if (steps >= 30) cancellation.Cancel(); };
            using (var child = Utf8ConstructionScope.Enter()) Assert.Throws<OperationCanceledException>(() => Collect(program));
            Assert.Equal(112ul, owner.Budget.ReservedBytes);
            Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.True(owner.Budget.WorkSteps >= 30);
        }
        AssertManifest(expected, Collect(program));
    }

    [Fact]
    public void ProfileDelegatesCannotReceiveAnOpaqueZeroByteCertificate()
    {
        var program = Utf8SliceParser.TryParse("a", RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        var copied = Copy(program, program.Assertions, program.GroupNames, [static scalar => scalar]);
        Assert.Throws<InvalidOperationException>(() => Collect(copied));
    }

    [Fact]
    public void OwnedComparersCannotReceiveAnOpaqueZeroByteCertificate()
    {
        var map = new Utf8ConstructionReadOnlyMap<string, int>([], new OwnedComparer(), 0);
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8RetainedPayloadBuilder<Utf8ConstructionTextNoControl>.Create(1024, default);
        Assert.Throws<InvalidOperationException>(() => map.CollectStorage(storage));
    }

    private sealed class OwnedComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => x == y;
        public int GetHashCode(string value) => value.GetHashCode();
    }

    private static Utf8RetainedPayloadExtent[] Collect(Utf8SliceProgram program)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8RetainedPayloadBuilder<Utf8ConstructionTextNoControl>.Create(64 * 1024 * 1024, default);
        Utf8ScalarProgramRetainedStorage.Collect(program, storage);
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

    private static Utf8BoundedPreparationCache<string, Utf8SliceProgram> Cache(ulong maximum)
        => Utf8BoundedPreparationCache<string, Utf8SliceProgram>.CreateRetained(8, StringComparer.Ordinal, 64 * 1024, maximum);
    private static Utf8SliceProgram Add(Utf8BoundedPreparationCache<string, Utf8SliceProgram> cache, string key, Utf8SliceProgram program)
        => cache.GetOrAdd(key, program, static (_, value) => value, default(Utf8ConstructionTextNoControl), default(ScalarStorage));
    private readonly struct ScalarStorage : IUtf8RetainedStorage<string, Utf8SliceProgram>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public void CollectKey<TControl>(string key, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Text(key);
        public void CollectValue<TControl>(Utf8SliceProgram value, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => Utf8ScalarProgramRetainedStorage.Collect(value, storage);
    }

    private static Utf8SliceProgram Copy(Utf8SliceProgram body, Utf8SliceAssertion[] assertions, string[] names, Func<int, int>[] lowerings)
        => Utf8SliceProgram.Create(body.Instructions, body.SetMasks, body.CategoryTables, body.CategorySetMembers,
            body.RepeatCount, body.GroupCount, body.RequiresUnicodeWordBoundary, body.RequiresUnicodeShorthand,
            body.RequiresUnicodeCategory, assertions, body.HasNonFixedLookbehind, names, body.HasMixedNamedGroups,
            body.HasBalancing, body.IgnoreCase, body.HasContiguousAnchor, body.HasConditionalTest, body.RightToLeft,
            body.SawLookaround, body.SawCapturingLookbehind, body.CanMatchEmpty, body.CaptureCatalog,
            body.UsesInterpretedLazyLoopSemantics, body.UsesLiteralClassBracketSyntax, body.CaseBehavior,
            body.IsNonBacktracking, body.NonBacktrackingCaptureOpeningCount, body.NonBacktrackingEpsilonOrder, lowerings);

    internal static Dictionary<object, ulong> InspectScalarGraph(Utf8SliceProgram program)
    {
        var seen = new Dictionary<object, ulong>(ReferenceEqualityComparer.Instance);
        var queue = new Queue<object>();
        queue.Enqueue(program);
        while (queue.TryDequeue(out var value))
        {
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) continue;
            if (ReferenceEquals(value, StringComparer.Ordinal) || ReferenceEquals(value, EqualityComparer<int>.Default)) continue;
            if (value is string text)
            {
                var bytes = Utf8ConstructionBudget.StringBytes(text.Length);
                if (bytes != 0) seen.TryAdd(value, bytes);
                continue;
            }
            if (value is Array array)
            {
                if (array.Length == 0 || !seen.TryAdd(value, ArrayBytes(array))) continue;
                foreach (var item in array) if (item is not null) queue.Enqueue(item);
                continue;
            }
            var fields = type.GetFields(Instance);
            if (!type.IsValueType)
            {
                var (count, bytes) = value switch
                {
                    Utf8SliceProgram => (33, 144ul),
                    Utf8SliceAssertion => (4, 64ul),
                    Utf8SliceCategoryTable => (3, 64ul),
                    Utf8SliceNode.CategorySetMember => (2, 48ul),
                    Utf8SliceClassProgram => (1, 48ul),
                    Utf8CaptureCatalog => (7, 80ul),
                    Utf8ConstructionReadOnlyMap<string, int> => (3, 64ul),
                    Utf8ConstructionReadOnlyMap<int, int> => (3, 64ul),
                    _ => throw new InvalidOperationException("Unqualified scalar graph type: " + type),
                };
                Assert.Equal(count, fields.Length);
                if (!seen.TryAdd(value, bytes)) continue;
            }
            foreach (var field in fields)
                if (field.GetValue(value) is { } child) queue.Enqueue(child);
        }
        return seen;
    }

    internal static Utf8SliceProgram ScalarProgram(Utf8Regex regex)
        => Assert.IsType<Utf8SliceProgram>(Field(regex, "_ownedSliceProgram"));

    private static ulong ArrayBytes(Array value)
    {
        var method = typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ArrayBytes), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ulong>(method.MakeGenericMethod(value.GetType().GetElementType()).Invoke(null, [value.Length]));
    }

    private static object? Field(object value, string name)
    {
        var field = value.GetType().GetField(name, Instance);
        Assert.NotNull(field);
        return field.GetValue(value);
    }

    private static object GlobalCache()
    {
        var field = typeof(Utf8RegexCache).GetField("s_cache", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(field);
        var result = field.GetValue(null);
        Assert.NotNull(result);
        return result;
    }

    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
}
