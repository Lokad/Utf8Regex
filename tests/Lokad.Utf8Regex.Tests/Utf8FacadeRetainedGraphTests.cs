using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics.X86;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8FacadeRetainedGraphTests
{
    [Theory]
    [InlineData("(?<n>abc)", false)] [InlineData("(?<n>abc)", true)]
    [InlineData("(?<a>ab)c(?<b>d)?", false)] [InlineData("(?<a>ab)c(?<b>d)?", true)]
    [InlineData("(ab(c)|x(yz))", false)] [InlineData("(ab(c)|x(yz))", true)]
    [InlineData("(?i:Шерлок Холмс|Джон Уотсон|Ирен Адлер)", false)]
    [InlineData("(?i:Шерлок Холмс|Джон Уотсон|Ирен Адлер)", true)]
    [InlineData("夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", false)]
    [InlineData("夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", true)]
    public void GlobalManifestIncludesProgramMetadataAndEveryFacadeHelperReferent(string pattern, bool compiled)
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            if (pattern.StartsWith("(?i:", StringComparison.Ordinal)) options |= RegexOptions.IgnoreCase;
            var regex = Utf8RegexCache.GetOrAdd(pattern, options);
            var expected = InspectFacadeGraph(regex);
            Assert.Contains(expected.Keys, static value => value is Utf8RegexProgram);
            var field = typeof(Utf8RegexCache).GetField("s_cache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var cache = field.GetValue(null); Assert.NotNull(cache);
            var entry = Field(cache, "_first"); Assert.NotNull(entry);
            var manifest = Assert.IsType<Utf8RetainedPayloadExtent[]>(Field(entry, "Payloads"));
            foreach (var pair in expected)
                Assert.Contains(manifest, extent => ReferenceEquals(extent.Value, pair.Key) && extent.Bytes == pair.Value);
            Assert.Same(regex, Utf8RegexCache.GetOrAdd(pattern, regex.Options));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ActualHelperFamiliesHaveCompleteTypedGraphsAndQualifiedMetadata(bool compiled)
    {
        var types = new HashSet<Type>();
        foreach (var (pattern, options) in new[] {
            ("(?<n>abc)", RegexOptions.None), ("(?<a>ab)c(?<b>d)?", RegexOptions.None),
            ("(ab(c)|x(yz))", RegexOptions.None), ("^(?<n>😀abc)", RegexOptions.None),
            ("Шерлок Холмс|Джон Уотсон|Ирен Адлер", RegexOptions.IgnoreCase),
            ("夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", RegexOptions.None) })
        {
            var regex = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var graph = InspectFacadeGraph(regex);
            types.UnionWith(graph.Keys.Select(static value => value.GetType()));
            AssertManifest(graph, Collect(regex));
            var program = Assert.IsType<Utf8RegexProgram>(Field(regex, "_program"));
            Assert.Same(program.PreparedRegex.ScalarProgram, Field(regex, "_ownedSliceProgram"));
            Assert.Same(program.PreparedRegex.SemanticRegex.CaptureCatalog, program.CaptureCatalog);
        }
        foreach (var type in HelperTypes)
        {
            if (type == typeof(Utf8PackedNibbleLiteralFamilyCountStrategy) && !(Sse2.IsSupported && Ssse3.IsSupported))
                Assert.DoesNotContain(type, types);
            else Assert.Contains(type, types);
        }
    }

    [Fact]
    public void AllStoredHelperAndInactiveSearchLayoutsKeepSharedAndDistinctIdentities()
    {
        var (regex, layouts) = PopulateStoredLayouts();
        foreach (var type in HelperTypes) Assert.Contains(type, layouts);
        foreach (var type in new[] { typeof(Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralCandidatePrefilter),
            typeof(Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralRootByteFamilySearch),
            typeof(Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralRareBytePrefilter),
            typeof(Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralOffsetMaskPrefilter),
            typeof(Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralNibbleMaskPrefilter),
            typeof(Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralLeadingUtf8SegmentPrefilter) }) Assert.Contains(type, layouts);
        var expected = InspectFacadeGraph(regex);
        Assert.True(expected.Keys.OfType<string>().Count(static text => text == "qqq") > 1);
        AssertManifest(expected, Collect(regex));
    }

    [Fact]
    public void ClosedHelperRootsCrossTheExactRetainedByteBoundary()
    {
        var (regex, _) = PopulateStoredLayouts();
        var probe = Cache(1024 * 1024); Assert.Same(regex, Add(probe, "key", regex));
        var maximum = probe.PayloadBytes; Assert.True(maximum > 0);
        foreach (var bytes in new[] { maximum - 1, maximum })
        {
            var cache = Cache(bytes); Assert.Same(regex, Add(cache, "key", regex));
            Assert.Equal(bytes == maximum ? 1 : 0, cache.Count);
            Assert.Equal(bytes == maximum ? maximum : 0, cache.PayloadBytes);
            cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes);
            AssertManifest(InspectFacadeGraph(regex), Collect(regex));
        }
    }

    [Fact]
    public void TwoEntriesShareHelperLeavesAndOwnSeparatePhysicalManifests()
    {
        var (regex, _) = PopulateStoredLayouts();
        var graph = InspectFacadeGraph(regex); var cache = Cache(1024 * 1024);
        Assert.Same(regex, Add(cache, "first", regex)); Assert.Same(regex, Add(cache, "second", regex));
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
        cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes); AssertManifest(graph, Collect(regex));
    }

    [Fact]
    public void EveryWorkCutoffRestoresParentLoansAndPreservesSpentWork()
    {
        var regex = new Utf8Regex("(?<a>ab)c(?<b>d)?", RegexOptions.CultureInvariant);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter()) { _ = Collect(regex); work = measured.Budget.WorkSteps; }
        for (var available = 0ul; available <= work; available++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 2 + available, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static _ => new object()); var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Collect(regex));
                if (available < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.Equal(0ul, owner.Budget.Depth); Assert.True(owner.Budget.WorkSteps >= 2);
        }
    }

    [Fact]
    public void ByteDenialAndPartialCancellationPreserveBorrowedHelpers()
    {
        var (regex, _) = PopulateStoredLayouts(); var expected = InspectFacadeGraph(regex);
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, 112, ulong.MaxValue, ulong.MaxValue))
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object()); var checkpoint = owner.Budget.ObjectCheckpoint;
            using (var child = Utf8ConstructionScope.Enter())
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.Throws<Utf8ResourceLimitExceededException>(() => Collect(regex)).ResourceKind);
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
        }
        using var cancellation = new CancellationTokenSource();
        using (var owner = Utf8ConstructionScope.Enter())
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object()); var checkpoint = owner.Budget.ObjectCheckpoint;
            owner.Budget.CancellationToken = cancellation.Token;
            owner.Budget.CheckpointHook = steps => { if (steps >= 30) cancellation.Cancel(); };
            using (var child = Utf8ConstructionScope.Enter()) Assert.Throws<OperationCanceledException>(() => Collect(regex));
            Assert.Equal(112ul, owner.Budget.ReservedBytes); Assert.Same(checkpoint, owner.Budget.ObjectCheckpoint);
            Assert.True(owner.Budget.WorkSteps >= 30);
        }
        AssertManifest(expected, Collect(regex));
    }

    [Fact]
    public void PackedHelperConstructionOwnsItsFullObjectStateBeforePublication()
    {
        var regex = new Utf8Regex("夏洛克·福尔摩斯|约翰华生|阿德勒|雷斯垂德|莫里亚蒂教授", RegexOptions.CultureInvariant);
        var program = Assert.IsType<Utf8RegexProgram>(Field(regex, "_program"));
        using var owner = Utf8ConstructionScope.Enter();
        var created = Utf8PackedNibbleLiteralFamilyCountStrategy.TryCreate(program.PreparedRegex,
            regex.Options, regex.MatchTimeout, out var strategy);
        Assert.Equal(Sse2.IsSupported && Ssse3.IsSupported, created);
        if (!created) { Assert.Null(strategy); return; }
        Assert.NotNull(strategy);
        var loan = owner.Budget.ObjectCheckpoint; Assert.NotNull(loan); Assert.Same(strategy, loan.Value);
        Assert.Equal(Utf8ConstructionBudget.ObjectBytes<Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralCandidatePrefilter>() +
            Utf8ConstructionBudget.ObjectBytes<(object, Utf8ConstructionBudget.ObjectAllocation?, ulong)>(), loan.Bytes);
    }

    private static readonly Type[] HelperTypes = [typeof(Utf8InvariantCyrillicLiteralCountStrategy),
        typeof(Utf8PackedNibbleLiteralFamilyCountStrategy), typeof(Utf8LiteralFamilyCaptureMap),
        typeof(Utf8GroupedLiteralSpanTable), typeof(Utf8AlternationBranchSpanMap)];

    private static (Utf8Regex Regex, HashSet<Type> Layouts) PopulateStoredLayouts()
    {
        // Storage sentinel only: it never runs matching or claims construction admission.
        var regex = new Utf8Regex("a", RegexOptions.CultureInvariant);
        var values = new Dictionary<Type, object>(); var layouts = new HashSet<Type>();
        var referenceTest = typeof(Utf8FacadeRetainedGraphTests).GetMethod(nameof(ContainsReferences), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(referenceTest);
        foreach (var name in new[] { "_invariantCyrillicLiteralCountStrategy", "_packedNibbleLiteralFamilyCountStrategy",
            "_literalFamilyCaptureMap", "_groupedLiteralSpanTable", "_alternationBranchSpanMap", "_caretAnchoredGroupedLiteralUtf8" })
        {
            var field = typeof(Utf8Regex).GetField(name, Instance); Assert.NotNull(field);
            field.SetValue(regex, Build(field.FieldType));
        }
        return (regex, layouts);

        object Build(Type type)
        {
            if (type == typeof(string)) return new string('q', 3);
            if (values.TryGetValue(type, out var shared)) return shared;
            if (type.IsArray)
            {
                var elementType = type.GetElementType(); Assert.NotNull(elementType);
                var array = Array.CreateInstance(elementType, 2); values.Add(type, array);
                for (var i = 0; i < array.Length; i++) array.SetValue(Build(elementType), i);
                return array;
            }
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType is not null) return Build(nullableType);
            if (!Assert.IsType<bool>(referenceTest.MakeGenericMethod(type).Invoke(null, [])))
            { var primitive = Activator.CreateInstance(type); Assert.NotNull(primitive); return primitive; }
            Assert.Equal(typeof(Utf8Regex).Assembly, type.Assembly);
            var result = RuntimeHelpers.GetUninitializedObject(type); values.Add(type, result); layouts.Add(type);
            foreach (var field in type.GetFields(Instance))
                if (Assert.IsType<bool>(referenceTest.MakeGenericMethod(field.FieldType).Invoke(null, [])))
                    field.SetValue(result, Build(field.FieldType));
            return result;
        }
    }

    private static bool ContainsReferences<T>() => RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    private static Utf8RetainedPayloadExtent[] Collect(Utf8Regex regex)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8RetainedPayloadBuilder<Utf8ConstructionTextNoControl>.Create(64 * 1024 * 1024, default);
        var program = Assert.IsType<Utf8RegexProgram>(Field(regex, "_program"));
        storage.Add(program, Utf8RegexProgram.ObjectBytes); regex.CollectFacadeHelpers(storage);
        Assert.False(storage.Exceeded); return storage.Finish();
    }

    private static void AssertManifest(Dictionary<object, ulong> expected, Utf8RetainedPayloadExtent[] manifest)
    {
        Assert.Equal(expected.Count, manifest.Length);
        foreach (var extent in manifest)
        { Assert.True(expected.TryGetValue(extent.Value, out var bytes)); Assert.Equal(bytes, extent.Bytes); }
        Assert.Equal(manifest.Length, manifest.Select(static extent => extent.Value).Distinct(ReferenceEqualityComparer.Instance).Count());
    }

    private static Utf8BoundedPreparationCache<string, Utf8Regex> Cache(ulong maximum)
        => Utf8BoundedPreparationCache<string, Utf8Regex>.CreateRootAndDescendantRetained(8, StringComparer.Ordinal, 64 * 1024, maximum);
    private static Utf8Regex Add(Utf8BoundedPreparationCache<string, Utf8Regex> cache, string key, Utf8Regex regex)
        => cache.GetOrAdd(key, regex, static (_, value) => value, default(Utf8ConstructionTextNoControl), default(HelperStorage));
    private readonly struct HelperStorage : IUtf8RetainedStorage<string, Utf8Regex>
    {
        public bool IsCertified => true;
        // Only the declared helper roots are closed; this is not a whole-regex certificate.
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.RootsAndBoundedDescendants;
        public void CollectKey<TControl>(string key, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl => storage.Text(key);
        public void CollectValue<TControl>(Utf8Regex value, Utf8RetainedPayloadBuilder<TControl> storage)
            where TControl : struct, IUtf8ConstructionTextControl
        {
            var program = Assert.IsType<Utf8RegexProgram>(Field(value, "_program"));
            storage.Add(program, Utf8RegexProgram.ObjectBytes); value.CollectFacadeHelpers(storage);
        }
    }

    internal static Dictionary<object, ulong> InspectFacadeGraph(Utf8Regex regex)
    {
        var seen = new Dictionary<object, ulong>(ReferenceEqualityComparer.Instance);
        var program = Assert.IsType<Utf8RegexProgram>(Field(regex, "_program"));
        Assert.Equal(6, program.GetType().GetFields(Instance).Length);
        seen.Add(program, StateBytes(program.GetType(), "ConstructionState"));
        var pending = new Queue<object>();
        foreach (var name in new[] { "_invariantCyrillicLiteralCountStrategy", "_packedNibbleLiteralFamilyCountStrategy",
            "_literalFamilyCaptureMap", "_groupedLiteralSpanTable", "_alternationBranchSpanMap", "_caretAnchoredGroupedLiteralUtf8" })
            if (Field(regex, name) is { } helper) pending.Enqueue(helper);
        Walk();
        return seen;

        void Walk()
        {
            while (pending.TryDequeue(out var value))
            {
                var type = value.GetType();
                if (type.IsPrimitive || type.IsEnum || seen.ContainsKey(value)) continue;
                if (value is string text)
                { if (text.Length > 0) seen.Add(text, Utf8ConstructionBudget.StringBytes(text.Length)); continue; }
                if (value is Array array)
                {
                    if (array.Length == 0) continue;
                    seen.Add(array, ArrayBytes(array));
                    foreach (var child in array) if (child is not null) pending.Enqueue(child);
                    continue;
                }
                var fields = type.GetFields(Instance);
                if (!type.IsValueType)
                {
                    var (count, bytes) = value switch
                    {
                        Utf8InvariantCyrillicLiteralCountStrategy => (5, StateBytes(type, "ConstructionState")),
                        Utf8PackedNibbleLiteralFamilyCountStrategy => (1, Utf8ConstructionBudget.ObjectBytes<
                            Lokad.Utf8Regex.Internal.Search.PreparedMultiLiteralCandidatePrefilter>()),
                        Utf8LiteralFamilyCaptureMap => (2, Utf8ConstructionBudget.ObjectBytes<(int[], int[][])>()),
                        Utf8GroupedLiteralSpanTable => (8, Utf8ConstructionBudget.ObjectBytes<(int, int[], int[], int[], bool, int, int[], int[])>()),
                        Utf8AlternationBranchSpanMap => (5, Utf8ConstructionBudget.ObjectBytes<(int[], string[], int[][], int[][], int[][])>()),
                        Lokad.Utf8Regex.Internal.Search.PreparedByteSet => (1, Utf8ConstructionBudget.ObjectBytes<
                            Lokad.Utf8Regex.Internal.Search.PreparedByteSetState>()),
                        _ => throw new InvalidOperationException("Unqualified facade helper reference: " + type.FullName),
                    };
                    Assert.Equal(count, fields.Length); Assert.All(fields, static field => Assert.True(field.IsInitOnly));
                    seen.Add(value, bytes);
                }
                foreach (var field in fields)
                {
                    if (field.FieldType.IsPrimitive || field.FieldType.IsEnum) continue;
                    if (field.GetValue(value) is { } child) pending.Enqueue(child);
                }
            }
        }
    }

    private static ulong StateBytes(Type type, string name)
    {
        var state = type.GetNestedType(name, BindingFlags.NonPublic); Assert.NotNull(state);
        var method = typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ObjectBytes), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ulong>(method.MakeGenericMethod(state).Invoke(null, []));
    }

    private static ulong ArrayBytes(Array value)
    {
        var method = typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ArrayBytes), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ulong>(method.MakeGenericMethod(value.GetType().GetElementType()).Invoke(null, [value.Length]));
    }

    private static object? Field(object value, string name)
    {
        var field = value.GetType().GetField(name, Instance); Assert.NotNull(field);
        return field.GetValue(value);
    }

    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
}
