using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RegexCacheCollection.Name)]
public sealed class Utf8PreparedRetainedGraphTests
{
    [Theory]
    [InlineData("(?<n>ab)[a-c]", false)] [InlineData("(?<n>ab)[a-c]", true)]
    [InlineData("(?i:abcdefghij|anotherword|sometimes)", false)] [InlineData("(?i:abcdefghij|anotherword|sometimes)", true)]
    [InlineData("(?<n>😀)\\k<n>", false)] [InlineData("(?<n>😀)\\k<n>", true)]
    [InlineData("(?:(?:alpha|beta|gamma)\\s+)[A-Z][a-z]+", false)]
    [InlineData("(?:(?:alpha|beta|gamma)\\s+)[A-Z][a-z]+", true)]
    public void GlobalChargeCoversEveryPreparedReferent(string pattern, bool compiled)
    {
        Utf8RegexCache.ResetForTests();
        try
        {
            var regex = Utf8RegexCache.GetOrAdd(pattern, RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None));
            var prepared = Utf8ExecutionRetainedGraphTests.Prepared(regex);
            var expected = InspectPreparedGraph(prepared);
            Assert.True(expected.ContainsKey(prepared));
            var cacheField = typeof(Utf8RegexCache).GetField("s_cache", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.NotNull(cacheField);
            var cache = cacheField.GetValue(null); Assert.NotNull(cache);
            var entry = Field(cache, "_first"); Assert.NotNull(entry);
            var manifest = Utf8GlobalCacheDescendantTests.InspectEntryPayloads(entry);
            foreach (var pair in expected)
                Assert.Contains(manifest, extent => ReferenceEquals(extent.Value, pair.Key) && extent.Bytes == pair.Value);
            Assert.Same(regex, Utf8RegexCache.GetOrAdd(pattern, regex.Options));
        }
        finally { Utf8RegexCache.ResetForTests(); }
    }

    [Theory]
    [InlineData("abc", RegexOptions.None)] [InlineData("abc", RegexOptions.Compiled)]
    [InlineData("😀𐐨", RegexOptions.None)] [InlineData("😀𐐨", RegexOptions.Compiled)]
    [InlineData("ab|abc|ac|ace|baz", RegexOptions.None)] [InlineData("ab|abc|ac|ace|baz", RegexOptions.Compiled)]
    [InlineData("alpha|beta|gamma|delta|omega|theta|sigma|lambda", RegexOptions.None)]
    [InlineData("alpha|beta|gamma|delta|omega|theta|sigma|lambda", RegexOptions.Compiled)]
    [InlineData("(?i:ALPHABETICAL|LOGARITHMIC|SUPPLEMENTARY)", RegexOptions.None)]
    [InlineData("(?i:ALPHABETICAL|LOGARITHMIC|SUPPLEMENTARY)", RegexOptions.Compiled)]
    [InlineData("(?m:^(?:using|import)\\s+[a-z.]+)", RegexOptions.None)]
    [InlineData("(?m:^(?:using|import)\\s+[a-z.]+)", RegexOptions.Compiled)]
    [InlineData("begin(?:.|\\n)*?end", RegexOptions.None)] [InlineData("begin(?:.|\\n)*?end", RegexOptions.Compiled)]
    [InlineData("(?<n>😀)[\\p{L}-[AE]]", RegexOptions.None)] [InlineData("(?<n>😀)[\\p{L}-[AE]]", RegexOptions.Compiled)]
    [InlineData("(?<900>ab)(?<900>c)[0-9]{2}", RegexOptions.None)]
    [InlineData("(?<900>ab)(?<900>c)[0-9]{2}", RegexOptions.Compiled)]
    [InlineData("(?<=\\p{L})(?=(?<n>.))(?<n>.)", RegexOptions.None)]
    [InlineData("(?<=\\p{L})(?=(?<n>.))(?<n>.)", RegexOptions.Compiled)]
    [InlineData("(?<n>.)+", RegexOptions.RightToLeft)] [InlineData("(?<n>.)+", RegexOptions.NonBacktracking)]
    [InlineData("^[-+]?[0-9]+(?:\\.[0-9]+)?$", RegexOptions.None)]
    [InlineData("^[-+]?[0-9]+(?:\\.[0-9]+)?$", RegexOptions.Compiled)]
    public void TypedCollectorMatchesTheCompleteActualPreparedGraph(string pattern, RegexOptions options)
    {
        var prepared = Utf8ExecutionRetainedGraphTests.Prepared(new Utf8Regex(pattern, options | RegexOptions.CultureInvariant));
        AssertManifest(InspectPreparedGraph(prepared), Collect(prepared));
    }

