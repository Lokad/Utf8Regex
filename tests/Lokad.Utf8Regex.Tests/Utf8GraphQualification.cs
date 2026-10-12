using System.Reflection;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

// Independent qualification only. Shipped cache admission uses a numeric program
// certificate; these physical/cycle/alias models are never a production strategy.
internal static class Utf8GraphQualification
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    internal static void Collect<TControl>(object? root, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var pending = new Queue<object>();
        if (root is not null) pending.Enqueue(root);
        while (pending.TryDequeue(out var value))
        {
            storage.Check(); var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) continue;
            if (ReferenceEquals(value, StringComparer.Ordinal) || ReferenceEquals(value, EqualityComparer<string>.Default) || ReferenceEquals(value, EqualityComparer<int>.Default)) continue;
            if (value is string text) { storage.Text(text); continue; }
            if (value is Array array)
            {
                var element = type.GetElementType()!;
                var bytes = (ulong)typeof(Utf8ConstructionBudget).GetMethod("ArrayBytes", Static)!.MakeGenericMethod(element).Invoke(null, [array.Length])!;
                if (!storage.TryAdd(array, bytes)) continue;
                foreach (var child in array) if (child is not null) pending.Enqueue(child);
                continue;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Utf8EmittedDelegate<>))
            {
                var callback = (Delegate?)type.GetProperty("Callback")!.GetValue(value);
                if (callback is null) continue;
                var receipt = (ulong)type.GetProperty("ManagedArrayBytes")!.GetValue(value)!;
                if (receipt == 0) throw new InvalidOperationException("Missing managed delegate copy receipt.");
                CollectDelegate(callback, storage, receipt); continue;
            }
            if (value is Delegate callbackValue) { CollectDelegate(callbackValue, storage); continue; }
            if (value is Utf8SliceProgram scalar && scalar.BackreferenceLowerings.Length != 0)
                throw new InvalidOperationException("Profile scalar delegates require their own qualification.");
            if (!type.IsValueType)
            {
                if (type.Assembly != typeof(Utf8Regex).Assembly) throw new InvalidOperationException("Unknown owned graph child: " + type);
                if (!storage.TryAdd(value, MetadataBytes(type))) continue;
            }
            foreach (var field in AllFields(type))
                if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.GetValue(value) is { } child) pending.Enqueue(child);
        }
    }

    internal static void CollectDelegate<TControl>(Delegate? callback, Utf8GraphInspection<TControl> storage, ulong bytes = 0)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (callback is null) return;
        if (!callback.HasSingleTarget || callback.Target is not null) throw new InvalidOperationException("A delegate qualification requires one static target.");
        storage.Add(callback, checked(80UL + bytes));
    }

    internal static ulong MetadataBytes(Type type)
    {
        if (type == typeof(Utf8Regex)) return Utf8Regex.CachedObjectBytes;
        if (type.GetProperty("ObjectBytes", Static) is { } modeled) return (ulong)modeled.GetValue(null)!;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Utf8ConstructionReadOnlyMap<,>)) return 64;
        if (typeof(Utf8StructuralVerifierRuntime).IsAssignableFrom(type)) return Utf8ConstructionBudget.ObjectBytes<Utf8StructuralVerifierRuntime.ConstructionState>();
        if (typeof(Utf8StructuralLinearRuntime).IsAssignableFrom(type)) return Utf8ConstructionBudget.ObjectBytes<Lokad.Utf8Regex.Internal.Planning.Utf8PreparedRegex>();
        var state = type.GetNestedType("ConstructionState", BindingFlags.NonPublic);
        if (state is not null) return StateBytes(state);
        return type.Name switch
        {
            nameof(Utf8ExactLiteralCompiledEngineRuntime) or nameof(Utf8LiteralFamilyCompiledEngineRuntime) => Utf8ConstructionBudget.ObjectBytes<Utf8LiteralCompiledEngineRuntime>(),
            nameof(Utf8EmittedWholeMatcher) => Utf8ConstructionBudget.ObjectBytes<Utf8EmittedDelegate<Utf8EmittedWholeMatcher.MatchDelegate>>(),
            nameof(Utf8EmittedDeterministicMatcher) => Utf8ConstructionBudget.ObjectBytes<(Utf8EmittedDelegate<Utf8EmittedDeterministicMatcher.FindNextDelegate>, byte[], AsciiCharClass[], bool)>(),
            nameof(Utf8EmittedSearchGuidedFallback) => Utf8ConstructionBudget.ObjectBytes<(Lokad.Utf8Regex.Internal.Planning.Utf8SearchPlan, Utf8VerifierRuntime, Utf8EmittedDelegate<Utf8EmittedSearchGuidedFallback.IsMatchDelegate>, Utf8EmittedDelegate<Utf8EmittedSearchGuidedFallback.CountDelegate>)>(),
            "CompositeTrailingLiteralSearch" => Utf8ConstructionBudget.ObjectBytes<(PreparedSubstringSearch, int, int)>(),
            nameof(PreparedByteSet) => Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>(),
            nameof(PreparedAsciiIgnoreCaseLiteralSetSearchData) => StateBytes(typeof(Utf8Regex).Assembly.GetType("Lokad.Utf8Regex.Internal.Search.PreparedAsciiIgnoreCaseLiteralSetSearchDataState", true)!),
            nameof(Utf8PackedNibbleLiteralFamilyCountStrategy) => Utf8ConstructionBudget.ObjectBytes<PreparedMultiLiteralCandidatePrefilter>(),
            nameof(Utf8LiteralFamilyCaptureMap) => Utf8ConstructionBudget.ObjectBytes<(int[], int[][])>(),
            nameof(Utf8GroupedLiteralSpanTable) => Utf8ConstructionBudget.ObjectBytes<(int, int[], int[], int[], bool, int, int[], int[])>(),
            nameof(Utf8AlternationBranchSpanMap) => Utf8ConstructionBudget.ObjectBytes<(int[], string[], int[][], int[][], int[][])>(),
            nameof(Utf8SliceProgram) => Utf8SliceProgram.ObjectBytes,
            nameof(Utf8SliceAssertion) => Utf8SliceAssertion.ObjectBytes,
            nameof(Utf8SliceCategoryTable) => Utf8SliceCategoryTable.ObjectBytes,
            nameof(Utf8SliceClassProgram) => 48,
            nameof(Utf8SliceNode.CategorySetMember) => 48,
            nameof(Utf8CaptureCatalog) => 80,
            nameof(Utf8ScalarClass) => Utf8ScalarClass.ObjectBytes,
            _ => throw new InvalidOperationException("Unqualified physical graph child: " + type)
        };
    }
    private static ulong StateBytes(Type type) => (ulong)typeof(Utf8ConstructionBudget).GetMethod("ObjectBytes", Static)!.MakeGenericMethod(type).Invoke(null, null)!;
    private static IEnumerable<FieldInfo> AllFields(Type type)
    {
        for (var current = type; current is not null; current = current.BaseType)
            foreach (var field in current.GetFields(Fields)) yield return field;
    }

    internal static void CollectRetainedStorage<TControl>(this object value, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => Collect(value, storage);
    internal static void CollectStorage<TControl>(this object value, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => Collect(value, storage);
    internal static void CollectCacheRoots<TControl>(this Utf8Regex value, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Add(value, Utf8Regex.CachedObjectBytes); storage.Text(value.Pattern);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var child = (Utf8ReplacementPlanCache)typeof(Utf8Regex).GetField("_replacementCache", flags)!.GetValue(value)!;
        storage.Add(child, child.MaximumRetainedBytes);
        Collect(typeof(Utf8Regex).GetField("_ownedSliceProgram", flags)!.GetValue(value), storage);
        Collect(typeof(Utf8Regex).GetField("_program", flags)!.GetValue(value), storage);
        Collect(typeof(Utf8Regex).GetField("_fusedAsciiBoundaryLiteralFamilyCounter", flags)!.GetValue(value), storage);
        CollectFacadeHelpers(value, storage);
    }
    internal static void CollectFacadeHelpers<TControl>(this Utf8Regex value, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        foreach (var name in new[] { "_invariantCyrillicLiteralCountStrategy", "_packedNibbleLiteralFamilyCountStrategy", "_literalFamilyCaptureMap", "_groupedLiteralSpanTable", "_caretAnchoredGroupedLiteralUtf8", "_alternationBranchSpanMap" })
            Collect(typeof(Utf8Regex).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(value), storage);
    }
}

// Compatibility of the independent qualification call sites, with no per-type
// production collectors or hooks. Actual field/schema models remain separate.
internal static class Utf8ExecutionRetainedStorage
{
    internal static void Collect<TControl>(Utf8ExecutionTree? tree, Utf8ExecutionProgram? program, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl { Utf8GraphQualification.Collect(tree, storage); Utf8GraphQualification.Collect(program, storage); }
}
internal static class Utf8ScalarProgramRetainedStorage
{
    internal static void Collect<TControl>(Utf8SliceProgram program, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => Utf8GraphQualification.Collect(program, storage);
}
internal static class Utf8PreparedRetainedStorage
{
    internal static void Collect<TControl>(Lokad.Utf8Regex.Internal.Planning.Utf8PreparedRegex value, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => Utf8GraphQualification.Collect(value, storage);
}
internal static class Utf8RuntimeRetainedStorage
{
    internal static void CollectDelegate<TControl>(Delegate? callback, Utf8GraphInspection<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => Utf8GraphQualification.CollectDelegate(callback, storage);
}
