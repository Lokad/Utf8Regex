using System.Reflection;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

// One independent test-only bound on representative compiled program storage.
// Shipped cache admission uses conservative numeric charges.
internal static class Utf8GraphQualification
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    // Independent test-only lower bound for covered immutable program storage.
    // No construction budget, cache publication, collector transaction or schema census.
    internal static ulong Measure(object root)
    {
        var pending = new Stack<object>();
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        pending.Push(root);
        ulong bytes = 0;
        while (pending.TryPop(out var value))
        {
            var type = value.GetType();
            if (type.IsPrimitive || type.IsEnum) continue;
            if (ReferenceEquals(value, StringComparer.Ordinal) || ReferenceEquals(value, EqualityComparer<string>.Default) ||
                ReferenceEquals(value, EqualityComparer<int>.Default)) continue;
            if (!type.IsValueType && !seen.Add(value)) continue;
            if (value is string text) { bytes += Utf8ConstructionBudget.StringBytes(text.Length); continue; }
            if (value is Array array)
            {
                bytes += (ulong)typeof(Utf8ConstructionBudget).GetMethod("ArrayBytes", Static)!
                    .MakeGenericMethod(type.GetElementType()!).Invoke(null, [array.Length])!;
                foreach (var child in array) if (child is not null) pending.Push(child);
                continue;
            }
            if (value is Utf8ReplacementPlanCache replacement)
            {
                bytes += replacement.MaximumRetainedBytes;
                continue;
            }
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Utf8EmittedDelegate<>))
            {
                var callback = (Delegate?)type.GetProperty("Callback")!.GetValue(value);
                var copies = (ulong)type.GetProperty("ManagedArrayBytes")!.GetValue(value)!;
                if (callback is not null && seen.Add(callback)) bytes += checked(80UL + copies);
                continue;
            }
            if (value is Delegate) { bytes += 80; continue; } // Opaque CLR/JIT metadata is outside the declared model.
            if (!type.IsValueType)
            {
                Assert.Equal(typeof(Utf8Regex).Assembly, type.Assembly);
                bytes += MetadataBytes(type);
            }
            foreach (var field in AllFields(type))
                if (!field.FieldType.IsPrimitive && !field.FieldType.IsEnum && field.GetValue(value) is { } child)
                    pending.Push(child);
        }
        return bytes;
    }

    internal static ulong MetadataBytes(Type type)
    {
        if (type == typeof(Utf8Regex)) return Utf8Regex.CachedObjectBytes;
        // These sizes belong to this physical-graph oracle. Production admits
        // both objects at acquisition without test-only ObjectBytes properties.
        if (type == typeof(Utf8ExecutionTree)) return Utf8ConstructionBudget.ObjectBytes<Utf8ExecutionNode>();
        if (type == typeof(Utf8ExecutionNode)) return Utf8ConstructionBudget.ObjectBytes<
            (Utf8ExecutionNodeKind, System.Text.RegularExpressions.RegexOptions, IReadOnlyList<Utf8ExecutionNode>, object, char, int, int, int)>();
        if (type.GetProperty("ObjectBytes", Static) is { } modeled) return (ulong)modeled.GetValue(null)!;
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Utf8ConstructionReadOnlyMap<,>)) return 64;
        if (typeof(Utf8StructuralVerifierRuntime).IsAssignableFrom(type)) return Utf8StructuralVerifierRuntime.ObjectBytes;
        if (typeof(Utf8StructuralLinearRuntime).IsAssignableFrom(type)) return Utf8ConstructionBudget.ObjectBytes<Lokad.Utf8Regex.Internal.Planning.Utf8PreparedRegex>();
        var state = type.GetNestedType("ConstructionState", BindingFlags.NonPublic);
        if (state is not null) return StateBytes(state);
        return type.Name switch
        {
            nameof(Utf8StructuralFamilyCompiledEngineRuntime) or nameof(Utf8SimplePatternCompiledEngineRuntime) or nameof(Utf8StructuralLinearAutomatonCompiledEngineRuntime) or nameof(Utf8FallbackRegexCompiledEngineRuntime)
                => Utf8CompiledRuntimeFactory.FixedRuntimeMetadataBytes,
            nameof(Utf8EmittedWholeMatcher) => Utf8ConstructionBudget.ObjectBytes<Utf8EmittedDelegate<Utf8EmittedWholeMatcher.MatchDelegate>>(),
            nameof(Utf8EmittedDeterministicMatcher) => Utf8ConstructionBudget.ObjectBytes<(Utf8EmittedDelegate<Utf8EmittedDeterministicMatcher.FindNextDelegate>, byte[], AsciiCharClass[], bool)>(),
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

}