    [Fact]
    public void EveryStoredReferenceBearingLayoutIncludesInactiveAndSharedPayloads()
    {
        var (prepared, layouts) = PopulateStoredLayouts();
        // The construction-only guard descriptor is no longer a retained layout.
        Assert.Equal(54, layouts);
        var expected = InspectPreparedGraph(prepared);
        Assert.Equal(4, expected.Keys.Count(static value => value is Utf8PreparedRegex or Utf8OperationMatchCursorPlans or
            PreparedByteSet or PreparedAsciiIgnoreCaseLiteralSetSearchData));
        Assert.True(expected.Keys.OfType<string>().Count(static value => value == "qqq") > 1);
        AssertManifest(expected, Collect(prepared));
    }

    [Fact]
    public void CompletePreparedGraphCrossesTheExactRetainedByteBoundary()
    {
        var prepared = Utf8FrontEnd.Compile("(?i:ALPHABETICAL|LOGARITHMIC|SUPPLEMENTARY)", RegexOptions.CultureInvariant);
        var probe = Cache(1024 * 1024);
        Assert.Same(prepared, Add(probe, "key", prepared));
        var maximum = probe.PayloadBytes; Assert.True(maximum > 0);
        foreach (var bytes in new[] { maximum - 1, maximum })
        {
            var cache = Cache(bytes);
            Assert.Same(prepared, Add(cache, "key", prepared));
            Assert.Equal(bytes == maximum ? 1 : 0, cache.Count);
            Assert.Equal(bytes == maximum ? maximum : 0, cache.PayloadBytes);
            cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes);
            AssertManifest(InspectPreparedGraph(prepared), Collect(prepared));
        }
    }

    [Fact]
    public void TwoEntriesConservativelyChargeTheirSharedGraphTwice()
    {
        var prepared = Utf8FrontEnd.Compile("(?i:ALPHABETICAL|LOGARITHMIC|SUPPLEMENTARY)", RegexOptions.CultureInvariant);
        var graph = InspectPreparedGraph(prepared);
        var cache = Cache(1024 * 1024);
        Assert.Same(prepared, Add(cache, "first", prepared));
        Assert.Same(prepared, Add(cache, "second", prepared));
        Assert.Equal(2, cache.Count);
        var expected = 2 * graph.Values.Aggregate(0UL, static (sum, bytes) => sum + bytes);
        expected += Utf8ConstructionBudget.StringBytes(5) + Utf8ConstructionBudget.StringBytes(6);
        Assert.Equal(expected, cache.PayloadBytes);
        cache.Clear(); Assert.Equal(0ul, cache.PayloadBytes);
        AssertManifest(graph, Collect(prepared));
    }

    [Fact]
    public void EveryWorkCutoffRestoresParentLoansAndPreservesSpentWork()
    {
        var prepared = Utf8FrontEnd.Compile("a", RegexOptions.CultureInvariant);
        ulong work;
        using (var measured = Utf8ConstructionScope.Enter()) { _ = Collect(prepared); work = measured.Budget.WorkSteps; }
        for (var available = 0ul; available <= work; available++)
        {
            using var owner = new Utf8ConstructionScope(ulong.MaxValue, ulong.MaxValue, 2 + available, ulong.MaxValue);
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
            {
                var failure = Record.Exception(() => Collect(prepared));
                if (available < work) Assert.Equal(Utf8ResourceLimitKind.ConstructionWorkSteps, Assert.IsType<Utf8ResourceLimitExceededException>(failure).ResourceKind);
                else Assert.Null(failure);
            }
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.Equal(0ul, owner.Budget.Depth);
            Assert.InRange(owner.Budget.WorkSteps, 2ul, 2 + available);
        }
    }

    [Fact]
    public void ByteDenialAndPartialCancellationPreserveBorrowedPreparedGraphs()
    {
        var prepared = Utf8FrontEnd.Compile("(?i:ALPHABETICAL|LOGARITHMIC|SUPPLEMENTARY)", RegexOptions.CultureInvariant);
        var expected = InspectPreparedGraph(prepared);
        using (var owner = new Utf8ConstructionScope(ulong.MaxValue, 128, ulong.MaxValue, ulong.MaxValue))
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ReservedBytes;
            using (var child = Utf8ConstructionScope.Enter())
                Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, Assert.Throws<Utf8ResourceLimitExceededException>(() => Collect(prepared)).ResourceKind);
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
        }
        using var cancellation = new CancellationTokenSource();
        using (var owner = Utf8ConstructionScope.Enter())
        {
            _ = owner.Budget.AllocateObject(17, static _ => new object());
            var checkpoint = owner.Budget.ReservedBytes;
            owner.Budget.CancellationToken = cancellation.Token;
            owner.Budget.CheckpointHook = steps => { if (steps >= 30) cancellation.Cancel(); };
            using (var child = Utf8ConstructionScope.Enter()) Assert.Throws<OperationCanceledException>(() => Collect(prepared));
            Assert.Equal(128ul, owner.Budget.ReservedBytes); Assert.Equal(checkpoint, owner.Budget.ReservedBytes);
            Assert.True(owner.Budget.WorkSteps >= 30);
        }
        AssertManifest(expected, Collect(prepared));
    }

    private static (Utf8PreparedRegex Prepared, int Layouts) PopulateStoredLayouts()
    {
        var seed = Utf8FrontEnd.Compile("(?<n>ab)[a-c]", RegexOptions.CultureInvariant);
        Assert.NotNull(seed.ExecutionTree); Assert.NotNull(seed.ExecutionProgram); Assert.NotNull(seed.ScalarProgram);
        Assert.NotNull(seed.SemanticRegex.CaptureCatalog);
        var borrowed = new Dictionary<Type, object>
        {
            [typeof(Utf8ExecutionTree)] = seed.ExecutionTree, [typeof(Utf8ExecutionProgram)] = seed.ExecutionProgram,
            [typeof(Utf8SliceProgram)] = seed.ScalarProgram, [typeof(Utf8CaptureCatalog)] = seed.SemanticRegex.CaptureCatalog,
        };
        var values = new Dictionary<Type, object>();
        var layouts = new HashSet<Type>();
        var referenceTest = typeof(Utf8PreparedRetainedGraphTests).GetMethod(nameof(ContainsReferences), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(referenceTest);
        return (Assert.IsType<Utf8PreparedRegex>(Build(typeof(Utf8PreparedRegex))), layouts.Count);

        object Build(Type type)
        {
            if (borrowed.TryGetValue(type, out var leaf)) return leaf;
            if (type == typeof(string)) return new string('q', 3);
            if (values.TryGetValue(type, out var shared)) return shared;
            if (type.IsArray)
            {
                var elementType = type.GetElementType(); Assert.NotNull(elementType);
                var array = Array.CreateInstance(elementType, 1); values.Add(type, array);
                array.SetValue(Build(elementType), 0);
                return array;
            }
            var nullableType = Nullable.GetUnderlyingType(type);
            if (nullableType is not null) return Build(nullableType);
            if (!Assert.IsType<bool>(referenceTest.MakeGenericMethod(type).Invoke(null, [])))
            {
                var primitive = Activator.CreateInstance(type); Assert.NotNull(primitive);
                return primitive;
            }
            Assert.Equal(typeof(Utf8Regex).Assembly, type.Assembly);
            // This sentinel exercises stored layouts, including inactive fields;
            // it is never used for matching or as construction-admission evidence.
            var result = RuntimeHelpers.GetUninitializedObject(type); values.Add(type, result); layouts.Add(type);
            foreach (var field in type.GetFields(Instance))
                if (Assert.IsType<bool>(referenceTest.MakeGenericMethod(field.FieldType).Invoke(null, [])))
                    field.SetValue(result, Build(field.FieldType));
            return result;
        }
    }

    private static bool ContainsReferences<T>() => RuntimeHelpers.IsReferenceOrContainsReferences<T>();

    private static Utf8RetainedPayloadExtent[] Collect(Utf8PreparedRegex prepared)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8GraphInspection<Utf8ConstructionTextNoControl>.Create(64 * 1024 * 1024, default);
        Utf8PreparedRetainedStorage.Collect(prepared, storage);
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

    private static Utf8BoundedPreparationCache<string, Utf8PreparedRegex> Cache(ulong maximum)
        => Utf8BoundedPreparationCache<string, Utf8PreparedRegex>.CreateRetained(8, StringComparer.Ordinal, 64 * 1024, maximum);
    private static Utf8PreparedRegex Add(Utf8BoundedPreparationCache<string, Utf8PreparedRegex> cache, string key, Utf8PreparedRegex prepared)
        => cache.GetOrAdd(key, prepared, static (_, value) => value, default(Utf8ConstructionTextNoControl), default(PreparedStorage));
    private readonly struct PreparedStorage : IUtf8RetainedStorage<string, Utf8PreparedRegex>
    {
        public bool IsCertified => true;
        public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
        public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
        public bool TryGetValueCharge<TControl>(Utf8PreparedRegex value, ulong maximum, TControl control, out ulong charge)
            where TControl : struct, IUtf8ConstructionTextControl
        {
            using var storage = Utf8GraphInspection<TControl>.Create(maximum, control);
            Utf8PreparedRetainedStorage.Collect(value, storage);
            charge = storage.FinishCharge();
            return !storage.Exceeded;
        }
    }

    internal static Dictionary<object, ulong> InspectPreparedGraph(Utf8PreparedRegex prepared)
    {
        var seen = new Dictionary<object, ulong>(ReferenceEqualityComparer.Instance);
        var pending = new Queue<object>(); pending.Enqueue(prepared);
        while (pending.TryDequeue(out var value))
        {
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) continue;
            if (ReferenceEquals(value, StringComparer.Ordinal) || ReferenceEquals(value, EqualityComparer<int>.Default)) continue;
            if (seen.ContainsKey(value)) continue;
            if (value is Utf8SliceProgram body)
            {
                foreach (var pair in Utf8ScalarRetainedGraphTests.InspectScalarGraph(body))
                    if (!seen.TryAdd(pair.Key, pair.Value)) Assert.Equal(seen[pair.Key], pair.Value);
                continue;
            }
            if (value is string text)
            {
                var bytes = Utf8ConstructionBudget.StringBytes(text.Length);
                if (bytes != 0) seen.Add(value, bytes);
                continue;
            }
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
                    Utf8PreparedRegex => (20, StateBytes(type, "ConstructionState")),
                    Utf8OperationMatchCursorPlans => (6, StateBytes(type, "ConstructionState")),
                    PreparedByteSet => (1, Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>()),
                    PreparedAsciiIgnoreCaseLiteralSetSearchData => (5, StateBytes(type.Assembly.GetType(
                        "Lokad.Utf8Regex.Internal.Search.PreparedAsciiIgnoreCaseLiteralSetSearchDataState", true))),
                    Utf8ExecutionTree => (1, 48ul),
                    Utf8ExecutionNode => (8, 80ul),
                    Utf8ExecutionProgram => (1, 48ul),
                    Utf8ScalarClass => (1, 48ul),
                    Utf8CaptureCatalog => (7, 80ul),
                    Utf8ConstructionReadOnlyMap<string, int> => (3, 64ul),
                    Utf8ConstructionReadOnlyMap<int, int> => (3, 64ul),
                    _ => throw new InvalidOperationException("Unqualified prepared reference type: " + type),
                };
                Assert.Equal(count, fields.Length);
                Assert.All(fields, static field => Assert.True(field.IsInitOnly));
                seen.Add(value, bytes);
            }
            foreach (var field in fields)
                if (field.GetValue(value) is { } child) pending.Enqueue(child);
        }
        return seen;
    }

    private static ulong StateBytes(Type type, string stateName)
    {
        var state = type.GetNestedType(stateName, BindingFlags.NonPublic); Assert.NotNull(state);
        Assert.Equal(type.GetFields(Instance).Select(static field => field.FieldType).OrderBy(static t => t.FullName),
            state.GetFields(Instance).Select(static field => field.FieldType).OrderBy(static t => t.FullName));
        return StateBytes(state);
    }

    private static ulong StateBytes(Type? state)
    {
        Assert.NotNull(state);
        var method = typeof(Utf8ConstructionBudget).GetMethod(nameof(Utf8ConstructionBudget.ObjectBytes), BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        return Assert.IsType<ulong>(method.MakeGenericMethod(state).Invoke(null, []));
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
